using System;
using System.Collections.Generic;

namespace WeeSpurts.Gameplay
{
    /// <summary>
    /// Why a request was granted or refused. Returned rather than thrown,
    /// because "you can't afford that" is a normal game event the UI has to
    /// render (a shake, a red flash, a barman shaking his head), not an
    /// exceptional condition.
    /// </summary>
    public enum TicketResult
    {
        Granted,
        InsufficientFunds,
        InvalidAmount,
        UnknownPlayer,
        BalanceCeiling
    }

    /// <summary>
    /// The answer to one request. A struct because it is small, immutable and
    /// created constantly — no reason to make garbage for the collector.
    /// </summary>
    public readonly struct TicketTransaction
    {
        public readonly ulong PlayerId;
        public readonly TicketResult Result;
        /// <summary>Signed: negative for a spend, positive for an award. Zero on a refusal.</summary>
        public readonly int Delta;
        public readonly int BalanceAfter;
        /// <summary>Short tag for logs and the ticket-feed HUD, e.g. "bar:pint", "bet:won".</summary>
        public readonly string Reason;

        public bool Granted => Result == TicketResult.Granted;

        public TicketTransaction(ulong playerId, TicketResult result, int delta, int balanceAfter, string reason)
        {
            PlayerId = playerId;
            Result = result;
            Delta = delta;
            BalanceAfter = balanceAfter;
            Reason = reason;
        }

        public override string ToString() =>
            $"[{Reason}] player {PlayerId}: {Result} {Delta:+#;-#;0} -> {BalanceAfter}";
    }

    /// <summary>
    /// THE one and only owner of every fake ticket in Wee Spurts.
    ///
    /// This is `Docs/SlopLayerPlan.md` F3, built to the three rules in that
    /// doc's "Build it single-machine first" section. Read those before adding
    /// anything here, because they are the whole reason this class exists in
    /// this shape:
    ///
    /// RULE 1 — ONE CHOKE POINT. No script anywhere writes a balance. The bar,
    /// the slots, the cosmetics counter and the betting window all call this.
    /// When the networked wrapper lands, exactly one class changes and no call
    /// site does. This is the same bet `BowlingPresentation.ThrowInputAllowed`
    /// made for throw input, and the spike proved it pays.
    ///
    /// RULE 2 — REQUEST, THEN DECIDE. There is deliberately no `Balance` setter
    /// and no `Spend()` that just does it. You <see cref="RequestSpend"/>, the
    /// ledger decides, and you react to the answer. Right now the ledger says
    /// yes instantly on this machine; under Mirror the same call becomes a
    /// Command and the host answers. The call sites are already written for
    /// the networked world. Client-authoritative money is THE classic
    /// multiplayer hole and `Docs/Networking.md` already forbids it.
    ///
    /// RULE 3 — IDS, NEVER REFERENCES. Players are ints here, not PlayerData
    /// pointers. An int replicates for free; an object reference does not
    /// replicate at all.
    ///
    /// DELIBERATELY PURE C#: no MonoBehaviour, no UnityEngine, no statics, no
    /// singleton. That is what lets it be unit-tested without Play mode
    /// (the `BowlingScorer` standard) — which is what lets Claude write it and
    /// Tony trust it without pressing Play. Whoever owns the match instantiates
    /// one and hands it out.
    ///
    /// NOT IN SCOPE HERE, on purpose: what things cost, what a bet pays, what
    /// the slots do. Those are S1/S4 and they are Tony's numbers to tune. This
    /// class only guarantees that tickets cannot be conjured or lost.
    /// </summary>
    public class TicketLedger
    {
        /// <summary>
        /// Hard ceiling on a balance. Exists so a runaway payout bug reports
        /// itself as a refused transaction instead of silently wrapping an int
        /// negative — an overflowed balance would look exactly like a player
        /// going bankrupt, which is a genuinely horrible bug to track down.
        /// A billion is far past anything the economy should ever produce.
        /// </summary>
        public const int MAX_BALANCE = 1_000_000_000;

        private readonly Dictionary<ulong, int> _balances = new Dictionary<ulong, int>();

        /// <summary>
        /// Raised after every GRANTED transaction, never for a refusal — a
        /// refusal is the caller's business to render, but a grant is everyone's
        /// (the ticket-feed HUD, an audio cue, and later the ClientRpc that tells
        /// the other machines). Subscribe rather than polling balances.
        /// </summary>
        public event Action<TicketTransaction> OnTransaction;

        /// <summary>
        /// Raised for refusals. Separate event because the reactions are
        /// completely different — a grant is a "cha-ching", a refusal is a
        /// "you're broke" shake — and keeping them apart means neither
        /// listener has to branch on Result.
        /// </summary>
        public event Action<TicketTransaction> OnRefused;

        /// <summary>Every player the ledger knows about. Order is not meaningful.</summary>
        public IEnumerable<ulong> Players => _balances.Keys;

        /// <summary>
        /// Put a player on the books. Idempotent by design: calling it twice
        /// for the same id does NOT reset their balance, because a reconnect
        /// or a late-joining client re-running setup must not wipe someone's
        /// tickets. Returns false if they were already registered, so a caller
        /// that cares can tell the difference.
        /// </summary>
        public bool Register(ulong playerId, int startingBalance = 0)
        {
            if (_balances.ContainsKey(playerId)) return false;
            _balances[playerId] = Clamp(startingBalance);
            return true;
        }

        public bool IsRegistered(ulong playerId) => _balances.ContainsKey(playerId);

        /// <summary>
        /// Current balance, or 0 for an unknown player. Read-only on purpose —
        /// see Rule 2. If you are looking for a setter, you want
        /// <see cref="RequestSpend"/> or <see cref="Award"/>.
        /// </summary>
        public int BalanceOf(ulong playerId) =>
            _balances.TryGetValue(playerId, out int b) ? b : 0;

        /// <summary>
        /// Can this player afford it? Pure query, changes nothing. For greying
        /// out a menu item BEFORE the player clicks it — never as a
        /// pre-check the caller then acts on, because between the check and
        /// the spend the balance can move (a payout lands, a bet resolves).
        /// Always act on the result of <see cref="RequestSpend"/> itself.
        /// </summary>
        public bool CanAfford(ulong playerId, int amount) =>
            amount > 0 && IsRegistered(playerId) && BalanceOf(playerId) >= amount;

        /// <summary>
        /// Ask to spend. The ledger decides. THIS IS THE ONLY WAY TICKETS LEAVE
        /// A BALANCE — see Rule 1.
        /// </summary>
        /// <param name="amount">Must be positive. A negative "spend" would be a
        /// backdoor credit, so it is refused rather than quietly inverted.</param>
        /// <param name="reason">Short tag for the log and the ticket feed.</param>
        public TicketTransaction RequestSpend(ulong playerId, int amount, string reason = "spend")
        {
            if (!IsRegistered(playerId))
                return Refuse(playerId, TicketResult.UnknownPlayer, reason);

            if (amount <= 0)
                return Refuse(playerId, TicketResult.InvalidAmount, reason);

            int balance = _balances[playerId];
            if (balance < amount)
                return Refuse(playerId, TicketResult.InsufficientFunds, reason);

            // No overdraft, ever. A negative balance has no meaning in this
            // game and would make every downstream comparison ("can they bet?")
            // subtly wrong.
            _balances[playerId] = balance - amount;
            return Grant(playerId, -amount, _balances[playerId], reason);
        }

        /// <summary>
        /// Pay a player. Also goes through the choke point, for the same reason
        /// spends do: the networked version has to be host-authoritative, and
        /// awards are exactly where a cheating client would attack.
        /// </summary>
        public TicketTransaction Award(ulong playerId, int amount, string reason = "award")
        {
            if (!IsRegistered(playerId))
                return Refuse(playerId, TicketResult.UnknownPlayer, reason);

            if (amount <= 0)
                return Refuse(playerId, TicketResult.InvalidAmount, reason);

            int balance = _balances[playerId];
            if (balance > MAX_BALANCE - amount)
                return Refuse(playerId, TicketResult.BalanceCeiling, reason);

            _balances[playerId] = balance + amount;
            return Grant(playerId, amount, _balances[playerId], reason);
        }

        /// <summary>
        /// Move tickets between two players in ONE step — the shape every wager
        /// payout needs. Deliberately not "spend then award": as two calls, a
        /// refused second half would leave the first half already taken and
        /// tickets would vanish from the economy. Here it either fully happens
        /// or nothing changes.
        ///
        /// Returns the PAYEE's transaction (the interesting half for UI). The
        /// payer's is raised on <see cref="OnTransaction"/> too, so a ticket feed
        /// listening to the event sees both sides.
        /// </summary>
        public TicketTransaction Transfer(ulong fromPlayerId, ulong toPlayerId, int amount, string reason = "transfer")
        {
            if (!IsRegistered(fromPlayerId) || !IsRegistered(toPlayerId))
                return Refuse(toPlayerId, TicketResult.UnknownPlayer, reason);

            // Paying yourself is a no-op that would otherwise double-fire
            // events and confuse a ticket feed. Treated as invalid, loudly.
            if (fromPlayerId == toPlayerId || amount <= 0)
                return Refuse(toPlayerId, TicketResult.InvalidAmount, reason);

            if (_balances[fromPlayerId] < amount)
                return Refuse(fromPlayerId, TicketResult.InsufficientFunds, reason);

            if (_balances[toPlayerId] > MAX_BALANCE - amount)
                return Refuse(toPlayerId, TicketResult.BalanceCeiling, reason);

            // Both guards passed, so neither half can fail from here.
            _balances[fromPlayerId] -= amount;
            Grant(fromPlayerId, -amount, _balances[fromPlayerId], reason);

            _balances[toPlayerId] += amount;
            return Grant(toPlayerId, amount, _balances[toPlayerId], reason);
        }

        /// <summary>
        /// Total tickets across every registered player. Exists for ONE reason:
        /// tests and a debug HUD can assert it only changes when the economy
        /// means it to. If a future feature makes this drift without an
        /// explicit Award or a sink, something is conjuring money.
        /// </summary>
        public int TotalInCirculation()
        {
            int total = 0;
            foreach (int b in _balances.Values) total += b;
            return total;
        }

        private static int Clamp(int v) => v < 0 ? 0 : (v > MAX_BALANCE ? MAX_BALANCE : v);

        private TicketTransaction Grant(ulong playerId, int delta, int balanceAfter, string reason)
        {
            var tx = new TicketTransaction(playerId, TicketResult.Granted, delta, balanceAfter, reason);
            OnTransaction?.Invoke(tx);
            return tx;
        }

        private TicketTransaction Refuse(ulong playerId, TicketResult result, string reason)
        {
            var tx = new TicketTransaction(playerId, result, 0, BalanceOf(playerId), reason);
            OnRefused?.Invoke(tx);
            return tx;
        }
    }
}
