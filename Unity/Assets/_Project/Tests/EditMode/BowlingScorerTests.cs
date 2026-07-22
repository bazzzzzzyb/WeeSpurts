using NUnit.Framework;
using WeeSpurts.Gameplay;

namespace WeeSpurts.Tests
{
    /// <summary>
    /// Unit tests for the pure-C# scorer. Run in Unity: Window > General >
    /// Test Runner > EditMode > Run All. Every test must be green before
    /// bowling is "done" (DefinitionOfDone [4]: correct 10-frame scoring).
    /// </summary>
    public class BowlingScorerTests
    {
        private static BowlingScorer Play(params int[] rolls)
        {
            var s = new BowlingScorer();
            foreach (int r in rolls) s.AddRoll(r);
            return s;
        }

        [Test]
        public void GutterGame_ScoresZero()
        {
            var s = Play(new int[20]); // twenty zeros
            Assert.IsTrue(s.IsGameOver);
            Assert.AreEqual(0, s.GetTotal());
        }

        [Test]
        public void AllOpenFrames_SumsPlainly()
        {
            // 9 and miss, every frame: 10 × 9 = 90.
            var s = Play(9, 0, 9, 0, 9, 0, 9, 0, 9, 0, 9, 0, 9, 0, 9, 0, 9, 0, 9, 0);
            Assert.IsTrue(s.IsGameOver);
            Assert.AreEqual(90, s.GetTotal());
        }

        [Test]
        public void PerfectGame_Scores300()
        {
            var s = Play(10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10); // 12 strikes
            Assert.IsTrue(s.IsGameOver);
            Assert.AreEqual(300, s.GetTotal());
        }

        [Test]
        public void AllSpares_WithFinalFive_Scores150()
        {
            var s = Play(5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5);
            Assert.IsTrue(s.IsGameOver);
            Assert.AreEqual(150, s.GetTotal());
        }

        [Test]
        public void ClassicUsbcExampleGame_Scores133()
        {
            // The textbook example game used on real scoring guides.
            var s = Play(1, 4, 4, 5, 6, 4, 5, 5, 10, 0, 1, 7, 3, 6, 4, 10, 2, 8, 6);
            Assert.IsTrue(s.IsGameOver);
            Assert.AreEqual(133, s.GetTotal());
        }

        [Test]
        public void Strike_EndsFrameImmediately()
        {
            var s = new BowlingScorer();
            Assert.AreEqual(RollOutcome.FrameComplete, s.AddRoll(10));
            Assert.AreEqual(1, s.CurrentFrame);
            Assert.IsTrue(s.NextRollNeedsFreshRack);
        }

        [Test]
        public void OpenFirstRoll_FrameContinues_AtLeftoverPins()
        {
            var s = new BowlingScorer();
            Assert.AreEqual(RollOutcome.FrameContinues, s.AddRoll(7));
            Assert.AreEqual(3, s.PinsStanding);
            Assert.IsFalse(s.NextRollNeedsFreshRack); // second roll plays the leftovers
        }

        [Test]
        public void TenthFrame_StrikeGrantsTwoBonusRolls()
        {
            var s = Play(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0); // frames 1-9 gutters
            Assert.AreEqual(RollOutcome.FrameContinues, s.AddRoll(10)); // 10th: strike
            Assert.IsTrue(s.NextRollNeedsFreshRack);                    // fresh rack for bonus
            Assert.AreEqual(RollOutcome.FrameContinues, s.AddRoll(3));
            Assert.AreEqual(RollOutcome.GameComplete, s.AddRoll(4));
            Assert.AreEqual(17, s.GetTotal());
        }

        [Test]
        public void TenthFrame_OpenFrame_EndsAfterTwoRolls()
        {
            var s = Play(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0); // frames 1-9
            s.AddRoll(3);
            Assert.AreEqual(RollOutcome.GameComplete, s.AddRoll(4));
            Assert.AreEqual(7, s.GetTotal());
        }

        [Test]
        public void UnresolvedStrike_LeavesFrameTotalBlank()
        {
            var s = Play(10, 5); // strike still waiting on its second bonus roll
            int?[] totals = s.GetFrameTotals();
            Assert.IsNull(totals[0]); // like the blank box on a real scorecard
        }

        [Test]
        public void ImpossibleRoll_Throws()
        {
            var s = new BowlingScorer();
            s.AddRoll(7);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => s.AddRoll(5)); // only 3 standing
        }

        [Test]
        public void RollAfterGameOver_Throws()
        {
            var s = Play(new int[20]);
            Assert.Throws<System.InvalidOperationException>(() => s.AddRoll(0));
        }
    }
}
