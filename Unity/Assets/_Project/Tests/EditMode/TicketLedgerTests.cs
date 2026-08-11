using System.Collections.Generic;
using NUnit.Framework;
using WeeSpurts.Gameplay;

namespace WeeSpurts.Tests
{
    /// <summary>
    /// Unit tests for the pure-C# ticket ledger (Docs/SlopLayerPlan.md F3).
    /// Run in Unity: Window > General > Test Runner > EditMode > Run All.
    ///
    /// These exist so nobody has to press Play to trust the economy. The rule
    /// they enforce above all others: tickets are never conjured and never lost.
    /// Every refusal path is tested too, because a refusal that quietly
    /// mutates a balance is the bug that would take a week to find.
    /// </summary>
    public class TicketLedgerTests
    {
        private const ulong ALICE = 1;
        private const ulong BOB = 2;
        private const ulong NOBODY = 99;

        private static TicketLedger WithPlayers(int aliceTickets = 100, int bobTickets = 100)
        {
            var ledger = new TicketLedger();
            ledger.Register(ALICE, aliceTickets);
            ledger.Register(BOB, bobTickets);
            return ledger;
        }

        // --- Registration ------------------------------------------------

        [Test]
        public void Register_SetsStartingBalance()
        {
            var ledger = new TicketLedger();
            Assert.IsTrue(ledger.Register(ALICE, 50));
            Assert.AreEqual(50, ledger.BalanceOf(ALICE));
        }

        [Test]
        public void Register_Twice_DoesNotWipeBalance()
        {
            // A reconnect or a late-joining client must not reset someone's tickets.
            var ledger = WithPlayers();
            ledger.RequestSpend(ALICE, 40, "bar:pint");

            Assert.IsFalse(ledger.Register(ALICE, 100), "second Register should report 'already known'");
            Assert.AreEqual(60, ledger.BalanceOf(ALICE), "balance must survive a re-register");
        }

        [Test]
        public void Register_NegativeStartingBalance_ClampsToZero()
        {
            var ledger = new TicketLedger();
            ledger.Register(ALICE, -500);
            Assert.AreEqual(0, ledger.BalanceOf(ALICE));
        }

        [Test]
        public void BalanceOf_UnknownPlayer_IsZeroNotAnException()
        {
            Assert.AreEqual(0, new TicketLedger().BalanceOf(NOBODY));
        }

        // --- Spending ----------------------------------------------------

        [Test]
        public void RequestSpend_WithEnoughTickets_GrantsAndDebits()
        {
            var ledger = WithPlayers(aliceTickets: 100);
            TicketTransaction tx = ledger.RequestSpend(ALICE, 30, "bar:pint");

            Assert.IsTrue(tx.Granted);
            Assert.AreEqual(-30, tx.Delta);
            Assert.AreEqual(70, tx.BalanceAfter);
            Assert.AreEqual(70, ledger.BalanceOf(ALICE));
        }

        [Test]
        public void RequestSpend_ExactBalance_IsAllowed()
        {
            // Spending your last ticket is legal; going one past it is not.
            var ledger = WithPlayers(aliceTickets: 25);
            Assert.IsTrue(ledger.RequestSpend(ALICE, 25, "slots").Granted);
            Assert.AreEqual(0, ledger.BalanceOf(ALICE));
        }

        [Test]
        public void RequestSpend_TooExpensive_RefusesAndLeavesBalanceUntouched()
        {
            var ledger = WithPlayers(aliceTickets: 10);
            TicketTransaction tx = ledger.RequestSpend(ALICE, 11, "cosmetics:hat");

            Assert.IsFalse(tx.Granted);
            Assert.AreEqual(TicketResult.InsufficientFunds, tx.Result);
            Assert.AreEqual(0, tx.Delta, "a refusal must move nothing");
            Assert.AreEqual(10, ledger.BalanceOf(ALICE));
        }

        [Test]
        public void RequestSpend_NeverGoesNegative()
        {
            var ledger = WithPlayers(aliceTickets: 5);
            for (int i = 0; i < 10; i++) ledger.RequestSpend(ALICE, 3, "slots");
            Assert.AreEqual(2, ledger.BalanceOf(ALICE));
            Assert.GreaterOrEqual(ledger.BalanceOf(ALICE), 0);
        }

        [Test]
        public void RequestSpend_ZeroOrNegative_IsRefused()
        {
            // A negative "spend" would be a backdoor credit.
            var ledger = WithPlayers(aliceTickets: 100);
            Assert.AreEqual(TicketResult.InvalidAmount, ledger.RequestSpend(ALICE, 0, "x").Result);
            Assert.AreEqual(TicketResult.InvalidAmount, ledger.RequestSpend(ALICE, -50, "x").Result);
            Assert.AreEqual(100, ledger.BalanceOf(ALICE));
        }

        [Test]
        public void RequestSpend_UnknownPlayer_IsRefused()
        {
            var ledger = WithPlayers();
            Assert.AreEqual(TicketResult.UnknownPlayer, ledger.RequestSpend(NOBODY, 5, "x").Result);
        }

        // --- Awards ------------------------------------------------------

        [Test]
        public void Award_CreditsTheBalance()
        {
            var ledger = WithPlayers(aliceTickets: 10);
            TicketTransaction tx = ledger.Award(ALICE, 90, "bet:won");

            Assert.IsTrue(tx.Granted);
            Assert.AreEqual(90, tx.Delta);
            Assert.AreEqual(100, ledger.BalanceOf(ALICE));
        }

        [Test]
        public void Award_ZeroOrNegative_IsRefused()
        {
            var ledger = WithPlayers(aliceTickets: 100);
            Assert.AreEqual(TicketResult.InvalidAmount, ledger.Award(ALICE, 0, "x").Result);
            Assert.AreEqual(TicketResult.InvalidAmount, ledger.Award(ALICE, -10, "x").Result);
            Assert.AreEqual(100, ledger.BalanceOf(ALICE), "a refused award must not debit either");
        }

        [Test]
        public void Award_PastTheCeiling_IsRefusedRatherThanOverflowing()
        {
            // An overflowed int balance looks exactly like bankruptcy, which
            // would be a nightmare to diagnose. Refuse loudly instead.
            var ledger = new TicketLedger();
            ledger.Register(ALICE, TicketLedger.MAX_BALANCE - 5);

            TicketTransaction tx = ledger.Award(ALICE, 100, "bet:won");

            Assert.AreEqual(TicketResult.BalanceCeiling, tx.Result);
            Assert.AreEqual(TicketLedger.MAX_BALANCE - 5, ledger.BalanceOf(ALICE));
            Assert.Greater(ledger.BalanceOf(ALICE), 0, "must never wrap negative");
        }

        // --- Transfers (the wager payout shape) ---------------------------

        [Test]
        public void Transfer_MovesTicketsAndConservesTheTotal()
        {
            var ledger = WithPlayers(aliceTickets: 100, bobTickets: 100);
            int before = ledger.TotalInCirculation();

            TicketTransaction tx = ledger.Transfer(ALICE, BOB, 40, "bet:lost");

            Assert.IsTrue(tx.Granted);
            Assert.AreEqual(60, ledger.BalanceOf(ALICE));
            Assert.AreEqual(140, ledger.BalanceOf(BOB));
            Assert.AreEqual(before, ledger.TotalInCirculation(), "a transfer must not create or destroy tickets");
        }

        [Test]
        public void Transfer_PayerCannotAfford_MovesNothingAtAll()
        {
            // The reason Transfer is not "spend then award": a half-completed
            // pair would delete tickets from the economy.
            var ledger = WithPlayers(aliceTickets: 10, bobTickets: 100);
            int before = ledger.TotalInCirculation();

            TicketTransaction tx = ledger.Transfer(ALICE, BOB, 50, "bet:lost");

            Assert.AreEqual(TicketResult.InsufficientFunds, tx.Result);
            Assert.AreEqual(10, ledger.BalanceOf(ALICE));
            Assert.AreEqual(100, ledger.BalanceOf(BOB));
            Assert.AreEqual(before, ledger.TotalInCirculation());
        }

        [Test]
        public void Transfer_ToYourself_IsRefused()
        {
            var ledger = WithPlayers(aliceTickets: 100);
            Assert.AreEqual(TicketResult.InvalidAmount, ledger.Transfer(ALICE, ALICE, 10, "x").Result);
            Assert.AreEqual(100, ledger.BalanceOf(ALICE));
        }

        [Test]
        public void Transfer_UnknownPlayer_IsRefused()
        {
            var ledger = WithPlayers();
            Assert.AreEqual(TicketResult.UnknownPlayer, ledger.Transfer(ALICE, NOBODY, 10, "x").Result);
            Assert.AreEqual(100, ledger.BalanceOf(ALICE));
        }

        // --- Events -------------------------------------------------------

        [Test]
        public void OnTransaction_FiresOnlyForGrants()
        {
            var ledger = WithPlayers(aliceTickets: 10);
            var granted = new List<TicketTransaction>();
            var refused = new List<TicketTransaction>();
            ledger.OnTransaction += granted.Add;
            ledger.OnRefused += refused.Add;

            ledger.RequestSpend(ALICE, 5, "bar:pint");   // granted
            ledger.RequestSpend(ALICE, 500, "bar:magnum"); // refused

            Assert.AreEqual(1, granted.Count);
            Assert.AreEqual("bar:pint", granted[0].Reason);
            Assert.AreEqual(1, refused.Count);
            Assert.AreEqual(TicketResult.InsufficientFunds, refused[0].Result);
        }

        [Test]
        public void OnTransaction_Transfer_ReportsBothSides()
        {
            // A ticket feed listening to the event should see the payer AND the
            // payee, even though only one transaction is returned to the caller.
            var ledger = WithPlayers();
            var seen = new List<TicketTransaction>();
            ledger.OnTransaction += seen.Add;

            ledger.Transfer(ALICE, BOB, 25, "bet:lost");

            Assert.AreEqual(2, seen.Count);
            Assert.AreEqual(-25, seen[0].Delta);
            Assert.AreEqual(25, seen[1].Delta);
        }

        // --- The invariant that matters most -------------------------------

        [Test]
        public void TotalInCirculation_OnlyMovesWhenTheEconomyMeansIt()
        {
            var ledger = WithPlayers(aliceTickets: 100, bobTickets: 100);
            Assert.AreEqual(200, ledger.TotalInCirculation());

            ledger.Transfer(ALICE, BOB, 50, "bet:lost");        // moves, conserves
            Assert.AreEqual(200, ledger.TotalInCirculation());

            ledger.RequestSpend(BOB, 30, "bar:pint");           // a sink: tickets leave
            Assert.AreEqual(170, ledger.TotalInCirculation());

            ledger.Award(ALICE, 30, "bet:won");                 // a source: tickets enter
            Assert.AreEqual(200, ledger.TotalInCirculation());

            ledger.RequestSpend(ALICE, 999999, "cannot afford"); // refused: no drift
            ledger.Transfer(BOB, NOBODY, 10, "unknown payee");   // refused: no drift
            Assert.AreEqual(200, ledger.TotalInCirculation());
        }
    }
}
