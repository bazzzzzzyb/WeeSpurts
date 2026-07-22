using UnityEngine;

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
        }

        public void SetState(AppState next)
        {
            if (State == next) return;
            State = next;
            OnStateChanged?.Invoke(next);
        }
    }
}
