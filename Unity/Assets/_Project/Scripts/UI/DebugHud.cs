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
    /// SETUP: same GameObject as BowlingMatchFlow.
    /// </summary>
    [RequireComponent(typeof(BowlingMatchFlow))]
    public class DebugHud : MonoBehaviour
    {
        // The MATCH half: everything this HUD draws (turns, phase, scores, the
        // armed ball) is match state. It deliberately does not hold a
        // BowlingPresentation reference — a scoreboard has no business asking
        // whose keyboard is live.
        private BowlingMatchFlow _game;
        private BallConfigSwitcher _switcher; // optional (sandbox only)

        private void Awake()
        {
            _game = GetComponent<BowlingMatchFlow>();
            _switcher = GetComponent<BallConfigSwitcher>();
        }

        private void OnGUI()
        {
            if (_game.Turns == null) return;

            // Nothing on screen while roaming: the scorecard, phase banner and
            // control hints are match furniture, and leaving them up while you
            // walk around the alley reads as UI debris rather than as a game
            // that hasn't started (Tony's call). Turns is non-null from scene
            // load now (BowlingMatchFlow.Initialize builds the players up front
            // and only STARTS the match on request), so the null check above no
            // longer implies "a match is happening" the way it used to.
            //
            // Deliberately NOT just MatchInProgress — that goes false the
            // instant the match completes, which would yank the final scores off
            // screen at exactly the moment everyone wants to read them.
            // MatchOver keeps them up until R reloads the scene.
            if (!(_game.MatchInProgress || _game.MatchOver)) return;

            // ----- Phase banner -----
            GUI.Box(new Rect(10, 10, 620, 26), _game.Phase);

            // ----- Controls hint -----
            // Q/E spin is GONE — spin is now the 2D selector (SpinSelectorHud),
            // dragged with the mouse or nudged with I/J/K/L, C to re-centre.
            GUI.Box(new Rect(10, 40, 620, 26),
                "←/→ slide   Shift+←/→ angle   drag spin ball (or IJKL, C centres)   hold SPACE = power   F = reset");

            // ----- Active ball (sandbox switcher) -----
            if (_switcher != null)
            {
                string hint = _switcher.Count > 1 ? $"  (press 1–{Mathf.Min(_switcher.Count, 9)})" : "";
                // Call out a nuke-armed ball explicitly. The name alone is not
                // enough: IsNuke lives on the BallConfig ASSET, so a config named
                // "BallConfig" can quietly be flagged as a nuke and every throw
                // then resolves as one — which looks like the game is broken
                // rather than like the ball is wrong. (This exact thing bit us.)
                BallConfig active = _game.ActiveBallConfig;
                string nukeFlag = active != null && active.IsNuke ? "   [NUKE ARMED]" : "";
                GUI.Box(new Rect(640, 10, 300, 26), $"BALL: {_switcher.ActiveName}{hint}{nukeFlag}");
            }

            // ----- Aim readouts (lateral/angle/power/spin), visible any time we're aiming -----
            BallLauncher l = _game.Launcher;
            if (l != null && l.IsAiming)
            {
                string powerText = l.ChargingPower ? $"{(int)(l.CurrentPower * 100)}%" : "-";
                // Spin is 2D now: X is the hook (- left, + right), Y is roll vs
                // skid (+ topspin, - backspin). Both numeric, next to the
                // graphical selector, so a feel test can be reported as numbers.
                GUI.Box(new Rect(10, 70, 620, 24),
                    $"LATERAL {l.CurrentLateral:F2}   ANGLE {l.CurrentAngle:F1}°   POWER {powerText}" +
                    $"   SPIN X {l.CurrentSpin.x:+0.00;-0.00;0.00} Y {l.CurrentSpin.y:+0.00;-0.00;0.00}");
            }

            // ----- Power meter bar, only while actively charging -----
            if (l != null && l.IsAiming && l.ChargingPower)
            {
                GUI.Box(new Rect(10, 98, 300, 22), $"POWER {(int)(l.CurrentPower * 100)}%");

                // Green target zone, drawn UNDER the fill so it "gets covered" as
                // power climbs through it — the player watches the fill approach
                // and pass through the target.
                float zoneX = 12 + 296f * l.GreenZoneMin;
                float zoneW = 296f * (l.GreenZoneMax - l.GreenZoneMin);
                Color prevColor = GUI.color;
                GUI.color = Color.green;
                GUI.Box(new Rect(zoneX, 100, zoneW, 18), GUIContent.none);
                GUI.color = prevColor;

                GUI.Box(new Rect(12, 100, 296f * Mathf.Clamp01(l.CurrentPower), 18), GUIContent.none);
            }

            // ----- Scorecards -----
            // Header spells out that the numbers are RUNNING totals. Without it
            // the row reads as ten separate frame scores, and adding them up
            // double-counts every bonus — a strike in frame 1 appears to add its
            // 10 again in every later box. The score is the LAST number, never
            // the sum. (This confused a real playtest; hence the label.)
            float y = 130;
            string head = $"{"",-10}";
            for (int f = 0; f < 10; f++) head += $"{f + 1,5}";
            GUI.Box(new Rect(10, y, 700, 22), "  " + head + "   running →");
            y += 24;

            foreach (PlayerData p in _game.Turns.Players)
            {
                int?[] totals = p.Scorer.GetFrameTotals();
                int[][] frameRolls = p.Scorer.GetFrameRolls();

                // Row 1: what was actually thrown, per frame. A strike's frame
                // box correctly stays blank until its two bonus rolls exist —
                // real scorecard behaviour, but it reads as "my strike didn't
                // count", so these marks prove the roll WAS recorded instantly.
                // Frame splitting comes from BowlingScorer.GetFrameRolls(), so
                // no scoring logic is duplicated here and this can never
                // disagree with the totals underneath it.
                string markLine = $"{p.DisplayName,-10}";
                for (int f = 0; f < 10; f++) markLine += $"{FrameMarks(frameRolls[f]),5}";

                // Row 2: the formal running totals, aligned under their frame.
                string totalLine = $"{"",-10}";
                for (int f = 0; f < 10; f++)
                    totalLine += totals[f].HasValue ? $"{totals[f],5}" : "    -";

                // The LIVE score, not GetTotal(). GetTotal() only sums frames
                // whose bonuses have fully resolved, so it reads 0 for two more
                // turns after a strike — you knock ten pins down and the
                // scoreboard says nothing happened. GetProvisionalTotal() counts
                // the pins immediately and folds each bonus in as it becomes
                // known, and lands on exactly the same number once the game
                // finishes. The per-frame boxes still use the formal resolved
                // totals, so the scorecard itself stays honest.
                totalLine += $"   TOTAL {p.Scorer.GetProvisionalTotal()}";

                bool isCurrent = p == _game.Turns.CurrentPlayer && !_game.MatchOver;
                GUI.Box(new Rect(10, y, 700, 22), (isCurrent ? "► " : "  ") + markLine);
                GUI.Box(new Rect(10, y + 20, 700, 22), "  " + totalLine);

                y += 46;
            }
        }

        /// <summary>
        /// Standard scorecard notation for one frame: X strike, / spare, - miss.
        /// Presentation only — it never decides what a frame is worth, it just
        /// draws what BowlingScorer already recorded.
        /// </summary>
        private static string FrameMarks(int[] rolls)
        {
            if (rolls == null || rolls.Length == 0) return "";

            // Tracks pins standing AND whether the rack is fresh, because those
            // two together are what separate a strike from a spare. Ten pins off
            // a FRESH rack is a strike; ten off a rack you already threw at is a
            // spare. Summing the pair instead would mark a gutter-then-all-ten
            // as "-X" when it is really "-/", and in the 10th frame — where the
            // rack resets mid-frame — would mark X then 4 then 6 as a spare
            // across two different racks.
            string s = "";
            int standing = 10;
            bool freshRack = true;

            foreach (int r in rolls)
            {
                if (r == standing && freshRack) s += "X";
                else if (r == standing && r > 0) s += "/";
                else if (r == 0) s += "-";
                else s += r.ToString();

                if (r == standing) { standing = 10; freshRack = true; }
                else { standing -= r; freshRack = false; }
            }
            return s;
        }
    }
}
