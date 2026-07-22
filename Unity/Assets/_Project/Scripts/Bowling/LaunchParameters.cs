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

        /// <summary>Spin, -1 (full left) .. +1 (full right).</summary>
        public float Spin;

        /// <summary>
        /// Random seed for any "chaos" effects (wobble, comedy events) so every
        /// client's simulation makes the SAME random choices. Set at throw time.
        /// </summary>
        public int Seed;

        public override string ToString() =>
            $"pos {LateralPosition01:F2}, angle {AngleDegrees:F1}°, power {Power01:P0}, spin {Spin:F2}, seed {Seed}";
    }
}
