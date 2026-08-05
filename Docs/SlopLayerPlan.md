# Slop Layer & Progression — mini-project breakdown

Decomposes `Docs/Roadmap.md` systems **[6] Slop Layer** and **[7] Progression**, which that doc
treats as one box each. This is not a competing plan: Roadmap stays the dependency graph, BLUEPRINT
stays the phases and gates, PLAYBOOK stays the task ledger. This is the level underneath — the
sub-systems inside [6] and [7], and what depends on what.

Written 2026-08-03, after the Mirror/KCP spike confirmed the networking architecture holds
(`Docs/spikes/2026-08-03-mirror-kcp-findings.md`).

---

## Decision recorded: the Stage C clip requirement is amended

**Tony's call, 2026-08-03.** BLUEPRINT §6 Phase 1 and PLAYBOOK Stage C both gate on "a throw makes
you both laugh **and** record a 15-second clip worth posting." The laugh half is **declared passed**.
The clip half is **deferred** — a greybox lane and placeholder characters cannot produce a postable
clip regardless of how funny the physics are, so holding the project behind it was gating fun on art
that the plan had deliberately postponed.

The clip requirement moves to the end of **V1** below. BLUEPRINT §6's "art pass happens only after
the first funny game gate" is therefore amended: a **bounded** presentation pass moves earlier.

*Needs a change-log line in `Docs/GameBible.md` per `CLAUDE.md` golden rule 6.*

**The risk this creates, stated plainly:** "make it look like a game" has no natural stopping point.
V1 is time-boxed for that reason. If V1 overruns its box, that is the signal to stop and ship what's
there, not to extend it.

---

## The dependency spine

```
        [F1] Turn authority ──┐
                              ├──> [S1] Betting <── [F3] Coin ledger
        [F2] Roll events ─────┘                          │
                                                         ├──> [S4] Bar & slots
        [S2] Taunts (no deps)                            ├──> [S5] Interference
                                                         └──> [P1] Cosmetics
        [F1] ──> [S3] Drink meter
                                              [Stage D Steam] ──> [P2] Achievements

        [V1] Presentation & juice — parallel, time-boxed, no code dependencies
```

Read it as: **nothing social works until the game knows whose turn it is and what just happened, and
nothing economic works until coins exist.** That's the whole insight. Everything else is leaves.

---

# Foundations

These are not features. They are the things every feature reaches for, and building them once,
deliberately, is what stops the slop layer turning into spaghetti.

## F1 — Networked match & turn authority

**Depends on:** Stage E networked bowling. **Blocked until the PC is back.**

The spike explicitly flagged this as the gap it did not close. Today `CmdThrow` on `BowlingMatchFlow`
runs with `requiresAuthority = false` and trusts any caller; a second player triggering a throw hits
an idempotency guard, safely no-ops, and gives that player *no feedback at all*.

**Scope:** real turn ownership (host decides whose turn, clients cannot spoof it); roll continuation
past a single throw; rejection feedback when a player acts out of turn.

**Done when:** a full networked 10-frame match completes with correct turn order, and acting out of
turn produces a visible "not your turn" response rather than silence.

**Why it's first:** betting needs to know whose turn it is. The drink meter needs to know whose turn
it is. Interference needs to know who's protected. Every social feature keys off this.

## F2 — Roll outcome event stream

**Depends on:** F1.

One host-authoritative event fired when a roll resolves, carrying what happened: pin count, strike,
spare, gutter, split, which archetype fumble (Hook / Chip / Overcook / Whiff), which player.

**Scope:** the event, its payload, and its replication. Nothing subscribes to it yet.

**Done when:** a test subscriber logs a correct, identical event on every machine for every roll of a
full match.

**Why it's separate:** without it, betting reaches into `BowlingMatchFlow` for outcomes, the drink
meter reaches in separately, taunt triggers reach in a third time, and progression a fourth. Four
reads of the same state is how the host-authoritative boundary you just validated gets quietly
eroded. Build the seam once.

## F3 — Coin ledger

**Depends on:** nothing for the core. F1 only for the networked wrapper.

Your own read, and it's correct: currency is the foundation the entire economy sits on.

**Scope:** a pure C# ledger — balances per player, transactions, atomic debit/credit, rejection of
overdraft — plus a host-authoritative networked wrapper. Config in a ScriptableObject
(`CoinConfig`: starting balance, payout multipliers, bet denominations) per `CLAUDE.md`.

**Done when:** the pure core passes EditMode tests the way `BowlingScorer` does — the existing 72
green tests are the standard to match — and balances survive a networked session including a
mid-match disconnect and rejoin.

**Build the core now, on the Mac.** It needs no networking, no art, and no PC. It's the single most
useful thing available while the hardware is down.

**Coins persist between sessions — decided 2026-08-03.** People should want to come back to their
cosmetics. Two consequences the ledger has to carry from day one: it needs **save/load** (new
infrastructure, doesn't exist yet — balances, cosmetics owned, cosmetics equipped, arcade high
scores), and it needs a real **balancing pass**, because persistent income that outpaces sinks makes
every wager weightless by session four. This answers the `OpenQuestions.md` → Progression entry;
move it out of open questions and into the Bible.

---

# Slop features

## S2 — Taunts & heckling

**Depends on:** effectively nothing. Do this early.

Synced emote and voice-line buttons usable at any time, by anyone, including spectators.

**Done when:** any player can fire a taunt at any point and every machine sees and hears it within a
frame or two.

**Why it's out of dependency order:** it's the cheapest thing on this list, it's usable by the 85% of
players who are idle at any moment, and it is *disproportionately* what makes clips funny. Recorded
lines from real people beat AI voices comfortably — `Docs/ContentPlan.md` already says this. Sequence
it early for morale and for V1's clip target.

## S1 — Payouts, wagers & skins

**Depends on:** F1, F2, F3. **Revised 2026-08-03 — per-throw betting was rejected. See
`Docs/Projects.md`.**

Three pieces, none of which add interaction cost to a throw:

1. **Automatic performance payouts.** Coins awarded from how you actually did — final score, rank,
   strike and spare bonuses, frame results. No action to collect. Payout table lives in a
   ScriptableObject so it's tunable without code.
2. **Round wager.** One stake agreed at the start of a game, custom amounts, settled after frame 10.
   One decision per game rather than forty.
3. **Skins mode.** Frame = skin, highest pin count that frame takes it, **ties carry to the next
   frame.** The pot builds when everyone plays well, which is what makes it survive a table of good
   players. Announce carryover loudly — it's a free drama beat and it tells the camera where to look.

**Done when:** four players can complete a wagered game and a skins game, with correct settlement on
every machine and correct carryover across tied frames.

**Design questions to surface rather than decide silently** (`Docs/OpenQuestions.md`): how the 10th
frame's extra rolls settle in skins; whether "the press" (a losing player doubling the stake once,
mid-round) is worth adding; payout magnitudes now that coins persist between sessions.

## S3 — Drink meter

**Depends on:** F1.

Each drink comedically degrades the drinker's next throw. Wobble amplitude driven by
`LaunchParameters.Seed` so every client replays it identically — the spike proved this replay path
works and drifts by zero, so this is on solid ground.

**Done when:** a drunk throw looks identically drunk on every machine, and sobering up is legible.

**Watch:** `Docs/GameBible.md` §8 says a miss must read as *the player's* incompetence, not the game
malfunctioning. A drink meter is the one system that can break that rule, because the player didn't
choose to be impaired. Keep the telegraph loud — the drink is visible, the wobble is earned.

## S4 — Coin sinks: the bar & the slots

**Depends on:** F3. Roaming — already proven by the spike.

The bar and casino nook already exist as greybox with named anchors. This wires them to the ledger:
buy a drink (feeds S3), pull a slot lever, sit at the card table.

**Done when:** a roaming player can spend coins at each anchor and the balance syncs correctly.

## S5 — Spectator interference

**Depends on:** roaming (proven), F1 (who's protected), F3 if it costs coins.

BLUEPRINT Push 1's differentiator: spectators physically interfere with the active player within
tunable comedic limits.

**Done when:** a spectator can meaningfully disrupt a throw, and the cost or cooldown is tunable
from a ScriptableObject without code changes.

**The live question** (`Docs/OpenQuestions.md`): how much interference is funny vs infuriating. This
is a play-and-tune answer, not a design-on-paper answer. Start far more restrictive than feels right
and loosen it — the failure mode is one player permanently griefing and everyone else quitting.

---

# Progression [7]

## P1 — Cosmetics & coin spending

**Depends on:** F3. Should follow the "first funny game" gate, not precede it.

The cosmetics counter exists in greybox with an anchor. Hats and colour first, per `ArtGuide.md`.

**Open:** earned only, or a future paid layer. Not urgent.

## P2 — Steam achievements & stats

**Depends on:** Stage D Steam framework. **Also gated on the wrapper decision** —
Facepunch vs Steamworks.NET vs Heathen, deliberately deferred until Stage D.

---

# V1 — Presentation & juice pass (time-boxed)

**Depends on:** nothing. Runs parallel to everything above.

**Box it at two weeks of actual working time.** If it isn't clip-worthy by then, ship what exists and
move on — that overrun is information, not a reason to extend.

**Target, concretely:** a 15-second screen recording of one throw that you would post without
apologising for it. Not a finished art style. Not every prop modelled. One postable throw.

**Order matters here, and it is not what people expect.** Cheapest impact first:

1. **Sound.** Ball roll, pin crash, gutter thunk, ambient alley murmur, a reaction sting. Silent
   footage reads as broken; the same footage with good pin crash reads as a game. Highest
   impact-per-hour on this entire list, by a wide margin.
2. **Juice.** Screen shake on impact, hit-stop on a strike, pin debris, a camera punch. Nearly free,
   transforms how footage feels.
3. **Lighting & palette.** One coherent lighting setup and a consistent material palette on existing
   greybox will do more than new models will. Greybox with good lighting reads as stylised; good
   models with flat lighting read as unfinished.
4. **Camera & post.** The six-beat throw cinematic already exists — grade it, add depth of field,
   frame it for a vertical crop, since that's what actually gets posted.
5. **UI pass.** Replace `DebugHud`. A programmer-art scorecard is the single clearest "this is
   unfinished" signal in any clip.
6. **Models — last, and only what's in frame.** Per `ArtGuide.md` and `AssetWorkbench.md`.

**Done when:** you have the clip. That's the whole exit condition.

---

# Build it single-machine first — the three rules that make integration cheap

**Amended 2026-08-03.** Most of the economy — the ledger, shops, cosmetics, slots, betting UI and
payout math — does not need networking to be *built*. It needs networking to be *judged*. Those are
different things, and the earlier ordering conflated them.

So build them now. The retrofit tax people pay when they do this is never about the features; it's
about **authority**, and it is avoidable by construction. Three rules, all of them the same lesson
the pre-networking hardening pass already taught on the throw path:

**Rule 1 — every coin mutation goes through one choke point.** No script anywhere writes a balance
directly. One `CoinLedger` service owns every debit and credit, and the bar, slots, cosmetics
counter, and betting all call *it*. When the networked wrapper lands, exactly one class changes.
This is precisely what `BowlingPresentation.ThrowInputAllowed` did for throw input — one predicate
to swap instead of hunting call sites, and that bet paid off in the spike.

**Rule 2 — write it as "request, then decide," even when the decider is local.** Not
`balance -= 10`. Instead: *request* to spend 10, the ledger validates and either grants or refuses,
the caller reacts to the answer. Locally the ledger says yes instantly. Under Mirror the same call
becomes a Command and the host answers. **No call site changes.** Client-authoritative money is the
classic multiplayer hole, and `Docs/Networking.md` already forbids it — "no client trusts another
client." Building it this way costs nothing today and saves a rewrite later.

**Rule 3 — anything visible to other players is stored as an ID, never a direct reference.** A hat is
`hatId = 4`, not a material reference or a prefab pointer. Cosmetics have to replicate, and an int
replicates for free while an object reference does not replicate at all. Same for taunt IDs and bet
selections.

Follow those three and "integrate it when the PC is back" is genuinely plumbing rather than a
rewrite.

## What you can build but cannot yet judge

Build the mechanism; don't over-invest in tuning these solo, because the numbers you land on will be
wrong and you'll throw them out:

- **Whether betting is fun.** It's spectators wagering on someone else's roll — with one machine
  there are no spectators. The math and the UI are buildable; the *loop* is unjudgeable alone.
- **How much interference is funny vs infuriating.** Needs a real table of people.
- **How hard a drink should hit.** Same.

Build them to work. Tune them with four people in the room.

---

# Suggested order

Sequenced around the PC being down roughly one to two weeks. Nothing here is assigned — it's the
order the work unblocks in.

**While networking is blocked (Mac only) — more than the earlier draft implied:**

1. **F3 coin ledger core** — pure C#, unit-tested against the `BowlingScorer` standard. Rules 1 and 2
   apply from the first line. Unblocks everything below it.
2. **S4 shops** — bar, slots, cosmetics counter. The anchors and `PlayerInteractor` already exist;
   this is wiring them to the ledger. Fully buildable and fully judgeable solo.
3. **P1 cosmetics** — hats and colour, stored as IDs per Rule 3.
4. **S2 taunts** — trigger and playback locally; sync is a small addition later.
5. **S1 betting — mechanism only.** Payout math against the ledger, the UI, the window open/close
   logic. Fake the spectators. Do not tune the design yet.
6. **S3 drink meter** — the wobble path is proven; `LaunchParameters.Seed` already replays at zero
   drift.
7. **V1 steps 1–3** — sound, juice, lighting, in parallel throughout. Directly serves the clip.

**When the PC returns — mostly integration, not construction:**

8. **F1 turn authority** — closes the gap the spike found. The one genuinely networking-shaped
   foundation left.
9. **F2 roll event stream** — small, protects the host-authoritative boundary.
10. **F3 networked wrapper** — swap the one class from Rule 1.
11. **Sync pass over S1/S2/S3/S4** — Commands and Rpcs over the seams already built.
12. **Then tune.** Betting, interference, drink strength — with four people, which is the only way
    those numbers are real.

**The gate this all runs at:** BLUEPRINT §6 Phase 5 — a networked session with 3–4 friends producing
repeated genuine laughter and a clip worth posting. That gate is still allowed to kill or pivot the
project. That's its job.

---

# Explicitly not in scope

- **Venue expansion.** The alley is built. Walking it and judging it is the work; adding to it is not,
  until the fun gate.
- **The second minigame.** Roadmap [8], post-launch or post-gate. Darts and beer pong reuse the
  launch-parameter pattern — that's an argument for the roadmap being right, not for starting now.
- **The Steam wrapper decision.** Deferred to Stage D by design.
- **Voice chat.** Still open (`Docs/Networking.md`). "Just use Discord" remains a legitimate answer.
