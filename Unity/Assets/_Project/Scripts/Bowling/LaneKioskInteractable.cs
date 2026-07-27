using UnityEngine;
using WeeSpurts.Interaction;
using WeeSpurts.Player;

namespace WeeSpurts.Bowling
{
    /// <summary>
    /// The lane's score console, as something you can walk up to and use. The
    /// first implementation of <see cref="IInteractable"/>, and the diegetic
    /// way a match begins.
    ///
    /// WHY THIS EXISTS AT ALL (Docs/OpenQuestions.md:23, Tony, 2026-07-22):
    /// "game start isn't a lobby-menu button, it's diegetic — players free-roam
    /// the alley, walk up to a lane, and type their name in at the lane itself
    /// (like real life) to start the match." This object is the "walk up to a
    /// lane" half of that. The "type your name in" half is STILL PARKED — it
    /// needs a text-entry UI and a player-name model that don't exist yet, and
    /// building a fake one now would just have to be thrown away. Pressing E
    /// starts the match with the debug players TurnManager already built. When
    /// name entry lands, it slots in inside <see cref="Interact"/> and nothing
    /// else in the interaction system changes shape.
    ///
    /// WHERE IT PHYSICALLY IS, and why it looks odd on paper: down in settee
    /// pit Pit05_06, beside the pit's score console, NOT on the approach. The
    /// venue greybox v2 deleted the per-lane approach kiosks because the
    /// approach band has to stay clear for the throw camera. Walking down the
    /// steps into the pit to start the game is the intended beat, confirmed by
    /// Tony — it is a place you go, which is the whole point.
    ///
    /// NO RENDERER. The pit's existing ConsolePost/ConsolePanel geometry is the
    /// visual; this is a bare transform sitting next to it. Adding a mesh here
    /// would double up on geometry that AlleyGreyboxBuilder already owns.
    /// </summary>
    [DisallowMultipleComponent]
    public class LaneKioskInteractable : MonoBehaviour, IInteractable
    {
        // [SerializeField] on every reference — wired by RoamingSetupTool at
        // EDITOR time, so anything Unity doesn't serialize is null again after
        // the next scene reload (the AimPreview lesson).
        [Tooltip("The scene's BowlingPresentation (on the BowlingGame object) — the thing this kiosk starts. It is the LOCAL half of the bowling game because starting a match from here also hands this machine's player to the foul line. Without it the kiosk offers nothing rather than throwing when used.")]
        [SerializeField] private BowlingPresentation game;

        [Tooltip("Which lane this kiosk belongs to (1-based, matching AlleyLayoutConfig.PlayableLaneIndex and the Anchor_LaneNN markers). Shown in the prompt, and the field the multi-lane version will key off when there is more than one playable lane.")]
        [SerializeField] private int laneNumber = 6;

        [Tooltip("Optional. Where the player is measured from/to. Empty means this object's own transform, which is what the setup tool leaves it as.")]
        [SerializeField] private Transform interactionPoint;

        /// <inheritdoc />
        public Transform InteractionPoint => interactionPoint != null ? interactionPoint : transform;

        // Register/deregister in OnEnable/OnDisable, which is the contract
        // PlayerInteractor's registry is built on. OnDisable also runs
        // immediately before OnDestroy for an active object, so a kiosk that is
        // deleted takes itself out of the list on the way past.
        private void OnEnable() => PlayerInteractor.Register(this);
        private void OnDisable() => PlayerInteractor.Deregister(this);

        /// <summary>
        /// Usable only when there is no match happening and none has finished.
        ///
        /// BOTH flags matter and they are not the same thing.
        /// MatchInProgress goes false the instant the last frame resolves, so
        /// checking only that would re-offer "[E] Start Game" over the final
        /// scorecard while everyone is still reading it — and the player is
        /// standing right here, because BowlingPresentation hands them back
        /// to roaming when the match ends. MatchOver is what keeps the kiosk
        /// quiet until R reloads the scene for a rematch. (Same pair of flags,
        /// same reasoning, as DebugHud's "is the HUD on screen" check.)
        ///
        /// Both flags are read through BowlingPresentation's passthroughs rather
        /// than by holding a second reference to BowlingMatchFlow — this kiosk
        /// only ever asks one question, so it only needs one reference.
        /// </summary>
        public bool CanInteract(PlayerAvatar player)
        {
            if (game == null) return false;
            return !game.MatchInProgress && !game.MatchOver;
        }

        /// <inheritdoc />
        public string GetPrompt(PlayerAvatar player)
        {
            // Empty, not a "match in progress" message: an unusable kiosk is
            // never selected by the interactor, so this would never be drawn
            // anyway — and returning a real-looking string here is how a prompt
            // that lies gets introduced later by accident.
            return CanInteract(player) ? BuildPrompt() : string.Empty;
        }

        /// <summary>
        /// Start the match, with this player as the thrower.
        ///
        /// THE RE-CHECK IS NOT REDUNDANT. Today PlayerInteractor already tested
        /// CanInteract this frame, so this looks like belt-and-braces. When this
        /// becomes a Mirror [Command] it is the actual authority check: the host
        /// runs this method, and it must not take a client's word that the
        /// client was allowed to press the button (Docs/Networking.md — "no
        /// client trusts another client"). Writing it now means the security
        /// check is already in the right place when the wire shows up.
        ///
        /// StartMatch is itself idempotent (it early-returns if a match is
        /// already running), so this is defence in depth, not the only guard.
        /// </summary>
        public void Interact(PlayerAvatar player)
        {
            if (!CanInteract(player)) return;
            game.StartMatch(player);
        }

        /// <summary>
        /// The one and only place the prompt string is built, so the wording
        /// can never disagree between the HUD and a future tooltip or log line.
        ///
        /// THE ACTION ONLY — no "[E]". InteractionPromptHud puts the key on the
        /// front, because the binding belongs to the player, not to this console.
        /// See IInteractable.GetPrompt for the full reasoning.
        /// </summary>
        private string BuildPrompt() => $"Start Game — Lane {laneNumber}";
    }
}
