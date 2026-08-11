using UnityEngine;
using WeeSpurts.Core;
using WeeSpurts.Gameplay;
using WeeSpurts.Interaction;
using WeeSpurts.Player;

namespace WeeSpurts.Slop
{
    /// <summary>
    /// Shared shape for anything in the venue that talks to the ticket economy:
    /// the bar, the blackjack table, and later the slot machines, the front
    /// desk and the DJ booth. Modelled on
    /// <see cref="WeeSpurts.Bowling.LaneKioskInteractable"/> — same
    /// registry-based <see cref="IInteractable"/> plumbing, same
    /// [SerializeField]-everything discipline because an editor tool wires
    /// these fields, not Play-mode code (the AimPreview lesson).
    ///
    /// WHAT THIS BASE CLASS OWNS: registration with <see cref="PlayerInteractor"/>
    /// and the interaction point. WHAT IT DOES NOT OWN: what a station DOES —
    /// each subclass reads its own numbers from its own ScriptableObject and
    /// drives its own pure-C# engine (<see cref="Vendor"/>, <see cref="BlackjackTable"/>).
    /// That split is `Docs/SlopLayerPlan.md` Rule 1 applied to the scene layer:
    /// a station is a thin shell, never a second place that decides what
    /// something costs.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class VenueStation : MonoBehaviour, IInteractable
    {
        [Tooltip("Optional. Where the player is measured from/to. Empty means this object's own transform, which is what the setup tool leaves it as.")]
        [SerializeField] private Transform interactionPoint;

        /// <inheritdoc />
        public Transform InteractionPoint => interactionPoint != null ? interactionPoint : transform;

        // Same registry contract every IInteractable in this project follows —
        // see PlayerInteractor's class comment for why a registry and not physics.
        private void OnEnable() => PlayerInteractor.Register(this);
        private void OnDisable() => PlayerInteractor.Deregister(this);

        public abstract bool CanInteract(PlayerAvatar player);
        public abstract string GetPrompt(PlayerAvatar player);
        public abstract void Interact(PlayerAvatar player);

        /// <summary>
        /// This session's ticket ledger, or null if there is no GameManager in
        /// the scene yet. A station offers nothing rather than throwing when
        /// that's true — same "no config, no crash" discipline as every other
        /// station/engine pair in this project.
        /// </summary>
        protected static TicketLedger Ledger => GameManager.Instance != null ? GameManager.Instance.Tickets : null;

        /// <summary>
        /// Puts <paramref name="player"/> on the books if they aren't already.
        /// EVERY station calls this before its first ledger read — there is no
        /// join/lobby flow yet that does it up front, so the first station a
        /// player walks up to has to be able to register them itself. Register
        /// is idempotent, so calling this on every single interaction is
        /// deliberately cheap and safe rather than something to optimise away.
        /// </summary>
        protected static void EnsureRegistered(PlayerAvatar player)
        {
            if (GameManager.Instance == null || player == null) return;
            GameManager.Instance.EnsurePlayer(player.EconomyPlayerId);
        }
    }
}
