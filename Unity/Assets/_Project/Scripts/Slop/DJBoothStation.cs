using UnityEngine;
using WeeSpurts.Core;
using WeeSpurts.Player;

namespace WeeSpurts.Slop
{
    /// <summary>
    /// The DJ booth, as a place in the venue rather than a mute prop. Fourth
    /// concrete <see cref="VenueStation"/>, anchored on the existing
    /// `DJ_Booth_and_Stage` art object same as every station before it.
    ///
    /// MUSIC IS NOT GATED BY INTERACTION. A DJ booth plays whether or not
    /// anyone's standing at it — the always-on loop lives on a
    /// <see cref="PositionalAmbience"/> on this same GameObject, started the
    /// instant a clip exists, independent of <see cref="CanInteract"/>.
    ///
    /// DELIBERATELY NOT INTERACTABLE YET. `Docs/VenueEconomyIdeas.md`'s own
    /// idea for this booth is "pay tickets to blast a horn or change the
    /// venue track" — a real ticket sink, and a real feature — but building
    /// it here would mean inventing a price and a horn sound that don't
    /// exist: no horn clip was provided, and Tony hasn't set a number.
    /// CanInteract returns false / GetPrompt returns empty so nothing lies to
    /// the player about a button that isn't there yet (same rule
    /// `LaneKioskInteractable`/`BarStation` already follow for an unusable
    /// station: "an unusable interactable is never selected, so a real-
    /// looking prompt here would only ever be introduced by accident"). This
    /// is the one deliberate gap in this station — the horn/track-change
    /// mechanic is real future work, not an oversight.
    /// </summary>
    [DisallowMultipleComponent]
    public class DJBoothStation : VenueStation
    {
        public override bool CanInteract(PlayerAvatar player) => false;

        public override string GetPrompt(PlayerAvatar player) => string.Empty;

        public override void Interact(PlayerAvatar player) { }
    }
}
