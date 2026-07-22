using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using WeeSpurts.Gameplay;

namespace WeeSpurts.Bowling
{
    /// <summary>
    /// The conductor for a single-machine ("hot-seat") bowling match.
    /// Owns the loop: set rack → aim → throw → settle → count → score → next.
    ///
    /// Everything flows through small, separated pieces (launcher makes
    /// parameters, ball simulates, deck counts, scorer scores) — the same
    /// pieces the networked version will reuse with a network layer between
    /// launcher and ball. This class is the ONLY thing that knows the order
    /// of operations.
    ///
    /// SETUP: GreyboxSceneBuilder creates and wires everything.
    /// </summary>
    public class BowlingGameController : MonoBehaviour
    {
        [Header("Wired by GreyboxSceneBuilder")]
        [SerializeField] private BallConfig ballConfig;
        [SerializeField] private LaneConfig laneConfig;
        [SerializeField] private BowlingBall ball;
        [SerializeField] private PinDeck pinDeck;
        [SerializeField] private BallLauncher launcher;
        [SerializeField] private ThrowCamera throwCamera;
        [SerializeField] private Transform ballSpawn;

        public TurnManager Turns { get; private set; }
        public BallLauncher Launcher => launcher;
        public bool MatchOver { get; private set; }

        /// <summary>Human-readable phase for the debug HUD.</summary>
        public string Phase { get; private set; } = "Starting";

        private bool _ballSettled;

        public void Configure(BallConfig ballCfg, LaneConfig laneCfg, BowlingBall b,
                              PinDeck deck, BallLauncher l, ThrowCamera cam, Transform spawn)
        {
            ballConfig = ballCfg; laneConfig = laneCfg; ball = b;
            pinDeck = deck; launcher = l; throwCamera = cam; ballSpawn = spawn;
        }

        private void Start()
        {
            Turns = new TurnManager();
            for (int i = 0; i < laneConfig.DebugPlayerCount; i++)
                Turns.AddPlayer(new PlayerData((ulong)i, $"Player {i + 1}"));

            Turns.OnMatchComplete += () => { MatchOver = true; Phase = "Match over — press R to rematch"; };
            launcher.OnThrow += HandleThrow;
            ball.OnSettled += () => _ballSettled = true;

            Turns.StartMatch();
            BeginRoll();
        }

        private void Update()
        {
            // Rematch: reloading the scene is the simplest reliable reset —
            // every object comes back in a known-good state.
            if (MatchOver && Input.GetKeyDown(KeyCode.R))
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private void BeginRoll()
        {
            BowlingScorer scorer = Turns.CurrentPlayer.Scorer;

            if (scorer.NextRollNeedsFreshRack)
                pinDeck.ResetFullRack();
            else
                pinDeck.ClearDeadWood(); // leave standing pins, remove fallen ones

            pinDeck.MarkRollStart();
            ball.ResetForThrow(ballSpawn.position);
            throwCamera.SnapToAimView();
            launcher.BeginAim();

            Phase = $"{Turns.CurrentPlayer.DisplayName} — frame {scorer.CurrentFrame + 1}, roll {scorer.RollInFrame + 1}: AIM";
        }

        private void HandleThrow(LaunchParameters p)
        {
            StartCoroutine(ResolveThrow(p));
        }

        private IEnumerator ResolveThrow(LaunchParameters p)
        {
            Phase = $"{Turns.CurrentPlayer.DisplayName} — rolling… ({p})";

            // Place the ball at the chosen lateral spot, then hand physics the wheel.
            float halfLane = laneConfig.Width * 0.5f - ballConfig.Radius;
            Vector3 start = ballSpawn.position + Vector3.right * (p.LateralPosition01 * halfLane);
            ball.ResetForThrow(start);

            _ballSettled = false;
            ball.Launch(p, ballConfig);
            throwCamera.FollowBall();

            while (!_ballSettled)
                yield return null;

            // Brief beat so tumbling pins finish falling before we judge them.
            yield return new WaitForSeconds(1.0f);

            BowlingScorer scorer = Turns.CurrentPlayer.Scorer;
            // Clamp defends against a comedy edge case: a "knocked" pin
            // wobbling back upright would desync deck vs scorer counts.
            int knocked = Mathf.Min(pinDeck.CountKnockedThisRoll(), scorer.PinsStanding);

            RollOutcome outcome = scorer.AddRoll(knocked);
            Debug.Log($"{Turns.CurrentPlayer.DisplayName} knocked {knocked}. Outcome: {outcome}.");

            switch (outcome)
            {
                case RollOutcome.FrameContinues:
                    BeginRoll();
                    break;
                case RollOutcome.FrameComplete:
                case RollOutcome.GameComplete:
                    Turns.EndTurn();
                    if (!MatchOver) BeginRoll();
                    break;
            }
        }
    }
}
