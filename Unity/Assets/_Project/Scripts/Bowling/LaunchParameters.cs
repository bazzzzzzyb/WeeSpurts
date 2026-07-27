using System;
using UnityEngine;

namespace WeeSpurts.Bowling
{
    /// <summary>
    /// THE most important struct in the project. This is everything needed to
    /// reproduce a throw, and it is ALL that will ever cross the network
    /// (Docs/Networking.md: sync launch parameters, never per-frame physics).
    /// Keep it small, serializable, and free of scene references.
    /// </summary>
    [Serializable]
    public struct LaunchParameters
    {
        /// <summary>Lateral start position across the lane, -1 (left edge) .. +1 (right edge).</summary>
        public float LateralPosition01;

        /// <summary>Aim angle in degrees off straight-down-the-lane. Negative = left.</summary>
        public float AngleDegrees;

        /// <summary>Throw power, 0..1. Mapped to real force by BallConfig.</summary>
        public float Power01;

        /// <summary>
        /// Player-dialled spin as a point on the ball's face, clamped inside the
        /// UNIT CIRCLE (see SpinModel.Clamp — a circle, not a square, so a full
        /// diagonal isn't secretly stronger than full sideways).
        ///
        ///   X = side spin / axis tilt: -1 hooks left, +1 hooks right.
        ///   Y = roll vs skid: +1 topspin (grips, curves EARLY, straighter),
        ///       -1 backspin (skids, then breaks LATE and harder).
        ///
        /// Spin deliberately does NOT affect power — that stays entirely on the
        /// timing meter (Tony's call), unlike pool where off-centre contact
        /// costs you speed. SpinModel turns this into actual forces.
        ///
        /// Vector2 rather than two floats because Unity serializes it natively
        /// and Mirror has a built-in writer for it, so the eventual networked
        /// version needs no custom serializer.
        /// </summary>
        public Vector2 Spin;

        /// <summary>
        /// Random seed for any "chaos" effects (wobble, comedy events) so every
        /// client's simulation makes the SAME random choices. Set at throw time.
        /// </summary>
        public int Seed;

        /// <summary>
        /// How well-timed the release was against the power meter's green
        /// zone: -1 = released late (held past the zone), 0 = perfect
        /// (anywhere inside the zone), +1 = released early (before reaching
        /// the zone). See BallLauncher.ComputeTimingError for the zone bounds.
        /// </summary>
        public float TimingError01;

        /// <summary>
        /// True when the release was such a total fumble (near-zero power — an
        /// accidental tap) that the ball comically flies backward instead of
        /// forward. A hand-placed gag archetype at the extreme low end of the
        /// meter, deliberately NOT derived from TimingError01's smooth curve.
        /// </summary>
        public bool IsBackwardFumble;

        /// <summary>
        /// True when the release landed inside the power meter's green zone
        /// (the same "0 = perfect" case TimingError01 already encodes). Split
        /// out as its own named field — rather than making every caller
        /// re-derive it by comparing TimingError01 to 0f — because this
        /// struct is about to be the network wire format (see the class doc
        /// comment): a float equality check re-typed at each call site is one
        /// bad refactor of the timing math away from silently breaking the
        /// Nuke Shot's green check with no compiler error to catch it.
        /// </summary>
        public bool IsGreen;

        public override string ToString() =>
            $"pos {LateralPosition01:F2}, angle {AngleDegrees:F1}°, power {Power01:P0}, spin ({Spin.x:F2}, {Spin.y:F2}), seed {Seed}, timing {TimingError01:+0.00;-0.00;0.00}"
            + (IsBackwardFumble ? ", BACKWARD FUMBLE" : "")
            + (IsGreen ? ", GREEN" : "");
    }
}
