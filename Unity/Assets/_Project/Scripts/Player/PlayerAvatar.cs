using System;
using Mirror;
using UnityEngine;
using WeeSpurts.Bowling;
using WeeSpurts.Interaction;

namespace WeeSpurts.Player
{
    /// <summary>
    /// ONE player's avatar, and the single owner of that player's
    /// <see cref="ControlMode"/>. Sits on the UNSCALED `Player` root, above the
    /// character model:
    ///
    ///   Player                 &lt;- this component, CharacterController, camera director
    ///     |- Thrower           &lt;- the PlayerCharacter prefab instance (scale 0.4)
    ///     |- FirstPersonCamera
    ///
    /// THE RULE THIS CLASS EXISTS TO ENFORCE:
    /// <see cref="ApplyMode"/> is the ONLY place in the entire codebase allowed
    /// to change which mode-owned component is enabled, or to touch the cursor.
    /// Every other system asks for a MODE — EnterRoaming/EnterBowling — and lets
    /// this method work out what that means. That is what makes "exactly one
    /// mode owns your input and your camera" a structural guarantee rather than
    /// a discipline problem that breaks the first time someone adds a feature.
    ///
    /// ONE NARROW, DELIBERATE EXCEPTION: <see cref="MoveToThrowingStance"/>
    /// toggles characterController.enabled off and back ON again, synchronously,
    /// around a single teleporting transform write — never leaving it in a
    /// state ApplyMode didn't already put it in. It is not a second place
    /// deciding WHICH mode owns the controller (ApplyMode already decided that
    /// this roll); it is a transient workaround for a CharacterController
    /// fighting a direct position write. Grepping for ".enabled" on these
    /// components should still turn up ApplyMode as the only place that
    /// persists a change.
    ///
    /// PER-PLAYER, NEVER GLOBAL. BowlingMatchFlow owns the MATCH; it does
    /// not own anyone's mode. In a networked game the active thrower is in
    /// Bowling while everyone else is still Roaming, which is exactly the
    /// walkable-alley behaviour Docs/OpenQuestions.md describes.
    ///
    /// NETWORKED (Mirror/mirror-kcp spike, 2026-08-03): see <see cref="isLocalPlayer"/>
    /// and <see cref="OnStartLocalPlayer"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerAvatar : NetworkBehaviour
    {
        // Was a plain public bool defaulting to true (fail-OPEN — every avatar
        // would consider itself local). Mirror's own isLocalPlayer defaults to
        // false until this identity's ownership is confirmed (fail-CLOSED), and
        // every input/camera/cursor decision below already asked this exact
        // question, so nothing else in this class had to change — see
        // OnStartLocalPlayer below for the one addition that DID need to change.

        [Header("Mode-owned components (wired by RoamingSetupTool)")]
        // All [SerializeField] because an editor tool wires them, not Play-mode
        // code — the AimPreview lesson (Docs/GameBible.md changelog 2026-07-22):
        // a reference Unity doesn't serialize is null again after a scene reload.
        [Tooltip("On THIS object, not on the model. Owned by roaming; switched OFF for bowling so it can't fight ThrowerAimSlide's direct transform writes.")]
        [SerializeField] private CharacterController characterController;

        [Tooltip("On THIS object. Owned by roaming.")]
        [SerializeField] private FirstPersonController firstPersonController;

        [Tooltip("On THIS object. Owned by roaming — finds and triggers world interactables (the lane kiosk today; the bar, slots and card table later). Off during bowling so a player at the line can't re-trigger the kiosk they just used. The prompt HUD follows this component's enabled state, so it needs no line of its own here.")]
        [SerializeField] private PlayerInteractor interactor;

        [Tooltip("On the Thrower child. Owned by bowling — it slides the character sideways with the aim.")]
        [SerializeField] private ThrowerAimSlide throwerAimSlide;

        [Tooltip("The character model child (the PlayerCharacter prefab instance). Its localPosition gets zeroed when you go back to roaming — see EnterRoaming.")]
        [SerializeField] private Transform throwerModel;

        /// <summary>What this player is doing right now. Only this class writes it.</summary>
        public ControlMode Mode { get; private set; } = ControlMode.Roaming;

        /// <summary>
        /// Fired AFTER a mode change has been fully applied. This is how systems
        /// that are not mode-owned components (the camera director today, a HUD
        /// tomorrow) react without anyone reaching into this class's business.
        /// </summary>
        public event Action<ControlMode> OnModeChanged;

        // Has any mode been applied yet? Guards against script execution ORDER:
        // if BowlingPresentation.Start happens to run before ours and puts us
        // into Bowling, our own Start must not then yank us back to Roaming.
        // Unity gives no ordering guarantee between two objects' Start methods.
        private bool _modeApplied;

        private void Start()
        {
            // Roaming is the default state of the world: you're standing in the
            // alley, not at the line. Only apply it if nobody beat us to it.
            if (!_modeApplied) EnterRoaming();
        }

        /// <summary>
        /// Fires once Mirror has confirmed THIS machine owns this identity —
        /// unlike Start(), which runs before that is guaranteed. Re-applying
        /// here is the fix for a predicted spike bug: Start() already ran
        /// ApplyMode() while isLocalPlayer still read false, which permanently
        /// disabled firstPersonController/interactor and skipped the cursor for
        /// the actual local player, with no console error. Start()'s call stays
        /// in place — non-local avatars and host-side setup still need it.
        /// </summary>
        public override void OnStartLocalPlayer()
        {
            ApplyMode();
        }

        /// <summary>Hand this player back their legs, their camera and their mouse.</summary>
        public void EnterRoaming()
        {
            Mode = ControlMode.Roaming;
            ApplyMode();
        }

        /// <summary>
        /// Put this player at the foul line and hand control to the bowling
        /// systems. <paramref name="stance"/> may be null, in which case the
        /// mode still changes but the avatar isn't moved.
        /// </summary>
        public void EnterBowling(Transform stance)
        {
            Mode = ControlMode.Bowling;
            // ApplyMode FIRST: it turns the CharacterController off, and the
            // teleport below wants it already off (see MoveToThrowingStance).
            ApplyMode();
            MoveToThrowingStance(stance);
        }

        /// <summary>
        /// THE ONLY PLACE mode-owned components and the cursor are switched.
        /// Read the class doc comment before adding anything here — and never
        /// add an equivalent line anywhere else.
        /// </summary>
        private void ApplyMode()
        {
            bool roaming = Mode == ControlMode.Roaming;
            _modeApplied = true;

            // --- Components that move this transform ------------------------
            // NOT gated on isLocalPlayer. These are about who is allowed to write the
            // transform, which is true for a remote avatar too (its position
            // will arrive over the network later, but ThrowerAimSlide still
            // drives its slide from replicated aim values).
            //
            // THE WHOLE REASON THE CHARACTERCONTROLLER GOES OFF FOR BOWLING:
            // an ENABLED CharacterController owns its object's position and
            // overwrites direct transform writes on the next physics step.
            // ThrowerAimSlide works by writing transform.position every
            // LateUpdate. Leave both on and they fight, every frame, and the
            // character judders on the spot instead of sliding with the aim.
            if (characterController != null) characterController.enabled = roaming;
            if (throwerAimSlide != null) throwerAimSlide.enabled = !roaming;

            // --- Local-input components -------------------------------------
            // Gated on isLocalPlayer: a remote player's avatar must never read
            // THIS machine's keyboard and mouse.
            if (firstPersonController != null) firstPersonController.enabled = roaming && isLocalPlayer;
            if (interactor != null) interactor.enabled = roaming && isLocalPlayer;

            // --- Clean-up on the way back to roaming ------------------------
            if (roaming) ResetThrowerModelOffset();

            // --- Cursor ------------------------------------------------------
            // Local only. A remote avatar touching your cursor would be a
            // genuinely baffling bug to track down.
            if (isLocalPlayer) ApplyCursor(roaming);

            // Camera switching lives in PlayerCameraDirector, which subscribes
            // to this event. Raised LAST so anything listening sees a fully
            // applied mode. The director does its own isLocalPlayer check.
            OnModeChanged?.Invoke(Mode);
        }

        /// <summary>
        /// ThrowerAimSlide writes the model's WORLD position to slide it
        /// sideways with the aim, which leaves the child sitting at a non-zero
        /// localPosition once the roll is over. Left alone, that offset rides
        /// along as you walk — your body permanently a metre to the left of
        /// where the game thinks you are.
        /// </summary>
        private void ResetThrowerModelOffset()
        {
            if (throwerModel != null) throwerModel.localPosition = Vector3.zero;
        }

        private void ApplyCursor(bool roaming)
        {
            if (roaming)
            {
                // Mouse-look: the cursor has to be captured or you'd shoot it
                // out of the window and start clicking on your desktop.
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                // DELIBERATE INVERSION, and it looks wrong until you know why:
                // the bowling controls are NOT mouse-look. SpinSelectorHud is a
                // click-and-DRAG widget (Input.GetMouseButton(0)), so the player
                // needs a visible pointer they can aim at it. Locking the cursor
                // here would make the spin selector unusable.
                //
                // HONEST CAVEAT: docs.unity3d.com states Confined "only works
                // on Windows and Linux Standalone" — it does NOT say what it
                // falls back to elsewhere. Observed by testing in this project
                // (Editor, macOS): it behaves as None, i.e. the cursor is free
                // rather than confined to the window. That is harmless here —
                // the part that matters is that the cursor is FREE and VISIBLE
                // — but don't be surprised when you can drag the mouse out of
                // the Game view on a Mac.
                Cursor.lockState = CursorLockMode.Confined;
                Cursor.visible = true;
            }
        }

        /// <summary>
        /// Teleport this avatar to the throwing stance and face them down-lane.
        ///
        /// THIS IS THE DELIBERATE SWAP POINT for the comedy transition that is
        /// currently parked in Docs/OpenQuestions.md:22-23 — "on your turn,
        /// you're dragged back AGAINST YOUR WILL, character's face shifts to
        /// comic fear, all the way to the starting line, ball already in hand".
        /// It is a hard snap for now on purpose: the snap is the cheapest thing
        /// that makes the mode switch testable, and Tony decides by playing
        /// whether the drag is worth building. When it is, this ONE method
        /// becomes a coroutine that animates the same journey, and no caller
        /// changes.
        /// </summary>
        public void MoveToThrowingStance(Transform stance)
        {
            if (stance == null) return;

            // A CharacterController that is ENABLED overwrites direct transform
            // writes with its own internal position, so a teleport silently
            // snaps back. Toggle it off around the write, and restore whatever
            // it was — this method is public and may be called in either mode.
            bool controllerWasEnabled = characterController != null && characterController.enabled;
            if (controllerWasEnabled) characterController.enabled = false;

            // Flatten the stance's forward onto the horizontal plane before
            // using it: a marker nudged slightly downward in the editor would
            // otherwise tip the whole character forward.
            Vector3 forward = stance.forward;
            forward.y = 0f;
            Quaternion facing = forward.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(forward, Vector3.up)
                : transform.rotation; // degenerate (marker aimed straight down): keep what we had

            transform.SetPositionAndRotation(stance.position, facing);

            if (controllerWasEnabled) characterController.enabled = true;

            // Clear any leftover slide offset BEFORE reading the model's
            // position below, so "home" is the stance itself and not the stance
            // plus however far sideways the last roll left the character.
            ResetThrowerModelOffset();

            // ThrowerAimSlide bakes its home position ONCE at scene-build time,
            // which was fine when the thrower never moved. Now that the player
            // can walk off and come back, re-bake it here or the very first aim
            // frame yanks them back to wherever the scene was built.
            if (throwerAimSlide != null)
                throwerAimSlide.SetHome(throwerModel != null ? throwerModel.position : transform.position);
        }
    }
}
