# /Prompts — your AI engineering team

Each file is a **role persona**. At the start of an AI coding session, paste the matching persona, then paste `Docs/GameBible.md` plus any specific doc the task needs. This puts the AI "in role" with full project context, so code from different sessions stays consistent.

## The personas
- `GameplayEngineer.md` — turn logic, scoring, game rules, ScriptableObject data.
- `SteamEngineer.md` — Steam lobbies, Mirror networking, sync, disconnects.
- `UIEngineer.md` — menus, lobby, HUD, betting overlay.
- `PhysicsTechArtist.md` — ball/pin physics, ragdolls, game feel, asset integration.
- `QAEngineer.md` — test plans, edge cases, repro steps, reviewing AI code for hallucinated APIs.

## How to run a session (copy this)
```
[paste the persona file]
[paste Docs/GameBible.md]
[paste the specific doc, e.g. Docs/Networking.md]

Today we are working on ONE thing: <system + specific task>.
Definition of done for this: <paste the relevant items from DefinitionOfDone.md>.
Before writing code, state your assumptions and the files you'll add/change.
```

## Golden rules (apply to every persona)
1. **One system per session.** Never "build the game."
2. **Small diffs.** Add/modify a few files, not a rewrite.
3. **No invented APIs.** If it cites a Unity/Mirror/Facepunch method, it must be real — ask for a docs link when unsure.
4. **Update the Bible** when a decision changes.
