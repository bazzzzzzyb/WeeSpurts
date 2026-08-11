# Venue economy, casino & arcade — SUPERSEDED, safe to delete

> **Dead doc, 2026-08-11.** Everything actionable here moved into
> `Docs/Prompts/2026-08-11-venue-economy-buildout.md`. The rest is wrong now: it argues tickets should
> *not* be a currency (they are — they're the only currency), and it carries the design-law table and
> age-ratings section that have since been withdrawn. Nothing should reference this file. Delete it.

Written against the actual repo state (`C:\Dev\WeeSpurts`, HEAD `e6f3f50`), `Docs/Projects.md`,
`Docs/DesignTerritories.md`, `Docs/SlopLayerPlan.md` and `Docs/GameBible.md`. Nothing here is built.
This is the menu to approve from before a line of code gets written.

---

## 1. What already exists (so we don't rebuild it)

**Built, tested, pure C# — the foundation is genuinely done:**

- `Gameplay/CoinLedger.cs` — the one choke point. Register / BalanceOf / CanAfford / RequestSpend /
  Award / Transfer / TotalInCirculation, `OnTransaction` + `OnRefused` events, no overdraft, no
  balance setter, `MAX_BALANCE` ceiling. Keyed by `ulong` (SteamId-ready).
- `Slop/Vendor.cs` + `VendorItem` + `VendorConfig` — priced items, per-match stock, ids not
  references, refuses zero-priced items as misconfiguration.
- `Slop/BlackjackTable.cs` + `BlackjackHand` + `BlackjackRules` + `BlackjackConfig` + `PlayingCard` —
  one seat, hit/stand only, 6-deck shoe, configurable 3:2 payout and soft-17, reshuffle guard.
- `Core/DeterministicRng.cs` (xorshift32 — chosen over `System.Random` deliberately for the shoe).
- `Gameplay/EconomyConfig.cs` (one knob: `StartingCoins = 500`).
- `Interaction/` — `IInteractable`, `PlayerInteractor`, `InteractionPromptHud`, and
  `Bowling/LaneKioskInteractable.cs` as the worked example of a station.
- EditMode tests: `CoinLedgerTests`, `VendorTests`, `BlackjackTests` (137 green per the Bible).
- `Bowling/LaneFrame.cs` — the lane-axis fix landed. ThunderLanesVenue bowling is unblocked.

**Not built:** slots, arcade, tickets, the performance payout table, the round-wager flow, save/
persistence, and — critically — **none of the above is reachable in-game.** Nothing instantiates a
`Vendor` or a `BlackjackTable` in any scene. The economy is a library with no doors.

**The real blockers, both flagged in your own docs:**

1. **`Seated` ControlMode doesn't exist.** Sitting at the blackjack table means editing
   `PlayerAvatar.ApplyMode` — the chokepoint whose whole value is that nobody edits it casually. The
   Bible says this was deliberately left for a session of its own *with Tony's explicit sign-off*.
2. **No save system.** Arcade high scores, persistent tickets and owned cosmetics are all downstream
   of infrastructure that doesn't exist. `Projects.md` J lists it; nothing implements it.

---

## 2. The laws every idea below has to obey

Pulled from your own docs, because three of them rule out obvious ideas:

| Law | Source | Consequence |
| --- | --- | --- |
| **One currency, never coins *and* tickets *and* chips** | `Projects.md` Direction | Tickets-as-currency is already rejected. See §3. |
| **The room orbits the lane.** The moment the arcade is more fun than bowling we've built a different game | `Projects.md` Direction | Every station is a place to *wait*, not a destination. |
| **Casino EV negative, lane EV positive** | `DesignTerritories.md` §0 | If the casino out-earns the lane, players optimise away from the centrepiece. |
| **Parody, not simulation** | `DesignTerritories.md` §3, §11 | Ratings *and* comedy point the same way. Build fantastical from day one. |
| **The attention test:** what does this let you say about the person currently throwing? | `DesignTerritories.md` §0 | A feature that gives a spectator nothing to say is dead weight. |
| **Every station is a stance** — rail = engagement, bar = bravado, slots = sulking, blackjack = truancy | `DesignTerritories.md` §9 | New stations must add a stance, not a menu. |
| IDs never references; request-then-decide; one choke point | `SlopLayerPlan.md` | Non-negotiable, already honoured by existing code. |
| Never hand-edit `.unity` / `.prefab` YAML | `CLAUDE.md` | Integration must be an **editor setup tool**, not scene surgery. |

---

## 3. The tickets problem — and two ways out

You asked for tickets as a currency. `Projects.md` explicitly forbids a second currency, and it's
right to: two currencies means two balances, two exchange rates to tune, and the classic
free-to-play smell this game is satirising rather than imitating.

But "no second currency" doesn't mean "no tickets." Two options, pick one:

### Option T1 — Tickets are a trophy, not money (recommended)

Arcade cabinets spit a **physical paper streamer** your character drags behind them, visibly, growing
as you play. It buys **nothing**. It is a status object and a shame object at the same time: everyone
can see at a glance who's been hiding in the arcade instead of bowling. Longest streamer at the end
of the night gets a title or a hat.

Why this is the better idea: it *passes the attention test* (it's a thing to say about someone —
"he's got forty feet of tickets and he's last place"), it needs **zero ledger changes**, and it makes
the arcade self-policing without a cooldown system. The docs' stated failure mode for the arcade is
someone disappearing into it all night; this makes disappearing into it the joke rather than a
problem to patch.

### Option T2 — Tickets are a sink receipt, one-way

Cabinets pay tickets; the **redemption counter** converts tickets to coins at a deliberately
insulting rate, with a long, slow, un-skippable count by a bored NPC. Tickets never buy anything
directly, so there is still exactly one *spendable* currency. The joke is that the arcade is a
visibly bad investment.

Weaker than T1 — it's still a second number to tune, and the conversion rate becomes a balancing
chore. Include it only if you want the redemption-counter bit specifically.

---

## 4. The one big idea: coins are physical objects anyone can pick up

`DesignTerritories.md` §1 already says payouts are physical — coins fountain out of the lane kiosk,
rain on the winner, spill on the floor, get walked through. Take that one step further and make it a
rule rather than a particle effect:

**Spilled coins are real, and anyone can pinch them.**

A strike payout that scatters means a spectator can sprint in and grab your winnings off the floor.
Suddenly:

- Wealth is spatial and mockable, exactly as §1 asks — a rich player *looks* rich.
- Every room is part of the economy, because coins are objects in the world rather than a number in a
  corner.
- It creates the single best spectator verb in the game, and it costs no UI whatsoever.
- It gives the *losing* player something to do that isn't sulking.

**Risk, stated plainly:** this is griefing with a bow on it. Gate it behind the existing
Friendly / PvP / Chaos preset dial (`Projects.md` G), default it OFF, and cap the pinchable fraction
(e.g. only the 20% that spills, never the banked payout). Start restrictive and loosen — the same
guidance §6 gives for interference.

This is the idea that ties economy, roaming, spectating and comedy into one system. If only one thing
on this page gets built, make it this.

---

## 5. Slots — make the machine the comedian

`DesignTerritories.md` §3 already has: pins/beers/Tony's face on the reels, visible rigging, a
full-body lever pull, and **the Loser's Machine** (only accepts players in last place, pays triple).
All good, all kept. Four additions:

**5a. Some machines pay in chaos, not coins.** A jackpot drops the alley lights and lowers a disco
ball for one frame; or turns everyone's next ball into a Wobbler; or triggers a venue-wide horn. This
directly fixes the doc's own complaint that slots are solitary — a payout the whole room has to react
to makes the sulking player's sulk *load-bearing*. Passes the attention test outright.

**5b. Invert the casino's psychology — and say so as a law.** Real machines are engineered around the
*near miss* (two jackpot symbols and a third just barely off) because it drives compulsion. That's the
single mechanic to deliberately not copy: it's the one regulators actually care about, and it isn't
funny. Instead: **losses are instant and curt** (reels slam, machine makes a dismissive noise, done in
under a second) and **wins are absurdly long and loud.** Generous with spectacle, stingy with money.

**5c. Tune volatility, not RTP.** For a party game the RTP number barely matters; the *shape* does.
Mostly nothing, rarely enormous — roughly 85% of pulls paying zero with a fat tail is funnier than
frequent small wins, because only a jackpot is a broadcast event and small wins are noise. Paytable
lives in a ScriptableObject so it's tunable without code.

**5d. The machine won't let go.** If your turn comes up while you're at the slots, the lever holds
your character for ~3 seconds of comedy tug-of-war before releasing. Gives slots the same "dragged
away mid-hand" beat `Projects.md` C wants for blackjack.

---

## 6. Blackjack — it's built; make it social and reachable

The rules engine is done and tested. What's missing is a chair. Two additions beyond wiring:

**6a. The dealer cheats visibly, and you can call it.** He palms a card in plain sight. Any player —
including spectators — can hit a taunt button to shout him down. Catching him wins the hand; a wrong
accusation costs double. This converts blackjack from truancy (a solitary stance) into theatre the
rail can participate in, and it's the clearest possible "mitigating fantastical element" for the
ratings question in §9.

**6b. Getting dragged away mid-hand** (already in `Projects.md` C) — the dealer plays your hand for
you, badly. Needs turn authority first.

**Prerequisite:** the `Seated` ControlMode. This is the gate on the entire casino nook.

---

## 7. Arcade — cheap, stupid, and secretly useful

`Projects.md` D wants cabinets, ~11-second games, high scores, presence-only sync, coin sink. Three
ideas that make it earn its floor space:

**7a. The cabinets are bad ports of your own game.** A cabinet literally called *WEE SPURTS* running a
2D 8-bit bowling game with terrible physics. Nearly free to build (no 3D, no rig, no camera work),
instantly funny, and it's a joke about the thing you're standing inside.

**7b. One cabinet is a practice trainer.** A spin/timing drill for the real throw. This is diegetic
onboarding — it teaches the mechanic without a tutorial screen, and it gives the arcade a reason to
exist beyond draining coins. Cheapest solution to a problem you haven't hit yet but will.

**7c. One cabinet watches the match.** A "spectate cam" showing the current thrower rendered in 8-bit,
so the arcade isn't a total attention sink — you can still heckle from it. Guards the
"room orbits the lane" law directly.

---

## 8. Smaller stations already sitting in the venue art

The venue has zones built and named (`WestWing_Zones`, `CenterSpine_Concourse`, `EastWing_Zones`).
Each of these is a one-component-plus-config job once the station pattern exists:

- **DJ booth — pay coins to blast a horn or change the venue track.** The purest possible form of
  "buy attention," which is the doc's own stated connective tissue. Probably the single cheapest
  good sink in the whole venue, and it needs no new systems.
- **Snack bar — food counteracts the drink meter.** Gives S3 a counter-play, makes chips a defensive
  item, and it's funny. Real mechanical purpose, tiny build.
- **Front desk — shoe rental as a mandatory opening sink.** A joke every player instantly gets, and a
  natural home for house-rule presets.
- **The bar tab.** Drinks on credit, settled from winnings at end of night. **Do not add overdraft to
  the ledger** — the no-negative-balance rule is load-bearing. Instead the tab is a Vendor with
  deferred settlement: accumulate what's owed outside the ledger, then one `RequestSpend` at match
  end. Can't pay? You get a humiliation cosmetic instead of a debt. Rule 1 stays intact.
- **Restrooms — sober up faster.** One interactable, one timer, one joke.
- **Vending machine that occasionally eats your coins and gives nothing.** One-line gag, real sink.
- **Mechanical catwalk — somewhere you're not supposed to be.** Pinsetter sabotage, preset-gated.
  Only if interference gets built.

---

## 9. Ratings risk — worth knowing before building, not after

`DesignTerritories.md` §11 tracks the Balatro precedent. Current status, checked August 2026:

- PEGI announced a classification overhaul on **12 March 2026**, effective **June 2026** for newly
  submitted games — the biggest change since the 2019 in-game-purchases descriptor.
- The **granular gambling-themed criteria** PEGI promised after the Balatro and Luck Be A Landlord
  reclassifications in Feb 2025 — the ones that would formally separate fantastical mechanics
  (~PEGI 12) from realistic casino simulation (PEGI 18) — **still have not been published.** Social
  casino games and comparable titles remain likely PEGI 18.
- The new criteria do add: paid random items (loot boxes, gacha, card packs) default to **PEGI 16
  minimum**. Not applicable here — no real money anywhere — but worth never becoming applicable.
- ESRB's **Simulated Gambling** descriptor turns on *the presence of a wager*, explicitly not on real
  money. Slots + blackjack + round wagers will very likely earn that descriptor regardless of how
  parodic it is. That's a descriptor, not a rating ceiling — accept it knowingly.

**Practical takeaway:** the parody direction is already correct and is the only available mitigation.
Two things follow. Build fantastical elements in from the first commit rather than as a retrofit
(pin-and-beer reels, a dealer who cheats, cartoon rules, no realistic casino furniture or chip
denominations). And **never implement the near-miss reel** — it's both the compulsion mechanic
regulators focus on and the least funny option on the table.

---

## 10. How to integrate it without scene surgery

`CLAUDE.md` forbids hand-editing `.unity` YAML, and `ThunderLanesVenueBowlingSetupTool.cs` is the
established pattern for getting around that. So:

**A `VenueStation` component family + one editor setup tool.**

- Each station is one small `MonoBehaviour` implementing `IInteractable` (the `LaneKioskInteractable`
  pattern), reading its numbers from a ScriptableObject.
- Every one of them talks to `CoinLedger` and nothing else touches a balance.
- A `ThunderLanesVenueStationSetupTool` places anchors and components into the named venue zones —
  idempotent, re-runnable, reversible, exactly like the bowling setup tool.
- Slots get a **pure C# `SlotMachine` engine** with EditMode tests, driven by `DeterministicRng`,
  mirroring `BlackjackTable` exactly. Reels, paytable and volatility in a `SlotConfig` asset.

This keeps every new system unit-testable without pressing Play, which is the standard the rest of
the codebase already holds to.

---

## 11. Suggested order

`CLAUDE.md` golden rule 1 is one system per session, so this is sequenced, not a single job:

1. **`Seated` ControlMode** — needs Tony's explicit sign-off. Gates the whole casino nook.
2. **Station pattern + setup tool** — makes the bar and blackjack reachable. First playable economy.
3. **Slot machine engine + tests**, then its station. Chaos payouts (5a) after the coin version works.
4. **Performance payout table** — the lane has to be the main coin source before any sink is tuned,
   or the EV law inverts by accident.
5. **Physical coins** (§4) — the big one. Needs the payout table to exist first.
6. **Save system** — the gate on tickets, high scores and persistent cosmetics.
7. **Arcade cabinets** — after save exists, or high scores evaporate.
8. **Tickets** (T1) — last, because it's decoration on top of the arcade.

Steps 1–4 are buildable solo. Step 5 onward wants a real table of people to judge.
