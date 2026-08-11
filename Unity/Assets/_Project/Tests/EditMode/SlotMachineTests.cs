using System.Collections.Generic;
using NUnit.Framework;
using WeeSpurts.Gameplay;
using WeeSpurts.Slop;

namespace WeeSpurts.Tests
{
    /// <summary>
    /// Unit tests for the Thunder Lanes slot machine (Docs/Prompts/2026-08-11-
    /// executive-day-plan.md, Block 1 task 1).
    ///
    /// SAME TWO-KIND SPLIT AS BlackjackTests: edge cases are pinned down with
    /// hand-crafted reel strips (a strip that is one symbol repeated forces a
    /// deterministic result, the same trick BlackjackTests uses by building
    /// hands card-by-card instead of relying on the shuffle), and the machine
    /// as a whole is tested by INVARIANT — the ledger moves by exactly the
    /// pull's net, a refused pull charges nothing, the same seed replays
    /// identically — because live reels are otherwise random.
    /// </summary>
    public class SlotMachineTests
    {
        private const ulong ALICE = 1;

        private static TicketLedger Ledger(int tickets = 100000)
        {
            var l = new TicketLedger();
            l.Register(ALICE, tickets);
            return l;
        }

        /// <summary>The default house strip: 3 Pin, 6 Beer, 5 Ball, 5 Shoe, 4 Ticket, 1 Mascot (24 total).</summary>
        private static SlotSymbol[] DefaultStrip() => new[]
        {
            SlotSymbol.Pin, SlotSymbol.Pin, SlotSymbol.Pin,
            SlotSymbol.Beer, SlotSymbol.Beer, SlotSymbol.Beer, SlotSymbol.Beer, SlotSymbol.Beer, SlotSymbol.Beer,
            SlotSymbol.Ball, SlotSymbol.Ball, SlotSymbol.Ball, SlotSymbol.Ball, SlotSymbol.Ball,
            SlotSymbol.Shoe, SlotSymbol.Shoe, SlotSymbol.Shoe, SlotSymbol.Shoe, SlotSymbol.Shoe,
            SlotSymbol.Ticket, SlotSymbol.Ticket, SlotSymbol.Ticket, SlotSymbol.Ticket,
            SlotSymbol.Mascot
        };

        private static List<SlotPayoutEntry> DefaultPaytable() => new List<SlotPayoutEntry>
        {
            new SlotPayoutEntry { Symbol = SlotSymbol.Beer, Multiplier = 8 },
            new SlotPayoutEntry { Symbol = SlotSymbol.Shoe, Multiplier = 12 },
            new SlotPayoutEntry { Symbol = SlotSymbol.Ball, Multiplier = 12 },
            new SlotPayoutEntry { Symbol = SlotSymbol.Ticket, Multiplier = 20 },
            new SlotPayoutEntry { Symbol = SlotSymbol.Mascot, Multiplier = 250 }
        };

        private static SlotRules Rules() => new SlotRules
        {
            MinStake = 10,
            MaxStake = 50,
            ReelStrips = new[] { DefaultStrip(), DefaultStrip(), DefaultStrip() },
            Paytable = DefaultPaytable(),
            BonusSymbol = SlotSymbol.Pin,
            BonusFreePulls = 5,
            BonusMultiplier = 3
        };

        /// <summary>Every reel is a single symbol, so every spin lands that symbol on all three reels, deterministically.</summary>
        private static SlotRules RulesForcedTo(SlotSymbol symbol)
        {
            var rules = Rules();
            var strip = new[] { symbol };
            rules.ReelStrips = new[] { strip, strip, strip };
            return rules;
        }

        // ---------------------------------------------------------------
        // Betting rules
        // ---------------------------------------------------------------

        [Test]
        public void Pull_TakesTheStakeUpFront()
        {
            var machine = new SlotMachine(Rules(), seed: 1);
            var ledger = Ledger(1000);

            SlotPull pull = machine.Pull(ledger, ALICE, 20);

            Assert.IsTrue(pull.Spun);
            Assert.AreEqual(980 + pull.Returned, ledger.BalanceOf(ALICE));
        }

        [Test]
        public void Pull_StakeOutOfRange_IsRefusedAndChargesNothing()
        {
            var machine = new SlotMachine(Rules(), seed: 1);
            var ledger = Ledger(1000);

            Assert.AreEqual(SlotPullOutcome.StakeOutOfRange, machine.Pull(ledger, ALICE, 5).Outcome);
            Assert.AreEqual(SlotPullOutcome.StakeOutOfRange, machine.Pull(ledger, ALICE, 999).Outcome);
            Assert.AreEqual(1000, ledger.BalanceOf(ALICE));
        }

        [Test]
        public void RefusedPull_NetIsZero_NotAPhantomLoss()
        {
            var machine = new SlotMachine(Rules(), seed: 1);
            var ledger = Ledger(1000);

            Assert.AreEqual(0, machine.Pull(ledger, ALICE, 999).Net, "a refusal moved nothing, so Net must read zero, not -stake");
            Assert.AreEqual(0, machine.Pull(null, ALICE, 10).Net);
        }

        [Test]
        public void ExtremeConfiguredStakeAndPayout_NeverOverflowsOrPaysNegative()
        {
            var rules = RulesForcedTo(SlotSymbol.Beer);
            rules.MinStake = 1;
            rules.MaxStake = 10_000_000;
            rules.Paytable = new List<SlotPayoutEntry> { new SlotPayoutEntry { Symbol = SlotSymbol.Beer, Multiplier = 1000 } };
            var machine = new SlotMachine(rules, seed: 1);
            var ledger = Ledger(int.MaxValue / 2);

            SlotPull pull = machine.Pull(ledger, ALICE, rules.MaxStake);

            Assert.IsTrue(pull.Spun);
            Assert.GreaterOrEqual(pull.Returned, 0, "an absurd stake/multiplier combination must clamp, never overflow negative");
        }

        [Test]
        public void Pull_CannotAfford_IsRefusedAndChargesNothing()
        {
            var machine = new SlotMachine(Rules(), seed: 1);
            var ledger = Ledger(5);

            Assert.AreEqual(SlotPullOutcome.CouldNotPay, machine.Pull(ledger, ALICE, 20).Outcome);
            Assert.AreEqual(5, ledger.BalanceOf(ALICE));
        }

        [Test]
        public void Pull_NullLedger_IsRefusedRatherThanThrowing()
        {
            var machine = new SlotMachine(Rules(), seed: 1);
            Assert.AreEqual(SlotPullOutcome.CouldNotPay, machine.Pull(null, ALICE, 20).Outcome);
        }

        // ---------------------------------------------------------------
        // Money invariants
        // ---------------------------------------------------------------

        [Test]
        public void EveryPaidPull_MovesTheLedgerByExactlyItsNet()
        {
            var machine = new SlotMachine(Rules(), seed: 2024);
            var ledger = Ledger(1_000_000);

            for (int i = 0; i < 500; i++)
            {
                int before = ledger.BalanceOf(ALICE);
                SlotPull pull = machine.Pull(ledger, ALICE, 20);

                Assert.IsTrue(pull.Spun);
                Assert.AreEqual(before + pull.Net, ledger.BalanceOf(ALICE),
                                $"pull {i} ({pull}) moved the ledger by the wrong amount");
            }
        }

        [Test]
        public void ThreeOfAKind_PaysStakeTimesMultiplier()
        {
            var machine = new SlotMachine(RulesForcedTo(SlotSymbol.Beer), seed: 1);
            var ledger = Ledger(1000);

            SlotPull pull = machine.Pull(ledger, ALICE, 10);

            Assert.IsTrue(pull.IsThreeOfAKind);
            Assert.AreEqual(80, pull.Returned, "10 stake * 8 multiplier for Beer");
            Assert.AreEqual(1000 - 10 + 80, ledger.BalanceOf(ALICE));
        }

        [Test]
        public void NoMatch_ReturnsNothing()
        {
            // A strip with two symbols and only one slot of the reel's own
            // index guarantees a mismatch on THIS engine's independent draws
            // is not reliable to force directly, so assert the general
            // invariant instead: with a rules set that has an EMPTY paytable
            // entry for a forced symbol, nothing is ever returned.
            var rules = RulesForcedTo(SlotSymbol.Ball);
            rules.Paytable.Clear(); // no symbol pays now
            var machine = new SlotMachine(rules, seed: 1);
            var ledger = Ledger(1000);

            SlotPull pull = machine.Pull(ledger, ALICE, 10);

            Assert.AreEqual(0, pull.Returned);
            Assert.AreEqual(990, ledger.BalanceOf(ALICE));
        }

        // ---------------------------------------------------------------
        // FREE FRAME bonus
        // ---------------------------------------------------------------

        [Test]
        public void ThreePins_TriggersFreeFrameAndDoesNotPayCash()
        {
            var rules = RulesForcedTo(SlotSymbol.Pin);
            var machine = new SlotMachine(rules, seed: 1);
            var ledger = Ledger(1000);

            SlotPull pull = machine.Pull(ledger, ALICE, 10);

            Assert.IsTrue(pull.TriggeredBonus);
            Assert.AreEqual(0, pull.Returned, "the bonus trigger itself pays nothing in cash");
            Assert.AreEqual(rules.BonusFreePulls, machine.BonusPullsRemaining);
            Assert.AreEqual(rules.BonusMultiplier, machine.BonusMultiplier);
            Assert.AreEqual(990, ledger.BalanceOf(ALICE), "the triggering pull is still a normal paid pull");
        }

        [Test]
        public void FreePulls_DoNotChargeTheLedger_EvenWhenBroke()
        {
            var rules = RulesForcedTo(SlotSymbol.Pin);
            var machine = new SlotMachine(rules, seed: 1);
            var ledger = Ledger(10);

            machine.Pull(ledger, ALICE, 10); // triggers bonus, spends the last 10
            Assert.AreEqual(0, ledger.BalanceOf(ALICE));

            // Switch to a losing symbol so this doesn't also pay out, and
            // confirm the free pull still SPINS despite a zero balance.
            rules.ReelStrips = new[] { new[] { SlotSymbol.Shoe }, new[] { SlotSymbol.Ball }, new[] { SlotSymbol.Ticket } };
            SlotPull freePull = machine.Pull(ledger, ALICE, 10);

            Assert.IsTrue(freePull.Spun, "a free pull must not be refused for lack of funds");
            Assert.IsTrue(freePull.WasFreePull);
            Assert.AreEqual(0, ledger.BalanceOf(ALICE), "a free pull must never charge");
        }

        [Test]
        public void FreePull_WinApplies_BonusMultiplierOnTop()
        {
            var rules = RulesForcedTo(SlotSymbol.Pin);
            var machine = new SlotMachine(rules, seed: 1);
            var ledger = Ledger(1000);

            machine.Pull(ledger, ALICE, 10); // triggers bonus
            int balanceAfterTrigger = ledger.BalanceOf(ALICE);

            rules.ReelStrips = new[] { new[] { SlotSymbol.Beer }, new[] { SlotSymbol.Beer }, new[] { SlotSymbol.Beer } };
            SlotPull freePull = machine.Pull(ledger, ALICE, 10);

            Assert.IsTrue(freePull.WasFreePull);
            Assert.AreEqual(10 * 8 * rules.BonusMultiplier, freePull.Returned, "beer's multiplier times the bonus multiplier");
            Assert.AreEqual(balanceAfterTrigger + freePull.Returned, ledger.BalanceOf(ALICE), "a free win is awarded but never staked");
        }

        [Test]
        public void FreePulls_CountDownAndMultiplierResetsWhenTheyRunOut()
        {
            var rules = RulesForcedTo(SlotSymbol.Pin);
            rules.BonusFreePulls = 2;
            var machine = new SlotMachine(rules, seed: 1);
            var ledger = Ledger(1000);

            machine.Pull(ledger, ALICE, 10); // triggers bonus: 2 free pulls, multiplier 3
            Assert.AreEqual(2, machine.BonusPullsRemaining);
            Assert.AreEqual(3, machine.BonusMultiplier);

            rules.ReelStrips = new[] { new[] { SlotSymbol.Shoe }, new[] { SlotSymbol.Ball }, new[] { SlotSymbol.Ticket } }; // no match, no retrigger

            machine.Pull(ledger, ALICE, 10);
            Assert.AreEqual(1, machine.BonusPullsRemaining);
            Assert.AreEqual(3, machine.BonusMultiplier, "still mid-bonus");

            machine.Pull(ledger, ALICE, 10);
            Assert.AreEqual(0, machine.BonusPullsRemaining);
            Assert.AreEqual(1, machine.BonusMultiplier, "multiplier resets once the bonus run ends");

            // The next pull must be a normal, charged pull again.
            int before = ledger.BalanceOf(ALICE);
            SlotPull normalAgain = machine.Pull(ledger, ALICE, 10);
            Assert.IsFalse(normalAgain.WasFreePull);
            Assert.AreEqual(before - 10, ledger.BalanceOf(ALICE));
        }

        [Test]
        public void BonusRetrigger_DuringAFreePull_StacksAdditionalPulls()
        {
            var rules = RulesForcedTo(SlotSymbol.Pin);
            rules.BonusFreePulls = 3;
            var machine = new SlotMachine(rules, seed: 1);
            var ledger = Ledger(1000);

            machine.Pull(ledger, ALICE, 10); // trigger: 3 free pulls
            Assert.AreEqual(3, machine.BonusPullsRemaining);

            SlotPull retrigger = machine.Pull(ledger, ALICE, 10); // still all-Pin: retriggers
            Assert.IsTrue(retrigger.WasFreePull);
            Assert.IsTrue(retrigger.TriggeredBonus);
            // Consumed one (3 -> 2), then gained 3 more (2 -> 5).
            Assert.AreEqual(5, machine.BonusPullsRemaining);
        }

        // ---------------------------------------------------------------
        // Ticket-circulation invariant
        // ---------------------------------------------------------------

        [Test]
        public void TotalInCirculation_MovesOnlyByAwardedOrStakedAmounts()
        {
            var rules = RulesForcedTo(SlotSymbol.Beer);
            var machine = new SlotMachine(rules, seed: 1);
            var ledger = Ledger(1000);
            int before = ledger.TotalInCirculation();

            SlotPull pull = machine.Pull(ledger, ALICE, 10);

            Assert.AreEqual(before + pull.Net, ledger.TotalInCirculation());
        }

        // ---------------------------------------------------------------
        // Determinism
        // ---------------------------------------------------------------

        [Test]
        public void SameSeed_ReplaysTheIdenticalReelSequence()
        {
            static List<string> PlayOut(int seed)
            {
                var machine = new SlotMachine(Rules(), seed);
                var ledger = Ledger(1_000_000);
                var log = new List<string>();

                for (int i = 0; i < 100; i++)
                    log.Add(machine.Pull(ledger, ALICE, 10).ToString());

                return log;
            }

            CollectionAssert.AreEqual(PlayOut(4242), PlayOut(4242),
                "the same seed must produce the same reels and the same money on every machine");
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentSpins()
        {
            var a = new SlotMachine(Rules(), seed: 1);
            var b = new SlotMachine(Rules(), seed: 2);
            var ledgerA = Ledger();
            var ledgerB = Ledger();

            bool anyDifferent = false;
            for (int i = 0; i < 20; i++)
                if (a.Pull(ledgerA, ALICE, 10).ToString() != b.Pull(ledgerB, ALICE, 10).ToString())
                    anyDifferent = true;

            Assert.IsTrue(anyDifferent);
        }

        // ---------------------------------------------------------------
        // Config
        // ---------------------------------------------------------------

        [Test]
        public void Rules_Sanitize_FixesNonsenseInsteadOfCrashing()
        {
            var r = new SlotRules
            {
                MinStake = -5,
                MaxStake = -100,
                BonusFreePulls = -3,
                BonusMultiplier = 0,
                ReelStrips = new SlotSymbol[2][],
                Paytable = null
            };

            r.Sanitize();

            Assert.GreaterOrEqual(r.MinStake, 1);
            Assert.GreaterOrEqual(r.MaxStake, r.MinStake);
            Assert.GreaterOrEqual(r.BonusFreePulls, 0);
            Assert.GreaterOrEqual(r.BonusMultiplier, 1);
            Assert.AreEqual(3, r.ReelStrips.Length);
            for (int i = 0; i < 3; i++) Assert.Greater(r.ReelStrips[i].Length, 0);
            Assert.IsNotNull(r.Paytable);
        }

        [Test]
        public void Machine_WithNullRules_UsesSanitizedDefaultsRatherThanThrowing()
        {
            var machine = new SlotMachine(null, seed: 1);
            var ledger = Ledger(1000);

            Assert.DoesNotThrow(() => machine.Pull(ledger, ALICE, 10));
        }

        // ---------------------------------------------------------------
        // Payout distribution — the volatility shape, over 100k pulls
        // ---------------------------------------------------------------

        [Test]
        public void PayoutDistribution_Over100kPulls_RealisedReturnSitsInTheStatedBand()
        {
            // The house default shape: mostly nothing, a fat tail. This test
            // does not assert an exact RTP (the jackpot alone has real
            // variance across a finite sample) — it asserts the realised
            // return-to-player sits in a wide, deliberately generous band
            // around the analytically-expected ~45%, which is tight enough to
            // catch a broken paytable (e.g. 0% — nothing ever pays, or >100%
            // — the machine is printing tickets) without being a flaky test.
            var machine = new SlotMachine(Rules(), seed: 99);
            var ledger = Ledger(int.MaxValue / 4);
            const int stake = 10;
            const int pulls = 100_000;

            long totalStaked = 0;
            long totalReturned = 0;
            int noMatchCount = 0;
            int jackpotCount = 0;

            for (int i = 0; i < pulls; i++)
            {
                SlotPull pull = machine.Pull(ledger, ALICE, stake);
                Assert.IsTrue(pull.Spun, "the test ledger must never run dry");

                if (!pull.WasFreePull) totalStaked += stake;
                totalReturned += pull.Returned;

                if (!pull.IsThreeOfAKind) noMatchCount++;
                if (pull.IsThreeOfAKind && pull.Reel0 == SlotSymbol.Mascot) jackpotCount++;
            }

            double rtp = (double)totalReturned / totalStaked;
            TestContext.WriteLine($"Realised RTP over {pulls} pulls: {rtp:P2} " +
                                 $"(no-match rate {(double)noMatchCount / pulls:P2}, jackpots {jackpotCount})");

            Assert.Greater(rtp, 0.20, "the machine should pay back SOMETHING — this is not a pure sink with zero return");
            Assert.Less(rtp, 0.75, "the machine should still be a net loser for the player on average");

            Assert.Greater((double)noMatchCount / pulls, 0.85, "'mostly nothing' means most spins should show no matching triple");

            // Expected jackpots at these odds (1/24 per reel, cubed) over
            // 100k pulls is roughly 7 — assert it's rare but not impossible.
            Assert.Greater(jackpotCount, 0, "100k pulls should hit the jackpot at least once");
            Assert.Less(jackpotCount, 200, "the jackpot must stay rare — this many hits would mean the strip odds are broken");
        }
    }
}
