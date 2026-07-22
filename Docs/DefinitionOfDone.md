# Definition of Done

A system is not "done" because code exists. It's done when it meets these criteria. This is what you paste to an AI to scope its work, and what Tony checks before moving on.

## [1] Core Framework
- [ ] Unity project opens from `/Unity`; repo clones clean for both people.
- [ ] Git LFS active; a binary asset commits and pulls correctly on the other machine.
- [ ] Folder structure matches `CodingStandards.md`.
- [ ] `GameManager` exists and survives scene loads; can load an empty scene by name.
- [ ] Both people can push a change without breaking each other.

## [2] Steam Framework
- [ ] Facepunch initializes with App ID 480; Steam overlay appears when running.
- [ ] Mirror + FizzyFacepunch installed and compiling.
- [ ] Host can create a Steam lobby; a friend can join via Steam invite.
- [ ] Player list reflects who's in the lobby; leaving updates it.
- [ ] No gameplay required yet — just a reliable lobby.

## [3] Gameplay Framework
- [ ] Turn manager cycles players in order and exposes "whose turn."
- [ ] Player abstraction holds id, name, score, coins.
- [ ] Score model is data-driven (ScriptableObject or serializable), not hard-coded.

## [4] Bowling
- [ ] Aim + power + spin control that *feels good* (Tony's call — subjective and required).
- [ ] Pins knock down with satisfying, funny physics.
- [ ] Correct 10-frame scoring incl. spares/strikes/10th-frame bonus.
- [ ] Works single-machine first.
- [ ] Then works networked: active player throws, all clients see the same result, host authoritative.
- [ ] Disconnect mid-turn doesn't freeze the game.

## [5] Menu / Lobby UI
- [ ] All core screens from `UI.md` exist and are controller-navigable.
- [ ] A new player reaches an online game without being told how.

## [6] Slop Layer
- [ ] Between-turn betting with fake coins, synced to all players.
- [ ] Satirical drink meter that comedically affects the active player.
- [ ] Taunt emotes / voice lines, synced.
- [ ] Verdict gate: a networked game with friends is genuinely *funny*. If not, iterate here before proceeding.

## [7] Progression
- [ ] Fake-coin balance persists across sessions.
- [ ] At least one cosmetic unlock loop.
- [ ] Steam achievements fire correctly.

## [8] Second minigame
- [ ] Only begins after Bowling passes the "first funny game" gate.
- [ ] Reuses Gameplay Framework + Steam Framework without forking them.
