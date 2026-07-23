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

        [Header("Players (temporary home until lobby exists)")]
        [Tooltip("How many hot-seat players the debug game creates.")]
        [Range(1, 8)] public int DebugPlayerCount = 2;
    }
}
