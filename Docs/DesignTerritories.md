# Design Territories — the bones of every mechanic

> ## ⚠️ PARTIALLY SUPERSEDED — read `Docs/Projects.md` first
>
> Reviewed by Tony 2026-08-03. This document remains valuable — its core reframe (the connective
> tissue is **attention**, not coins) stands and is the best idea in it. But four of its proposals
> were considered and **rejected or changed**. Where this file and `Docs/Projects.md` disagree,
> **Projects.md wins.**
>
> **1. Prop bets on failure archetypes — REJECTED.** Territory 2's core proposal. Ten frames × four
> players is forty betting decisions a night; that's admin, not a game. The doc names "bet fatigue"
> as the failure mode and proposes it anyway. **Replaced by:** automatic performance-based payouts
> (score, rank, strike bonuses) plus a single wager agreed at the start of the round, settled after
> frame 10. Same spectator payoff, no per-throw interaction cost.
>
> **2. Skins is the competitive mode — ADDED, not in this doc.** Frame = skin, ties carry to the next
> frame. Solves the strike-after-strike runaway problem that betting was reaching for: two players
> both striking is a tie, so the pot grows instead of someone banking a lead.
>
> **3. The night-out ledger / wallet reset — REJECTED.** Territory 1's central conceit. **Coins
> persist between sessions**, because people should want to come back to their cosmetics. This is
> knowingly harder — the economy now needs real balancing — but §1's anti-inflation and
> anti-runaway arguments no longer apply as written. Territory 8's "the alley remembers" idea
> survives on its own merits, not as the persistence answer.
>
> **4. The casino as pressure valve with a negative-EV design law — DOWNGRADED.** The casino is a
> **dumb coin sink**, structurally identical to the cosmetics shop. It doesn't need to integrate with
> anything. Negative EV is a tuning default, not a law. The §0 disagreement with `FeelIdeas.md` §G is
> therefore moot: the lane is the centre because the room orbits the lane, full stop.
>
> **Also added since:** an arcade — cabinets with high scores, local single-player toys, nothing
> syncing but presence and score.
>
> Everything else below — the attention spine, the drinking/§8 resolution, the reaction director, the
> heckling line, the ratings research — stands and is worth reading in full.

*A creative pass, not a production plan. Written 2026-08-03 against everything in the repo — Bible §8/§9 are treated as law, `BowlingFeelIdeas.md` §G decisions (ball return = randomiser, shop = cosmetics-only, host-gated presets) as standing positions I build on and occasionally argue with. Nothing here is decided by being written; each territory ends with what only playing can answer. This file is the index of a series — dig into one territory at a time and log the digs as `Docs/design/<territory>.md`.*

---

## 0. The argument up front

**Your coin instinct is half right, and the half that's wrong matters.** Coins are a spine, but they're not the *reason* anything is fun — they're the scorekeeping for the thing that is. The real connective tissue of this game is **attention**. Four friends in a call have exactly one shared spotlight, and every good system in Wee Spurts either aims that spotlight (betting forces you to watch the throw), fights for it (heckling, slots jackpots), or monetises it (coins are how the game keeps score of who commanded the room). Test every mechanic with: *what does this let you say about the person currently throwing?* A feature that gives a spectator nothing to say is dead weight no matter how good its economy hookup is.

**The frame that makes the economy design itself: a session is a NIGHT OUT.** Wallet resets every session, like poker night — everyone buys in, the night has winners and losers, and the ledger dies at closing time. This solves, in one move, four problems you'd otherwise balance forever: inflation (impossible — the ledger resets), why-care-about-fake-coins (the same reason poker night works with matchsticks: your *friends* take them and lord it over you *tonight*), winner-runaway (a fresh night is a fresh race), and grind pressure (nothing to grind). What persists between sessions is not wealth but **the room and your reputation in it** — trophies, cosmetics, records, scars (Territory 8). Coins are the night; the alley is the years.

**The three things I'd build first if this were my game:** the prop-bet system on failure archetypes (Territory 2 — it converts your existing fumble system into a spectator sport for free), the reaction director (Territory 7 — the game as its own camera operator is the single highest clip-value-per-effort idea in this document), and the drink round (Territory 5 — the cheapest social-pressure mechanic ever designed, invented by actual pubs).

**Strongest disagreement with the existing docs:** `FeelIdeas.md` §G frames the casino as the economy's engine ("win coins → buy cards → deploy chaos"). I think that's backwards. The *lane* is the engine — bowling and betting on bowling should generate and move most coins, because that's where the attention is. The casino nook is the *pressure valve*: where the losing player goes to catch up on variance and the bored player goes to make noise. If the casino out-earns the lane, players optimise away from the game's centrepiece and you've built a slots app with a bowling minigame. The fix is a design law, stated now: **expected value at every casino game is negative; expected value at the lane and the betting rail is positive.** The house always wins so the lane always matters.

---

## 1. The Economy — the night-out ledger

**What it's for:** making outcomes *cost* something, so the table has stakes to yell about. Not progression, not retention — stakes.

**Shape:** everyone starts the night with the same buy-in (say 100). Sources, in order of designed magnitude: bowling results (frame payouts, strike bonuses — steady drip), winning bets on other people (the big swings), casino wins (rare, loud). Sinks: losing bets, drinks and rounds, cards from the dealer, slots, cosmetic-counter impulse buys, and *interference fees* (Territory 6). Payouts are **physical** — coins fountain out of the lane kiosk and rain on the winner, spill on the floor, get walked through. Wealth must be spatial and mockable, never a number ticking in a corner. A rich player should *look* rich (coin pile at their settee seat?) so poverty is visible comedy, not private shame.

**The reckless proposal — debt.** You can't be locked out of the fun for being broke; broke players are exactly who the casino and the heckle economy need. So the alley extends credit: go negative and the **tab** opens. Debt has teeth, but comedic ones: the dealer's enormous cousin starts *standing near you*; the kiosk announces your balance to the room when you bet ("credit approved… again"); and at closing time debtors face a forfeit — mop the lane while everyone watches, wear the shame cosmetic into the next game, do the walk of shame with the reaction director filming. Debt turns bottom-of-the-economy into content instead of exclusion. Failure mode: real feels-bad if debts snowball — cap credit small (one round of drinks deep, roughly), and make every forfeit performative rather than mechanical.

**Anti-runaway, solved by player choice instead of rubber-banding:** the leader has no rational reason to touch negative-EV casino games; the loser has every reason. Variance is the trailing player's friend, and they choose it themselves — Mario Party's catch-up logic without the game visibly cheating. Protect this: no comeback mechanics that the game imposes. The comeback is always something the loser *did*, drunk, at the blackjack table, at 2am. That's the story they tell afterwards.

**Play-to-answer:** the right buy-in size (big enough that going broke is possible, small enough that it's funny); whether frame payouts or bet winnings should dominate a typical night; whether debt reads as funny or miserable with real friends.

## 2. Betting on throws — the attention engine

**What it's for:** making watching *mandatory fun*. This is the system that fixes turn-based downtime, and it should be the default thing a spectator is doing.

**The core idea — prop bets on the failure archetypes.** Not just win/lose. The kiosk offers a board every throw: *Strike (3x) · Spare (2x) · Hook (4x) · Chip (5x) · Overcook (5x) · Whiff (8x) · Gutter (3x)* — odds tuned by feel, displayed big. This does three things at once: it converts your named-archetype system (the Bible §8 crown jewel) into a spectator sport; it makes the archetype names *lore* ("I had five coins on the Chip and he DELIVERED"); and it makes bad throws pay somebody, so the room celebrates failure instead of just the thrower mourning it — which is the entire friendslop emotional design in one mechanic. The auto-clip: three players bet Whiff, the fourth bet Strike, and the strike lands — one winner screaming among the bankrupt.

**Betting on YOURSELF** is allowed and is the bravado mechanic — calling your shot. Pays slightly better than even (skill should buy style). The drunk parlay (Territory 5) multiplies it.

**Mechanics of the window:** a 10-second bet window opens when the thrower gets dragged to the line (the drag IS the betting bell — diegetic countdown). Bets are public the moment placed — secret bets kill the table talk that is the entire point. Spectators bet from wherever they stand via a radial menu; the kiosk shouts entries ("Braeden: 20 on the Hook"). The thrower hears everything. *That's* the psychological warfare: aiming while your friends audibly price your incompetence.

**Hooks:** feeds the economy (main coin mover), feeds heckling (a bet is a heckle with money on it), feeds cards ("Insurance" pays out when you gutter — Territory 4), feeds the reaction director (it knows who has money riding and frames them at impact — Territory 7).

**Failure mode:** bet fatigue — if betting every throw is optimal, it becomes admin. Prevent with friction: no reminder nags, walk-in-range or radial only, and let not-betting be a legible stance too ("nobody bet on Tony" is its own insult).

**Play-to-answer:** window length; whether odds should adapt to a player's actual record tonight (the kiosk learning "he Chips a lot" is hilarious but might be cruel); minimum/maximum stakes.

## 3. The casino nook — three risk shapes, three social textures

Slots, blackjack, and the dealer are not one system. They're three different answers to "what do I do while it's not my turn," and each earns its place only by its social texture. All three obey the house-always-wins law from §0. All three are **abstracted into parody, not simulated** — that's a ratings decision as much as a comedy one (see §11): [PEGI 18 is what casino-realistic simulation costs](https://www.gamespot.com/articles/balatros-confusing-rating-finally-changed-leads-to-europe-changing-how-it-rates-gambling-games/1100-6529673/); [Balatro got knocked down to 12 on appeal because of "mitigating fantastical elements"](https://www.gamedeveloper.com/business/balatro-s-contentious-pegi-18-rating-has-been-amended-thanks-to-mitigating-fantastical-elements-). Build the fantastical elements in from day one, not as a retrofit.

**Slots — solitary, stupid, and LOUD.** The purpose of slots is not gambling; it's *broadcast*. A jackpot is heard by the whole alley — sirens, coins hitting the floor, everyone's camera flinch. Slots are where the sulking player goes after a gutter ball, and the machine's job is to make their sulk audible and interruptible ("is he seriously on the slots again"). Make the machine itself the comedian: reels showing pins/beers/Tony's face instead of cherries; visible rigging (a hand reaches inside to hold a reel); a lever pull so violent it's a full-body animation. One reckless idea: the **Loser's Machine** — one specific slot that only accepts players currently in last place and pays triple. It becomes a walk of shame TO a machine ("he's at the Loser's Machine") that occasionally, gloriously, funds a comeback. Failure mode: someone AFKs the slots all night — cap pulls between turns, or make each pull cost attention (long animation, can't be skipped, your character visibly wastes their life).

**Blackjack — the second room.** Purpose: give the two players furthest from their turn something *communal*, so the game supports two centres of attention without dissolving the main one. Rules parodied to fit 30-second hands: dealt fast, hit/stand only (no splits — this isn't a card game, it's a conversation prop), and the dealer plays with visible contempt. The critical timing rule: **getting dragged to the lane mid-hand is the joke, not a bug.** Your character is yanked away and the dealer plays your hand for you — badly, always badly, with a shrug. You bowl while listening to your friends narrate the dealer busting your 20. Failure mode: blackjack pulling ALL attention from the lane — prevent by pausing the table automatically during the throw itself (dealer folds his arms and watches too; even the NPC obeys the attention spine).

**The dealer — not gambling at all: the chaos vendor.** He sells cards (Territory 4). His alcove is deliberately a squeeze (already built — the 0.83m exemption). The design insight worth keeping: **buying from the dealer is a public act with private contents.** Everyone sees you slip into the alcove; nobody knows what you bought. That's a paranoia generator on legs — "he visited the dealer before my turn, someone check my ball." Give him a personality: whispers, refuses eye contact, occasionally short-changes you and dares you to mention it. NPC, not vending machine — but an NPC with maybe six lines and two animations. Reckless: the dealer occasionally offers a **rigged deal** — a card at half price, announced to everyone EXCEPT the buyer's target. Failure mode: dealer visits becoming rote shopping — keep stock tiny and rotating so the visit is a gamble about what's even for sale.

**Play-to-answer:** whether blackjack actually coexists with the lane or cannibalises it (this is THE 4-player playtest question); slot pull frequency caps; whether the dealer's hidden-purchase paranoia lands or just reads as menu shopping.

## 4. Cards & powerup balls — two deliveries, one chaos budget

**Standing decisions I agree with and won't relitigate:** balls are never bought — the ball return is the randomiser, per-frame, with the Nuke rare (§G — this is genuinely better than a shop; watching your ball rise from the chute is free suspense every frame and nobody can farm the Nuke). Shop is cosmetics-only. Cards are host-gated by preset (Friendly / PvP / Chaos) — consent is what makes cruelty funny.

**Cards, sharpened:** hand limit of **two**, secret in hand, **public and diegetic when played** — you physically slap the card somewhere (on the ball return, on a friend's back) with a sound the whole alley knows. Three families, on a spice scale the presets slice:
- **Self-buffs** (mild): *Mulligan* (rethrow one roll, once a night), *Bumpers* (your gutters bounce, this frame, visibly — everyone sees the bumpers rise and boos), *Insurance* (pays out if you gutter — a bet against yourself, the coward's card, and the game says so on the card face).
- **Table cards** (medium — bets on the world): *Double or Nothing* (your next payout doubles or zeroes), *Splitter* (§G's 50% both-sides split — the coin-flip reveal is a drama beat: the card physically flips over the lane), *Everyone Drinks* (the round, weaponised — Territory 5).
- **Offensive** (hot, PvP/Chaos presets only): *Skip* (softened to *Stagger* in most presets — victim throws with a shrunken green zone instead of losing the turn; total skips feel worse than they look funny), *Butterfingers* (target's next power meter runs 30% faster), *My Ball Now* (swap your randomised ball with theirs — chaos without damage).

**Determinism note carried from §G:** every random card effect resolves from the throw seed, never live Random — the Splitter's coin flip must land identically on every machine.

**The reckless proposal — the Dead Man's Hand:** if you're in debt (Territory 1), the dealer offers you one free card, drawn blind, that you MUST play before the night ends. Debt buys chaos you didn't choose. This may be too cruel; it may also be the best story generator in the game. Preset-gate it into Chaos and find out.

**Failure mode:** cards becoming the game. They're seasoning — if a night's outcome is decided by card plays rather than throws, players stop watching throws and the spine snaps. Guards: hand cap, cost, cooldowns, and the §G rule that the core game must be fully fun with cards OFF.

**Play-to-answer:** hand size (2 vs 3), whether offensive cards need target-lockout (no double-targeting), the §G open question of whether card purchases and bets share one pool (my lean: yes, one wallet — more tension, less UI).

## 5. Drinking — the round is the mechanic

**What it's for:** voluntary self-sabotage as social performance, and the game's best tool for *pressuring other people*.

**The Bible-§8 tension, resolved:** a locked rule says misses read as the player's incompetence — but drunk impairment is chosen once and then applies itself, which risks reading as "the game did it." The fix is to put ALL drunk effects **before release, visibly, in the aim phase** — never on the ball mid-flight. Drunk = the aim slide develops a slow visible sway you fight; the green zone wanders a little; the spin dot drifts back toward where you last left it; the camera does a gentle lean. The player can see every distortion while aiming and throws anyway — so the fumble is still *authored by them*, at the bar, an hour ago, and finalised by them at release. The ball stays a ball. (The Wobbler-as-drunk-status-ball from §G still works for the heavy-drunk tier — a drunk player's ball return starts serving them the Wobbler, which is diegetic, legible, and already built.)

**What drinking BUYS (it must pay, or nobody drinks):**
1. **The drunk parlay** — bets you place on *yourself* pay a multiplier per active drink. Impaired AND leveraged: the ultimate risk stack, the clip that writes itself (three drinks deep, all-in on his own strike, sways, releases… ). This makes drinking a *strategy* the trailing player reaches for, which is exactly who should be drinking.
2. **Heckle license** — certain interference actions require a drink in hand (you can't throw a cup you don't have — Territory 6).
3. **The round** — the masterstroke pubs invented centuries ago: buying a round for the table is cheaper per-drink than buying solo, and each recipient gets a big two-button choice broadcast to everyone: **ACCEPT / DECLINE**. Declining is free and safe and *everyone sees you do it*. That's it. That's the whole mechanic. Social pressure does the rest, exactly like real life, and the game never forces anyone to drink — it just makes refusal legible. (Rating-wise this is the line to watch: pressure as comedy, never as requirement — see §11.)

**Tone guard:** it's satirical — "Wee Spurtz Brew," absurd foam, burps, hiccup-timed camera bumps. No vomit, no misery. The drunk state is a *performance mode*, not a debuff screen.

**Failure mode:** drunk-griefing yourself into uselessness stops being funny around throw three. Drinks wear off (per-frame decay), and the max-drunk state should be so theatrical (character supported by an invisible friend, heroic squint) that even uselessness performs.

**Play-to-answer:** the §8 question of whether the sway is fightable enough to feel owned; parlay multiplier size; whether DECLINE needs any cost at all (my bet: no — visibility IS the cost).

## 6. Heckling & interference — tax composure, never outcomes

**The line, drawn precisely:** spectators may attack the thrower's **composure** freely, and their **outcome** only through priced, preset-gated, telegraphed actions. Composure attacks: proximity (breathing at the foul line is legal), thrown cups and popcorn (cosmetic physics, bounce off harmlessly, but the *sound and sight* while aiming is real interference), taunts and emotes, standing at the pin end pulling faces. Outcome attacks: cards only (priced, gated, Territory 4). Nothing free ever touches the ball.

**The one they'll discover themselves — lean in now:** there are no lane barriers (Tony's call, already law), so a spectator can stand *in the lane as a human pin*. The ball hitting a human is the best clip in the game — full ragdoll, launched into the settee pit, crowd sting. Rules: a human hit scores zero pins but triggers a special "STRIKE?" announcement; the hit spectator's coins scatter physically out of them on impact (comedy AND a real cost, self-administered — griefing prices itself). The thrower loses nothing: getting your throw ruined by a friend's body is a rethrow, announced with maximum ceremony.

**Failure mode:** the fun-to-infuriating line moves with the group, which is why the host presets (already decided) are the right answer — Friendly mode can make the foul-line area a no-stand zone; Chaos mode can allow everything. Don't tune one universal line; ship the dial.

**Play-to-answer:** the §G question verbatim — how much interference is funny vs infuriating, per preset; whether human-pin needs a cooldown ("you can only die once per frame").

## 7. Animation & the reaction director — the game as its own camera operator

**Taken as seriously as requested, because this is the sleeper system of the whole game.** The comedy of this genre is consumed as clips, and clips live or die on *framing*. You already have a six-beat throw cinematic. The missing half is the **reaction director**: a camera brain that knows the story of every throw — who's throwing, who has money riding, who played a card, who's standing in the lane — and cuts to the right faces at the right beats. Pin impact → smash-cut to the bettor who just lost 40 coins, mouth open. Whiff → split-frame the thrower's despair with the Whiff-bettor's ecstasy. This *manufactures the composition streamers make manually*, in-game, every throw. Cost: camera logic plus a six-state face system (idle / effort / dread / triumph / despair / gloat) — modest. Value: it is the clip machine. Build the face sheet the moment the character look settles; the OpenQuestions entry about PEAK-style expressiveness already points here.

**The authored performance moments, ranked by work-per-laugh:** (1) the drag to the line with dread face — the signature, already designed, protect it; (2) reaction cutaways as above; (3) the outcome-keyed walk back — strut vs trudge vs carried-off-ragdoll; (4) the sit — plopping into the settee pit next to whoever just took your money; (5) drunk locomotion tiers. Everything else (dances, idles) is Mixamo shopping, cheap and last.

**One reckless idea: the instant replay board.** After a spectacular archetype (Whiff, human-pin, nuke), the alley's big screen replays it once from the director's best angle while play continues. Diegetic, self-owned humiliation infrastructure — the alley itself is your worst fan. Also quietly doubles as the built-in clip framing for anyone recording.

**Play-to-answer:** whether cutaways feel great or seasick at 4 players; replay board frequency before it wears out.

## 8. Cosmetics & the alley that remembers

**Shop stays cosmetics-only (decided, correct).** Ball skins are the main line (§G's list stands; crumpled paper remains the best one). Hats/tints v1. The two additions worth making:

**Humiliation cosmetics, applied by others.** The night's loser doesn't pick their look — the winner picks it for them, from a curated shame rack (dunce crown, tutu, a hat that is just a smaller bowling pin), worn until they win a frame next session. Consent via preset. This is what "can other people put things on you" should mean — cosmetics as a *verb between friends*, not a wardrobe.

**The alley remembers — this is the persistence answer.** Between sessions, the group's alley accumulates its own history: a plaque appears over the lane where someone bowled a 260; a scorch mark stays where a Nuke landed; the settee pit gets a little trophy shelf; the Loser's Machine gets a brass nameplate of its best customer. Implementation is cheap — decals and props keyed to a persistent stats file — and the payoff is enormous: the alley becomes *your group's place*, and coming back after a month feels like walking into your local. Wealth resets every night (Territory 1); the *room* is what compounds. That's what carries between sessions, and it's a better retention hook than any XP bar because it's made of stories the group already owns.

**Failure mode:** shame cosmetics in the wrong group are just bullying — preset-gate; and the memory system must record *events*, never rankings, or the alley becomes a leaderboard with wallpaper.

## 9. The turn as drama — the 30–60 second script

The window structure, as a repeating three-act beat: **(1) The Call** — the drag begins, the betting bell rings, ten seconds of public odds, shouted entries, card plays slapped down. **(2) The Throw** — all systems point one direction; blackjack pauses, the director rolls, the room watches (the game *makes* them: bets, cards, and the reveal of what ball the return served all resolve here). **(3) The Settlement** — coins physically fly, reactions cut, the replay board fires on spectacle, the walk back happens in whatever dignity remains. Then attention releases, and the alley's stations reabsorb people until the next Call.

Every station in the venue is a *stance toward the current thrower* — the rail is engagement, the bar is bravado, the slots are sulking, blackjack is truancy, the dealer's alcove is scheming. That's the test for any future station: what stance does it add? (The cosmetics counter, note, currently has no stance — it's a menu. Give it one: try-on visible to the room, so browsing mid-match is itself a flex or an insult.)

## 10. Is bowling deep enough? Yes — vary the house, not the sport

With timing, 2D spin, archetypes, randomised balls, drunk-aim, cards, and human pins, the throw has more live dimensions than most full games. The variety lever for launch is **house rules via host presets**, not a second sport: Golden Pin night (one pin worth 20 coins), Rising Tide (drink meter auto-fills each frame), Poverty Night (buy-in of 10), No-Bumpers-No-Mercy. Each is a config file, not a system. The second minigame (darts is the natural sibling — same launch-parameter shape) stays post-launch, as the roadmap already says; nothing in this document changes that.

## 11. Ratings & content — the researched reality

- **The Balatro precedent is your map.** PEGI rated Balatro 18 for gambling imagery in early 2024; the [appeal won in Feb 2025 explicitly because of "mitigating fantastical elements," landing it at PEGI 12](https://www.gamedeveloper.com/business/balatro-s-contentious-pegi-18-rating-has-been-amended-thanks-to-mitigating-fantastical-elements-), and [PEGI is now writing granular criteria that keep **18 for games simulating casino games as played in real casinos and betting halls**](https://focusgn.com/pegi-reviews-policy-on-simulated-gambling-after-balatro-age-rating-overturned). Translation for us: a *realistic* slots/blackjack sim risks PEGI 18; a *parodic, fantastical* one (pin-and-beer reels, a dealer who cheats, cartoon rules) has a defensible case for lower. Build the parody in from the start — it's the funnier choice anyway, which is the rare case of the ratings incentive and the comedy incentive pointing the same direction.
- **ESRB is calmer:** ["Simulated Gambling" is an allowed descriptor at Teen](https://www.esrb.org/ratings-guide/) — fake-currency casino play typically lands T, not M. Alcohol references likewise live at Teen with a descriptor. A T/PEGI 12-16 target is realistic for everything in this document if the casino stays parody and the drinking stays satirical-with-refusal-always-available.
- **Steam itself doesn't require a rating** — the [content survey](https://partner.steamgames.com/doc/gettingstarted/contentsurvey) requires honest disclosure of simulated gambling and alcohol, which may age-gate the store page in some regions. That's the whole near-term cost. The PEGI question bites for real if we ever port to console or mobile — so the cheap insurance is making the parody-not-simulation choice now, in the design, where it costs nothing.
- **Hard lines regardless:** no real-money path to coins, ever, in any direction; no card/loot mechanics purchasable with real money; drinking always refusable and never mechanically required. These also happen to be §G's existing rules — keep them sacred.

---

## 12. The series — dig order

Each of these becomes its own `Docs/design/` file when we dig, with the playtest questions promoted into `OpenQuestions.md` as they sharpen. My recommended dig order, spiciest-and-most-load-bearing first:

1. **The betting rail** (Territory 2) — archetype odds, windows, the kiosk voice. Unblocks the turn script.
2. **The reaction director** (Territory 7) — beats, face states, replay board. Unblocks the clip test for everything else.
3. **The night ledger + debt** (Territory 1) — buy-in, payouts, the tab, forfeits.
4. **Drinking & the round** (Territory 5) — sway model, parlay math, ACCEPT/DECLINE.
5. **Cards + the dealer** (Territory 4 + dealer half of 3) — the deck list, spice scale, dealer personality.
6. **The casino floor** (slots + blackjack halves of 3) — parody rules, timing, the Loser's Machine.
7. **Heckling & the human pin** (Territory 6) — the priced-interference table per preset.
8. **The alley that remembers** (Territory 8) — the event-memory schema, shame rack, trophies.
9. **House rules & modes** (Territory 10) — the preset format itself, since five other territories hang dials on it.

*Everything above obeys: host-authoritative (money doubly so), tunables in ScriptableObjects, chaos from the throw seed, misses read as the player's fault, skill buys style not domination, and the clip test. Where a proposal touches an `OpenQuestions.md` item, playing decides it — Tony calls it, the Bible records it.*
