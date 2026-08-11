# Prompt — Venue buildout: tickets, stations, inventory, lane sabotage, casino, arcade

**For:** Claude Code, in `C:\Dev\WeeSpurts`.
**Written 2026-08-11. Supersedes anything in `Docs/` that contradicts it.**

Tony is the creative director. Where this file and an older doc disagree, **this file wins** and the
older doc gets updated. Do not quote `GameBible.md`, `Projects.md`, `DesignTerritories.md` or
`SlopLayerPlan.md` back at Tony as a reason not to build something. If a design decision here
contradicts an earlier one, that is intentional — note it in the change log and move on.

---

## Where things stand

Built, tested, and green:

- `Gameplay/CoinLedger.cs` — the currency ledger. Register / BalanceOf / CanAfford / RequestSpend /
  Award / Transfer / TotalInCirculation, transaction events, no overdraft, no balance setter.
- `Slop/Vendor.cs` + `VendorItem` + `VendorConfig` — priced items, per-match stock.
- `Slop/BlackjackTable.cs` + `BlackjackHand` + `BlackjackRules` + `BlackjackConfig` + `PlayingCard`.
- `Core/DeterministicRng.cs`, `Gameplay/EconomyConfig.cs`.
- `Interaction/` — `IInteractable`, `PlayerInteractor`, `InteractionPromptHud`,
  `Bowling/LaneKioskInteractable.cs` as the worked example.
- `Bowling/LaneFrame.cs` — lane axis basis. ThunderLanesVenue bowling works.
- **`ControlMode.Seated` — done.** Players can sit. No work needed here.
- EditMode tests: `CoinLedgerTests`, `VendorTests`, `BlackjackTests`, `BowlingScorerTests`,
  `SpinModelTests`, `ThrowCameraFramingTests`, `CharacterSetupToolTests`.

Not built: tickets rename, stations in the venue, inventory, lane conditioning, sabotage items, slots,
payout table, physical currency, save/persistence, arcade.

**Nothing in the economy is reachable in-game.** No scene instantiates a `Vendor` or a
`BlackjackTable`. That's what Stage 2 fixes.

---

## Direction

**Tickets are the currency.** Not coins. There is one currency and it is called tickets — the fiction
is an arcade/alley ticket, and it buys everything: drinks, cosmetics, arcade plays, blackjack stakes,
slot pulls. Stage 1 renames the existing ledger accordingly.

**Inventory is a core system.** Players carry items. Items can be thrown down the lane in place of a
ball, used on the lane, or consumed. This is the backbone for lane sabotage and anything like it
later.

**Lane sabotage is real.** Oil the lane and the next player deals with it. A mop clears it. The wider
set is janitorial: oil can, mop, rosin, floor wax, a bluff sign, a loose pin.

---

## Engineering conventions

These are about code that works, not about design. They stay.

1. **One ledger.** No script writes a balance directly — everything goes through `TicketLedger`. Two
   places holding a balance is how money appears from nowhere.
2. **Request, then decide.** The caller requests, the ledger grants or refuses, the caller reacts.
   Never `balance -= 10`. Under Mirror the same call becomes a Command with no call-site changes.
3. **IDs, never object references.** Items, cosmetics, cabinets, sabotage tools — all ints. Object
   references don't replicate.
4. **Host-authoritative.** Clients request; the host decides. Applies to tickets, inventory, and lane
   conditioning.
5. **Pure C# engines with EditMode tests.** No MonoBehaviour, no UnityEngine, no statics, no
   singletons in a rules engine. `BlackjackTable` and `BowlingScorer` are the pattern.
6. **Never hand-edit `.unity` or `.prefab` YAML.** Scene changes go through a re-runnable editor tool.
   `ThunderLanesVenueBowlingSetupTool.cs` is the precedent.
7. **No invented APIs.** Unity and Mirror APIs must be real — check the official docs when unsure.
   Tony and Braeden cannot catch a hallucinated method.
8. **One stage per session, and stop at each gate.** Every stage below ends with a concrete editor
   check. Don't start the next stage until it passes. Don't stack untested changes.

---

## Stage 1 — Rename the currency to tickets

Mechanical rename across the codebase and tests. Do it before more code lands on top of the old
names.

| From | To |
| --- | --- |
| `CoinLedger` | `TicketLedger` |
| `CoinTransaction` | `TicketTransaction` |
| `CoinResult` | `TicketResult` |
| `CoinLedgerTests` | `TicketLedgerTests` |
| `EconomyConfig.StartingCoins` | `StartingTickets` |
| `EconomyConfig.DEFAULT_STARTING_COINS` | `DEFAULT_STARTING_TICKETS` |
| `GameManager.Instance.Coins` | `GameManager.Instance.Tickets` |

Update `Vendor`, `BlackjackTable`, `BowlingMatchFlow` and every call site. Update file names and
`.meta` files together so Unity doesn't lose references. Update any UI strings from "coins" to
"tickets".

**Done when:** the full test suite is green under the new names and nothing in the codebase says
"coin" except a deliberate historical note in the change log.

**STOP.**

---

## Stage 2 — Station pattern + venue setup tool

Makes the existing `Vendor` and `BlackjackTable` reachable in `ThunderLanesVenue.unity`.

**Scope:**

- A `VenueStation` component family implementing `IInteractable`, modelled on
  `Bowling/LaneKioskInteractable.cs`. Each station reads its numbers from a ScriptableObject and
  talks to `TicketLedger`.
- Two concrete stations: **the bar** (a `Vendor`) and **the blackjack table** (uses the existing
  `ControlMode.Seated` to sit the player down, drives the existing `BlackjackTable`).
- `ThunderLanesVenueStationSetupTool` — an editor tool placing station anchors and components into
  the venue's named zones (`WestWing_Zones` Casino/Bar/Snack/FrontDesk, `EastWing_Zones` Arcade,
  `CenterSpine_Concourse` DJ stage). Idempotent and re-runnable — running it twice must not
  duplicate anything.

**Done when:** a roaming player can walk to the bar, buy a drink, see their ticket balance change,
walk to the casino nook, sit, and play a hand of blackjack. The setup tool runs twice cleanly.

**STOP.**

---

## Stage 3 — Inventory core

Pure C#, unit-tested, no Unity. The backbone for everything in Stages 4–6.

**Scope:**

- `Inventory` — a fixed number of slots per player, each holding an item id and a count. `TryAdd`,
  `Remove`, `Has`, `CountOf`, `SlotAt`, `Clear`. Events on change so UI can subscribe.
- Refuses overfill and unknown ids the same way the ledger refuses overdraft — return a result, don't
  throw.
- `ItemCatalog` ScriptableObject holding `InventoryItem` entries: `ItemId` (int, stable, never
  renumbered), `DisplayName`, `Icon`, `Stackable`, `MaxStack`, and a `UseContext` enum of
  `Throwable` / `LaneAction` / `Instant`.
- Acquisition paths: granted by the ball return, and purchasable from a `Vendor` (a vendor item can
  now carry an `ItemId` to deposit into inventory).

**Done when:** EditMode tests cover add/remove/stack/overfill/unknown-id, and the suite is green.

**STOP.**

---

## Stage 4 — Throw loadout

Item selection becomes the first beat of the throw sequence.

**Scope:**

- Before aim, the thrower picks what they're throwing: the ball by default, or any `Throwable` item
  in their inventory.
- **Zero friction when the inventory is empty.** No item, no prompt — the throw sequence is exactly
  what it is today. This has to be true or every throw in the game gets taxed.
- A throwable item consumes on use and follows the existing launch-parameter path — same struct, same
  seed, same network message, same camera sequence. Different mass, drag and pin interaction per item,
  read from the catalog.

**Done when:** a player holding an oil can can select it, throw it, and watch it travel the lane using
the existing throw camera. A player holding nothing sees no change at all.

**STOP.**

---

## Stage 5 — Lane conditioning

The field the sabotage items write into.

**Scope:**

- `LaneConditioning` — pure C#. A sampled field down the lane, each sample carrying a conditioning
  value from dry through neutral to slick. `Apply(downLaneStart, downLaneEnd, lateral, amount)` to
  lay or remove conditioning; `SampleAt(downLane, lateral)` to read it.
- `BowlingBall` samples it and scales hook force accordingly. Slick reduces bite so the ball skates;
  dry increases grip so it hooks harder. Use `LaneFrame.DistanceAlong` and `LaneFrame.LateralOf` for
  coordinates — that's what they're for.
- **Breakdown:** every ball thrown through a conditioned patch shifts it back toward neutral. Real
  lane behaviour, and it gives the effect a natural half-life.
- Visible on the lane. Make the slick unmissable — a glossy patch, glowing under the venue blacklight.
  A player should be able to see exactly what they're bowling into.
- Tunables in a `LaneConditioningConfig` ScriptableObject: strength, spread, breakdown rate.

**Done when:** oil can be applied by a debug key, is visible on the lane, measurably changes hook, and
fades as balls roll through it. Tests cover apply, sample and breakdown.

**STOP.**

---

## Stage 6 — The janitorial set

The sabotage items themselves, on top of Stages 3–5.

**Items:**

- **Oil can** — `Throwable`. Lays slick down a section of lane. Less bite, less hook.
- **Rosin bag** — `Throwable`. The opposite: over-dries the lane so the ball grips and hooks wildly.
- **Floor wax** — `Throwable`. Targets the approach rather than the lane; attacks the slide.
- **The mop** — `LaneAction`. Clears conditioning. **Usable by spectators**, not just the thrower:
  a roaming player can pick it up, walk onto the lane and mop, on a timer, in the open.
- **Wet floor sign** — `Throwable`, no mechanical effect whatsoever. It exists to be placed as a
  bluff.
- **Loose pin** — `Throwable`. Sabotages the rack instead of the lane.

**Rules:**

- Sabotage throwables are usable on the **second throw of a frame only**. A strike ends the frame, so
  bowling well means no sabotage that frame.
- Conditioning persists into following frames and decays through use.
- All of it sits behind the Friendly / PvP / Chaos preset dial.

**Done when:** a player can oil the lane on their second throw, the next player visibly bowls on a
slick lane, and a spectator can mop it clear.

**STOP.**

---

## Stage 7 — Slot machine

**7a — the engine.** Pure C# `SlotMachine` mirroring `BlackjackTable`'s shape: reels, paytable, a
`Pull(TicketLedger, playerId, stake)` taking the stake via `RequestSpend` and paying via `Award`.
Driven by `DeterministicRng`.

Tuning lives in a `SlotConfig` ScriptableObject: reel strips, paytable, payout frequency. Aim for
mostly-nothing with a fat tail — a jackpot should be an event the whole alley hears. Losses resolve
fast and curt; wins are long and loud.

Reel symbols are pins, beers and Tony's face.

EditMode tests to the `BlackjackTests` standard, including payout distribution over N pulls.

**7b — the station.** The machine in the casino nook. Full-body lever pull.

**7c — the Loser's Machine.** One machine that only accepts players currently in last place and pays
triple.

**Done when:** a player can pull the lever and the alley hears a jackpot. Tests cover the paytable.

**STOP.**

---

## Stage 8 — Performance payout table

**Scope:** a `PayoutConfig` ScriptableObject — what a strike, spare, frame win and final rank pay.
Awards fire automatically from match results through `TicketLedger.Award`. No action to collect.

**Done when:** completing a match awards tickets matching the config, and `TotalInCirculation` moves
only by the specified amounts.

**STOP.**

---

## Stage 9 — Physical tickets

**Scope:** payouts spill physical tickets into the world at the lane kiosk — paper, everywhere. They
rain on the winner, hit the floor, get walked through.

- A configurable fraction (default ~20%) is **pinchable** — any player can pick it up.
- Preset-gated behind Friendly / PvP / Chaos, default OFF.
- Pickup goes through `TicketLedger.Award`. A ticket on the floor is a pending award, not a balance
  mutation.
- **Optional and worth trying:** render a player's balance as a physical ticket streamer trailing
  behind them, so wealth is visible across the room and being rich looks ridiculous.

**Done when:** a strike scatters tickets, a spectator can pinch some, the dial turns it off cleanly,
and `TotalInCirculation` only ever changes through the ledger.

**STOP.**

---

## Stage 10 — Save & persistence

New infrastructure. Nothing implements it yet.

**Scope:** persist ticket balances, inventory contents, cosmetics owned and equipped, and arcade high
scores across sessions. The save is the host's.

**Done when:** a balance and an inventory survive quitting and relaunching, and a missing or corrupt
save produces a clean new profile rather than an exception.

**STOP.**

---

## Stage 11 — The arcade

Presence and score sync only — cabinet game state doesn't replicate.

**Three cabinets:**

- **"WEE SPURTS"** — a 2D 8-bit port of this game with terrible physics, about eleven seconds long.
- **The trainer** — a spin and timing drill for the real throw.
- **The spectate cam** — shows the current thrower rendered in 8-bit, so you can still watch and
  heckle from the arcade.

Each play costs tickets. High scores persist via Stage 10.

**Done when:** a player can stand at a cabinet, others see them there, a play costs tickets, and a
high score survives a relaunch.

---

## Smaller stations — any time after Stage 2

One component plus one config asset each:

- **DJ booth** — pay tickets to blast a horn or change the venue track.
- **Snack bar** — food counteracts the drink meter.
- **Front desk** — shoe rental, and the home for house-rule presets.
- **The bar tab** — drinks on credit, settled from winnings at match end. Accumulate outside the
  ledger, settle with a single `RequestSpend`.
- **Restrooms** — sober up faster.
- **Vending machine** that occasionally eats your tickets and gives nothing.

---

## Two additions to existing systems

- **The dealer cheats, and the rail can call it.** He palms a card in plain sight; any player,
  including spectators, can shout him down with a taunt button. Catching him wins the hand; a wrong
  accusation costs double.
- **Some slot jackpots pay in chaos, not tickets.** The lights drop and a disco ball descends for a
  frame; or everyone's next ball becomes a Wobbler; or a venue-wide horn fires.

---

## Definition of done for the buildout

- Every pure C# engine has EditMode tests and the suite is green.
- `TicketLedger.TotalInCirculation()` only moves through an explicit `Award`, `RequestSpend` or
  `Transfer`.
- No scene or prefab YAML was hand-edited. All scene changes went through a re-runnable editor tool.
- `Docs/GameBible.md` has change-log lines covering: tickets as the single currency, the inventory
  system, lane conditioning and sabotage, and physical tickets.
- Every stage ended with "now do X in the editor and expect Y", confirmed before the next began.

## Before you write anything

Restate the task, list your assumptions, and list the files you'll add or change for **Stage 1 only**.
Wait for a go-ahead. Don't scope past Stage 1 in your first plan.
