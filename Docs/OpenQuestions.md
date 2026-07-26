# Open Questions

Deliberately undecided. Answering these on paper *before* the prototype would be guessing. The bowling prototype + slop layer will answer most of them. Tony owns the final calls (creative director). Revisit at the "first funny game" gate.

## Players
- Max players per lobby? 2 / 4 / 8 / 12? (Turn-based bowling scales cheaply; UI & pacing don't.)
- Friends-only, invite-code, or public matchmaking? (Leaning friends-first.)
- Voice chat in-game, or "just use Discord"?

## Characters — ANSWERED 2026-07-21 → moved to `ArtGuide.md` + `ContentPlan.md`
- ~~Miis? Animals? Ragdoll humans? Potatoes?~~ → **Phasmo-style low-poly humanoids** (revised same day from beans; beans parked as fallback).
- Customization v1: bean color + hats. Depth beyond that: still open, decide at Progression (Roadmap [7]).
- **Blocked by the placeholder rig (2026-07-25):** the Quaternius Universal Base Characters export a Blender **IK control rig**, not a clean deform skeleton — `Foot.L`/`Foot.R` are parented to the root as IK targets rather than to `LowerLeg`, `UpperLeg` hangs off `Body` instead of `Hips`, plus `PoleTarget` bones. Unity's Humanoid rig validates the *hierarchy*, so the import hard-fails (`Required human bone 'LeftFoot' not found`) and no bone mapping can fix it. **The placeholder therefore imports as Generic**, which works fine today (Mixamo pre-retargeted the clips onto this exact skeleton, all bones baked) but forfeits Humanoid-only features — Animator **IK**, foot IK, humanoid avatar masks — and the `ContentPlan.md` §2 / `AssetWorkbench.md` §2 promise that *any* rigged humanoid drops into the same slot with zero code changes. **Tony's call, three options:** (a) live with Generic for the placeholder and get Humanoid free once your own Mixamo auto-rigged characters arrive (auto-rig emits a proper FK hierarchy) — cheapest, and the placeholder is disposable anyway; (b) re-parent the feet in Blender and re-export the one body we actually use — ~15 min of rig surgery, but `ContentPlan.md` §"not doing" explicitly parks Blender work beyond cleanup; (c) find a different CC0 base mesh with a clean FK rig. **Leaning (a).** Note this also bears on the ragdoll plan — `PLAYBOOK.md` wants "ragdoll from the rig", and ragdolls are built from colliders/joints on bones, which works on Generic too, so ragdolls are *not* blocked by this.
- **Refinement (2026-07-22, Tony):** reference point is **PEAK** and **Super Battle Golf** — very *expressive* humanoid characters, reactive faces and exaggerated body language selling the moment (fear on the drag, triumph on a strike, etc.), not just janky-but-blank low-poly. Consistent with §8's "grounded human-fumble chaos" call (not Gang Beasts floppiness) — this is about expressiveness, not looseness. Practical effect: raises the priority of `AssetWorkbench.md` §3's face/expression sheet, currently parked "until the character look settles" — worth revisiting once the Quaternius placeholder + Mixamo clips are in and you can judge whether the base rig can sell this or needs a custom face rig sooner than planned. Not yet promoted to `ArtGuide.md` — confirm by feel on the placeholder first.

## Lobby / menu flow
- **Idea (2026-07-22, Tony):** the lobby isn't a menu screen, it's a scene — players sit at a bar drinking. The host character asks "anyone wanna bowl?" and each player gets a Yes/No popup. If No wins, host just goes "...huh, okay" and everyone keeps sitting there until he asks again. Solo lobby: host still asks, sighs, gets up, and the game loads anyway. Directly literalizes UI.md principle 3 ("the lobby is a room, not a menu") and gives the host character a personality beat instead of a plain "Ready Up" button. Scope note: this is real production work (bar scene, sitting/drinking idle, a reactive host) — belongs at Menu/Lobby UI (Roadmap [5]), not now.

## The alley as a place
- Is the bowling alley a *walkable social space* (spectators wander, heckle up close, bar + slots in the corner, comedic interference) rather than a fixed-camera lane? `BLUEPRINT.md` Push 1 says test this in the first prototype — it may be the differentiator.
- If yes: how much interference is funny vs. infuriating? (Cooldowns? Coin costs to interfere?)
- **Idea (2026-07-22, Tony):** turn transition as a visual beat — between turns your character walks around freely (social space), and when your turn comes up you get slowly dragged/pulled to the front and snapped into the Wii-bowling stance to throw. Turns downtime into a readable "you're up" moment instead of a menu/UI cue. Untested — try it once the walkable-alley prototype exists.
- **Refined (2026-07-22, Tony) — the game-start trigger + the drag played for comedy:** game start isn't a lobby-menu button, it's diegetic — players free-roam the alley, walk up to a lane, and type their name in at the lane itself (like real life) to start the match. Free-roam continues for everyone even mid-game; only the active player gets pulled. On your turn, you're dragged back **against your will** — character's face shifts to comic fear as it happens — all the way to the starting line, ball already in hand. Combines with the entry above (walk-freely / get-dragged-on-your-turn) but adds: (1) the actual game-start moment, (2) the drag is not a neutral cutscene, it's a bit — the character visibly doesn't want to go. Needs a face-expression system (see `AssetWorkbench.md` §3 face/expression sheet, currently parked "until the character look settles" — this idea is a reason to prioritize it sooner).

## Physics feel — ANSWERED 2026-07-22 (Tony) → decision promoted to `GameBible.md` §8
- ~~Realistic-ish, or full Gang Beasts / Human: Fall Flat floppiness?~~ → **Neither. Grounded human-fumble chaos.** The ball always behaves like a real bowling ball; a bad power-meter release means *the thrower* fumbled it, not that physics broke. No map-collapse, no supernatural ball behavior — that's explicitly out of bounds.
- **The mechanic:** timing error opens a *second dimension* on the throw. It distorts **direction** and **spin** (the funny, spatial axes), nudges **speed** a little, and never touches physics integrity. Good timing collapses that dimension to zero — the ball goes exactly where aimed. Pillar 1 ("chaos over precision") lives *inside* this mechanic, not against it: precision is the thing you master, chaos is what missing produces.
- **Escalation curve (Tony's key call):** **non-linear, not proportional.** A *small* miss = mild wobble (stays mostly fine — keeps hope alive, no punishing fail-state). A *big* whiff = spectacle. Reserve the crazy stuff for genuine screwups so "worse is funnier" actually lands and stays special.
- **Failure archetypes at the extremes:** the red zones trigger a few *named, recognizable* disasters, each with its own telegraph + sound so the table reads it instantly — e.g. the **Hook** (savage gutter-ward curve), the **Chip** (feeble glancing dribble into a corner pin), the **Overcook** (too-hard skid/rattle), the **Whiff** (release fumbles, ball peels off near-sideways). Legible failure = shareable table-talk (Pillar 2 — friends will start yelling "he Chipped it again"). Give each archetype internal variance so the *category* is legible but the *instance* still surprises.
- **Legibility rule:** a miss must read as the *player's* incompetence, NOT the game malfunctioning. "You released early and hooked it" is funny and ownable; "the ball randomly did something" reads as a bug. Author the chaos into recognizable human failures.
- **Domination guard:** skill buys **consistency + style, not scoreboard domination.** A clean throw = the throw you *intended*, not a guaranteed strike — the pins' own scatter (`PinConfig`) keeps honest variance in every throw. The betting/heckling/drink layers are what actually swing outcomes, so a precise player is a pleasure to watch, not a tyrant. (Optional, untested spice: the leader gets a slightly faster/narrower good-zone — more to lose, more comedy.)

**Resolved 2026-07-23 (Tony):**
- **Hook direction by timing:** too **HARD** (over-powered release) → hooks **RIGHT**; too **SOFT** (under-powered release) → hooks **LEFT**. Confirms early-vs-late hook *opposite* ways, with this specific mapping. (Impl: verify the built `TimingError01` sign in `BowlingBall` produces right-on-hard / left-on-soft; flip if reversed.)
- **Spin-outs may reach the gutter** — funnier beats less-demoralizing; no need to cap them short.

**Still open (feel calls — decide by playing, then log here):**
- **Should a player be able to dial away part of their own fumble? (raised 2026-07-25 by QA, needs a play test.)** The mistiming Hook is fully separate and always lands — that part is safe. But the *other* half of the fumble, the one-off `ConeSpinJitterMagnitude` side-kick baked in at release, is currently folded into the player's spin vector, so it gets shaped by the ramp the player dialled. Measured at the defaults (`SpinCurveForce` 6, `RollSkidHookScale` 0.6, full jitter, at the pins): **topspin 0.03 N, neutral 1.80 N, backspin 5.31 N**. So parking the dot at the top of the widget damps that kick to ~2% of neutral, and parking it at the bottom roughly triples it. Two readings, both defensible: (a) **it's a skill leak** — §8 says a bad release must read as the thrower's incompetence and shouldn't be dialable away; fix is to apply the jitter as its own unshaped sideways force next to the Hook; or (b) **it's a real risk/reward trade** — topspin buys forgiveness on timing at the cost of hook, backspin buys the dramatic late break at the cost of punishing your mistakes, which is a genuinely interesting choice and arguably better design than the "fix". **Decide by playing it** (throw five topspin and five backspin balls with deliberately early releases and see which feels right), then log the call here and correct the "neither force scales the other" comment in `BowlingBall.FixedUpdate` if (b) wins.
- **Good-zone width / how much a small miss hurts.** Too tight = punishing, people tune out; too loose = no skill to it. Start forgiving.
- **Miss bias: gutter-ward vs pin-ward.** Gutter-biased is funnier but more demoralizing; pin-biased keeps hope alive. Tune live.

**Implementation note (when built):** derive the wobble from `LaunchParameters.Seed` (deterministic — networked clients must replay the exact same throw), NOT live `Random` calls; apply to `Spin`/`AngleDegrees` in `BallLauncher.cs`, scaled by a **non-linear** function of distance from the meter peak; hand-place the archetype trigger zones at the extreme ends of the meter rather than deriving them from the smooth curve.

## Progression
- Coins only, or XP + unlockables + cosmetics?
- Cosmetics: earned, or also a future paid layer?

## The big one — what makes Wee Spurts *different*?
- Current hypothesis: the between-turn "table talk" loop (betting + heckling) on chaotic physics.
- This is a marketing hook AND a design north star. Do not lock it until the prototype proves what's actually fun.

## Business / release
- Price point and release scope (early access?).
- Content review: confirm the satirical gambling/drinking framing passes Steam's rules + age rating before submission.
- When exactly to pay the $100 Steam Direct fee (currently: at "first store build").

---
When one of these gets answered, move the decision into the relevant Bible doc and note it in the Bible change log.
