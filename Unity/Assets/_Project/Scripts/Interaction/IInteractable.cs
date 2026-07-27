using UnityEngine;
using WeeSpurts.Player;

namespace WeeSpurts.Interaction
{
    /// <summary>
    /// Anything in the venue a player can walk up to and use: the lane kiosk
    /// (built today), and later the bar, the slot machines, the card dealer's
    /// table and the cosmetics counter.
    ///
    /// THIS IS PLUMBING, NOT A "START GAME" SCRIPT. Every one of those things
    /// wants the same three questions answered — "can I use this?", "what does
    /// the prompt say?", "do the thing" — so they get asked once, here, and
    /// each object answers for itself. <see cref="PlayerInteractor"/> is the
    /// only thing that asks.
    ///
    /// EVERY METHOD TAKES THE PLAYER, from day one, even though there is
    /// exactly one player in the scene today. The alternative — a parameterless
    /// interface plus a global "the player" singleton — reads fine right up
    /// until there are four avatars in the room, at which point every single
    /// implementor has to change shape. Passing the avatar costs nothing now
    /// and means the answers can already differ per player: a bar that refuses
    /// you because YOUR drink meter is full, a kiosk that only the host may
    /// use, a slot machine that knows whose coins are whose.
    ///
    /// WHERE MIRROR GOES (Docs/Networking.md — host-authoritative, "no client
    /// trusts another client"): <see cref="Interact"/> is the exact seam. It
    /// becomes a `[Command]` on the player's NetworkBehaviour, so the CLIENT
    /// only ever asks. The HOST then re-runs <see cref="CanInteract"/> itself,
    /// server-side, against its own copy of the world before doing anything —
    /// it must never take the caller's word that they were in range, looking at
    /// it, and allowed. A client that lies about any of those is the whole
    /// attack surface of a system like this, and re-validating on the host is
    /// what closes it. That is also why <see cref="Interact"/> implementations
    /// must re-check CanInteract rather than assuming the interactor already
    /// did: today it is belt-and-braces, later it is the actual security check.
    ///
    /// NO NETWORKING CODE HERE. Shaped for it, none of it built (CLAUDE.md).
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// Is this thing usable by <paramref name="player"/> RIGHT NOW?
        /// Return false and the interactor will not target it at all — no
        /// prompt, no keypress, as if it weren't there.
        ///
        /// Deliberately separate from <see cref="GetPrompt"/>: a lane kiosk
        /// whose match is already running has to stop offering, and it must not
        /// keep advertising "[E] Start Game" while doing so. One method for
        /// both would force every implementor to encode "unavailable" as a
        /// magic prompt string, and prompts that lie are worse than no prompt.
        /// </summary>
        bool CanInteract(PlayerAvatar player);

        /// <summary>
        /// The ACTION this offers <paramref name="player"/> — "Start Game",
        /// "Buy a Drink", "Play Slots". Return an empty string for "say nothing".
        ///
        /// RETURN THE ACTION ONLY, NEVER THE KEY. No "[E]", no "Press E to".
        /// <see cref="PlayerInteractor"/> owns the binding and
        /// <see cref="InteractionPromptHud"/> puts the prefix on, so the glyph
        /// on screen can never drift from the key that actually works.
        ///
        /// The reason is not tidiness — it is that WHICH key you press is a
        /// property of the PLAYER, not of the object in the room. Two players
        /// looking at the same bar, one on a rebound key and one on a controller
        /// (Docs/UI.md requires every interactive element to be
        /// controller-navigable), must see different prefixes on the same
        /// interactable. An object that bakes its own key in cannot do that, and
        /// the bug does not appear until rebinding exists — by which time every
        /// implementor has copied the mistake.
        ///
        /// Still per-player, because the ACTION text can legitimately differ:
        /// "Buy a Drink (5 coins)" vs "You've had enough".
        /// </summary>
        string GetPrompt(PlayerAvatar player);

        /// <summary>
        /// Do the thing. See the class comment: this is where a Mirror
        /// `[Command]` will live, and implementations must re-validate
        /// <see cref="CanInteract"/> themselves rather than trusting the caller.
        /// </summary>
        void Interact(PlayerAvatar player);

        /// <summary>
        /// The point in the world this interactable is measured from — how far
        /// away you are, and whether you are looking at it. Usually the
        /// object's own transform, but exposed so a big object (a bar counter
        /// four metres long) can put its hotspot somewhere sensible rather than
        /// at its pivot.
        ///
        /// May be null if the object has been destroyed mid-frame; callers must
        /// cope (<see cref="PlayerInteractor"/> does).
        /// </summary>
        Transform InteractionPoint { get; }
    }
}
