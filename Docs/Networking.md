# Networking

## Decision 🔒
- **Mirror** (high-level netcode framework) + **FizzyFacepunch** (transport) routing traffic over **Steam Game Networking Sockets** (Valve's relay).
- *(Corrected 2026-07-21: originally said FizzySteamworks, which pairs with the Steamworks.NET wrapper. Since we use Facepunch.Steamworks for lobbies, the matching transport by the same author is FizzyFacepunch — mixing the two Steam wrappers causes DLL conflicts.)*
- **Facepunch.Steamworks** for lobbies, friends list, invites, achievements.
- Dev against **App ID 480 (Spacewar)**; swap to the real App ID at store-setup time.

## Why
- Free, no concurrent-player caps, no monthly fees (vs. Photon which bills by CCU).
- Steam relay hides players' home IPs and handles NAT punch-through, so "invite a friend and they join" works without port forwarding.
- Mirror is the best-documented free Unity netcode framework — critical for beginners.

## Architecture model
- **Host-authoritative.** One player (or a listen-server host) owns the truth. Simplest model; fine for a private-friends party game.
- Bowling is **near-turn-based**: only the active player throws. This is the key simplification — we do NOT sync per-frame physics.

### The bowling sync pattern
1. Lobby decides turn order.
2. Active player aims and throws locally.
3. Client sends **launch parameters only** — the `LaunchParameters` struct (lateral position, angle, power, 2D spin vector, timing error, backward-fumble flag, seed). The struct is the contract; when it grows, this doc doesn't need to.
4. All clients simulate the roll deterministically from those parameters.
5. Host confirms the resulting pin state + score; that's the authority everyone adopts.

Because we send intent (not thousands of physics frames), bandwidth is tiny and desyncs are easy to reason about. If clients drift, snap to the host's confirmed pin state at end of roll.

### Roaming sync (confirmed by the mirror-kcp spike, `Docs/spikes/2026-08-03-mirror-kcp-findings.md`)
This is a **second, different sync model that coexists with the throw pattern above, not a contradiction of it**:
- Free-roaming avatars (`PlayerAvatar`, walking the alley between turns) are **continuously synced** via `NetworkTransformUnreliable` — real per-frame position/rotation replication, the opposite of "send intent only."
- Ownership/authority still follows the same host-authoritative rule: `PlayerAvatar.isLocalPlayer` (Mirror's own, not a hand-rolled flag) gates every input/camera/cursor decision, so a remote avatar can never grab another machine's controls. Confirmed on real hardware: camera and input never crossed machines in either roaming or bowling mode.
- The active thrower's avatar switches to `ControlMode.Bowling` (present-only, driven by `PlayerAvatar.EnterBowling`/`EnterRoaming`) while every other avatar keeps roaming and stays on the continuous-sync path. Both models run in the same scene at once with no observed conflict.

### House rules for networked scene objects (learned the hard way in the spike)
Anything like a shared match-state object (e.g. a future `BowlingMatchFlow`/`BowlingPresentation` pair) that is **pre-placed in a scene** rather than spawned from a prefab:
- **Don't trust Mirror's auto-generated `NetworkIdentity.sceneId`** in a scene that gets rebuilt by tooling (a `GreyboxSceneBuilder`-style "brand new scene every run" builder). It was observed disagreeing between host and client across repeated rebuilds even when both were supposedly running the same committed scene. Fix: assign a fixed constant directly (`sceneId` is a public field for exactly this) — or spawn the object as a prefab instead of pre-placing it, sidestepping the whole issue.
- **Never test one side via a standalone build and the other via Editor Play** for a scene containing pre-placed `NetworkIdentity` objects. Unity's `OnPostProcessScene` bakes an extra scene-path hash into `sceneId`, but only during an actual Player build, never when just pressing Play in the Editor — so a build-vs-editor pair will disagree on that object's identity by design, not by mistake. Both sides must use the same method.
- **Windows Firewall blocks inbound connections to the Unity Editor process by default**, separately from whatever rule exists for the shipped game's own `.exe`. Confirmed via explicit `Block` rules on Private/Public profiles (`Allow` only on Domain, which no home network uses) the first time a two-machine test tried to host from the Editor rather than a build.

## Rules
- Every networked action goes host → clients through Mirror commands/RPCs. No client trusts another client directly.
- Handle join / leave / disconnect at every state (lobby, mid-turn, between frames). A dropped player must not freeze the game.
- Build and test with TWO builds early (host + client on two machines) — bugs that never appear in the editor appear across the wire.

## Open ❓
- Max players (see `OpenQuestions.md`). Turn-based bowling scales to more players cheaply, but UI and pacing don't.
- Voice chat: use Steam's built-in voice, a third-party SDK, or "just use Discord"? Undecided.
