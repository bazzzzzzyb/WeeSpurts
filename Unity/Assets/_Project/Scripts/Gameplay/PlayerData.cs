namespace WeeSpurts.Gameplay
{
    /// <summary>
    /// Everything the game knows about one player. Plain C# (not a
    /// MonoBehaviour) so it can be created for local hot-seat play now and
    /// filled from Steam lobby data later without changing gameplay code.
    /// </summary>
    public class PlayerData
    {
        /// <summary>Stable id. Local play: 0..N. Networked: derived from SteamId later.</summary>
        public ulong Id { get; }

        public string DisplayName { get; set; }

        // DELIBERATELY NO `Coins` FIELD — removed 2026-08-04.
        //
        // There used to be a `public int Coins { get; set; }` here. It was
        // never read or written by anything, and it had to go the moment
        // CoinLedger existed, because two places holding a balance is exactly
        // the bug that class was built to prevent. `Docs/SlopLayerPlan.md`
        // Rule 1: every coin mutation goes through ONE choke point. A public
        // setter on a second copy is a bypass sitting there waiting to be used
        // by whoever is in a hurry, and the two numbers would then disagree in
        // a way nobody notices until money appears out of nowhere mid-match.
        //
        // Ask GameManager.Instance.Coins.BalanceOf(player.Id) instead. Note the
        // ledger keys on the SAME ulong Id above — deliberately, because that
        // becomes a 64-bit SteamId later and an int cannot hold one.

        /// <summary>Each player owns their own scorecard.</summary>
        public BowlingScorer Scorer { get; } = new BowlingScorer();

        public PlayerData(ulong id, string displayName)
        {
            Id = id;
            DisplayName = displayName;
        }
    }
}
