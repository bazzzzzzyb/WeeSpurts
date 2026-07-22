# Roadmap (systems, not weeks)

> Phase-by-phase detail, effort guesses, and the gates between systems live in `BLUEPRINT.md` §6. This doc is the dependency graph.

Systems survive; timelines drift. Build in dependency order. Each system is "done" only when it meets its criteria in `DefinitionOfDone.md`. No calendar dates — a system is finished when it's finished.

```
[1] Core Framework
      ↓
[2] Steam Framework
      ↓
[3] Gameplay Framework
      ↓
[4] Bowling
      ↓
[5] Menu / Lobby UI
      ↓
[6] Slop Layer (bets · drink meter · taunts)
      ↓
[7] Progression (coins · cosmetics · achievements)
      ↓
[8] Second minigame
```

## System summaries

**[1] Core Framework** — Repo, Unity project in `/Unity`, Git+LFS, folder structure, `GameManager`, scene loader, an `AudioManager` stub. The skeleton everything hangs on.

**[2] Steam Framework** — Facepunch initialized against App ID 480, Steam overlay confirmed, Mirror + FizzySteamworks installed, a bare "host creates lobby → friend joins via invite" flow with no gameplay yet. *De-risks the whole project — reach it early.*

**[3] Gameplay Framework** — Turn manager, player abstraction, score model, input handling. Game-agnostic scaffolding the bowling logic plugs into.

**[4] Bowling** — Lane, pins, ball, aim+power+spin, 10-frame scoring. First single-machine (feel), then networked over the Steam Framework using the launch-parameter sync in `Networking.md`.

**[5] Menu / Lobby UI** — The screens in `UI.md`. Makes the game playable by humans, not just the editor.

**[6] Slop Layer** — The differentiator hypothesis: between-turn betting with fake coins, the satirical drink meter, taunt emotes/voice lines. Synced across the lobby.

**[7] Progression** — Fake-coin economy, cosmetics, Steam achievements/stats. Only after the core loop is proven fun.

**[8] Second minigame** — Do NOT start until Bowling has shipped a fun networked loop end-to-end.

## Milestone-ish checkpoints (outcomes, not dates)
- **First playable feel:** a full 10-frame bowling game feels good on one machine.
- **First online game:** you + Braeden finish a networked game from separate houses.
- **First funny game:** the slop layer makes a networked game genuinely funny, not just functional. ← the real greenlight for everything after.
- **First store build:** Steam Direct paid, real App ID, a page, a build friends can install.
