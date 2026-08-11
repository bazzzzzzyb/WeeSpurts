# Feature ideas — what could make this fantastic rather than merely good

Written 2026-08-11. New territory only — anything already on the menu in `Docs/Projects.md` or spec'd
in `Docs/LaneSabotage.md` is left out.

Each idea is marked **FREE** (falls out of architecture already built), **CHEAP** (days), or
**PROJECT** (weeks — its own thing). Sorted by value-per-hour. Currency is tickets throughout.

---

## 1. The CCTV system — replays are already free and nobody noticed — **FREE**

**This is the most valuable thing on this page and it's a consequence of a decision you made months
ago.**

`Docs/Networking.md` fixes the model: bowling syncs **launch parameters** — position, direction,
power, spin, seed — never per-frame physics. `LaunchParameters.cs` exists. `DeterministicRng` exists.
The Mirror/KCP spike measured **zero drift Mac↔PC on the throw path**.

Sit with what that means. A throw is completely described by a struct of about a dozen floats. Which
means:

- **A saved replay is ~50 bytes, not a video file.** An entire night of four players bowling ten
  frames is under 2 KB.
- **You can replay from any camera angle**, because you aren't playing back a recording — you're
  re-running the simulation. Orbit it. Slow it to 10%. Watch it from the pin's point of view. Watch
  it from inside the ball.
- **You already have the camera rig.** `ThrowCameraSequence` is seven authored beats; a replay is
  those beats pointed at a re-simulated throw.

**What to build:** the alley has security cameras, because of course it does. Monitors behind the
front desk. A "save that one" button that any player — including spectators — can hit for a few
seconds after a throw resolves. Saved throws replay on the venue's own screens.

**Why it matters more than it sounds:** your V1 exit condition is literally *one 15-second clip you'd
post without apologising.* Nothing in the entire plan actually builds clip capture. This does, at
roughly the cost of a serialisation format and a camera controller — and it does it **diegetically**,
inside the fiction, which is far funnier than a menu option.

**Then:** the end-of-night highlight reel, auto-assembled from saved params — best strike, worst
gutter, longest ragdoll flight — playing on the big screens while everyone's still in the lobby
arguing.

**Honest caveats, because this is the one idea worth getting right:**

- PhysX is deterministic for the same binary on the same platform, and your spike is encouraging
  evidence for cross-platform, but it is **not a guarantee across Unity versions or physics settings**.
  Stamp every saved replay with a version and refuse to play back mismatches rather than showing a
  throw that visibly didn't happen.
- Ragdolls and particle effects need to be either deterministic or excluded from the replay path.
- Treat cross-session replay persistence as a stretch goal. Session-scoped is where the value is.

---

## 2. Lane oil — real bowling's hidden skill layer, and it glows — **CHEAP**

Casual players don't know this: the main skill layer in real bowling isn't the throw, it's the **oil
pattern**. Lanes are dressed with oil in a specific pattern, the ball slides on oil and grips on dry
board, and the oil **breaks down over the course of games** as balls track through it. Pros read the
lane and move their feet as the night wears on.

Nobody in the party-game space uses this. It is genuinely fantastic material because:

- **It's teachable in one sentence.** Oil = slide, dry = grip. That's the whole tutorial.
- **It gives your hook mechanic somewhere to go.** You already have spin, `SpinModel`, and archetype
  fumbles. Oil is the dimension that turns those from a toggle into a read.
- **It's visible under blacklight** — and your venue is already neon-and-blacklight themed with an
  arcade alcove built around exactly that lighting. The oil pattern glowing on the lane is a gorgeous,
  cheap, instantly-legible visual.
- **It degrades over the night**, which means frame 9 plays differently to frame 1, for free, with no
  new systems. The lane itself becomes an opponent.
- **It's a house rule dial.** "The house oiled lane 5 tonight." Slots straight into the preset system
  in `Projects.md` A.

Implementation is a scalar field along the lane sampled by `BowlingBall` when computing hook force —
genuinely a small change now that `LaneFrame` exists and gives you clean down-lane/lateral
coordinates.

**Risk:** this adds real depth, and `GameBible.md` §8 says a miss must read as *the player's*
incompetence. Keep the pattern visible at all times so a hooked-out throw is legible as a misread,
not a malfunction.

---

## 3. The ball return accepts anything — **CHEAP**

Your architecture syncs launch parameters, not objects. So **anything** you can throw down a lane
costs almost nothing to add and replicates correctly by construction: a shoe, a chair, a pint glass, a
bowling pin, a small dog, another player.

Each object gets wildly different mass, bounce and pin interaction — same code path, same seed, same
network message. `BowlingFeelIdeas.md` already made the ball return the randomiser (per frame, not per
roll, never purchasable), so the delivery mechanism is decided; this is just widening what it can
deliver.

This is the highest comedy-per-line-of-code idea available to you, and it exists *because* you chose
launch-parameter sync. Architecture paying rent.

---

## 4. Earned titles, and The Book — **CHEAP, and it's the retention idea**

Two halves of one thing: **the group remembers.**

**Earned titles.** The game assigns you a name based on what you actually did, and you cannot choose
it. *The Gutter King. Slots Guy. Never Sober. Nine Pins. The Human Pin.* It floats above your head,
persists between sessions, and the only way to lose it is to earn a different one. Brutal, funny, and
mechanically it's a condition plus a string.

**The Book.** A ledger at the front desk that remembers across sessions: head-to-head records, longest
gutter streak, who's never beaten who, who owes what. The alley keeps records because alleys keep
records.

**Why this is more important than it looks:** it's what turns a party game into a *ritual*. Almost
nothing in this genre does group memory well, and it is the cheapest possible reason to come back
next week — far cheaper than new content. It's also the purest possible expression of the attention
law: a title is *literally* a thing to say about someone.

Depends on the save system (Stage 6 of the economy buildout), so it sequences naturally after it.

---

## 5. The night has a shape — **CHEAP, and it makes everything else cohere**

Right now a session is "a game of bowling." Give it an arc instead: **a night out.**

*Doors open → happy hour (drinks are cheap) → prime time (the main game) → last call (bar shuts,
prices spike) → closing time (final settlement, the walk out, the lights come up).*

The venue lighting shifts through it. Prices move. The music changes. Closing time is a natural,
non-arbitrary end to a session — which is the thing party games are usually worst at.

Costs almost nothing: a timeline, a few config curves, and lighting states your venue already has
zones for. But it gives every other system a context to sit in, and it makes the economy legible
("get your drinks in before last call") without a tutorial.

---

## 6. The crowd meter — spectating that isn't griefing — **CHEAP**

Every spectator system on your list is *destructive*: interference, heckling, coin theft. That's a
comedy engine with no counterweight, and `DesignTerritories.md` §6 already worries about the
grief-until-people-quit failure mode.

So add the productive one. Spectators build **hype** by cheering, heckling, being close to the lane —
and a hot lane pays more. The rail becomes worth standing at whether you like the thrower or not, and
you've given the 85%-idle players something to do that doesn't end friendships.

It also creates the game's best dilemma: heckling builds hype, and hype pays the person you're
heckling.

---

## 7. Jobs — the venue needs staffing — **CHEAP each**

Three roles an idle player can occupy, each with a physical location cost (you have to *be* there,
which means you're not at the rail):

- **The pinsetter.** Up on the mechanical catwalk you can operate the pinsetter manually — badly.
  Telegraphed, located, sanctioned interference. The catwalk is already built.
- **The scorekeeper.** Real alleys have those overhead screens with cartoon animations. Give one
  player control of it: draw on it, rename people, editorialise the scoreboard. A griefing toy that
  produces *artifacts* rather than outcomes — it can't cost anyone the game, so it can be generous.
- **The DJ.** The booth exists. Give it a person, not just a coin slot.

The scorekeeper is the pick of the three: it's pure expression, zero mechanical harm, and it
generates screenshots.

---

## 8. The commentator who is confidently wrong — **CHEAP**

An announcer voice calling the action, slightly and consistently incorrect about what just happened. A
gutter ball described as a strategic play. The wrong player's name. Sincere enthusiasm about nothing.

Recorded lines beat generated ones for quality (`ContentPlan.md` already says this about taunts), and
a small pool of deliberately mismatched lines is funnier than a large accurate one, because the joke
is the mismatch. Very high laugh-per-hour, and it directly serves clips — silent footage reads as
broken, and `SlopLayerPlan.md` V1 already ranks sound as the highest-impact work available.

---

## 9. Bumpers as a public humiliation — **CHEAP**

Anyone can call a vote to give a struggling player bumpers. They rise with a mechanical clunk, they
light up, everyone sees. The player can refuse — loudly, publicly, and then has to actually bowl.

Turns a mercy mechanic into a status mechanic. Preset-gated, obviously.

---

## 10. The leave-behind — **CHEAP, and it solves a real requirement**

`Docs/DefinitionOfDone.md` [4] requires that a disconnect mid-turn doesn't freeze the game. Everyone
solves that with a UI message. Solve it with comedy instead:

**When a player quits or drops, their character doesn't vanish — they slump asleep at the bar.** Still
there. Still interactable. You can put a hat on them. Draw on them. Photograph them. If they
reconnect, they wake up wearing whatever the room decided.

A hard technical requirement, discharged as a gag, which is the best kind of feature.

---

## 11. Photo mode — the disposable camera — **PROJECT**

A physical camera item in the world. Someone has to *be* the photographer, which means someone chose
not to bowl. Shots go on a corkboard by the front desk and persist between sessions.

Lower priority than the CCTV replay system (#1) — which delivers most of the same value for less —
but it's the version with a social cost attached, and the corkboard filling up over weeks is
genuinely lovely.

---

## What I'd actually do

If you took only three things off this page:

1. **The CCTV replay system (#1).** It is nearly free, you already paid for it, and it discharges the
   V1 clip gate that the entire plan currently hand-waves.
2. **Lane oil (#2).** It's the one idea here that adds *depth* rather than noise, it's real, and it
   looks spectacular under the lighting you've already chosen.
3. **Earned titles + The Book (#4).** The cheapest reason to come back next week.

The rest are good, but those three change what the game *is*.

## What to be careful about

- **The backlog is already long.** Nine projects, eight economy stages, and a networking gap that
  gates everything social. None of this should jump the queue ahead of turn authority.
- **`Projects.md` is explicit that venue expansion is out of scope until the fun gate.** Ideas 6, 7
  and 11 add venue surface area. Park them behind the gate.
- **The room orbits the lane.** Ideas 1, 2, 3 and 9 all point *at* the lane. Ideas 6, 7 and 11 point
  away from it. Weight accordingly.
