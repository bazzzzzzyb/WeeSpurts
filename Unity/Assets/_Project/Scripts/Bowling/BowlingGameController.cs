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

        [Tooltip("Optional. Any MonoBehaviour implementing IThrowReactionActor — the greybox capsule now, a real rig later. Drag any matching component in; leave empty to skip (e.g. scenes with no thrower actor yet).")]
        [SerializeField] private MonoBehaviour throwReactionBehaviour;
        private IThrowReactionActor _throwReaction;

        [Tooltip("Optional. Presentation layer for the Nuke Shot powerup. Leave empty in scenes that don't have one yet — a Nuke BallConfig just won't resolve until this is wired.")]
        [SerializeField] private NukeShotResolver nukeResolver;

        private float _defaultGreenZoneMin;
        private float _defaultGreenZoneMax;

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

        /// <summary>The ball config the NEXT throw will use. Read by the debug HUD.</summary>
        public BallConfig ActiveBallConfig => ballConfig;

        /// <summary>Where a fresh roll starts. Exposed for the sandbox aim preview.</summary>
        public Transform BallSpawn => ballSpawn;

        /// <summary>
        /// Half the lane width available to the ball's CENTER, accounting for
        /// its radius. The single source of truth for "how far can LateralPosition01
        /// push the ball sideways" — ResolveThrow and the sandbox aim preview
        /// both read this, so the preview always matches where the throw resolves.
        /// </summary>
        public float HalfLaneWidth => laneConfig.Width * 0.5f - ballConfig.Radius;

        /// <summary>
        /// Swap the active ball config at runtime. Because Launch() reads the
        /// config per-throw, most of a config's effect takes hold on the next
        /// roll — but the green zone is armed for the throw currently being
        /// aimed (BallLauncher reads it live), so we re-push it immediately
        /// here too. This is the seam the sandbox switcher (and later,
        /// powerups) hooks into.
        /// </summary>
        public void SetBallConfig(BallConfig cfg)
        {
            if (cfg == null) return;
            ballConfig = cfg;
            ApplyGreenZoneForActiveBall();
        }

        /// <summary>
        /// Pushes the green zone bounds matching the currently active ball
        /// config into the launcher. Nuke Shot's green zone may be tighter
        /// than the default throw's — same ComputeTimingError signal
        /// underneath, just parameterized per active ball config. Called from
        /// BeginRoll (start of every roll) AND SetBallConfig (mid-aim ball
        /// switches), so whichever config is active always governs the throw
        /// about to happen, not just the one active when the roll began.
        /// </summary>
        private void ApplyGreenZoneForActiveBall()
        {
            launcher.SetGreenZone(
                ballConfig.IsNuke ? ballConfig.NukeGreenZoneMin : _defaultGreenZoneMin,
                ballConfig.IsNuke ? ballConfig.NukeGreenZoneMax : _defaultGreenZoneMax);
        }

        /// <summary>Wires the optional thrower reaction actor (GreyboxSceneBuilder pattern, same as SetBallConfig).</summary>
        public void SetThrowReactionActor(MonoBehaviour actor) => throwReactionBehaviour = actor;

        /// <summary>Wires the optional Nuke Shot presentation layer (same pattern as SetThrowReactionActor).</summary>
        public void SetNukeResolver(NukeShotResolver resolver) => nukeResolver = resolver;

        private void Start()
        {
            Turns = new TurnManager();
            for (int i = 0; i < laneConfig.DebugPlayerCount; i++)
                Turns.AddPlayer(new PlayerData((ulong)i, $"Player {i + 1}"));

            // Unity can't serialize an interface field directly, so
            // throwReactionBehaviour is serialized as a MonoBehaviour and
            // cast here. "as" on a null reference just yields null, so an
            // unassigned slot safely resolves to no-op via the ?. below.
            _throwReaction = throwReactionBehaviour as IThrowReactionActor;

            // Cache the normal green zone BEFORE anything (a Nuke throw) might
            // override it via launcher.SetGreenZone, so a Nuke roll can restore
            // it for the following normal roll.
            _defaultGreenZoneMin = launcher.GreenZoneMin;
            _defaultGreenZoneMax = launcher.GreenZoneMax;

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

            // Sandbox: quick-reset just the CURRENT frame (re-rack, same
            // player, same frame) without ending the match. For retrying one
            // throw setup — e.g. bounce/spin feel-testing — without playing
            // back through pin counting each time. Not available once the
            // match is over; R already covers a full restart there.
            if (!MatchOver && Input.GetKeyDown(KeyCode.F))
                ResetCurrentFrame();
        }

        private void ResetCurrentFrame()
        {
            // Cancel any throw still resolving so it can't fight the reset;
            // ball.ResetForThrow below independently clears its in-flight state.
            StopAllCoroutines();

            Turns.CurrentPlayer.Scorer.ResetCurrentFrame();
            _ballSettled = false;
            BeginRoll();
        }

        private void BeginRoll()
        {
            // Defensive cleanup FIRST: covers every path that starts a new roll
            // (normal frame progression, turn-end-then-next-player, AND the F-key
            // sandbox reset) with one call, so an interrupted nuke sequence never
            // leaves stale visual state (stuck sphere/looping poof) bleeding into
            // this roll. No-ops via ?. in scenes with no NukeShotResolver wired.
            nukeResolver?.ResetVisualState(ball);

            BowlingScorer scorer = Turns.CurrentPlayer.Scorer;

            if (scorer.NextRollNeedsFreshRack)
                pinDeck.ResetFullRack();
            else
                pinDeck.ClearDeadWood(); // leave standing pins, remove fallen ones

            pinDeck.MarkRollStart();
            ball.ResetForThrow(ballSpawn.position);
            throwCamera.SnapToAimView();

            ApplyGreenZoneForActiveBall();

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

            if (ballConfig.IsNuke)
            {
                yield return ResolveNukeThrow(p);
                yield break;
            }

            // Place the ball at the chosen lateral spot, then hand physics the wheel.
            Vector3 start = ballSpawn.position + Vector3.right * (p.LateralPosition01 * HalfLaneWidth);
            ball.ResetForThrow(start);

            _ballSettled = false;
            ball.Launch(p, ballConfig);
            // Body English: the thrower's pose reacts the instant the ball
            // leaves, not after it settles/pins are counted. Purely cosmetic.
            _throwReaction?.PlayReaction(p);
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

        /// <summary>
        /// Nuke Shot (GameBible §9 — powerups are a sanctioned exception to
        /// §8's realism rule): green-or-not is known at release from the SAME
        /// TimingError01 signal a normal throw already computes (0 = inside
        /// the zone). The up/lock/down movement is a pure Transform tween
        /// (NukeShotResolver); the explosion still runs real physics
        /// (pinDeck.ApplyExplosion) so pins visibly fly, but a green hit is a
        /// GUARANTEED full clear by design — the scored outcome reads
        /// Scorer.PinsStanding directly, not CountKnockedThisRoll. The
        /// explosion is pure spectacle for a hit; CountKnockedThisRoll is
        /// still used for a normal throw's outcome (see ResolveThrow above).
        /// </summary>
        private IEnumerator ResolveNukeThrow(LaunchParameters p)
        {
            bool isGreen = p.TimingError01 == 0f;
            Vector3 spawnPos = ballSpawn.position + Vector3.right * (p.LateralPosition01 * HalfLaneWidth);
            int knocked;

            if (isGreen)
            {
                Vector3 pinCenter = pinDeck.transform.position;
                yield return nukeResolver.PlayHitSequence(ballConfig, spawnPos, pinCenter, pinDeck, ball, throwCamera);
                // Bigger blast (see BallConfig Nuke tunables) deserves a beat longer
                // than the normal throw's settle wait so the spectacle actually reads
                // before phase/UI moves on — this is NOT waiting for physics to
                // settle so we can trust a count anymore, see below.
                yield return new WaitForSeconds(0.85f);

                // Design guarantee, not a physics read (GameBible §9 — powerups are
                // spectacle-first, arcade logic; direct instruction from Tony): a
                // green-timed Nuke ALWAYS clears every pin standing at the start of
                // this roll. pinDeck.ApplyExplosion above still ran real PhysX and
                // pins still visibly fly everywhere — that's pure spectacle now,
                // fully decoupled from the scoring result. Deliberately NOT calling
                // CountKnockedThisRoll here: the outcome is a pure function of game
                // state (PinsStanding), zero dependency on PhysX results, so a
                // future host/client split trivially agrees on it with no
                // physics-divergence risk for this specific outcome.
                knocked = Turns.CurrentPlayer.Scorer.PinsStanding;
            }
            else
            {
                yield return nukeResolver.PlayMissSequence(ballConfig, spawnPos, ball, throwCamera);
                knocked = 0;
            }

            BowlingScorer scorer = Turns.CurrentPlayer.Scorer;
            RollOutcome outcome = scorer.AddRoll(knocked);
            Debug.Log($"{Turns.CurrentPlayer.DisplayName} NUKE {(isGreen ? "HIT" : "MISS")} — knocked {knocked}. Outcome: {outcome}.");

            if (!isGreen)
            {
                // Punishing whiff by design (BowlingFeelIdeas.md fairness guard): a
                // nuke miss always ends the WHOLE turn. But ending the turn mid-frame
                // (while the scorer still thinks a second roll is coming) desyncs the
                // scorer's roll/frame bookkeeping from the single shared PinDeck the
                // NEXT player uses — so force the frame to actually finish first, via
                // AddRoll's own existing public logic (never touching BowlingScorer
                // internals), before advancing the turn.
                while (outcome == RollOutcome.FrameContinues)
                    outcome = scorer.AddRoll(0);

                Turns.EndTurn();
                if (!MatchOver) BeginRoll();
                yield break;
            }

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
