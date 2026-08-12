# Executive Day Plan — 2026-08-11

**For:** Tony, and for Claude Code in `C:\Dev\WeeSpurts`.
**Status:** Tony's executive-decision day. Where this file contradicts an older doc, **this file wins**
and the older doc gets a change-log line. Do not quote `GameBible.md`, `Projects.md`,
`DesignTerritories.md` or `SlopLayerPlan.md` back at Tony as a reason not to build something here.

---

## The shape of the day, in one paragraph

Your list is four projects, not one day. The single highest-leverage move on it is the **Humanoid rig
conversion**, because it is the shared dependency under *four* of your bullets — the animation fixes,
the gooey arms, sit-anywhere, and drunk stumbling all wait on it. So the day is built around getting
that done early and correctly, with everything that does **not** need the Unity editor pulled forward
into a single unattended run that happens **while you are downloading animations by hand**. That
parallel is the whole efficiency trick: the expensive resource today is not tokens, it's *your* time
in the editor pressing Play.

**Realistic landing zone:** Blocks 0–6 is a strong, complete day. Blocks 7–9 are the stretch. The claw
machine is parked with a spec at the bottom — see "What gets parked and why."

---

## 1. What I found before planning

### State of play

Stages 1–3 of `2026-08-11-venue-economy-buildout.md` are **done**: `TicketLedger`, `Vendor`/`BarStation`,
`BlackjackTable` + rules engine, `BlackjackStation` (seats you via `ControlMode.Seated`), `Inventory` +
`ItemCatalog`, `VenueStation`, and `ThunderLanesVenueStationSetupTool` all exist. Stage 4 onward does not.

Not built at all: slots, arcade, drink meter, throw loadout, lane conditioning, save/persistence, any
inventory UI, any card or chip visuals, any audio content.

### The animation diagnosis — this is the concrete answer to "why does my dude do that"

`Prefabs/PlayerCharacter.controller` is **stale relative to `CharacterSetupTool.cs`**. The tool's clip
table has been edited since the controller was last generated, so the committed controller points at
clips nobody intended:

| Animator state | Clip it *actually* plays | Should be |
| --- | --- | --- |
| **Idle** | `Agree_Gesture` — he is nodding in agreement, forever | an idle |
| **Defeat** | `Headache_Relief` — he is rubbing his temples | a slump |
| **DrunkIdle** | `Headache_Relief` — **the same clip as Defeat** | a sway |
| **Throw** | `Female_Crouch_Pick_Throw_Forward` | a bowling delivery |
| Sprint / Walking / Excited / FallFlat | correct | — |

So: your idle is a nod, your defeat and your drunk idle are the identical headache animation, and your
throw is someone crouching to pick something off the floor. That is the entire mystery. **Re-running
`WeeSpurts > 1 Assets > Set Up Player Character` would fix half of it in thirty seconds** — but don't, because
Block 2 rewrites that tool anyway.

### The gooey arms — this one is not a code bug

Every FBX in the project imports as `animationType: 2` (**Generic**), including the mascot. "Arms stick
to the body and he looks gooey" is the signature of **skin-weight bleed at the shoulder and armpit** in
a Meshy auto-rig: torso vertices get weighted to the upper-arm bone, so the chest stretches like gum
when the arm swings. No amount of animator or C# work touches that. It is fixed by re-skinning or by
changing the mesh.

You chose **Convert to Humanoid + Mixamo**, which is the right first move and will *substantially*
improve the read — Humanoid retargeting rebuilds the pose from a normalized skeleton, so proper
arm-swing clips will separate the arms from the torso and the bleed becomes far less visible. Be clear
with yourself though: **it reduces the symptom, it does not remove the cause.** If he still looks gooey
after Block 2, the honest next step is a different mesh (that was option 2 on the question), and that
is a decision for a later day, not this one.

### Environment, confirmed

Unity **6000.5.4f1**, URP 17.5, Input System 1.19 installed but the codebase uses legacy `Input.*`
throughout (fine — don't migrate today). Mirror 96.11.1 vendored. **No Cinemachine** — all camera work
stays in your own `ThrowCamera`/`ThrowCameraSequence` stack. Branch is `spike/mirror-kcp` with a large
uncommitted diff. Mixamo is up, free, and commercially licensed for shipping inside a game (you may not
redistribute the raw FBX as an asset pack), but it is unmaintained/maintenance-mode — grab what you
need today rather than assuming it'll be there next year.

---

## 2. How to run Claude Code today (you asked, so here is the direct answer)

### The one change that will save you the most time

`CLAUDE.md` currently says **"One system per session"** and the buildout prompt says **"STOP"** after
every stage. Those rules exist for good reasons and they should stay as the default — but they are
what is making your sessions short and expensive, because every stage ends with a handoff and a
re-read of context.

**Today, override them explicitly and in writing, but only for work that has EditMode tests.** The rule
that replaces them:

> Claude Code may run multiple stages back-to-back **without stopping** when every stage in the run is
> pure C# with EditMode coverage, and it runs the test suite itself after each stage. It must stop
> immediately on a red test. It must still stop before any change that touches a `.unity` scene, a
> `.prefab`, an importer setting, or anything you can only verify by pressing Play.

That single distinction — *can a machine verify this, or only Tony?* — is the whole efficiency model
for the day. Code that tests itself gets a long leash. Everything else gets a short one, because a
"check" you can't run is not a check.

### Model recommendations

| Use | Model | Why |
| --- | --- | --- |
| Blocks 2, 3, 6 (rig, cameras, inventory-at-the-line) | **Opus** | High blast radius, asset-pipeline and feel reasoning, touches code that already works. These are the ones where a wrong turn costs you an hour of editor time. |
| Blocks 1, 4, 5, 8, 9 (engines + tests, sit, audio, stations, cabinet) | **Sonnet** | Well-specified, test-verified or cosmetic. Sonnet is faster and you will not notice the difference on work this well-defined. |
| Change-log and doc updates at end of day | **Haiku** | Mechanical. Don't spend Opus on prose. |

Run **Block 1 in its own Claude Code window** so it can grind unattended while you're on Mixamo. Don't
try to run two windows against the same working tree on *editor* work — you'll get scene and `.meta`
conflicts.

### QA levels — where to spend verification and where not to

| Level | Applies to | What it means |
| --- | --- | --- |
| **High** — EditMode tests **plus** the `qa-engineer` agent | Anything touching `TicketLedger` (slot stakes and payouts, blackjack double/split stakes, dropped-ticket pickup), and anything with a seed (`DeterministicRng`, the slot reels, the card shoe) | Money bugs and desyncs are the ones that are expensive to find later and impossible for you and Braeden to spot by eye. This is where the agent earns its keep. |
| **Medium** — EditMode tests, no agent review | Inventory changes, drink-meter model, anything pure C# without money in it | Tests are cheap here and catch real regressions. Agent review is overkill. |
| **Low** — you play it, no tests | Animations, cameras, audio wiring, station placement, the arcade cabinet, sit-anywhere | A test cannot tell you whether a camera feels good. Writing one costs more than it catches. **Do not let Claude Code write tests for these** — say so, or it will, because your own docs tell it to. |
| **Skip** | Editor-tool idempotency beyond literally running the tool twice | You already have the precedent and the pattern works. |

---

## 3. The blocks

Each block below is one Claude Code session. The **prompt** is copy-paste. The **gate** is what you
check before moving on.

---

### BLOCK 0 — Ten minutes, you alone, no AI

You are on `spike/mirror-kcp` with a large uncommitted diff spanning `.claude/`, `Docs/`, `BLUEPRINT.md`,
`CHANGELOG.md` and Unity assets. **Working on a dirty tree all day is how you lose a day.** Commit it
(`chore: WIP checkpoint before executive day`) or stash it, then cut a fresh branch:

```
git checkout -b feat/executive-day-2026-08-11
```

You will be touching the character prefab, the animator, several scenes and a lot of new scripts.
Being able to `git checkout .` when Block 2 goes sideways is worth ten minutes.

---

### BLOCK 1 — The unattended run (Sonnet, long leash, ~90 min, **you are not at the computer**)

This is everything that needs no editor. Start it, then go do the Mixamo list in §4.

> **Prompt:**
>
> Read `Docs/Prompts/2026-08-11-executive-day-plan.md` first. You are running Block 1.
>
> **Override for this session only:** ignore CLAUDE.md's "one system per session" rule and the
> buildout prompt's per-stage STOP. Every task below is pure C# with EditMode tests. Run the full
> EditMode suite after each task and continue straight to the next one if it is green. Stop
> immediately and report if a test goes red. Do not touch any `.unity`, `.prefab`, `.controller`,
> `.meta` or importer setting in this session — if a task seems to need one, skip it and note it.
>
> Do not write tests for anything cosmetic. Do not run the qa-engineer agent except where I say to.
>
> **1. `SlotMachine` engine.** Pure C#, mirroring `BlackjackTable`'s shape exactly — constructor takes
> config + seed, driven by `DeterministicRng`, `Pull(TicketLedger, playerId, stake)` taking the stake
> via `RequestSpend` and paying via `Award`. Theme is the Thunder Lanes house machine: reel symbols are
> **pin, beer, bowling ball, bowling shoe, ticket, mascot face**. Three reels. Paytable and reel strips
> live in a `SlotConfig` ScriptableObject (data only — the engine takes plain arrays, same
> `VendorConfig`/`Vendor` split you already use).
> Volatility shape: mostly nothing, fat tail. A jackpot should be rare enough that the alley hearing it
> is an event.
> **Bonus round:** three pins triggers `FREE FRAME` — the engine exposes a bonus state where the player
> gets N free pulls at a multiplier. Model it as state on the machine (`BonusPullsRemaining`,
> `BonusMultiplier`), resolved through the same `Pull` call, so the station layer stays dumb.
> Tests to the `BlackjackTests` standard, **including a payout-distribution test over 100k pulls
> asserting the realised return sits in a stated band.** *(High QA: run the qa-engineer agent on this
> one when it's green — it touches the ledger and a seed.)*
>
> **2. Blackjack double-down and splits.** Extend `BlackjackTable`. `Double(ledger)` — legal on the
> first two cards only, takes a second stake equal to the first via `RequestSpend`, deals exactly one
> card, auto-stands. `Split(ledger)` — legal on a matched pair, takes a second stake, produces two
> hands played in sequence. This means `BlackjackTable` now needs **multiple player hands** and a
> notion of the active one; restructure `PlayerHand` into an indexed collection with `ActiveHandIndex`,
> and make `BlackjackRound` able to report a per-hand result. Keep `DealerHitsSoft17` and the existing
> payout maths untouched. Aces split once, one card each, no re-split — standard casual rule, and it
> keeps the state machine finite.
> **Read `Docs/OpenQuestions.md` §Blackjack and `Projects.md` §C before you start** — those docs say
> "hit/stand only, no splits." Tony has overridden that today; add a change-log line in
> `Docs/GameBible.md` recording the override rather than arguing it.
> Tests: double legality, split legality, split stake taken correctly, two-hand settlement, a split
> hand that busts while the other wins, and `TotalInCirculation` moving by exactly the expected amount
> across a full split round. *(High QA: qa-engineer on this one too.)*
>
> **3. `DrinkMeter` model.** Pure C#, no Unity. A 0..1 intoxication level with `Drink(amount)`,
> `Decay(deltaFrames)`, and named tiers (Sober / Loose / Drunk / Gone). It exposes, as pure
> read-only outputs, the three numbers the aim phase will consume:
> `PowerMeterSpeedMultiplier` (**goes DOWN as you get drunker** — the meter fills slower),
> `GreenZoneWidthMultiplier` (**goes DOWN too** — the perfect window narrows), and
> `AimSwayAmplitude` (goes up). Tunables in a `DrinkConfig` ScriptableObject.
> The meter must NOT know about the ball, the lane, or the animator — it is a number with a curve.
> Tests: tiering, decay to zero, monotonicity of each output, and that all three outputs are clamped
> to sane ranges at extreme input.
>
> **4. Inventory additions for the item loop.** On the existing `Inventory`: a `Drop(itemId, amount)`
> that returns a result the same shape as `Remove`, plus `FirstNonEmptySlot()` / `NextNonEmptySlot(int
> from)` / `PrevNonEmptySlot(int from)` so a HUD can cycle without reaching into the array. Add a
> `HeldSlotIndex` concept with `SelectSlot(int)`, `CycleNext()`, `CycledPrev()` and an
> `OnHeldChanged` event. Tests for cycling across empty slots, cycling with an empty inventory, and
> that dropping the held item updates the held slot sensibly.
>
> When all four are green, write the `Docs/GameBible.md` change-log lines and stop.

**Gate:** full EditMode suite green, and the count has gone up by roughly 25–35 tests. You check this
when you get back from Mixamo, not while it runs.

---

### BLOCK 2 — The Humanoid rig conversion (**Opus**, tight loop, ~90 min, the important one)

Do not start this until the Mixamo files are on disk. This is the block with the highest chance of
needing a manual step from you, so read the "if it fails" note before you begin.

> **Prompt:**
>
> Read `Docs/Prompts/2026-08-11-executive-day-plan.md` §1 first — it contains a diagnosis of the
> current animator that you need.
>
> We are converting the mascot from a Generic rig to a **Humanoid** rig so that Mixamo clips retarget
> onto him. This is the highest-blast-radius change of the day. Plan first, list every file you'll
> change, and wait for my go-ahead before writing anything.
>
> **Context you must verify yourself, not take from me:** every FBX under
> `Assets/_Project/Characters/` and `Assets/_Project/Animations/` currently imports with
> `animationType: 2` (Generic). `CharacterSetupTool.cs` generates both the avatar setup and
> `Prefabs/PlayerCharacter.controller`, and its clip table has drifted out of sync with the committed
> controller — Idle currently plays `Agree_Gesture`, and Defeat and DrunkIdle both play
> `Headache_Relief`. Confirm all of this by reading the assets before you plan.
>
> **Scope:**
>
> 1. Rewrite the rig-import half of `CharacterSetupTool` to set the mascot rig FBX (`Idle_3`) to
>    `ModelImporterAnimationType.Human` with `avatarSetup = CreateFromThisModel`, and every animation
>    FBX (both the Meshy set and the newly-added Mixamo set in
>    `Assets/_Project/Animations/Mixamo/`) to `Human` with `CopyFromOther` against the mascot's avatar.
>    **Verify every ModelImporter API you use against the Unity 6000.5 scripting reference before you
>    write it** — I cannot catch a hallucinated importer property, and getting this wrong silently
>    produces a T-posed character rather than an error.
> 2. After reimport, **log the resulting avatar's validity and print which human bones failed to
>    map.** If the auto-mapping is incomplete, stop and tell me exactly which bones — I'll fix it by
>    hand in the Configure window, which is a manual step no script should fake.
> 3. Rewrite the clip table with the corrected mapping. Prefer the Mixamo clip for every state where
>    one exists; keep the Meshy clip only where Mixamo has nothing better. The states are:
>    `Idle`, `Walking`, `Sprint`, `DrunkIdle`, `DrunkWalk`, `Throw`, `Excited`, `Defeat`, `FallFlat`,
>    and new: `SitIdle`, `StandToSit`, `Stumble`.
>    **`DrunkIdle` and `Defeat` must be different clips.** They are currently the same one.
> 4. Set the Mixamo locomotion clips to loop, and set root-motion locking on them appropriately
>    (these are `CharacterController`-driven, so the clips must be in-place — check
>    `lockRootPositionXZ` / `keepOriginalPositionXZ` behaviour against the docs and tell me what you
>    chose and why).
> 5. `MascotConfig.DisplayScale` is currently `0.56` against a 3.14 m Generic rig. Humanoid
>    retargeting normalizes proportions, so re-measure with the existing `LogCharacterHeight` path and
>    report the new number to me — do not silently retune it.
>
> **Do not** change `CharacterThrowReactionActor`, `PlayerAvatar`, `ThrowerAimSlide` or
> `FirstPersonController` in this session. If the conversion appears to require it, stop and say so.
>
> **No tests.** This is verified by me pressing Play. Do not write EditMode tests for importer settings
> beyond the existing `CharacterSetupToolTests` guards.

**Gate:** run `WeeSpurts > 1 Assets > Set Up Player Character`, enter Play, and walk around. You are looking for
**arms that swing independently of the torso**. Also fire a throw and watch which clip plays.

**If the auto-avatar fails:** Unity's automatic bone mapping regularly fails on auto-generated
skeletons with non-standard bone names. If Claude Code reports unmapped bones, open the rig FBX →
Rig → Configure and map them by hand (it's usually the shoulders, the spine chain, or the fingers —
fingers are optional, map them to nothing and move on). Budget 20 minutes for this and don't let it
derail the day. **If it fails badly enough that you're an hour in with no working avatar, stop, revert
Block 2 with `git checkout .`, and go straight to Block 5.** The rest of the day survives without it;
only sit-anywhere and drunk stumbling degrade.

---

### BLOCK 3 — Throw cameras and followthrough (**Opus**, tight loop, ~45 min)

> **Prompt:**
>
> `Bowling/ThrowCameraSequence.cs` (783 lines) and `ThrowCameraSequenceConfig.cs` already drive a
> multi-beat throw cinematic. Read both, plus `ThrowCameraFraming.cs`, before proposing anything.
>
> Tony's complaint: he cannot watch the character's animation through to its followthrough — the
> camera leaves the thrower too early to see the throw and the celebration land.
>
> **Scope:** add a beat, or extend the existing release beat, so the camera **holds on the thrower
> through the end of the Throw clip and into the first ~1s of the outcome reaction**, before it cuts
> to follow the ball. Every duration and offset is a `ThrowCameraSequenceConfig` field — no new
> constants in code. Add a config field for how long the hold lasts and for the camera's offset during
> it, so Tony can tune the framing in the Inspector without a recompile.
>
> Note that `CharacterThrowReactionActor.PlayOutcomeAfterThrow` already reads the Throw clip's real
> length off the Animator; the camera should key off the same source rather than a second hardcoded
> duration, or the two will drift the moment the clip changes.
>
> **No tests** beyond keeping the existing `ThrowCameraFramingTests` green. This is a feel change and
> Tony judges it by watching.

**Gate:** throw three balls. You should see the delivery finish, and see him celebrate or slump, before
the camera goes to the pins. Tune the two new config fields yourself until it reads right — that is a
Tony call, not a Claude Code call.

---

### BLOCK 4 — Sit at any chair or bench (Sonnet, ~45 min)

> **Prompt:**
>
> `ControlMode.Seated` and `PlayerAvatar.EnterSeated(Transform)` already exist and work — read them,
> and read `Slop/BlackjackStation.cs` as the worked example of a thing that seats a player.
>
> **Scope:** a `Seat` component implementing `IInteractable`, following the `VenueStation` registration
> pattern exactly. Interacting seats the player at the seat transform and plays the `StandToSit` →
> `SitIdle` animator states; interacting again (or pressing the roam key) stands them back up. One seat
> holds one player — a taken seat reports `CanInteract == false` with a prompt saying so.
>
> Plus a re-runnable editor tool `ThunderLanesVenueSeatSetupTool` in the style of
> `ThunderLanesVenueStationSetupTool` that finds every bench, stool and settee in the venue's named
> zones and stamps a `Seat` on it with a sensible seat transform. **Idempotent — running it twice must
> not duplicate anything.** Report how many seats it found.
>
> Add the two animator states and their transitions to `CharacterSetupTool` (not by hand-editing the
> controller).
>
> **No EditMode tests.** This is verified by sitting down.

**Gate:** run the tool, walk up to a settee, press E, sit. Press E again, stand. Then do it at the
blackjack table and confirm you didn't break the existing station.

---

### BLOCK 5 — Basic audio (Sonnet, ~45 min, **highest impact-per-hour on this entire page**)

Your own `Projects.md` §I says sound is the single highest-return item you have, and you currently have
an `AudioManager` that is 35 lines and plays nothing. Silent footage reads as broken.

**Do this yourself first, ~15 min:** grab CC0 clips from freesound.org or Kenney's audio packs — ball
roll (loop), ball drop onto lane, pin crash (three variations), gutter thunk, ticket dispense, slot
reel stop, slot jackpot, generic UI click, and a looping alley-murmur ambience. Drop them in
`Assets/_Project/Audio/`.

> **Prompt:**
>
> `Core/AudioManager.cs` is a 35-line stub with a single `PlaySfx`. Grow it into something usable
> without over-building it — this is a party game, not an audio-driven one.
>
> **Scope:** an `AudioCatalog` ScriptableObject mapping named sound ids to clip arrays (an array so a
> pin crash can pick a variation), a small pool of `AudioSource`s so overlapping sounds don't cut each
> other off, `PlaySfx(id)` and `PlaySfxAt(id, Vector3)` for positional sound, plus a single looping
> ambience slot. Random variation picks from the array **using `DeterministicRng` seeded off the throw
> where a sound is throw-driven**, and plain `UnityEngine.Random` where it isn't — a pin crash that
> sounds different on two machines is fine, but don't reach for live random inside anything the
> networking layer will later need to agree on.
>
> Then wire the obvious callers: `BowlingBall` (roll loop, lane impact, gutter), `Pin` (collision),
> `TicketLedger` consumers (dispense on award), `PlayerInteractor` (UI click). Volume per call site,
> all clips optional — `PlaySfx` with a missing id must be silent, never an exception, exactly like the
> current stub.
>
> **No tests.**

**Gate:** throw a ball with your speakers on. This block will change how the game feels more than
anything else today.

---

### BLOCK 6 — Inventory, carrying, throwing, and the item at the line (**Opus**, ~90 min)

This is the block that implements your "two systems, one inventory" decision, and it is the one with a
real trap in it. Read §5 of this document before starting it.

> **Prompt:**
>
> Read §5 "The traps" in `Docs/Prompts/2026-08-11-executive-day-plan.md` before you plan. It contains a
> ledger-integrity requirement you must not design around.
>
> Block 1 added `HeldSlotIndex`, cycling and `Drop` to `Inventory`. This block puts them in the world.
> Plan first, wait for go-ahead.
>
> **Scope, in this order:**
>
> **6a — Inventory HUD.** A minimal hotbar showing the player's slots and which is held, subscribing to
> `Inventory.OnChanged` and `OnHeldChanged`. Mouse wheel and number keys cycle. Style it to match
> `InteractionPromptHud`; this is not the real UI, it's a working one.
>
> **6b — Carryable, throwable items in roam mode.** A `CarriedItem` component: the held item's mesh
> appears in the character's hand, and a throw key launches it as a real physics rigidbody with an
> impulse from the camera's forward. **The object persists in the world** — it does not despawn. You can
> walk over and pick it back up via `IInteractable`. The bowling ball is the first item to use this and
> must be throwable at any time from roam mode.
> Cap the number of loose physics objects in the scene (config field, default 64) and despawn the
> oldest past that, or the venue will fill with junk and tank the framerate.
>
> **6c — Tickets as a droppable item.** Holding tickets and pressing **Q** drops one; **holding Q**
> drops at 10/sec. See §5 — dropped tickets are an **escrow**, not a balance mutation, and this is a
> hard requirement, not a preference.
>
> **6d — The item at the line.** In `ControlMode.Bowling`, the held inventory slot determines what's in
> the thrower's hand and what the HUD says. Default (or empty inventory) = the bowling ball and the
> existing throw instructions, **byte-for-byte unchanged** — the buildout prompt's Stage 4 rule that an
> empty inventory means zero added friction still holds and I am not overriding it. A held non-ball item
> swaps the mesh in his hand and swaps the prompt to "Press F to use". Cycling works at the line.
>
> **Do not change the aim slide, the timing meter, or `LaunchParameters` in this session.** The Wii-mode
> throw stays exactly as tuned. If 6d appears to require touching them, stop and tell me.
>
> **Tests:** EditMode coverage for the escrow accounting in 6c only. Nothing else in this block is
> testable without the editor — don't write tests for the rest.

**Gate:** pick up a ball in the lobby, throw it across the concourse, walk over, pick it up again. Then
hold Q and watch tickets pour out. Then walk to the line and confirm the throw is unchanged when you're
holding nothing.

---

### BLOCK 7 — Drink meter integration (Sonnet, ~45 min) — **stretch begins here**

> **Prompt:**
>
> Block 1 built a pure-C# `DrinkMeter` with three outputs: `PowerMeterSpeedMultiplier`,
> `GreenZoneWidthMultiplier` and `AimSwayAmplitude`. Wire it up.
>
> **Read `Docs/GameBible.md` §8 first.** It is locked: a miss must read as *the player's* incompetence,
> never the game malfunctioning. `DesignTerritories.md` §5 resolves the tension and its resolution
> stands — **every drunk effect happens in the aim phase, visible, before release. Nothing touches the
> ball in flight. Ever.**
>
> **Scope:** `BarStation` grants drinks (it's already a `Vendor`). The meter drives:
> `ThrowerAimSlide` gains a visible sway proportional to `AimSwayAmplitude`; `BallLauncher`'s power
> meter fills at `PowerMeterSpeedMultiplier` and its green zone narrows by `GreenZoneWidthMultiplier`;
> `CharacterThrowReactionActor.SetDrunk(true)` above the Drunk tier; `FirstPersonController` gains a
> gentle drunk wobble in roam mode at high tiers, driven through the same `Speed` float and the new
> `Stumble` state, **never by fighting the CharacterController's movement directly**.
>
> Tony's design call, recorded: the meter filling slower is a *help*, and the narrowed green zone is
> the *cost* that pays for it. Net difficulty should be roughly neutral at Loose and clearly worse at
> Gone. Expose both curves in `DrinkConfig` so he can tune that balance by playing.
>
> **Explicitly out of scope, per §9 of the Bible:** drinking does **not** affect the bouncy ball, the
> Nuke, or any powerup ball. Those run on the powerup rulebook. Gate the effects off when the active
> ball config isn't the default.
>
> **No new tests** — Block 1 covered the model, and everything here is felt, not asserted.

**Gate:** buy four drinks, go bowl, and see whether it's funny or annoying. That's a Tony verdict.

---

### BLOCK 8 — Blackjack presentation + the slot station (Sonnet, ~90 min)

> **Prompt:**
>
> Block 1 added double-down and splits to `BlackjackTable`, and built the `SlotMachine` engine. Both
> are headless. Make them real in `ThunderLanesVenue.unity`.
>
> **Scope:**
>
> **8a — Blackjack table presentation.** Card meshes (a quad with a texture atlas is correct here — do
> not model cards), dealt to felt positions with a short tween, face-down hole card, chips as a simple
> stack whose height reads the stake. A dealer NPC at the table using the existing mascot prefab with
> the Animator; he needs no AI, just deal/idle/shrug. HUD for hit / stand / **double** / **split**,
> hand totals, and the outcome.
>
> **8b — Multiple seats.** `BlackjackStation` currently tracks a single `_seatedPlayer`. Extend it to
> an array of seats using the `Seat` component from Block 4, each seat mapping to a player at the
> table. Only the seated local player's input is read — follow the existing
> `IsThisMachinesPlayer` guard, don't invent a new one.
>
> **8c — The slot station.** A `SlotStation : VenueStation` driving the `SlotMachine` engine. Reels as
> three spinning quads, a lever the player pulls, symbol strip textures for pin/beer/ball/shoe/ticket/
> mascot. Losses resolve fast and curt; wins are long and loud. **The `FREE FRAME` bonus round gets its
> own presentation** — the reels become a mini pin rack, and the jackpot audio plays venue-wide through
> `AudioManager`, not positionally, so the whole alley hears it.
>
> Scene changes go through a re-runnable editor tool, extending
> `ThunderLanesVenueStationSetupTool` rather than a new one. **Never hand-edit the scene YAML.**
>
> **No new tests** — the engines are already covered and none of this block is assertable.

**Gate:** sit, get dealt a pair, split it, double one hand, and watch the ledger. Then go pull the
lever until you hit the bonus.

---

### BLOCK 9 — One arcade cabinet (Sonnet, ~60 min)

> **Prompt:**
>
> Build **one** arcade cabinet: **"WEE SPURTS"**, a deliberately terrible 8-bit port of this game, about
> eleven seconds long. A bad one is funnier than a good one — do not polish this.
>
> **Scope:** an `ArcadeCabinet : VenueStation`. Interacting costs tickets via `RequestSpend` and puts
> the player in `ControlMode.Seated` facing the cabinet. The game itself renders to a `RenderTexture`
> on the cabinet's screen mesh and is a self-contained MonoBehaviour: a 2D side view, a ball you launch
> with one button at a moving power bar, pins that fall over. Score is pins knocked down. On game over,
> the score goes to a `HighScoreBoard` component on the cabinet (in-memory today — persistence is
> Stage 10 and is not happening today).
>
> Other players see your character standing at the cabinet. Nothing about the game state syncs — that's
> the standing decision in `Projects.md` §D and it stands.
>
> **No tests.**

**Gate:** put a ticket in, play it, laugh or don't.

---

## 4. Your Mixamo shopping list — do this during Block 1

Go to mixamo.com, upload `Meshy_AI_Bowling_Mascot_Rig_biped_Animation_Idle_3_withSkin.fbx` as your
character (Mixamo's auto-rigger will re-rig it, which is fine — you only need the clips, and Humanoid
retargeting means the skeleton they come off doesn't have to match yours).

**Download settings for every clip:** FBX for Unity, **Without Skin**, 30fps, keyframe reduction none.
Tick **In Place** on every locomotion clip. Save into `Assets/_Project/Animations/Mixamo/`.

Search for these and pick whichever take looks best — I've described what you're looking for rather
than promising exact names, because Mixamo's library names shift and I'd rather you judge by eye:

| You need | Search | Looking for |
| --- | --- | --- |
| **Idle** | `idle` | A breathing/standing idle with visible arm separation from the torso. **Pick the one where the arms hang furthest from the body** — this is your best single lever against the gooey look. |
| **Walk** | `walking` | In-place, with a real arm swing. Again: pick for arm separation. |
| **Run** | `running` | In-place. |
| **Sit down** | `sit` | A stand-to-sit transition, plus a seated idle. You need **both**. |
| **Drunk idle** | `drunk` | A swaying, unstable standing idle. |
| **Drunk walk** | `drunk walk` / `stagger` | Stumbling locomotion, in place. |
| **Stumble** | `stumble` | A one-shot trip or lurch, for the heavy-drunk tier. |
| **Throw** | `throw`, `bowling`, `pitch` | Honestly, nothing stock is a real bowling delivery. Grab the closest underhand or forward throw and accept it — **Block 3's camera work is what actually sells the throw**, not the clip. |
| **Celebrate** | `victory`, `cheer` | Two or three, so celebrations vary. |
| **Defeat** | `defeat`, `disappointed`, `sad idle` | A slump. **This must not be your drunk idle** — that's the current bug. |
| **Fall** | `falling back`, `fall` | You have `Fall_Down` from Meshy already; grab a Mixamo one anyway to compare. |

Fifteen or so clips. Budget 45 minutes. This is genuinely the best possible use of your hands while
Block 1 runs itself.

---

## 5. The traps — read these before Blocks 6 and 8

**The ticket-drop trap, and it's the serious one.** Your own law is that
`TicketLedger.TotalInCirculation()` moves *only* through `Award`, `RequestSpend` or `Transfer`. Dropping
tickets on the floor breaks that the naive way round: if a drop debits your balance and a pickup awards
it, then any spawn bug, despawn, or two players grabbing the same ticket **mints or destroys money**,
and you will not notice for weeks.

The correct model, which Stage 9 of the buildout prompt already gets right: **a dropped ticket is
escrow.** The drop is a `RequestSpend` into a house/escrow account, not a deletion. The pickup is a
`Transfer` out of escrow to the picker. A despawned ticket transfers back to the dropper, or stays in
escrow forever — either is fine, as long as it is *accounted*. Make the EditMode test for this assert
that `TotalInCirculation` is bit-identical before a drop and after every possible resolution. **This is
the one thing today that is worth being pedantic about**, because money bugs are invisible until
they're catastrophic.

**The 10/sec spawn rate.** Ten physics rigidbodies a second, held down for twenty seconds, is 200
loose objects. Cap the loose-object count (Block 6 has this) and consider making dropped tickets
non-physics after they settle — freeze them, or swap them for a cheap decal. Otherwise your framerate
dies in the exact moment you were trying to make funny.

**Splits are more expensive than they sound.** `BlackjackTable` currently has one `PlayerHand`.
Splits mean an indexed collection, an active-hand pointer, per-hand stakes and per-hand settlement —
that's most of Block 1's task 2. It also complicates the "you get dragged to the lane and the dealer
plays your hand badly" gag you have designed, because now he has to play *two* hands badly. That gag
isn't built yet, so there's nothing to break today — just know the bill arrives later.

---

## 6. Executive decisions to record in `GameBible.md`

You said today is an executive-decision day, so here is what today actually overrides. Each of these
needs a change-log line, and Claude Code should add them rather than argue:

1. **Blackjack gets doubles and splits.** `Projects.md` §C and `DesignTerritories.md` §3 both say
   "hit/stand only, no splits — this isn't a card game, it's a conversation prop." **Overridden.** Worth
   knowing what you're trading: the argument against splits was never that they're bad blackjack, it's
   that a longer hand pulls attention away from the lane, which is the thing your own docs call the
   spine. Watch for that in the first four-player test — if the casino nook goes quiet and stays quiet
   while someone's bowling, that's the prediction coming true, and the fix is a hand timer, not
   removing splits.
2. **Drunk mechanics: slower meter, narrower green zone.** This is *new* and it's better than what was
   written. `DesignTerritories.md` §5 only had sway and a wandering green zone; the "impairment slows
   the meter, which helps, and the narrowed window is what you pay for it" trade is a genuinely nicer
   piece of design because it makes drinking a real decision rather than a pure debuff. It also stays
   fully inside the §8 rule, because all of it is visible before release. **No conflict — record it and
   build it.**
3. **Drinking does not affect powerup balls.** Follows from §9's two-rulebook structure. Recorded so
   nobody "fixes" it later.
4. **The ball is carryable and throwable anywhere, and persists in the world.** No conflict with §8 —
   a person chucking a real bowling ball across a bowling alley is exactly the grounded register §8
   asks for. One thing it does bump into: the **drag-to-the-line** moment that `DesignTerritories.md`
   §7 calls the signature beat. If you can rip the ball out at any time, being *dragged* to the line
   holding it is slightly less special. Not a blocker, and not today's problem — but that's the seam,
   so you've seen it now.
5. **The Wii-mode lane game survives unchanged.** Your "two systems, one inventory" answer is the low
   risk call and I think the right one. The timing meter is §8's stated heart of the game and it's the
   one part of this project that is already tuned and working. Layer on top of it; don't reopen it.

---

## 7. What gets parked, and why

**The claw machine full of bowling balls.** This is a good idea and it is not a same-day item alongside
everything else on this list. It's a physics toy that needs its own rig: a gantry on two axes, a
grabber with configurable joints and a deliberately weak grip strength, a ball pit with enough
rigidbodies to be satisfying without tanking the frame, a prize chute, and a "the claw drops it 90% of
the time" tuning pass that is genuinely most of the work. Call it a session of its own.

Two things worth deciding now while it's fresh, so the session is cheap when it happens: **what does a
bowling ball out of the claw actually get you** — an inventory ball with a random skin? a rare powerup
ball? just a ball you can throw at your friends? — and **does it cost tickets per grab or per play**.
The mechanic is funnier if the balls it dispenses are visibly, uselessly identical to the ones already
free on the ball return.

**Also parked:** lane conditioning and the janitorial set (Stages 5–6), the performance payout table,
save/persistence, the other two arcade cabinets, the Loser's Machine, and the dealer-cheats mechanic.
None of them are blocked by today's work; all of them get easier after it, because Blocks 1 and 6 build
the inventory and item plumbing the whole sabotage set hangs off.

---

## 8. End of day, ten minutes (Haiku)

> **Prompt:** Update `CHANGELOG.md` and `Docs/GameBible.md` with everything that landed today. Follow
> the existing entry style — dense, explains *why* not just *what*. Add the change-log lines for the
> five executive decisions in §6 of `Docs/Prompts/2026-08-11-executive-day-plan.md`. Then update
> `Docs/Projects.md` to strike through what's now done. Do not write any code.

Then commit, and push the branch. Whatever landed, landed.
