namespace WeeSpurts.Slop
{
    /// <summary>
    /// Every knob on the blackjack table, as plain C# so the rules can be
    /// unit-tested without Unity. <see cref="BlackjackConfig"/> is the
    /// ScriptableObject that lets Tony edit these in the Inspector — same
    /// data/logic split as Vendor and VendorConfig.
    ///
    /// EVERY NUMBER HERE IS A PLACEHOLDER. `Docs/SlopLayerPlan.md` is explicit
    /// that the economy gets tuned with four people in the room, not solo, so
    /// these defaults are "recognisably blackjack" rather than "balanced".
    /// </summary>
    public class BlackjackRules
    {
        /// <summary>Smallest legal stake. Also the thing that keeps payout rounding sane — see BlackjackPayout.</summary>
        public int MinBet = 10;

        /// <summary>Largest legal stake. A cap exists so one lucky player can't drain the whole match economy in a hand.</summary>
        public int MaxBet = 100;

        /// <summary>
        /// A natural pays this ratio ON TOP of the stake — 3:2 by default, so a
        /// 10 stake returns 25 (10 back plus 15). Expressed as two ints rather
        /// than a float because money is integer here and 1.5f would invite
        /// rounding drift between machines.
        ///
        /// ROUNDING IS DOWN, IN THE HOUSE'S FAVOUR: at 3:2 a stake of 5 pays 7,
        /// not 7.5. Keep MinBet even and this never comes up.
        /// </summary>
        public int BlackjackPayoutNumerator = 3;
        public int BlackjackPayoutDenominator = 2;

        /// <summary>
        /// Does the dealer hit a SOFT 17 (Ace + 6)? Real casinos differ and it
        /// measurably moves the house edge. False (dealer stands on all 17s) is
        /// the friendlier, more common casual rule and the better default for a
        /// party game where nobody is counting.
        /// </summary>
        public bool DealerHitsSoft17 = false;

        /// <summary>How many 52-card decks in the shoe.</summary>
        public int DeckCount = 6;

        /// <summary>
        /// Reshuffle at the start of a deal once the shoe drops below this many
        /// cards. Never mid-hand — a shoe that reshuffles halfway through a
        /// round is how you get the same card twice in one hand.
        /// </summary>
        public int ReshuffleBelowCards = 20;

        /// <summary>
        /// Clamp anything nonsensical an Inspector edit could produce, so a
        /// typo degrades the table rather than crashing or hanging it. Called
        /// by the table on construction.
        /// </summary>
        public void Sanitize()
        {
            if (MinBet < 1) MinBet = 1;
            if (MaxBet < MinBet) MaxBet = MinBet;
            if (BlackjackPayoutNumerator < 1) BlackjackPayoutNumerator = 1;
            if (BlackjackPayoutDenominator < 1) BlackjackPayoutDenominator = 1;
            if (DeckCount < 1) DeckCount = 1;

            // Must be able to deal a full round (up to ~10 cards is generous)
            // out of a freshly shuffled shoe, or the reshuffle rule could loop.
            int shoeSize = DeckCount * 52;
            if (ReshuffleBelowCards < 0) ReshuffleBelowCards = 0;
            if (ReshuffleBelowCards > shoeSize - 12) ReshuffleBelowCards = shoeSize - 12;
        }
    }
}
