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

        private void Awake() => _game = GetComponent<BowlingGameController>();

        private void OnGUI()
        {
            if (_game.Turns == null) return;

            // ----- Phase banner -----
            GUI.Box(new Rect(10, 10, 620, 26), _game.Phase);

            // ----- Controls hint -----
            GUI.Box(new Rect(10, 40, 620, 26),
                "←/→ slide   Shift+←/→ angle   hold SPACE = power (release to throw)   Q/E = spin");

            // ----- Power + spin meters, only while charging -----
            BallLauncher l = _game.Launcher;
            if (l != null && l.IsAiming && l.ChargingPower)
            {
                GUI.Box(new Rect(10, 70, 300, 22), $"POWER {(int)(l.CurrentPower * 100)}%");
                GUI.Box(new Rect(12, 72, 296f * Mathf.Clamp01(l.CurrentPower), 18), GUIContent.none);
                GUI.Box(new Rect(320, 70, 150, 22), $"SPIN {l.CurrentSpin:F2}");
            }

            // ----- Scorecards -----
            float y = 102;
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
