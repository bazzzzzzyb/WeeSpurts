using UnityEngine;

namespace WeeSpurts.Bowling
{
    /// <summary>
    /// Pure camera-framing MATH for the scripted throw camera. No MonoBehaviour,
    /// no scene, no Unity objects — just numbers in, numbers out.
    ///
    /// WHY a separate static class? Two reasons a beginner should know:
    ///  1. It can be unit-tested in an EditMode test with no scene loaded, which
    ///     is impossible for anything that needs a Camera or a Transform.
    ///  2. It keeps ThrowCameraSequence readable: that class is about WHEN each
    ///     beat happens; this one is about WHERE the camera has to stand.
    ///
    /// Everything here is deterministic — same inputs always give the same
    /// answer, no UnityEngine.Random anywhere (Docs/Networking.md keeps the
    /// throw itself reproducible; the camera never gets to be the odd one out).
    /// </summary>
    public static class ThrowCameraFraming
    {
        // Unity's own Camera inspector clamps field of view to 1..179 degrees.
        // Matching that here means tan() below can never blow up to infinity.
        private const float MIN_FOV_DEGREES = 1f;
        private const float MAX_FOV_DEGREES = 179f;

        // Aspect = width / height. A zero or negative aspect is nonsense (it
        // would come from an uninitialised camera), so we floor it instead of
        // returning NaN and dumping the camera at an invalid position.
        private const float MIN_ASPECT = 0.01f;

        /// <summary>
        /// Converts a camera's VERTICAL field of view (the only one Unity's
        /// Camera.fieldOfView exposes) into the HORIZONTAL field of view.
        ///
        /// Why we need this: "show me 3 lanes side by side" is a question about
        /// how WIDE the frame is, and width depends on the screen's aspect
        /// ratio. Solving it from vertical FOV alone would give a shot that only
        /// framed correctly on one monitor.
        /// </summary>
        public static float HorizontalFovDegrees(float verticalFovDegrees, float aspect)
        {
            float vFov = Mathf.Clamp(verticalFovDegrees, MIN_FOV_DEGREES, MAX_FOV_DEGREES);
            float safeAspect = Mathf.Max(aspect, MIN_ASPECT);

            // Standard perspective-projection identity:
            //   tan(hFov/2) = tan(vFov/2) * aspect
            float halfVerticalRadians = vFov * 0.5f * Mathf.Deg2Rad;
            float halfHorizontalRadians = Mathf.Atan(Mathf.Tan(halfVerticalRadians) * safeAspect);
            return halfHorizontalRadians * 2f * Mathf.Rad2Deg;
        }

        /// <summary>
        /// How far BACK the camera must stand, along its own view direction, for
        /// exactly <paramref name="lanesInFrame"/> lane-widths to span the screen.
        ///
        /// This is the trick that makes beat F (the impact shot) resolution-proof:
        /// instead of hand-authoring "camera sits 2.2m from the pins" — which
        /// would frame differently on ultrawide vs. 16:9 — we ASK for a framing
        /// ("our lane centred, a sliver of the neighbours either side") and solve
        /// for the distance. Note we DOLLY (move the camera); we never write
        /// Camera.fieldOfView, because changing FOV mid-shot warps perspective
        /// and reads as a cheap zoom rather than a camera move.
        ///
        /// Returns 0 for degenerate inputs (zero/negative width or lane count)
        /// rather than NaN/Infinity, so a half-configured scene puts the camera
        /// somewhere silly-but-finite instead of nowhere at all.
        /// </summary>
        public static float DistanceForLanesInFrame(float laneWidth, float lanesInFrame,
                                                    float verticalFovDegrees, float aspect)
        {
            float widthToFrame = Mathf.Max(0f, laneWidth) * Mathf.Max(0f, lanesInFrame);
            if (widthToFrame <= 0f) return 0f;

            float halfHorizontalRadians = HorizontalFovDegrees(verticalFovDegrees, aspect) * 0.5f * Mathf.Deg2Rad;
            float tangent = Mathf.Tan(halfHorizontalRadians);
            // Cannot happen given the FOV clamp above, but a divide-by-zero here
            // would put the camera at infinity, so it is worth one cheap check.
            if (tangent <= Mathf.Epsilon) return 0f;

            // Right-triangle: half the frame width is the opposite side, the
            // distance is the adjacent side, half the horizontal FOV is the angle.
            return (widthToFrame * 0.5f) / tangent;
        }

        /// <summary>
        /// Blends between two camera positions by SWINGING around a pivot
        /// (the thrower) instead of sliding in a straight line.
        ///
        /// WHY THIS MATTERS (this is the whole reason beats A2/B/C feel good):
        /// a plain Vector3.Lerp from "behind-left of the thrower" to
        /// "behind-right of the thrower" cuts the CHORD — the straight shortcut
        /// — which (a) reads as the camera sliding sideways rather than orbiting,
        /// and (b) can pass straight through the thrower's body on the way. By
        /// splitting the move into angle / radius / height and interpolating each
        /// separately, the camera travels along an ARC, which is what a real
        /// camera operator walking around a subject would do.
        ///
        /// <paramref name="minRadius"/> is a hard floor on how close the arc may
        /// get to the pivot, so the camera can never end up inside the thrower.
        /// </summary>
        public static Vector3 CylindricalLerp(Vector3 from, Vector3 to, Vector3 pivot, float t, float minRadius)
        {
            float clampedT = Mathf.Clamp01(t);

            // Split each endpoint into (angle around the pivot, distance from the
            // pivot on the flat ground plane, height above the pivot).
            Vector3 fromFlat = new Vector3(from.x - pivot.x, 0f, from.z - pivot.z);
            Vector3 toFlat = new Vector3(to.x - pivot.x, 0f, to.z - pivot.z);

            float fromRadius = fromFlat.magnitude;
            float toRadius = toFlat.magnitude;

            // Atan2(x, z) — deliberately x-then-z, not the usual y-then-x — gives
            // an angle in Unity's convention where 0 degrees points along +Z and
            // angles increase clockwise looking down, exactly matching
            // Quaternion.Euler(0, angle, 0). If a point sits exactly on the pivot
            // its angle is meaningless; Atan2(0,0) returns 0, which is harmless
            // because the radius is 0 there anyway.
            float fromAngle = Mathf.Atan2(fromFlat.x, fromFlat.z) * Mathf.Rad2Deg;
            float toAngle = Mathf.Atan2(toFlat.x, toFlat.z) * Mathf.Rad2Deg;

            // LerpAngle (not Lerp) takes the SHORT way round the circle, so a
            // swing from 170 to -170 degrees travels 20 degrees, not 340.
            float angle = Mathf.LerpAngle(fromAngle, toAngle, clampedT);
            float radius = Mathf.Max(Mathf.Lerp(fromRadius, toRadius, clampedT), Mathf.Max(0f, minRadius));
            float height = Mathf.Lerp(from.y - pivot.y, to.y - pivot.y, clampedT);

            return PointAround(pivot, angle, radius, height);
        }

        /// <summary>
        /// Builds a world position from cylindrical coordinates around a pivot.
        /// angleDegrees 0 = straight out along +Z (down-lane side of the thrower),
        /// 180 = straight behind them. This is how the beat B / beat C knobs in
        /// ThrowCameraSequenceConfig are expressed, so "swing 10 degrees further
        /// round" is one number to change rather than three.
        /// </summary>
        public static Vector3 PointAround(Vector3 pivot, float angleDegrees, float radius, float height)
        {
            Vector3 direction = Quaternion.Euler(0f, angleDegrees, 0f) * Vector3.forward;
            return pivot + direction * Mathf.Max(0f, radius) + Vector3.up * height;
        }

        /// <summary>
        /// Blends between two positions in a STRAIGHT push, but bulged out to one
        /// side so the path steps around an obstacle in the middle instead of
        /// walking through it.
        ///
        /// WHY THIS EXISTS (beat D — the release): the camera has to get from
        /// behind the thrower to out over the lane in front of them. Two obvious
        /// approaches both fail:
        ///  - A plain straight line passes almost exactly through the thrower.
        ///  - Orbiting around them (CylindricalLerp) is a ~170 degree swing,
        ///    because "behind" and "in front" are nearly opposite sides of the
        ///    same pivot. That reads as a violent whip-pan sideways, not as a
        ///    camera moving down the lane.
        /// So instead we keep the straight push — which is what the move is
        /// actually about — and displace it sideways by a smooth bump that is
        /// ZERO at both ends and widest in the middle. The camera slips PAST the
        /// thrower on one side and arrives exactly where the beat asked for.
        ///
        /// The bump is sin(pi * t): 0 at t=0, 1 at t=0.5, 0 at t=1. Using a sine
        /// rather than a triangle means it also eases in and out of the sidestep,
        /// so there is no visible kink at the widest point.
        /// </summary>
        public static Vector3 SidePassLerp(Vector3 from, Vector3 to, float t, float sideOffset)
        {
            float clampedT = Mathf.Clamp01(t);
            Vector3 straight = Vector3.Lerp(from, to, clampedT);
            if (Mathf.Approximately(sideOffset, 0f)) return straight;

            float bump = Mathf.Sin(clampedT * Mathf.PI);
            return straight + Vector3.right * (sideOffset * bump);
        }

        /// <summary>
        /// Pushes a position away from a pivot until it is at least
        /// <paramref name="minRadius"/> away on the flat ground plane, leaving its
        /// height alone. Used on the beats whose target pose is authored in world
        /// space (beat D) so a hand-typed offset can never bury the camera inside
        /// the thrower capsule.
        /// </summary>
        public static Vector3 EnforceRadialClearance(Vector3 position, Vector3 pivot, float minRadius)
        {
            if (minRadius <= 0f) return position;

            Vector3 flat = new Vector3(position.x - pivot.x, 0f, position.z - pivot.z);
            float radius = flat.magnitude;
            if (radius >= minRadius) return position;

            // Sitting exactly on the pivot gives no direction to push along, so
            // fall back to "straight behind" rather than dividing by zero.
            Vector3 direction = radius > 0.0001f ? flat / radius : Vector3.back;
            Vector3 pushed = pivot + direction * minRadius;
            return new Vector3(pushed.x, position.y, pushed.z);
        }
    }
}
