using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using WeeSpurts.Gameplay;
using WeeSpurts.Player;

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

        [Header("Match start (control modes)")]
        [Tooltip("SANDBOX FEEL-TESTING PATH: start the match the instant the scene loads, so you can throw immediately without walking to a lane kiosk first. DEFAULT TRUE so every existing scene behaves exactly as it did before roaming existed. RoamingSetupTool switches it OFF in the walkable venue scene, where the match is meant to start diegetically.")]
        [SerializeField] private bool sandboxAutoStart = true;

        [Tooltip("Optional. Where the active thrower stands at the foul line — the avatar is snapped here on their turn. Empty just means the avatar changes mode without being moved.")]
        [SerializeField] private Transform throwingStance;

        [Tooltip("Optional. Which avatar sandboxAutoStart hands the first turn to. Leave empty in scenes with no PlayerAvatar (e.g. BowlingAlley.unity) — the match then starts with nobody's mode touched, exactly as before.")]
        [SerializeField] private PlayerAvatar sandboxThrower;

        [Tooltip("Seconds of ←/→ steering lockout at the START of a player's turn, while the 'you're up' camera beat is looking BACK at the thrower and left/right therefore read mirrored ON SCREEN. DEFAULT 0 = off, i.e. steer immediately and live with the mirrored window. Set it to ThrowCameraSequenceConfig's ReturnDuration + ADuration (0.75 + 0.9 = 1.65 at the tuned defaults) to suppress steering until the shot swings down-lane, or 2.45 to also cover the swing itself. Power and spin are never locked. NOTE: this is unrelated to a NEGATIVE HalfLaneWidth, which inverts aim permanently — see HalfLaneWidth.")]
        [SerializeField] private float turnStartSteeringLockSeconds = 0f;

        private float _defaultGreenZoneMin;
        private float _defaultGreenZoneMax;

        // Whose turn the last aim phase belonged to, so BeginRoll can tell a new
        // player's first roll (the "you're up" beat plays) from their second
        // (it doesn't). Deliberately mirrors ThrowCameraSequence's own
        // _lastTurnPlayer rather than reading it — the camera stays a
        // presentation layer that nothing in the throw path depends on.
        private PlayerData _lastAimTurnPlayer;

        // Whoever is currently at the line. Stored so the match can hand control
        // back to them (EnterRoaming) when it ends.
        private PlayerAvatar _thrower;

        public TurnManager Turns { get; private set; }
        public BallLauncher Launcher => launcher;
        public bool MatchOver { get; private set; }

        /// <summary>
        /// True between StartMatch and the match completing. Guards against a
        /// second StartMatch call — e.g. a player walking back up to the kiosk
        /// mid-match and triggering it again — restarting the game under
        /// everyone.
        /// </summary>
        public bool MatchInProgress { get; private set; }

        /// <summary>
        /// Should the bowling HUD be on screen at all? False while roaming: the
        /// scorecard, phase banner and control hints are match furniture, and
        /// leaving them up while you walk around the alley reads as UI debris
        /// rather than as a game that hasn't started (Tony's call).
        ///
        /// Deliberately NOT just MatchInProgress — that goes false the instant
        /// the match completes, which would yank the final scores off screen at
        /// exactly the moment everyone wants to read them. MatchOver keeps them
        /// up until R reloads the scene.
        /// </summary>
        public bool HudVisible => MatchInProgress || MatchOver;

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
        ///
        /// NEVER NEGATIVE, and that clamp is load-bearing. This subtraction goes
        /// below zero the moment a ball's Radius exceeds half the lane Width —
        /// a ball too fat to fit. Unclamped, every consumer multiplies the
        /// player's aim by a NEGATIVE number, so left/right silently swap in the
        /// thrower's slide, the aim preview AND the resolved throw at once. It
        /// reads as "the controls are backwards", nothing logs, and the ball can
        /// still LOOK normal because GreyboxSceneBuilder bakes the sphere's
        /// visual scale at scene-build time — so a config edited after the scene
        /// was built shows no visual clue at all. (This cost us a debugging
        /// session: BallConfig.Radius 1 against LaneConfig.Width 1.4.)
        ///
        /// Clamping to zero is the honest answer rather than a fudge: a ball
        /// exactly as wide as the lane genuinely has nowhere to go sideways, so
        /// aim collapses to the centreline instead of inverting.
        /// </summary>
        public float HalfLaneWidth
        {
            get
            {
                float half = laneConfig.Width * 0.5f - ballConfig.Radius;
                if (half >= 0f) return half;

                WarnOnceAboutOversizedBall(half);
                return 0f;
            }
        }

        // Which config we have already complained about. This property is read
        // every frame by the aim preview, so an unguarded Debug.LogError would
        // produce thousands of identical lines and bury the message it is trying
        // to deliver. Keyed on the config so switching balls re-reports.
        //
        // SINGLE-SLOT, NOT A SEEN-SET: this remembers only the LAST config
        // warned about, not history. Switching oversized A -> oversized B ->
        // back to A re-warns on the return to A, which is fine for a human
        // swapping balls between throws (a fresh transition is a fresh event).
        // It would NOT stay quiet if something cycled between two oversized
        // configs every frame (e.g. a future powerup alternating BallConfig
        // rapidly) — that would spam the log once per cycle rather than once
        // ever. Nothing in the game does that today; worth remembering if one
        // ever does.
        private BallConfig _oversizedBallWarned;

        private void WarnOnceAboutOversizedBall(float half)
        {
            if (ReferenceEquals(_oversizedBallWarned, ballConfig)) return;
            _oversizedBallWarned = ballConfig;

            Debug.LogError(
                $"[Bowling] '{ballConfig.name}' has Radius {ballConfig.Radius:0.###}, which is wider than " +
                $"half of LaneConfig.Width ({laneConfig.Width:0.###} / 2 = {laneConfig.Width * 0.5f:0.###}). " +
                $"HalfLaneWidth would be {half:0.###} — NEGATIVE — which inverts left/right aim for the " +
                "thrower, the aim preview and the actual throw. It has been clamped to 0 (no lateral aim) " +
                "so the controls are dead rather than backwards.\n" +
                $"FIX IT: set Radius below {laneConfig.Width * 0.5f:0.###} on that BallConfig asset " +
                "(0.11 is the tuned default), or widen LaneConfig.Width.\n" +
                "NOTE the ball may still LOOK the right size — the sphere's visual scale is baked when the " +
                "scene is built, so it does not follow a config edited afterwards.", ballConfig);
        }

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
            Initialize();

            // Split in two so that a walkable-alley scene can wire everything up
            // at load and only actually START the match when a player walks up
            // to the lane (Docs/OpenQuestions.md — game start is diegetic, not a
            // menu button). Step 2 of that plan builds the interaction; this is
            // just the seam it will call into.
            if (sandboxAutoStart) StartMatch(sandboxThrower);
        }

        /// <summary>
        /// Everything that has to exist before a match can start: players,
        /// cached green zone, event subscriptions. Runs on scene load whether or
        /// not anyone is bowling yet.
        /// </summary>
        private void Initialize()
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

            Turns.OnMatchComplete += HandleMatchComplete;
            launcher.OnThrow += HandleThrow;
            ball.OnSettled += () => _ballSettled = true;
        }

        /// <summary>
        /// Begin a match, optionally taking control of an avatar and putting
        /// them at the line. Public because the walkable alley starts matches
        /// from outside (a player walking up to the lane); the sandbox path
        /// calls it from Start.
        /// </summary>
        public void StartMatch(PlayerAvatar thrower)
        {
            if (Turns == null)
            {
                Debug.LogError("[Bowling] StartMatch was called before Initialize ran. " +
                               "Nothing has been started.", this);
                return;
            }

            // Idempotent by design: a second trigger while a match is running
            // must be ignored, not restart the game under everyone.
            if (MatchInProgress) return;
            MatchInProgress = true;

            _thrower = thrower;
            // The avatar owns its own mode — we ask, we don't reach in and flip
            // components ourselves (see PlayerAvatar's class comment). Null is
            // fine: scenes with no avatar just start the match as they always did.
            if (_thrower != null) _thrower.EnterBowling(throwingStance);

            Turns.StartMatch();
            BeginRoll();
        }

        private void HandleMatchComplete()
        {
            MatchOver = true;
            MatchInProgress = false;
            Phase = "Match over — press R to rematch";

            // Give the player their legs back rather than leaving them stranded
            // at the foul line with no camera control. R still reloads the scene
            // for a rematch, exactly as before.
            if (_thrower != null) _thrower.EnterRoaming();
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
            //
            // MatchInProgress is also required now that a match no longer always
            // auto-starts: without it, tapping F while walking around the venue
            // would call BeginRoll and open an aim phase behind your back, with
            // nobody at the line.
            if (!MatchOver && MatchInProgress && Input.GetKeyDown(KeyCode.F))
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

            // Steering lockout for the turn-start beat. ThrowCameraSequence plays
            // the "you're up" shot — camera in front, looking BACK — only when the
            // player CHANGES, and that shot mirrors left/right on screen. So the
            // lockout is applied on exactly the same condition, and roll 2 of a
            // frame (or an F-key reset) steers immediately. See
            // BallLauncher.SteeringLocked for why the launcher is told a duration
            // rather than being allowed to ask the camera what it is doing.
            //
            // ReferenceEquals, not ==: we care whether it is literally the same
            // PlayerData object, matching how ThrowCameraSequence decides the
            // same thing. The two must agree or the lockout covers the wrong roll.
            PlayerData current = Turns.CurrentPlayer;
            bool newTurn = !ReferenceEquals(current, _lastAimTurnPlayer);
            _lastAimTurnPlayer = current;

            launcher.BeginAim(newTurn ? turnStartSteeringLockSeconds : 0f);

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
