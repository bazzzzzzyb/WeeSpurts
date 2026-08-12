using System.Collections.Generic;
using NUnit.Framework;
using WeeSpurts.Core;
using WeeSpurts.Gameplay;
using WeeSpurts.Slop;

namespace WeeSpurts.Tests
{
    /// <summary>
    /// Unit tests for the card table (Docs/SlopLayerPlan.md S4).
    ///
    /// TWO KINDS OF TEST IN HERE, deliberately:
    ///
    /// 1. HAND MATH is tested exactly, with hands built card by card. This is
    ///    where blackjack implementations actually go wrong (aces, and calling
    ///    a three-card 21 a blackjack), so it is pinned down precisely.
    ///
    /// 2. THE TABLE is tested by INVARIANT rather than by dealing a known hand,
    ///    because the shoe is shuffled. The invariants — the ledger moves by
    ///    exactly the round's net, a refused bet charges nothing, the same seed
    ///    replays identically — are the properties that actually matter, and
    ///    they hold for every hand rather than for one hand I picked.
    /// </summary>
    public class BlackjackTests
    {
        private const ulong ALICE = 1;

        private static PlayingCard C(Rank r, Suit s = Suit.Spades) => new PlayingCard(r, s);

        private static BlackjackHand Hand(params PlayingCard[] cards)
        {
            var h = new BlackjackHand();
            foreach (PlayingCard c in cards) h.Add(c);
            return h;
        }

        private static TicketLedger Ledger(int tickets = 1000)
        {
            var l = new TicketLedger();
            l.Register(ALICE, tickets);
            return l;
        }

        private static BlackjackRules Rules() => new BlackjackRules
        {
            MinBet = 10, MaxBet = 100, DeckCount = 6, ReshuffleBelowCards = 20
        };

        // ---------------------------------------------------------------
        // Card values
        // ---------------------------------------------------------------

        [Test]
        public void FaceCards_AreWorthTen()
        {
            Assert.AreEqual(10, C(Rank.Jack).BlackjackValue);
            Assert.AreEqual(10, C(Rank.Queen).BlackjackValue);
            Assert.AreEqual(10, C(Rank.King).BlackjackValue);
            Assert.AreEqual(10, C(Rank.Ten).BlackjackValue);
            Assert.AreEqual(7, C(Rank.Seven).BlackjackValue);
        }

        [Test]
        public void Ace_ReportsElevenAndTheHandDemotesIt()
        {
            Assert.AreEqual(11, C(Rank.Ace).BlackjackValue);
        }

        // ---------------------------------------------------------------
        // Hand totals — the ace arithmetic
        // ---------------------------------------------------------------

        [Test]
        public void EmptyHand_IsZero()
        {
            Assert.AreEqual(0, new BlackjackHand().Total);
        }

        [Test]
        public void HardTotal_SumsPlainly()
        {
            Assert.AreEqual(16, Hand(C(Rank.Nine), C(Rank.Seven)).Total);
        }

        [Test]
        public void SoftSeventeen_IsSeventeenAndSoft()
        {
            var h = Hand(C(Rank.Ace), C(Rank.Six));
            Assert.AreEqual(17, h.Total);
            Assert.IsTrue(h.IsSoft);
            Assert.IsFalse(h.IsBust);
        }

        [Test]
        public void SoftHandGoesHardWhenTheAceMustDemote()
        {
            // A + 6 + 10 = 27 as soft, so the ace drops to 1 -> 17, now HARD.
            var h = Hand(C(Rank.Ace), C(Rank.Six), C(Rank.Ten));
            Assert.AreEqual(17, h.Total);
            Assert.IsFalse(h.IsSoft, "the ace has been demoted, so one more card CAN bust this");
        }

        [Test]
        public void TwoAces_AreTwelveNotTwentyTwo()
        {
            var h = Hand(C(Rank.Ace), C(Rank.Ace, Suit.Hearts));
            Assert.AreEqual(12, h.Total);
            Assert.IsTrue(h.IsSoft);
        }

        [Test]
        public void ManyAces_DemoteOneAtATime()
        {
            // 11+1+1 = 13
            Assert.AreEqual(13, Hand(C(Rank.Ace), C(Rank.Ace, Suit.Hearts), C(Rank.Ace, Suit.Clubs)).Total);
            // 1+1+1+10 = 13
            Assert.AreEqual(13, Hand(C(Rank.Ace), C(Rank.Ace, Suit.Hearts),
                                     C(Rank.Ace, Suit.Clubs), C(Rank.Ten)).Total);
        }

        [Test]
        public void AceAndTen_IsBlackjack()
        {
            var h = Hand(C(Rank.Ace), C(Rank.King));
            Assert.AreEqual(21, h.Total);
            Assert.IsTrue(h.IsBlackjack);
        }

        [Test]
        public void ThreeCardTwentyOne_IsNotBlackjack()
        {
            // The classic bug. 21 in three cards does NOT pay 3:2.
            var h = Hand(C(Rank.Five), C(Rank.Six), C(Rank.Ten));
            Assert.AreEqual(21, h.Total);
            Assert.IsFalse(h.IsBlackjack);
        }

        [Test]
        public void OverTwentyOne_IsBust()
        {
            var h = Hand(C(Rank.Ten), C(Rank.Nine), C(Rank.Five));
            Assert.AreEqual(24, h.Total);
            Assert.IsTrue(h.IsBust);
        }

        // ---------------------------------------------------------------
        // The deterministic RNG
        // ---------------------------------------------------------------

        [Test]
        public void Rng_SameSeed_SameSequence()
        {
            var a = new DeterministicRng(12345);
            var b = new DeterministicRng(12345);
            for (int i = 0; i < 100; i++) Assert.AreEqual(a.NextUInt(), b.NextUInt());
        }

        [Test]
        public void Rng_DifferentSeeds_Diverge()
        {
            var a = new DeterministicRng(1);
            var b = new DeterministicRng(2);
            bool anyDifferent = false;
            for (int i = 0; i < 20; i++)
                if (a.NextUInt() != b.NextUInt()) anyDifferent = true;
            Assert.IsTrue(anyDifferent);
        }

        [Test]
        public void Rng_SeedZero_StillProducesValues()
        {
            // xorshift is dead at state zero; the constructor must remap it.
            var rng = new DeterministicRng(0);
            Assert.AreNotEqual(0u, rng.NextUInt());
            Assert.AreNotEqual(0u, rng.NextUInt());
        }

        [Test]
        public void Rng_NextInt_StaysInRange()
        {
            var rng = new DeterministicRng(99);
            for (int i = 0; i < 5000; i++)
            {
                int v = rng.NextInt(52);
                Assert.GreaterOrEqual(v, 0);
                Assert.Less(v, 52);
            }
        }

        [Test]
        public void Rng_Shuffle_IsDeterministicAndKeepsEveryCard()
        {
            List<int> Make() { var l = new List<int>(); for (int i = 0; i < 52; i++) l.Add(i); return l; }

            var a = Make();
            var b = Make();
            new DeterministicRng(7).Shuffle(a);
            new DeterministicRng(7).Shuffle(b);
            CollectionAssert.AreEqual(a, b, "same seed must shuffle identically");

            var sorted = new List<int>(a);
            sorted.Sort();
            CollectionAssert.AreEqual(Make(), sorted, "a shuffle must not lose or duplicate anything");
        }

        // ---------------------------------------------------------------
        // The table: betting rules
        // ---------------------------------------------------------------

        [Test]
        public void Deal_TakesTheStakeUpFront()
        {
            var table = new BlackjackTable(Rules(), seed: 1);
            var ledger = Ledger(1000);

            Assert.AreEqual(DealOutcome.Dealt, table.Deal(ledger, ALICE, 50));

            // Either still playing, or already settled by a natural.
            if (table.Phase == BlackjackPhase.PlayerTurn)
                Assert.AreEqual(950, ledger.BalanceOf(ALICE));
        }

        [Test]
        public void Deal_StakeOutOfRange_IsRefusedAndChargesNothing()
        {
            var table = new BlackjackTable(Rules(), seed: 1);
            var ledger = Ledger(1000);

            Assert.AreEqual(DealOutcome.StakeOutOfRange, table.Deal(ledger, ALICE, 5));
            Assert.AreEqual(DealOutcome.StakeOutOfRange, table.Deal(ledger, ALICE, 500));
            Assert.AreEqual(1000, ledger.BalanceOf(ALICE));
            Assert.AreEqual(BlackjackPhase.AwaitingBet, table.Phase);
        }

        [Test]
        public void Deal_CannotAfford_IsRefusedAndDealsNoCards()
        {
            var table = new BlackjackTable(Rules(), seed: 1);
            var ledger = Ledger(5);

            Assert.AreEqual(DealOutcome.CouldNotPay, table.Deal(ledger, ALICE, 50));
            Assert.AreEqual(5, ledger.BalanceOf(ALICE));
            Assert.AreEqual(0, table.PlayerHand.Count, "no cards should come out if the bet wasn't paid");
        }

        [Test]
        public void Hit_BeforeDealing_DoesNothing()
        {
            var table = new BlackjackTable(Rules(), seed: 1);
            Assert.IsFalse(table.Hit(Ledger()));
            Assert.IsFalse(table.Stand(Ledger()));
        }

        [Test]
        public void Deal_DealsTwoCardsEach()
        {
            var table = new BlackjackTable(Rules(), seed: 4);
            table.Deal(Ledger(), ALICE, 10);
            Assert.AreEqual(2, table.PlayerHand.Count);
            Assert.AreEqual(2, table.DealerHand.Count);
        }

        // ---------------------------------------------------------------
        // The table: money invariants — the part that matters
        // ---------------------------------------------------------------

        [Test]
        public void EveryRound_MovesTheLedgerByExactlyItsNet()
        {
            // Play a lot of hands with a fixed strategy and assert, every
            // single round, that the player's balance changed by exactly what
            // the round says it should. Catches double-charging, double-paying,
            // and a stake that never comes back on a push.
            var table = new BlackjackTable(Rules(), seed: 2024);
            var ledger = Ledger(100000);

            for (int round = 0; round < 200; round++)
            {
                int before = ledger.BalanceOf(ALICE);
                if (table.Deal(ledger, ALICE, 20) != DealOutcome.Dealt) break;

                // Basic-ish strategy: draw under 17, then stop.
                while (table.Phase == BlackjackPhase.PlayerTurn && table.PlayerHand.Total < 17)
                    table.Hit(ledger);

                if (table.Phase == BlackjackPhase.PlayerTurn) table.Stand(ledger);

                Assert.AreEqual(BlackjackPhase.Settled, table.Phase, $"round {round} should have settled");
                Assert.AreEqual(before + table.LastRound.Net, ledger.BalanceOf(ALICE),
                                $"round {round} ({table.LastRound.Outcome}) moved the ledger by the wrong amount");
            }
        }

        [Test]
        public void Push_ReturnsExactlyTheStake()
        {
            var table = new BlackjackTable(Rules(), seed: 77);
            var ledger = Ledger(100000);
            int pushes = 0;

            for (int round = 0; round < 300 && pushes < 5; round++)
            {
                int before = ledger.BalanceOf(ALICE);
                if (table.Deal(ledger, ALICE, 20) != DealOutcome.Dealt) break;

                while (table.Phase == BlackjackPhase.PlayerTurn && table.PlayerHand.Total < 17)
                    table.Hit(ledger);
                if (table.Phase == BlackjackPhase.PlayerTurn) table.Stand(ledger);

                if (table.LastRound.Outcome != BlackjackOutcome.Push) continue;
                pushes++;
                Assert.AreEqual(0, table.LastRound.Net, "a push must be a wash");
                Assert.AreEqual(before, ledger.BalanceOf(ALICE));
            }

            Assert.Greater(pushes, 0, "300 rounds should contain at least one push");
        }

        [Test]
        public void Blackjack_PaysThreeToTwo()
        {
            var table = new BlackjackTable(Rules(), seed: 555);
            var ledger = Ledger(100000);
            int naturals = 0;

            for (int round = 0; round < 400 && naturals < 3; round++)
            {
                if (table.Deal(ledger, ALICE, 20) != DealOutcome.Dealt) break;

                if (table.LastRound.Outcome == BlackjackOutcome.PlayerBlackjack
                    && table.Phase == BlackjackPhase.Settled)
                {
                    naturals++;
                    Assert.AreEqual(50, table.LastRound.Returned, "20 stake + 30 bonus");
                    Assert.AreEqual(30, table.LastRound.Net);
                    continue;
                }

                while (table.Phase == BlackjackPhase.PlayerTurn && table.PlayerHand.Total < 17)
                    table.Hit(ledger);
                if (table.Phase == BlackjackPhase.PlayerTurn) table.Stand(ledger);
            }

            Assert.Greater(naturals, 0, "400 rounds should contain at least one natural");
        }

        [Test]
        public void PlayerBust_ReturnsNothing()
        {
            var table = new BlackjackTable(Rules(), seed: 31337);
            var ledger = Ledger(100000);
            int busts = 0;

            for (int round = 0; round < 300 && busts < 3; round++)
            {
                if (table.Deal(ledger, ALICE, 20) != DealOutcome.Dealt) break;

                // Hit recklessly to force busts.
                while (table.Phase == BlackjackPhase.PlayerTurn) table.Hit(ledger);

                if (table.LastRound.Outcome != BlackjackOutcome.PlayerBust) continue;
                busts++;
                Assert.AreEqual(0, table.LastRound.Returned);
                Assert.AreEqual(-20, table.LastRound.Net);
            }

            Assert.Greater(busts, 0, "hitting until you stop should bust sometimes");
        }

        [Test]
        public void DealerNeverStandsBelowSeventeen()
        {
            var table = new BlackjackTable(Rules(), seed: 8080);
            var ledger = Ledger(100000);

            for (int round = 0; round < 200; round++)
            {
                if (table.Deal(ledger, ALICE, 10) != DealOutcome.Dealt) break;
                if (table.Phase == BlackjackPhase.PlayerTurn) table.Stand(ledger);

                // Three cases where the dealer correctly never plays out, so a
                // two-card total under 17 is right and not a bug:
                //   - the player busted (dealer wins without drawing),
                //   - the dealer had a natural,
                //   - the PLAYER had a natural, which settles at the deal.
                // That last one is what this assertion originally missed.
                if (table.LastRound.Outcome == BlackjackOutcome.PlayerBust) continue;
                if (table.LastRound.Outcome == BlackjackOutcome.PlayerBlackjack) continue;
                if (table.DealerHand.IsBlackjack) continue;

                Assert.GreaterOrEqual(table.DealerHand.Total, 17,
                                      $"round {round}: dealer stood on {table.DealerHand.Total}");
            }
        }

        // ---------------------------------------------------------------
        // Determinism — what makes this survivable when it goes networked
        // ---------------------------------------------------------------

        [Test]
        public void SameSeed_ReplaysTheIdenticalShoe()
        {
            static List<string> PlayOut(int seed)
            {
                var table = new BlackjackTable(Rules(), seed);
                var ledger = Ledger(100000);
                var log = new List<string>();

                for (int round = 0; round < 25; round++)
                {
                    if (table.Deal(ledger, ALICE, 20) != DealOutcome.Dealt) break;
                    while (table.Phase == BlackjackPhase.PlayerTurn && table.PlayerHand.Total < 17)
                        table.Hit(ledger);
                    if (table.Phase == BlackjackPhase.PlayerTurn) table.Stand(ledger);

                    log.Add($"{table.PlayerHand} vs {table.DealerHand} = {table.LastRound.Outcome} {table.LastRound.Net}");
                }

                return log;
            }

            CollectionAssert.AreEqual(PlayOut(4242), PlayOut(4242),
                "the same seed must produce the same cards and the same money on every machine");
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentGames()
        {
            var a = new BlackjackTable(Rules(), seed: 1);
            var b = new BlackjackTable(Rules(), seed: 2);
            a.Deal(Ledger(), ALICE, 20);
            b.Deal(Ledger(), ALICE, 20);

            Assert.AreNotEqual(a.PlayerHand.ToString() + a.DealerHand,
                               b.PlayerHand.ToString() + b.DealerHand);
        }

        // ---------------------------------------------------------------
        // Config
        // ---------------------------------------------------------------

        [Test]
        public void Rules_Sanitize_FixesNonsenseInsteadOfCrashing()
        {
            var r = new BlackjackRules
            {
                MinBet = -5, MaxBet = -100, DeckCount = 0,
                BlackjackPayoutDenominator = 0, ReshuffleBelowCards = 99999
            };

            r.Sanitize();

            Assert.GreaterOrEqual(r.MinBet, 1);
            Assert.GreaterOrEqual(r.MaxBet, r.MinBet);
            Assert.GreaterOrEqual(r.DeckCount, 1);
            Assert.GreaterOrEqual(r.BlackjackPayoutDenominator, 1);
            Assert.Less(r.ReshuffleBelowCards, r.DeckCount * 52);
        }

        // ---------------------------------------------------------------
        // Double-down and splits (Tony's 2026-08-11 executive-day override —
        // see Docs/GameBible.md's change log). Same invariant-testing
        // philosophy as the rest of this file: the shoe is shuffled, so
        // these loop looking for the situation under test (a matched pair, a
        // pair of Aces, a non-terminal hit) rather than assuming a seed lands
        // on it, exactly like Push_ReturnsExactlyTheStake and
        // Blackjack_PaysThreeToTwo already do above.
        // ---------------------------------------------------------------

        /// <summary>Hits under 17 then stands — the same fixed strategy the money-invariant tests above use.</summary>
        private static void PlayOutSimple(BlackjackTable table, TicketLedger ledger)
        {
            while (table.Phase == BlackjackPhase.PlayerTurn && table.PlayerHand.Total < 17) table.Hit(ledger);
            if (table.Phase == BlackjackPhase.PlayerTurn) table.Stand(ledger);
        }

        [Test]
        public void DoubleAndSplit_BeforeDealing_DoNothing()
        {
            var table = new BlackjackTable(Rules(), seed: 1);
            Assert.IsFalse(table.Double(Ledger()));
            Assert.IsFalse(table.Split(Ledger()));
        }

        // ---------------------------------------------------------------
        // CanDouble / CanSplit — legality READ-OUTS for a HUD, mirroring
        // Double()/Split()'s own checks exactly. Every case here has a
        // matching behavioural test above (Double_*/Split_*); these just
        // confirm the read-only flag agrees with what the action itself
        // would do, without duplicating rule logic.
        // ---------------------------------------------------------------

        [Test]
        public void CanDouble_And_CanSplit_AreFalseBeforeDealing()
        {
            var table = new BlackjackTable(Rules(), seed: 1);
            Assert.IsFalse(table.CanDouble);
            Assert.IsFalse(table.CanSplit);
        }

        [Test]
        public void CanDouble_IsTrueOnFreshTwoCardHand_FalseAfterHitting()
        {
            var table = new BlackjackTable(Rules(), seed: 6);
            var ledger = Ledger(100000);
            bool tested = false;

            for (int round = 0; round < 500 && !tested; round++)
            {
                if (table.Deal(ledger, ALICE, 20) != DealOutcome.Dealt) break;
                if (table.Phase != BlackjackPhase.PlayerTurn) continue; // resolved by a natural

                Assert.IsTrue(table.CanDouble, "a fresh two-card hand should always be double-eligible");

                table.Hit(ledger);
                if (table.Phase != BlackjackPhase.PlayerTurn) continue; // that hit finished the hand

                Assert.IsFalse(table.CanDouble, "double is illegal once a third card has been drawn");
                tested = true;
            }

            Assert.IsTrue(tested, "500 rounds should include at least one non-terminal hit to test against");
        }

        [Test]
        public void CanSplit_MatchesOnlyAMatchedPair()
        {
            var table = new BlackjackTable(Rules(), seed: 9);
            var ledger = Ledger(100000);
            bool sawMatched = false, sawUnmatched = false;

            for (int round = 0; round < 500 && !(sawMatched && sawUnmatched); round++)
            {
                if (table.Deal(ledger, ALICE, 20) != DealOutcome.Dealt) break;
                if (table.Phase != BlackjackPhase.PlayerTurn) continue;

                BlackjackHand hand = table.PlayerHands[0];
                bool matched = hand.Cards[0].Rank == hand.Cards[1].Rank;
                Assert.AreEqual(matched, table.CanSplit);
                if (matched) sawMatched = true; else sawUnmatched = true;

                PlayOutSimple(table, ledger);
            }

            Assert.IsTrue(sawMatched, "500 rounds should include at least one matched starting pair");
            Assert.IsTrue(sawUnmatched, "500 rounds should include at least one unmatched starting hand");
        }

        [Test]
        public void CanSplit_IsFalseAfterAlreadySplitting_OnEitherHand()
        {
            var table = new BlackjackTable(Rules(), seed: 15);
            var ledger = Ledger(1_000_000);
            bool tested = false;

            for (int round = 0; round < 3000 && !tested; round++)
            {
                if (table.Deal(ledger, ALICE, 20) != DealOutcome.Dealt) break;
                if (table.Phase != BlackjackPhase.PlayerTurn) continue;

                BlackjackHand hand = table.PlayerHands[0];
                if (hand.Cards[0].Rank != hand.Cards[1].Rank) { PlayOutSimple(table, ledger); continue; }

                Assert.IsTrue(table.Split(ledger));
                if (table.Phase != BlackjackPhase.PlayerTurn)
                {
                    // a forced ace-split (or a lucky 21) auto-resolved both hands before this could be checked mid-hand
                    Assert.IsFalse(table.CanSplit);
                    tested = true;
                    break;
                }

                Assert.IsFalse(table.CanSplit, "no re-splitting, even mid-hand-0");
                PlayOutSimple(table, ledger);
                if (table.Phase == BlackjackPhase.PlayerTurn)
                {
                    Assert.IsFalse(table.CanSplit, "hand 1 cannot split either, even if it happens to be a pair");
                    PlayOutSimple(table, ledger);
                }
                tested = true;
            }

            Assert.IsTrue(tested, "3000 rounds should include at least one split to test against");
        }

        [Test]
        public void CanDouble_And_CanSplit_AreFalseOnceSettled()
        {
            var table = new BlackjackTable(Rules(), seed: 2);
            var ledger = Ledger(100000);

            table.Deal(ledger, ALICE, 20);
            while (table.Phase == BlackjackPhase.PlayerTurn) table.Stand(ledger);

            Assert.AreEqual(BlackjackPhase.Settled, table.Phase);
            Assert.IsFalse(table.CanDouble);
            Assert.IsFalse(table.CanSplit);
        }

        /// <summary>
        /// QA-reviewed finding, pinned rather than silently "fixed" at this
        /// layer: CanDouble/CanSplit check every DOUBLE/SPLIT legality rule
        /// EXCEPT affordability, because BlackjackTable holds no TicketLedger
        /// reference by design (see the class comment). Double()/Split() DO
        /// check affordability (via RequestSpend) and correctly refuse here.
        /// The real fix lives one layer up — BlackjackStation.CanDouble/
        /// CanSplit additionally check the seated player's balance against
        /// ActiveHandStake, which is what BlackjackHud actually reads. This
        /// test exists so nobody "fixes" this property in isolation later and
        /// accidentally gives BlackjackTable a ledger dependency it was
        /// deliberately built without.
        /// </summary>
        [Test]
        public void CanDouble_DoesNotCheckAffordability_ThatIsTheStationsJob()
        {
            var table = new BlackjackTable(Rules(), seed: 6);
            var ledger = Ledger(1000);
            bool tested = false;

            for (int round = 0; round < 500 && !tested; round++)
            {
                if (table.Deal(ledger, ALICE, 20) != DealOutcome.Dealt) break;
                if (table.Phase != BlackjackPhase.PlayerTurn) continue; // resolved by a natural

                // Drain the player far below the stake a double would need.
                ledger.RequestSpend(ALICE, ledger.BalanceOf(ALICE) - 1, "test: drain");

                Assert.IsTrue(table.CanDouble, "CanDouble reads state legality only, not affordability");
                Assert.IsFalse(table.Double(ledger), "but the actual action still correctly refuses");
                tested = true;
            }

            Assert.IsTrue(tested, "500 rounds should include at least one dealt hand to drain against");
        }

        [Test]
        public void Double_OnFirstTwoCards_TakesASecondStakeAndDealsOneCardThenAutoStands()
        {
            var table = new BlackjackTable(Rules(), seed: 3);
            var ledger = Ledger(100000);

            DealOutcome outcome;
            do { outcome = table.Deal(ledger, ALICE, 20); } while (table.Phase != BlackjackPhase.PlayerTurn);
            Assert.AreEqual(DealOutcome.Dealt, outcome);

            int balanceBeforeDouble = ledger.BalanceOf(ALICE);
            bool doubled = table.Double(ledger);

            Assert.IsTrue(doubled);
            Assert.AreEqual(3, table.PlayerHands[0].Count, "double deals exactly one card");
            Assert.AreEqual(balanceBeforeDouble - 20, ledger.BalanceOf(ALICE), "the second stake is taken immediately");
            Assert.AreEqual(BlackjackPhase.Settled, table.Phase, "double auto-stands and resolves a solo hand");
        }

        [Test]
        public void Double_AfterHitting_IsIllegalAndChargesNothing()
        {
            var table = new BlackjackTable(Rules(), seed: 5);
            var ledger = Ledger(100000);
            bool tested = false;

            for (int round = 0; round < 500 && !tested; round++)
            {
                if (table.Deal(ledger, ALICE, 20) != DealOutcome.Dealt) break;
                if (table.Phase != BlackjackPhase.PlayerTurn) continue; // resolved by a natural

                table.Hit(ledger);
                if (table.Phase != BlackjackPhase.PlayerTurn) continue; // that hit finished the hand

                int before = ledger.BalanceOf(ALICE);
                Assert.IsFalse(table.Double(ledger), "double is legal only on the first two cards");
                Assert.AreEqual(before, ledger.BalanceOf(ALICE));
                tested = true;
            }

            Assert.IsTrue(tested, "500 rounds should include at least one non-terminal hit to test against");
        }

        [Test]
        public void Split_OnUnmatchedFirstTwoCards_IsIllegalAndChargesNothing()
        {
            var table = new BlackjackTable(Rules(), seed: 21);
            var ledger = Ledger(100000);
            bool tested = false;

            for (int round = 0; round < 200 && !tested; round++)
            {
                if (table.Deal(ledger, ALICE, 20) != DealOutcome.Dealt) break;
                if (table.Phase != BlackjackPhase.PlayerTurn) continue;

                BlackjackHand hand = table.PlayerHands[0];
                if (hand.Cards[0].Rank == hand.Cards[1].Rank) { table.Stand(ledger); continue; }

                int before = ledger.BalanceOf(ALICE);
                Assert.IsFalse(table.Split(ledger));
                Assert.AreEqual(before, ledger.BalanceOf(ALICE));
                Assert.AreEqual(1, table.PlayerHands.Count);
                table.Stand(ledger);
                tested = true;
            }

            Assert.IsTrue(tested, "200 rounds should include at least one unmatched starting hand");
        }

        [Test]
        public void Split_OnMatchedPair_TakesASecondStakeAndCreatesTwoHands()
        {
            var table = new BlackjackTable(Rules(), seed: 7);
            var ledger = Ledger(100000);
            bool tested = false;

            for (int round = 0; round < 3000 && !tested; round++)
            {
                if (table.Deal(ledger, ALICE, 20) != DealOutcome.Dealt) break;
                if (table.Phase != BlackjackPhase.PlayerTurn) continue;

                BlackjackHand hand = table.PlayerHands[0];
                if (hand.Cards[0].Rank != hand.Cards[1].Rank) { PlayOutSimple(table, ledger); continue; }

                int before = ledger.BalanceOf(ALICE);
                bool split = table.Split(ledger);

                Assert.IsTrue(split);
                Assert.AreEqual(2, table.PlayerHands.Count);
                Assert.AreEqual(before - 20, ledger.BalanceOf(ALICE), "the second stake is taken immediately");
                Assert.IsTrue(table.HasSplit);
                tested = true;
            }

            Assert.IsTrue(tested, "3000 rounds should include at least one matched starting pair");
        }

        [Test]
        public void Split_ASecondTime_IsIllegal_NoReSplitting()
        {
            var table = new BlackjackTable(Rules(), seed: 33);
            var ledger = Ledger(1_000_000);
            bool tested = false;

            for (int round = 0; round < 3000 && !tested; round++)
            {
                if (table.Deal(ledger, ALICE, 20) != DealOutcome.Dealt) break;
                if (table.Phase != BlackjackPhase.PlayerTurn) continue;

                BlackjackHand hand = table.PlayerHands[0];
                if (hand.Cards[0].Rank != hand.Cards[1].Rank) { PlayOutSimple(table, ledger); continue; }

                Assert.IsTrue(table.Split(ledger));
                if (table.Phase != BlackjackPhase.PlayerTurn)
                {
                    tested = true; // a forced ace-split auto-resolved before a re-split was even possible
                    break;
                }

                Assert.IsFalse(table.Split(ledger), "no re-splitting, even if the new hand happens to match again");

                PlayOutSimple(table, ledger);
                if (table.Phase == BlackjackPhase.PlayerTurn) PlayOutSimple(table, ledger);
                tested = true;
            }

            Assert.IsTrue(tested, "3000 rounds should include at least one split to test against");
        }

        [Test]
        public void SplitAces_DealExactlyOneCardEachAndAutoResolve_NoFurtherAction()
        {
            var table = new BlackjackTable(Rules(), seed: 41);
            var ledger = Ledger(1_000_000);
            bool tested = false;

            for (int round = 0; round < 8000 && !tested; round++)
            {
                if (table.Deal(ledger, ALICE, 20) != DealOutcome.Dealt) break;
                if (table.Phase != BlackjackPhase.PlayerTurn) continue;

                BlackjackHand hand = table.PlayerHands[0];
                if (hand.Cards[0].Rank != Rank.Ace || hand.Cards[1].Rank != Rank.Ace)
                {
                    PlayOutSimple(table, ledger);
                    continue;
                }

                Assert.IsTrue(table.Split(ledger));
                Assert.AreEqual(2, table.PlayerHands[0].Count, "split aces get exactly one card each");
                Assert.AreEqual(2, table.PlayerHands[1].Count);
                Assert.AreEqual(BlackjackPhase.Settled, table.Phase, "split aces auto-resolve with no further player action");
                Assert.AreEqual(2, table.LastRoundResults.Count);
                tested = true;
            }

            Assert.IsTrue(tested, "8000 rounds should include at least one starting pair of Aces");
        }

        [Test]
        public void SplitRound_SettlesBothHandsIndependently_AndMovesTheLedgerByExactlyTheirCombinedNet()
        {
            var table = new BlackjackTable(Rules(), seed: 11);
            var ledger = Ledger(1_000_000);
            int splitRoundsChecked = 0;

            for (int round = 0; round < 3000 && splitRoundsChecked < 5; round++)
            {
                // Captured BEFORE the deal, not before the split — expectedNet
                // below sums BOTH hands' Net (each already relative to its own
                // stake), so the baseline must predate every stake this round
                // takes, or hand 0's original stake gets subtracted twice.
                int before = ledger.BalanceOf(ALICE);
                int circBefore = ledger.TotalInCirculation();

                if (table.Deal(ledger, ALICE, 20) != DealOutcome.Dealt) break;
                if (table.Phase != BlackjackPhase.PlayerTurn) continue;

                BlackjackHand hand = table.PlayerHands[0];
                if (hand.Cards[0].Rank != hand.Cards[1].Rank) { PlayOutSimple(table, ledger); continue; }

                Assert.IsTrue(table.Split(ledger));

                while (table.Phase == BlackjackPhase.PlayerTurn)
                {
                    if (table.PlayerHand.Total < 17) table.Hit(ledger);
                    else table.Stand(ledger);
                }

                Assert.AreEqual(BlackjackPhase.Settled, table.Phase);
                Assert.AreEqual(2, table.LastRoundResults.Count, "a split round must report a result per hand");

                int expectedNet = 0;
                for (int i = 0; i < table.LastRoundResults.Count; i++) expectedNet += table.LastRoundResults[i].Net;

                Assert.AreEqual(before + expectedNet, ledger.BalanceOf(ALICE),
                                "the ledger must move by exactly the sum of both hands' nets");
                Assert.AreEqual(circBefore + expectedNet, ledger.TotalInCirculation(),
                                "circulation must move by exactly the round's total net");

                splitRoundsChecked++;
            }

            Assert.Greater(splitRoundsChecked, 0, "3000 rounds should include at least one split round");
        }

        [Test]
        public void SplitHand_CanBustWhileTheOtherWins()
        {
            var table = new BlackjackTable(Rules(), seed: 13);
            var ledger = Ledger(1_000_000);
            bool found = false;

            for (int round = 0; round < 5000 && !found; round++)
            {
                if (table.Deal(ledger, ALICE, 20) != DealOutcome.Dealt) break;
                if (table.Phase != BlackjackPhase.PlayerTurn) continue;

                BlackjackHand hand = table.PlayerHands[0];
                if (hand.Cards[0].Rank != hand.Cards[1].Rank) { PlayOutSimple(table, ledger); continue; }

                Assert.IsTrue(table.Split(ledger));

                // Hand 0: hit recklessly to encourage a bust. Hand 1: stand immediately.
                while (table.Phase == BlackjackPhase.PlayerTurn)
                {
                    if (table.ActiveHandIndex == 0) table.Hit(ledger);
                    else table.Stand(ledger);
                }

                if (table.LastRoundResults.Count != 2) continue;

                BlackjackRound r0 = table.LastRoundResults[0];
                BlackjackRound r1 = table.LastRoundResults[1];

                if (r0.Outcome == BlackjackOutcome.PlayerBust &&
                    (r1.Outcome == BlackjackOutcome.PlayerWin || r1.Outcome == BlackjackOutcome.DealerBust))
                {
                    Assert.AreEqual(0, r0.Returned);
                    Assert.AreEqual(-20, r0.Net);
                    Assert.Greater(r1.Net, 0);
                    found = true;
                }
            }

            Assert.IsTrue(found, "5000 split rounds (hand 0 hitting recklessly, hand 1 standing) should find at least one bust-vs-win pair");
        }

        [Test]
        public void SplitHandReaching21_PaysEvenMoney_NeverTheBlackjackBonus()
        {
            var table = new BlackjackTable(Rules(), seed: 51);
            var ledger = Ledger(1_000_000);
            bool tested = false;

            for (int round = 0; round < 8000 && !tested; round++)
            {
                if (table.Deal(ledger, ALICE, 20) != DealOutcome.Dealt) break;
                if (table.Phase != BlackjackPhase.PlayerTurn) continue;

                BlackjackHand hand = table.PlayerHands[0];
                if (hand.Cards[0].Rank != hand.Cards[1].Rank) { PlayOutSimple(table, ledger); continue; }

                Assert.IsTrue(table.Split(ledger));
                while (table.Phase == BlackjackPhase.PlayerTurn) table.Stand(ledger);

                for (int i = 0; i < table.LastRoundResults.Count; i++)
                {
                    BlackjackRound r = table.LastRoundResults[i];
                    if (r.PlayerTotal != 21 || r.Outcome != BlackjackOutcome.PlayerWin) continue;

                    Assert.AreEqual(r.Stake * 2, r.Returned,
                                    "post-split 21 pays even money (2x stake), never the 3:2 blackjack bonus");
                    tested = true;
                }
            }

            Assert.IsTrue(tested, "8000 split rounds should include at least one split hand standing on 21 and winning");
        }
    }
}
