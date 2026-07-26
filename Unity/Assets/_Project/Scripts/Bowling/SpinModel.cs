using UnityEngine;

namespace WeeSpurts.Bowling
{
    /// <summary>
    /// The maths behind 2D spin, in one pure static place.
    ///
    /// WHY THIS FILE EXISTS: BowlingBall (real physics) and AimPreview (the
    /// aim-phase line) both need to know how a given spin curves the ball. If
    /// each worked it out for itself they would quietly drift apart and the
    /// preview would start lying — exactly the failure that
    /// BowlingGameController.HalfLaneWidth already exists to prevent for the
    /// lateral start position. Same trick, applied to the curve.
    ///
    /// Everything here is a pure function of floats: no Unity objects, no
    /// randomness, no Time. That makes it deterministic (a hard requirement —
    /// networked clients must replay the identical throw) and testable in
    /// EditMode with no scene at all.
    ///
    /// THE SPIN VECTOR (LaunchParameters.Spin, clamped inside the unit circle):
    ///   X = side spin / axis tilt = THE HOOK. -1 hooks left, +1 hooks right.
    ///   Y = roll vs skid.
    ///       +1 TOPSPIN  — grips and rolls out EARLY: the curve is front-loaded
    ///                     and there is LESS of it. Straighter, more forward drive.
    ///       -1 BACKSPIN — SKIDS first, then bites: the curve is back-loaded and
    ///                     there is MORE of it. The dramatic late break.
    ///        0          — flat, constant sideways force (the original behaviour).
    ///
    /// Y is deliberately split into two independent terms so tuning one can't
    /// secretly move the other:
    ///   RampShape  = WHEN the curve happens (mean is always exactly 1, so it
    ///                redistributes the force over the lane without adding any).
    ///   GripScale  = HOW MUCH curve there is in total.
    /// </summary>
    public static class SpinModel
    {
        /// <summary>
        /// Clamps a raw spin input into the unit circle. A circle, not a square:
        /// full diagonal must not be stronger than full sideways, and the UI
        /// widget is round, so the maths has to agree with what the player sees.
        /// </summary>
        public static Vector2 Clamp(Vector2 spin) => Vector2.ClampMagnitude(spin, 1f);

        /// <summary>
        /// How far down the ramp the ball is, 0 (just released) .. 1 (fully
        /// rolled out). Measured as distance travelled DOWN-LANE against
        /// BallConfig.SpinRampDistance, not as a fraction of the real lane, so
        /// a different ball config can front- or back-load its whole curve.
        /// </summary>
        public static float Progress01(float distanceDownLane, float rampDistance)
            => Mathf.Clamp01(distanceDownLane / Mathf.Max(0.01f, rampDistance));

        /// <summary>
        /// WHEN the sideways force happens: a multiplier on the base curve force.
        ///
        ///   topspin  (Y=+1): 2(1-u)  — 2x at release, 0 at the pins
        ///   neutral  (Y= 0): 1       — flat, the pre-2D behaviour exactly
        ///   backspin (Y=-1): 2u      — 0 at release, 2x at the pins
        ///
        /// All three average to 1.0 over u in [0,1] ON PURPOSE. This term only
        /// ever moves the curve around in time; the total amount of curve is
        /// GripScale's job. Diagonals combine for free because the whole thing
        /// is a Lerp between these three.
        /// </summary>
        public static float RampShape(float progress01, float spinY)
        {
            float u = Mathf.Clamp01(progress01);
            float y = Mathf.Clamp(spinY, -1f, 1f);

            return y >= 0f
                ? Mathf.Lerp(1f, 2f * (1f - u), y)   // topspin: front-loaded
                : Mathf.Lerp(1f, 2f * u, -y);        // backspin: back-loaded
        }

        /// <summary>
        /// HOW MUCH curve there is in total: topspin grips and rolls straight,
        /// backspin skids and then bites harder.
        ///
        ///   scale = 1 - Y * rollSkidHookScale
        ///   at rollSkidHookScale 0.6: topspin 0.4x, neutral 1x, backspin 1.6x
        ///
        /// Setting rollSkidHookScale to 0 makes Y purely a timing axis (same
        /// total curve, just earlier or later), which is a legitimate feel
        /// choice worth trying.
        /// </summary>
        public static float GripScale(float spinY, float rollSkidHookScale)
            => 1f - Mathf.Clamp(spinY, -1f, 1f) * Mathf.Clamp01(rollSkidHookScale);

        /// <summary>
        /// The sideways force from PLAYER-DIALLED spin, in the same units as
        /// BallConfig.SpinCurveForce. Positive pushes right (world +X).
        ///
        /// This is ONLY the player's spin. The mistiming Hook (GameBible §8) is
        /// a completely separate force that BowlingBall ADDS on top of this —
        /// see the comment at BowlingBall.FixedUpdate. The two are meant to
        /// fight each other, so neither is allowed to scale the other here.
        /// </summary>
        public static float LateralForce(Vector2 spin, float progress01,
                                         float spinCurveForce, float rollSkidHookScale)
            => spin.x * spinCurveForce
               * RampShape(progress01, spin.y)
               * GripScale(spin.y, rollSkidHookScale);

        /// <summary>
        /// Extra forward push from TOPSPIN only ("grips and drives"). Backspin
        /// gets nothing rather than a penalty: a skidding ball keeps its forward
        /// speed in real bowling, and Tony's rule is that spin must never fight
        /// the power meter for control of speed — so this only ever adds, and
        /// only on the top half of the widget.
        /// </summary>
        public static float DriveForce(Vector2 spin, float spinDriveForce)
            => Mathf.Max(0f, spin.y) * spinDriveForce;

        /// <summary>
        /// Sideways DRIFT (a distance, not a force) after travelling
        /// `progress01` of the ramp, for a unit of side spin. This is the exact
        /// double integral of RampShape, so the preview line curves the way the
        /// physics actually will instead of guessing with a hand-picked
        /// quadratic.
        ///
        ///   neutral : u^2/2          -> 0.500 at the pins
        ///   topspin : u^2 - u^3/3    -> 0.667 at the pins
        ///   backspin: u^3/3          -> 0.333 at the pins
        ///
        /// Topspin drifting MORE here is not a bug and not a contradiction: a
        /// front-loaded force has the whole rest of the lane to turn into
        /// displacement. GripScale is what makes topspin end up straighter
        /// overall (0.667 * 0.4 = 0.27 vs neutral's 0.50 at the default).
        ///
        /// Assumes constant forward speed, which real drag makes only roughly
        /// true — fine for a preview line, and deliberately NOT used by the
        /// physics, which integrates the real force every FixedUpdate.
        /// </summary>
        public static float NormalizedDrift(float progress01, float spinY)
        {
            float u = Mathf.Clamp01(progress01);
            float y = Mathf.Clamp(spinY, -1f, 1f);

            float flat = 0.5f * u * u;

            return y >= 0f
                ? Mathf.Lerp(flat, u * u - u * u * u / 3f, y)
                : Mathf.Lerp(flat, u * u * u / 3f, -y);
        }

        /// <summary>
        /// Sideways drift in METERS at a point along the flight, for the aim
        /// preview. Straight Newton: x = a * t^2 * (normalized double integral),
        /// where `lateralAccel` is force/mass and `travelSeconds` is how long
        /// the whole ramp takes at the assumed speed.
        /// </summary>
        public static float LateralDriftMeters(Vector2 spin, float progress01, float lateralAccel,
                                               float travelSeconds, float rollSkidHookScale)
            => spin.x * lateralAccel * travelSeconds * travelSeconds
               * NormalizedDrift(progress01, spin.y)
               * GripScale(spin.y, rollSkidHookScale);
    }
}
