using System;
using System.Collections.Generic;

namespace WeeSpurts.Gameplay
{
    /// <summary>
    /// Why a request was granted or refused. Same shape as
    /// <see cref="TicketResult"/> and for the same reason — a refusal is a
    /// normal game event the UI has to render, not an exceptional condition.
    /// </summary>
    public enum InventoryResult
    {
        Added,
        Removed,
        UnknownItem,
        NotEnough,
        Full,
        InvalidAmount
    }

    /// <summary>
    /// One slot: an item id and how many of it, or empty. A struct because it
    /// is small, immutable and created constantly.
    ///
    /// EMPTY IS "Count &lt;= 0", NOT "ItemId == 0" — item id 0 is a legal id
    /// (nothing in this project reserves it), so the slot's own count is the
    /// only thing that means "nothing here". Never branch on ItemId to decide
    /// emptiness; always check <see cref="IsEmpty"/>.
    /// </summary>
    public readonly struct InventorySlot
    {
        public readonly int ItemId;
        public readonly int Count;

        public bool IsEmpty => Count <= 0;

        public InventorySlot(int itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }
    }

    /// <summary>
    /// The answer to one Add/Remove request. A struct for the same reason
    /// <see cref="TicketTransaction"/> is.
    /// </summary>
    public readonly struct InventoryChange
    {
        public readonly InventoryResult Result;
        public readonly int ItemId;
        /// <summary>Signed: positive for an add, negative for a remove. Zero on a refusal.</summary>
        public readonly int Delta;
        /// <summary>This item's total count across every slot, after the change.</summary>
        public readonly int CountAfter;

        public bool Granted => Result == InventoryResult.Added || Result == InventoryResult.Removed;

        public InventoryChange(InventoryResult result, int itemId, int delta, int countAfter)
        {
            Result = result;
            ItemId = itemId;
            Delta = delta;
            CountAfter = countAfter;
        }
    }

    /// <summary>
    /// ONE player's carried items — a fixed number of slots, each holding an
    /// item id and a count. The backbone Stages 4-6 build the throw loadout
    /// and lane sabotage on top of (`Docs/Prompts/2026-08-11-venue-economy-
    /// buildout.md` Stage 3).
    ///
    /// SAME THREE RULES `TicketLedger` FOLLOWS, applied to items instead of
    /// balances:
    ///
    /// RULE 1 — ONE CHOKE POINT. Nothing reaches into a slot directly; every
    /// change goes through <see cref="TryAdd"/> or <see cref="Remove"/>.
    ///
    /// RULE 2 — REQUEST, THEN DECIDE, ATOMICALLY. TryAdd either fits the
    /// WHOLE amount or changes nothing at all — same reasoning as
    /// TicketLedger.Transfer being all-or-nothing: a partial add would leave
    /// a caller reconciling "some of it fit", and under Mirror the same call
    /// becomes a Command with no call-site changes.
    ///
    /// RULE 3 — IDS, NEVER REFERENCES. Items are ints, exactly like tickets
    /// and vendor items.
    ///
    /// DELIBERATELY PURE C#: no MonoBehaviour, no UnityEngine, no statics, no
    /// singleton — unit-tested without Play mode, the same bet that made
    /// TicketLedger and BlackjackTable trustworthy without pressing Play.
    /// </summary>
    public class Inventory
    {
        // Its OWN copy of the catalog, not a reference to the ItemCatalog
        // ScriptableObject — same split Vendor makes from VendorConfig. This
        // is what keeps Inventory testable without touching UnityEngine at
        // all (ItemCatalog is a ScriptableObject; InventoryItem, like
        // VendorItem, is a plain [Serializable] class with no Unity base
        // type, so holding a list of THOSE costs nothing).
        private readonly List<InventoryItem> _catalog = new List<InventoryItem>();
        private readonly InventorySlot[] _slots;

        /// <summary>Raised after every GRANTED Add/Remove, never for a refusal — same split as TicketLedger.</summary>
        public event Action<InventoryChange> OnChanged;

        /// <summary>Raised for refusals — a full inventory, an unknown item, not enough to remove.</summary>
        public event Action<InventoryChange> OnRefused;

        /// <summary>Raised whenever <see cref="HeldSlotIndex"/> changes, whether from <see cref="SelectSlot"/>, cycling, or a <see cref="Drop"/> that empties the held slot.</summary>
        public event Action<int> OnHeldChanged;

        public int SlotCount => _slots.Length;

        /// <summary>
        /// Which slot a HUD should show as "in hand". -1 means nothing held —
        /// the starting state, and also where <see cref="CycleNext"/>/
        /// <see cref="CycledPrev"/> leave it if nothing in the inventory is
        /// non-empty. Unlike <see cref="SelectSlot"/>, cycling never lands on
        /// an empty slot — see <see cref="NextNonEmptySlot"/>.
        /// </summary>
        public int HeldSlotIndex { get; private set; } = -1;

        /// <param name="catalog">What TryAdd checks new items against — item existence and stack rules. May be null (every TryAdd then refuses as UnknownItem, no config no crash — same as a Vendor with no items). On a duplicate id the FIRST entry wins, same rule Vendor uses for its own item list.</param>
        /// <param name="slotCount">Negative clamps to zero — a zero-slot inventory is legal, just permanently full.</param>
        public Inventory(IEnumerable<InventoryItem> catalog, int slotCount)
        {
            _slots = new InventorySlot[Math.Max(0, slotCount)];

            if (catalog == null) return;
            foreach (InventoryItem item in catalog)
            {
                if (item == null) continue;
                if (TryFindInCatalog(item.ItemId, out _)) continue;
                _catalog.Add(item);
            }
        }

        /// <summary>The slot at <paramref name="index"/>, or an empty slot for an out-of-range index rather than throwing.</summary>
        public InventorySlot SlotAt(int index) =>
            index >= 0 && index < _slots.Length ? _slots[index] : default;

        public bool Has(int itemId, int amount = 1) => amount > 0 && CountOf(itemId) >= amount;

        public int CountOf(int itemId)
        {
            int total = 0;
            for (int i = 0; i < _slots.Length; i++)
                if (!_slots[i].IsEmpty && _slots[i].ItemId == itemId) total += _slots[i].Count;
            return total;
        }

        /// <summary>
        /// Try to add <paramref name="amount"/> of <paramref name="itemId"/>.
        /// ALL OR NOTHING — see Rule 2. Fills existing stacks of the same item
        /// before spilling into empty slots, so a partial stack never sits
        /// unfilled next to a full one for no reason.
        /// </summary>
        public InventoryChange TryAdd(int itemId, int amount = 1)
        {
            if (amount <= 0) return Refuse(InventoryResult.InvalidAmount, itemId);
            if (!TryFindInCatalog(itemId, out InventoryItem item))
                return Refuse(InventoryResult.UnknownItem, itemId);

            int cap = EffectiveMaxStack(item);

            // Dry run first: is there room for the WHOLE amount? Existing
            // stacks of this item, then empty slots — same order the real
            // placement below uses, so this can never say yes when the real
            // pass would fall short.
            int capacity = 0;
            for (int i = 0; i < _slots.Length && capacity < amount; i++)
            {
                if (_slots[i].IsEmpty) capacity += cap;
                else if (_slots[i].ItemId == itemId) capacity += cap - _slots[i].Count;
            }
            if (capacity < amount) return Refuse(InventoryResult.Full, itemId);

            int remaining = amount;
            for (int i = 0; i < _slots.Length && remaining > 0; i++)
            {
                if (_slots[i].IsEmpty || _slots[i].ItemId != itemId) continue;
                int room = cap - _slots[i].Count;
                if (room <= 0) continue;
                int add = Math.Min(room, remaining);
                _slots[i] = new InventorySlot(itemId, _slots[i].Count + add);
                remaining -= add;
            }
            for (int i = 0; i < _slots.Length && remaining > 0; i++)
            {
                if (!_slots[i].IsEmpty) continue;
                int add = Math.Min(cap, remaining);
                _slots[i] = new InventorySlot(itemId, add);
                remaining -= add;
            }

            return Grant(InventoryResult.Added, itemId, amount);
        }

        /// <summary>
        /// Try to remove <paramref name="amount"/> of <paramref name="itemId"/>.
        /// ALL OR NOTHING, same as TryAdd. No catalog lookup needed — an id
        /// nobody has ever owned and an id that doesn't exist both read as
        /// "you don't have enough", which is the honest answer either way.
        /// </summary>
        public InventoryChange Remove(int itemId, int amount = 1)
        {
            if (amount <= 0) return Refuse(InventoryResult.InvalidAmount, itemId);
            if (CountOf(itemId) < amount) return Refuse(InventoryResult.NotEnough, itemId);

            int remaining = amount;
            for (int i = 0; i < _slots.Length && remaining > 0; i++)
            {
                if (_slots[i].IsEmpty || _slots[i].ItemId != itemId) continue;
                int take = Math.Min(_slots[i].Count, remaining);
                int newCount = _slots[i].Count - take;
                _slots[i] = newCount > 0 ? new InventorySlot(itemId, newCount) : default;
                remaining -= take;
            }

            return Grant(InventoryResult.Removed, itemId, -amount);
        }

        /// <summary>Empties every slot. No event fired — same silent-reset precedent as Vendor.ResetStock.</summary>
        public void Clear()
        {
            for (int i = 0; i < _slots.Length; i++) _slots[i] = default;
        }

        /// <summary>
        /// Drop <paramref name="amount"/> of <paramref name="itemId"/> out of
        /// the inventory — same removal as <see cref="Remove"/> (all or
        /// nothing, same result shape), except that if this empties OUT the
        /// currently <see cref="HeldSlotIndex"/> slot, the hold moves to
        /// whatever's next rather than leaving a HUD pointed at an empty
        /// slot. Only reacts when the held slot was actually holding the
        /// item being dropped — dropping something you're not holding never
        /// touches <see cref="HeldSlotIndex"/>.
        /// </summary>
        public InventoryChange Drop(int itemId, int amount = 1)
        {
            bool heldSlotHadThisItem = HeldSlotIndex >= 0 && HeldSlotIndex < _slots.Length
                && !_slots[HeldSlotIndex].IsEmpty && _slots[HeldSlotIndex].ItemId == itemId;

            InventoryChange change = Remove(itemId, amount);

            if (change.Granted && heldSlotHadThisItem && _slots[HeldSlotIndex].IsEmpty)
            {
                HeldSlotIndex = FirstNonEmptySlot();
                OnHeldChanged?.Invoke(HeldSlotIndex);
            }

            return change;
        }

        /// <summary>The lowest-index non-empty slot, or -1 if every slot is empty.</summary>
        public int FirstNonEmptySlot()
        {
            for (int i = 0; i < _slots.Length; i++)
                if (!_slots[i].IsEmpty) return i;
            return -1;
        }

        /// <summary>
        /// The next non-empty slot after <paramref name="from"/>, wrapping
        /// around. -1 if nothing in the inventory is non-empty. A bounded
        /// scan (at most <see cref="SlotCount"/> steps) rather than a
        /// while-true, so an empty or single-item inventory can never hang.
        /// </summary>
        public int NextNonEmptySlot(int from)
        {
            if (_slots.Length == 0) return -1;

            for (int step = 1; step <= _slots.Length; step++)
            {
                int index = Wrap(from + step);
                if (!_slots[index].IsEmpty) return index;
            }

            return -1;
        }

        /// <summary>The previous non-empty slot before <paramref name="from"/>, wrapping around. -1 if nothing is non-empty. Same bounded-scan shape as <see cref="NextNonEmptySlot"/>.</summary>
        public int PrevNonEmptySlot(int from)
        {
            if (_slots.Length == 0) return -1;

            for (int step = 1; step <= _slots.Length; step++)
            {
                int index = Wrap(from - step);
                if (!_slots[index].IsEmpty) return index;
            }

            return -1;
        }

        /// <summary>
        /// Point the hold directly at a slot — a HUD click, unlike cycling,
        /// MAY land on an empty slot (that's a legal "holding nothing, but
        /// this slot is highlighted" state). False on an out-of-range index;
        /// the hold is left untouched rather than clamped.
        /// </summary>
        public bool SelectSlot(int index)
        {
            if (index < 0 || index >= _slots.Length) return false;
            if (HeldSlotIndex == index) return true;

            HeldSlotIndex = index;
            OnHeldChanged?.Invoke(HeldSlotIndex);
            return true;
        }

        /// <summary>Hold the next non-empty slot, wrapping around. A no-op (no event) if nothing is non-empty, or if it's the only one already held.</summary>
        public void CycleNext()
        {
            int next = NextNonEmptySlot(HeldSlotIndex);
            if (next == -1 || next == HeldSlotIndex) return;

            HeldSlotIndex = next;
            OnHeldChanged?.Invoke(HeldSlotIndex);
        }

        /// <summary>Hold the previous non-empty slot, wrapping around. Same no-op rules as <see cref="CycleNext"/>.</summary>
        public void CycledPrev()
        {
            int prev = PrevNonEmptySlot(HeldSlotIndex);
            if (prev == -1 || prev == HeldSlotIndex) return;

            HeldSlotIndex = prev;
            OnHeldChanged?.Invoke(HeldSlotIndex);
        }

        /// <summary>Positive-safe modulo against <see cref="SlotCount"/> — <c>-1 % n</c> in C# is negative, which is not a valid array index.</summary>
        private int Wrap(int index)
        {
            int n = _slots.Length;
            int wrapped = index % n;
            return wrapped < 0 ? wrapped + n : wrapped;
        }

        private bool TryFindInCatalog(int itemId, out InventoryItem found)
        {
            for (int i = 0; i < _catalog.Count; i++)
            {
                if (_catalog[i].ItemId != itemId) continue;
                found = _catalog[i];
                return true;
            }

            found = null;
            return false;
        }

        private static int EffectiveMaxStack(InventoryItem item) =>
            item.Stackable ? Math.Max(1, item.MaxStack) : 1;

        private InventoryChange Grant(InventoryResult result, int itemId, int delta)
        {
            var change = new InventoryChange(result, itemId, delta, CountOf(itemId));
            OnChanged?.Invoke(change);
            return change;
        }

        private InventoryChange Refuse(InventoryResult result, int itemId)
        {
            var change = new InventoryChange(result, itemId, 0, CountOf(itemId));
            OnRefused?.Invoke(change);
            return change;
        }
    }
}
