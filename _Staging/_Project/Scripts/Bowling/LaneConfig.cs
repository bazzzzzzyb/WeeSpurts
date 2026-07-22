using UnityEngine;

namespace WeeSpurts.Bowling
{
    /// <summary>
    /// Lane geometry tunables. The greybox scene is built FROM these numbers,
    /// so changing the asset and re-running the scene builder reshapes the lane.
    /// </summary>
    [CreateAssetMenu(fileName = "LaneConfig", menuName = "WeeSpurts/Lane Config")]
    public class LaneConfig : ScriptableObject
    {
        [Header("Lane")]
        [Tooltip("Meters. Real lane ~18.3 from foul line to head pin.")]
        public float Length = 18f;

        [Tooltip("Meters. Real lane ~1.05. Wider is more forgiving.")]
        public float Width = 1.4f;

        [Header("Pins")]
        [Tooltip("Distance between neighboring pins in a row. Real ~0.3.")]
        public float PinSpacing = 0.3f;

        [Tooltip("Pin height in meters. Real 0.38.")]
        public float PinHeight = 0.38f;

        [Tooltip("Kilograms. Real ~1.6. Lighter pins fly further = funnier.")]
        public float PinMass = 1.4f;

        [Tooltip("A pin tilted past this many degrees counts as knocked down.")]
        public float KnockedAngleDegrees = 40f;

        [Header("Players (temporary home until lobby exists)")]
        [Tooltip("How many hot-seat players the debug game creates.")]
        [Range(1, 8)] public int DebugPlayerCount = 2;
    }
}
