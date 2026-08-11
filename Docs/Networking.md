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

### Roaming sync (confirmed 2026-08-03, mirror-kcp spike)
- Roaming avatars are **continuously synced** via `NetworkTransformUnreliable` — a different model from the launch-parameter-only throw sync above. Both coexist in the same scene at the same time by design; confirmed on real hardware, two physical machines, not just in-editor (`Docs/spikes/2026-08-03-mirror-kcp-findings.md`).
- Camera and input ownership are decided **locally, per machine**, via `isLocalPlayer` (or `NetSession.IsOffline || isLocalPlayer` when no session is running at all — see Offline fallback below): a remote avatar never grabs this machine's camera, cursor or keyboard, in either Roaming or Bowling mode.

## Rules
- Every networked action goes host → clients through Mirror commands/RPCs. No client trusts another client directly.
- Handle join / leave / disconnect at every state (lobby, mid-turn, between frames). A dropped player must not freeze the game.
- Build and test with TWO builds early (host + client on two machines) — bugs that never appear in the editor appear across the wire.
- **Never mix a standalone build with Editor Play** for a scene containing pre-placed `NetworkIdentity` objects. `NetworkIdentity.sceneId` is baked differently for the two: Unity's `OnPostProcessScene` adds an extra scene-path hash only during an actual Player build, never during Editor Play — so a build-vs-editor pair will never agree on that object's identity. Both sides of a test must use the same method (two builds, or two editors — never one of each).
- **Windows Firewall blocks inbound connections to the Unity Editor process by default** on Private/Public network profiles (only Domain is allowed, and no home network uses Domain) — this is separate from, and not fixed by, allowing the shipped game's own `.exe` through the firewall; Windows treats them as different programs. Add an explicit inbound allow rule for the Editor process before testing host-via-Editor on a home LAN.

## Pre-placed networked scene objects (house rule)
Don't trust Mirror's own `sceneId` auto-generation (`NetworkIdentity.AssignSceneID`, `OnValidate`-driven) for any scene that a script rebuilds from scratch (e.g. `GreyboxSceneBuilder`/`AlleyGreyboxBuilder`) — every rebuild is a fresh chance for host and client to disagree on that object's ID, even off what's supposed to be the identical committed scene. Two options, confirmed by the spike: hardcode the `sceneId` field directly in the builder script (it's public specifically so tooling can do this), or spawn the match-state object as a prefab instead of pre-placing it in the scene.

**Not yet applied to `TestVenue.unity`/`BowlingAlley.unity`** — see Offline fallback immediately below for why it hasn't needed to be yet. It becomes relevant the moment a future session actually starts a Mirror session against either scene.

## Offline fallback (added 2026-08-04)
Scenes carrying `NetworkBehaviour`s but no `NetworkManager`/session at all — `TestVenue.unity` and `BowlingAlley.unity` today — still need to play solo, and a bare `isLocalPlayer`/`isServer`/`isClient`/`isOwned` read throws on a null `netIdentity` in that state (they're all `netIdentity.X` under the hood). `WeeSpurts.Core.NetSession.IsOffline` (`!NetworkClient.active && !NetworkServer.active`) is the one shared check every consumer uses instead: `PlayerAvatar.IsThisMachinesPlayer => NetSession.IsOffline || isLocalPlayer` (and `BowlingMatchFlow`'s own `IsOffline`, which originated this rule) skip Commands/RPCs and resolve locally rather than touching a null identity. **Fail-open ONLY when no session exists at all** — the instant one starts (host, server, or client), this reads false and every consumer falls back to the authoritative rules above, unchanged.

**The check must come FIRST in the expression, not just exist.** `BowlingMatchFlow.ResolveThrow` had `IsOffline` in scope from day one but wrote `if (!isServer && !IsOffline)` — C#'s `&&` evaluates left-to-right, so `isServer` still ran (and threw) before `IsOffline` was ever read. Found 2026-08-04 by an actual offline playtest: the first throw worked, then the coroutine died on the second throw's pin-count check, freezing the camera with no error visible unless you were watching the Console. Fixed by reordering to `if (!IsOffline && !isServer)`. When adding a new `NetSession.IsOffline`-guarded check, put `IsOffline` (or `IsThisMachinesPlayer`) as the *leftmost* operand of any `&&`/`||` it's combined with.

## Open ❓
- Max players (see `OpenQuestions.md`). Turn-based bowling scales to more players cheaply, but UI and pacing don't.
- Voice chat: use Steam's built-in voice, a third-party SDK, or "just use Discord"? Undecided.
