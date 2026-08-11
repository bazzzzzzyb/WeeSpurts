using UnityEngine;

namespace WeeSpurts.Slop
{
    /// <summary>
    /// The card table's knobs, as an asset Tony can edit in the Inspector while
    /// the game is running. Same data/logic split as VendorConfig: this is
    /// DATA, <see cref="BlackjackRules"/> is the plain-C# mirror the rules
    /// engine actually runs on, and <see cref="CreateRules"/> is the only
    /// bridge — which is what keeps <see cref="BlackjackTable"/> unit-testable
    /// without Unity.
    ///
    /// Every default is a placeholder. `Docs/SlopLayerPlan.md`: build the
    /// mechanism now, tune the economy with four people in the room.
    /// </summary>
    [CreateAssetMenu(fileName = "BlackjackConfig", menuName = "WeeSpurts/Blackjack Config")]
    public class BlackjackConfig : ScriptableObject
    {
        [Header("Stakes")]
        [Tooltip("Smallest legal bet. Keep it EVEN — a 3:2 blackjack on an odd stake rounds down in the house's favour, which is correct but looks like a bug to anyone watching the number.")]
        public int MinBet = 10;

        [Tooltip("Largest legal bet. This is the main brake on the table as a coin SOURCE: unlike the bar, blackjack can hand coins back into the match economy, and one player winning huge repeatedly is how the whole betting layer stops meaning anything.")]
        public int MaxBet = 100;

        [Header("Payouts")]
        [Tooltip("A natural (21 on the first two cards) pays this ratio ON TOP of the stake. 3:2 is the traditional number; 6:5 is the modern casino tightening and is noticeably meaner.")]
        public int BlackjackPayoutNumerator = 3;
        public int BlackjackPayoutDenominator = 2;

        [Header("Dealer policy")]
        [Tooltip("Should the dealer hit a SOFT 17 (an Ace counted as 11, e.g. A+6)? OFF means the dealer stands on every 17, which is friendlier to the player. Real casinos do both and it measurably moves the house edge.")]
        public bool DealerHitsSoft17 = false;

        [Header("Shoe")]
        [Tooltip("How many 52-card decks are in play. More decks means a longer stretch between reshuffles and less swing.")]
        [Range(1, 8)] public int DeckCount = 6;

        [Tooltip("Reshuffle at the START of a hand once fewer than this many cards remain. Never mid-hand — that is how the same card appears twice in one round.")]
        public int ReshuffleBelowCards = 20;

        /// <summary>Snapshot these settings into the plain-C# rules the table runs on.</summary>
        public BlackjackRules CreateRules() => new BlackjackRules
        {
            MinBet = MinBet,
            MaxBet = MaxBet,
            BlackjackPayoutNumerator = BlackjackPayoutNumerator,
            BlackjackPayoutDenominator = BlackjackPayoutDenominator,
            DealerHitsSoft17 = DealerHitsSoft17,
            DeckCount = DeckCount,
            ReshuffleBelowCards = ReshuffleBelowCards
        };
    }
}
