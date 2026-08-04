# Mirror + KCP spike — findings

Companion to `MirrorKcpSpikePrompt.md` (the question) and `MirrorKcpSpikeStatus.md`
(the running log). This is Step 5: what the spike answered, what it cost, and
what to do next. Branch `spike/mirror-kcp` — never merged to `main`.

## The one question, answered

**Does our host-authoritative, launch-parameter-only sync model coexist with
continuously synced free-roaming avatars? Yes.**

Confirmed on real hardware, two physical machines (Tony's Windows PC and Mac),
over a real LAN connection, not localhost:

- Roaming avatars replicate continuously (`NetworkTransformUnreliable`) while a
  separate object resolves a bowling throw via launch-parameter Commands/RPCs.
  Both sync models ran in the same scene at the same time with no conflict.
- Camera and input ownership held correctly under `isLocalPlayer`: a remote
  avatar never grabbed the other machine's camera, cursor, or keyboard, in
  either roaming or bowling mode.
- One full networked throw resolved correctly end to end: active client's
  `LaunchParameters` → host via `Command` → broadcast to every machine via
  `ClientRpc` → each machine replayed the same physics independently → host's
  authoritative pin count confirmed back to the client.

## Drift: the actual number

**Zero**, across four throws tested Mac↔PC. Every throw: this machine's own
local physics knocked 10 pins, host reported 10 pins knocked. No divergence
observed on this test's parameters (a full-power, centered throw — no drift
data exists yet for grazing/light contact throws, which are the more likely
place two independent PhysX simulations would actually disagree).

Caveat already flagged in the status doc and still true: this is a **Mac↔PC
upper bound**, not a Windows↔Windows number — different CPU architecture,
different floating-point behavior. We ship Windows-only, so a Windows↔Windows
number is still unmeasured. Given this result, that remeasurement is low
priority rather than urgent.

## What broke, and what it cost

The one-question test passed cleanly. Getting there cost far more than the
throw-sync code itself — almost entirely from networking a **pre-placed scene
object** (`BowlingGame`, carrying `BowlingMatchFlow` + `BowlingPresentation`)
rather than a spawned prefab. These are genuine "Networked Bowling" migration
costs, not spike-specific accidents:

- **Standalone builds and Editor Play mode disagree on `NetworkIdentity.sceneId`
  for pre-placed scene objects.** Unity's `OnPostProcessScene` bakes an
  additional scene-path hash into the ID, but only during an actual Player
  build — never when you just press Play in the Editor. A build-vs-editor pair
  (which is exactly what "editor + one standalone build" testing on one
  machine looks like) will never agree on that object's identity. Both sides
  must use the same method — this cost us the better part of the two-machine
  test session before it was understood.
- **Mirror's own sceneId auto-generation (`NetworkIdentity.AssignSceneID`,
  `OnValidate`-driven) was unreliable across a scripted, rebuild-from-scratch
  scene.** The exact "safe to run repeatedly, brand-new scene each time"
  pattern `GreyboxSceneBuilder` uses successfully for single-player scenes
  actively fights Mirror here: every rebuild is a fresh chance for host and
  client to disagree on a scene object's ID, even when the two machines are
  running what's supposed to be the identical committed scene. Fixed for this
  spike by hardcoding the `sceneId` field directly in the builder script
  (`sceneId` is public specifically so tooling can do this) rather than
  trusting Mirror's auto-assignment. **Any future networked scene object needs
  this treatment, or a fundamentally different pattern** (e.g. spawning the
  match-state object as a prefab instead of pre-placing it in the scene).
- **Windows Firewall blocks inbound connections to the Unity Editor process by
  default** — found explicit `Block` rules for "Unity 6000.5.4f1/6000.5.6f1
  Editor" on Private/Public network profiles (`Allow` only on Domain, which no
  home network uses). This is separate from, and not fixed by, allowing the
  standalone game's own `.exe` through the firewall — they're different
  programs as far as Windows is concerned. Anyone testing host-via-Editor on a
  home LAN will hit this.
- **`BallLauncher`/`BowlingMatchFlow`'s "who may throw" gating has no real
  turn-authority check.** `CmdThrow` on `BowlingMatchFlow` trusts any caller
  (`requiresAuthority = false`, deliberately, for this spike). Observed live: a
  second player pressing the "become thrower" trigger while a match is already
  running hits the existing idempotency guard and safely no-ops — but gives
  that player zero feedback, so from their side it just looks like nothing
  happened (their avatar stays in Roaming, movement keeps working, no error).
  Real Networked Bowling needs an actual turn-ownership check and UI feedback,
  not just a silent guard.
- Migration cost predicted in advance and confirmed exactly as expected:
  `PlayerAvatar` becoming a `NetworkBehaviour` breaks `BowlingAlley.unity` /
  `TestVenue.unity` / etc. (`NullReferenceException` on `isLocalPlayer`, no
  `NetworkIdentity` in their hierarchy). Not fixed — out of scope by design,
  same as the plan said.

## Does the walkable-alley design survive contact with the netcode?

Yes, for what was tested. The core mechanism — one player's avatar in
`ControlMode.Bowling` while everyone else stays in `Roaming`, each machine
deciding locally who owns its own camera/input via `isLocalPlayer` — held up
under a real two-machine test, not just in the editor. The remaining open
questions are about **match/turn state**, not the roaming/bowling split
itself: turn ownership, continuing past one throw, and giving players
feedback when an action was silently ignored.

## What `Docs/Networking.md` needs to say about roaming that it doesn't yet

Its 34 lines currently describe only the launch-parameter throw sync. It needs
a second section covering:

1. Roaming avatars are **continuously synced** (`NetworkTransform`), a
   different model from launch-parameter-only throw sync — both coexist by
   design, this isn't a contradiction, but it's undocumented today.
2. House rules for any future **pre-placed networked scene object**: don't
   trust Mirror's auto-generated `sceneId` in a scene that gets rebuilt by
   tooling; hardcode it, or spawn the object as a prefab instead of
   pre-placing it.
3. Never test one side via a standalone build and the other via Editor Play
   for a scene containing pre-placed `NetworkIdentity` objects — the two
   disagree on scene-object identity by design (`OnPostProcessScene`).
4. Windows Firewall needs an explicit allow rule for the Unity Editor process
   itself (not just the shipped game) for any LAN testing done via Editor Play
   on both sides.

## What to throw away, what to keep

**Throw away** — spike scaffolding only, delete with the branch:
- `SpikeNetKcpSceneBuilder.cs`, `SpikeNetKcp.unity`, `SpikeNetKcpPlayer.prefab`
- `PlayerAvatar.CmdRequestStartBowling` / the "B" key debug trigger
- `BowlingMatchFlow`'s `[Command(requiresAuthority = false)]` on `CmdThrow` —
  keep the Command/Rpc *shape*, not this specific trust level

**Keep** — these are real production-file changes already made directly to
`PlayerAvatar.cs`, `BowlingPresentation.cs`, `BowlingMatchFlow.cs`, and
`PlayerCameraDirector.cs` on this branch, not spike-only additions. They still
need a deliberate decision to port to `main` (this branch itself never merges),
but the design is validated, not just proposed:
- `PlayerAvatar : NetworkBehaviour`, `IsLocal` → `isLocalPlayer` (fail-closed),
  `OnStartLocalPlayer` re-applying `ApplyMode()` — confirmed fixes the exact
  bug predicted in Step 3 (Start() running before ownership is confirmed)
- `PlayerCameraDirector.Configure()` — the runtime camera-wiring seam needed
  because a networked player is a prefab instantiated per connection, not a
  scene-baked object `RoamingSetupTool` can wire at edit time
- The `BowlingMatchFlow`/`BowlingPresentation` Command → ClientRpc → local
  replay → host-authoritative confirm shape — proven correct, needs turn-
  authority validation and BeginRoll-continuation-past-one-throw added before
  it's feature-complete, not redesigned
- Mirror 96.11.1 via GitHub release (not Asset Store), `.dll`s under Git LFS —
  the real dependency decision for Stage D, unrelated to spike scaffolding

## Recommendation

Ship the "keep" list's design intent into the real Networked Bowling work
(PLAYBOOK Stage E), written up against these findings rather than starting
that design from scratch. Don't reuse the spike's scene/branch content
directly — rebuild the real feature with the caller-authority and turn-
continuation gaps closed from the start.
