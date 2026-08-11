using System;
using System.Collections.Generic;

namespace WeeSpurts.Slop
{
    /// <summary>One payout line: three-of-a-kind on <see cref="Symbol"/> returns the stake times <see cref="Multiplier"/>.</summary>
    [Serializable]
    public class SlotPayoutEntry
    {
        public SlotSymbol Symbol;
        public int Multiplier = 1;
    }

    /// <summary>
    /// Every knob on the slot machine, as plain C# so the reels can be
    /// unit-tested without Unity. <see cref="SlotConfig"/> is the
    /// ScriptableObject that lets Tony edit these in the Inspector — same
    /// data/logic split as <see cref="BlackjackConfig"/>/<see cref="BlackjackRules"/>.
    ///
    /// VOLATILITY LIVES IN THE REEL STRIPS, NOT THE PAYTABLE. Each reel spins
    /// independently by drawing a random index into its own strip — a symbol
    /// that appears once in a 24-slot strip is roughly 1/24 likely per reel,
    /// so making the jackpot symbol (<see cref="SlotSymbol.Mascot"/>) rare on
    /// the strip is what makes three of them rare, not a special-cased low
    /// probability bolted onto the payout math. That is also what makes "mostly
    /// nothing" true: only a matching triple pays anything at all, and most
    /// spins across three independent reels don't match.
    /// </summary>
    public class SlotRules
    {
        /// <summary>Smallest legal stake per pull.</summary>
        public int MinStake = 10;

        /// <summary>Largest legal stake per pull.</summary>
        public int MaxStake = 50;

        /// <summary>
        /// One strip per reel — three arrays, always. A symbol's REPEAT COUNT
        /// on its strip is its odds of landing that reel; this is the entire
        /// mechanism behind the fat tail. Reels are independent, so the same
        /// strip can be reused for all three (the default does) or varied per
        /// reel for finer control.
        /// </summary>
        public SlotSymbol[][] ReelStrips = new SlotSymbol[3][];

        /// <summary>Stake multiplier for a three-of-a-kind, per symbol. A symbol with no entry pays nothing.</summary>
        public List<SlotPayoutEntry> Paytable = new List<SlotPayoutEntry>();

        /// <summary>Three of THIS symbol does not pay cash — it triggers FREE FRAME instead.</summary>
        public SlotSymbol BonusSymbol = SlotSymbol.Pin;

        /// <summary>How many free pulls FREE FRAME grants. Stacks if the bonus retriggers during a free pull.</summary>
        public int BonusFreePulls = 5;

        /// <summary>Payout multiplier applied ON TOP of the normal paytable while a free pull is in flight.</summary>
        public int BonusMultiplier = 3;

        public bool TryGetPayout(SlotSymbol symbol, out int multiplier)
        {
            for (int i = 0; i < Paytable.Count; i++)
            {
                if (Paytable[i] == null || Paytable[i].Symbol != symbol) continue;
                multiplier = Paytable[i].Multiplier;
                return true;
            }

            multiplier = 0;
            return false;
        }

        /// <summary>
        /// Clamp anything nonsensical an Inspector edit could produce, so a
        /// typo degrades the machine rather than crashing or dividing by zero.
        /// Called by <see cref="SlotMachine"/> on construction, same contract
        /// as <see cref="BlackjackRules.Sanitize"/>.
        /// </summary>
        public void Sanitize()
        {
            if (MinStake < 1) MinStake = 1;
            if (MaxStake < MinStake) MaxStake = MinStake;
            if (BonusFreePulls < 0) BonusFreePulls = 0;
            if (BonusMultiplier < 1) BonusMultiplier = 1;

            if (ReelStrips == null || ReelStrips.Length != 3)
                ReelStrips = new SlotSymbol[3][];

            for (int i = 0; i < 3; i++)
                if (ReelStrips[i] == null || ReelStrips[i].Length == 0)
                    ReelStrips[i] = new[] { SlotSymbol.Beer };

            if (Paytable == null) Paytable = new List<SlotPayoutEntry>();
        }
    }
}
