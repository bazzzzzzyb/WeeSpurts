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

        [Header("Neighbour lanes (cosmetic only)")]
        [Tooltip("Build dummy lanes either side of the real one. PURELY DECORATION — no pins, no colliders, no physics, no scoring; the ball can never reach them. They exist so the wide camera beats have an alley to frame instead of empty void. Turn off for a bare test lane.")]
        public bool BuildNeighbourLanes = true;

        [Tooltip("How many fake lanes to build on EACH side. 2 fills the widest camera shot comfortably. Raise it for a bigger venue (costs a few more draw calls); 0 is the same as switching the tickbox off.")]
        [Range(0, 6)] public int NeighbourLanesPerSide = 2;

        [Tooltip("Meters between lane centres. Must stay comfortably above Width + 1.35 or neighbouring rails will overlap and flicker (z-fight). At the default Width of 1.4 anything below ~2.8 will start to touch.")]
        public float NeighbourLaneSpacing = 2.9f;

        [Header("Players (temporary home until lobby exists)")]
        [Tooltip("How many hot-seat players the debug game creates.")]
        [Range(1, 8)] public int DebugPlayerCount = 2;
    }
}
