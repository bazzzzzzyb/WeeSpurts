using UnityEngine;
using WeeSpurts.Bowling;
using WeeSpurts.Gameplay;

namespace WeeSpurts.UI
{
    /// <summary>
    /// Zero-setup scorecard + controls overlay using Unity's immediate-mode
    /// OnGUI. Intentionally ugly: it needs no Canvas, no fonts, no wiring —
    /// perfect for the greybox phase. The real HUD (Roadmap [5]) replaces it.
    ///
    /// SETUP: same GameObject as BowlingGameController.
    /// </summary>
    [RequireComponent(typeof(BowlingGameController))]
    public class DebugHud : MonoBehaviour
    {
        private BowlingGameController _game;
        private BallConfigSwitcher _switcher; // optional (sandbox only)

        private void Awake()
        {
            _game = GetComponent<BowlingGameController>();
            _switcher = GetComponent<BallConfigSwitcher>();
        }

        private void OnGUI()
        {
            if (_game.Turns == null) return;

            // ----- Phase banner -----
            GUI.Box(new Rect(10, 10, 620, 26), _game.Phase);

            // ----- Controls hint -----
            GUI.Box(new Rect(10, 40, 620, 26),
                "←/→ slide   Shift+←/→ angle   hold SPACE = power (release to throw)   Q/E = spin   F = reset frame");

            // ----- Active ball (sandbox switcher) -----
            if (_switcher != null)
            {
                string hint = _switcher.Count > 1 ? $"  (press 1–{Mathf.Min(_switcher.Count, 9)})" : "";
                GUI.Box(new Rect(640, 10, 300, 26), $"BALL: {_switcher.ActiveName}{hint}");
            }

            // ----- Aim readouts (lateral/angle/power/spin), visible any time we're aiming -----
            BallLauncher l = _game.Launcher;
            if (l != null && l.IsAiming)
            {
                string powerText = l.ChargingPower ? $"{(int)(l.CurrentPower * 100)}%" : "-";
                GUI.Box(new Rect(10, 70, 620, 24),
                    $"LATERAL {l.CurrentLateral:F2}   ANGLE {l.CurrentAngle:F1}°   POWER {powerText}   SPIN {l.CurrentSpin:F2}");
            }

            // ----- Power meter bar, only while actively charging -----
            if (l != null && l.IsAiming && l.ChargingPower)
            {
                GUI.Box(new Rect(10, 98, 300, 22), $"POWER {(int)(l.CurrentPower * 100)}%");
                GUI.Box(new Rect(12, 100, 296f * Mathf.Clamp01(l.CurrentPower), 18), GUIContent.none);
            }

            // ----- Scorecards -----
            float y = 130;
            foreach (PlayerData p in _game.Turns.Players)
            {
                int?[] totals = p.Scorer.GetFrameTotals();
                string line = $"{p.DisplayName,-10}";
                for (int f = 0; f < 10; f++)
                    line += totals[f].HasValue ? $"{totals[f],4}" : "   -";
                line += $"   TOTAL {p.Scorer.GetTotal()}";

                bool isCurrent = p == _game.Turns.CurrentPlayer && !_game.MatchOver;
                GUI.Box(new Rect(10, y, 620, 24), (isCurrent ? "► " : "  ") + line);
                y += 26;
            }
        }
    }
}
