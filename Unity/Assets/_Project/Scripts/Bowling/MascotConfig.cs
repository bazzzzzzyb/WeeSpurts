using UnityEngine;

namespace WeeSpurts.Bowling
{
    /// <summary>
    /// Look tunables for the mascot player character. A ScriptableObject
    /// rather than constants in CharacterSetupTool so Tony can retune these
    /// in the Inspector — no code edit, and no losing the number the next
    /// time "Set Up Player Character" runs (CLAUDE.md: "config/tunables are
    /// ScriptableObjects, not hard-coded constants").
    ///
    /// SETUP: CharacterSetupTool creates Assets/_Project/ScriptableObjects/
    /// MascotConfig.asset on first run and never overwrites your tuning after
    /// that (same create-once rule as RoamConfig/Wobbler/Nuke).
    /// </summary>
    [CreateAssetMenu(fileName = "MascotConfig", menuName = "WeeSpurts/Mascot Config")]
    public class MascotConfig : ScriptableObject
    {
        [Tooltip("0 (invisible) .. 1 (solid). Wii Sports keeps the thrower clearly readable but lets you see the lane through them, roughly 0.5-0.7.")]
        [Range(0f, 1f)]
        public float Opacity = 0.6f;

        [Tooltip("Uniform scale applied to the PlayerCharacter prefab root. Change this and re-run WeeSpurts > Set Up Player Character to resize — the console logs the resulting height each time (target ~1.7-1.8 m for an adult).")]
        public float DisplayScale = 0.56f;
    }
}
