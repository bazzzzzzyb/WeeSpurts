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
    /// GreyboxSceneBuilder seeds the canonical five on every rebuild —
    /// 1 default, 2 BouncyBall, 3 Cannonball, 4 Wobbler, 5 Nuke. Drag more
    /// BallConfig assets into the list to get slots 6, 7, …
    ///
    /// Key 1 is guaranteed to be the default ball at runtime even if the
    /// serialized list has lost it (see Start) — this list is ordinary
    /// Inspector state and drifts easily.
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
            // A null slot would silently shift every key after it (delete entry 2
            // in the Inspector and suddenly 3 throws what 4 used to), so drop them
            // before anything else looks at the list.
            configs.RemoveAll(c => c == null);

            // The default ball is ALWAYS key 1, whatever the serialized list says.
            // This list is ordinary Inspector state, so it drifts: a hand-edit, a
            // stray default Preset re-applying an old snapshot, or a scene saved
            // mid-tinker can all drop the plain throw and leave only the powerups.
            // Insert rather than Add so key 1 stays "the normal ball" and the
            // variants keep their order behind it.
            if (_game.ActiveBallConfig != null && !configs.Contains(_game.ActiveBallConfig))
                configs.Insert(0, _game.ActiveBallConfig);

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

        /// <summary>
        /// Editor-only: empties the list so GreyboxSceneBuilder can seed a known
        /// slot order from scratch. Without this a rebuild MERGES into whatever
        /// was already there — a stale default Preset, or a hand-edit — so the
        /// key order depended on the component's history rather than the builder.
        /// </summary>
        public void EditorClearConfigs() => configs.Clear();
#endif
    }
}
