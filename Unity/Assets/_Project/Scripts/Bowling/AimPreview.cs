using UnityEngine;

namespace WeeSpurts.Bowling
{
    /// <summary>
    /// Sandbox/debug-only visual for the AIM phase. While the ball is frozen
    /// waiting to be thrown, this slides it sideways to match
    /// BallLauncher.CurrentLateral (using the SAME halfLane math
    /// BowlingGameController uses at throw time, via HalfLaneWidth) and draws
    /// a short curved LineRenderer previewing direction (CurrentAngle) and
    /// curve (CurrentSpin). Purely cosmetic: never touches LaunchParameters,
    /// BowlingScorer, or the resolved throw. Safe to remove before shipping.
    ///
    /// SETUP: sits on the ball's GameObject alongside BowlingBall.
    /// GreyboxSceneBuilder creates the LineRenderer and calls Configure().
    /// </summary>
    public class AimPreview : MonoBehaviour
    {
        private const int LineSegments = 12;
        private const float LineLength = 2.5f;

        // Sideways offset (meters) the line reaches at full spin and full
        // length. A preview cheat, not real physics — grows with distance
        // SQUARED to loosely mirror BowlingBall's constant sideways force
        // (constant lateral acceleration -> roughly quadratic drift over
        // distance at near-constant forward speed), so the curve at least
        // reads in the right shape.
        private const float CurveBiasAtFullSpin = 0.5f;

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

        private void DrawAimLine()
        {
            line.enabled = true;

            // Same rotation formula BowlingBall.Launch uses for direction.
            Quaternion aim = Quaternion.Euler(0f, launcher.CurrentAngle, 0f);
            float curveEnd = launcher.CurrentSpin * CurveBiasAtFullSpin;

            for (int i = 0; i < LineSegments; i++)
            {
                float t = i / (float)(LineSegments - 1);
                Vector3 forward = aim * Vector3.forward * (LineLength * t);
                // Curve added in WORLD space (not aim-rotated), matching
                // BowlingBall's spin force, which is always a world-space
                // Vector3.right push regardless of aim angle.
                Vector3 curve = Vector3.right * (curveEnd * t * t);
                line.SetPosition(i, transform.position + forward + curve);
            }
        }
    }
}
