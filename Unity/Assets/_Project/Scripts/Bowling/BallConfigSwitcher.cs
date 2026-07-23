using System.Collections.Generic;
using UnityEngine;

namespace WeeSpurts.Bowling
{
    /// <summary>
    /// Sandbox-only helper: press number keys 1–9 in Play mode to choose which
    /// BallConfig the next throw uses. Great for A/B testing ball feel and for
    /// prototyping powerups (a powerup is really just "use a different ball for
    /// the next roll"). The change applies on the NEXT throw, because
    /// BowlingGameController reads its config per-roll.
    ///
    /// This does NOT touch throw logic or networking — it only calls
    /// BowlingGameController.SetBallConfig(). Safe to delete before shipping.
    ///
    /// SETUP: sits on the same GameObject as BowlingGameController.
    /// GreyboxSceneBuilder adds it and seeds slot 1 with the default ball.
    /// Drag more BallConfig assets into the list to get slots 2, 3, …
    /// </summary>
    [RequireComponent(typeof(BowlingGameController))]
    public class BallConfigSwitcher : MonoBehaviour
    {
        [Tooltip("Ball variants. Press 1–9 in Play mode to select which one the " +
                 "next throw uses. Slot order = key order.")]
        [SerializeField] private List<BallConfig> configs = new List<BallConfig>();

        private BowlingGameController _game;

        /// <summary>Name of the currently-selected config, for the debug HUD.</summary>
        public string ActiveName { get; private set; } = "(default)";

        /// <summary>How many configs are wired, for the HUD hint.</summary>
        public int Count => configs.Count;

        private void Awake() => _game = GetComponent<BowlingGameController>();

        private void Start()
        {
            // If nobody wired a list, fall back to whatever the controller has,
            // so slot 1 always does something sensible.
            if (configs.Count == 0 && _game.ActiveBallConfig != null)
                configs.Add(_game.ActiveBallConfig);

            if (_game.ActiveBallConfig != null)
                ActiveName = _game.ActiveBallConfig.name;
        }

        private void Update()
        {
            // KeyCode.Alpha1..Alpha9 are sequential, so this maps 1→0, 2→1, …
            int slots = Mathf.Min(configs.Count, 9);
            for (int i = 0; i < slots; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    Select(i);
                    break;
                }
            }
        }

        private void Select(int index)
        {
            BallConfig cfg = configs[index];
            if (cfg == null) return;
            _game.SetBallConfig(cfg);
            ActiveName = cfg.name;
            Debug.Log($"WeeSpurts sandbox: next ball = {cfg.name} (slot {index + 1})");
        }

#if UNITY_EDITOR
        /// <summary>Editor-only: lets GreyboxSceneBuilder pre-populate the list.</summary>
        public void EditorAddConfig(BallConfig cfg)
        {
            if (cfg != null && !configs.Contains(cfg))
                configs.Add(cfg);
        }
#endif
    }
}
