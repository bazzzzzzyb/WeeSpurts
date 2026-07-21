# Wee Spurts — Networking

Rationale for netcode and Steam integration choices. These are **decided** (🔒); don't re-litigate without editing `GameBible.md` §4 first.

---

## Stack

| Layer | Choice | Rationale |
|---|---|---|
| Netcode library | Mirror | Battle-tested, Unity-native, large community, free. Unet is dead; Netcode for GameObjects is still maturing. |
| Transport | FizzySteamworks | Routes traffic over Steam's relay network — no port forwarding, no exposed IPs, works behind NAT. |
| Steam API | Facepunch.Steamworks | Modern C# wrapper, actively maintained, cleaner API than the official Steamworks.NET. |
| Topology | Listen-server (host = one player) | Dedicated servers are overkill for a private-lobby party game. Host-migration is a stretch goal, not MVP. |

## Dev App ID

`480` (Spacewar) is used during development so we can test Steam lobbies without owning a Steam Direct slot. Switch to our real App ID before any public build.

## Authority model

- **Host is authoritative** for all physics and game-state.
- Clients send input; host simulates; host broadcasts results.
- No client-side prediction for v1 — latency is acceptable in a turn-based party game.

## Lobby flow (planned)

1. Host creates Steam lobby (via Facepunch.Steamworks).
2. Host shares invite link or invites friends directly.
3. Clients join lobby → Mirror connection opens over FizzySteamworks transport.
4. All players land in the game scene together.

## Known risks

- **Host rage-quit** ends the session. Acceptable for MVP; host-migration is a `docs/OpenQuestions.md` stretch goal.
- **Steam relay latency** adds ~30–80 ms on top of raw ping. Fine for bowling (turn-based), revisit for twitch minigames.

---

## Change log
- 2026-07-21 — Document created.
