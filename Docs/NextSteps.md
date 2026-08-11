# Next Steps — working task list

Generated 2026-08-03, after the Mirror/KCP spike landed. This is a *working* list,
not a Bible doc: `PLAYBOOK.md` owns the stage order, `Docs/Roadmap.md` owns the
dependency graph. When a task here is done, delete the line.

Tags follow PLAYBOOK: 🤖 = paste the prompt into Claude Code and supervise.
🧑 = only you can do it (clicks, judgement, fun).

---

## 0. Housekeeping — do first, ~10 minutes

- [ ] 🧑 **Get off the spike branch.** `spike/mirror-kcp` is checked out and is
      8 commits ahead of `main`. It **never merges** (its own findings doc says so).
      `main` already has the findings. `git checkout main`, and leave the spike
      branch alone as a reference until Stage E is done, then delete it.
- [ ] 🤖 **Kill the line-ending churn.** Every `.md` in the repo shows as modified
      but the diff is *pure CRLF↔LF* — zero real content change. It will keep
      hiding real doc edits from you. Prompt:
      `Every .md file shows modified with only line-ending changes. Fix this properly with .gitattributes (text eol handling) and renormalize, then show me that git status is clean.`

## 1. Close the Stage C gate before touching netcode

The gate is "a full 10-frame game on one machine that makes you and Braeden laugh,
plus a 15-second clip." Networking multiplies whatever feel you have — it does not
create any. Everything below is a **playtest**, not a code session.

- [ ] 🧑 **Play a full 10 frames on the venue scene and record a 15s clip.**
      If nothing is worth clipping, that is the signal to stay in Stage C.
- [ ] 🧑 **Answer the two spin questions in `OpenQuestions.md`** by throwing, not
      reasoning: (a) five topspin and five backspin throws with deliberately early
      releases — is the dial-away-your-own-fumble effect a skill leak or a real
      risk/reward trade? (b) does hard-release hook RIGHT and soft hook LEFT, as
      §8 decided? Log both calls in `OpenQuestions.md`.
- [ ] 🧑 **Walk the venue.** The character controller now exists (0.30m radius),
      which unblocks four questions that were parked waiting for it: does the
      concourse read narrow at 3.75m, is the settee pit usable, can you get in
      **and back out** of the card-dealer alcove, and does the walkable floor make
      the throw cinematic redundant or complementary. Report numbers, don't tune.
- [ ] 🤖 **Backward-fumble gag doesn't fire** (logged under Known Issues). Tony's
      direction is already recorded: tie it to release-during-backswing animation
      timing, not the current power threshold. Prompt:
      `/build-system Bowling feel: the backward-fumble gag never fires. Diagnose first against the current power-threshold implementation, then re-tie it to thrower animation timing (release during the backswing) per the CHANGELOG's Known Issues note. Use the physics-tech-artist agent. Driven only by LaunchParameters + config.`

## 2. Stage D — Steam skeleton

The spike used **KCP** (raw LAN), not Steam. FizzyFacepunch is still not imported,
and Mirror 96.11.1 exists **only on the spike branch** — it has to be brought to
`main` deliberately.

- [ ] 🤖 **Port Mirror onto `main`.** Prompt:
      `Bring Mirror 96.11.1 (the GitHub release build, DLLs under Git LFS) onto main exactly as it exists on spike/mirror-kcp — the dependency only, none of the spike scene/prefab/builder scaffolding, and none of the CmdRequestStartBowling debug trigger. Show me the file list before you move anything.`
- [ ] 🧑 **Import FizzyFacepunch** — github.com/Chykary/FizzyFacepunch → Releases →
      `.unitypackage` → Assets → Import Package → Custom Package. **Not**
      FizzySteamworks; that one pairs with Steamworks.NET and will DLL-conflict
      with Facepunch.
- [ ] 🤖 `/build-system Steam Framework step 1: SteamManager that initializes Facepunch.Steamworks with App ID 480, creates steam_appid.txt, survives scene loads, shuts down cleanly. Use the steam-engineer agent. Verify FizzyFacepunch and Facepunch are actually imported and cite real API names.`
- [ ] 🧑 Press Play with Steam running. Expect: console names your Steam account,
      Shift+Tab overlay works, game shows as "Spacewar."
- [ ] 🤖 `/build-system Steam Framework step 2: Mirror NetworkManager + FizzyFacepunch transport configured in code; host can start a lobby.` → `/qa-review`
- [ ] 🤖 `/build-system Steam Framework step 3: friend joins via Steam overlay invite; lobby lists players by Steam name; leaving updates the list.` → `/qa-review`
- [ ] 🧑 **Two-house test with Braeden.** Gate: you see each other's names from
      separate houses.

## 3. Stage E — bowling goes online

Do **not** design this from scratch. The spike validated a shape and named three
gaps it deliberately left open; write each session against the findings doc.

- [ ] 🤖 **Port the validated "keep" list to `main`.** Now FIVE changes, not four —
      the 2026-08-04 offline-fallback fix (`NetSession`/`IsThisMachinesPlayer`,
      plus the `ResolveThrow` `isServer`/`IsOffline` ordering fix found the same
      day by playtest) exists ONLY on `spike/mirror-kcp` today and will be lost
      when that branch is deleted unless it's explicitly carried over here. Prompt:
      `Port the five production changes the mirror-kcp spike validated — PlayerAvatar : NetworkBehaviour with IsLocal → isLocalPlayer fail-closed and OnStartLocalPlayer re-applying ApplyMode, PlayerCameraDirector.Configure() as the runtime camera-wiring seam, the BowlingMatchFlow/BowlingPresentation Command → ClientRpc → local replay → host-confirm shape, AND the 2026-08-04 offline-fallback fix (WeeSpurts.Core.NetSession.IsOffline + PlayerAvatar.IsThisMachinesPlayer + BowlingMatchFlow.ResolveThrow's IsOffline-before-isServer ordering, see Docs/Networking.md's Offline fallback section) that restores solo play in BowlingAlley/TestVenue. Read Docs/spikes/2026-08-03-mirror-kcp-findings.md first. Do NOT carry over requiresAuthority = false on CmdThrow.`
- [ ] 🤖 `/build-system Networked bowling 1: NetworkedBowlingController — host assigns turns via TurnManager; the active player's launch parameters go to the host as a Command and are broadcast for local replay on every client. Close the two gaps the spike named: a real turn-ownership check on the Command (not the silent idempotency guard), and player-visible feedback when an action is rejected. Decide and document whether the match-state object is spawned as a prefab or pre-placed — if pre-placed, apply the hardcoded-sceneId house rule.` → `/qa-review`
- [ ] 🤖 `/build-system Networked bowling 2: host-authoritative pin confirmation — after settle, host counts pins, broadcasts the roll result, all clients snap racks and scorecards to it. Continue past a single throw: BeginRoll continuation through a full frame and a full 10-frame match.` → `/qa-review`
- [ ] 🤖 `/build-system Networked bowling 3: disconnect handling — player drops mid-turn, their turn is skipped and play continues; host drops, match ends gracefully to a message with no freeze.` → `/qa-review`
- [ ] 🧑 **Two-build test after every one of the three.** Editor + build is
      explicitly forbidden for scenes with pre-placed NetworkIdentity objects —
      two builds, or two editors, never one of each.
- [ ] 🧑 **Measure drift Windows↔Windows.** The spike's zero-drift number is a
      Mac↔PC upper bound on a full-power centred throw. The risky cases are
      grazing and light-contact throws — throw a dozen of those and compare each
      machine's local pin count against the host's.

## 4. Parked / not yet

Do not start these until the "first online game" gate passes: Stage F real UI,
Stage G slop layer (betting, drink meter, taunts, heckling), Stage H store.
The art pass is explicitly a two-week stage **after** the "first funny game" gate.
