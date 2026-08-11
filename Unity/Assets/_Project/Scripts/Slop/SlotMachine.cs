using System;
using WeeSpurts.Core;
using WeeSpurts.Gameplay;

namespace WeeSpurts.Slop
{
    /// <summary>Why a pull did or didn't happen.</summary>
    public enum SlotPullOutcome { Spun, StakeOutOfRange, CouldNotPay }

    public readonly struct SlotPull
    {
        public readonly SlotPullOutcome Outcome;
        public readonly ulong PlayerId;
        public readonly int Stake;
        public readonly SlotSymbol Reel0, Reel1, Reel2;
        public readonly int Returned;
        /// <summary>True if this pull was free — drawn from <see cref="SlotMachine.BonusPullsRemaining"/> rather than charged.</summary>
        public readonly bool WasFreePull;
        /// <summary>True if THIS pull is the one that started (or re-triggered) FREE FRAME.</summary>
        public readonly bool TriggeredBonus;
        public readonly int BonusPullsRemainingAfter;
        public readonly int BonusMultiplierAfter;

        public bool Spun => Outcome == SlotPullOutcome.Spun;
        public bool IsThreeOfAKind => Spun && Reel0 == Reel1 && Reel1 == Reel2;

        /// <summary>Net change to the player's balance from this pull alone. A free pull never costs anything. Zero on a refusal — nothing moved.</summary>
        public int Net => Spun ? Returned - (WasFreePull ? 0 : Stake) : 0;

        public SlotPull(SlotPullOutcome outcome, ulong playerId, int stake, SlotSymbol reel0, SlotSymbol reel1,
                        SlotSymbol reel2, int returned, bool wasFreePull, bool triggeredBonus,
                        int bonusPullsRemainingAfter, int bonusMultiplierAfter)
        {
            Outcome = outcome;
            PlayerId = playerId;
            Stake = stake;
            Reel0 = reel0;
            Reel1 = reel1;
            Reel2 = reel2;
            Returned = returned;
            WasFreePull = wasFreePull;
            TriggeredBonus = triggeredBonus;
            BonusPullsRemainingAfter = bonusPullsRemainingAfter;
            BonusMultiplierAfter = bonusMultiplierAfter;
        }

        public override string ToString() => $"[{Reel0} {Reel1} {Reel2}] {Outcome} returned {Returned}";
    }

    /// <summary>
    /// The Thunder Lanes house slot machine. Pure C#, no Unity — same shape as
    /// <see cref="BlackjackTable"/> on purpose, so the station layer that
    /// drives both has one pattern to learn.
    ///
    /// MONEY: the stake is taken by <see cref="TicketLedger.RequestSpend"/> at
    /// the start of a paid pull and any win is paid by
    /// <see cref="TicketLedger.Award"/> at the end of the SAME call — there is
    /// no in-between state a disconnect could leave dangling, same reasoning
    /// as <see cref="BlackjackTable.Deal"/>.
    ///
    /// DETERMINISM: reels are drawn from <see cref="DeterministicRng"/> seeded
    /// once at construction, same discipline as the card shoe.
    ///
    /// FREE FRAME: three <see cref="SlotRules.BonusSymbol"/> (Pin) does not
    /// pay cash — it sets <see cref="BonusPullsRemaining"/> and
    /// <see cref="BonusMultiplier"/>. The NEXT calls to <see cref="Pull"/>
    /// consume that state automatically (no charge, multiplier applied to any
    /// win) until it runs out, so the station calling Pull never needs to know
    /// whether the machine is mid-bonus — "the station layer stays dumb".
    /// </summary>
    public class SlotMachine
    {
        private readonly SlotRules _rules;
        private DeterministicRng _rng;

        public int BonusPullsRemaining { get; private set; }
        public int BonusMultiplier { get; private set; } = 1;
        public SlotPull LastPull { get; private set; }
        public SlotRules Rules => _rules;

        /// <summary>Raised after every resolved pull — paid, free, win or loss. Never raised for a refusal.</summary>
        public event Action<SlotPull> OnPulled;

        public SlotMachine(SlotRules rules, int seed)
        {
            _rules = rules ?? new SlotRules();
            _rules.Sanitize();
            _rng = new DeterministicRng(seed);
        }

        /// <summary>
        /// Spin. If <see cref="BonusPullsRemaining"/> is above zero this pull
        /// is FREE — <paramref name="stake"/> still sets the bet size (a free
        /// spin plays at the bet that earned it) but nothing is taken from the
        /// ledger, and any win is multiplied by <see cref="BonusMultiplier"/>.
        /// </summary>
        public SlotPull Pull(TicketLedger ledger, ulong playerId, int stake)
        {
            if (ledger == null) return Refuse(SlotPullOutcome.CouldNotPay, playerId, stake);

            bool isFree = BonusPullsRemaining > 0;

            if (!isFree)
            {
                if (stake < _rules.MinStake || stake > _rules.MaxStake)
                    return Refuse(SlotPullOutcome.StakeOutOfRange, playerId, stake);

                TicketTransaction tx = ledger.RequestSpend(playerId, stake, "Slots:pull");
                if (!tx.Granted) return Refuse(SlotPullOutcome.CouldNotPay, playerId, stake);
            }

            // A free pull can arrive with a stale or degenerate stake (nobody
            // re-asks the bet size mid-bonus) — clamp so a win still pays
            // something sensible instead of silently multiplying by zero.
            int effectiveStake = isFree ? ClampToStakeRange(stake) : stake;

            SlotSymbol reel0 = SpinReel(0);
            SlotSymbol reel1 = SpinReel(1);
            SlotSymbol reel2 = SpinReel(2);

            int returned = 0;
            bool triggeredBonus = false;

            if (reel0 == reel1 && reel1 == reel2)
            {
                if (reel0 == _rules.BonusSymbol)
                {
                    triggeredBonus = true;
                    BonusPullsRemaining += _rules.BonusFreePulls;
                    BonusMultiplier = _rules.BonusMultiplier;
                }
                else if (_rules.TryGetPayout(reel0, out int multiplier))
                {
                    // long intermediate: an Inspector-tuned MaxStake or
                    // paytable multiplier far outside today's placeholder
                    // numbers must not silently wrap the int multiplication
                    // negative and skip payment (or wrap positive and pay
                    // the wrong amount) — clamp instead of overflow.
                    long rawReturned = (long)effectiveStake * multiplier * (isFree ? BonusMultiplier : 1);
                    returned = rawReturned > int.MaxValue ? int.MaxValue : (int)rawReturned;
                }
            }

            if (isFree) BonusPullsRemaining--;
            if (BonusPullsRemaining <= 0) BonusMultiplier = 1;

            if (returned > 0)
                ledger.Award(playerId, returned, isFree ? "Slots:bonus-win" : "Slots:win");

            var pull = new SlotPull(SlotPullOutcome.Spun, playerId, stake, reel0, reel1, reel2, returned,
                                    isFree, triggeredBonus, BonusPullsRemaining, BonusMultiplier);
            LastPull = pull;
            OnPulled?.Invoke(pull);
            return pull;
        }

        private SlotSymbol SpinReel(int index)
        {
            SlotSymbol[] strip = _rules.ReelStrips[index];
            return strip[_rng.NextInt(strip.Length)];
        }

        // Manual clamp rather than System.Math.Clamp: this class is pure C#,
        // no Unity, so Mathf isn't an option either — Math.Clamp(int,int,int)
        // needs .NET Standard 2.1, and this project targets 2.0
        // (ProjectSettings.asset's apiCompatibilityLevel), same reason
        // TicketLedger.Clamp and SlotRules.Sanitize already do this by hand.
        private int ClampToStakeRange(int stake) =>
            stake < _rules.MinStake ? _rules.MinStake : (stake > _rules.MaxStake ? _rules.MaxStake : stake);

        private SlotPull Refuse(SlotPullOutcome outcome, ulong playerId, int stake) =>
            new SlotPull(outcome, playerId, stake, default, default, default, 0, false, false,
                        BonusPullsRemaining, BonusMultiplier);
    }
}
