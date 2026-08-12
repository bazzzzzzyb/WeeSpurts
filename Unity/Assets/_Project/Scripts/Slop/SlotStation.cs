using UnityEngine;
using WeeSpurts.Gameplay;
using WeeSpurts.Player;

namespace WeeSpurts.Slop
{
    /// <summary>
    /// One of the casino nook's slot machines, as something you can walk up to
    /// and use — the third concrete <see cref="VenueStation"/>, same shape as
    /// <see cref="BlackjackStation"/> on purpose (one pattern for the station
    /// layer to learn, per <see cref="SlotMachine"/>'s own class comment).
    /// Sits the player down with the existing <see cref="ControlMode.Seated"/>
    /// and drives a <see cref="SlotMachine"/> engine underneath.
    ///
    /// INPUT WHILE SEATED IS NOT ROUTED THROUGH IInteractable — same reason as
    /// BlackjackStation: PlayerAvatar.ApplyMode turns PlayerInteractor off for
    /// Seated, so <see cref="UI.SlotHud"/> (a Canvas child of this station,
    /// built by ThunderLanesVenueStationSetupTool) reads the seated player's
    /// clicks directly and calls the public methods below. This class stays
    /// the single choke point that actually talks to <see cref="SlotMachine"/>
    /// and the ledger.
    ///
    /// ONE STATION PER PHYSICAL MACHINE: the venue has three SlotMachine_N art
    /// objects, so the setup tool creates three of these, each with its own
    /// seed (offset per machine) so they don't roll identical sequences.
    /// </summary>
    [DisallowMultipleComponent]
    public class SlotStation : VenueStation
    {
        [Tooltip("Machine paytable/reel strips/bonus rules. WeeSpurts/Slot Config asset.")]
        [SerializeField] private SlotConfig config;

        [Tooltip("Where the player is teleported to sit — a stool transform facing the machine. Empty means they sit wherever they were standing.")]
        [SerializeField] private Transform seat;

        [Tooltip("Reel seed. Fixed rather than randomised, same DeterministicRng discipline as BlackjackStation — give each machine in the venue a different value so they don't all roll identically.")]
        [SerializeField] private int seed = 20260812;

        private SlotMachine _machine;
        private PlayerAvatar _seatedPlayer;

        /// <summary>The bet the next <see cref="Pull"/> will use (ignored for a free bonus pull — see SlotMachine). Set by the HUD's slider, always clamped to the config's range.</summary>
        public int SelectedBet { get; private set; }

        public int MinStake => config != null ? config.MinStake : 0;
        public int MaxStake => config != null ? config.MaxStake : 0;

        /// <summary>Read-only view of the engine for a HUD to render — BonusPullsRemaining, BonusMultiplier, LastPull.</summary>
        public SlotMachine Machine => GetMachine();

        /// <summary>Whoever is currently sitting here, or null.</summary>
        public PlayerAvatar SeatedPlayer => _seatedPlayer;

        /// <summary>True while the LOCAL machine's seated player may act here — the HUD's own "should I show myself" gate.</summary>
        public bool CanPlay => CanAct;

        private SlotMachine GetMachine()
        {
            if (_machine == null)
            {
                SlotRules rules = config != null ? config.CreateRules() : new SlotRules();
                _machine = new SlotMachine(rules, seed);
            }
            return _machine;
        }

        public override bool CanInteract(PlayerAvatar player) =>
            config != null && player != null && player.Mode == ControlMode.Roaming;

        public override string GetPrompt(PlayerAvatar player) =>
            CanInteract(player) ? "Sit Down — Slots" : string.Empty;

        /// <summary>Sit down. No pull happens yet — the HUD's bet slider + Pull button take it from here.</summary>
        public override void Interact(PlayerAvatar player)
        {
            if (!CanInteract(player)) return;
            if (Ledger == null) return;

            EnsureRegistered(player);
            player.EnterSeated(seat);
            _seatedPlayer = player;
            SelectedBet = config.MinStake;
        }

        /// <summary>Called by the HUD's slider. Clamps to the config's stake range so a UI bug can't submit a nonsense bet.</summary>
        public void SetBet(int bet)
        {
            if (config == null) return;
            SelectedBet = bet < config.MinStake ? config.MinStake : (bet > config.MaxStake ? config.MaxStake : bet);
        }

        /// <summary>
        /// Pull the lever at <see cref="SelectedBet"/>. Re-validates the seat
        /// itself — see IInteractable's class comment on never trusting the
        /// caller. Returns a CouldNotPay refusal (never the default struct,
        /// whose zero-valued Outcome is Spun — the FIRST SlotPullOutcome enum
        /// entry, which would misreport "nothing happened" as "you won
        /// nothing") if nobody local is actually seated here to pull it.
        /// </summary>
        public SlotPull Pull()
        {
            if (!CanAct)
            {
                ulong playerId = _seatedPlayer != null ? _seatedPlayer.EconomyPlayerId : 0ul;
                return new SlotPull(SlotPullOutcome.CouldNotPay, playerId, SelectedBet,
                                    default, default, default, 0, false, false, 0, 1);
            }

            return GetMachine().Pull(Ledger, _seatedPlayer.EconomyPlayerId, SelectedBet);
        }

        /// <summary>Stand up. Safe to call even if nobody's seated (the HUD's Leave button and the Esc shortcut both funnel through here).</summary>
        public void Leave()
        {
            if (_seatedPlayer == null) return;
            _seatedPlayer.EnterRoaming();
            _seatedPlayer = null;
        }

        private bool CanAct =>
            _seatedPlayer != null && _seatedPlayer.IsThisMachinesPlayer
            && _seatedPlayer.Mode == ControlMode.Seated && Ledger != null;

        private void Update()
        {
            // Nobody seated here, or this machine doesn't own the seated avatar
            // (a remote player's seat, once this goes networked) — read nothing.
            if (_seatedPlayer == null || !_seatedPlayer.IsThisMachinesPlayer) return;
            if (_seatedPlayer.Mode != ControlMode.Seated) { _seatedPlayer = null; return; }

            // Esc is a keyboard shortcut alongside SlotHud's Leave button, not
            // a replacement for it — Pull is click-only (WeeSpurts.UI.SlotHud).
            if (Input.GetKeyDown(KeyCode.Escape)) Leave();
        }
    }
}
