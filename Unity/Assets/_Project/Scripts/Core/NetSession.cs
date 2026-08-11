using Mirror;

namespace WeeSpurts.Core
{
    /// <summary>
    /// Single source of truth for "is there any Mirror session running at
    /// all" — no host, no server, no client. Lifted out of
    /// <c>BowlingMatchFlow.IsOffline</c> (Docs/Networking.md's throw-sync
    /// section names this exact rule) so a second and third NetworkBehaviour
    /// needing the same check don't each grow their own copy.
    ///
    /// FAIL-OPEN, AND ONLY HERE: <see cref="IsOffline"/> reading true means
    /// "nobody is even trying to network this session" (e.g. TestVenue.unity
    /// or BowlingAlley.unity pressed Play with no NetworkManager in the
    /// scene) — it is not a relaxed trust check. The instant a session exists
    /// in ANY role (host, dedicated server, or a connecting client),
    /// NetworkClient.active or NetworkServer.active goes true, this flips to
    /// false, and every consumer falls straight back to Mirror's own
    /// authority rules (isLocalPlayer, isServer, Commands/RPCs) exactly as
    /// they behave today. It never weakens behaviour while a session is live.
    /// </summary>
    public static class NetSession
    {
        public static bool IsOffline => !NetworkClient.active && !NetworkServer.active;
    }
}
