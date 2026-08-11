using System.Collections.Generic;
using NUnit.Framework;
using WeeSpurts.Gameplay;

namespace WeeSpurts.Tests
{
    /// <summary>
    /// Unit tests for the pure-C# inventory (Docs/Prompts/2026-08-11-venue-
    /// economy-buildout.md Stage 3). Run in Unity: Window > General > Test
    /// Runner > EditMode > Run All.
    ///
    /// The headline rule these protect: TryAdd/Remove are ALL OR NOTHING — a
    /// refusal must leave every slot exactly as it found them, same as a
    /// refused TicketLedger.RequestSpend leaves the balance untouched.
    /// </summary>
    public class InventoryTests
    {
        private const int APPLE = 1;    // stackable, max 5
        private const int MOP = 2;      // not stackable
        private const int UNKNOWN = 999;

        // No ItemCatalog (ScriptableObject) touched anywhere in this file —
        // Inventory takes a plain IEnumerable<InventoryItem>, same reason
        // VendorTests never instantiates a VendorConfig either.
        private static List<InventoryItem> Catalog() => new List<InventoryItem>
        {
            new InventoryItem { ItemId = APPLE, DisplayName = "Apple", Stackable = true, MaxStack = 5 },
            new InventoryItem { ItemId = MOP, DisplayName = "Mop", Stackable = false, MaxStack = 99 }
        };

        // --- Adding --------------------------------------------------------

        [Test]
        public void TryAdd_UnknownItem_IsRefused()
        {
            var inv = new Inventory(Catalog(), 3);
            InventoryChange change = inv.TryAdd(UNKNOWN, 1);

            Assert.IsFalse(change.Granted);
            Assert.AreEqual(InventoryResult.UnknownItem, change.Result);
            Assert.AreEqual(0, inv.CountOf(UNKNOWN));
        }

        [Test]
        public void TryAdd_NoCatalog_IsRefusedRatherThanThrowing()
        {
            var inv = new Inventory(null, 3);
            Assert.AreEqual(InventoryResult.UnknownItem, inv.TryAdd(APPLE, 1).Result);
        }

        [Test]
        public void TryAdd_NewItem_FillsAnEmptySlot()
        {
            var inv = new Inventory(Catalog(), 3);
            InventoryChange change = inv.TryAdd(APPLE, 3);

            Assert.IsTrue(change.Granted);
            Assert.AreEqual(InventoryResult.Added, change.Result);
            Assert.AreEqual(3, change.CountAfter);
            Assert.AreEqual(3, inv.CountOf(APPLE));
            Assert.AreEqual(APPLE, inv.SlotAt(0).ItemId);
            Assert.AreEqual(3, inv.SlotAt(0).Count);
            Assert.IsTrue(inv.SlotAt(1).IsEmpty);
        }

        [Test]
        public void TryAdd_StackableItem_FillsExistingStackBeforeANewSlot()
        {
            var inv = new Inventory(Catalog(), 3);
            inv.TryAdd(APPLE, 2);   // slot 0: 2/5
            inv.TryAdd(APPLE, 2);   // should top up slot 0 to 4/5, not open slot 1

            Assert.AreEqual(4, inv.SlotAt(0).Count);
            Assert.IsTrue(inv.SlotAt(1).IsEmpty, "a second stack should not open while the first has room");
        }

        [Test]
        public void TryAdd_PastMaxStack_SpillsIntoANewSlot()
        {
            var inv = new Inventory(Catalog(), 3);
            InventoryChange change = inv.TryAdd(APPLE, 7); // MaxStack 5: 5 + 2

            Assert.IsTrue(change.Granted);
            Assert.AreEqual(5, inv.SlotAt(0).Count);
            Assert.AreEqual(2, inv.SlotAt(1).Count);
            Assert.AreEqual(7, inv.CountOf(APPLE));
        }

        [Test]
        public void TryAdd_NonStackableItem_NeverSharesASlot()
        {
            var inv = new Inventory(Catalog(), 3);
            inv.TryAdd(MOP, 1);
            inv.TryAdd(MOP, 1);

            Assert.AreEqual(1, inv.SlotAt(0).Count);
            Assert.AreEqual(1, inv.SlotAt(1).Count, "a second mop must open a second slot, not stack onto the first");
            Assert.AreEqual(2, inv.CountOf(MOP));
        }

        [Test]
        public void TryAdd_NotEnoughRoom_RefusesAndChangesNothing()
        {
            // 3 slots, MaxStack 5 each: 15 is the ceiling for apples alone.
            var inv = new Inventory(Catalog(), 3);
            inv.TryAdd(APPLE, 15); // fills every slot exactly full

            InventoryChange change = inv.TryAdd(APPLE, 1);

            Assert.IsFalse(change.Granted);
            Assert.AreEqual(InventoryResult.Full, change.Result);
            Assert.AreEqual(15, inv.CountOf(APPLE), "a refused add must not partially land");
        }

        [Test]
        public void TryAdd_WouldPartiallyFit_RefusesTheWholeAmountAtomically()
        {
            // 1 empty slot left (cap 5) plus MOP takes it all: asking for 6
            // apples must refuse ENTIRELY, not add 5 and drop 1.
            var inv = new Inventory(Catalog(), 2);
            inv.TryAdd(MOP, 1);         // slot 0 taken
            InventoryChange change = inv.TryAdd(APPLE, 6); // only slot 1 (cap 5) is free

            Assert.IsFalse(change.Granted);
            Assert.AreEqual(InventoryResult.Full, change.Result);
            Assert.AreEqual(0, inv.CountOf(APPLE), "an all-or-nothing add must not leave a partial stack behind");
        }

        [Test]
        public void TryAdd_ZeroOrNegative_IsRefused()
        {
            var inv = new Inventory(Catalog(), 3);
            Assert.AreEqual(InventoryResult.InvalidAmount, inv.TryAdd(APPLE, 0).Result);
            Assert.AreEqual(InventoryResult.InvalidAmount, inv.TryAdd(APPLE, -1).Result);
        }

        // --- Removing --------------------------------------------------------

        [Test]
        public void Remove_TakesFromExistingStacks()
        {
            var inv = new Inventory(Catalog(), 3);
            inv.TryAdd(APPLE, 5);

            InventoryChange change = inv.Remove(APPLE, 2);

            Assert.IsTrue(change.Granted);
            Assert.AreEqual(InventoryResult.Removed, change.Result);
            Assert.AreEqual(-2, change.Delta);
            Assert.AreEqual(3, inv.CountOf(APPLE));
        }

        [Test]
        public void Remove_EmptiesASlotCompletely_RatherThanLeavingAZeroStack()
        {
            var inv = new Inventory(Catalog(), 3);
            inv.TryAdd(MOP, 1);

            inv.Remove(MOP, 1);

            Assert.IsTrue(inv.SlotAt(0).IsEmpty);
        }

        [Test]
        public void Remove_NotEnough_RefusesAndChangesNothing()
        {
            var inv = new Inventory(Catalog(), 3);
            inv.TryAdd(APPLE, 2);

            InventoryChange change = inv.Remove(APPLE, 5);

            Assert.IsFalse(change.Granted);
            Assert.AreEqual(InventoryResult.NotEnough, change.Result);
            Assert.AreEqual(2, inv.CountOf(APPLE));
        }

        [Test]
        public void Remove_UnknownOrNeverOwnedItem_IsNotEnoughNotAnException()
        {
            var inv = new Inventory(Catalog(), 3);
            Assert.AreEqual(InventoryResult.NotEnough, inv.Remove(UNKNOWN, 1).Result);
        }

        [Test]
        public void Remove_ZeroOrNegative_IsRefused()
        {
            var inv = new Inventory(Catalog(), 3);
            inv.TryAdd(APPLE, 5);
            Assert.AreEqual(InventoryResult.InvalidAmount, inv.Remove(APPLE, 0).Result);
            Assert.AreEqual(InventoryResult.InvalidAmount, inv.Remove(APPLE, -1).Result);
            Assert.AreEqual(5, inv.CountOf(APPLE));
        }

        // --- Queries -----------------------------------------------------------

        [Test]
        public void CountOf_SumsAcrossMultipleSlots()
        {
            var inv = new Inventory(Catalog(), 3);
            inv.TryAdd(APPLE, 7); // 5 + 2 across two slots

            Assert.AreEqual(7, inv.CountOf(APPLE));
        }

        [Test]
        public void Has_ReflectsCountOf()
        {
            var inv = new Inventory(Catalog(), 3);
            inv.TryAdd(APPLE, 3);

            Assert.IsTrue(inv.Has(APPLE, 3));
            Assert.IsFalse(inv.Has(APPLE, 4));
        }

        [Test]
        public void SlotAt_OutOfRange_ReturnsEmptyRatherThanThrowing()
        {
            var inv = new Inventory(Catalog(), 3);
            Assert.IsTrue(inv.SlotAt(-1).IsEmpty);
            Assert.IsTrue(inv.SlotAt(99).IsEmpty);
        }

        [Test]
        public void NegativeSlotCount_ClampsToZero()
        {
            var inv = new Inventory(Catalog(), -5);
            Assert.AreEqual(0, inv.SlotCount);
            Assert.AreEqual(InventoryResult.Full, inv.TryAdd(APPLE, 1).Result);
        }

        [Test]
        public void Clear_EmptiesEverySlot()
        {
            var inv = new Inventory(Catalog(), 3);
            inv.TryAdd(APPLE, 5);
            inv.TryAdd(MOP, 1);

            inv.Clear();

            Assert.AreEqual(0, inv.CountOf(APPLE));
            Assert.AreEqual(0, inv.CountOf(MOP));
            for (int i = 0; i < inv.SlotCount; i++) Assert.IsTrue(inv.SlotAt(i).IsEmpty);
        }

        // --- Events -------------------------------------------------------

        [Test]
        public void OnChanged_FiresOnlyForGrants()
        {
            var inv = new Inventory(Catalog(), 1); // 1 slot, cap 5 for apples
            var granted = new List<InventoryChange>();
            var refused = new List<InventoryChange>();
            inv.OnChanged += granted.Add;
            inv.OnRefused += refused.Add;

            inv.TryAdd(APPLE, 3);      // granted
            inv.TryAdd(APPLE, 999);    // refused (Full)

            Assert.AreEqual(1, granted.Count);
            Assert.AreEqual(InventoryResult.Added, granted[0].Result);
            Assert.AreEqual(1, refused.Count);
            Assert.AreEqual(InventoryResult.Full, refused[0].Result);
        }

        // --- Drop (Docs/Prompts/2026-08-11-executive-day-plan.md, Block 1 task 4) ---

        [Test]
        public void Drop_SameShapeAsRemove_TakesFromExistingStacks()
        {
            var inv = new Inventory(Catalog(), 3);
            inv.TryAdd(APPLE, 5);

            InventoryChange change = inv.Drop(APPLE, 2);

            Assert.IsTrue(change.Granted);
            Assert.AreEqual(InventoryResult.Removed, change.Result);
            Assert.AreEqual(-2, change.Delta);
            Assert.AreEqual(3, inv.CountOf(APPLE));
        }

        [Test]
        public void Drop_NotEnough_RefusesAndChangesNothing()
        {
            var inv = new Inventory(Catalog(), 3);
            inv.TryAdd(APPLE, 2);

            InventoryChange change = inv.Drop(APPLE, 5);

            Assert.IsFalse(change.Granted);
            Assert.AreEqual(InventoryResult.NotEnough, change.Result);
            Assert.AreEqual(2, inv.CountOf(APPLE));
        }

        [Test]
        public void Drop_ZeroOrNegative_IsRefused()
        {
            var inv = new Inventory(Catalog(), 3);
            inv.TryAdd(APPLE, 5);
            Assert.AreEqual(InventoryResult.InvalidAmount, inv.Drop(APPLE, 0).Result);
            Assert.AreEqual(InventoryResult.InvalidAmount, inv.Drop(APPLE, -1).Result);
            Assert.AreEqual(5, inv.CountOf(APPLE));
        }

        // --- FirstNonEmptySlot / NextNonEmptySlot / PrevNonEmptySlot ---

        [Test]
        public void FirstNonEmptySlot_SkipsLeadingEmptySlots()
        {
            var inv = new Inventory(Catalog(), 4);
            inv.TryAdd(APPLE, 1); // slot 0
            inv.TryAdd(MOP, 1);   // slot 1
            inv.Remove(APPLE, 1); // slot 0 empty again; slot 1 still holds the mop

            Assert.AreEqual(1, inv.FirstNonEmptySlot());
        }

        [Test]
        public void FirstNonEmptySlot_WithNothingOwned_IsNegativeOne()
        {
            var inv = new Inventory(Catalog(), 3);
            Assert.AreEqual(-1, inv.FirstNonEmptySlot());
        }

        [Test]
        public void NextAndPrevNonEmptySlot_SkipEmptySlotsAndWrap()
        {
            // Deterministic [Apple, empty, empty, Mop] layout: fill all four
            // slots (Apple, Mop, Mop, Mop), then remove 2 mops — Remove drains
            // matching slots in index order, so that empties slots 1 and 2
            // specifically and leaves slot 3's mop untouched.
            var inv = new Inventory(Catalog(), 4);
            inv.TryAdd(APPLE, 1); // slot 0
            inv.TryAdd(MOP, 1);   // slot 1
            inv.TryAdd(MOP, 1);   // slot 2
            inv.TryAdd(MOP, 1);   // slot 3
            inv.Remove(MOP, 2);   // empties slots 1 and 2

            Assert.AreEqual(3, inv.NextNonEmptySlot(0), "skips the empty middle and lands on slot 3");
            Assert.AreEqual(0, inv.NextNonEmptySlot(3), "wraps past the end back to slot 0");
            Assert.AreEqual(3, inv.PrevNonEmptySlot(0), "wraps before the start to the last non-empty slot");
            Assert.AreEqual(0, inv.PrevNonEmptySlot(3));
        }

        [Test]
        public void NextNonEmptySlot_WithOnlyOneItemOwned_ReturnsThatSameSlot()
        {
            var inv = new Inventory(Catalog(), 3);
            inv.TryAdd(APPLE, 1); // slot 0

            Assert.AreEqual(0, inv.NextNonEmptySlot(0), "the only non-empty slot wraps back to itself");
            Assert.AreEqual(0, inv.PrevNonEmptySlot(0));
        }

        [Test]
        public void NextAndPrevNonEmptySlot_WithNothingOwned_AreNegativeOneAndDoNotHang()
        {
            var inv = new Inventory(Catalog(), 5);
            Assert.AreEqual(-1, inv.NextNonEmptySlot(0));
            Assert.AreEqual(-1, inv.PrevNonEmptySlot(0));
        }

        [Test]
        public void NextNonEmptySlot_ZeroSlotInventory_IsNegativeOne()
        {
            var inv = new Inventory(Catalog(), 0);
            Assert.AreEqual(-1, inv.NextNonEmptySlot(0));
            Assert.AreEqual(-1, inv.PrevNonEmptySlot(0));
        }

        // --- HeldSlotIndex / SelectSlot / cycling ---

        [Test]
        public void NewInventory_HoldsNothing()
        {
            var inv = new Inventory(Catalog(), 3);
            Assert.AreEqual(-1, inv.HeldSlotIndex);
        }

        [Test]
        public void SelectSlot_InRange_SetsTheHoldAndFiresOnHeldChanged_EvenIfEmpty()
        {
            var inv = new Inventory(Catalog(), 3);
            var heldChanges = new List<int>();
            inv.OnHeldChanged += heldChanges.Add;

            bool selected = inv.SelectSlot(1);

            Assert.IsTrue(selected);
            Assert.AreEqual(1, inv.HeldSlotIndex, "selecting is a direct HUD action and may land on an empty slot");
            CollectionAssert.AreEqual(new[] { 1 }, heldChanges);
        }

        [Test]
        public void SelectSlot_OutOfRange_FailsAndLeavesTheHoldUntouched()
        {
            var inv = new Inventory(Catalog(), 3);
            inv.SelectSlot(1);

            Assert.IsFalse(inv.SelectSlot(-1));
            Assert.IsFalse(inv.SelectSlot(99));
            Assert.AreEqual(1, inv.HeldSlotIndex);
        }

        [Test]
        public void SelectSlot_SameSlotAgain_IsANoOp_NoEventFired()
        {
            var inv = new Inventory(Catalog(), 3);
            inv.SelectSlot(1);
            var heldChanges = new List<int>();
            inv.OnHeldChanged += heldChanges.Add;

            Assert.IsTrue(inv.SelectSlot(1));
            Assert.AreEqual(0, heldChanges.Count);
        }

        [Test]
        public void CycleNext_AcrossEmptySlots_LandsOnTheNextOwnedItem()
        {
            var inv = new Inventory(Catalog(), 4);
            inv.TryAdd(MOP, 1);   // slot 0
            inv.TryAdd(APPLE, 3); // slot 1

            inv.SelectSlot(0);
            inv.CycleNext(); // slots 2, 3 are empty; should land on slot 1

            Assert.AreEqual(1, inv.HeldSlotIndex);
        }

        [Test]
        public void CycleNext_WithNothingOwned_IsANoOpAndStaysAtNegativeOne()
        {
            var inv = new Inventory(Catalog(), 3);
            var heldChanges = new List<int>();
            inv.OnHeldChanged += heldChanges.Add;

            inv.CycleNext();
            inv.CycledPrev();

            Assert.AreEqual(-1, inv.HeldSlotIndex);
            Assert.AreEqual(0, heldChanges.Count, "nothing to hold means no event, not a phantom selection");
        }

        [Test]
        public void CycleNext_WithExactlyOneItemOwned_StaysOnIt_NoEventFired()
        {
            var inv = new Inventory(Catalog(), 3);
            inv.TryAdd(APPLE, 1);
            inv.SelectSlot(0);
            var heldChanges = new List<int>();
            inv.OnHeldChanged += heldChanges.Add;

            inv.CycleNext();

            Assert.AreEqual(0, inv.HeldSlotIndex);
            Assert.AreEqual(0, heldChanges.Count, "cycling onto the slot you already hold is a no-op");
        }

        [Test]
        public void CycledPrev_MirrorsCycleNext()
        {
            var inv = new Inventory(Catalog(), 4);
            inv.TryAdd(MOP, 1);   // slot 0
            inv.TryAdd(APPLE, 3); // slot 1
            inv.SelectSlot(1);

            inv.CycledPrev();
            Assert.AreEqual(0, inv.HeldSlotIndex);

            inv.CycledPrev();
            Assert.AreEqual(1, inv.HeldSlotIndex, "wraps back around");
        }

        // --- Dropping the held item ---

        [Test]
        public void Drop_TheHeldItemToZero_AdvancesTheHoldToTheNextOwnedItem()
        {
            var inv = new Inventory(Catalog(), 4);
            inv.TryAdd(MOP, 1);   // slot 0
            inv.TryAdd(APPLE, 3); // slot 1
            inv.SelectSlot(0);
            var heldChanges = new List<int>();
            inv.OnHeldChanged += heldChanges.Add;

            InventoryChange change = inv.Drop(MOP, 1); // empties slot 0, the held slot

            Assert.IsTrue(change.Granted);
            Assert.AreEqual(1, inv.HeldSlotIndex, "holding an emptied slot is never sensible when something else is carried");
            CollectionAssert.AreEqual(new[] { 1 }, heldChanges);
        }

        [Test]
        public void Drop_TheLastHeldItem_ClearsTheHoldToNegativeOne()
        {
            var inv = new Inventory(Catalog(), 3);
            inv.TryAdd(MOP, 1);
            inv.SelectSlot(0);

            inv.Drop(MOP, 1);

            Assert.AreEqual(-1, inv.HeldSlotIndex, "nothing left to hold");
        }

        [Test]
        public void Drop_SomethingOtherThanTheHeldItem_DoesNotTouchTheHold()
        {
            var inv = new Inventory(Catalog(), 4);
            inv.TryAdd(MOP, 1);   // slot 0
            inv.TryAdd(APPLE, 3); // slot 1
            inv.SelectSlot(0); // holding the mop

            inv.Drop(APPLE, 3); // drop the apples, not what's held

            Assert.AreEqual(0, inv.HeldSlotIndex, "dropping an item you aren't holding must not move the hold");
        }

        [Test]
        public void Drop_PartialStackOfTheHeldItem_LeavesTheHoldInPlace()
        {
            var inv = new Inventory(Catalog(), 3);
            inv.TryAdd(APPLE, 5);
            inv.SelectSlot(0);

            inv.Drop(APPLE, 2); // slot 0 still has 3 apples — not emptied

            Assert.AreEqual(0, inv.HeldSlotIndex);
        }
    }
}
