using System.Collections.Generic;
using NUnit.Framework;
using WeeSpurts.Gameplay;
using WeeSpurts.Slop;

namespace WeeSpurts.Tests
{
    /// <summary>
    /// Unit tests for the pure-C# shop (Docs/SlopLayerPlan.md S4 — the bar and
    /// the cosmetics counter). Run in Unity: Window > General > Test Runner >
    /// EditMode > Run All.
    ///
    /// The headline rule these protect: a REFUSED purchase must leave the world
    /// exactly as it found it — no coins moved, no stock consumed. Get that
    /// wrong and a broke player destroys the last golden hat for everyone,
    /// which is the kind of bug you only find with four people in the room.
    /// </summary>
    public class VendorTests
    {
        private const ulong ALICE = 1;
        private const ulong BOB = 2;

        private const int PINT = 10;
        private const int GOLDEN_HAT = 20;
        private const int FREEBIE = 30;

        private static VendorItem Item(int id, string name, int price, int stock = -1) =>
            new VendorItem { ItemId = id, DisplayName = name, Price = price, StockPerMatch = stock };

        /// <summary>Unlimited pints at 10, exactly one golden hat at 100.</summary>
        private static Vendor Bar() => new Vendor("Bar", new List<VendorItem>
        {
            Item(PINT, "Pint", 10),
            Item(GOLDEN_HAT, "Golden Hat", 100, stock: 1)
        });

        private static CoinLedger LedgerWith(int aliceCoins = 100, int bobCoins = 100)
        {
            var ledger = new CoinLedger();
            ledger.Register(ALICE, aliceCoins);
            ledger.Register(BOB, bobCoins);
            return ledger;
        }

        // --- The happy path ------------------------------------------------

        [Test]
        public void Purchase_WithEnoughCoins_SucceedsAndDebits()
        {
            var bar = Bar();
            var ledger = LedgerWith(aliceCoins: 100);

            PurchaseResult r = bar.Purchase(ledger, ALICE, PINT);

            Assert.IsTrue(r.Success);
            Assert.AreEqual(PurchaseOutcome.Purchased, r.Outcome);
            Assert.AreEqual(10, r.Price);
            Assert.AreEqual(90, ledger.BalanceOf(ALICE));
            Assert.AreEqual(90, r.Transaction.BalanceAfter, "the result should carry the new balance");
        }

        [Test]
        public void Purchase_TagsTheTransactionWithVendorAndItem()
        {
            // So the coin feed and the logs read "Bar:Pint", not "spend".
            var bar = Bar();
            var ledger = LedgerWith();

            PurchaseResult r = bar.Purchase(ledger, ALICE, PINT);

            Assert.AreEqual("Bar:Pint", r.Transaction.Reason);
        }

        [Test]
        public void Purchase_UnlimitedItem_NeverRunsOut()
        {
            var bar = Bar();
            var ledger = LedgerWith(aliceCoins: 1000);

            for (int i = 0; i < 20; i++)
                Assert.IsTrue(bar.Purchase(ledger, ALICE, PINT).Success, $"pint {i + 1} should be available");

            Assert.AreEqual(800, ledger.BalanceOf(ALICE));
            Assert.Less(bar.StockRemaining(PINT), 0, "unlimited stock reads negative");
        }

        // --- Stock ----------------------------------------------------------

        [Test]
        public void Purchase_LimitedItem_SellsOutAfterItsStock()
        {
            var bar = Bar();
            var ledger = LedgerWith(aliceCoins: 500, bobCoins: 500);

            Assert.IsTrue(bar.Purchase(ledger, ALICE, GOLDEN_HAT).Success);
            Assert.AreEqual(0, bar.StockRemaining(GOLDEN_HAT));

            PurchaseResult second = bar.Purchase(ledger, BOB, GOLDEN_HAT);
            Assert.AreEqual(PurchaseOutcome.OutOfStock, second.Outcome);
            Assert.AreEqual(500, ledger.BalanceOf(BOB), "a sold-out item must not charge");
        }

        [Test]
        public void Purchase_TooPoor_DoesNotConsumeStock()
        {
            // THE test. Broke player tries for the last golden hat; a rich
            // player must still be able to buy it afterwards.
            var bar = Bar();
            var ledger = LedgerWith(aliceCoins: 5, bobCoins: 500);

            PurchaseResult broke = bar.Purchase(ledger, ALICE, GOLDEN_HAT);

            Assert.AreEqual(PurchaseOutcome.NotEnoughCoins, broke.Outcome);
            Assert.AreEqual(1, bar.StockRemaining(GOLDEN_HAT), "a refused purchase must not eat stock");
            Assert.AreEqual(5, ledger.BalanceOf(ALICE));

            Assert.IsTrue(bar.Purchase(ledger, BOB, GOLDEN_HAT).Success, "the hat should still be there");
        }

        [Test]
        public void ResetStock_RestocksForTheNextMatch()
        {
            var bar = Bar();
            var ledger = LedgerWith(aliceCoins: 500);
            bar.Purchase(ledger, ALICE, GOLDEN_HAT);
            Assert.AreEqual(0, bar.StockRemaining(GOLDEN_HAT));

            bar.ResetStock();

            Assert.AreEqual(1, bar.StockRemaining(GOLDEN_HAT));
            Assert.IsTrue(bar.Purchase(ledger, ALICE, GOLDEN_HAT).Success);
        }

        // --- Bad requests ---------------------------------------------------

        [Test]
        public void Purchase_UnknownItem_IsRefusedAndChargesNothing()
        {
            var bar = Bar();
            var ledger = LedgerWith(aliceCoins: 100);

            PurchaseResult r = bar.Purchase(ledger, ALICE, itemId: 999);

            Assert.AreEqual(PurchaseOutcome.UnknownItem, r.Outcome);
            Assert.AreEqual(100, ledger.BalanceOf(ALICE));
        }

        [Test]
        public void Purchase_UnknownPlayer_IsInvalidNotJustPoor()
        {
            // An unregistered player is a wiring bug, not a player-facing
            // "you're broke" state, and the outcome should say so.
            var bar = Bar();
            var ledger = LedgerWith();

            PurchaseResult r = bar.Purchase(ledger, playerId: 99ul, itemId: PINT);

            Assert.AreEqual(PurchaseOutcome.InvalidRequest, r.Outcome);
        }

        [Test]
        public void Purchase_FreeOrNegativelyPricedItem_IsRejectedAsMisconfiguration()
        {
            var bar = new Vendor("Bar", new List<VendorItem> { Item(FREEBIE, "Tap Water", 0) });
            var ledger = LedgerWith(aliceCoins: 100);

            PurchaseResult r = bar.Purchase(ledger, ALICE, FREEBIE);

            Assert.AreEqual(PurchaseOutcome.InvalidRequest, r.Outcome);
            Assert.AreEqual(100, ledger.BalanceOf(ALICE));
        }

        [Test]
        public void Purchase_NullLedger_IsRefusedRatherThanThrowing()
        {
            Assert.AreEqual(PurchaseOutcome.InvalidRequest,
                            Bar().Purchase(null, ALICE, PINT).Outcome);
        }

        // --- Construction from a half-filled Inspector list -------------------

        [Test]
        public void Vendor_SkipsNullRowsAndDuplicateIds()
        {
            // An unfilled row in the Inspector is null; a copy-pasted row has a
            // duplicate id. Neither should stop the game loading.
            var vendor = new Vendor("Bar", new List<VendorItem>
            {
                Item(PINT, "Pint", 10),
                null,
                Item(PINT, "Pint (copy-paste mistake)", 999)
            });

            Assert.AreEqual(1, vendor.Items.Count);
            Assert.IsTrue(vendor.TryGetItem(PINT, out VendorItem kept));
            Assert.AreEqual(10, kept.Price, "the FIRST item with an id wins");
        }

        [Test]
        public void Vendor_WithNoItems_IsUsableAndSellsNothing()
        {
            var vendor = new Vendor("Empty", null);
            Assert.AreEqual(0, vendor.Items.Count);
            Assert.AreEqual(PurchaseOutcome.UnknownItem,
                            vendor.Purchase(LedgerWith(), ALICE, PINT).Outcome);
        }

        // --- Queries ---------------------------------------------------------

        [Test]
        public void CanBuy_ReflectsAffordabilityAndStock()
        {
            var bar = Bar();
            var ledger = LedgerWith(aliceCoins: 50, bobCoins: 500);

            Assert.IsTrue(bar.CanBuy(ledger, ALICE, PINT), "10 coins out of 50");
            Assert.IsFalse(bar.CanBuy(ledger, ALICE, GOLDEN_HAT), "100 coins out of 50");

            bar.Purchase(ledger, BOB, GOLDEN_HAT);
            Assert.IsFalse(bar.CanBuy(ledger, BOB, GOLDEN_HAT), "sold out even though he's rich");
        }

        [Test]
        public void StockRemaining_UnknownItem_IsZero()
        {
            Assert.AreEqual(0, Bar().StockRemaining(999));
        }

        // --- Events -----------------------------------------------------------

        [Test]
        public void OnPurchased_FiresOnlyOnSuccess_AndCarriesTheItemId()
        {
            // This is the seam S3 (drink meter) hangs off — it listens here and
            // decides what being three pints deep means.
            var bar = Bar();
            var ledger = LedgerWith(aliceCoins: 15);
            var bought = new List<PurchaseResult>();
            var refused = new List<PurchaseResult>();
            bar.OnPurchased += bought.Add;
            bar.OnRefused += refused.Add;

            bar.Purchase(ledger, ALICE, PINT);        // 10 of 15 — fine
            bar.Purchase(ledger, ALICE, GOLDEN_HAT);  // 100 of 5 — refused

            Assert.AreEqual(1, bought.Count);
            Assert.AreEqual(PINT, bought[0].ItemId);
            Assert.AreEqual(ALICE, bought[0].PlayerId);
            Assert.AreEqual(1, refused.Count);
            Assert.AreEqual(PurchaseOutcome.NotEnoughCoins, refused[0].Outcome);
        }

        // --- The invariant ------------------------------------------------------

        [Test]
        public void ShopIsASink_CoinsLeaveCirculationOnlyByTheExactPrice()
        {
            var bar = Bar();
            var ledger = LedgerWith(aliceCoins: 100, bobCoins: 100);
            Assert.AreEqual(200, ledger.TotalInCirculation());

            bar.Purchase(ledger, ALICE, PINT);   // -10
            Assert.AreEqual(190, ledger.TotalInCirculation());

            bar.Purchase(ledger, BOB, PINT);     // -10
            Assert.AreEqual(180, ledger.TotalInCirculation());

            bar.Purchase(ledger, ALICE, 999);            // unknown  — no drift
            bar.Purchase(ledger, ALICE, GOLDEN_HAT);     // too poor — no drift
            bar.Purchase(null, ALICE, PINT);             // bad call — no drift
            Assert.AreEqual(180, ledger.TotalInCirculation());
        }
    }
}
