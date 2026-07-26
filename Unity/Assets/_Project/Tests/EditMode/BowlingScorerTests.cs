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

        [Test]
        public void ResetCurrentFrame_DiscardsMidFrameRoll_KeepsFrameIndex()
        {
            var s = new BowlingScorer();
            s.AddRoll(7); // open first roll, 3 pins left standing

            s.ResetCurrentFrame();

            Assert.AreEqual(0, s.CurrentFrame);
            Assert.AreEqual(0, s.RollInFrame);
            Assert.AreEqual(10, s.PinsStanding);
            Assert.IsTrue(s.NextRollNeedsFreshRack);
            Assert.AreEqual(0, s.Rolls.Count);
        }

        [Test]
        public void ResetCurrentFrame_LeavesEarlierFramesIntact()
        {
            var s = Play(9, 0); // frame 1 done, worth 9
            s.AddRoll(7); // frame 2, first roll

            s.ResetCurrentFrame();

            Assert.AreEqual(1, s.CurrentFrame);
            Assert.AreEqual(2, s.Rolls.Count); // frame 1's rolls untouched
            Assert.AreEqual(9, s.GetTotal());
        }

        [Test]
        public void ResetCurrentFrame_MidTenthFrameBonusRolls_ReRacks()
        {
            var s = Play(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0); // frames 1-9 gutters
            s.AddRoll(10); // 10th: strike, earns bonus rolls

            s.ResetCurrentFrame();

            Assert.AreEqual(9, s.CurrentFrame);
            Assert.AreEqual(0, s.RollInFrame);
            Assert.AreEqual(10, s.PinsStanding);
            Assert.IsFalse(s.IsGameOver);
            Assert.AreEqual(18, s.Rolls.Count); // only frames 1-9's rolls remain
        }

        [Test]
        public void ResetCurrentFrame_MidTenthFrameSpareBonus_ReRacks()
        {
            var s = Play(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0); // frames 1-9 gutters
            s.AddRoll(4); // 10th: open first roll
            s.AddRoll(6); // spare — earns a bonus roll

            s.ResetCurrentFrame();

            Assert.AreEqual(9, s.CurrentFrame);
            Assert.AreEqual(0, s.RollInFrame);
            Assert.AreEqual(10, s.PinsStanding);
            Assert.IsFalse(s.IsGameOver);
            Assert.AreEqual(18, s.Rolls.Count); // only frames 1-9's rolls remain
        }

        [Test]
        public void ResetCurrentFrame_AfterDoubleBonusRoll_ReRacks()
        {
            var s = Play(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0); // frames 1-9 gutters
            s.AddRoll(10); // 10th: strike
            s.AddRoll(5); // first bonus roll (RollInFrame now 2, one more ball owed)

            s.ResetCurrentFrame();

            Assert.AreEqual(9, s.CurrentFrame);
            Assert.AreEqual(0, s.RollInFrame);
            Assert.AreEqual(10, s.PinsStanding);
            Assert.IsFalse(s.IsGameOver);
            Assert.AreEqual(18, s.Rolls.Count); // only frames 1-9's rolls remain
        }

        [Test]
        public void ResetCurrentFrame_AfterGameOver_IsNoOp()
        {
            var s = Play(new int[20]);
            int rollCountBefore = s.Rolls.Count;

            s.ResetCurrentFrame();

            Assert.IsTrue(s.IsGameOver);
            Assert.AreEqual(rollCountBefore, s.Rolls.Count);
        }

        // ---------------------------------------------------------------
        // GetProvisionalTotal — the LIVE scoreboard number.
        //
        // GetTotal() only sums frames whose bonuses have fully resolved, so it
        // reads 0 for two more turns after a strike. That is correct paper-
        // scorecard bookkeeping and a useless scoreboard: you knock ten pins
        // down and the number says nothing happened. GetProvisionalTotal()
        // counts pins immediately and folds bonuses in as they become known.
        //
        // The contract these tests defend: it must NEVER disagree with the
        // official score once a game is complete, and it must never go DOWN.
        // ---------------------------------------------------------------

        [Test]
        public void ProvisionalTotal_CountsALoneStrikeImmediately()
        {
            var s = Play(10);
            Assert.AreEqual(0, s.GetTotal());              // bonus unknown — correctly blank
            Assert.AreEqual(10, s.GetProvisionalTotal());  // ...but the pins are down
        }

        [Test]
        public void ProvisionalTotal_FoldsInStrikeBonusAsItArrives()
        {
            Assert.AreEqual(10, Play(10).GetProvisionalTotal());          // 10
            Assert.AreEqual(30, Play(10, 10).GetProvisionalTotal());      // (10+10) + 10
            Assert.AreEqual(60, Play(10, 10, 10).GetProvisionalTotal());  // 30 + 20 + 10
        }

        [Test]
        public void ProvisionalTotal_CountsFirstRollOfAnUnfinishedFrame()
        {
            var s = Play(7);
            Assert.AreEqual(7, s.GetProvisionalTotal());
        }

        [Test]
        public void ProvisionalTotal_CountsSpareBonusOnlyOnceKnown()
        {
            Assert.AreEqual(10, Play(6, 4).GetProvisionalTotal());     // spare, bonus pending
            // 20, not 19: the 5 counts TWICE by design — once as frame 1's spare
            // bonus (10+5 = 15) and again as frame 2's own pins. Same rule the
            // strike test above relies on for Play(10, 10) == 30.
            Assert.AreEqual(20, Play(6, 4, 5).GetProvisionalTotal());
        }

        [Test]
        public void ProvisionalTotal_NeverDecreases()
        {
            var s = new BowlingScorer();
            int previous = 0;
            foreach (int roll in new[] { 10, 7, 3, 9, 0, 10, 0, 8, 8, 2, 0, 6, 10, 10, 10, 8, 1 })
            {
                s.AddRoll(roll);
                int now = s.GetProvisionalTotal();
                Assert.GreaterOrEqual(now, previous, "provisional score went backwards");
                previous = now;
            }
        }

        [Test]
        public void ProvisionalTotal_MatchesOfficialTotal_OnCompletedGames()
        {
            // Same games as the official-scoring tests above. Once every bonus
            // has resolved there is no such thing as "provisional" any more, so
            // these two numbers MUST agree — otherwise the live scoreboard would
            // visibly correct itself at the end of a match.
            Assert.AreEqual(0, Play(new int[20]).GetProvisionalTotal());
            Assert.AreEqual(90, Play(9, 0, 9, 0, 9, 0, 9, 0, 9, 0, 9, 0, 9, 0, 9, 0, 9, 0, 9, 0).GetProvisionalTotal());
            Assert.AreEqual(300, Play(10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10).GetProvisionalTotal());
            Assert.AreEqual(150, Play(5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5).GetProvisionalTotal());
            Assert.AreEqual(133, Play(1, 4, 4, 5, 6, 4, 5, 5, 10, 0, 1, 7, 3, 6, 4, 10, 2, 8, 6).GetProvisionalTotal());
        }
    }
}
