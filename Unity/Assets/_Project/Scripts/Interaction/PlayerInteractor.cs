using System.Collections.Generic;
using UnityEngine;
using WeeSpurts.Player;

namespace WeeSpurts.Interaction
{
    /// <summary>
    /// ONE player's "what am I looking at, and can I use it" component. Lives
    /// on the Player root next to <see cref="PlayerAvatar"/>.
    ///
    /// WHO TURNS THIS ON AND OFF: only <see cref="PlayerAvatar.ApplyMode"/>,
    /// exactly like FirstPersonController. Nothing else in the codebase may set
    /// `enabled` on this component. Roaming owns it; bowling does not, because
    /// a player at the foul line pressing E should not be able to re-trigger
    /// the kiosk they just used.
    ///
    /// ---------------------------------------------------------------------
    /// WHY A REGISTRY AND NOT PHYSICS
    /// ---------------------------------------------------------------------
    /// The obvious Unity way to do this is trigger colliders, or a
    /// Physics.SphereCast on an "Interactable" layer. Both work. Both also
    /// require every interactable to carry a correctly-sized collider on a
    /// correctly-configured layer, with the layer collision matrix set so those
    /// colliders don't shove the player around or, worse, deflect the ball.
    /// This project has a hard rule that decorative venue geometry is
    /// collider-free precisely because a stray collider near the lane is a
    /// catastrophic, hard-to-spot bug.
    ///
    /// So instead: interactables put THEMSELVES in a list when they switch on
    /// and take themselves out when they switch off, and we scan the list. A
    /// venue has a few dozen interactables, not a few thousand — scanning that
    /// once a frame is free, and it means zero colliders, zero layers, zero
    /// physics settings to mis-wire. If the count ever climbs into the
    /// thousands, THAT is the moment to reach for a spatial query, not before.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerInteractor : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // The registry. Static methods on this class rather than a separate
        // type, so "where do I register?" has exactly one obvious answer.
        // ------------------------------------------------------------------

        private static readonly List<IInteractable> Registered = new List<IInteractable>();

        /// <summary>
        /// Called by an interactable from its own OnEnable. Safe to call twice.
        /// </summary>
        public static void Register(IInteractable interactable)
        {
            if (interactable == null) return;
            if (!Registered.Contains(interactable)) Registered.Add(interactable);
        }

        /// <summary>
        /// Called by an interactable from its own OnDisable. Safe to call on
        /// something that was never registered.
        /// </summary>
        public static void Deregister(IInteractable interactable)
        {
            Registered.Remove(interactable);
        }

        /// <summary>
        /// Empties the registry before a Play session starts.
        ///
        /// THIS IS NOT PARANOIA, IT IS A REAL BUG WE ARE PRE-EMPTING. A `static`
        /// field belongs to the loaded assembly, not to the scene, so it does
        /// NOT get cleared when you press Play — UNLESS Unity reloads the C#
        /// domain, which is the default but which projects routinely turn off
        /// (Edit > Project Settings > Editor > "Enter Play Mode Options") to
        /// make entering Play mode near-instant. With domain reload off, this
        /// list would still be holding every interactable from your LAST Play
        /// session — all of them destroyed objects — and it would grow every
        /// time you pressed Play until something eventually picked a dead entry
        /// as its target. Clearing it here makes the behaviour identical either
        /// way.
        ///
        /// SubsystemRegistration is the EARLIEST of the RuntimeInitializeLoadType
        /// values — it runs before the first scene loads, so before any
        /// interactable's OnEnable has had a chance to register. Anything later
        /// (BeforeSceneLoad, AfterSceneLoad) risks wiping registrations that
        /// were legitimately made this session. Verified against
        /// docs.unity3d.com/ScriptReference/RuntimeInitializeLoadType.html.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry()
        {
            Registered.Clear();
        }

        /// <summary>
        /// Is this interactable still a live object?
        ///
        /// READ THIS BEFORE YOU "SIMPLIFY" IT — it is a genuine C# / Unity trap.
        /// Unity OVERLOADS `operator ==` on UnityEngine.Object so that a
        /// DESTROYED object compares equal to null (that's the famous "fake
        /// null"). But operator overloads are resolved at COMPILE time from the
        /// STATIC type of the operands — and the static type here is an
        /// interface, which knows nothing about UnityEngine.Object. So
        /// `interactable == null` on an interface reference silently uses plain
        /// reference comparison, and a destroyed MonoBehaviour sails straight
        /// through as "not null". Casting to Component first is what brings
        /// Unity's overload back into play.
        ///
        /// Public because <see cref="InteractionPromptHud"/> needs the same
        /// check a frame later and there must be exactly one copy of this
        /// reasoning in the codebase.
        /// </summary>
        public static bool IsAlive(IInteractable interactable)
        {
            if (interactable == null) return false;          // real C# null

            // The Unity-aware check. `is Component c` also fails for a real
            // null, but we already handled that above.
            if (interactable is Component component) return component != null;

            // Not a Unity object at all (a plain C# interactable — nothing
            // implements this that way yet, but the interface allows it).
            // There is nothing to be destroyed, so it's alive.
            return true;
        }

        // ------------------------------------------------------------------
        // Instance
        // ------------------------------------------------------------------

        // [SerializeField] on every reference, for the reason AimPreview taught
        // this project the hard way: these are wired by an EDITOR tool, not at
        // Play-mode startup, so a field Unity doesn't serialize comes back null
        // after a scene reload and the component silently does nothing.
        [Tooltip("Range, cone angle and the interact key. Without it this component does nothing rather than guessing at numbers.")]
        [SerializeField] private InteractionConfig config;

        [Tooltip("The EYE transform — the FirstPersonCamera child. Facing is measured from where you are LOOKING, not from where your hips point: in first person those are different things, and 'is it in front of me' means the camera.")]
        [SerializeField] private Transform eye;

        [Tooltip("The avatar this interactor belongs to. Handed to every interactable so their answers can be per-player.")]
        [SerializeField] private PlayerAvatar avatar;

        /// <summary>
        /// The best thing in reach right now, or null. Recomputed every frame
        /// while this component is enabled; read by the prompt HUD.
        /// </summary>
        public IInteractable Current { get; private set; }

        /// <summary>The avatar to pass to interactables. Exposed for the HUD.</summary>
        public PlayerAvatar Avatar => avatar;

        /// <summary>
        /// The key that actually triggers <see cref="Current"/>, so the HUD can
        /// draw a prefix that is guaranteed to match. Interactables deliberately
        /// do NOT know this — see IInteractable.GetPrompt — because the binding
        /// belongs to the player, and a rebound key or a controller must change
        /// what the prompt says without touching a single object in the venue.
        ///
        /// KeyCode.None when unconfigured, which the HUD reads as "draw the
        /// action with no prefix" rather than naming a key that does nothing.
        /// </summary>
        public KeyCode InteractKey => config != null ? config.InteractKey : KeyCode.None;

        private void OnDisable()
        {
            // Drop the target on the way out of roaming. Without this, the last
            // thing you looked at stays in Current, and the moment anything
            // asks (a HUD that forgets to check `enabled`, a future system) it
            // gets a stale answer — a "[E] Start Game" prompt floating over the
            // screen while you are mid-throw.
            Current = null;
        }

        private void Update()
        {
            // No config means no range, no cone and no key — there is nothing
            // sensible to do, and inventing defaults here would quietly
            // duplicate the ScriptableObject's job.
            if (config == null || eye == null)
            {
                Current = null;
                return;
            }

            Current = FindBestTarget();

            if (Current != null && Input.GetKeyDown(config.InteractKey))
            {
                // The local player ASKS. See IInteractable.Interact — when
                // Mirror lands this call becomes a [Command] and the host
                // re-validates before acting.
                Current.Interact(avatar);
            }
        }

        /// <summary>
        /// The nearest interactable that is (a) in range, (b) inside the facing
        /// cone, and (c) says it is usable by this player. Null if none is.
        ///
        /// Nearest-wins is the tie-break because it matches what a player
        /// expects when two things are close together: the one you are standing
        /// next to. Note the CanInteract test is done LAST, after the two cheap
        /// geometric rejections — most of the venue's interactables are nowhere
        /// near you on any given frame, and CanInteract is the one call here we
        /// don't control the cost of.
        /// </summary>
        private IInteractable FindBestTarget()
        {
            float rangeSqr = config.Range * config.Range;

            // Compare a dot product against the COSINE of the half-angle rather
            // than calling Vector3.Angle per candidate: same result, no inverse
            // trig, and it makes the "both vectors must be normalised" contract
            // explicit. cos is monotonically DECREASING, so a bigger dot means
            // a smaller angle — hence the `<` rejection below.
            float cosLimit = Mathf.Cos(config.FacingAngleDegrees * Mathf.Deg2Rad);

            // Flatten the look direction onto the horizontal plane. In the pit
            // the kiosk sits at your feet and you are looking down at it — an
            // un-flattened forward vector would put it well outside the cone and
            // the prompt would vanish exactly when you got close enough to use
            // it. We only care about which way you are FACING, not your pitch.
            Vector3 eyeForward = eye.forward;
            eyeForward.y = 0f;
            if (eyeForward.sqrMagnitude < 0.0001f)
            {
                // Looking straight up or straight down: the flattened forward is
                // meaningless, so fall back to the body's facing rather than
                // normalising a zero vector (which yields (0,0,0) and would make
                // every dot product 0 — i.e. nothing is ever in the cone).
                eyeForward = transform.forward;
                eyeForward.y = 0f;
                if (eyeForward.sqrMagnitude < 0.0001f) return null;
            }
            eyeForward.Normalize();

            IInteractable best = null;
            float bestSqr = float.MaxValue;

            // Indexed loop, not foreach: Interact() is not called from in here,
            // but CanInteract() is, and an implementor that registers or
            // deregisters something during it would invalidate a foreach
            // enumerator. An indexed loop over a list that only ever grows at
            // the end degrades gracefully instead of throwing.
            for (int i = 0; i < Registered.Count; i++)
            {
                IInteractable candidate = Registered[i];
                if (!IsAlive(candidate)) continue;

                Transform point = candidate.InteractionPoint;
                // Plain "!= null" is correct here: point is a Transform, a real
                // UnityEngine.Object, so Unity's == overload DOES apply and a
                // destroyed transform is caught. (Contrast IsAlive above.)
                if (point == null) continue;

                // Range: feet to hotspot, plain 3D distance. Squared, so no
                // square root per candidate.
                Vector3 toPointFromFeet = point.position - transform.position;
                float distSqr = toPointFromFeet.sqrMagnitude;
                if (distSqr > rangeSqr) continue;

                // Facing: measured from the EYE, because that is what the
                // player is aiming with.
                Vector3 toPointFromEye = point.position - eye.position;
                toPointFromEye.y = 0f;
                if (toPointFromEye.sqrMagnitude > 0.0001f)
                {
                    toPointFromEye.Normalize();
                    if (Vector3.Dot(eyeForward, toPointFromEye) < cosLimit) continue;
                }
                // else: you are standing directly on top of it, so "which way am
                // I facing relative to it" has no answer. Count it as in-cone
                // rather than making a thing impossible to use by hugging it.

                if (!candidate.CanInteract(avatar)) continue;

                if (distSqr < bestSqr)
                {
                    bestSqr = distSqr;
                    best = candidate;
                }
            }

            return best;
        }
    }
}
