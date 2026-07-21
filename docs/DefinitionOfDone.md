# Wee Spurts — Definition of Done

Exit criteria for each system. A system is **done** when every checkbox is ticked, not when the code compiles.

---

## Core Framework

- [ ] Unity project opens without errors on a fresh clone
- [ ] Folder structure matches `CodingStandards.md`
- [ ] Git LFS tracking configured for binary assets (`.png`, `.fbx`, `.wav`, etc.)
- [ ] Scene management: a minimal bootstrap scene loads a named scene by string
- [ ] Basic input manager in place (keyboard + mouse; gamepad is a stretch goal)
- [ ] CI build passes (or manual build verified on Windows)

---

## Steam Framework

- [ ] Facepunch.Steamworks initializes without errors using App ID 480 (Spacewar)
- [ ] Host can create a Steam lobby
- [ ] Client can join via Steam invite link
- [ ] Steam overlay opens in-game (Shift+Tab)
- [ ] No Steamworks calls crash in offline/no-Steam mode (graceful fallback or clear error)

---

## Gameplay Framework

- [ ] Mirror + FizzySteamworks transport is wired up
- [ ] Host and client can connect via Steam relay (no LAN hack required)
- [ ] A simple synced object (e.g., a cube) moves on host and replicates to clients
- [ ] Host/client roles are clearly established in code
- [ ] Network scene load works: host loads a scene, clients follow

---

## Bowling

- [ ] Players take turns (enforced, not just honored)
- [ ] Ball physics feels chaotic and funny (test: throw the ball badly — is it amusing?)
- [ ] Pin physics: pins fall, interact with each other, reset cleanly
- [ ] Score is tracked correctly (strikes, spares, open frames, 10th frame rules)
- [ ] Score is visible to all players
- [ ] Game ends after 10 frames; winner is declared
- [ ] Playable in a 2-player Steam lobby with no crashes over a full game

---

## Menu / Lobby UI

- [ ] Host can create a lobby from the main menu
- [ ] Friend can join via Steam invite
- [ ] Player list shows all connected players
- [ ] Ready-up system: game only starts when all players are ready
- [ ] Host can kick players
- [ ] Basic settings (audio volume) accessible from menu

---

## Slop Layer

- [ ] Before each player's turn, other players can place a bet on the outcome
- [ ] Fake currency is tracked per player
- [ ] Bet resolves automatically after the throw
- [ ] Taunt system: players can send a contextual taunt during another player's approach
- [ ] Fake drink prompt triggers on defined events (e.g., gutter ball, strike)
- [ ] All slop interactions are visible to all players in the lobby

---

## Progression

- [ ] Fake currency persists between sessions (local save or Steam Cloud)
- [ ] At least one cosmetic item is unlockable via currency
- [ ] At least one Steam achievement is implemented and triggers correctly
- [ ] Cosmetic equip screen is accessible from the main menu

---

## Second minigame

- TBD — exit criteria defined when the minigame is chosen (see `OpenQuestions.md` Q5)

---

## Change log
- 2026-07-21 — Document created.
