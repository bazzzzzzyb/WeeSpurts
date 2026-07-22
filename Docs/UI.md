# UI / UX

## Principles 🔒
- **10-second rule.** Any screen is understood in 10 seconds without instructions.
- **Big, loud, chunky.** Party-game energy: large buttons, bold type, playful motion. This is a couch-and-Discord game, not a spreadsheet.
- **The lobby is a room, not a menu.** Because "friends first," the lobby screen is where heckling starts — show everyone's avatar, let people react, make waiting fun.

## Core screens (minimum)
1. **Main menu** — Play (host), Join (accept Steam invite / code), Quit.
2. **Lobby** — player list w/ avatars, ready-up, host starts, invite button (opens Steam overlay).
3. **In-game HUD** — whose turn, frame/score, coin balances, bet prompt.
4. **Between-turns / betting overlay** — the "table talk" moment: place bets, taunt buttons/emotes.
5. **Results** — final scores, coins won/lost, rematch.

## Tech
- Unity **UI Toolkit** (modern) or **uGUI** (more tutorials, more beginner-friendly). Default to uGUI for the prototype unless a persona argues otherwise.
- Every interactive element must have a controller-navigable path (party games get played on couches with gamepads).

## Open ❓
- Visual identity / branding — after we know what makes Wee Spurts *different*.
- Emote/taunt set — depends on prototype learnings.
