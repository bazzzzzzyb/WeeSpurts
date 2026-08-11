using System.Collections.Generic;
using UnityEngine;

namespace WeeSpurts.Slop
{
    /// <summary>
    /// The house machine's knobs, as an asset Tony can edit in the Inspector.
    /// Same data/logic split as <see cref="BlackjackConfig"/>: this is DATA,
    /// <see cref="SlotRules"/> is the plain-C# mirror the engine actually runs
    /// on, and <see cref="CreateRules"/> is the only bridge.
    ///
    /// DEFAULT REEL STRIPS ARE THE SAME 24-SYMBOL ARRAY ON ALL THREE REELS:
    /// 3 Pin, 6 Beer, 5 Ball, 5 Shoe, 4 Ticket, 1 Mascot. That single Mascot
    /// slot per reel is what makes the jackpot rare (roughly 1 in 13,800
    /// pulls) without any special-cased probability in the payout math — see
    /// <see cref="SlotRules"/>'s class comment.
    ///
    /// Every number here is a placeholder, same as every other economy asset
    /// in this project — tune with four people in the room.
    /// </summary>
    [CreateAssetMenu(fileName = "SlotConfig", menuName = "WeeSpurts/Slot Config")]
    public class SlotConfig : ScriptableObject
    {
        [Header("Stakes")]
        public int MinStake = 10;
        public int MaxStake = 50;

        [Header("Reel strips — one array per reel, symbol order is the physical strip")]
        [Tooltip("A symbol's REPEAT COUNT here is its odds of landing on this reel. Keep the jackpot symbol (Mascot) rare.")]
        public SlotSymbol[] ReelStrip0 = DefaultStrip();
        public SlotSymbol[] ReelStrip1 = DefaultStrip();
        public SlotSymbol[] ReelStrip2 = DefaultStrip();

        [Header("Paytable")]
        [Tooltip("Stake multiplier for a three-of-a-kind. Pin is deliberately absent here — it pays through the bonus below, not cash.")]
        public List<SlotPayoutEntry> Paytable = DefaultPaytable();

        [Header("Bonus — FREE FRAME")]
        [Tooltip("Three of this symbol triggers the bonus instead of a cash payout.")]
        public SlotSymbol BonusSymbol = SlotSymbol.Pin;
        public int BonusFreePulls = 5;
        public int BonusMultiplier = 3;

        private static SlotSymbol[] DefaultStrip() => new[]
        {
            SlotSymbol.Pin, SlotSymbol.Pin, SlotSymbol.Pin,
            SlotSymbol.Beer, SlotSymbol.Beer, SlotSymbol.Beer, SlotSymbol.Beer, SlotSymbol.Beer, SlotSymbol.Beer,
            SlotSymbol.Ball, SlotSymbol.Ball, SlotSymbol.Ball, SlotSymbol.Ball, SlotSymbol.Ball,
            SlotSymbol.Shoe, SlotSymbol.Shoe, SlotSymbol.Shoe, SlotSymbol.Shoe, SlotSymbol.Shoe,
            SlotSymbol.Ticket, SlotSymbol.Ticket, SlotSymbol.Ticket, SlotSymbol.Ticket,
            SlotSymbol.Mascot
        };

        private static List<SlotPayoutEntry> DefaultPaytable() => new List<SlotPayoutEntry>
        {
            new SlotPayoutEntry { Symbol = SlotSymbol.Beer, Multiplier = 8 },
            new SlotPayoutEntry { Symbol = SlotSymbol.Shoe, Multiplier = 12 },
            new SlotPayoutEntry { Symbol = SlotSymbol.Ball, Multiplier = 12 },
            new SlotPayoutEntry { Symbol = SlotSymbol.Ticket, Multiplier = 20 },
            new SlotPayoutEntry { Symbol = SlotSymbol.Mascot, Multiplier = 250 }
        };

        /// <summary>Snapshot these settings into the plain-C# rules the machine runs on.</summary>
        public SlotRules CreateRules() => new SlotRules
        {
            MinStake = MinStake,
            MaxStake = MaxStake,
            ReelStrips = new[]
            {
                (SlotSymbol[])(ReelStrip0?.Clone() ?? DefaultStrip()),
                (SlotSymbol[])(ReelStrip1?.Clone() ?? DefaultStrip()),
                (SlotSymbol[])(ReelStrip2?.Clone() ?? DefaultStrip())
            },
            Paytable = new List<SlotPayoutEntry>(Paytable ?? DefaultPaytable()),
            BonusSymbol = BonusSymbol,
            BonusFreePulls = BonusFreePulls,
            BonusMultiplier = BonusMultiplier
        };
    }
}
