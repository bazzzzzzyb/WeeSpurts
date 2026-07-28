using NUnit.Framework;
using UnityEngine;
using WeeSpurts.Bowling;
using WeeSpurts.Editor;

namespace WeeSpurts.Tests
{
    /// <summary>
    /// Unit tests for the animation-take picker in CharacterSetupTool. Run in
    /// Unity: Window > General > Test Runner > EditMode > Run All.
    ///
    /// WHY THIS EXISTS: a Mixamo FBX is not one animation. Each download also
    /// carries every take baked into the source skin it was retargeted onto —
    /// twelve in our files, with the Quaternius set (Man_Clapping, Man_Death,
    /// ...) FIRST and the motion we actually asked for LAST, named "mixamo.com".
    /// The tool used to import all of them under one shared name and then pick
    /// "the first clip in the file", which bound EVERY state in the controller
    /// to Man_Clapping — the thrower stood at the foul line clapping forever.
    ///
    /// SelectTakeIndex is deliberately pure and string-only so that exact
    /// decision can be pinned down here rather than only being visible by
    /// pressing Play and watching the character.
    /// </summary>
    public class CharacterSetupToolTests
    {
        /// <summary>The real take list from Animations/Idle.fbx, in file order.</summary>
        private static readonly string[] RealMixamoFile =
        {
            "HumanArmature|Man_Clapping",
            "HumanArmature|Man_Death",
            "HumanArmature|Man_Idle",
            "HumanArmature|Man_Jump",
            "HumanArmature|Man_Punch",
            "HumanArmature|Man_Run",
            "HumanArmature|Man_RunningJump",
            "HumanArmature|Man_Sitting",
            "HumanArmature|Man_Standing",
            "HumanArmature|Man_SwordSlash",
            "HumanArmature|Man_Walk",
            "mixamo.com",
        };

        // ---------- the bug this was written for ----------

        [Test]
        public void SelectTakeIndex_PicksTheMixamoTake_NotTheFirstTakeInTheFile()
        {
            // The regression test proper: index 11, NOT index 0 (Man_Clapping).
            Assert.AreEqual(11, CharacterSetupTool.SelectTakeIndex(RealMixamoFile));
        }

        [Test]
        public void SelectTakeIndex_IsIndependentOfTakeOrder()
        {
            // Mixamo happens to export the wanted take last today. Nothing
            // guarantees that, so the picker must match by name, not position.
            string[] mixamoFirst = { "mixamo.com", "HumanArmature|Man_Clapping", "HumanArmature|Man_Idle" };
            Assert.AreEqual(0, CharacterSetupTool.SelectTakeIndex(mixamoFirst));
        }

        // ---------- non-Mixamo and malformed files ----------

        [Test]
        public void SelectTakeIndex_AcceptsASingleTake_EvenWhenItIsNotAMixamoExport()
        {
            // A hand-made or re-exported clip has one take and no ambiguity.
            Assert.AreEqual(0, CharacterSetupTool.SelectTakeIndex(new[] { "Take 001" }));
        }

        [Test]
        public void SelectTakeIndex_RefusesToGuess_WhenSeveralTakesAndNoMixamoTake()
        {
            // -1 makes the caller warn and skip rather than silently bind a
            // random animation — which is precisely how the clapping bug hid.
            string[] ambiguous = { "Take 001", "Take 002" };
            Assert.AreEqual(-1, CharacterSetupTool.SelectTakeIndex(ambiguous));
        }

        [Test]
        public void SelectTakeIndex_HandlesEmptyAndNullTakeLists()
        {
            Assert.AreEqual(-1, CharacterSetupTool.SelectTakeIndex(new string[0]));
            Assert.AreEqual(-1, CharacterSetupTool.SelectTakeIndex(null));
        }

        [Test]
        public void SelectTakeIndex_ToleratesNullEntriesAndSurroundingWhitespace()
        {
            string[] messy = { null, "HumanArmature|Man_Clapping", "  mixamo.com  " };
            Assert.AreEqual(2, CharacterSetupTool.SelectTakeIndex(messy));
        }

        [Test]
        public void SelectTakeIndex_MatchesTheMixamoTakeCaseInsensitively()
        {
            Assert.AreEqual(1, CharacterSetupTool.SelectTakeIndex(new[] { "Man_Clapping", "Mixamo.com" }));
        }

        [Test]
        public void SelectTakeIndex_DoesNotMatchATakeThatMerelyContainsTheName()
        {
            // "HumanArmature|mixamo.com_something" is a different take; only an
            // exact name is the downloaded motion.
            string[] lookalikes = { "HumanArmature|mixamo.com_extra", "mixamo.com.001" };
            Assert.AreEqual(-1, CharacterSetupTool.SelectTakeIndex(lookalikes));
        }

        // ---------- scale ----------

        [Test]
        public void MascotConfig_DefaultDisplayScale_IsPositive()
        {
            // Guards the one thing that would make the prefab root degenerate:
            // a zero or negative uniform scale flattens or mirrors the model,
            // and LogCharacterHeight divides by it. DisplayScale moved from a
            // CharacterSetupTool constant to a MascotConfig ScriptableObject
            // field (so Tony can retune it from the Inspector) — this checks
            // the shipped default rather than a live asset on disk, so it
            // stays a pure, fast test like the ones above.
            var config = ScriptableObject.CreateInstance<MascotConfig>();
            Assert.Greater(config.DisplayScale, 0f);
            Object.DestroyImmediate(config);
        }
    }
}
