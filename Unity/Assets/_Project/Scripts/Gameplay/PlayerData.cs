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

        /// <summary>Fake-coin balance for the betting layer (Roadmap [6]).</summary>
        public int Coins { get; set; }

        /// <summary>Each player owns their own scorecard.</summary>
        public BowlingScorer Scorer { get; } = new BowlingScorer();

        public PlayerData(ulong id, string displayName, int startingCoins = 100)
        {
            Id = id;
            DisplayName = displayName;
            Coins = startingCoins;
        }
    }
}
