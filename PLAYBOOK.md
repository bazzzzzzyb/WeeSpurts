# Wee Spurts — The Playbook

Every task between here and a shipped game, in order. Each task is tagged:

- 🤖 **AI does it** — either already done, or you paste the given prompt into Claude Code and supervise.
- 🧑 **You do it** — exact clicks provided. These are the tasks a computer physically can't do for you (installing software, clicking in Unity, judging fun).

Text in `blocks like this` after "Tell Claude Code:" is a prompt — paste it verbatim.

**Already done by AI (this session):** the entire Phase 0+1 codebase sits in `_Staging/` — core framework, turn manager, a fully unit-tested bowling scorer (verified against the classic 300 / 133 / 150 test games), ball physics, pin deck, input, camera, debug HUD, and a one-click scene builder so you never wire a scene by hand.

---

## Stage A — Install the tools 🧑 (once per machine, ~1 hour; Braeden does A too)

1. **Unity Hub + Unity 6 LTS**: download Unity Hub from unity.com/download → install → sign in (free Personal license) → Installs → Install Editor → pick the newest **6000.x LTS** (badge says LTS) → check "Windows Build Support" (and nothing else) → Install. The code in this repo requires Unity 6 (it uses Unity 6 physics APIs, with fallbacks — but 6 LTS is the target).
2. **Git + Git LFS**: install from git-scm.com and git-lfs.com → open a terminal → `git lfs install`.
3. **Claude Code**: follow code.claude.com → install → in a terminal, `cd` into this repo folder → run `claude`. It reads `CLAUDE.md` automatically — no pasting, ever.
4. **Steam** running and logged in (needed from Stage D on).
5. Clone the repo (Braeden): `git clone https://github.com/bazzzzzzyb/WeeSpurts.git`

✅ **Check:** `claude` starts inside the repo and can answer "what are this project's golden rules?"

## Stage B — Create the Unity project and drop the code in (~30 min)

1. 🧑 Unity Hub → New Project → template **Universal 3D** → name `WeeSpurts` → location: your **Documents** folder (NOT this repo — Hub needs an empty folder; we move it next) → Create. Wait for first open, then **close Unity**.
2. 🤖 Tell Claude Code:
   `Move the Unity project from my Documents/WeeSpurts into this repo's Unity/ folder (keep Unity/README.md), then merge _Staging/_Project into Unity/Assets/_Project and delete the _Staging folder. Show me what you moved.`
3. 🧑 Unity Hub → Add → select the repo's `Unity` folder → open it. First import takes a few minutes.
4. 🧑 **One required setting** (our input code uses the classic system): Edit → Project Settings → Player → Other Settings → **Active Input Handling = Both** → let the editor restart.
5. 🧑 **Prove the code is healthy**: Window → General → Test Runner → EditMode tab → Run All. ✅ **Expect: 12/12 green.** Anything red: copy the message, tell Claude Code `these tests failed: <paste>`.
6. 🧑 **The magic click**: menu bar → **WeeSpurts → Build Greybox Bowling Scene** → press **Play**. Aim with ←/→ (Shift+←/→ for angle), hold **SPACE** and release at the top of the power bar, **Q/E** for spin. Two hot-seat players, full 10 frames, live scorecard.
7. 🤖 Tell Claude Code: `Everything works. Commit all of this on main with a good message and push.`

✅ **Gate (Core Framework done):** tests green, you bowled a frame, Braeden pulls and can do the same.

## Stage C — Make the throw FUNNY 🧑 (the highest-leverage week in the project)

Feel is tuned by playing, not coding. In the Project window: `_Project/ScriptableObjects/` → click **BallConfig** / **LaneConfig** → change numbers in the Inspector → press Play. No code, no rebuild (re-run the scene builder only after LaneConfig *geometry* changes).

Things to try deliberately: MaxLaunchSpeed 25 (cannonball), Mass 2 + Bounciness 0.9 (beach ball), PinMass 0.4 (pins launch into orbit), Width 3 (party lane). Find 2–3 configs that make you both laugh.

When you want something the knobs can't do — wobbly drunk aim, exploding 7-10 splits, a ball that grows mid-roll — 🤖 tell Claude Code:
`/build-system Bowling feel: <describe the funny thing>. Use the physics-tech-artist agent. It must be driven only by LaunchParameters + config so it stays network-reproducible.`

✅ **Gate ("first playable feel"):** a full 10-frame game on one machine that makes you and Braeden laugh. Record a 15-second clip of the funniest moment — if there's nothing worth clipping yet, iterate here. This clip habit becomes your marketing engine (`Docs/Marketing.md`).

## Stage D — Steam skeleton (lobby, no gameplay)

1. 🧑 **Install Mirror**: Unity Asset Store (assetstore.unity.com) → search "Mirror" (free, by Mirror Networking) → Add to My Assets → in Unity: Window → Package Manager → My Assets → Mirror → Download → Import.
2. 🧑 **Install FizzyFacepunch** (transport): github.com/Chykary/FizzyFacepunch → Releases → download the `.unitypackage` → in Unity: Assets → Import Package → Custom Package. ⚠️ Note: `Docs/Networking.md` originally said Fizzy**Steamworks** — that variant pairs with Steamworks.NET, not with Facepunch, and mixing the two Steam wrappers causes DLL conflicts. Since our lobby code uses **Facepunch**.Steamworks, the matching transport is Fizzy**Facepunch** (same author). The doc has been corrected.
3. 🤖 Tell Claude Code:
   `/build-system Steam Framework step 1: SteamManager that initializes Facepunch.Steamworks with App ID 480, creates a steam_appid.txt, survives scene loads, and shuts down cleanly. Use the steam-engineer agent. Verify FizzyFacepunch and Facepunch are actually imported and cite real API names.`
4. 🧑 Press Play with Steam running. ✅ Expect: console says Steam initialized with your Steam name; Shift+Tab opens the overlay (game shows as "Spacewar" — normal until we buy our App ID).
5. 🤖 Tell Claude Code (one session each, `/qa-review` after each):
   - `/build-system Steam Framework step 2: Mirror NetworkManager + FizzyFacepunch transport configured in code; host can start a lobby.`
   - `/build-system Steam Framework step 3: friend joins via Steam overlay invite; lobby screen lists players by Steam name; leaving updates the list.`
6. 🧑 **The two-house test**: File → Build Settings → Build. Send Braeden the build (or he builds from the repo). You host, invite him via the overlay, he joins. ✅ **Gate (Steam Framework done):** you see each other's names from separate houses.

## Stage E — Bowling goes online

🤖 One session each, in order, `/qa-review` after every one (this is the zone where AI code most often *looks* right):

1. `/build-system Networked bowling 1: NetworkedBowlingController — host assigns turns via TurnManager; active player's BallLauncher sends LaunchParameters to the host as a Mirror Command; host broadcasts it; every client replays the throw locally.`
2. `/build-system Networked bowling 2: host-authoritative pin confirmation — after settle, host counts pins, broadcasts the roll result, all clients snap their racks and scorecards to it.`
3. `/build-system Networked bowling 3: disconnect handling — player drops mid-turn: their turn is skipped and the game continues; host drops: match ends gracefully to a message, no freeze.`

🧑 After each: two-build test with Braeden (checklists come from `/qa-review`). ✅ **Gate ("first online game"):** full 10-frame match from separate houses, including one deliberate rage-quit test.

## Stage F — Real UI

🤖 One session per screen with the ui-engineer agent: main menu → lobby room (avatars, ready-up, invite button) → in-game HUD (replaces DebugHud) → results/rematch. Then: `/build-system UI: controller navigation across all screens.`
🧑 Playtest each screen as it lands; you are the judge of "10-second readability."
✅ **Gate:** a stranger reaches an online game unaided.

## Stage G — The slop layer (the differentiator)

Creative calls are yours (`Docs/OpenQuestions.md`) — decide by playing, not on paper. Suggested session order:

1. `/build-system Slop: between-turn betting — spectators bet coins on the active player's roll outcome (strike/spare/gutter), host-authoritative payouts, synced balances.`
2. `/build-system Slop: drink meter — each drink comedically degrades the drinker's next throw (wobble amplitude driven by LaunchParameters.Seed so all clients replay identically).`
3. `/build-system Slop: taunts — 6 synced emote/soundboard buttons usable any time.`
4. **The walkable alley experiment** (BLUEPRINT Push 1): `/build-system Slop experiment: spectators control a ragdoll-ish character who can wander near the lane and heckle physically; active player is protected by rules we can tune (cost coins to interfere, cooldowns).` Also prototype the bar + slots corner as coin sinks.

✅ **Gate ("first funny game" — THE gate):** a 4-friend networked session produces repeated genuine laughter and ≥1 clip you'd post. Iterate here until it does. This gate can pivot or kill the project; that's its job.

## Stage H — Store, marketing, launch

Follow `Docs/Marketing.md`. Summary of who does what: 🧑 pay $100 Steam Direct, fill the content survey honestly (fake gambling + satirical drinking = disclose, never real money), commission capsule art, record clips, pick the Next Fest (Feb/Jun/Oct) you can hit with a stable demo. 🤖 Claude Code drafts store copy, trailer shot lists, creator outreach emails, achievement/stat integration, demo build config, and progression (Roadmap [7]) — one `/build-system` session each. Price corridor: $5.99–$9.99, sell a 4-pack.

---

## The content track (runs alongside Stages C–H)

Everything visual/audible is planned in `Docs/ContentPlan.md` — exact packs, licenses, palette, pipelines. The short version of who does what:

- 🧑🤖 **Characters**: download Quaternius Universal Base Characters + Mixamo clips (steps: `Docs/AssetWorkbench.md` §2), then `/build-system Characters: player character from the Quaternius rig — Humanoid import, Animator with the Mixamo clips, ragdoll from the rig with a comedy floppiness dial, PlayerCharacter prefab.` Your own AI-made humanoids swap in later, zero code changes.
- 🧑 **Your parallel AI-art lane**: work `Docs/AssetWorkbench.md` §3 top-down (logo → UI icons → decals → signage → skybox). Never blocks code; placeholders ship.
- 🤖 AI generates: face expression sheets, UI icons, VFX (particles), logo concepts; wires fonts, SFX, music.
- 🧑 You: download the named CC0 packs (drag-and-drop steps in ContentPlan §4), record taunt lines with Braeden (funnier than AI voices), pick winners, log licenses in `Assets/README.md`.
- 🗓️ Real art replaces greybox only at the **art pass** — a two-week-max stage AFTER the "first funny game" gate, before the store page. Until then: greybox + beans + juice. That's not a compromise; it's how every comp shipped.

## The working rhythm (every session, forever)

1. One feature. `/build-system <system>: <one thing>`.
2. Claude states plan → you say go → it builds in small steps → you test in the editor after each step.
3. Networking/scoring/coins touched? `/qa-review` before you believe it.
4. Green? Commit and push. Feature branches for anything multi-day.
5. Something feels off but works? That's a note for a *feel* session, not a reason to stop shipping.

**When anything breaks:** copy the exact console error → tell Claude Code `I got this error: <paste>. Diagnose before fixing.` Never let it "fix" what it hasn't explained.

**When you're lost:** `Read BLUEPRINT.md and PLAYBOOK.md and tell me exactly where we are and what the next single task is.`
