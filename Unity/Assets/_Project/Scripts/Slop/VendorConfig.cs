using UnityEngine;

namespace WeeSpurts.Slop
{
    /// <summary>
    /// One shop's menu, as an asset Tony can edit in the Inspector — the bar's
    /// drinks list, the cosmetics counter's hats. Follows the same rule as
    /// every other config in this project (CLAUDE.md: "config/tunables are
    /// ScriptableObjects, not hard-coded constants"), which here matters more
    /// than usual: `Docs/SlopLayerPlan.md` says explicitly to build the
    /// mechanism now and tune the economy later with four people in the room.
    /// Every number in this asset is expected to be wrong until then, and
    /// changing them must never need a programmer.
    ///
    /// SEPARATION ON PURPOSE: this asset is DATA. <see cref="Vendor"/> is the
    /// LOGIC and knows nothing about Unity, which is what lets the purchase
    /// rules be unit-tested without Play mode. <see cref="CreateVendor"/> is
    /// the only bridge between them.
    /// </summary>
    [CreateAssetMenu(fileName = "VendorConfig", menuName = "WeeSpurts/Vendor Config")]
    public class VendorConfig : ScriptableObject
    {
        [Tooltip("Shown in the interaction prompt and used as the tag on every coin transaction from this shop, so it turns up in the coin feed and the logs. Keep it short: 'Bar', 'Cosmetics'.")]
        public string VendorName = "Bar";

        [Tooltip("Everything on sale here. ItemIds must be unique within this vendor — duplicates are ignored at load rather than crashing, but they mean one of your items is unreachable.")]
        public VendorItem[] Items = new VendorItem[0];

        /// <summary>
        /// Build the runtime shop from this asset. Called once by whatever
        /// scene component owns the bar; the returned Vendor keeps its own
        /// per-match stock and does not write back to the asset — editing a
        /// ScriptableObject at runtime would persist into your project files
        /// in the Editor, which is how "the bar was sold out on a fresh
        /// launch" bugs happen.
        /// </summary>
        public Vendor CreateVendor() => new Vendor(VendorName, Items);
    }
}
