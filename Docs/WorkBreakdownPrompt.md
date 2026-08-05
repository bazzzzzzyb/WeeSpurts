# Prompt: produce the Wee Spurts work breakdown

Paste everything below the line into Fable. If it has access to the repo folder, it should read the
files named in §1. If it doesn't, §2 carries enough ground truth to work from — but say so up front
so it knows which mode it's in.

---

You are acting as a senior technical producer on a small indie game. Your job is to turn a pile of
existing design documents and a working prototype into a **dependency-ordered work breakdown of
mini-projects** — the level between "system" and "task." Be opinionated. I want judgement, not a
restatement of what I already wrote.

## 1. Read these first, in this order

If you have repo access:

- `CLAUDE.md` — golden rules and constraints. These are law.
- `Docs/GameBible.md` — locked decisions. If code and Bible disagree, Bible wins.
- `Docs/Roadmap.md` — the 8-system dependency graph.
- `BLUEPRINT.md` — market reasoning, phases, gates.
- `PLAYBOOK.md` — the task ledger, stages A–H.
- `Docs/OpenQuestions.md` — deliberately undecided. **Long, and the most important input here.**
- `Docs/spikes/2026-08-03-mirror-kcp-findings.md` — the networking spike result.
- `Docs/SlopLayerPlan.md` — **an existing first attempt at this exact task. Read it last, treat it
  as a draft to argue with, and feel free to replace its structure entirely if you have a better
  one. Do not defer to it.**
- `CHANGELOG.md` — obsessively detailed project memory. Skim; don't read all 75KB.

## 2. Ground truth — do not contradict any of this

**The game.** Wee Spurts: online party game for Steam. Wii Sports readability crossed with
"friendslop" chaos — exaggerated physics, between-turn betting with fake coins, heckling, a satirical
drink meter, and a walkable bowling alley with a bar and casino corner. First minigame: bowling.

**The team.** Two beginner developers building in Unity/C# with Claude Code as the engineering team.
They cannot catch a hallucinated API. One is creative director and owns all "is it fun" calls. Work
should be describable as single scoped sessions, not multi-week epics.

**Tech stack, validated and locked.** Unity 6000.5.4f1, URP. Mirror 96.11.1 for netcode. Host-
authoritative. Bowling syncs **launch parameters** (position, angle, power, 2D spin, seed, timing
error, flags) and never per-frame physics. The Steam wrapper choice — Facepunch vs Steamworks.NET vs
Heathen — is **deliberately still open** and deferred.

**What already works, single-machine:** a complete hot-seat bowling game. 10-frame scoring with 72
green EditMode tests. A timing-meter throw with a green zone, a 2D spin selector, non-linear fumble
chaos with named failure archetypes (Hook / Chip / Overcook / Whiff), a Nuke Shot powerup, a six-beat
Wii-style throw cinematic, a rigged character with animation clips, first-person roaming through a
full greybox alley venue (bar, casino nook, card dealer, six seating pits, front desk, cosmetics
counter, lane kiosks — all with named anchor transforms), and a diegetic match start by walking up to
a lane console.

**What the networking spike proved (2026-08-03, two physical machines, real LAN):** continuously
synced roaming avatars and launch-parameter throw sync **coexist with no conflict**. Camera and input
ownership held correctly under `isLocalPlayer`. One full throw resolved end to end: client Command →
host → ClientRpc → independent local replay on every machine → host-authoritative pin confirmation.
**Physics drift across four throws was zero.** The walkable-alley design survives the netcode. This
was the project's biggest open risk and it is now retired.

**What the spike explicitly left open:** no real turn-ownership check — the throw Command trusts any
caller; no roll continuation past a single throw; no feedback when a player's action is silently
rejected. Also four hard-won environment gotchas: pre-placed `NetworkIdentity` scene objects get
different `sceneId`s in a build vs Editor Play; Mirror's auto-`sceneId` fights scripted scene
builders; never test build-vs-editor for scenes containing pre-placed identities; Windows Firewall
blocks the Unity **Editor** process separately from the shipped game.

**A recent decision that changes sequencing.** The original gate required "a throw that makes you
laugh AND a 15-second clip worth posting" before any art work. The laugh half is declared passed. The
clip half is **deferred**, because greybox cannot produce a postable clip regardless of how funny the
physics are. A **bounded** presentation pass therefore moves earlier than the original phase order
allowed. The risk this creates — unbounded art work — is understood and must be managed by time-
boxing, not by reverting the decision.

**A capability worth exploiting.** The game is already hot-seat with a working `TurnManager` and
`PlayerData`. Extending to four addressable player slots, one locally controlled, lets most
multiplayer-shaped systems be built and tested solo — and "four slots, host owns the array, each
client owns one" is the same shape Mirror wants, so it doubles as migration prep rather than throwaway
scaffolding.

**Current constraint.** The Windows PC is out of action for roughly one to two weeks. Networked work
needs two machines. Mac-only work is unblocked. Plan around this without treating it as permanent.

## 3. Your task

Produce a **dependency-ordered breakdown of mini-projects** covering the work from today to the
"first funny game" gate — a networked session with 3–4 friends producing repeated genuine laughter
and a clip worth posting — and sketch, more lightly, what lies between that gate and a Steam launch.

A mini-project is a coherent body of work with a single reason to exist, buildable in a handful of
scoped sessions, with a testable exit condition. "Currency" is a mini-project. "The slop layer" is
not — it's four or five of them. "Add a button" is not — it's a task.

**For each mini-project, give me:**

1. **Name and one-sentence purpose.**
2. **Depends on** — which other mini-projects must exist first, and *why*, specifically. "Betting
   needs to know whose turn it is" is useful; "depends on networking" is not.
3. **What it actually contains** — the concrete pieces, in enough detail that someone could scope a
   session from it.
4. **Exit condition** — an observable test. "A player can spend coins at the bar and the balance is
   correct on every machine," not "coins are done."
5. **Can this be built and judged solo, built-but-not-judged solo, or does it need multiple humans?**
   Be precise — this distinction drives the whole schedule given the PC situation.
6. **Design questions it forces** — cross-reference `OpenQuestions.md`. Flag anything where building
   it would silently decide a question the creative director owns. Do not decide those questions
   yourself; surface them.
7. **Risk or trap** — the specific way this one goes wrong, if there is one.

**Then give me:**

- **A dependency diagram** in the ASCII style `Docs/Roadmap.md` already uses.
- **A recommended order**, split into "buildable now on one Mac" and "needs the PC back," with the
  reasoning for anything sequenced out of pure dependency order.
- **The seams to build now that make later integration cheap** — the architectural decisions that
  cost nothing today and save a rewrite later. Be concrete and name the pattern. (The project has
  already learned this lesson once: routing all throw input through a single authority predicate
  before Mirror existed meant the migration swapped one property instead of hunting call sites.
  Identify the equivalent seams for the economy, cosmetics, and social features.)
- **What you'd cut.** Genuinely — what in these documents is scope creep, gold-plating, or premature.
  Two beginners with evening time built a six-pit venue with a circulation audit before their
  networking existed. Be honest about where else that pattern is repeating.

## 4. Principles you must respect

- **Host-authoritative, always.** No client trusts another client. Money especially — a
  client-authoritative economy is the classic hole.
- **Tunables are ScriptableObjects**, never hard-coded constants, so feel can be changed without
  code.
- **Chaos must be deterministic** — derived from a seed carried in launch parameters, never live
  `Random`, because every client replays the same throw independently.
- **A miss must read as the player's incompetence, not the game malfunctioning.** This is a locked
  Bible decision and it constrains any system that degrades a throw.
- **The clip test.** Every feature should survive the question "would 15 seconds of this be worth
  posting?" This is how games in this genre actually sell.
- **Skill buys consistency and style, not scoreboard domination.** A precise player should be a
  pleasure to watch, not a tyrant.

## 5. Anti-requirements

- **No calendar.** No dates, no week numbers, no sprint names. Dependency order and gates only —
  timelines drift, systems survive.
- **No invented APIs.** If you reference a Unity or Mirror type, it must be real. Say "verify this
  exists" rather than asserting confidently.
- **Don't gold-plate.** The bar is a game that reliably makes four friends laugh, not a feature-
  complete party platform.
- **Don't restate the existing docs back to me.** Where you agree with them, say so in one line and
  move on. Spend your words on structure, dependencies, sequencing, and disagreement.
- **Don't decide the creative director's open questions.** Surface them at the point they'd get
  decided by accident.

## 6. Output format

Markdown. Lead with a **one-page executive summary**: the dependency spine in three or four
sentences, the single most important thing to build next, and your strongest disagreement with the
existing plans. Then the full breakdown. Then the diagram, the order, the seams, and the cuts.

Write it as a document that will live in `Docs/` alongside the others and be read months from now by
someone who has forgotten the context. Match the house voice: direct, opinionated, reasoning shown,
no filler.

**Before you start, if anything above is ambiguous or you think the framing itself is wrong, say so
first.** A plan built on a misunderstanding is worse than no plan.
