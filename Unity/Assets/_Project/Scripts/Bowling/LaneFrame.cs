using UnityEngine;

namespace WeeSpurts.Bowling
{
    /// <summary>
    /// A lane's own down-lane/lateral basis, explicit instead of assumed.
    ///
    /// WHY THIS EXISTS: BowlingBall, BowlingMatchFlow, AimPreview,
    /// ThrowerAimSlide and ThrowCameraSequence used to hardcode Unity's global
    /// axes — Vector3.forward (world +Z) for "down the lane", Vector3.right
    /// (world +X) for "lateral/hook". That was never wrong exactly; every lane
    /// GreyboxSceneBuilder generates IS built along world Z, so the assumption
    /// held everywhere it mattered — until ThunderLanesVenue.unity's imported
    /// art put a lane along world X instead, and the ball launched sideways
    /// into a gutter instead of down the lane.
    ///
    /// This component makes the axis explicit and per-lane instead of global:
    /// place one at a lane's foul-line centre, rotate it so its own +Z faces
    /// down that lane, and every consumer below reads Forward/Right from here
    /// instead of from Unity's world constants.
    ///
    /// DEFAULT-SAFE BY CONSTRUCTION: every consumer treats a null LaneFrame
    /// reference as "use Vector3.forward/right", so TestVenue, BowlingTestbed
    /// and ThunderLanes.unity — none of which wire this field — are
    /// numerically unchanged. Only Origin at the world origin with identity
    /// rotation reproduces the exact same numbers as the old hardcoded
    /// axes, which is exactly the SHAPE (not just the spirit) of what those
    /// scenes' lanes already are.
    /// </summary>
    public class LaneFrame : MonoBehaviour
    {
        /// <summary>The lane's own reference point — typically the foul line, on the playable lane's own centreline.</summary>
        public Vector3 Origin => transform.position;

        /// <summary>Down-lane direction — the way a centred throw travels.</summary>
        public Vector3 Forward => transform.forward;

        /// <summary>Lateral direction — hook, wobble, and the gutter/aim clamp.</summary>
        public Vector3 Right => transform.right;

        /// <summary>How far a world point is down-lane from <see cref="Origin"/>, measured along <see cref="Forward"/>.</summary>
        public float DistanceAlong(Vector3 worldPosition) => Vector3.Dot(worldPosition - Origin, Forward);

        /// <summary>How far a world point is lateral from <see cref="Origin"/>, measured along <see cref="Right"/>.</summary>
        public float LateralOf(Vector3 worldPosition) => Vector3.Dot(worldPosition - Origin, Right);

        /// <summary>
        /// Builds a world position from lane-relative coordinates: how far
        /// down-lane, how far lateral, and how far up — the inverse of
        /// <see cref="DistanceAlong"/>/<see cref="LateralOf"/>.
        /// </summary>
        public Vector3 PointAt(float downLane, float lateral, float height) =>
            Origin + Forward * downLane + Right * lateral + Vector3.up * height;
    }
}
