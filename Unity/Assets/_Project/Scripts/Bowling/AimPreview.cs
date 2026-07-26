using UnityEngine;

namespace WeeSpurts.Bowling
{
    /// <summary>
    /// Sandbox/debug-only visual for the AIM phase. While the ball is frozen
    /// waiting to be thrown, this slides it sideways to match
    /// BallLauncher.CurrentLateral (using the SAME halfLane math
    /// BowlingGameController uses at throw time, via HalfLaneWidth) and draws
    /// a short curved LineRenderer previewing direction (CurrentAngle) and
    /// curve (CurrentSpin, via SpinModel). Purely cosmetic: never touches LaunchParameters,
    /// BowlingScorer, or the resolved throw. Safe to remove before shipping.
    ///
    /// SETUP: sits on the ball's GameObject alongside BowlingBall.
    /// GreyboxSceneBuilder creates the LineRenderer and calls Configure().
    /// </summary>
    public class AimPreview : MonoBehaviour
    {
        // Enough segments to keep a late break reading as a curve rather than a
        // dogleg, now that the line spans the whole spin ramp instead of 2.5m.
        private const int LineSegments = 24;

        // [SerializeField]: Configure() below is called by the EDITOR-time
        // GreyboxSceneBuilder, not at Play-mode startup. Without
        // SerializeField these references wouldn't survive being saved into
        // the .unity scene, and would be null on the next Play session or
        // scene reload — same reason BowlingGameController's own
        // Configure()-wired fields are SerializeField.
        [SerializeField] private BallLauncher launcher;
        [SerializeField] private BowlingGameController game;
        [SerializeField] private LineRenderer line;

        public void Configure(BallLauncher launcherRef, BowlingGameController gameRef, LineRenderer lineRef)
        {
            launcher = launcherRef;
            game = gameRef;
            line = lineRef;
            line.positionCount = LineSegments;
            line.useWorldSpace = true;
            line.enabled = false;
        }

        // LateUpdate (not Update): BallLauncher writes CurrentLateral/Angle/
        // Spin in its own Update, and Unity runs every component's Update
        // before any component's LateUpdate, so this always reads the
        // value for the CURRENT frame, never last frame's.
        private void LateUpdate()
        {
            if (launcher == null || !launcher.IsAiming)
            {
                if (line != null) line.enabled = false;
                return;
            }

            // Same formula ResolveThrow uses to place the ball at throw
            // time, so the preview position IS where it will actually launch from.
            Vector3 basePos = game.BallSpawn.position;
            transform.position = basePos + Vector3.right * (launcher.CurrentLateral * game.HalfLaneWidth);

            DrawAimLine();
        }

        /// <summary>
        /// Draws the predicted path over the whole spin ramp.
        ///
        /// The curve is no longer a hand-picked quadratic: it comes from
        /// SpinModel — the SAME file BowlingBall integrates every FixedUpdate —
        /// so a bottom-weighted (backspin) dial visibly runs straight and then
        /// breaks late, and topspin visibly bends early and then straightens
        /// out. If the two ever disagreed the preview would be lying, which is
        /// the whole reason the maths lives in one shared place.
        ///
        /// Two honest approximations remain, both preview-only: it assumes
        /// constant forward speed (real drag bends this slightly) and it assumes
        /// a well-timed release, since actual power isn't chosen yet during AIM.
        /// It therefore shows your INTENT, not your fumble — the mistiming Hook
        /// is deliberately absent, because a preview of a mistake you haven't
        /// made yet would be nonsense.
        /// </summary>
        private void DrawAimLine()
        {
            line.enabled = true;

            BallConfig config = game.ActiveBallConfig;

            // Same rotation formula BowlingBall.Launch uses for direction.
            Quaternion aim = Quaternion.Euler(0f, launcher.CurrentAngle, 0f);

            // Assume a green release: it's the throw the player is trying to
            // make, and power genuinely isn't known during the aim phase.
            float assumedPower = (launcher.GreenZoneMin + launcher.GreenZoneMax) * 0.5f;
            float speed = Mathf.Lerp(config.MinLaunchSpeed, config.MaxLaunchSpeed, assumedPower);

            // F = ma, so the sideways acceleration at full side spin is
            // SpinCurveForce / Mass — the same numbers BowlingBall hands PhysX.
            float lateralAccel = config.SpinCurveForce / Mathf.Max(0.01f, config.Mass);
            float rampSeconds = config.SpinRampDistance / Mathf.Max(0.01f, speed);

            for (int i = 0; i < LineSegments; i++)
            {
                float t = i / (float)(LineSegments - 1);
                Vector3 forward = aim * Vector3.forward * (config.SpinRampDistance * t);
                // Curve added in WORLD space (not aim-rotated), matching
                // BowlingBall's spin force, which is always a world-space
                // Vector3.right push regardless of aim angle.
                float drift = SpinModel.LateralDriftMeters(
                    launcher.CurrentSpin, t, lateralAccel, rampSeconds, config.RollSkidHookScale);
                line.SetPosition(i, transform.position + forward + Vector3.right * drift);
            }
        }
    }
}
