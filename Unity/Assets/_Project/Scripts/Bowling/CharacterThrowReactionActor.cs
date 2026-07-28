using System.Collections;
using UnityEngine;

namespace WeeSpurts.Bowling
{
    /// <summary>
    /// Rigged "Body English" reaction: the thrower character celebrates,
    /// slumps, or faceplants based on how the release went. Same contract as
    /// the greybox <see cref="CapsuleThrowReactionActor"/> it replaces — purely
    /// cosmetic, reads LaunchParameters and never writes them — but it drives
    /// Animator triggers instead of transform tweens.
    ///
    /// Deterministic, as IThrowReactionActor requires: which clip plays is a
    /// pure function of TimingError01/IsBackwardFumble, no live randomness, so
    /// every client picks the SAME reaction for the same throw.
    ///
    /// SETUP: sits on the PlayerCharacter prefab root, above the imported model
    /// (the Animator is found in children). CharacterSetupTool builds that
    /// prefab; GreyboxSceneBuilder drops it in and wires it into
    /// BowlingPresentation. The Animator needs the PlayerCharacter controller
    /// that CharacterSetupTool generates — its trigger names must match the
    /// constants below.
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterThrowReactionActor : MonoBehaviour, IThrowReactionActor
    {
        // Animator parameter names. These are the contract between this script
        // and the generated controller — CharacterSetupTool declares the exact
        // same strings, so if you rename one, rename it there and rebuild.
        public const string ExcitedTrigger = "Excited";
        public const string DefeatTrigger = "Defeat";
        public const string FallFlatTrigger = "FallFlat";
        public const string ThrowTrigger = "Throw";
        public const string DrunkBool = "Drunk";
        public const string SpeedFloat = "Speed";

        /// <summary>
        /// Safety net for <see cref="PlayOutcomeAfterThrow"/>'s poll loop — not
        /// the throw's real duration (that comes from the Animator itself once
        /// the Throw state is actually current). Purely an upper bound so a
        /// broken Animator wiring (wrong controller, missing Throw state) can't
        /// wedge the coroutine forever waiting for a state that will never
        /// become current.
        /// </summary>
        private const float MaxThrowStateWaitSeconds = 3f;

        [Tooltip("Animator driving the character. Left empty, it's found on this object or its children at Awake.")]
        [SerializeField] private Animator animator;

        [Tooltip("How close to perfect the release must be (absolute TimingError01, 0 = perfect) to play the celebration instead of the dejected clip. Above this, the thrower is disappointed in themselves.")]
        [Range(0f, 1f)]
        [SerializeField] private float goodThrowThreshold = 0.35f;

        // Integer hashes are the cheap way to talk to an Animator — Unity hashes
        // the string on every SetTrigger(string) call otherwise.
        private int _excitedHash;
        private int _defeatHash;
        private int _fallFlatHash;
        private int _throwHash;
        private int _drunkHash;
        private int _speedHash;

        // The coroutine started by PlayReaction, tracked so a rapid re-throw
        // (back-to-back turns, or the sandbox F-key frame reset) can cancel a
        // still-running wait instead of leaving two queued outcome triggers to
        // fight each other.
        private Coroutine _reactionRoutine;

        private void Awake()
        {
            // GetComponentInChildren also checks THIS object, so it covers both
            // the wrapper-root layout the prefab uses and a bare model root.
            if (animator == null) animator = GetComponentInChildren<Animator>();

            _excitedHash = Animator.StringToHash(ExcitedTrigger);
            _defeatHash = Animator.StringToHash(DefeatTrigger);
            _fallFlatHash = Animator.StringToHash(FallFlatTrigger);
            _throwHash = Animator.StringToHash(ThrowTrigger);
            _drunkHash = Animator.StringToHash(DrunkBool);
            _speedHash = Animator.StringToHash(SpeedFloat);
        }

        public void PlayReaction(LaunchParameters p)
        {
            if (animator == null) return;

            // Reset all four triggers before setting one. A trigger that was
            // set but never consumed (e.g. two throws resolved back to back,
            // or the sandbox frame-reset key) stays queued in the Animator and
            // would fire a stale reaction — or a stale second Throw — on the
            // NEXT throw.
            animator.ResetTrigger(_excitedHash);
            animator.ResetTrigger(_defeatHash);
            animator.ResetTrigger(_fallFlatHash);
            animator.ResetTrigger(_throwHash);

            // SEQUENCING: play the throw motion FIRST and hold the outcome
            // reaction (Excited/Defeat/FallFlat) until it's actually finished,
            // instead of firing both triggers in the same frame. AnyState
            // transitions (see CharacterSetupTool.AddReaction) are re-evaluated
            // every single frame regardless of the CURRENT state — so setting
            // an outcome trigger the same frame as ThrowTrigger would let the
            // outcome's AnyState transition win almost immediately, cutting the
            // throw motion off before it ever got to play. Sequencing the two
            // is what lets the throw animation actually finish before the
            // celebrate/dejected reaction takes over.
            PlayThrow();

            if (_reactionRoutine != null) StopCoroutine(_reactionRoutine);
            _reactionRoutine = StartCoroutine(PlayOutcomeAfterThrow(p));
        }

        /// <summary>
        /// Fires the Throw trigger on its own. Small and separately testable/
        /// callable per Tony's instruction — PlayReaction is the only caller
        /// today, but nothing stops another system from triggering just the
        /// throw motion later.
        /// </summary>
        public void PlayThrow()
        {
            if (animator != null) animator.SetTrigger(_throwHash);
        }

        /// <summary>
        /// Waits for the Throw state to actually be playing, then for its full
        /// clip length, before firing the outcome trigger — see the sequencing
        /// comment in PlayReaction for why this delay exists.
        /// </summary>
        private IEnumerator PlayOutcomeAfterThrow(LaunchParameters p)
        {
            // The AnyState -> Throw transition cross-fades in over its own
            // duration (CharacterSetupTool.AddReaction's 0.1s), so
            // GetCurrentAnimatorStateInfo(0) keeps reporting whatever was
            // playing BEFORE the trigger fired until that blend completes.
            // Poll until Throw is genuinely the current state rather than
            // trusting the very next frame, with a safety timeout in case the
            // Animator is wired wrong and Throw never becomes current.
            float waited = 0f;
            while (!animator.GetCurrentAnimatorStateInfo(0).IsName(ThrowTrigger) &&
                   waited < MaxThrowStateWaitSeconds)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            AnimatorStateInfo throwState = animator.GetCurrentAnimatorStateInfo(0);
            // normalizedTime is how far through the clip we already are (0 at
            // the state's start, 1 at its end) — multiplying the REMAINDER by
            // length means this waits only for whatever's actually left of the
            // clip, so it doesn't double-wait if the poll above ate a frame or
            // two before Throw became current.
            float remaining = throwState.length * Mathf.Max(0f, 1f - throwState.normalizedTime);
            if (remaining > 0f) yield return new WaitForSeconds(remaining);

            animator.ResetTrigger(_excitedHash);
            animator.ResetTrigger(_defeatHash);
            animator.ResetTrigger(_fallFlatHash);
            animator.SetTrigger(ChooseReactionHash(p));

            _reactionRoutine = null;
        }

        /// <summary>
        /// The whole reaction decision, isolated so it stays easy to read and
        /// to reason about determinism. Backward fumble is a hand-placed gag
        /// (see LaunchParameters.IsBackwardFumble) so it wins outright rather
        /// than being folded into the timing curve.
        /// </summary>
        private int ChooseReactionHash(LaunchParameters p)
        {
            if (p.IsBackwardFumble) return _fallFlatHash;
            return Mathf.Abs(p.TimingError01) <= goodThrowThreshold ? _excitedHash : _defeatHash;
        }

        /// <summary>
        /// Swaps the idle to the sway-and-stumble one. Hook for the drink
        /// meter (Docs/GameBible.md) — nothing drives it yet.
        /// </summary>
        public void SetDrunk(bool drunk)
        {
            if (animator != null) animator.SetBool(_drunkHash, drunk);
        }

        /// <summary>
        /// Blends idle into the walk cycle. Hook for when characters actually
        /// move between turns — nothing drives it yet.
        /// </summary>
        public void SetSpeed(float speed)
        {
            if (animator != null) animator.SetFloat(_speedHash, speed);
        }
    }
}
