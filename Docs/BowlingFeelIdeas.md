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

### G — Casino economy + CARDS (Tony, 2026-07-24) — parked, but structurally important
**The idea:** the slot machines in the alley corner pay out casino winnings. Winnings buy
**powerups** (ball variants) and a new mechanic: **CARDS** — single-use, discarded on play,
Bogos Binted / Liar's Bar energy. Acquisition undecided: a small shop sells them, or you win them
straight from the machines, or both.

**Why this matters more than a typical parked idea:** it *closes the loop*. The bar/slots corner
is currently set dressing with no mechanical purpose; this makes it the engine — win coins → buy
cards → deploy chaos at the table. Gives players a reason to engage with the social space between
turns (Pillar 2), and gives the walkable-alley hypothesis (`OpenQuestions.md`) an actual job.
Also slots neatly into §9's "powerups = sanctioned cartoon logic" ruling — cards are just powerups
with a different delivery mechanism.

**Card ideas so far (Tony):**
- **Splitter** — if you have a split, 50% chance your ball hits both sides.
- **Skip an opponent's turn.**
- (more TBD)

**Design notes for when this gets built:**
- **Two card families, and the split matters.** *Self-buff* (Splitter — help your own throw) vs.
  *Offensive* (Skip a turn — mess with someone else). Offensive cards are the Pillar-2 goldmine
  (they create table drama) but are also where "fun vs. infuriating" gets decided — the same
  question `OpenQuestions.md` already raises about heckling/interference. Expect to need
  cooldowns, per-game caps, or coin costs to keep offensive cards spicy rather than miserable.
- **Randomised cards (the 50% Splitter) must derive from the deterministic seed**, not live
  `Random`, or a networked game will show different results on different clients. Same rule as
  everything else in this file. The *reveal* of a coin-flip card is also a natural drama beat —
  play the suspense.
- **Rage-bait is a FEATURE, gated by the host (Tony, 2026-07-24).** The reference games (Super
  Battle Golf, Bogos Binted, Liar's Bar) are all deliberately rage-baity with friends, and that's
  wanted. Resolution: **lean into it as an OPTION, never as a required mechanic.** The lobby host
  picks which items/cards appear in the shop, via presets — e.g. *Friendly* (self-buffs only),
  *PvP Cards Only*, *Everything/Chaos*, plus a custom toggle list. Same idiom those games use.
  This means:
  * The core bowling game must stand alone and be fun with cards fully OFF. Cards are a layer,
    never load-bearing.
  * Nastier cards can be MUCH nastier than they otherwise could, because the table opted in.
    A group that picks "Chaos" has consented to the misery — that consent is what makes it funny
    instead of infuriating.
  * Design cards on a spice scale (mild self-buff → hard grief) and let presets slice it, rather
    than balancing every card to be universally palatable.
  * Anti-griefing guardrails (no targeting the same player twice running, compensation, softening
    skip to a handicap) become *preset-dependent tuning*, not universal rules — Friendly mode can
    be padded, Chaos mode doesn't need to be.
  * Host-picks-the-ruleset is also a natural fit for the Steam lobby (Roadmap [2]/[5]).
- **Feeds the betting layer.** Cards + fake-coin betting are the same economy — settle whether
  bets and card purchases share one coin pool (simpler, more tension) or separate ones.
- **Ties to parked mode idea F** (lane-vs-lane battle mode): offensive cards are exactly the
  "powerups used to mess people up" that mode wants.

**Ball acquisition — DECIDED direction (Tony, 2026-07-24): the BALL RETURN is the slot machine.**
- Balls are **NOT purchasable**. You get whatever the ball return gives you — the chute is the
  diegetic randomiser, and watching your ball come up (normal? nuke?) is free suspense every turn.
- **Randomised PER FRAME**, not per roll: you live with your ball for both throws. More
  commitment, more strategy, and the reveal stays an event instead of getting spammy.
- Rough odds: **Nuke ~5% or less** (rare enough to be an event, common enough that everyone dreads
  it). BouncyBall / Cannonball more common. Tune by feel.
- **Wobbler is NOT in the loot table** — it's the drunk-status ball (drink meter drives it), so it
  never dilutes the good draws.
- Why this is better than picking your ball: today the sandbox lets you *choose* (keys 1-9), which
  means an optimising player just takes the best ball every time. Randomising makes every frame a
  gamble the whole table watches — and a random nuke is *fairer* than a purchasable one because
  nobody can farm it (protects §8's domination guard).
- **Purchases tilt the odds, they don't bypass them** — e.g. a token that boosts your nuke chance
  this frame, or a re-roll of the return. Coins let you lean on fate, never skip it.

**Shop = COSMETICS ONLY (Tony, 2026-07-24).** Coins buy looks, never power. A clean permanent line
that protects the domination guard, avoids any pay-to-win instinct if this is ever monetised, and
means cosmetics can be added endlessly without touching balance. Cards (above) are the one
non-cosmetic spend, and they're host-gated by preset.

**Ball SKINS — the main cosmetic line (Tony, 2026-07-24).** Purely visual reskins of the ball;
zero effect on physics or `BallConfig` stats. Cheap content (texture swap on a sphere that already
exists), high personality-per-byte, and exactly what the cosmetics-only shop should sell.
- **Tony's list:** tiger face, golf ball, basketball, crumpled piece of paper, soccer ball, dark
  matter, galaxy, girly/flowery, meme-balls, country flags.
- **Strongest of those:** crumpled paper (reads instantly as "this should not work" — pure
  friendslop), golf ball (absurd scale contrast), tiger face, galaxy/dark matter as the
  aspirational rare unlock.
- **Prefer timeless-absurd over current-meme.** Memes date fast (a Doge ball reads as 2013);
  meatball / disco ball / globe / 8-ball / crumpled paper stay funny indefinitely.
- **Flags carry hidden support cost** — which flags, disputed territories, etc. Other games have
  had this headache. Not a no, just decide deliberately.
- **Bonus benefit: clip readability.** Four friends throwing four visually distinct balls means a
  highlight clip instantly communicates whose throw it was. Good for the marketing loop.

**❓ OPEN — how "fully customizable"?** Two very different products:
- *Curated library* — a big set of premade skins. Safe, cheap, no moderation burden.
- *Player-uploaded images* — **carries a real moderation problem**: any player could broadcast an
  arbitrary image to everyone in a Steam lobby. Two-person team should not own that.
- *Recommended middle ground:* a **layered customiser** — base pattern + colour + a decal/emblem
  from a curated set. Huge combination space, players feel expressive, we never host user images.
Decide before the shop is built.

**Open when built:** card acquisition (shop vs. slot payout vs. both), hand size, whether cards
persist between frames/games, one shared coin pool or separate, offensive-card guardrails,
exact ball-return odds table, what "odds-tilting" purchases look like, ball-skin customisation
model (curated vs. layered vs. uploads).

**Sequencing:** do NOT build before the core throw is proven fun and networking exists — cards are
a layer on top of a working game, and most of them (turn-skip especially) only make sense
multiplayer. Natural home is Roadmap [6] Slop Layer / [7] Progression.

---

## Feel-calls — RESOLVED 2026-07-23 (Tony) → also logged in `OpenQuestions.md`
- **Hook direction:** **too HARD (over-powered release) → hooks RIGHT; too SOFT (under-powered) → hooks LEFT.** (Confirms opposite-directions, with the specific mapping. Verify the built code's `TimingError01` sign produces this; flip if not.)
- **Spin-outs may reach the gutter** — funnier wins over less-demoralizing. Cap not required.

Still open (decide by playing): good-zone width / how much a small miss hurts (`OpenQuestions.md`).
