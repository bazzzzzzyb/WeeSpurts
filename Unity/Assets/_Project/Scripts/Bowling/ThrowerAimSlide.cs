using UnityEngine;

namespace WeeSpurts.Bowling
{
    /// <summary>
    /// Slides the thrower character sideways with the aim during the AIM phase,
    /// so the player and the ball move together instead of the ball drifting
    /// away from a statue.
    ///
    /// CRITICAL — it reads BowlingGameController.HalfLaneWidth, the SAME value
    /// AimPreview and ResolveThrow use, and does NOT recompute the lateral
    /// maths. That property exists precisely so the preview and the resolved
    /// throw can't disagree; the character has to be bound to it for the same
    /// reason. If the lane width or ball radius ever changes, all three move
    /// together or none of them do.
    ///
    /// Purely cosmetic: it never touches LaunchParameters, and it only ever
    /// writes this transform's X. Rotation is left alone entirely, so the
    /// character keeps facing down-lane (+Z) — the Mixamo clips are in-place
    /// and the Animator has applyRootMotion off, so nothing else moves the root.
    ///
    /// SETUP: sits on the Thrower root. GreyboxSceneBuilder adds it and calls
    /// Configure().
    /// </summary>
    [DisallowMultipleComponent]
    public class ThrowerAimSlide : MonoBehaviour
    {
        [Tooltip("Seconds for the character to catch up to the aim (Vector3.SmoothDamp style). " +
                 "Small = snappy and rigid, large = a lazy drift that lags behind the ball. " +
                 "This is a feel knob: it wants to read as someone stepping across the approach, " +
                 "not as a slider being dragged.")]
        [SerializeField] private float smoothTime = 0.14f;

        [Tooltip("Ceiling on how fast the character can slide, in meters/second. Stops a full " +
                 "left-to-right aim sweep from teleporting them across the lane.")]
        [SerializeField] private float maxSpeed = 6f;

        // [SerializeField] for the same reason as AimPreview's references:
        // Configure() is called by the EDITOR-time GreyboxSceneBuilder, not at
        // Play-mode startup, so these must survive being saved into the scene.
        [SerializeField] private BallLauncher launcher;
        [SerializeField] private BowlingGameController game;

        [Tooltip("Where the character stands at zero lateral aim. Captured by Configure() at " +
                 "scene-build time so it can't drift with wherever they happened to stop.")]
        [SerializeField] private Vector3 homePosition;

        // SmoothDamp's running velocity. It owns this value between calls —
        // resetting it mid-slide is what makes damping snap.
        private float _slideVelocity;

        public void Configure(BallLauncher launcherRef, BowlingGameController gameRef)
        {
            launcher = launcherRef;
            game = gameRef;
            homePosition = transform.position;
        }

        /// <summary>
        /// Re-bakes "where the character stands at zero lateral aim".
        ///
        /// WHY THIS EXISTS: homePosition is captured ONCE, at scene-build time,
        /// by Configure() above. That was correct while the thrower was a statue
        /// that never left the foul line. Now that the player can walk away and
        /// come back (PlayerAvatar.MoveToThrowingStance), a stale home would
        /// yank them back to wherever the scene happened to be built the instant
        /// they started aiming. PlayerAvatar calls this as part of the move.
        /// </summary>
        public void SetHome(Vector3 home)
        {
            homePosition = home;
            // SmoothDamp owns _slideVelocity between calls. Carrying a velocity
            // across a teleport would fling the character sideways from a
            // stimulus that no longer applies — same reasoning as the reset in
            // LateUpdate below, just triggered by a jump instead of an aim ending.
            _slideVelocity = 0f;
        }

        // LateUpdate, matching AimPreview: BallLauncher writes CurrentLateral in
        // its own Update, and Unity runs every Update before any LateUpdate, so
        // this always reads the CURRENT frame's aim rather than last frame's —
        // which is what keeps the character visually locked to the ball instead
        // of trailing it by one frame.
        private void LateUpdate()
        {
            if (launcher == null || game == null) return;

            // Only track while aiming. Once the ball is away the thrower stays
            // where they threw from — following a CurrentLateral that's about to
            // be reset would drag them sideways mid-reaction. BeginAim() zeroes
            // the aim for the next roll, so they then damp back to centre on
            // their own, which reads as stepping back to the approach.
            if (!launcher.IsAiming)
            {
                // Zero the damping velocity at the END of an aim phase, not
                // during one. Resetting mid-slide is what makes damping snap
                // (SmoothDamp owns this value between calls), but carrying it
                // across a whole roll + pin count + turn change and then feeding
                // it into the next aim produces an overshoot from a stimulus
                // several seconds stale.
                _slideVelocity = 0f;
                return;
            }

            float targetX = homePosition.x + launcher.CurrentLateral * game.HalfLaneWidth;

            Vector3 p = transform.position;
            p.x = Mathf.SmoothDamp(p.x, targetX, ref _slideVelocity, smoothTime, maxSpeed);
            // Y and Z are pinned to home rather than left as-is, so nothing else
            // nudging the root (a stray reaction clip, a future ragdoll) can
            // quietly walk the thrower off the foul line over a match.
            p.y = homePosition.y;
            p.z = homePosition.z;
            transform.position = p;
        }
    }
}
