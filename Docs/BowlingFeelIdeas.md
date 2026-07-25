# Bowling Feel — Idea Parking Lot

> ⚠️ **STATUS: PARKED IDEAS, NOT PLAN** (except where marked ✅ Built or ⭐ Promoted below).
> Most of this file is a brainstorm of *possible* "funny feel" directions. Do NOT treat a
> parked item as a decided feature. The only decided throw design is `GameBible.md` §8–§9;
> feel-knobs live in `OpenQuestions.md`. An idea graduates only when Tony explicitly promotes
> it — then it moves to the Bible/Roadmap and gets logged. Until then: thoughts, not commitments.

## The one hard constraint (applies to anything built from here)
Everything funny must derive **deterministically** from `LaunchParameters` (`LateralPosition01`,
`AngleDegrees`, `Power01`, `Spin`, `Seed`, `TimingError01`) + config. No live `Random`, no
`Time`-based noise, no live wind — all replay-side variation from a local `System.Random(Seed)`.
AND: Unity PhysX is **not** guaranteed identical across machines, so the **host is authoritative
on the outcome** (final pin state), clients snap at settle (`Networking.md`). Comedy may lean on
visual reproducibility; scoring must not depend on frame-perfect cross-machine agreement.

---

## ✅ Built — graduated out of the parking lot (see CHANGELOG for detail)
- **Hook + Cone** — timing-error curve force + seeded jitter, in `BowlingBall`. *(Direction rule now resolved — see below + `OpenQuestions.md`.)*
- **Wobbler** — `BallConfig` variant, seeded sinusoidal weave; in the sandbox switcher.
- **Body English** — `IThrowReactionActor` + greybox `CapsuleThrowReactionActor` (placeholder body; real rig swaps in later).
- **Cannonball / BouncyBall** — `BallConfig` feel variants.

## ⭐ Promoted — speced, not yet built
- **Nuke Shot** — Bible ruling in §9; full spec below; `/build-system` prompt is ready to run. Next prototype.

---

## ⭐ Nuke Shot — greybox prototype candidate (2026-07-23)
Ruling it depends on (powerups = sanctioned exception to §8's grounded rule) is in the Bible §9.

**Concept — high-risk/high-reward powerup ball. Must be released in the GREEN zone.**
- **Green hit:** ball flies straight up → locks onto the pins → rockets down → explosion (big pin scatter, ~guaranteed strike).
- **Miss (not green):** detonates in-hand at chest height → ends the player's turn.

**Why it fits:** peak Pillar 2 — an all-or-nothing bet-magnet moment the whole table reacts to.

**Fairness guards (keep it out of "skill = domination", §8):** earned / coin-bought, limited uses per game, punishing whiff, optionally a *tighter* green window than a normal throw.

**Network / determinism (easier than a normal roll):** green-or-not known at release (deterministic); up-lock-rocket is a *canned tween*; explosion applies radial force to pins with the **host authoritative on final pin state** (clients play VFX only). Variation via `System.Random(Seed)`.

**Boundary:** blast scatters PINS only — never damages lane/map, never spirals.

**Greybox prototype (zero assets):** hide ball → tween sphere up → beacon pause → tween down onto pins → `Physics.AddExplosionForce` + placeholder poof + screen shake. Miss → ball vanishes → poof at chest → end turn. Tests the ONE question: is the moment fun and tense?

**Open (park in `OpenQuestions.md` when built):** acquisition (earn / buy / pickup?), uses per game, nuke green-window width, blast radius.

---

## 🅿️ Still parked (untouched thoughts)

### A — Release fumbles (more §8 archetypes; hold until we've felt which meter end is funniest)
- **Chip** — big error in low-power zone → feeble hop + reduced speed, dribbles into a corner pin.
- **Overcook** — big error in high-power zone → short deterministic low-friction "skip" window, skids/rattles.
- **Whiff** — extreme meter edges only; authored bit, ball peels off near-sideways. Hand-placed, kept special.

### B — Ball "personalities" as `BallConfig` (future powerups)
- **BeachBall** — light, high drag, over-curves on any spin (chaos amplifier).
- **Drunk ball** — reuses the built **Wobbler**, amplitude scaled by the (future) drink meter. Cheap once that lands.

### C — Spin & curve comedy
- **Banana / spin-out** — high spin + mistime overshoots into a gutter-ward loop. *(May reach the gutter — resolved yes, see below.)*
- **Late-breaking curve** — spin force ramps in over distance: straight, then snaps near the pins (suspense + clip timing).

### D — Pin-interaction drama (the *watching* comedy — highest untapped clip value)
- **Teetering survivor** — glancing entry leaves a pin spinning; tips (or not) a beat later. Host-authoritative on result.
- **Funny splits** — bias glancing hits toward recognizable "oh no" pin configs rather than random.

### E — Feel & feedback (cheap, high payoff)
- **Reaction tiers** — crowd/announcer/SFX keyed to the classified outcome; the game heckles you.

### F — Modes (post-launch back-pocket, 2026-07-23 Tony)
- **Lane-vs-lane battle mode** — multiple lanes side by side (lane v lane v lane, 2v2s, etc.), with **powerups used offensively to mess with opponents** rather than just help yourself. Explicitly a *post-launch update* idea — attractive because it needs **no new map**: reuse the existing alley, just multiple active lanes. Synergy: pairs with allowing **cross-lane pin collisions** (pins can fly into a neighbor's lane) — which the current nuke work is already setting up. Park until after launch; do not scope into v1.

---

## Feel-calls — RESOLVED 2026-07-23 (Tony) → also logged in `OpenQuestions.md`
- **Hook direction:** **too HARD (over-powered release) → hooks RIGHT; too SOFT (under-powered) → hooks LEFT.** (Confirms opposite-directions, with the specific mapping. Verify the built code's `TimingError01` sign produces this; flip if not.)
- **Spin-outs may reach the gutter** — funnier wins over less-demoralizing. Cap not required.

Still open (decide by playing): good-zone width / how much a small miss hurts (`OpenQuestions.md`).
