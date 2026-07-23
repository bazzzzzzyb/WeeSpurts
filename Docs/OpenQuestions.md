# Open Questions

Deliberately undecided. Answering these on paper *before* the prototype would be guessing. The bowling prototype + slop layer will answer most of them. Tony owns the final calls (creative director). Revisit at the "first funny game" gate.

## Players
- Max players per lobby? 2 / 4 / 8 / 12? (Turn-based bowling scales cheaply; UI & pacing don't.)
- Friends-only, invite-code, or public matchmaking? (Leaning friends-first.)
- Voice chat in-game, or "just use Discord"?

## Characters — ANSWERED 2026-07-21 → moved to `ArtGuide.md` + `ContentPlan.md`
- ~~Miis? Animals? Ragdoll humans? Potatoes?~~ → **Phasmo-style low-poly humanoids** (revised same day from beans; beans parked as fallback).
- Customization v1: bean color + hats. Depth beyond that: still open, decide at Progression (Roadmap [7]).

## The alley as a place
- Is the bowling alley a *walkable social space* (spectators wander, heckle up close, bar + slots in the corner, comedic interference) rather than a fixed-camera lane? `BLUEPRINT.md` Push 1 says test this in the first prototype — it may be the differentiator.
- If yes: how much interference is funny vs. infuriating? (Cooldowns? Coin costs to interfere?)
- **Idea (2026-07-22, Tony):** turn transition as a visual beat — between turns your character walks around freely (social space), and when your turn comes up you get slowly dragged/pulled to the front and snapped into the Wii-bowling stance to throw. Turns downtime into a readable "you're up" moment instead of a menu/UI cue. Untested — try it once the walkable-alley prototype exists.

## Physics feel — ANSWERED 2026-07-22 (Tony) → decision promoted to `GameBible.md` §8
- ~~Realistic-ish, or full Gang Beasts / Human: Fall Flat floppiness?~~ → **Neither. Grounded human-fumble chaos.** The ball always behaves like a real bowling ball; a bad power-meter release means *the thrower* fumbled it, not that physics broke. No map-collapse, no supernatural ball behavior — that's explicitly out of bounds.
- **The mechanic:** timing error opens a *second dimension* on the throw. It distorts **direction** and **spin** (the funny, spatial axes), nudges **speed** a little, and never touches physics integrity. Good timing collapses that dimension to zero — the ball goes exactly where aimed. Pillar 1 ("chaos over precision") lives *inside* this mechanic, not against it: precision is the thing you master, chaos is what missing produces.
- **Escalation curve (Tony's key call):** **non-linear, not proportional.** A *small* miss = mild wobble (stays mostly fine — keeps hope alive, no punishing fail-state). A *big* whiff = spectacle. Reserve the crazy stuff for genuine screwups so "worse is funnier" actually lands and stays special.
- **Failure archetypes at the extremes:** the red zones trigger a few *named, recognizable* disasters, each with its own telegraph + sound so the table reads it instantly — e.g. the **Hook** (savage gutter-ward curve), the **Chip** (feeble glancing dribble into a corner pin), the **Overcook** (too-hard skid/rattle), the **Whiff** (release fumbles, ball peels off near-sideways). Legible failure = shareable table-talk (Pillar 2 — friends will start yelling "he Chipped it again"). Give each archetype internal variance so the *category* is legible but the *instance* still surprises.
- **Legibility rule:** a miss must read as the *player's* incompetence, NOT the game malfunctioning. "You released early and hooked it" is funny and ownable; "the ball randomly did something" reads as a bug. Author the chaos into recognizable human failures.
- **Domination guard:** skill buys **consistency + style, not scoreboard domination.** A clean throw = the throw you *intended*, not a guaranteed strike — the pins' own scatter (`PinConfig`) keeps honest variance in every throw. The betting/heckling/drink layers are what actually swing outcomes, so a precise player is a pleasure to watch, not a tyrant. (Optional, untested spice: the leader gets a slightly faster/narrower good-zone — more to lose, more comedy.)

**Still open (feel calls — decide by playing, then log here):**
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
