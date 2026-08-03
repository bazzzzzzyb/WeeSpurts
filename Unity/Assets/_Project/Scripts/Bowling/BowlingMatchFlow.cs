using System;
using System.Collections;
using Mirror;
using UnityEngine;
using WeeSpurts.Gameplay;

namespace WeeSpurts.Bowling
{
    /// <summary>
    /// THE MATCH ITSELF: whose turn it is, what is on the rack, what a throw
    /// resolved to, and what the score is. Owns the loop — set rack → aim →
    /// throw → settle → count → score → next — and is the ONLY thing that knows
    /// the order of operations.
    ///
    /// WHY THIS IS SPLIT FROM <see cref="BowlingPresentation"/> (read before
    /// moving anything between the two): this game is online-only, and
    /// Docs/Networking.md is host-authoritative. Under Mirror, THIS class is the
    /// half that must run on the host and be trusted — turn order, pin counts,
    /// scores — while cameras, reaction animations, keyboard shortcuts and the
    /// nuke's fireworks run on every machine locally. Splitting them while there
    /// is no networking code yet is enormously cheaper than unpicking one
    /// 500-line class after Mirror has started serializing it.
    ///
    /// THE RULE THAT KEEPS THE SPLIT HONEST: this class never references a
    /// camera, an animation, a HUD or a key press. When the match flow needs
    /// something to be SHOWN, it calls a hook on <see cref="BowlingPresentation"/>
    /// (a sibling component on the same GameObject) and carries on. Nothing in
    /// here reads back from presentation except one tuning number, and nothing in
    /// here knows what a PlayerAvatar is.
    ///
    /// Everything still flows through small, separated pieces (launcher makes
    /// parameters, ball simulates, deck counts, scorer scores) — the same pieces
    /// the networked version will reuse with a network layer between launcher
    /// and ball.
    ///
    /// SETUP: sits on the "BowlingGame" GameObject next to BowlingPresentation.
    /// GreyboxSceneBuilder creates and wires both.
    /// </summary>
    [RequireComponent(typeof(BowlingPresentation))]
    public class BowlingMatchFlow : NetworkBehaviour
    {
        [Header("Wired by GreyboxSceneBuilder")]
        [SerializeField] private BallConfig ballConfig;
        [SerializeField] private LaneConfig laneConfig;
        [SerializeField] private BowlingBall ball;
        [SerializeField] private PinDeck pinDeck;
        [SerializeField] private BallLauncher launcher;
        [SerializeField] private Transform ballSpawn;

        // The local presentation half. Same sibling-component pattern
        // BallConfigSwitcher/BallLauncher already use to reach this class:
        // both live on the "BowlingGame" GameObject, so neither needs wiring.
        // Fetched in Awake (never Start) because Unity gives NO ordering
        // guarantee between two components' Start methods — by the time either
        // side's Start runs, both cross-references must already be valid.
        private BowlingPresentation _presentation;

        private float _defaultGreenZoneMin;
        private float _defaultGreenZoneMax;

        // Whose turn the last aim phase belonged to, so BeginRoll can tell a new
        // player's first roll (the "you're up" beat plays) from their second
        // (it doesn't). Deliberately mirrors ThrowCameraSequence's own
        // _lastTurnPlayer rather than reading it — the camera stays a
        // presentation layer that nothing in the throw path depends on.
        //
        // Lives HERE and not on the presentation half despite feeding a camera
        // beat: it is turn IDENTITY bookkeeping, which is match-flow's job. Only
        // the lockout's DURATION comes from presentation (see BeginRoll).
        private PlayerData _lastAimTurnPlayer;

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

        /// <summary>Human-readable phase for the debug HUD.</summary>
        public string Phase { get; private set; } = "Starting";

        /// <summary>
        /// Fired once the match state has fully finished (MatchOver set,
        /// MatchInProgress cleared, Phase updated).
        ///
        /// This exists so the match can END without this class knowing that
        /// players have avatars, cameras or control modes: BowlingPresentation
        /// subscribes and hands the thrower back to roaming. Under Mirror the
        /// same shape holds — the host decides the match is over, and each
        /// machine reacts locally.
        /// </summary>
        public event Action OnMatchComplete;

        private bool _ballSettled;

        private void Awake()
        {
            _presentation = GetComponent<BowlingPresentation>();
        }

        public void Configure(BallConfig ballCfg, LaneConfig laneCfg, BowlingBall b,
                              PinDeck deck, BallLauncher l, Transform spawn)
        {
            ballConfig = ballCfg; laneConfig = laneCfg; ball = b;
            pinDeck = deck; launcher = l; ballSpawn = spawn;
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

        /// <summary>
        /// Everything that has to exist before a match can start: players,
        /// cached green zone, event subscriptions. Runs on scene load whether or
        /// not anyone is bowling yet.
        ///
        /// Called by BowlingPresentation.Start, because that is where the
        /// sandbox auto-start lives and the two must happen in that order.
        /// </summary>
        public void Initialize()
        {
            Turns = new TurnManager();
            for (int i = 0; i < laneConfig.DebugPlayerCount; i++)
                Turns.AddPlayer(new PlayerData((ulong)i, $"Player {i + 1}"));

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
        /// Begin a match. Public because the walkable alley starts matches from
        /// outside (a player walking up to the lane); the sandbox path starts it
        /// on scene load.
        ///
        /// Takes NO avatar: putting a player at the line is a local presentation
        /// concern, so BowlingPresentation.StartMatch does that half and then
        /// calls this. Under Mirror this becomes the host-side half.
        /// </summary>
        public void StartMatch()
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

            Turns.StartMatch();
            BeginRoll();
        }

        private void HandleMatchComplete()
        {
            MatchOver = true;
            MatchInProgress = false;
            Phase = "Match over — press R to rematch";

            // Presentation gives the player their legs back (EnterRoaming) —
            // this class does not know avatars exist. Raised LAST so every
            // subscriber sees fully-settled match state.
            OnMatchComplete?.Invoke();
        }

        /// <summary>
        /// Sandbox quick-reset of the CURRENT frame: re-rack, same player, same
        /// frame, without ending the match. Public because the F key that
        /// triggers it is local input, and local input lives in
        /// BowlingPresentation.
        /// </summary>
        public void ResetCurrentFrame()
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
            // this roll. No-ops in scenes with no NukeShotResolver wired.
            _presentation.ResetNukeVisualState(ball);

            BowlingScorer scorer = Turns.CurrentPlayer.Scorer;

            if (scorer.NextRollNeedsFreshRack)
                pinDeck.ResetFullRack();
            else
                pinDeck.ClearDeadWood(); // leave standing pins, remove fallen ones

            pinDeck.MarkRollStart();
            ball.ResetForThrow(ballSpawn.position);
            _presentation.SnapCameraToAimView();

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
            //
            // The DURATION comes from presentation (it is a camera-timing knob,
            // tuned against the camera beats); the "is this a new turn" decision
            // stays here, where turn identity lives.
            PlayerData current = Turns.CurrentPlayer;
            bool newTurn = !ReferenceEquals(current, _lastAimTurnPlayer);
            _lastAimTurnPlayer = current;

            launcher.BeginAim(newTurn ? _presentation.SteeringLockSecondsForNewTurn : 0f);

            Phase = $"{Turns.CurrentPlayer.DisplayName} — frame {scorer.CurrentFrame + 1}, roll {scorer.RollInFrame + 1}: AIM";
        }

        private void HandleThrow(LaunchParameters p)
        {
            // SPIKE Step 4 (Docs/spikes/MirrorKcpSpikeStatus.md): send to the
            // host instead of resolving directly. BallLauncher.OnThrow only
            // ever fires on the actual thrower's own machine (gated by
            // BowlingPresentation.ThrowInputAllowed), so in practice only the
            // right client calls this — but the server takes that entirely on
            // trust. KNOWN GAP for /qa-review and the Step 5 findings: no
            // server-side check that the caller is Turns.CurrentPlayer. Real
            // turn-authority validation is bigger than "one throw" and belongs
            // to the actual Networked Bowling feature (PLAYBOOK Stage E).
            CmdThrow(p);
        }

        [Command(requiresAuthority = false)]
        private void CmdThrow(LaunchParameters p)
        {
            RpcResolveThrow(p);
        }

        [ClientRpc]
        private void RpcResolveThrow(LaunchParameters p)
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
            // Body English + camera handoff: the thrower's pose reacts and the
            // camera picks the ball up the instant it leaves, not after it
            // settles/pins are counted. Purely cosmetic, hence one presentation
            // call rather than two direct ones.
            _presentation.OnThrowLaunched(p);

            while (!_ballSettled)
                yield return null;

            // Brief beat so tumbling pins finish falling before we judge them.
            yield return new WaitForSeconds(1.0f);

            BowlingScorer scorer = Turns.CurrentPlayer.Scorer;
            // Clamp defends against a comedy edge case: a "knocked" pin
            // wobbling back upright would desync deck vs scorer counts.
            int knocked = Mathf.Min(pinDeck.CountKnockedThisRoll(), scorer.PinsStanding);
            // SPIKE Step 4 drift measurement: every machine just replayed the
            // SAME LaunchParameters independently — BowlingBall.cs's own class
            // comment says this drifts a few cm and that's fine, the host's
            // count is authority. Logging THIS machine's own count is what
            // makes the drift observable: compare it against the host's number
            // below (RpcConfirmPinCount) on the non-host machine's console.
            Debug.Log($"[SpikeThrow] This machine's local physics knocked {knocked} pins.");

            if (!isServer)
            {
                // SPIKE SCOPE, deliberately: clients stop here. No pin-transform
                // snapping (forcing remote clients' individual Pin objects into
                // the host's exact final state), no local scoring or turn
                // continuation on non-host machines. RpcConfirmPinCount below is
                // the entire "everyone snaps to it" for this pass — a count, not
                // corrected physics. Full state-snap is a Step 5 finding, not
                // implemented here.
                yield break;
            }

            RollOutcome outcome = scorer.AddRoll(knocked);
            Debug.Log($"{Turns.CurrentPlayer.DisplayName} knocked {knocked}. Outcome: {outcome}.");
            RpcConfirmPinCount(knocked);

            switch (outcome)
            {
                case RollOutcome.FrameContinues:
                    // SPIKE SCOPE: this only reopens aim on the HOST's own
                    // machine (BeginRoll -> launcher.BeginAim runs on whichever
                    // machine's code path reaches it, and only the server's
                    // does past the isServer gate above). If the next roll
                    // belongs to a remote client, THEIR BallLauncher never
                    // hears about it. Continuing a match past one throw needs
                    // BeginRoll itself networked — the real Networked Bowling
                    // feature, out of scope here. Flag in Step 5 findings.
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
        /// SPIKE Step 4: the host's confirmed pin count, for every OTHER
        /// machine to compare against its own locally-logged count above —
        /// that comparison is the drift measurement this step exists to
        /// produce. Not a pin-state correction (see ResolveThrow's isServer
        /// branch) — just the number.
        /// </summary>
        [ClientRpc]
        private void RpcConfirmPinCount(int hostKnocked)
        {
            if (isServer) return; // host already logged/acted above
            Debug.Log($"[SpikeThrow] HOST reports {hostKnocked} pins knocked (authoritative) — " +
                      "compare against this machine's own local count logged above for drift.");
        }

        /// <summary>
        /// Nuke Shot (GameBible §9 — powerups are a sanctioned exception to
        /// §8's realism rule): green-or-not is known at release from
        /// LaunchParameters.IsGreen, the same green-zone signal a normal
        /// throw's TimingError01 already computes (0 = inside the zone). The
        /// up/lock/down movement is a pure Transform tween (NukeShotResolver,
        /// reached through the presentation half); the explosion still runs real
        /// physics (pinDeck.ApplyExplosion) so pins visibly fly, but a green hit
        /// is a GUARANTEED full clear by design — the scored outcome reads
        /// Scorer.PinsStanding directly, not CountKnockedThisRoll. The explosion
        /// is pure spectacle for a hit; CountKnockedThisRoll is still used for a
        /// normal throw's outcome (see ResolveThrow above).
        /// </summary>
        private IEnumerator ResolveNukeThrow(LaunchParameters p)
        {
            bool isGreen = p.IsGreen;
            Vector3 spawnPos = ballSpawn.position + Vector3.right * (p.LateralPosition01 * HalfLaneWidth);
            int knocked;

            if (isGreen)
            {
                Vector3 pinCenter = pinDeck.transform.position;
                yield return _presentation.PlayNukeHitSequence(ballConfig, spawnPos, pinCenter, pinDeck, ball);
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
                yield return _presentation.PlayNukeMissSequence(ballConfig, spawnPos, ball);
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
