using UnityEngine;
using WeeSpurts.Gameplay;
using WeeSpurts.Player;

namespace WeeSpurts.Slop
{
    /// <summary>
    /// The bar, as something you can walk up to and use — the first concrete
    /// <see cref="VenueStation"/>. Drives a <see cref="Vendor"/> built from
    /// <see cref="VendorConfig"/>; sells exactly ONE item per interaction, the
    /// first one on the vendor's list unless <see cref="primaryItemId"/> says
    /// otherwise.
    ///
    /// WHY ONE ITEM, NOT A MENU: Stage 2's job is proving the economy is
    /// reachable at all — walk up, buy a drink, watch your balance move.
    /// A real drinks menu is a UI system nobody has scoped yet. The underlying
    /// Vendor already supports a full item list for when that menu exists;
    /// this station just doesn't expose it through [E] yet.
    /// </summary>
    [DisallowMultipleComponent]
    public class BarStation : VenueStation
    {
        [Tooltip("This shop's name, items and prices. WeeSpurts/Vendor Config asset.")]
        [SerializeField] private VendorConfig config;

        [Tooltip("Which item [E] buys. -1 (default) means 'the first item in the config's list' — set this only if a particular bar should sell something other than its first row.")]
        [SerializeField] private int primaryItemId = -1;

        private Vendor _vendor;

        private Vendor GetVendor()
        {
            if (_vendor == null && config != null) _vendor = config.CreateVendor();
            return _vendor;
        }

        private int ResolveItemId(Vendor vendor)
        {
            if (primaryItemId >= 0 && vendor.Contains(primaryItemId)) return primaryItemId;
            return vendor.Items.Count > 0 ? vendor.Items[0].ItemId : -1;
        }

        public override bool CanInteract(PlayerAvatar player)
        {
            Vendor vendor = GetVendor();
            return vendor != null && ResolveItemId(vendor) >= 0;
        }

        public override string GetPrompt(PlayerAvatar player)
        {
            if (!CanInteract(player)) return string.Empty;

            Vendor vendor = GetVendor();
            int itemId = ResolveItemId(vendor);
            vendor.TryGetItem(itemId, out VendorItem item);

            return vendor.StockRemaining(itemId) == 0
                ? $"{item.DisplayName} (Sold Out)"
                : $"Buy {item.DisplayName} ({item.Price} Tickets)";
        }

        /// <summary>
        /// Buy it. A refusal (broke, sold out mid-frame) is handled entirely
        /// inside Vendor.Purchase — it fires OnRefused and leaves the world
        /// untouched, so there is nothing for this station to catch or undo.
        /// </summary>
        public override void Interact(PlayerAvatar player)
        {
            if (!CanInteract(player) || player == null) return;
            TicketLedger ledger = Ledger;
            if (ledger == null) return;

            EnsureRegistered(player);
            Vendor vendor = GetVendor();
            vendor.Purchase(ledger, player.EconomyPlayerId, ResolveItemId(vendor));
        }
    }
}
