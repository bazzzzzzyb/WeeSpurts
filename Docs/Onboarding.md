# Onboarding — Braeden

Written for you AND your Claude. You run this project through Claude Code exactly like Tony does: your Claude reads `CLAUDE.md` automatically the moment you run `claude` in the repo, and it handles the *how* (Unity, Git, C#). This doc is the *what and why*. Read it once; your Claude keeps it as context.

## 1. The project

Wee Spurts: online-only Steam party game — Wii Sports bowling readability with a friendslop chaos layer (fake-coin betting, heckling, a satirical drink meter, a walkable alley with a bar and casino corner). Two-person team + Claude Code as the engineering staff. The working hypothesis for what makes it *different*: the between-turn social loop — betting, heckling, physically wandering a venue with your friends — layered on grounded human-fumble throw chaos (Bible §8) plus sanctioned cartoon powerups (§9, the Nuke).

Source of truth chain: **`CLAUDE.md`** (entry point, golden rules) → **`Docs/GameBible.md`** (locked decisions; if code and Bible disagree, the Bible wins) → `Docs/OpenQuestions.md` (deliberately undecided; Tony owns the calls) → `CHANGELOG.md` (what happened, in obsessive detail — genuinely worth skimming; it's the project's memory). `BLUEPRINT.md`/`PLAYBOOK.md` are the strategy and task ledger.

Current state (2026-07-26): a complete single-machine hot-seat bowling game — 10-frame scoring (72 green EditMode tests), timing-meter throw with green zone, 2D spin selector, Hook/fumble chaos, Nuke Shot powerup, a six-beat Wii-style throw cinematic, a rigged Quaternius character with Mixamo clips, first-person roaming through a full greybox alley venue, and a diegetic match start at the lane kiosk. No networking yet — that's the next front, and it's why you're here.

## 2. Environment setup (exact — deviations poison diffs)

1. **Unity Hub, then Unity `6000.5.4f1` — EXACTLY.** Hub → Installs → Install Editor → Archive tab if it's not listed. Any other version re-serialises `.asset`/`.unity` files on open and turns every future diff into noise. Check against `Unity/ProjectSettings/ProjectVersion.txt` if in doubt. Windows Build Support module on.
2. **Git + Git LFS, LFS before cloning**: install both → `git lfs install` → then `git clone https://github.com/bazzzzzzyb/WeeSpurts.git`. (LFS after the fact = broken binary pointers.)
3. **Claude Code**: install from code.claude.com → terminal → `cd` into the repo → `claude`.
4. Unity Hub → **Add** → select the repo's `Unity/` folder → open. First import takes minutes.
5. **Verify, don't assume** (project settings travel with the repo, so these should already be right on a fresh clone — check, and only fix if wrong):
   - Edit → Project Settings → Tags and Layers → **User Layer 6 = `LocalPlayerModel`** (exact string; roaming hides your own body via camera culling on this layer).
   - Project Settings → Player → Other Settings → Active Input Handling = **Both**.
6. **Prove it works**: Window → General → Test Runner → EditMode → Run All → **all green** (72). Then open `Assets/_Project/Scenes/TestVenue.unity` → Play. A working Play: you're in first person in the greybox alley, WASD+mouse works, you walk to a lane's console screen, an `[E] Start Game` prompt appears, E starts the match, the camera swings into the "you're up" cinematic and hands you the aim phase.
   - For pure throw feel-testing, `BowlingAlley.unity` skips the walking: match auto-starts at the line (`sandboxAutoStart`).
7. Steam installed + running (matters from the networking work on; App ID 480 "Spacewar" is the dev stand-in).

## 3. Controls (read out of the code, current as of 2026-07-26)

**Roaming** (`FirstPersonController`, `PlayerInteractor`): WASD/arrows move, mouse look, Left Shift sprint, Space jump, **E** interact (prompt shows when in range).

**Bowling — AIM phase** (`BallLauncher`, `SpinSelectorHud`): **←/→ (or A/D)** slide across the lane (character slides with you; briefly locked at turn start if configured, while the camera faces you). **Shift + ←/→** aim angle. **Spin**: drag the dot on the circular ball widget (left mouse), or **I/J/K/L** nudge, **C** recenter — X = hook, Y = topspin (early curve, forgiving) vs backspin (late hard break). **Hold SPACE**: power rises once 0→100% and caps — no ping-pong. Release inside the **green band** (~80–85%; Nuke's is tighter, 82–85%) for a clean throw; miss it and the Hook/jitter fumble scales non-linearly with your error (hard release hooks RIGHT, soft hooks LEFT). Release under 5% = the backward-fumble gag.

**Sandbox keys** (`BallConfigSwitcher`, `BowlingGameController`): **1–5** pick the next throw's ball — 1 default, 2 BouncyBall, 3 Cannonball, 4 Wobbler, 5 Nuke (HUD shows `[NUKE ARMED]`). **F** re-racks and replays the current frame (mid-match only). **R** rematch after the match ends. These keys are sandbox-only and will be authority-gated before networking.

## 4. How we work

The golden rules in `CLAUDE.md` are law for your Claude too, the short version: one system per session; small diffs; no invented APIs (your Claude verifies Unity/Mirror/Facepunch methods against official docs — neither of us can catch hallucinations, so it must); plan before code; every change ends with "press Play and expect X"; never stack untested changes; decisions go in the Bible with a change-log line, session history goes in `CHANGELOG.md` under `[Unreleased]`.

Both of us drive the same specialist agents in `.claude/agents/` — gameplay-engineer, steam-engineer, ui-engineer, physics-tech-artist, qa-engineer — and the same commands: `/build-system <one thing>` to start a scoped session, `/qa-review` after ANY change touching scoring, networking, or coins. QA has caught real defects before merge in almost every session so far; don't skip it.

## 5. The two-person working agreement

- **Ownership is by SYSTEM, not by scene.** Tony: gameplay, throw feel, physics, camera. You: playtesting + config tuning now, growing into environment/props.
- **Prefabs are the handoff format.** Both of us do art. One owner per prefab at a time; build in your own sandbox scene, hand off the finished prefab.
- **One owner per scene at a time, announced in chat.** Scene YAML does not merge — a conflict means someone's work is lost. (This is the #1 two-person Unity killer; take it seriously.)
- **Physics tuning without collisions:** duplicate the config asset (`PinConfig_BraedenTest.asset`, `BallConfig_BraedenTest.asset`), tune YOUR copy, and merge only winning NUMBERS back into the real asset — same pattern the ball switcher already uses. Add your ball copies to `BallConfigSwitcher`'s list in the Inspector to A/B them live. Note: ball configs apply on the next throw, but PIN physics is partly baked into the scene at build time — after editing a pin config, re-run the scene builder (or ask your Claude which fields need it). Physics CODE changes are a feature branch, one person at a time.
- **Feature branches, merge often.** Long-lived branches diverge and get dangerous.
- **Experiment on a branch, not a separate project.** A branch can be wrecked and deleted with zero risk to `main`, and anything good merges back. If you want a totally throwaway playground to learn Unity in, a separate duplicate project is fine — but it stays COMPLETELY separate; never copy files from it back in.
- **Third-party asset packs never enter the repo** — import them to `Unity/Assets/ThirdParty/` (gitignored) and share via Drive. Everything that IS committed gets a license row in `Assets/README.md` first.

## 6. Your first task: playtest

Highest-value work in the project right now, and it needs zero code. Play `BowlingAlley.unity` and `TestVenue.unity` hard, tune your duplicated configs, and write findings to `Docs/Playtests/<date>-braeden.md` (create the folder). These `OpenQuestions.md` items are explicitly blocked on someone playing:

- **The spin-jitter question** (Physics feel → "dial away your own fumble?"): throw five topspin and five backspin balls with deliberately early releases — does damping your own fumble with topspin feel like a skill-leak or a fair trade? This is the top open feel call.
- **Good-zone width** — is 80–85% forgiving enough to keep hope alive, tight enough to be a skill?
- **Miss bias** — do fumbles feel gutter-ward (funny but demoralizing) or pin-ward (hopeful)?
- **The venue on foot**: does walking the alley feel fun or like a commute? Is the concourse generous or mean (it measures 3.75m vs a 4.0m target)? Are the settee pits sittable-looking or cramped (the audit says cramped; Tony wants a human verdict)? Can you get in AND out of the card-dealer alcove squeeze?
- **Does the throw cinematic feel right from the venue**, or redundant once you've walked up to the lane yourself?

Format per finding: what you did → what happened → verdict/number you'd try. Tony makes the calls; your job is evidence.

## 7. Where things live

`Docs/` — law + plans (Bible, this file, OpenQuestions, system docs). `CHANGELOG.md` — history. `Unity/Assets/_Project/` — everything ours: `Scripts/<System>/` (Bowling, Gameplay, Player, Interaction, Core, UI, Environment, Editor — one folder per system), `ScriptableObjects/` (all tunables — your playground), `Scenes/`, `Tests/EditMode/`. `Assets/` (repo root) — source art + the license log. `.claude/` — agents and commands, same for both of us. WeeSpurts menu in Unity — the one-click builders (greybox scene, alley venue, character setup, physics retune).
