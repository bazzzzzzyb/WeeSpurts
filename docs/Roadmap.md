# Wee Spurts — Roadmap

Build order is a **dependency graph**, not a calendar. Each system must satisfy its Definition of Done (see `DefinitionOfDone.md`) before the next system begins.

---

## Dependency graph

```
Core Framework  →  Steam Framework  →  Gameplay Framework  →  Bowling
                                                                 ↓
                                                       Menu / Lobby UI
                                                                 ↓
                                                       Slop Layer (bets, drink, taunts)
                                                                 ↓
                                                          Progression
                                                                 ↓
                                                        Second minigame
```

---

## System summaries

### 1. Core Framework
Unity project scaffold: folder structure, Git LFS, scene management, basic input, build pipeline.

**Blocks:** Everything.

### 2. Steam Framework
Facepunch.Steamworks integration: lobby creation/join, friend invites, Steam overlay, dev App ID (480) working.

**Blocks:** Multiplayer gameplay.

### 3. Gameplay Framework
Mirror + FizzySteamworks transport wired up: host/client roles, network scene loading, basic synchronized objects.

**Blocks:** Bowling (and all future minigames).

### 4. Bowling
First minigame. Chaotic physics lane. Turn-based. Scores tracked. Fun enough to play twice.

**Blocks:** Menu/Lobby UI (needs a real game to navigate to).

### 5. Menu / Lobby UI
Steam lobby list, host/join flow, player list, ready-up screen, settings.

**Blocks:** Slop Layer (needs a place to surface bet prompts between turns).

### 6. Slop Layer
Bets, fake drinks, and taunts system. The secret sauce. Tied to bowling turns.

**Blocks:** Progression (can't design rewards until we know what behaviors to reward).

### 7. Progression
Fake currency, cosmetic unlocks, achievement hooks (Steam).

**Blocks:** Second minigame (don't add content until the loop is proven fun).

### 8. Second minigame
TBD — see `OpenQuestions.md` Q5.

---

## Change log
- 2026-07-21 — Document created.
