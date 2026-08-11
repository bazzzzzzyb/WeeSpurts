using System.Collections.Generic;

namespace WeeSpurts.Slop
{
    /// <summary>
    /// A hand of cards and, more importantly, the ace arithmetic — the one bit
    /// of blackjack people reliably get wrong.
    ///
    /// THE ACE RULE, stated once so nobody has to re-derive it: an Ace is worth
    /// 11 unless that busts you, in which case it drops to 1. With two Aces
    /// only ONE can be 11 (11+11 = 22), so the rule has to be applied
    /// repeatedly, not once. The implementation below counts every Ace as 11
    /// and then demotes them one at a time while the total is over 21, which
    /// handles any number of aces without a special case.
    ///
    /// A hand is SOFT while an Ace is still counted as 11 — meaning you cannot
    /// bust by taking one more card. That distinction is why "soft 17" is a
    /// different thing from "hard 17" and why the dealer rule needs to know.
    ///
    /// Pure C#, no Unity: this is the part most worth unit-testing, and it
    /// tests instantly without Play mode.
    /// </summary>
    public class BlackjackHand
    {
        private readonly List<PlayingCard> _cards = new List<PlayingCard>();

        public IReadOnlyList<PlayingCard> Cards => _cards;
        public int Count => _cards.Count;

        public void Add(PlayingCard card) => _cards.Add(card);
        public void Clear() => _cards.Clear();

        /// <summary>
        /// The best total that isn't a bust, or the lowest possible total if
        /// every option busts. Never returns something misleading like 22 when
        /// a 12 was available.
        /// </summary>
        public int Total
        {
            get
            {
                int total = 0;
                int aces = 0;

                for (int i = 0; i < _cards.Count; i++)
                {
                    total += _cards[i].BlackjackValue;
                    if (_cards[i].IsAce) aces++;
                }

                // Demote aces from 11 to 1, one at a time, only as far as needed.
                while (total > 21 && aces > 0)
                {
                    total -= 10;
                    aces--;
                }

                return total;
            }
        }

        /// <summary>
        /// True while an Ace is still being counted as 11 — i.e. one more card
        /// cannot bust this hand. Recomputed rather than cached because a hand
        /// changes as cards arrive and a stale flag here is a silent, ugly bug.
        /// </summary>
        public bool IsSoft
        {
            get
            {
                int total = 0;
                int aces = 0;

                for (int i = 0; i < _cards.Count; i++)
                {
                    total += _cards[i].BlackjackValue;
                    if (_cards[i].IsAce) aces++;
                }

                // Every demotion consumes one ace-as-11. If any survive, soft.
                while (total > 21 && aces > 0)
                {
                    total -= 10;
                    aces--;
                }

                return aces > 0;
            }
        }

        public bool IsBust => Total > 21;

        /// <summary>
        /// A NATURAL: 21 on the first two cards. Deliberately NOT "any 21",
        /// because a three-card 21 does not win the 3:2 bonus and does not beat
        /// a dealer's natural — treating them the same is the classic blackjack
        /// implementation bug.
        /// </summary>
        public bool IsBlackjack => _cards.Count == 2 && Total == 21;

        public override string ToString() => string.Join(" ", _cards) + $" ({Total}{(IsSoft ? " soft" : "")})";
    }
}
