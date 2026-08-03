# Mirror + KCP spike — status and decisions log

Companion to `MirrorKcpSpikePrompt.md` (the prompt) — this file is the running state. Read both
before doing anything. Last updated 2026-08-03.

**Machine: the spike runs on Tony's Windows PC, not the Mac.** Step 4 needs two machines and the
whole Steam phase after this is Windows-only, so there's no reason to start on the Mac and hand off
mid-spike.

## Where we are

Step 0 (plan) is **done and approved, twice** — an initial plan and a revision after three
corrections. Nothing has been written to disk yet. No `spike/mirror-kcp` branch exists yet.

**Next action: Step 1 — install Mirror, verify it compiles, report version and layout.**

## Decisions already made — do not relitigate

**Transport: Mirror's stock KCP only.** No Facepunch.Steamworks, FizzyFacepunch, FizzySteamworks,
Steamworks.NET, or `steam_appid.txt`. Steam is a separate later task, deliberately excluded so that
a failure in this spike has exactly one possible cause.

**Mirror install: GitHub release, not the Asset Store.** Confirmed latest is **v96.11.1**, published
2026-07-26, single asset `Mirror-96.11.1.unitypackage` (14.26 MB). Reason: this project is on Unity
6000.5.4f1 while Mirror documents support through 6000.1, so we want the newest build, and the Asset
Store copy lags. `PLAYBOOK.md:58` (Stage D step 1) still says Asset Store — flagged, to be updated in
Step 5 only if the spike confirms GitHub was the better path.

**Commit Mirror to the branch.** Imported package content under `Assets/` is source the project needs
to compile, not a rebuildable cache like `Library/`. It also anchors the "cite the exact class name,
namespace and file path" rule to something in the branch's history. The branch never merges to main.

**`.gitattributes` and DLLs: conditional, not speculative.** `.gitattributes` currently has no `.dll`
rule. After importing, check for `*.dll` under `Assets/Mirror` — historically Mirror's weaver
depended on Mono.Cecil, which ships precompiled. Add an LFS rule **only if DLLs actually turn up**.

**Editing is invasive, on purpose.** Modify `PlayerAvatar.cs` and `BowlingMatchFlow.cs` directly. Do
not build parallel `NetworkedPlayerAvatar` / `SpikeThrowRelay` classes. The constraint in the prompt
is *don't redesign the split or the struct layout* — not *don't touch the files*. Testing a copy
would answer nothing about the real seam. If this breaks `BowlingAlley`, `TestVenue`,
`AlleyVenueGreybox` or `TestGreyblockAlley`, **let it break and record it** — the migration cost is
one of the findings.

**No ParrelSync.** It clones the project directory and `Assets/` is 803MB. Evaluate Unity's
Multiplayer Play Mode (`com.unity.multiplayer.playmode` — a separate package, NOT the already-
installed `com.unity.multiplayer.center`, which is just an onboarding hub). If MPPM turns out to
require Netcode for GameObjects or won't launch against a Mirror project, fall back to editor + one
standalone Development Build.

**Assembly definition: `WeeSpurts.Runtime.asmdef` has `"references": []`.** Mirror ships its own
asmdef, so Mirror must be added there or `PlayerAvatar.cs` won't compile. Confirmed by reading the
file. This is in scope and expected.

## Two-builds answer, settled per step

- **Step 2** (bare KCP connection): editor + one virtual player or standalone build. One machine.
- **Step 3** (roaming, `IsLocal` → `isLocalPlayer`, ControlMode replication): editor + one client
  process. One machine. The bug being hunted — a remote avatar grabbing your input, camera or
  cursor — is a function of ownership identity, which Mirror resolves identically whether the second
  process is local or across a LAN.
- **Step 4** (throw + drift): **two physical machines, genuinely required.** Two processes on one CPU
  would show artificially low drift — same executable, same silicon, same floating-point unit.

## Step 3 — logged prediction, test it exactly as stated

`PlayerAvatar.ApplyMode()` runs from `Start()` (`PlayerAvatar.cs:106`). Under Mirror, `isLocalPlayer`
is not guaranteed true at `Start()` — it's assigned when the identity spawns, with
`OnStartLocalPlayer` / `OnStartClient` as the real signal.

**Prediction:** on the local player's first frame, `IsLocal` (now backed by `isLocalPlayer`) reads
false, so `ApplyMode()` permanently disables `firstPersonController` and `interactor`
(`PlayerAvatar.cs:151-152`) and skips `ApplyCursor` (`PlayerAvatar.cs:160`). Symptom: spawn in,
can't move, no console error.

**If confirmed,** the fix is re-invoking `ApplyMode()` from `OnStartLocalPlayer()` — leaving the
`Start()` call in place, since it's still needed for non-local and host-side setup.

Report whether this happened **as predicted or not**, either way, verbatim in the findings doc. This
is the precision test of the `PlayerAvatar.cs:46-55` claim that "nothing else in this class has to
change" — the bet the entire 2026-07-26 pre-networking hardening pass was making.

## Step 4 — drift number caveat

Tony's two machines are a **Mac and a Windows PC**, so any Step 4 number is a cross-platform
measurement: different CPU architecture, different floating-point behaviour. We ship Windows-only.

Label any drift figure explicitly as a **Mac↔PC upper bound**. Do not recommend architecture changes
off it until Windows↔Windows has been measured separately.

## Standing reminders

- Branch `spike/mirror-kcp` off `main`. Nothing merges to `main` without a separate conversation.
- STOP after every step and wait for a human test.
- `Docs/DefinitionOfDone.md` does not apply — a spike's output is an answer and a findings doc.
- Do not assume Mirror component names from memory. `NetworkTransform` was split into
  reliable/unreliable variants at some version. Read `Assets/Mirror/` and cite real file paths.
- Push before switching machines; pull on arrival. Scene YAML does not merge.
