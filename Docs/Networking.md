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
3. Client sends **launch parameters only**: position, direction, power, spin, seed.
4. All clients simulate the roll deterministically from those parameters.
5. Host confirms the resulting pin state + score; that's the authority everyone adopts.

Because we send intent (not thousands of physics frames), bandwidth is tiny and desyncs are easy to reason about. If clients drift, snap to the host's confirmed pin state at end of roll.

## Rules
- Every networked action goes host → clients through Mirror commands/RPCs. No client trusts another client directly.
- Handle join / leave / disconnect at every state (lobby, mid-turn, between frames). A dropped player must not freeze the game.
- Build and test with TWO builds early (host + client on two machines) — bugs that never appear in the editor appear across the wire.

## Open ❓
- Max players (see `OpenQuestions.md`). Turn-based bowling scales to more players cheaply, but UI and pacing don't.
- Voice chat: use Steam's built-in voice, a third-party SDK, or "just use Discord"? Undecided.
