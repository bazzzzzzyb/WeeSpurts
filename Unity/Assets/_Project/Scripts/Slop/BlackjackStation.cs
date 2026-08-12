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
    /// them), so once someone is sitting at THIS table, <see cref="UI.BlackjackHud"/>
    /// (a Canvas child of this station, built by ThunderLanesVenueStationSetupTool)
    /// is what reads their clicks and calls the public methods below — same
    /// shape as ThrowerAimSlide owning input during Bowling, just through a
    /// real UI instead of raw keys now. This class stays the single choke
    /// point that actually talks to <see cref="BlackjackTable"/> and the
    /// ledger; the HUD never touches either directly.
    ///
    /// BET SELECTION: <see cref="SelectedBet"/> is set by the HUD's slider
    /// (clamped to the config's range) and used by <see cref="Deal"/> — sitting
    /// down no longer auto-deals at a fixed stake.
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

        /// <summary>The bet the next <see cref="Deal"/> will use. Set by the HUD's slider, always clamped to the config's range.</summary>
        public int SelectedBet { get; private set; }

        public int MinBet => config != null ? config.MinBet : 0;
        public int MaxBet => config != null ? config.MaxBet : 0;

        /// <summary>Read-only view of the engine for a HUD to render — Phase, PlayerHands, DealerHand, LastRoundResults, CanDouble/CanSplit.</summary>
        public BlackjackTable Table => GetTable();

        /// <summary>Whoever is currently sitting here, or null. A HUD compares this against its own PlayerAvatar to decide whether to show itself.</summary>
        public PlayerAvatar SeatedPlayer => _seatedPlayer;

        /// <summary>True while the LOCAL machine's seated player may act here — the HUD's own "should I show myself" gate.</summary>
        public bool CanPlay => CanAct;

        /// <summary>
        /// True if a Double button should actually be enabled: BlackjackTable.CanDouble
        /// (every legality check EXCEPT affordability — see its own doc comment
        /// for why the table can't check this) AND the seated player can
        /// currently pay BlackjackTable.ActiveHandStake. This is the check the
        /// HUD reads, not the table's own CanDouble, or a broke player would
        /// see an enabled button that silently fails on click.
        /// </summary>
        public bool CanDouble => CanAct && GetTable().CanDouble
            && Ledger.BalanceOf(_seatedPlayer.EconomyPlayerId) >= GetTable().ActiveHandStake;

        /// <summary>Same affordability combination as <see cref="CanDouble"/>, for Split.</summary>
        public bool CanSplit => CanAct && GetTable().CanSplit
            && Ledger.BalanceOf(_seatedPlayer.EconomyPlayerId) >= GetTable().ActiveHandStake;

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

        /// <summary>Sit down. No hand is dealt yet — the HUD's bet slider + Deal button take it from here.</summary>
        public override void Interact(PlayerAvatar player)
        {
            if (!CanInteract(player)) return;
            if (Ledger == null) return;

            EnsureRegistered(player);
            player.EnterSeated(seat);
            _seatedPlayer = player;
            SelectedBet = config.MinBet;
        }

        /// <summary>Called by the HUD's slider. Clamps to the config's bet range so a UI bug can't submit a nonsense stake.</summary>
        public void SetBet(int bet)
        {
            if (config == null) return;
            SelectedBet = bet < config.MinBet ? config.MinBet : (bet > config.MaxBet ? config.MaxBet : bet);
        }

        /// <summary>Deal at <see cref="SelectedBet"/>. Re-validates the seat itself — see IInteractable's class comment on never trusting the caller.</summary>
        public DealOutcome Deal()
        {
            if (!CanAct) return DealOutcome.CouldNotPay;
            return GetTable().Deal(Ledger, _seatedPlayer.EconomyPlayerId, SelectedBet);
        }

        public bool Hit() => CanAct && GetTable().Hit(Ledger);
        public bool Stand() => CanAct && GetTable().Stand(Ledger);
        public bool Double() => CanAct && GetTable().Double(Ledger);
        public bool Split() => CanAct && GetTable().Split(Ledger);

        /// <summary>Stand up. Safe to call even if nobody's seated (the HUD's Leave button and the Esc shortcut both funnel through here).</summary>
        public void Leave()
        {
            if (_seatedPlayer == null) return;
            _seatedPlayer.EnterRoaming();
            _seatedPlayer = null;
        }

        /// <summary>True while the LOCAL machine's seated player may act here right now — the one guard every action method above shares.</summary>
        private bool CanAct =>
            _seatedPlayer != null && _seatedPlayer.IsThisMachinesPlayer
            && _seatedPlayer.Mode == ControlMode.Seated && Ledger != null;

        private void Update()
        {
            // Nobody seated here, or this machine doesn't own the seated avatar
            // (a remote player's seat, once this goes networked) — read nothing.
            // Mirrors PlayerAvatar's own IsThisMachinesPlayer gate.
            if (_seatedPlayer == null || !_seatedPlayer.IsThisMachinesPlayer) return;
            if (_seatedPlayer.Mode != ControlMode.Seated) { _seatedPlayer = null; return; }

            // Esc is a keyboard shortcut alongside BlackjackHud's Leave button,
            // not a replacement for the HUD — Hit/Stand/Double/Split are
            // click-only now (WeeSpurts.UI.BlackjackHud).
            if (Input.GetKeyDown(KeyCode.Escape)) Leave();
        }
    }
}
