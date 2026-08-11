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

    public readonly struct BlackjackRound
    {
        public readonly BlackjackOutcome Outcome;
        public readonly ulong PlayerId;
        public readonly int Stake;
        /// <summary>Coins handed back: 0 on a loss, the stake on a push, more on a win.</summary>
        public readonly int Returned;
        public readonly int PlayerTotal;
        public readonly int DealerTotal;

        /// <summary>Net change across the whole round. Negative on a loss.</summary>
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
    /// `Docs/SlopLayerPlan.md` S4 — a coin sink.
    ///
    /// SCOPE, DELIBERATELY SMALL: one player versus the dealer, hit and stand
    /// only. NO double-down, NO splitting, NO insurance, NO side bets, and no
    /// second seat. Each of those is a real rules branch with its own money
    /// path, and a half-built split is worse than no split. They are additive
    /// later; nothing here blocks them.
    ///
    /// PURE C#, NO UNITY. The table is a rules engine. Walking up to it,
    /// sitting down, the camera and the buttons are a separate scene-side
    /// job — that split is what lets every rule below be proven by unit test
    /// instead of by playing forty hands.
    ///
    /// MONEY: the stake is taken by <see cref="CoinLedger.RequestSpend"/> at
    /// the deal and winnings are paid by <see cref="CoinLedger.Award"/> at
    /// settlement. It is deliberately NOT a Transfer, because the house is not
    /// a player — so unlike the bar (a pure sink) this table BOTH removes and
    /// creates coins, and `CoinLedger.TotalInCirculation` legitimately moves in
    /// either direction. That is the one place the economy gets a source, and
    /// it is why MaxBet exists.
    ///
    /// DETERMINISM: the shoe is shuffled by <see cref="DeterministicRng"/> from
    /// an explicit seed. Same seed plus same sequence of hits equals the same
    /// cards on every machine, which is what makes this survivable when it goes
    /// networked — the host seeds the table and every client replays it. Same
    /// discipline as `LaunchParameters.Seed` on the throw path.
    /// </summary>
    public class BlackjackTable
    {
        private readonly BlackjackRules _rules;
        private readonly List<PlayingCard> _shoe = new List<PlayingCard>();
        private DeterministicRng _rng;

        private int _dealtIndex;
        private ulong _playerId;
        private int _stake;

        public BlackjackPhase Phase { get; private set; } = BlackjackPhase.AwaitingBet;
        public BlackjackHand PlayerHand { get; } = new BlackjackHand();
        public BlackjackHand DealerHand { get; } = new BlackjackHand();

        /// <summary>The dealer's face-up card. Meaningless before a deal.</summary>
        public PlayingCard DealerUpCard => DealerHand.Count > 0 ? DealerHand.Cards[0] : default;

        /// <summary>Result of the last completed round, for the UI to display after settlement.</summary>
        public BlackjackRound LastRound { get; private set; }

        public int CardsRemaining => _shoe.Count - _dealtIndex;
        public BlackjackRules Rules => _rules;

        /// <summary>Raised once per settled round — the seam a payout HUD or a coin feed hangs off.</summary>
        public event Action<BlackjackRound> OnRoundSettled;

        public BlackjackTable(BlackjackRules rules, int seed)
        {
            _rules = rules ?? new BlackjackRules();
            _rules.Sanitize();
            _rng = new DeterministicRng(seed);
            BuildAndShuffleShoe();
        }

        /// <summary>
        /// Sit down and bet. Takes the stake immediately — that is what makes
        /// a walk-away mid-hand harmless, since the money is already resolved
        /// one way or the other by the time anything can go wrong.
        /// </summary>
        public DealOutcome Deal(CoinLedger ledger, ulong playerId, int stake)
        {
            if (ledger == null) return DealOutcome.CouldNotPay;
            if (Phase != BlackjackPhase.AwaitingBet && Phase != BlackjackPhase.Settled)
                return DealOutcome.WrongPhase;
            if (stake < _rules.MinBet || stake > _rules.MaxBet)
                return DealOutcome.StakeOutOfRange;

            // Reshuffle BETWEEN hands only, never during one.
            if (CardsRemaining < _rules.ReshuffleBelowCards) BuildAndShuffleShoe();

            CoinTransaction tx = ledger.RequestSpend(playerId, stake, "Blackjack:stake");
            if (!tx.Granted) return DealOutcome.CouldNotPay;

            _playerId = playerId;
            _stake = stake;
            PlayerHand.Clear();
            DealerHand.Clear();

            // Real dealing order: player, dealer, player, dealer.
            PlayerHand.Add(Draw());
            DealerHand.Add(Draw());
            PlayerHand.Add(Draw());
            DealerHand.Add(Draw());

            // Naturals resolve immediately, before anyone acts.
            if (PlayerHand.IsBlackjack || DealerHand.IsBlackjack)
            {
                if (PlayerHand.IsBlackjack && DealerHand.IsBlackjack) Settle(ledger, BlackjackOutcome.Push);
                else if (PlayerHand.IsBlackjack) Settle(ledger, BlackjackOutcome.PlayerBlackjack);
                else Settle(ledger, BlackjackOutcome.DealerWin);

                return DealOutcome.Dealt;
            }

            Phase = BlackjackPhase.PlayerTurn;
            return DealOutcome.Dealt;
        }

        /// <summary>
        /// Take a card. Busting settles the round on the spot.
        ///
        /// Reaching exactly 21 AUTO-STANDS. There is no legal reason to hit a
        /// 21 and offering the button is a trap that only ever produces an
        /// accidental bust and a bad feeling — this is a party game, not a
        /// test of whether you know not to press it.
        /// </summary>
        public bool Hit(CoinLedger ledger)
        {
            if (Phase != BlackjackPhase.PlayerTurn) return false;

            PlayerHand.Add(Draw());

            if (PlayerHand.IsBust)
            {
                Settle(ledger, BlackjackOutcome.PlayerBust);
                return true;
            }

            if (PlayerHand.Total == 21) Stand(ledger);
            return true;
        }

        /// <summary>Stop drawing and let the dealer play out.</summary>
        public bool Stand(CoinLedger ledger)
        {
            if (Phase != BlackjackPhase.PlayerTurn) return false;

            Phase = BlackjackPhase.DealerTurn;
            PlayDealer();

            if (DealerHand.IsBust) Settle(ledger, BlackjackOutcome.DealerBust);
            else if (DealerHand.Total > PlayerHand.Total) Settle(ledger, BlackjackOutcome.DealerWin);
            else if (DealerHand.Total < PlayerHand.Total) Settle(ledger, BlackjackOutcome.PlayerWin);
            else Settle(ledger, BlackjackOutcome.Push);

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

        private void Settle(CoinLedger ledger, BlackjackOutcome outcome)
        {
            int returned = outcome switch
            {
                // Stake back plus the 3:2 bonus. Integer division rounds down.
                BlackjackOutcome.PlayerBlackjack =>
                    _stake + (_stake * _rules.BlackjackPayoutNumerator / _rules.BlackjackPayoutDenominator),

                // Even money: stake back plus the same again.
                BlackjackOutcome.PlayerWin or BlackjackOutcome.DealerBust => _stake * 2,

                // Nobody wins; the stake comes home.
                BlackjackOutcome.Push => _stake,

                // Lost. The stake was already taken at the deal.
                _ => 0
            };

            if (returned > 0 && ledger != null)
                ledger.Award(_playerId, returned, $"Blackjack:{outcome}");

            Phase = BlackjackPhase.Settled;
            LastRound = new BlackjackRound(outcome, _playerId, _stake, returned,
                                           PlayerHand.Total, DealerHand.Total);
            OnRoundSettled?.Invoke(LastRound);
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
