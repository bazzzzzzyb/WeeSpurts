# Wee Spurts

A chaotic online party game for Steam — Wii Sports energy with a "friendslop" twist: funny physics, yelling at your friends, fake gambling, and (satirical) drinking. First minigame: **Bowling**.

Built by Tony + Braeden, using AI as a specialist team.

---

## How this repo works (read this first)

This project is designed to be built *with AI as your engineering team*. The repo itself carries the context so every AI session stays consistent, even months apart.

- **`/Docs`** — the "Game Bible." The single source of truth for vision, tech decisions, and rules. Every AI session should be given the relevant doc(s) as context before it writes anything.
- **`/Prompts`** — role personas (Gameplay Engineer, Steam Engineer, etc.). Paste the matching persona at the start of a session to put the AI "in role."
- **`/Unity`** — the actual Unity project lives here.
- **`/Assets`** — source art/audio and asset-pack notes (not Unity's internal Assets folder — that's inside `/Unity`).

### The workflow, in one sentence

> Tony decides *what's fun and what ships*; the AI personas build one system at a time, always grounded in the Bible.

### Starting any AI coding session

1. Open the relevant persona from `/Prompts`.
2. Paste it, then paste `Docs/GameBible.md` (and any specific doc — e.g. `Networking.md`).
3. State the ONE system or feature you're working on. Never "build the game."
4. When done, update the Bible if a decision changed.

See `Docs/OpenQuestions.md` for the things we deliberately haven't decided yet — those get answered by the prototype, not by guessing.
