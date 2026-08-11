using System;
using System.Collections.Generic;
using WeeSpurts.Gameplay;

namespace WeeSpurts.Slop
{
    /// <summary>Why a purchase did or didn't happen. Rendered by the UI, never thrown.</summary>
    public enum PurchaseOutcome
    {
        Purchased,
        UnknownItem,
        OutOfStock,
        NotEnoughTickets,
        InvalidRequest
    }

    /// <summary>
    /// The answer to one purchase attempt. Carries the underlying
    /// <see cref="TicketTransaction"/> so a caller that wants the new balance
    /// doesn't have to go and ask the ledger again.
    /// </summary>
    public readonly struct PurchaseResult
    {
        public readonly PurchaseOutcome Outcome;
        public readonly ulong PlayerId;
        public readonly int ItemId;
        public readonly int Price;
        public readonly TicketTransaction Transaction;
        /// <summary>Negative means unlimited. Unchanged on a failure.</summary>
        public readonly int StockRemaining;

        public bool Success => Outcome == PurchaseOutcome.Purchased;

        public PurchaseResult(PurchaseOutcome outcome, ulong playerId, int itemId, int price,
                              TicketTransaction transaction, int stockRemaining)
        {
            Outcome = outcome;
            PlayerId = playerId;
            ItemId = itemId;
            Price = price;
            Transaction = transaction;
            StockRemaining = stockRemaining;
        }
    }

    /// <summary>
    /// A place that takes tickets and gives you a thing: the bar, the cosmetics
    /// counter, later the card table's buy-in. `Docs/SlopLayerPlan.md` S4.
    ///
    /// DELIBERATELY PURE C# — no MonoBehaviour, no UnityEngine. The scene side
    /// (an interactable on the bar anchor, a prompt, a menu) is a thin shell
    /// that owns one of these and calls <see cref="Purchase"/>. That split is
    /// what lets the money rules be unit-tested without Play mode, which is
    /// the only reason you can trust them without playing the game.
    ///
    /// IT DOES NOT TOUCH BALANCES ITSELF. Every ticket movement goes through
    /// <see cref="TicketLedger"/> — Rule 1, one choke point. This class decides
    /// what things cost and whether any are left; the ledger decides whether
    /// the player can pay. When the networked wrapper lands, this class does
    /// not change at all.
    ///
    /// WHAT IT DELIBERATELY DOESN'T KNOW: what a pint DOES. Buying drink id 3
    /// raises <see cref="OnPurchased"/> and that is the end of this class's
    /// interest. S3 (drink meter) subscribes and decides what being three
    /// pints deep means for your throw. Keeping the shop ignorant of the
    /// effect is what stops "the bar" and "the drink meter" becoming one
    /// tangled system that can only be tested by playing.
    /// </summary>
    public class Vendor
    {
        /// <summary>Display name for the prompt — "The Bar", "Cosmetics". Never switch logic on it.</summary>
        public string Name { get; }

        private readonly List<VendorItem> _items;

        /// <summary>Item id -> units sold this match. Absent means none sold.</summary>
        private readonly Dictionary<int, int> _sold = new Dictionary<int, int>();

        /// <summary>
        /// Raised only on a SUCCESSFUL purchase. This is the seam S3 and P1
        /// hang off: the drink meter listens for bar items, cosmetics listens
        /// for hat items, and neither has to know the other exists.
        /// </summary>
        public event Action<PurchaseResult> OnPurchased;

        /// <summary>Raised on every failure, so the UI can shake, buzz, or have the barman shrug.</summary>
        public event Action<PurchaseResult> OnRefused;

        public IReadOnlyList<VendorItem> Items => _items;

        public Vendor(string name, IEnumerable<VendorItem> items)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Vendor" : name;
            _items = new List<VendorItem>();

            if (items == null) return;
            foreach (VendorItem item in items)
            {
                // A null entry is what an unfilled row in the Inspector list
                // looks like, and a duplicate id would make TryGetItem
                // ambiguous forever after. Skip both rather than throwing —
                // a half-filled config should still let you play.
                if (item == null) continue;
                if (Contains(item.ItemId)) continue;
                _items.Add(item);
            }
        }

        public bool Contains(int itemId) => TryGetItem(itemId, out _);

        public bool TryGetItem(int itemId, out VendorItem found)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].ItemId != itemId) continue;
                found = _items[i];
                return true;
            }

            found = null;
            return false;
        }

        /// <summary>
        /// Units left this match. Negative means unlimited; 0 means sold out;
        /// an unknown item also reads 0, because "you can't have one" is the
        /// honest answer either way.
        /// </summary>
        public int StockRemaining(int itemId)
        {
            if (!TryGetItem(itemId, out VendorItem item)) return 0;
            if (item.IsUnlimited) return -1;

            _sold.TryGetValue(itemId, out int sold);
            int left = item.StockPerMatch - sold;
            return left < 0 ? 0 : left;
        }

        /// <summary>
        /// Can this player buy it right now? Pure query. For greying out a menu
        /// row BEFORE the click — never as a pre-check you then act on, because
        /// the balance can move between the check and the purchase (a bet
        /// resolves, a payout lands). Always act on <see cref="Purchase"/>'s result.
        /// </summary>
        public bool CanBuy(TicketLedger ledger, ulong playerId, int itemId)
        {
            if (ledger == null) return false;
            if (!TryGetItem(itemId, out VendorItem item)) return false;
            if (StockRemaining(itemId) == 0) return false;
            return ledger.CanAfford(playerId, item.Price);
        }

        /// <summary>
        /// Try to buy. The ORDER OF THE CHECKS HERE IS THE WHOLE POINT:
        ///
        ///   1. Does the item exist?
        ///   2. Is there stock left?
        ///   3. Ask the ledger to take the tickets.
        ///   4. ONLY IF the ledger granted, consume a unit of stock.
        ///
        /// Step 4 after step 3 is not a style preference. Consume stock first
        /// and a player who can't afford the last golden hat destroys it for
        /// everyone — a refused purchase must leave the world exactly as it
        /// found it. Same reasoning as TicketLedger.Transfer being atomic.
        /// </summary>
        public PurchaseResult Purchase(TicketLedger ledger, ulong playerId, int itemId)
        {
            if (ledger == null)
                return Fail(PurchaseOutcome.InvalidRequest, playerId, itemId, 0);

            if (!TryGetItem(itemId, out VendorItem item))
                return Fail(PurchaseOutcome.UnknownItem, playerId, itemId, 0);

            if (StockRemaining(itemId) == 0)
                return Fail(PurchaseOutcome.OutOfStock, playerId, itemId, item.Price);

            // A free or negatively-priced item would make the ledger refuse
            // with InvalidAmount, which reads as a bug rather than a config
            // mistake. Catch it here where the message can be honest.
            if (item.Price <= 0)
                return Fail(PurchaseOutcome.InvalidRequest, playerId, itemId, item.Price);

            TicketTransaction tx = ledger.RequestSpend(playerId, item.Price, $"{Name}:{item.DisplayName}");
            if (!tx.Granted)
            {
                // Everything that isn't "you're broke" — unknown player, bad
                // amount — is a wiring mistake rather than a player-facing
                // state, so it is reported as invalid rather than pretending
                // they were short of tickets.
                PurchaseOutcome why = tx.Result == TicketResult.InsufficientFunds
                    ? PurchaseOutcome.NotEnoughTickets
                    : PurchaseOutcome.InvalidRequest;

                var refused = new PurchaseResult(why, playerId, itemId, item.Price, tx, StockRemaining(itemId));
                OnRefused?.Invoke(refused);
                return refused;
            }

            if (!item.IsUnlimited)
            {
                _sold.TryGetValue(itemId, out int sold);
                _sold[itemId] = sold + 1;
            }

            var result = new PurchaseResult(PurchaseOutcome.Purchased, playerId, itemId,
                                            item.Price, tx, StockRemaining(itemId));
            OnPurchased?.Invoke(result);
            return result;
        }

        /// <summary>
        /// Restock for a new match. Stock is per-match by design — the golden
        /// hat gag only works if it comes back next game.
        /// </summary>
        public void ResetStock() => _sold.Clear();

        private PurchaseResult Fail(PurchaseOutcome outcome, ulong playerId, int itemId, int price)
        {
            var result = new PurchaseResult(outcome, playerId, itemId, price, default, StockRemaining(itemId));
            OnRefused?.Invoke(result);
            return result;
        }
    }
}
