# Wee Spurts — Game Bible

The single source of truth. When a decision is made, it's recorded here. Claude Code reads it automatically via `CLAUDE.md`. If code and Bible disagree, the Bible wins (or the Bible gets updated on purpose).

**Status:** Light / pre-prototype. Sections marked 🔒 are decided. Sections marked ❓ are deliberately open until the bowling prototype teaches us the answer (see `OpenQuestions.md`).

---

## 1. Vision 🔒

Wee Spurts is an online party game where the *fun is the friction*. It borrows Wii Sports' instantly-readable sports but swaps clean family competition for chaos: exaggerated physics, taunting, side-bets on each other's throws, and a comedic "drinking" gimmick. You play it in a Steam lobby with friends, laughing at each other, not at the game. Think Lethal Company, Meccha Chameleon, Thief Simulator, etc.

**One-line pitch:** Wii Sports if it you were able to throw a bowling ball at your friend as he's about to throw a strike, then you can go over to the bar, grab a beer and hit some slots in the corner. 

> **Naming note:** "Wee Spurts" is the game. "Friendslop" is the trending *genre* it belongs to (party games built around chaos + friends), not the title.

## 2. Core Pillars 🔒

Every feature must serve at least one. If it serves none, cut it.

1. **Chaos over precision.** Physics should surprise and amuse, not reward mastery. A perfect throw and a terrible one should both be funny. The games themselves should be fun and have replayability, not just getting bored in 30 seconds after you beat up your friend. 
2. **The table talk is the game.** Features exist to create reasons to yell, bet, gloat, and heckle. Downtime between turns is a feature, not dead air.
3. **Instantly readable.** A new player understands a minigame in 10 seconds. No manuals.
4. **Friends first.** Designed for a private lobby of people who know each other, not anonymous matchmaking.

## 3. Tone & content guardrails 🔒

- While gambling and drinking are present, we don't want to have to rate ourselves on Steam due to adult content and get screwed over on sales. Keep it funny but somewhat clean if possible?

## 4. Tech Stack 🔒

| Layer | Choice |
|---|---|
| Engine | Unity (latest LTS) |
| Language | C# |
| Netcode | Mirror + FizzyFacepunch (transport over Steam relay) |
| Steam API | Facepunch.Steamworks (lobbies, friends, invites, achievements) |
| Dev App ID | 480 (Spacewar) until we buy Steam Direct |
| Version control | GitHub + Git LFS |
| Art direction | Low-poly / stylized |
| Free assets | Kenney.nl, Unity Asset Store (free), Mixamo |
| AI 2D | Any image generator (UI, textures, concepts) |
| AI 3D | Meshy/Tripo/etc. for filler props only |
| AI audio | ElevenLabs + SFX generators |

Rationale for each choice lives in `Networking.md` (netcode) and the root roadmap. Don't re-litigate these mid-build; change them only by editing this table on purpose.

## 5. Systems overview 🔒

Build order is a dependency graph, not a calendar. See `Roadmap.md` for detail and `DefinitionOfDone.md` for exit criteria per system.

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

See `CodingStandards.md`. Summary: the Unity project lives in `/Unity`; docs are law; every AI session is scoped to ONE system.

## 7. What makes Wee Spurts *different* ❓

This is the most important question and it CANNOT be answered on paper. The honest hypothesis: the differentiator is the "table talk" loop — betting on and heckling each other between turns — layered on chaotic physics. The prototype exists to prove or kill that hypothesis. Do not lock cosmetics, progression, or a marketing hook until a bowling game with the slop layer has actually made you and Braeden laugh. Tracked in `OpenQuestions.md`.

> **Candidate answer (July 2026, from `BLUEPRINT.md` research):** make the alley a *walkable social space* — spectators physically present, heckling from the gutter, bar + slots in the corner, comedic interference with the active thrower. It unifies all three pillars, fixes turn-based downtime, and no comp owns it. Test it in the first prototype; promote to a pillar if it's as fun as it sounds.

## 8. The throw — core bowling mechanic 🔒

The timing-based power meter is the heart of bowling and the concrete expression of Pillar 1. Timing well vs. poorly changes *what kind of throw* you get — not just how hard:

- **Good timing = control.** The ball goes exactly where aimed, with the spin you dialed. Clean, predictable, satisfying. This is the competitive hook that makes players keep trying to master it.
- **Bad timing = human fumble.** A mistimed release makes *the thrower* screw up — the ball hooks, chips, overcooks, or peels off. It always stays a believable bowling ball thrown badly by a person: never supernatural, never world-breaking. The miss must read as the player's incompetence, not the game malfunctioning.
- **Worse is funnier — non-linearly.** Small misses stay mostly fine; big whiffs go spectacular. Spectacle is reserved for genuine screwups. The extreme ends of the meter trigger a few *named* failure bits (Hook, Chip, Overcook, Whiff) with clear telegraphs, so the table instantly reads and heckles them (Pillar 2).
- **Skill ≠ domination.** Mastery buys consistency and style, not a runaway scoreboard. Inherent pin scatter plus the betting/heckling/drink layers keep any thrower from quietly running away with it, so *everyone's* throw stays watchable and funny.

Guiding line: a bad shot should feel like the best possible outcome of a screwup, not a punishment — **failure is content.** Full design detail + the open feel-knobs (good-zone width, gutter-vs-pin bias) live in `OpenQuestions.md`.

## 9. Powerups & special shots 🔒 (direction)

Wee Spurts leans **arcade, not sim** — closer to a "super battle golf" than to realistic bowling. The design runs **two rulebooks on purpose:**

- **Base throw = grounded (§8).** The default ball is a real bowling ball thrown by a person; comedy comes from human fumbles, never supernatural behavior (that would read as a bug).
- **Powerups = sanctioned cartoon logic.** Special shots and powerup balls MAY break physical realism — homing, explosions, wild trajectories — *because they're obviously special items*, clearly telegraphed, not the base ball misbehaving. This is where spectacle and the biggest bet-and-heckle moments live (Pillar 2).

Guardrails so powerups don't wreck balance:
- Powerups are **earned / limited**, never the default. Scarcity + risk offset their power, so §8's "skill = consistency, not domination" still holds.
- Spectacle stays **contained**: it affects the pins / the shot, never permanently damages the lane/map and never spirals — "no map collapsing" still applies.

First candidate: the **Nuke Shot** (green-zone-only; detonates on the pins if nailed, in-hand if missed). Speced in `BowlingFeelIdeas.md`; greybox prototype pending.

---

## Change log
- _(date)_ — Bible created, light/pre-prototype version.
- 2026-07-21 — Repo restructured for Claude Code (`CLAUDE.md`, `.claude/` agents & commands). Chat-paste personas retired to `Docs/archive/`. `BLUEPRINT.md` added (market research, phases, gates). `Marketing.md` added. Walkable-alley hypothesis logged in §7 and `OpenQuestions.md`.
- 2026-07-21 (later) — `PLAYBOOK.md` added: full AI/human task ledger. Phase 0+1 code written and staged in `_Staging/` (core framework, unit-tested scorer, ball/pins/input/camera, one-click greybox scene builder). Transport corrected: FizzySteamworks → **FizzyFacepunch** (must match Facepunch.Steamworks).
- 2026-07-21 (later still) — Tony decided: characters = **bean-people ragdolls**; world = **bright Wii-clean**. `Docs/ContentPlan.md` added: full asset inventory (all free/CC0 sources named), UI kit + fonts, audio plan, art-pass schedule.
- 2026-07-21 (revision) — Characters revised on Tony's call: **Phasmo-style low-poly humanoids** (free rigged placeholders + Mixamo; own AI models swap in later); beans parked as fallback. `Docs/AssetWorkbench.md` added: Tony's hands-on AI asset guide (prompt templates, pipelines, priority queue).
- 2026-07-22 — Bugfix, not a design decision: `GreyboxSceneBuilder.cs` ball bounce material now sets `bounceCombine = Maximum` so a ball's own `BallConfig.Bounciness` always wins over the lane floor's, instead of averaging with it. Fixes bouncy-ball feel testing (ball was "slamming down" even at high Bounciness) and means future ball-feel power-up variants (bouncyball, cannonball, etc.) never need a matching floor material.
- 2026-07-22 (later) — Bugfix, not a design decision: `BallConfig.Bounciness` wasn't reaching the ball at all when swapped via the sandbox switcher — `GreyboxSceneBuilder` baked bounciness onto the ball's physic material once, at scene-build time, from whichever `BallConfig` was default then. `BowlingBall.Launch()` now restamps the collider's physic-material bounciness from the active config on every throw. Also seeded the switcher with `BouncyBall`/`Cannonball` automatically on scene build, so they no longer need manual Inspector wiring.
- 2026-07-23 (design direction) — Vision nudged toward **arcade "super battle golf"** feel (Tony's call): grounded skill-based base throw (§8) + an over-the-top **powerup layer** as a *sanctioned exception* to §8's realism rule. New §9 records the two-rulebook split and its guardrails (powerups earned/limited; spectacle contained, no map damage). First powerup candidate — the **Nuke Shot** — parked/speced in `BowlingFeelIdeas.md` as a greybox prototype candidate.
- 2026-07-22 (design decision) — **Throw mechanic locked** (new §8). Miss-timing = *grounded human-fumble chaos*, not floppiness or world-breaking: bad timing distorts direction/spin (funny axes), good timing stays clean/predictable (the competitive hook). Escalation is **non-linear** — small misses mild, big whiffs spectacular — with named failure archetypes (Hook/Chip/Overcook/Whiff) hand-placed at the meter extremes. Skill = consistency/style, not domination (pin scatter + slop layers keep it level). Feel-knobs (good-zone width, gutter-vs-pin bias) stay open in `OpenQuestions.md`. Refined from an earlier Sonnet draft of the same idea.
- 2026-07-22 (later still) — Sandbox dev tool, not a design decision: added a quick-reset-frame debug key (`F`) so Tony can retry one throw setup repeatedly instead of playing through pin counting every time. `BowlingScorer.ResetCurrentFrame()` discards only the current frame's rolls (earlier frames + `IsGameOver` untouched); QA-reviewed (qa-engineer agent) for scoring-integrity edge cases across strikes/spares/10th-frame bonus rolls — no bugs found, all traced cases correct. QA flagged one pre-networking item: sandbox keys (`F`, `R`, `1`-`9`) mutate `BowlingGameController`/`BowlingScorer` with no host-authority check, which is fine now (not networked) but must be gated before Mirror lands on this class.
- 2026-07-22 (later still) — Sandbox dev tool, not a design decision: added an AIM-phase visual preview (new `AimPreview.cs`) — the ball now visibly slides with lateral aim input and a curved line previews direction + spin, purely cosmetic, verified to never touch `LaunchParameters` or the resolved throw. Confirms §8's mechanic isn't implemented in code yet: `BallLauncher` currently gives direct, undistorted player control over angle/spin with no timing-driven distortion and no consumption of `LaunchParameters.Seed` — that's tracked as future work, not a doc/code contradiction. QA-reviewed (qa-engineer agent): caught and fixed a real bug pre-merge (`AimPreview`'s editor-wired references weren't `[SerializeField]`, so they'd go null on scene reload) plus a `DebugHud` power-bar-stuck-on-screen regression introduced in the same session.
- 2026-07-25 (presentation decision + sandbox prototype) — **Throw camera is now a scripted six-beat cinematic move** (`ThrowCameraSequence`), replacing the old two-mode aim/chase camera as the default. Wii Sports read: "you're up" front-on beat → swing behind into the over-the-shoulder aim framing → a slow push-in on the thrower while the power meter charges → whip out low over the lane at release → travel with the ball → arrive at the pins as it hits. Two calls Tony made by playing, both recorded here because they overrode the original spec: (1) the front-on "you're up" beat is a **turn-start hold only** and must NOT be the aim framing — you cannot aim while looking at your own face, and `AimPreview`'s guide line would point at the camera; the aim phase keeps the existing over-the-shoulder view as its basis. (2) The charge beat **pushes IN slowly toward the character** rather than pulling back to a wide shot, and the old separate "swing through" beat was deleted into it — one slow creep that then waits reads better than two beats. Camera travel down the lane is capped at ~3/4 (`TravelCapLane01`) so the ball runs the last stretch alone, as the Wii does; documented trade is that while the cap binds it overrides the lanes-in-frame framing solve. Presentation only — never touches `LaunchParameters`, physics or scoring — and the Nuke Shot keeps its own camera outright. Does NOT foreclose `OpenQuestions.md`'s walkable-alley question (Tony: a walkable space would cut to this cinematic for the throw itself, per the drag-to-the-lane idea at `OpenQuestions.md:22`).
- 2026-07-25 (bugfix, not a design decision) — Character setup fixed in the GENERATOR (`CharacterSetupTool`), not in hand-edited asset state, so a reimport can't reintroduce either bug. (1) The thrower clapped forever because **a Mixamo FBX is not one animation** — each carries twelve takes (the Quaternius set first, the downloaded motion last as `mixamo.com`), and the tool renamed all twelve to one name then picked "the first clip in the file". Every state in the controller was bound to `Man_Clapping`; no Mixamo clip had ever played. Now only the `mixamo.com` take is imported, and states match clips by name. (2) The model rendered stretched because its Scale Factor (0.4) no longer matched its clips' (1) — under a Generic rig clips drive absolute bone positions in the clip's units, so the skeleton was pulled apart. Scale Factor is now pinned identical across all fourteen FBXs, and display size is a single uniform scale on the prefab root (`CharacterDisplayScale`, default 0.4). **Rig type re-examined and deliberately left Generic:** these Quaternius bodies export a Blender IK control rig (`Foot.L`/`Foot.R` are root-parented IK targets with `PoleTarget` siblings, leg chain dead-ends at `LowerLeg_end`), so Humanoid cannot validate — confirming `OpenQuestions.md`'s existing entry and Tony's option (a). Mixamo retargeted server-side at export, so Generic plays the clips correctly. Presentation/setup only — `LaunchParameters`, physics and scoring untouched, and no camera value changed. Deviation noted: the 0.4 knob is a documented `const` on the editor tool rather than a ScriptableObject, because it is consumed at prefab-generation time and could not be a runtime tweak either way (Tony's call).
- 2026-07-25 (scoreboard decision) — **The player-facing score is now the LIVE score, not the resolved score.** `BowlingScorer.GetProvisionalTotal()` added (purely additive; `AddRoll`/`GetTotal`/`GetFrameTotals` untouched) and `DebugHud`'s `TOTAL` switched to it. Standard ten-pin bookkeeping cannot total a strike's frame until its two bonus rolls exist, so the headline number sat at `0` for two turns after a strike — correct on paper, unreadable in a party game, and glaring with a guaranteed-clear Nuke. The per-frame boxes still show formal resolved totals (blank until resolved), so the scorecard itself stays honest; only the headline number went live. Verified against the official total on the standard reference games and across 200,000 randomised games (never decreased, never exceeded, always converged exactly). Not a rules change — a display decision.
- 2026-07-25 (design decision, §8) — **Spin became a 2D selector, and spin never costs power.** `LaunchParameters.Spin` went from one float to a `Vector2` clamped inside the unit circle: **X = side spin (the hook), Y = roll vs skid** — topspin grips and curves EARLY (straighter overall, slight forward drive), backspin SKIDS then breaks LATE and harder. Two calls recorded here because they're deliberate deviations. (1) **Off-centre placement does NOT reduce power**, unlike pool: power stays entirely on the timing meter, because two systems fighting over speed muddies the one thing §8 says the player is meant to master. (2) **The mistiming Hook survives intact and is ADDITIVE with player spin** — separate forces, separate config knobs (`SpinCurveForce` vs `HookForceMagnitude`), neither scaling the other, so a hard release still slices right even when the player dialled left. That tension is the point: the fumble fights the intent rather than replacing it, which keeps §8's "a miss reads as the player's incompetence" true even for a player who has mastered spin. Schema change made now on purpose — networking doesn't exist yet, so this is the cheapest moment it will ever be. Feel-knobs (`RollSkidHookScale`, `SpinRampDistance`, and the spin-vs-Hook balance) are Inspector-tunable and stay open. QA-reviewed (qa-engineer agent): both spin-math invariants independently re-derived, determinism and the seeded RNG stream confirmed intact, no hallucinated APIs; five defects found and fixed pre-merge (a URP transparency knob that silently did nothing, a stuck mouse-drag that could apply spin the player never dialled, and three generator bugs in the character material path). **One open item deliberately NOT decided here** — the release fumble's `ConeSpinJitterMagnitude` kick is folded into the player's spin vector, so dialling topspin damps most of it and backspin roughly triples it; whether a player should be able to partly dial away their own fumble is Tony's call, logged in `OpenQuestions.md`.
