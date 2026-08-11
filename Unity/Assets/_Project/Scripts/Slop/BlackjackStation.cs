using UnityEngine;
using WeeSpurts.Gameplay;
using WeeSpurts.Player;

namespace WeeSpurts.Slop
{
    /// <summary>
    /// The casino nook's card table, as something you can walk up to and use —
    /// the second concrete <see cref="VenueStation"/>. Sits the player down
    /// with the existing <see cref="ControlMode.Seated"/> and drives the
    /// existing <see cref="BlackjackTable"/> engine underneath.
    ///
    /// INPUT WHILE SEATED IS NOT ROUTED THROUGH IInteractable. PlayerAvatar.
    /// ApplyMode turns PlayerInteractor OFF for Seated (same reason it's off
    /// for Bowling: a seated player must not re-trigger the thing that seated
    /// them), so once someone is sitting at THIS table, THIS component reads
    /// their Hit/Stand/leave input directly in Update — same shape as
    /// ThrowerAimSlide owning input during Bowling. Deliberately debug-key, no
    /// card UI: Stage 2 of the venue buildout only asks that a hand be
    /// PLAYABLE, and a felt-and-chips UI is real work nobody has scoped yet.
    /// OnGUI below is the DebugHud-style stand-in.
    ///
    /// ONE STAKE, NO BET SELECTION: every deal bets config.MinBet. Same
    /// reasoning as BarStation selling a single item — this stage proves the
    /// table is reachable, it doesn't build a betting UI.
    /// </summary>
    [DisallowMultipleComponent]
    public class BlackjackStation : VenueStation
    {
        [Tooltip("Table rules: bet range, payouts, dealer policy, shoe size. WeeSpurts/Blackjack Config asset.")]
        [SerializeField] private BlackjackConfig config;

        [Tooltip("Where the player is teleported to sit — a stool/seat transform facing the table. Empty means they sit wherever they were standing.")]
        [SerializeField] private Transform seat;

        [Tooltip("Shoe seed. Fixed rather than randomised, same DeterministicRng discipline BlackjackTable already uses elsewhere — revisit once there's a reason to vary it per session (e.g. a networked host seeding it).")]
        [SerializeField] private int seed = 20260811;

        private BlackjackTable _table;
        private PlayerAvatar _seatedPlayer;

        private BlackjackTable GetTable()
        {
            if (_table == null)
            {
                BlackjackRules rules = config != null ? config.CreateRules() : new BlackjackRules();
                _table = new BlackjackTable(rules, seed);
            }
            return _table;
        }

        public override bool CanInteract(PlayerAvatar player)
        {
            // Only usable from ROAMING, to sit down. Once seated, PlayerInteractor
            // is off for that player anyway (see class comment), so this never
            // needs to distinguish "already seated here" from "usable".
            return config != null && player != null && player.Mode == ControlMode.Roaming;
        }

        public override string GetPrompt(PlayerAvatar player) =>
            CanInteract(player) ? "Sit Down — Blackjack" : string.Empty;

        /// <summary>Sit down and deal the first hand at the table's minimum bet.</summary>
        public override void Interact(PlayerAvatar player)
        {
            if (!CanInteract(player)) return;

            TicketLedger ledger = Ledger;
            if (ledger == null) return;

            EnsureRegistered(player);
            player.EnterSeated(seat);
            _seatedPlayer = player;

            GetTable().Deal(ledger, player.EconomyPlayerId, config.MinBet);
        }

        private void Update()
        {
            // Nobody seated here, or this machine doesn't own the seated avatar
            // (a remote player's seat, once this goes networked) — read nothing.
            // Mirrors PlayerAvatar's own IsThisMachinesPlayer gate.
            if (_seatedPlayer == null || !_seatedPlayer.IsThisMachinesPlayer) return;
            if (_seatedPlayer.Mode != ControlMode.Seated) { _seatedPlayer = null; return; }

            TicketLedger ledger = Ledger;
            if (ledger == null) return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                _seatedPlayer.EnterRoaming();
                _seatedPlayer = null;
                return;
            }

            BlackjackTable table = GetTable();
            if (table.Phase == BlackjackPhase.PlayerTurn)
            {
                if (Input.GetKeyDown(KeyCode.Space)) table.Hit(ledger);
                else if (Input.GetKeyDown(KeyCode.Return)) table.Stand(ledger);
            }
            else if (table.Phase == BlackjackPhase.Settled)
            {
                // Space deals the next hand. Return does nothing here — there is
                // no hand to stand on, and silently re-dealing on the wrong key
                // would be a surprising thing for a debug control to do.
                if (Input.GetKeyDown(KeyCode.Space))
                    table.Deal(ledger, _seatedPlayer.EconomyPlayerId, config.MinBet);
            }
        }

        private void OnGUI()
        {
            if (_seatedPlayer == null || !_seatedPlayer.IsThisMachinesPlayer) return;
            if (_seatedPlayer.Mode != ControlMode.Seated) return;

            BlackjackTable table = GetTable();
            string dealerLine = table.Phase == BlackjackPhase.PlayerTurn
                ? $"{table.DealerUpCard} + ?"
                : table.DealerHand.ToString();

            GUI.Box(new Rect(10, 10, 620, 26),
                $"BLACKJACK — {table.Phase}   YOU: {table.PlayerHand}   DEALER: {dealerLine}");

            string hint = table.Phase switch
            {
                BlackjackPhase.PlayerTurn => "SPACE = Hit   ENTER = Stand   ESC = Leave",
                BlackjackPhase.Settled =>
                    $"Last round: {table.LastRound.Outcome} ({table.LastRound.Net:+#;-#;0} tickets)   SPACE = Deal Again   ESC = Leave",
                _ => "ESC = Leave"
            };
            GUI.Box(new Rect(10, 40, 620, 26), hint);
        }
    }
}
