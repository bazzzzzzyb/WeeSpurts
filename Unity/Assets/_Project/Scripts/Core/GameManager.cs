using UnityEngine;
using WeeSpurts.Gameplay;

namespace WeeSpurts.Core
{
    /// <summary>
    /// The one object that survives every scene change. Holds global state
    /// (what phase of the app we're in) and nothing else — systems own their
    /// own logic; this just remembers where we are.
    ///
    /// WHY a singleton? Because there is genuinely only ever one "game".
    /// Per CodingStandards.md, singletons are allowed for exactly this case.
    ///
    /// SETUP: none. GreyboxSceneBuilder creates it, or add an empty GameObject
    /// named "GameManager" with this component to your boot scene.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public enum AppState { Boot, Menu, Lobby, InGame, Results }

        public static GameManager Instance { get; private set; }

        public AppState State { get; private set; } = AppState.Boot;

        /// <summary>Fired whenever the app state changes. UI listens to this.</summary>
        public event System.Action<AppState> OnStateChanged;

        [Header("Economy")]
        [Tooltip("Optional. Leave empty and the session runs on EconomyConfig.DEFAULT_STARTING_COINS, so an unwired scene still plays.")]
        [SerializeField] private EconomyConfig economyConfig;

        /// <summary>
        /// THE fake-coin ledger for this whole session — Tony's call, 2026-08-04.
        ///
        /// SESSION-SCOPED, NOT MATCH-SCOPED, and living here specifically
        /// because this object already survives scene changes. Two consequences
        /// worth knowing before anyone "fixes" one of them:
        ///
        ///   1. Winnings CARRY BETWEEN MATCHES, like a real alley. Nobody's
        ///      coins reset because a game ended.
        ///   2. The bar and the card table keep working when NO match is
        ///      running — which is exactly when people wander off to use them.
        ///      A match-scoped ledger would make the casino corner dead in the
        ///      lobby, which is the opposite of what the walkable-alley
        ///      hypothesis needs (see Docs/OpenQuestions.md).
        ///
        /// WHEN THIS GOES NETWORKED: this is the single property that changes
        /// (SlopLayerPlan Rule 1). Every shop, the card table and the betting
        /// layer ask this object for a ledger and call the same methods, so
        /// swapping it for a host-authoritative wrapper touches this class and
        /// nothing else.
        /// </summary>
        public CoinLedger Coins { get; private set; }

        /// <summary>Starting balance for a newly-seen player. Falls back if no config asset is wired.</summary>
        public int StartingCoins =>
            economyConfig != null ? economyConfig.StartingCoins : EconomyConfig.DEFAULT_STARTING_COINS;

        private void Awake()
        {
            // Standard Unity singleton pattern: if a second GameManager ever
            // loads (e.g. returning to a scene that contains one), destroy it.
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Built here rather than inline so the DUPLICATE GameManager above
            // returns before ever making one — otherwise a second manager would
            // create a second ledger, briefly, and any listener that grabbed it
            // in the same frame would be writing coins into an orphan.
            Coins = new CoinLedger();
        }

        /// <summary>
        /// Put a player on the books if they aren't already, with the session's
        /// starting balance. Idempotent — safe to call on every join, every
        /// reconnect, and every scene load, which is the point: callers should
        /// never have to remember whether they've done this before, because
        /// getting that wrong either wipes someone's coins or leaves them
        /// unable to buy a drink.
        /// </summary>
        /// <returns>True if they were newly registered.</returns>
        public bool EnsurePlayer(ulong playerId) => Coins.Register(playerId, StartingCoins);

        public void SetState(AppState next)
        {
            if (State == next) return;
            State = next;
            OnStateChanged?.Invoke(next);
        }
    }
}
