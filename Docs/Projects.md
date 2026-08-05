# Projects — the working menu

**What this is:** the browsable list. When you've got an evening and want to know what's worth
picking up, open this. Big ideas at the top of each section, smaller pieces underneath. Not a
schedule, not a dependency graph (`Docs/SlopLayerPlan.md` has that) — a menu.

Direction decisions live at the top so nothing below drifts out of line with them.

---

# Direction — decided 2026-08-03

These supersede anything that contradicts them elsewhere in `Docs/`. Each needs a change-log line in
`Docs/GameBible.md`.

**The room orbits the lane.** The alley is a fun room containing a bowling game, not a menu with
bowling attached — but bowling has the most impact on the night. Every other station is a place to
spend, mess about, or wait. The moment the arcade is more fun than bowling, we've built a different
game by accident. That's the test.

**No per-throw betting.** Prop bets on failure archetypes are **rejected** — ten frames × four
players is forty betting decisions a night, which is admin, not a game. Wagering must not add
interaction cost to every throw.

**Coins are earned by performance, automatically.** Payouts derive from how you actually did —
final score, rank, strike and spare bonuses, frame results. No action required to collect. The
payout is a *comment on what just happened*, which does the spectator job betting was supposed to do,
for free.

**Wagering happens once, at the start of a round.** Custom stake amounts, agreed up front. Settlement
after all ten frames. One decision per game, not per throw.

**Skins is a mode, and it's the competitive one.** Each frame is a skin; highest pin count that frame
takes it; **ties carry the skin to the next frame.** Directly solves the strike-after-strike problem —
two players both striking is a tie, so nobody banks it and the pot grows. Mutual excellence creates
pressure instead of runaway. Group already knows the format from golf, which is worth more than any
mechanic we'd have to teach.

**Coins PERSIST between sessions.** Decided — people should want to come back to their cosmetics.
Consequence, accepted knowingly: the economy now needs real balancing. Income must not outpace sinks
or wagers become weightless. This also answers the `OpenQuestions.md` → Progression entry.

**The casino is a dumb sink, and that's the whole job.** Slots, blackjack and the dealer do not need
to integrate with anything. They exist to be fun, stupid, and to drain coins — same structural role
as the cosmetics shop. Negative expected value is a tuning default, not a design law.

**The arcade is real, and it's the cheap version.** Cabinets you stand at, with high scores. Local
single-player toys — nothing about game state syncs, only your presence and the score. Others see
your character at the cabinet. Deliberately stupid, deliberately short.

**Currency name: still open.** "Coins" vs "tickets" vs something else. Whatever wins, there is only
**one** currency — never coins *and* tickets *and* chips. Name it after whichever fiction we want
dominant.

---

# The projects

## A — The Lane

The centre of gravity. Everything else is a place to be between turns.

- **Skins mode** — frame = skin, ties carry, pot builds. Announce carryover loudly ("THREE SKINS ON
  THIS FRAME"); it's a free drama beat and it tells the camera where to point.
- **10th frame in skins** — the extra rolls need a rule. Does the 10th carry extra weight, or does
  it settle everything outstanding? Worth deciding by playing.
- **The press** *(suggestion, not decided)* — a golf side bet: a player who's behind can double the
  stakes once, mid-round. One optional button, only available when losing, pure trash talk. Adds a
  real wager decision without the forty-decision problem.
- **House rules via presets** — Golden Pin, Rising Tide, Poverty Night, No Bumpers. Config files, not
  systems. Cheap variety, and five other projects hang their dials here.
- **Powerup ball family** — the Nuke exists. What are its siblings? Ball return stays the randomiser
  (decided): per frame, not per roll, never purchasable.
- **Throw depth check** — timing, 2D spin, archetypes, randomised balls. Is it enough to sustain a
  night, or does it want one more dimension?

## B — Economy & currency

- **Coin ledger core** — pure C#, unit-tested to the `BowlingScorer` standard. Keyed by player ID
  from the first line, never a single "the player's coins."
- **Request-then-decide** — never `balance -= 10`. The caller *requests*, the ledger grants or
  refuses. Locally instant; under Mirror the same call becomes a Command. No call site changes later.
- **Performance payout table** — what a strike is worth, a spare, a frame win, final rank. A
  ScriptableObject so it's tunable without code.
- **Save & persistence** — new infrastructure, doesn't exist yet. Balances, cosmetics owned,
  cosmetics equipped, arcade high scores, records.
- **Economy balancing pass** — income vs sinks, now that coins persist. The failure mode is
  everyone rich by session four and wagers meaning nothing.
- **Round wager flow** — set stake, agree, settle after frame 10.
- **Name the currency** — coins / tickets / other. One only.

## C — The casino

Pure sink. Fun and dumb is the entire brief.

- **Slots** — loud, broadcast, jackpots heard across the alley. Reels of pins and beers, not
  cherries. Violent full-body lever pull.
- **Blackjack** — parodied to 30-second hands, hit/stand only. Note this is genuinely a small card
  game with an NPC, not a small feature — price it accordingly.
- **Getting dragged away mid-hand** — the dealer plays your hand for you, badly. Cheap joke, big
  payoff, needs the turn system to exist first.
- **The card dealer** — personality over systems. Six lines and two animations goes a long way. His
  alcove squeeze is already built.
- **Parody, not simulation** — keep casino games cartoonish rather than realistic. Same reason
  Balatro's PEGI 18 was reduced to 12 on appeal: "mitigating fantastical elements." Costs nothing to
  choose now, expensive to retrofit. Funnier anyway.

## D — The arcade

New, and deliberately cheap.

- **Cabinets + presence** — stand at it, others see you there. Nothing syncs but position and score.
- **Two or three tiny games** — eleven seconds each. A bad one is funnier than a good one.
- **High score boards** — persistent, per group. Cheap status.
- **Coin sink hookup** — a play costs; that's the whole economic role.

## E — Cosmetics & customization

The reason coins persist, so this carries real weight now.

- **Shop stays cosmetics-only** — coins buy looks, never power. Standing decision.
- **Ball skins** — the main line. Crumpled paper is still the best idea on the list.
- **Hats and tints** — v1 customization.
- **Store as IDs, never object references** — an int replicates for free; a material reference
  doesn't replicate at all. Five seconds now, a day of refactoring if skipped.
- **Try-on visible to the room** *(suggestion)* — makes browsing mid-match a flex or an insult
  rather than a menu.
- **Humiliation cosmetics** *(suggestion, preset-gated)* — winner picks the loser's look for next
  session. Funny in the right group, bullying in the wrong one. Your call.

## F — Drinking

- **Drink meter** — comedic degradation, satirical framing. No vomit, no misery.
- **All effects in the aim phase, visible, pre-release** — sway on the slide, wandering green zone,
  drifting spin dot. Never on the ball in flight. This is what keeps a drunk miss *yours* and
  preserves the Bible §8 rule that a miss reads as your incompetence.
- **Wobbler as the heavy-drunk ball** — already built, diegetic, legible.
- **The round** — buy for the table, each recipient gets a public ACCEPT / DECLINE. Declining is free
  and everyone sees it. Social pressure does the rest; the game never forces a drink.
- **What drinking buys** — it has to pay or nobody does it. Open question.

## G — Heckling & presence

- **Taunts** — synced emotes and voice lines, usable any time by anyone. Cheapest thing on this
  whole list and disproportionately what makes clips funny. Record real lines; they beat AI voices.
- **Composure vs outcome** — spectators attack composure freely (proximity, noise, thrown cups that
  bounce off harmlessly). Outcome only via priced, preset-gated, telegraphed actions.
- **The human pin** — no lane barriers is already law, so someone will stand in the lane. Lean in:
  ragdoll, coins scatter out of them on impact, thrower gets a rethrow. Note ragdoll on the current
  Generic rig needs checking.
- **Preset dials** — Friendly / PvP / Chaos. Don't tune one universal line; ship the dial.

## H — Character & animation

- **Face states** — idle, effort, dread, triumph, despair, gloat. Six states carries the comedy.
- **The drag to the line with dread** — the signature moment. Already designed. Protect it.
- **Outcome-keyed walk back** — strut, trudge, carried off. Cheap, high return.
- **Reaction cutaways** *(big, and underpriced elsewhere)* — a camera that cuts to the right face at
  the right beat. Genuinely the best clip-value idea anyone's had, and genuinely a month of work with
  the rig situation as it stands. Not a quick win. Treat as its own project when it's time.
- **Rig question** — placeholder imports as Generic, not Humanoid. Blocks Animator IK and humanoid
  masks. Currently fine; revisit when real characters land.

## I — Presentation & juice

Time-boxed. The exit condition is one 15-second clip you'd post without apologising.

- **Sound first** — ball roll, pin crash, gutter thunk, alley murmur. Highest impact per hour on this
  entire page, by a distance. Silent footage reads as broken.
- **Juice** — screen shake, hit-stop on a strike, pin debris, camera punch. Nearly free.
- **Lighting and palette** — coherent lighting on existing greybox beats new models with flat
  lighting. Greybox + good light reads as stylised.
- **Camera and post** — grade the existing throw cinematic, frame for a vertical crop.
- **Replace `DebugHud`** — programmer-art scorecard is the clearest "unfinished" signal in any clip.
- **Models last** — and only what's in frame.

## J — Networking & infrastructure

- **Turn authority** — the gap the spike left. The throw Command currently trusts any caller, and
  acting out of turn fails silently with no feedback. Everything social waits on this.
- **Roll outcome events** — one host-authoritative event per resolved roll. Payouts, skins, drink
  decay and reactions all subscribe. Build the seam once or four systems reach into match state
  separately.
- **Roll continuation** — past a single throw. Spike only proved one.
- **Save system** — needed the moment coins persist. Doesn't exist yet.
- **Port the spike's "keep" list to main** — `PlayerAvatar` as `NetworkBehaviour`, the
  `OnStartLocalPlayer` fix, `PlayerCameraDirector.Configure()`. Validated, currently stranded on a
  branch that never merges.
- **Networking.md roaming section** — four house rules from the spike exist only in a findings doc.
  The `sceneId` trap will bite whoever builds the next networked scene object.

---

## Housekeeping

- `Unity/testbuild(mac)/` is not gitignored — a full Mac `.app` bundle in the repo. Add it before it
  gets committed.
- `Docs/GameBible.md` needs change-log lines for every decision in the Direction section above.
- `Docs/OpenQuestions.md` → Progression is now answered (coins persist, cosmetics-only). Move it.
