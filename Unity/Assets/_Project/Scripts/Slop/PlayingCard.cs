using System;

namespace WeeSpurts.Slop
{
    public enum Suit { Clubs = 0, Diamonds = 1, Hearts = 2, Spades = 3 }

    /// <summary>
    /// Rank values are the ORDINALS 1..13, not blackjack scores — Jack is 11
    /// here, and worth 10 at the table. Keeping the card honest about what it
    /// is and letting the game decide what it's worth means the same deck can
    /// later run a different card game without a rewrite.
    /// </summary>
    public enum Rank
    {
        Ace = 1, Two, Three, Four, Five, Six, Seven, Eight, Nine, Ten,
        Jack, Queen, King
    }

    /// <summary>
    /// One card. A readonly struct because a deck creates 52 of them and a
    /// shoe may hold several decks — no reason to make heap garbage for two
    /// bytes of information.
    ///
    /// NETWORK NOTE (Rule 3, Docs/SlopLayerPlan.md): a card is fully described
    /// by two small enums, so it replicates as two bytes. Nothing here points
    /// at a sprite or a prefab; which artwork a King of Spades shows is
    /// resolved locally on each machine from the rank and suit.
    /// </summary>
    [Serializable]
    public readonly struct PlayingCard : IEquatable<PlayingCard>
    {
        public readonly Rank Rank;
        public readonly Suit Suit;

        public PlayingCard(Rank rank, Suit suit)
        {
            Rank = rank;
            Suit = suit;
        }

        /// <summary>
        /// Blackjack value. Face cards are 10; an Ace reports 11 here and the
        /// HAND is what demotes it to 1 when needed — see
        /// <see cref="BlackjackHand"/>. A card cannot know whether its ace is
        /// soft, because that depends on every other card in the hand.
        /// </summary>
        public int BlackjackValue => Rank switch
        {
            Rank.Ace => 11,
            Rank.Jack or Rank.Queen or Rank.King => 10,
            _ => (int)Rank
        };

        public bool IsAce => Rank == Rank.Ace;

        public bool Equals(PlayingCard other) => Rank == other.Rank && Suit == other.Suit;
        public override bool Equals(object obj) => obj is PlayingCard c && Equals(c);
        public override int GetHashCode() => ((int)Rank * 4) + (int)Suit;

        /// <summary>Short form for logs and tests: "AS", "10H", "KD".</summary>
        public override string ToString()
        {
            string r = Rank switch
            {
                Rank.Ace => "A",
                Rank.Jack => "J",
                Rank.Queen => "Q",
                Rank.King => "K",
                _ => ((int)Rank).ToString()
            };

            return r + Suit.ToString()[0];
        }
    }
}
