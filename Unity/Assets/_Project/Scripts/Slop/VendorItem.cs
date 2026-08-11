using System;
using UnityEngine;

namespace WeeSpurts.Slop
{
    /// <summary>
    /// One thing you can buy at an anchor — a pint at the bar, a hat at the
    /// cosmetics counter. Plain serializable data so it edits as a list inside
    /// a <see cref="VendorConfig"/> asset and Tony can retune every price
    /// without a recompile.
    ///
    /// RULE 3 FROM Docs/SlopLayerPlan.md — IDS, NEVER REFERENCES. An item is
    /// an <see cref="ItemId"/>, not a prefab pointer or a material. Purchases
    /// have to replicate, and an int replicates for free while an object
    /// reference does not replicate at all. Whatever the id MEANS (which hat
    /// mesh, which drink strength) is resolved on each machine locally, from
    /// the id.
    /// </summary>
    [Serializable]
    public class VendorItem
    {
        [Tooltip("Stable, unique within this vendor. This is what gets sent over the network and what S3 (drink meter) and P1 (cosmetics) will switch on, so DO NOT renumber existing items once anyone has bought one — add new ids instead.")]
        public int ItemId;

        [Tooltip("What the interaction prompt says. Display only — never switch game logic on this string.")]
        public string DisplayName = "Item";

        [Tooltip("Cost in fake tickets. Tony's number, and deliberately a placeholder until there are four people in the room — SlopLayerPlan says build the mechanism, tune the economy with a real table.")]
        public int Price = 10;

        [Tooltip("How many exist per match. NEGATIVE means unlimited (the bar never runs out of lager). A positive number is a scarcity gag — one golden hat per match, first come first served.")]
        public int StockPerMatch = -1;

        [Tooltip("If set (not -1), buying this ALSO deposits this id into the buyer's Inventory (Gameplay/ItemCatalog) — a vendor selling a mop or an oil can, say. -1 (default) means this purchase has no inventory item, same as every vendor item works today. NOT WIRED YET: nothing currently reads this field — it exists so the data shape is right ahead of the stage that owns a per-player Inventory and can act on it.")]
        public int InventoryItemId = -1;

        public bool IsUnlimited => StockPerMatch < 0;
        public bool GrantsInventoryItem => InventoryItemId != -1;
    }
}
