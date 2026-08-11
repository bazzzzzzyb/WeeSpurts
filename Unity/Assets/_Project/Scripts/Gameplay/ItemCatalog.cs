using System;
using UnityEngine;

namespace WeeSpurts.Gameplay
{
    /// <summary>
    /// What happens when you USE an inventory item. Read by whatever system
    /// drives the throw loadout / lane sabotage later (Stages 4-6); Stage 3
    /// only needs the enum to exist so the catalog's shape is right.
    /// </summary>
    public enum ItemUseContext
    {
        /// <summary>Thrown down the lane in place of the ball — an oil can, the loose pin.</summary>
        Throwable,
        /// <summary>Used ON the lane without a throw — the mop.</summary>
        LaneAction,
        /// <summary>Used on yourself immediately, no throw, no lane — a drink, a cosmetic.</summary>
        Instant
    }

    /// <summary>
    /// One thing that can sit in an <see cref="Inventory"/> slot — an oil can, a
    /// mop, a wet floor sign. Same shape and same reasoning as
    /// <see cref="WeeSpurts.Slop.VendorItem"/>: plain serializable data, so
    /// Tony edits the list in the Inspector without a recompile.
    ///
    /// RULE 3 FROM Docs/SlopLayerPlan.md — IDS, NEVER REFERENCES, same as
    /// every other economy id in this project. Whatever the id MEANS (which
    /// mesh, which throw behaviour) is resolved locally from the id.
    /// </summary>
    [Serializable]
    public class InventoryItem
    {
        [Tooltip("Stable, unique within this catalog. Sent over the network later, same as VendorItem.ItemId — DO NOT renumber existing items once anyone has one; add new ids instead.")]
        public int ItemId;

        [Tooltip("What the inventory UI and prompts say. Display only — never switch game logic on this string.")]
        public string DisplayName = "Item";

        [Tooltip("Optional. No inventory UI exists yet to draw it, but the field is here now so adding one later doesn't need a data migration.")]
        public Sprite Icon;

        [Tooltip("Can more than one sit in the same slot? OFF means every unit takes its own slot regardless of MaxStack (a mop doesn't stack with another mop).")]
        public bool Stackable = true;

        [Tooltip("Ignored when Stackable is OFF. How many of this item share one slot.")]
        public int MaxStack = 99;

        [Tooltip("How this item gets USED — thrown down the lane, used on the lane without a throw, or used on yourself instantly. Read by the throw loadout / sabotage systems, not by Inventory itself.")]
        public ItemUseContext UseContext = ItemUseContext.Instant;
    }

    /// <summary>
    /// Every item that exists, as an asset Tony can edit in the Inspector —
    /// same DATA/LOGIC split as <see cref="WeeSpurts.Slop.VendorConfig"/>:
    /// this is DATA, <see cref="Inventory"/> is the LOGIC and knows nothing
    /// about Unity beyond this catalog's plain data.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemCatalog", menuName = "WeeSpurts/Item Catalog")]
    public class ItemCatalog : ScriptableObject
    {
        [Tooltip("Every item that exists. ItemIds must be unique — on a duplicate, the FIRST entry with that id wins (same rule Vendor uses for its own item list).")]
        public InventoryItem[] Items = new InventoryItem[0];

        public bool TryGetItem(int itemId, out InventoryItem found)
        {
            for (int i = 0; i < Items.Length; i++)
            {
                if (Items[i] == null || Items[i].ItemId != itemId) continue;
                found = Items[i];
                return true;
            }

            found = null;
            return false;
        }

        /// <summary>
        /// Build a runtime <see cref="Inventory"/> against this catalog. Same
        /// bridge role as VendorConfig.CreateVendor/BlackjackConfig.CreateRules
        /// — the only place this asset's data crosses into the pure-C# engine.
        /// Slot count is NOT this asset's business (it's per-player, this
        /// catalog is shared/global), so it stays a parameter rather than a
        /// field here.
        /// </summary>
        public Inventory CreateInventory(int slotCount) => new Inventory(Items, slotCount);
    }
}
