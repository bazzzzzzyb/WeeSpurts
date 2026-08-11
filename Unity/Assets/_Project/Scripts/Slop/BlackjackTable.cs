using System;
using System.Collections.Generic;
using WeeSpurts.Core;
using WeeSpurts.Gameplay;

namespace WeeSpurts.Slop
{
    public enum BlackjackPhase { AwaitingBet, PlayerTurn, DealerTurn, Settled }

    /// <summary>How a round ended, from the PLAYER's point of view.</summary>
    public enum BlackjackOutcome
    {
        None,
        PlayerBlackjack,
        PlayerWin,
        DealerBust,
        Push,
        DealerWin,
        PlayerBust
    }

    /// <summary>Why a deal didn't start.</summary>
    public enum DealOutcome { Dealt, WrongPhase, StakeOutOfRange, CouldNotPay }

    /// <summary>The settled result of ONE hand. A split round produces two of these, one per hand — see <see cref="BlackjackTable.LastRoundResults"/>.</summary>
    public readonly struct BlackjackRound
    {
        public readonly BlackjackOutcome Outcome;
        public readonly ulong PlayerId;
        public readonly int Stake;
        /// <summary>Tickets handed back: 0 on a loss, the stake on a push, more on a win.</summary>
        public readonly int Returned;
        public readonly int PlayerTotal;
        public readonly int DealerTotal;

        /// <summary>Net change from this hand alone. Negative on a loss.</summary>
        public int Net => Returned - Stake;

        public BlackjackRound(BlackjackOutcome outcome, ulong playerId, int stake, int returned,
                              int playerTotal, int dealerTotal)
        {
            Outcome = outcome;
            PlayerId = playerId;
            Stake = stake;
            Returned = returned;
            PlayerTotal = playerTotal;
            DealerTotal = dealerTotal;
        }
    }

    /// <summary>
    /// One seat of blackjack against the house, in the casino nook.
    /// `Docs/SlopLayerPlan.md` S4 — a ticket sink.
    ///
    /// SCOPE: hit, stand, double-down and split (Tony's 2026-08-11 executive-
    /// day override of the earlier "hit/stand only" scope — see
    /// `Docs/GameBible.md`'s change log). Still NO insurance, NO side bets,
    /// and no second seat — each of those is its own real money path.
    ///
    /// MULTIPLE HANDS: a split produces a SECOND <see cref="BlackjackHand"/>,
    /// played in sequence. <see cref="PlayerHands"/> is the indexed
    /// collection (always at least 1) and <see cref="ActiveHandIndex"/> is
    /// which one is currently accepting Hit/Stand/Double. ONE SPLIT PER
    /// ROUND, NO RE-SPLITTING — that keeps the state machine finite no matter
    /// what the shoe deals, which matters more here than in a real casino
    /// because nothing bounds how unlucky/lucky the shoe can get.
    ///
    /// PURE C#, NO UNITY. The table is a rules engine. Walking up to it,
    /// sitting down, the camera and the buttons are a separate scene-side
    /// job — that split is what lets every rule below be proven by unit test
    /// instead of by playing forty hands.
    ///
    /// MONEY: each hand's stake is taken by <see cref="TicketLedger.RequestSpend"/>
    /// (at the deal, at a Double, and at a Split) and winnings are paid by
    /// <see cref="TicketLedger.Award"/> at settlement. Deliberately NOT a
    /// Transfer, because the house is not a player — so unlike the bar (a pure
    /// sink) this table BOTH removes and creates tickets, and
    /// `TicketLedger.TotalInCirculation` legitimately moves in either
    /// direction. That is the one place the economy gets a source, and it is
    /// why MaxBet exists.
    ///
    /// THE 3:2 NATURAL BONUS ONLY EVER APPLIES TO THE ORIGINAL, UN-SPLIT
    /// TWO-CARD HAND — it is checked once, at the deal, before a split is even
    /// possible. Every settlement that happens AFTER the player has acted
    /// (hit, stood, doubled, or split) is a plain win/push/loss at even money,
    /// which is also the correct real-world rule for a hand that reaches 21
    /// after a split (it is NOT a "blackjack").
    ///
    /// DETERMINISM: the shoe is shuffled by <see cref="DeterministicRng"/> from
    /// an explicit seed. Same seed plus same sequence of actions equals the
    /// same cards on every machine, which is what makes this survivable when
    /// it goes networked — the host seeds the table and every client replays
    /// it. Same discipline as `LaunchParameters.Seed` on the throw path.
    /// </summary>
    public class BlackjackTable
    {
        private readonly BlackjackRules _rules;
        private readonly List<PlayingCard> _shoe = new List<PlayingCard>();
        private DeterministicRng _rng;

        private readonly List<BlackjackHand> _playerHands = new List<BlackjackHand>();
        private readonly List<int> _handStakes = new List<int>();
        private readonly List<bool> _handDone = new List<bool>();
        private List<BlackjackRound> _roundResults = new List<BlackjackRound>();
        private bool _hasSplit;

        private int _dealtIndex;
        private ulong _playerId;

        public BlackjackPhase Phase { get; private set; } = BlackjackPhase.AwaitingBet;

        /// <summary>Every hand the player currently has in play. Always at least 1 — a split adds a second.</summary>
        public IReadOnlyList<BlackjackHand> PlayerHands => _playerHands;

        /// <summary>Which of <see cref="PlayerHands"/> is currently accepting Hit/Stand/Double.</summary>
        public int ActiveHandIndex { get; private set; }

        /// <summary>The active hand. Convenience alias for <c>PlayerHands[ActiveHandIndex]</c> — never null.</summary>
        public BlackjackHand PlayerHand => _playerHands[ActiveHandIndex];

        /// <summary>True once this round has split. Blocks a second split — see the class comment.</summary>
        public bool HasSplit => _hasSplit;

        public BlackjackHand DealerHand { get; } = new BlackjackHand();

        /// <summary>The dealer's face-up card. Meaningless before a deal.</summary>
        public PlayingCard DealerUpCard => DealerHand.Count > 0 ? DealerHand.Cards[0] : default;

        /// <summary>
        /// Every hand's result from the last completed round, in hand order —
        /// one entry normally, two after a split. For the UI to display after
        /// settlement.
        /// </summary>
        public IReadOnlyList<BlackjackRound> LastRoundResults => _roundResults;

        /// <summary>The primary (first) hand's result from the last completed round. Convenience alias for <c>LastRoundResults[0]</c>.</summary>
        public BlackjackRound LastRound => _roundResults.Count > 0 ? _roundResults[0] : default;

        public int CardsRemaining => _shoe.Count - _dealtIndex;
        public BlackjackRules Rules => _rules;

        /// <summary>Raised once per settled HAND (twice for a split round) — the seam a payout HUD or a ticket feed hangs off.</summary>
        public event Action<BlackjackRound> OnRoundSettled;

        public BlackjackTable(BlackjackRules rules, int seed)
        {
            _rules = rules ?? new BlackjackRules();
            _rules.Sanitize();
            _rng = new DeterministicRng(seed);
            _playerHands.Add(new BlackjackHand());
            _handStakes.Add(0);
            _handDone.Add(false);
            BuildAndShuffleShoe();
        }

        /// <summary>
        /// Sit down and bet. Takes the stake immediately — that is what makes
        /// a walk-away mid-hand harmless, since the money is already resolved
        /// one way or the other by the time anything can go wrong. Resets to
        /// exactly one hand, discarding any split from a previous round.
        /// </summary>
        public DealOutcome Deal(TicketLedger ledger, ulong playerId, int stake)
        {
            if (ledger == null) return DealOutcome.CouldNotPay;
            if (Phase != BlackjackPhase.AwaitingBet && Phase != BlackjackPhase.Settled)
                return DealOutcome.WrongPhase;
            if (stake < _rules.MinBet || stake > _rules.MaxBet)
                return DealOutcome.StakeOutOfRange;

            // Reshuffle BETWEEN hands only, never during one.
            if (CardsRemaining < _rules.ReshuffleBelowCards) BuildAndShuffleShoe();

            TicketTransaction tx = ledger.RequestSpend(playerId, stake, "Blackjack:stake");
            if (!tx.Granted) return DealOutcome.CouldNotPay;

            _playerId = playerId;

            _playerHands.Clear();
            _handStakes.Clear();
            _handDone.Clear();
            _playerHands.Add(new BlackjackHand());
            _handStakes.Add(stake);
            _handDone.Add(false);
            ActiveHandIndex = 0;
            _hasSplit = false;

            DealerHand.Clear();

            // Real dealing order: player, dealer, player, dealer.
            _playerHands[0].Add(Draw());
            DealerHand.Add(Draw());
            _playerHands[0].Add(Draw());
            DealerHand.Add(Draw());

            // Naturals resolve immediately, before anyone acts — and before a
            // split is even possible, which is why this is the ONLY place
            // BlackjackOutcome.PlayerBlackjack (the 3:2 bonus) is ever assigned.
            if (_playerHands[0].IsBlackjack || DealerHand.IsBlackjack)
            {
                if (_playerHands[0].IsBlackjack && DealerHand.IsBlackjack) SettleNatural(ledger, BlackjackOutcome.Push);
                else if (_playerHands[0].IsBlackjack) SettleNatural(ledger, BlackjackOutcome.PlayerBlackjack);
                else SettleNatural(ledger, BlackjackOutcome.DealerWin);

                return DealOutcome.Dealt;
            }

            Phase = BlackjackPhase.PlayerTurn;
            return DealOutcome.Dealt;
        }

        /// <summary>
        /// Take a card on the active hand. Busting or reaching 21 finishes
        /// THIS hand and moves on — to the next split hand if one is waiting,
        /// or to the dealer if this was the last one.
        ///
        /// Reaching exactly 21 AUTO-STANDS. There is no legal reason to hit a
        /// 21 and offering the button is a trap that only ever produces an
        /// accidental bust and a bad feeling — this is a party game, not a
        /// test of whether you know not to press it.
        /// </summary>
        public bool Hit(TicketLedger ledger)
        {
            if (Phase != BlackjackPhase.PlayerTurn) return false;

            BlackjackHand hand = _playerHands[ActiveHandIndex];
            hand.Add(Draw());

            if (hand.IsBust || hand.Total == 21)
            {
                _handDone[ActiveHandIndex] = true;
                AdvanceOrSettle(ledger);
            }

            return true;
        }

        /// <summary>Stop drawing on the active hand and move on — to the next split hand, or to the dealer.</summary>
        public bool Stand(TicketLedger ledger)
        {
            if (Phase != BlackjackPhase.PlayerTurn) return false;

            _handDone[ActiveHandIndex] = true;
            AdvanceOrSettle(ledger);
            return true;
        }

        /// <summary>
        /// Double down: legal only on the active hand's first two cards. Takes
        /// a second stake equal to the hand's own stake, deals exactly one
        /// card, and auto-stands — same "no legal reason to act further"
        /// reasoning as hitting a 21.
        /// </summary>
        public bool Double(TicketLedger ledger)
        {
            if (ledger == null) return false;
            if (Phase != BlackjackPhase.PlayerTurn) return false;

            BlackjackHand hand = _playerHands[ActiveHandIndex];
            if (hand.Count != 2) return false;

            int stake = _handStakes[ActiveHandIndex];
            TicketTransaction tx = ledger.RequestSpend(_playerId, stake, "Blackjack:double");
            if (!tx.Granted) return false;

            _handStakes[ActiveHandIndex] = stake * 2;
            hand.Add(Draw());
            _handDone[ActiveHandIndex] = true;
            AdvanceOrSettle(ledger);
            return true;
        }

        /// <summary>
        /// Split: legal only when the active hand is the original, un-split,
        /// first-two-cards hand and both cards share a RANK (a "matched
        /// pair" — not just equal blackjack value, so King+Queen does not
        /// qualify). Takes a second stake equal to the original. Each new
        /// hand is dealt one more card immediately.
        ///
        /// ACES ARE SPECIAL: split Aces get their one card each and are
        /// immediately done — no further hitting, standing or doubling, and
        /// (see the class comment) reaching 21 here is NOT a blackjack. This
        /// is the standard casual rule and it is what keeps the state machine
        /// finite: nothing here can recurse into a third hand.
        /// </summary>
        public bool Split(TicketLedger ledger)
        {
            if (ledger == null) return false;
            if (Phase != BlackjackPhase.PlayerTurn) return false;
            if (_hasSplit) return false;
            if (ActiveHandIndex != 0) return false;

            BlackjackHand hand = _playerHands[0];
            if (hand.Count != 2) return false;
            if (hand.Cards[0].Rank != hand.Cards[1].Rank) return false;

            int stake = _handStakes[0];
            TicketTransaction tx = ledger.RequestSpend(_playerId, stake, "Blackjack:split");
            if (!tx.Granted) return false;

            PlayingCard first = hand.Cards[0];
            PlayingCard second = hand.Cards[1];
            bool isAceSplit = first.IsAce;

            hand.Clear();
            hand.Add(first);
            hand.Add(Draw());

            var secondHand = new BlackjackHand();
            secondHand.Add(second);
            secondHand.Add(Draw());

            _playerHands.Add(secondHand);
            _handStakes.Add(stake);
            _handDone.Add(isAceSplit || secondHand.Total == 21);
            _handDone[0] = isAceSplit || hand.Total == 21;
            _hasSplit = true;

            // If hand 0 is already finished (a forced ace, or a lucky 21),
            // advance immediately — to hand 1 if it still needs play, or
            // straight to the dealer if it's also already done.
            if (_handDone[0]) AdvanceOrSettle(ledger);

            return true;
        }

        /// <summary>
        /// The dealer has no choices — that is the whole point of the role.
        /// Draw to 17, then stop; the only variation is whether a SOFT 17
        /// counts as made, which is <see cref="BlackjackRules.DealerHitsSoft17"/>.
        /// </summary>
        private void PlayDealer()
        {
            while (true)
            {
                int total = DealerHand.Total;
                if (total > 21) return;
                if (total > 17) return;
                if (total == 17 && !(DealerHand.IsSoft && _rules.DealerHitsSoft17)) return;
                if (total < 17 || (total == 17 && DealerHand.IsSoft && _rules.DealerHitsSoft17))
                    DealerHand.Add(Draw());
            }
        }

        /// <summary>
        /// Called whenever the ACTIVE hand has just finished. Moves to the
        /// next unfinished hand if one exists; otherwise plays the dealer out
        /// (skipping it entirely if every player hand already busted — a
        /// busted hand loses regardless of the dealer's total, so there is
        /// nothing for the dealer to prove) and settles every hand.
        /// </summary>
        private void AdvanceOrSettle(TicketLedger ledger)
        {
            for (int i = ActiveHandIndex + 1; i < _playerHands.Count; i++)
            {
                if (_handDone[i]) continue;
                ActiveHandIndex = i;
                return;
            }

            Phase = BlackjackPhase.DealerTurn;

            bool anyHandStillIn = false;
            for (int i = 0; i < _playerHands.Count; i++)
                if (!_playerHands[i].IsBust) anyHandStillIn = true;

            if (anyHandStillIn) PlayDealer();

            SettleAllHands(ledger);
        }

        /// <summary>The ONE path that can pay the 3:2 natural bonus — see the class comment. Always exactly one hand.</summary>
        private void SettleNatural(TicketLedger ledger, BlackjackOutcome outcome)
        {
            int stake = _handStakes[0];
            int returned = outcome switch
            {
                // Stake back plus the 3:2 bonus. Integer division rounds down.
                BlackjackOutcome.PlayerBlackjack =>
                    stake + (stake * _rules.BlackjackPayoutNumerator / _rules.BlackjackPayoutDenominator),
                BlackjackOutcome.Push => stake,
                _ => 0
            };

            if (returned > 0 && ledger != null)
                ledger.Award(_playerId, returned, $"Blackjack:{outcome}");

            _handDone[0] = true;
            var round = new BlackjackRound(outcome, _playerId, stake, returned, _playerHands[0].Total, DealerHand.Total);
            _roundResults = new List<BlackjackRound> { round };
            OnRoundSettled?.Invoke(round);
            ActiveHandIndex = 0;
            Phase = BlackjackPhase.Settled;
        }

        /// <summary>
        /// Settles every player hand against the dealer's FINAL hand, once,
        /// after every hand has finished acting. Even money only — never the
        /// 3:2 bonus, per the class comment.
        /// </summary>
        private void SettleAllHands(TicketLedger ledger)
        {
            var results = new List<BlackjackRound>(_playerHands.Count);

            for (int i = 0; i < _playerHands.Count; i++)
            {
                BlackjackHand hand = _playerHands[i];
                int stake = _handStakes[i];

                BlackjackOutcome outcome;
                if (hand.IsBust) outcome = BlackjackOutcome.PlayerBust;
                else if (DealerHand.IsBust) outcome = BlackjackOutcome.DealerBust;
                else if (DealerHand.Total > hand.Total) outcome = BlackjackOutcome.DealerWin;
                else if (DealerHand.Total < hand.Total) outcome = BlackjackOutcome.PlayerWin;
                else outcome = BlackjackOutcome.Push;

                int returned = outcome switch
                {
                    BlackjackOutcome.PlayerWin or BlackjackOutcome.DealerBust => stake * 2,
                    BlackjackOutcome.Push => stake,
                    _ => 0
                };

                if (returned > 0 && ledger != null)
                    ledger.Award(_playerId, returned, $"Blackjack:{outcome}");

                var round = new BlackjackRound(outcome, _playerId, stake, returned, hand.Total, DealerHand.Total);
                results.Add(round);
                OnRoundSettled?.Invoke(round);
            }

            _roundResults = results;
            // Reset the "active hand" pointer so PlayerHand/ActiveHandIndex
            // line back up with LastRound (always hand 0) once the round is
            // over — otherwise a UI reading both after a split settles would
            // show hand 1's cards next to hand 0's result.
            ActiveHandIndex = 0;
            Phase = BlackjackPhase.Settled;
        }

        private PlayingCard Draw()
        {
            // Defensive: a correctly configured shoe cannot run dry mid-hand
            // (Sanitize guarantees at least 12 cards past the reshuffle line),
            // but if it somehow does, reshuffling beats an IndexOutOfRange.
            if (_dealtIndex >= _shoe.Count) BuildAndShuffleShoe();
            return _shoe[_dealtIndex++];
        }

        private void BuildAndShuffleShoe()
        {
            _shoe.Clear();
            _dealtIndex = 0;

            for (int d = 0; d < _rules.DeckCount; d++)
                for (int suit = 0; suit < 4; suit++)
                    for (int rank = 1; rank <= 13; rank++)
                        _shoe.Add(new PlayingCard((Rank)rank, (Suit)suit));

            _rng.Shuffle(_shoe);
        }
    }
}
