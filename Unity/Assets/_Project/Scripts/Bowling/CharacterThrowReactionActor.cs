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
        public const string DrunkBool = "Drunk";
        public const string SpeedFloat = "Speed";

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
        private int _drunkHash;
        private int _speedHash;

        private void Awake()
        {
            // GetComponentInChildren also checks THIS object, so it covers both
            // the wrapper-root layout the prefab uses and a bare model root.
            if (animator == null) animator = GetComponentInChildren<Animator>();

            _excitedHash = Animator.StringToHash(ExcitedTrigger);
            _defeatHash = Animator.StringToHash(DefeatTrigger);
            _fallFlatHash = Animator.StringToHash(FallFlatTrigger);
            _drunkHash = Animator.StringToHash(DrunkBool);
            _speedHash = Animator.StringToHash(SpeedFloat);
        }

        public void PlayReaction(LaunchParameters p)
        {
            if (animator == null) return;

            // Reset the other two triggers before setting one. A trigger that
            // was set but never consumed (e.g. two throws resolved back to
            // back, or the sandbox frame-reset key) stays queued in the
            // Animator and would fire a stale reaction on the NEXT throw.
            animator.ResetTrigger(_excitedHash);
            animator.ResetTrigger(_defeatHash);
            animator.ResetTrigger(_fallFlatHash);

            animator.SetTrigger(ChooseReactionHash(p));
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
