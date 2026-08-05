# Prompt: design the bones of every mechanic

For a creative design pass, not a production plan. Paste everything below the line into Fable.

---

I'm designing a game and I want you to think with me, not organise me. Be a creative collaborator:
research things, propose mechanics, invent, connect systems to each other, disagree with me, and
follow interesting threads. I'd rather you be provocative and occasionally wrong than safe and
useless.

## The game

**Wee Spurts** — an online party game for Steam. The reference points are Wii Sports for readability
and the "friendslop" genre for chaos: Lethal Company, Content Warning, PEAK, Golf With Your Friends.
Games built for four friends in a voice call, where the social interaction matters more than
mechanical depth, where deliberately rough art is accepted, and where the game sells through 15-second
clips rather than trailers.

The setting is a bowling alley you walk around in. Bowling is the first minigame, but the alley is a
whole social space: a bar, a casino nook with slot machines, a blackjack table with a card dealer, a
cosmetics counter, seating pits where your friends sit and heckle you. You free-roam it. When it's
your turn you get dragged to the foul line against your will, with visible comic dread on your face,
ball already in hand.

The intended feel: **grounded human-fumble chaos.** The ball always behaves like a real bowling ball —
a bad throw means *you* fumbled it, not that physics broke. Mistiming your release distorts direction
and spin on a deliberately non-linear curve: a small miss wobbles, a big whiff is spectacle. Failures
are authored into named, recognisable archetypes — the Hook, the Chip, the Overcook, the Whiff — so
the table reads them instantly and starts yelling "he Chipped it again." Skill buys consistency and
style, never scoreboard domination.

## What exists today

A complete single-machine bowling game: ten-frame scoring, a timing-meter throw with a green zone, a
two-axis spin selector, the fumble archetypes above, one powerup ball (a "Nuke Shot"), a six-beat
Wii-style throw cinematic, a rigged character with animations, and first-person roaming through a
full greybox venue with every location above already built and anchored.

The networking architecture is proven: players roam continuously while throws sync as compact
"launch parameters" replayed identically on every machine. Physics drift measured at zero.

So the skeleton is real. **What's missing is the game.**

## What I want from you

This is the phase where I figure out the **bones of every mechanic** — economy, gambling, cosmetics,
customization, drinking, animation and character performance, the card dealer, slots, blackjack,
playing cards, powerup balls, heckling, and whatever else should exist that I haven't thought of.

I don't want a schedule. I want to understand what this game *is* across all of these, and I want a
set of design territories I can then dig into one at a time.

### Start with the loop, not the parts

My instinct is that the coin economy is the connective tissue between everything on that list. Coins
come from somewhere, go somewhere, and every system either feeds or drains them. Bowling well earns;
betting risks; slots drain; drinks cost and impair; cosmetics are status; cards buy chaos.

**Design that loop first.** Where does money come from, what makes it worth having, what drains it,
and what stops the winner running away with it? If you think a coin economy is the wrong spine
entirely — that the real connective tissue is something else, like reputation, or drunkenness, or
debt — argue for that instead. That's exactly the kind of disagreement I want.

Then work outward into the individual systems.

### Territories to explore

Not exhaustive, not an outline to fill in. Find your own, merge these, split them, throw some out:

- **The economy** — sources, sinks, whether coins persist between sessions, what stops inflation,
  and crucially *why a player cares about a fake coin*.
- **Gambling, plural.** Betting on a friend's throw, slots, blackjack, the card dealer — these are
  four completely different risk shapes with different social textures. Betting is interpersonal;
  slots are solitary and stupid; blackjack is a table game that could pull spectators together while
  someone else bowls. What is each one *for*?
- **Cards and powerup balls.** I have one powerup (the Nuke). I want a family of them, and I have a
  loose idea that cards are how you get them. What's the acquisition loop? Do you hold a hand? Play
  them on yourself or on other people? Is the card dealer an NPC, a minigame, or a vending machine
  with personality?
- **Drinking.** A satirical drink meter that comedically degrades your throw. The tension: a locked
  rule says a bad throw must read as *the player's* incompetence, and being drunk is something the
  player chose but the impairment isn't. How do you keep drunk throws funny and owned rather than
  arbitrary? What does drinking buy you that makes it worth the cost?
- **Customization and cosmetics.** What is expressed — status, in-jokes, humiliation? Can other
  people put things on you? Is there anything better than a hat shop?
- **Animation and character performance.** This is underrated and I want it taken seriously. The
  comedy lives in faces and body language: dread on the drag to the line, triumph, despair, the
  heckle. What performance moments are worth authoring, and which do the most work per unit of
  effort?
- **Heckling and interference.** Spectators have physical presence near the lane. What can they
  actually do? Where's the line between funny and infuriating?
- **The turn as a unit of drama.** Someone throws every 30-60 seconds. What is everyone else *doing*
  in that window, and how does the game make that window the best part rather than dead air?
- **Bowling itself.** Is the throw deep enough to sustain a whole game, or does it need another
  dimension?
- **What carries between sessions**, if anything.

### For each territory, I want

Depth over coverage. For the ones you find most interesting:

- What the mechanic is actually *for* — the experience it creates, not the feature list.
- Two or three concrete proposals, including at least one that's a bit reckless.
- How it hooks into the other systems. Isolated mechanics are dead weight; I want a web.
- What could make it *funny*, specifically. Not "it's fun" — what's the joke, what's the moment
  someone clips.
- The failure mode. How each one turns tedious, cruel, or exploitable, and what prevents that.
- Open questions you'd want answered by playing rather than arguing.

### Research freely

Look at how other games solved these problems and tell me what you find — party game economies,
gambling minigames in non-gambling games, social deduction and sabotage systems, how the friendslop
canon handles downtime, why some party games stay funny on the tenth session and others die on the
second. Bring in comparisons I wouldn't think of, including from outside games entirely. Cite what's
worth citing.

One genuinely useful research thread: **Steam's content rules and age ratings for simulated gambling
and alcohol.** I have slots, blackjack, betting and a drink meter, with no real money anywhere. I need
to know what that costs me in rating and disclosure, and whether any of it would shape the design —
better to know now than after the store page.

## The few things that are actually fixed

Hold these lightly, but don't design around them being false:

- **Four friends in a voice call** is the target. Not solo, not sixty players.
- **The clip test.** If fifteen seconds of it isn't worth posting, it probably isn't worth building.
  This is how these games actually sell.
- **The bad throw is the player's fault**, legibly. Chaos is authored into recognisable human
  failure, never "the game did something random."
- **Skill buys style, not domination.** A good player should be a pleasure to watch.
- **Two beginner developers with evening time** and an AI pair-programmer. Don't self-censor ambitious
  ideas — but flag when something is a weekend versus a month, so I can decide.

## How to work with me

Lead with your **strongest ideas**, not a balanced survey. Tell me which two or three things you'd
build first if it were your game, and why.

Push back on things I said here. I've been wrong before and the good version of this conversation
involves you telling me so.

If you have questions that would change your answer, **ask them before you write** — I'd rather
answer three good questions than read a plan built on a guess.

End with the design territories laid out as a set of things I can come back and dig into one at a
time, so this becomes the start of a series rather than one document I read and forget.
