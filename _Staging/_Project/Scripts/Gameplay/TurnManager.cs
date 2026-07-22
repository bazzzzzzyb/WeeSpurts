using System;
using System.Collections.Generic;

namespace WeeSpurts.Gameplay
{
    /// <summary>
    /// Cycles players in a fixed order, one frame of bowling per turn
    /// (classic alley etiquette). Pure C# — the networked version will drive
    /// this same class from host commands rather than replacing it.
    /// </summary>
    public class TurnManager
    {
        private readonly List<PlayerData> _players = new List<PlayerData>();
        private int _currentIndex;

        public IReadOnlyList<PlayerData> Players => _players;
        public PlayerData CurrentPlayer => _players.Count > 0 ? _players[_currentIndex] : null;

        /// <summary>Fired when the active player changes. UI + camera listen.</summary>
        public event Action<PlayerData> OnTurnStarted;

        /// <summary>Fired when every player's 10 frames are complete.</summary>
        public event Action OnMatchComplete;

        public void AddPlayer(PlayerData player) => _players.Add(player);

        /// <summary>Call once after all players are added.</summary>
        public void StartMatch()
        {
            if (_players.Count == 0)
                throw new InvalidOperationException("TurnManager: no players added.");
            _currentIndex = 0;
            OnTurnStarted?.Invoke(CurrentPlayer);
        }

        /// <summary>
        /// Call when the current player's frame is complete.
        /// Advances to the next player who still has frames left.
        /// </summary>
        public void EndTurn()
        {
            if (AllPlayersDone())
            {
                OnMatchComplete?.Invoke();
                return;
            }

            // Find the next player whose game isn't over (players finish at
            // slightly different times only in edge cases, but be safe).
            do
            {
                _currentIndex = (_currentIndex + 1) % _players.Count;
            }
            while (CurrentPlayer.Scorer.IsGameOver);

            OnTurnStarted?.Invoke(CurrentPlayer);
        }

        public bool AllPlayersDone()
        {
            foreach (var p in _players)
                if (!p.Scorer.IsGameOver) return false;
            return true;
        }
    }
}
