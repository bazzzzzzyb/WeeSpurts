# Lane sabotage — the maintenance equipment is the weapon system

**Tony's idea, 2026-08-11. Committed.** Swap your throw for an oil can and leave the lane slick for
whoever throws next. A mop clears it.

Build order lives in `Docs/Prompts/2026-08-11-venue-economy-buildout.md`, Stages 3–6. This doc is the
design detail behind it.

---

## The mechanic

On the **second throw of a frame**, instead of a ball you can select an item from your inventory and
throw that. You forfeit the throw. The lane keeps whatever you did to it, and the next player bowls on
it.

An oil can throw **is a throw** — same launch-parameter path, same seed, same network message, same
camera sequence. Different mass, drag and pin interaction, read from the item catalog.

## Second throw only, and why that rule earns its place

A strike ends the frame. No second throw means no sabotage — **bowling well disarms you.**

The player who just struck can't oil the lane. The player who gutter-balled has a dead throw and
nothing to lose, and converts a ruined frame into everyone else's problem. The people who are behind
end up armed, but they pay a real throw for it rather than being handed a pity advantage.

Keep this as a hard rule. It's doing more work than it looks like.

## The mop belongs to the rail

If the mop cost the sabotaged player a throw, nobody would ever use it. They'd be spending a *first*
throw — full rack, can still strike, the throw the frame's scoring hangs off — to undo a *second*
throw the attacker had half-wasted anyway. Bowling on the oil would always be better, and the option
would sit in the menu looking like counter-play without being any.

**So spectators mop.** Any idle player can pick up the mop, walk onto the lane and clear it. No pin
cost — they weren't throwing. The cost is position and time: out in the open, on a clock, while the
thrower waits.

This gives idle players a job, and it makes "someone mop for me!" a negotiation. Whether anyone
actually helps you is a social fact about the table, which is content you don't have to build.

## Conditioning breaks down on its own

Real lane oil degrades as balls track through it. Copy that: **every ball thrown through a
conditioned patch shifts it back toward neutral.**

- Sabotage gets a natural half-life without an arbitrary timer.
- No escalation spiral — conditioning can't stack forever while people are throwing through it.
- "Just eat it" becomes a real third option. You take the penalty and partly clear the lane for the
  player after you, which raises the question of whether you want to.

## The set

One visual language, all janitorial.

| Item | Context | Effect |
| --- | --- | --- |
| **Oil can** | Throwable | Lays slick. Less bite, less hook, the ball skates through the break point. |
| **Rosin bag** | Throwable | The opposite — over-dries the lane so the ball grips and hooks wildly. |
| **Floor wax** | Throwable | Targets the approach rather than the lane. Attacks the slide. |
| **The mop** | LaneAction | Clears conditioning. Usable by spectators. |
| **Wet floor sign** | Throwable | Nothing. It exists to be placed as a bluff. |
| **Loose pin** | Throwable | Sabotages the rack instead of the lane. |

Rosin mattering as much as oil is what makes reading the lane a skill — the mop clears both, but you
have to know which one you're standing on before you decide whether it's worth clearing.

## Where items come from

Two paths, both live:

- **The ball return**, per frame — the alley arms you on its own schedule.
- **Vendors**, bought with tickets — a vendor item can carry an `ItemId` that lands in your inventory.

## Make the slick loud

The conditioning has to be unmissable — a glossy patch on the lane, glowing under the venue
blacklight, announced when it's laid. If you can see the oil and you hook it into the gutter anyway,
that reads as your mistake. If it's subtle, it reads as the game misbehaving. This is a presentation
requirement, not a nice-to-have.

## Loadout costs nothing when you're empty

Item selection is the first beat of the throw sequence, but **an empty inventory shows no prompt** and
the throw plays exactly as it does today. Otherwise every throw in the game pays a tax for a mechanic
that's only live some of the time.

## Technical shape

- **Lane conditioning** is a sampled field down the lane, host-owned, replicated. A client that can
  write lane state can write its own hook, so clients request and the host decides.
- `BowlingBall` samples it via `LaneFrame.DistanceAlong` / `LaneFrame.LateralOf` and scales hook
  force.
- Tunables — strength, spread, breakdown rate — in a `LaneConditioningConfig` ScriptableObject.
- Needs turn authority to answer "who throws next" reliably.

## Still to decide by playing

1. Does conditioning still be there when it comes back round to **you**? Tail risk makes sabotage a
   much more interesting decision.
2. Can a spectator mop **during** another player's approach, or only between throws? Mid-approach is
   funnier and far more disruptive.
3. Does the 10th frame allow sabotage? Three throws means more capacity in exactly the frame that
   should be settling.
4. Preset placement — on by default, or Chaos only?
