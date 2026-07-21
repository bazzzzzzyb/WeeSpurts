# Wee Spurts — Game Bible

The single source of truth. When a decision is made, it's recorded here. When an AI session starts, it reads here. If code and Bible disagree, the Bible wins (or the Bible gets updated on purpose).

**Status:** Light / pre-prototype. Sections marked 🔒 are decided. Sections marked ❓ are deliberately open until the bowling prototype teaches us the answer (see `docs/OpenQuestions.md`).

---

## 1. Vision 🔒

Wee Spurts is an online party game where the *fun is the friction*. It borrows Wii Sports' instantly-readable sports but swaps clean family competition for chaos: exaggerated physics, taunting, side-bets on each other's throws, and a comedic "drinking" gimmick. You play it in a Steam lobby with friends, laughing at each other, not at the game.

**One-line pitch:** Wii Sports if it egged you on to gamble on your friends and heckle their form.

> **Naming note:** "Wee Spurts" is the game. "Friendslop" is the trending *genre* it belongs to (party games built around chaos + friends), not the title.

## 2. Core Pillars 🔒

Every feature must serve at least one. If it serves none, cut it.

1. **Chaos over precision.** Physics should surprise and amuse, not reward mastery. A perfect throw and a terrible one should both be funny.
2. **The table talk is the game.** Features exist to create reasons to yell, bet, gloat, and heckle. Downtime between turns is a feature, not dead air.
3. **Instantly readable.** A new player understands a minigame in 10 seconds. No manuals.
4. **Friends first.** Designed for a private lobby of people who know each other, not anonymous matchmaking.

## 3. Tone & content guardrails 🔒

- Gambling and drinking are **satirical and fake** — fake currency, cartoon exaggeration. No real money, no real-money-adjacent mechanics.
- Keep it clearly comedic to stay on the right side of Steam's rules and age ratings. Revisit before store submission (see `docs/OpenQuestions.md`).

## 4. Tech Stack 🔒

| Layer | Choice |
|---|---|
| Engine | Unity (latest LTS) |
| Language | C# |
| Netcode | Mirror + FizzySteamworks (transport over Steam relay) |
| Steam API | Facepunch.Steamworks (lobbies, friends, invites, achievements) |
| Dev App ID | 480 (Spacewar) until we buy Steam Direct |
| Version control | GitHub + Git LFS |
| Art direction | Low-poly / stylized |
| Free assets | Kenney.nl, Unity Asset Store (free), Mixamo |
| AI 2D | Any image generator (UI, textures, concepts) |
| AI 3D | Meshy/Tripo/etc. for filler props only |
| AI audio | ElevenLabs + SFX generators |

Rationale for each choice lives in `docs/Networking.md` (netcode) and the root roadmap. Don't re-litigate these mid-build; change them only by editing this table on purpose.

## 5. Systems overview 🔒

Build order is a dependency graph, not a calendar. See `docs/Roadmap.md` for detail and `docs/DefinitionOfDone.md` for exit criteria per system.

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

## 6. Folder & project conventions 🔒

See `docs/CodingStandards.md`. Summary: the Unity project lives in `/Unity`; docs are law; every AI session is scoped to ONE system.

## 7. What makes Wee Spurts *different* ❓

This is the most important question and it CANNOT be answered on paper. The honest hypothesis: the differentiator is the "table talk" loop — betting on and heckling each other between turns — layered on chaotic physics. The prototype exists to prove or kill that hypothesis. Do not lock cosmetics, progression, or a marketing hook until a bowling game with the slop layer has actually made you and Braeden laugh. Tracked in `docs/OpenQuestions.md`.

---

## Change log
- 2026-07-21 — Bible created, light/pre-prototype version.
