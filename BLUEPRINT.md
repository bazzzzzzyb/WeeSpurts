# Wee Spurts — The Blueprint

*The master plan. Written July 2026 after researching the friendslop market, validating the tech stack, and restructuring this repo for Claude Code. Read this once fully, then live in `Docs/` and `CLAUDE.md`.*

---

## 1. The verdict up front

Your instincts are good. The genre you're aiming at is real, still growing in 2026, and has repeatedly minted hits from tiny teams. Your tech stack is correct. Your "docs are law, one system at a time" discipline is exactly how AI-assisted development works when it works.

The honest part: the teams behind the hits you're emulating were *experienced* developers moving fast, not beginners. PEAK was built mostly in four weeks — by seven veterans from two established studios, on a sub-$200k budget, and it sold ~10 million copies. You will not move at that speed, and that's fine. Your edge is different: you have unlimited senior-engineer time via Claude Code, a genuinely underserved niche (turn-based party *sports* friendslop — almost everything in the genre is co-op horror/climbing), and no burn rate. The plan below is built around that reality: prove the fun cheaply, gate every expansion, and design every feature to be clippable, because short-form clips are how these games actually sell.

## 2. What the market research says

**Friendslop is a real, named genre and it's still hot.** The term appeared in March 2025, was reclaimed from an insult, and by 2026 is treated as the dominant indie multiplayer trend. The canon: Lethal Company (240k concurrent players), Content Warning (6.2M claims in 24h), R.E.P.O., PEAK. The consistent finding across all of them: **live social interaction matters more than gameplay depth or art**. Players crave communication, not competition. Deliberately minimal art is accepted — even expected.

**Clips are the marketing engine.** Content Warning succeeded because its core mechanic (filming your friends) *was* content creation. These games are consumed as TikTok clips and Twitch highlights first, purchases second. Per marketing consultant Chris Zukowski, ~90% of new wishlists come from online festivals and streamer coverage. Design implication: every Wee Spurts feature should pass the **clip test** — would 15 seconds of this be worth posting?

**Your closest comp is Golf With Your Friends** — 12-player turn-adjacent party sports, sabotage items (honey-trap your friend's ball, freeze it, cube it), ~50k Steam reviews at 88% positive, successful enough that a sequel ships fall 2026. This proves party-sports-with-sabotage sells on Steam. Notably: its players play *simultaneously*, which kills downtime. Remember that when bowling pacing feels slow (see §3).

**The launch playbook is known** (detail in `Docs/Marketing.md`): Steam page early → build wishlists over 6–12 months → demo into Steam Next Fest (Feb/Jun/Oct) → daily short-form clips during the fest → outreach to 5k–250k-follower creators → launch. This is a solved pipeline; the only hard part is having a game funny enough to clip.

**Content guardrails are manageable.** Steam's content survey requires disclosing simulated gambling and alcohol references; they raise the age rating more when central to gameplay. Fake-coin betting with no real money and a cartoon "drink meter" is survivable — many rated games do more — but disclose honestly, never connect coins to real money, and keep the framing satirical. Flag: check PEGI/ESRB implications before the store page, not after.

## 3. Direction — three honest pushes

**Push 1: Kill the downtime, don't celebrate it.** The Bible says "downtime between turns is a feature." Half-true. The *table talk* is the feature; *waiting* is not — and turn-based bowling for 6 people means each player is idle ~85% of the time. The genre's hits are all simultaneous-chaos games for a reason. Your betting layer is the right instinct (it gives spectators a job), but go further: spectators should have *physical presence and agency* during throws. Your own pitch already contains the answer — "throw a bowling ball at your friend as he's about to throw," "go over to the bar, grab a beer, hit some slots." That's not flavor text; that's the design. **The alley is a physical social space you walk around in** — heckle from the gutter, spend coins at the bar and slots, physically interfere (within comedic limits) with the active player. That single idea unifies your gambling, drinking, and chaos pillars, fixes the pacing risk, and is the differentiator no comp currently owns. Recommendation: promote it to a pillar and test it in the first prototype, not the slop-layer phase.

**Push 2: Physics floppiness is your biggest open feel question — answer it first.** Gang Beasts-style floppiness vs. Wii-style readable control defines your comedy, your netcode complexity, and your clips. Prototype the throw feel *before* building networking around it. A weekend of pure single-machine throw-feel iteration is the highest-leverage work in the whole project.

**Push 3: Aim for "funny with 4 friends," not "high-selling."** A high-selling Steam game is not a plannable outcome — it's the tail of a distribution. What *is* plannable: a game that reliably makes your own group laugh, shipped, clipped, and put through Next Fest. Every friendslop hit started exactly there. The Blueprint therefore optimizes for shortest path to the "first funny game" gate, and treats everything after (progression, second minigame, marketing spend) as conditional on passing it.

## 4. The tech stack — validated, locked

Mirror + FizzySteamworks + Facepunch.Steamworks + host-authoritative + launch-parameter sync: **correct, keep it.** Mirror remains the best-documented free option (claims 100M+ players served); the launch-parameter pattern is exactly right for near-turn-based bowling. Alternatives exist (FishNet, Unity Netcode for GameObjects + Facepunch transport) but switching buys you nothing and costs momentum. One technical caution the docs already half-know: PhysX is not bit-deterministic across machines, so "all clients simulate from the same parameters" will drift slightly — your existing rule (host's confirmed pin state is authority, snap at end of roll) is the correct mitigation. Keep it sacred.

## 5. How you build: the Claude Code operating manual

The old workflow (paste personas into chats) is retired; those files are archived in `Docs/archive/`. The new machinery, already in this repo:

- **`CLAUDE.md`** — auto-loaded into every Claude Code session. The rules live there now; no pasting.
- **`.claude/agents/`** — your five specialists (gameplay-engineer, steam-engineer, ui-engineer, physics-tech-artist, qa-engineer). Claude delegates to them automatically, or ask: "use the steam-engineer agent to…".
- **`/build-system <task>`** — starts a properly-scoped session (restate → assumptions → files → small steps → editor instructions → DoD check).
- **`/qa-review`** — audits recent changes for hallucinated APIs, network-trust violations, and edge cases, and hands you a runnable test checklist. **Run it after every networking/scoring/coin change.**

A working day looks like: open Claude Code in the repo → `/build-system Bowling: 10-frame scoring model` → follow the editor instructions it gives you → playtest → `/qa-review` → commit on a feature branch → merge when green. One system, small diffs, always testable.

Setup you still need (one-time, ~an hour): install Claude Code (`https://code.claude.com`), run it in this repo folder, and create the Unity project in `/Unity` via Unity Hub (latest LTS). Optional later upgrade: a Unity MCP server (e.g., Unity's official MCP, or IvanMurzak/Unity-MCP) lets Claude read console errors and editor state directly — worth adding once you're deep in Unity work, not required for day one.

## 6. The phases (gates, not dates)

Same dependency order as `Docs/Roadmap.md`, with the gates made explicit. Do not start a phase until the previous gate is passed. Effort notes are honest guesses for two beginners + Claude working evenings.

**Phase 0 — Rig up** *(a weekend)*: Claude Code installed and reading this repo; Unity LTS project in `/Unity`; Git LFS + `.gitignore` working; both machines can clone, open, push. Gate: Core Framework DoD.

**Phase 1 — Throw feel** *(1–3 weeks)*: single-machine bowling only. Grey-box lane, Kenney placeholder pins, aim/power/spin, and *relentless* iteration on physics comedy (Push 2). Also prototype the "walkable alley" spectator idea cheaply here (Push 1) — even just a second capsule that can waddle around and bump things. Gate: **a throw makes you both laugh**, and 10-frame scoring is correct.

**Phase 2 — Steam skeleton** *(1–2 weeks)*: Facepunch init on App ID 480, overlay, lobby create/invite/join, player list. No gameplay online yet. This de-risks the scariest tech early. Gate: Steam Framework DoD — Braeden joins your lobby from his house via Steam invite.

**Phase 3 — First online game** *(2–4 weeks; the hard one)*: launch-parameter sync, host-authoritative pin state, turn manager over the network, disconnect handling. Two real builds, two houses. Gate: you finish a full networked 10-frame game together and it doesn't break when someone rage-quits.

**Phase 4 — Make it a game** *(1–2 weeks)*: main menu, lobby-as-a-room, HUD, results, controller nav. Gate: a friend who's never seen it reaches an online game unaided.

**Phase 5 — The slop layer** *(2–4 weeks, iterative)*: betting overlay, fake-coin economy, drink meter, taunts, the bar/slots corner, spectator interference — synced. **Gate: THE gate — "first funny game."** A networked session with 3–4 friends produces real, repeated laughter and at least one clip you'd actually post. If it doesn't, iterate *here*; do not proceed. This gate is allowed to kill or pivot the project — that's it working as intended.

**Phase 6 — Store beachhead** *(parallel with 5 once confident)*: pay the $100 Steam Direct fee, real App ID, store page with clips from Phase 5, content survey honestly filled, wishlists start accruing. Gate: page live, first 15-30s clips posted.

**Phase 7 — Demo → Next Fest → launch** *(3–6 months of polish + marketing)*: progression/cosmetics, achievements, a free demo, Next Fest entry (Feb/Jun/Oct), daily clips during fest, creator outreach, then launch at an impulse price (comps: PEAK $7.99, Lethal Company/R.E.P.O. ~$10; you likely $5.99–9.99). Gate: launch.

**Phase 8 — Second minigame**: only after launch (or after Phase 5 if the framework begs for it). Darts, pool, air hockey, and beer pong all reuse the same launch-parameter pattern — the bar setting makes the roadmap obvious, which is another point for Push 1.

## 7. Risks, plainly

The design risk is pacing (addressed by Push 1). The technical risk is netcode (addressed by Phase 2-early and the QA agent). The project risk is scope creep (addressed by gates) and the two-person Unity scene-merge hazard (addressed by git rules). The market risk is unavoidable: most indie games earn very little, and no blueprint changes that — what this one does is make the cheap, high-information experiments happen first, so by Phase 6 you're spending $100 and marketing effort on something you already *know* is funny. And the quiet superpower risk: believing AI output without testing. Claude writes code that looks right; only the editor and two builds across two houses tell the truth. Test everything, every session.

## 8. Sources

- [Game Developer — How PEAK achieved 2M sales for <$200k](https://www.gamedeveloper.com/production/how-co-op-climbing-hit-peak-achieved-2-million-sales-for-less-than-200-000-)
- [Dexerto — PEAK mostly made in four weeks](https://www.dexerto.com/gaming/viral-co-op-game-peak-was-mostly-made-in-just-four-weeks-we-locked-in-3218842/)
- [Know Your Meme / Adventure Gamers — friendslop, defined](https://adventuregamers.com/article/what-does-friendslop-mean)
- [Creative Bloq — friendslop in 2026](https://www.creativebloq.com/3d/video-game-design/what-is-friendslop-and-why-it-it-taking-over-gaming-in-2026)
- [Game Developer — what devs can learn from indie social co-op](https://www.gamedeveloper.com/design/what-developers-can-learn-from-the-indie-social-co-op-games-topping-the-steam-charts)
- [How To Market A Game (Zukowski) — Next Fest breakthroughs](https://howtomarketagame.com/2025/10/20/steam-next-fest-october-2025-checking-in-on-the-games-that-broke-through/)
- [game-developers.org — where wishlists actually come from](https://game-developers.org/indie-game-steam-wishlists-sources)
- [Steamworks — content survey documentation](https://partner.steamgames.com/doc/gettingstarted/contentsurvey)
- [Steam — Golf With Your Friends](https://store.steampowered.com/app/431240/Golf_With_Your_Friends/)
- [Unity — official MCP server for AI agents](https://unity.com/blog/unity-ai-mcp-how-to-get-started)
