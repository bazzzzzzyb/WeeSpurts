# Wee Spurts — Claude Code Context

Online party game for Steam. Wii Sports readability + "friendslop" chaos: exaggerated physics, between-turn betting with fake coins, heckling, and a satirical drink meter. First minigame: **Bowling**. Built in Unity/C# by Tony + Braeden — **two beginner developers** — with Claude Code as the engineering team.

## Read before coding

- `Docs/GameBible.md` — single source of truth. If code and Bible disagree, the Bible wins (or gets updated on purpose).
- `Docs/Roadmap.md` — build order (dependency graph, not calendar).
- `Docs/DefinitionOfDone.md` — exit criteria per system. Scope every session against these.
- The doc for whatever you're touching: `Networking.md`, `UI.md`, `ArtGuide.md`, `CodingStandards.md`.
- `BLUEPRINT.md` — the master plan, market research, and phase gates.

## Golden rules

1. **One system per session.** Never "build the game." If asked for more, scope down and say so.
2. **Small diffs.** Add/modify a few files, not rewrites. Prefer "add this one script."
3. **No invented APIs.** Unity, Mirror, and Facepunch.Steamworks APIs must be real. If unsure a method exists, say so and check the official docs (mirror-networking.gitbook.io, wiki.facepunch.com/steamworks, docs.unity3d.com). The humans cannot catch hallucinated APIs — you are the last line of defense.
4. **Plan first.** Before writing code: restate the task, list assumptions, list files you'll add/change. Wait for a go-ahead on anything ambiguous.
5. **Teach while you build.** Brief "why" comments and explanations — Tony and Braeden are learning Unity/C# through this project.
6. **Update the Bible** when a decision is made or changed, and add a change-log line.
7. **Keep them testable.** End every task with "now do X in the editor and expect Y." Never stack untested changes.

## Unity-specific rules

- **Never hand-edit `.unity` scene files or `.prefab` YAML** unless explicitly asked; guide the human through editor steps instead ("select the Canvas, add component X, set Y to 3"). Scene surgery by text edit is how beginners' projects die.
- The Unity project lives in `/Unity`. Code goes in `Unity/Assets/_Project/Scripts/<System>/` per `Docs/CodingStandards.md`.
- Config/tunables (ball stats, bet options, lane setups) are **ScriptableObjects**, not hard-coded constants, so Tony can tweak feel without code.
- Editor-vs-build differences are real for networking. Anything networked gets tested with two builds, not two editor instances.

## Networking model (fixed — see Docs/Networking.md)

Mirror + FizzySteamworks over Steam relay; Facepunch.Steamworks for lobbies/invites; dev App ID 480. **Host-authoritative.** Bowling syncs **launch parameters** (position, direction, power, spin, seed) — never per-frame physics. No client trusts another client. Handle join/leave/disconnect at every state.

## Git

- `main` stays working; feature branches (`feat/bowling-scoring`).
- Git LFS for binaries; never commit `Library/`.
- Scenes/prefabs are the #1 merge hazard — one owner per scene, prefer prefabs.

## Specialist agents

`.claude/agents/` defines the team: `gameplay-engineer`, `steam-engineer`, `ui-engineer`, `physics-tech-artist`, `qa-engineer`. Use `qa-engineer` to review any networking or scoring code before it's called done. Slash commands in `.claude/commands/`: `/build-system`, `/qa-review`.

## Tony's role

Tony is creative director: he decides what's fun and what ships. Feel decisions ("does this throw feel good?") are his, by playing — not yours, by reasoning. When a task touches an item in `Docs/OpenQuestions.md`, don't decide it silently; surface the question.
