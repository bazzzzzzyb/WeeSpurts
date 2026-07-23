using UnityEngine;

namespace WeeSpurts.Bowling
{
    /// <summary>
    /// All ball-feel tunables in one asset so Tony can tweak comedy without
    /// touching code (CodingStandards: config lives in ScriptableObjects).
    /// GreyboxSceneBuilder creates a default instance; duplicate the asset to
    /// experiment with wildly different balls later (that's a feature idea, too).
    /// </summary>
    [CreateAssetMenu(fileName = "BallConfig", menuName = "WeeSpurts/Ball Config")]
    public class BallConfig : ScriptableObject
    {
        [Header("Body")]
        [Tooltip("Kilograms. Real bowling balls are ~7. Heavier = pins fly less.")]
        public float Mass = 6f;

        [Tooltip("Ball radius in meters. Real: 0.108. Bigger is funnier.")]
        public float Radius = 0.11f;

        [Header("Launch")]
        [Tooltip("Forward speed in m/s at minimum power.")]
        public float MinLaunchSpeed = 4f;

        [Tooltip("Forward speed in m/s at full power. Real pro throws ~9 m/s. Go higher for comedy.")]
        public float MaxLaunchSpeed = 14f;

        [Tooltip("Sideways curve force applied while rolling, per unit of spin.")]
        public float SpinCurveForce = 6f;

        [Tooltip("Height (meters) the ball starts at, measured to its center. " +
                 "~1.3 = chest height, so the ball drops onto the lane when thrown. " +
                 "Set to Radius + a hair (~0.13) to start it resting on the lane instead.")]
        public float SpawnHeight = 1.3f;

        [Header("Feel")]
        [Tooltip("0 = dead ball, 1 = superball. Around 0.3 reads as 'heavy but lively'.")]
        [Range(0f, 1f)] public float Bounciness = 0.25f;

        [Tooltip("Linear drag while rolling. Lower = slicker lane.")]
        public float RollingDrag = 0.12f;

        [Tooltip("Seconds after which a throw is force-ended even if the ball is still wandering.")]
        public float ThrowTimeout = 9f;

        [Tooltip("Ball is considered settled when slower than this (m/s) for SettleDuration.")]
        public float SettleSpeed = 0.25f;

        [Tooltip("How long the ball must stay slow to count as settled (seconds).")]
        public float SettleDuration = 0.75f;
    }
}
