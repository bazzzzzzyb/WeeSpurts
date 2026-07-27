using UnityEngine;
using WeeSpurts.Gameplay;

namespace WeeSpurts.Bowling
{
    /// <summary>
    /// The scripted, Wii-Sports-style throw camera: ONE continuous six-beat
    /// move from "you're up" to the ball hitting the pins. See
    /// ThrowCameraSequenceConfig for what each beat is and every tuning knob.
    ///
    /// HOW IT COOPERATES WITH ThrowCamera (read this before changing anything):
    /// both components live on the Main Camera, but only ONE of them is ever
    /// allowed to write transform.position — ThrowCamera, in its LateUpdate.
    /// This class computes a pose in Update() and hands it over via
    /// throwCamera.SetSequenceFraming(). Unity guarantees every Update() runs
    /// before any LateUpdate(), so the pose we push is always this frame's, and
    /// no script execution order tweaking is needed.
    ///
    /// Why not just write transform.position here? Because ThrowCamera keeps the
    /// camera's clean anchor (_basePosition) separate from screen shake
    /// (_shakeOffset) and adds them together every frame. If we wrote the
    /// transform directly, last frame's shake would get baked into this frame's
    /// starting point and the camera would slowly drift away from where it is
    /// supposed to be instead of settling back after a shake.
    ///
    /// EVERY BEAT IS A RELATIVE BLEND, not an absolute keyframe: when a beat
    /// starts we capture the pose the camera is at RIGHT NOW and ease from there
    /// to that beat's target. That is what makes this read as one move with no
    /// cuts, and it is why an interrupted beat (the player releases early, the
    /// F-key resets the frame) always hands off smoothly from wherever it got to.
    ///
    /// PRESENTATION ONLY. This class reads public state and never writes any of
    /// it: it cannot touch LaunchParameters, physics, scoring, or the throw flow,
    /// so it cannot affect anything Docs/Networking.md syncs. There is no
    /// UnityEngine.Random anywhere in here — the whole move is deterministic and
    /// config-driven. (Camera.fieldOfView and Camera.aspect are READ for beat F's
    /// framing solve; they are never written.)
    ///
    /// SETUP: on the Main Camera, next to ThrowCamera. GreyboxSceneBuilder adds
    /// and wires it.
    /// </summary>
    [RequireComponent(typeof(ThrowCamera))]
    public class ThrowCameraSequence : MonoBehaviour
    {
        /// <summary>
        /// How far in front of the camera a "look point" is placed when we need
        /// to convert a rotation into a point to aim at. Any distance works (the
        /// resulting rotation is the same); 10m just keeps the numbers readable.
        /// </summary>
        private const float LOOK_POINT_DISTANCE = 10f;

        /// <summary>
        /// The six beats, plus the "hold" states that sit at the end of a beat
        /// waiting for the player (A2 waits for the charge, B waits for the
        /// release, F waits for the roll to finish).
        /// </summary>
        private enum Beat
        {
            Off,                // not driving the camera at all (e.g. a Nuke throw owns it)
            YoureUp,            // A
            TakeStance,         // A2 — easing into the aim view
            TakeStanceHold,     // A2 — parked in the aim view, waiting for SPACE
            ChargeIn,           // B — slow push in while the power meter charges
            ChargeInHold,       // B — arrived; parked, still waiting for the release
            Release,            // D
            Travel,             // E
            Impact,             // F — ball-driven approach
            ImpactHold          // F — arrived; the move is over
        }

        [Header("Wired by GreyboxSceneBuilder")]
        [Tooltip("Every timing/height/distance/curve knob for the move. Without this the sequence disables itself and ThrowCamera behaves exactly as it did before.")]
        [SerializeField] private ThrowCameraSequenceConfig config;

        [Tooltip("Read-only: we poll IsAiming / ChargingPower and subscribe to OnThrow. Never written to.")]
        [SerializeField] private BallLauncher launcher;

        [Tooltip("Read-only: used for whose turn it is (beat A plays only on a NEW player's turn) and whether the active ball is a Nuke.")]
        [SerializeField] private BowlingMatchFlow game;

        [Tooltip("Read-only: the ball's position drives beats E and F, and its OnSettled event ends the move.")]
        [SerializeField] private BowlingBall ball;

        [Tooltip("The PinDeck's transform. Beat F frames on this. If missing, beat F simply never fires and the move ends gracefully instead.")]
        [SerializeField] private Transform pinDeck;

        [Tooltip("The ThrowerProxy's transform. Beats A, A2 and B are all anchored to this, and beat D swings past it. Required.")]
        [SerializeField] private Transform thrower;

        [Tooltip("Read-only: lane Width feeds beat F's 'how many lanes fill the screen' framing solve.")]
        [SerializeField] private LaneConfig laneConfig;

        [Tooltip("The Camera on this GameObject. Its field of view and aspect ratio are READ (never written) so beat F frames identically on any monitor. Left empty, it is found automatically.")]
        [SerializeField] private Camera sequenceCamera;

        private ThrowCamera _throwCamera;
        // False when a reference is missing: the component then does nothing at
        // all rather than throwing a NullReferenceException every single frame.
        private bool _wired;

        // --- beat state ---
        /// <summary>
        /// How a beat travels from its starting pose to its target pose. This is
        /// about the PATH, not the timing — the easing curve is separate.
        /// </summary>
        private enum BlendMode
        {
            /// <summary>Straight line. The default; correct whenever nothing is in the way.</summary>
            Linear,
            /// <summary>Arc around the thrower. Interpolates angle and radius separately, so it covers both orbiting them and dollying straight in (B).</summary>
            Cylindrical,
            /// <summary>Straight push, bulged to one side to slip past the thrower (D).</summary>
            SidePass
        }

        private Beat _beat = Beat.Off;
        private float _beatElapsed;
        private float _blendDuration;
        private AnimationCurve _blendCurve;
        private BlendMode _blendMode;
        private Vector3 _fromPosition;
        private Vector3 _fromLookPoint;

        // The pose we pushed last frame. This is the "from" for the next beat,
        // and it is deliberately the CLEAN value (no screen shake baked in).
        private Vector3 _currentPosition;
        private Vector3 _currentLookPoint;
        private bool _hasPushedPose;

        // --- input edge detection ---
        // We poll rather than subscribe because BallLauncher exposes these as
        // plain properties. A "rising edge" just means "false last frame, true
        // this frame", i.e. the instant something started.
        private bool _wasAiming;
        private bool _wasCharging;
        // Set by the OnThrow handler, consumed in Update. Events can fire in any
        // order relative to our Update, so we never do camera work inside them.
        private bool _throwPending;
        private bool _ballSettled;

        // Which player the last roll belonged to. Beat A ("you're up") plays only
        // when this CHANGES — roll 2 of a frame, or an F-key reset, skips straight
        // to the stance. Compared by reference: PlayerData is a plain C# object
        // owned by TurnManager, one instance per player for the whole match.
        private PlayerData _lastTurnPlayer;

        // --- post-release watchdogs (see the config's "Safety nets" header) ---
        private bool _rollInFlight;
        private float _maxBallZ;
        private float _lastProgressTime;
        private float _throwStartTime;

        // --- beat F progress ---
        private float _impactStartZ;
        private float _impactU;          // 0 = just triggered, 1 = ball at the pins
        private bool _impactForceComplete;

        // Lane anchors, refreshed each frame from the ball spawn so moving the
        // spawn point in the editor reshapes the move without a rebuild.
        private float _laneOriginZ;
        private float _laneCentreX;

        private void Awake()
        {
            _throwCamera = GetComponent<ThrowCamera>();
            if (sequenceCamera == null) sequenceCamera = GetComponent<Camera>();
        }

        private void OnEnable()
        {
            _wired = config != null && _throwCamera != null && sequenceCamera != null
                     && launcher != null && game != null && ball != null && thrower != null;

            if (!_wired)
            {
                Debug.LogWarning("ThrowCameraSequence: a reference is missing, so the scripted throw " +
                                 "camera is disabled for this scene. ThrowCamera's original behaviour " +
                                 "is unaffected. Re-run WeeSpurts > Build Greybox Bowling Scene to rewire.", this);
                return;
            }

            // Subscribing in OnEnable and unsubscribing in OnDisable (below) is
            // the rule for events: a handler left attached to an object that
            // outlives this component is a genuine, hard-to-find bug — it fires
            // on a destroyed camera after a scene reload.
            launcher.OnThrow += HandleThrow;
            ball.OnSettled += HandleBallSettled;

            // A sensible starting pose in case anything reads it before the first
            // roll begins; BeginRollFraming re-seeds from the live transform.
            _currentPosition = _throwCamera.AimViewPosition;
            _currentLookPoint = _currentPosition
                                + Quaternion.Euler(_throwCamera.AimViewEuler) * Vector3.forward * LOOK_POINT_DISTANCE;
            _hasPushedPose = false;
        }

        private void OnDisable()
        {
            // Guarded and unconditional: "-=" on a handler that was never added
            // is a safe no-op, so this is correct even if OnEnable bailed out.
            if (launcher != null) launcher.OnThrow -= HandleThrow;
            if (ball != null) ball.OnSettled -= HandleBallSettled;

            // Hand the camera back so disabling this component restores exactly
            // the behaviour the game had before the sequence existed.
            if (_throwCamera != null) _throwCamera.EndSequenceFraming();
            _beat = Beat.Off;
            _hasPushedPose = false;
        }

        private void HandleThrow(LaunchParameters p) => _throwPending = true;

        // BowlingBall raises this from FixedUpdate for BOTH a natural settle and
        // the ThrowTimeout path, so this one subscription covers a clean roll, a
        // gutter ball, and a ball that never stops wandering.
        private void HandleBallSettled() => _ballSettled = true;

        private void Update()
        {
            if (!_wired) return;

            if (game.BallSpawn != null)
            {
                _laneOriginZ = game.BallSpawn.position.z;
                _laneCentreX = game.BallSpawn.position.x;
            }

            // 1. A new roll starting always wins: BeginAim() is the one signal
            //    every path (next frame, next player, F-key reset) goes through.
            bool aiming = launcher.IsAiming;
            if (aiming && !_wasAiming) BeginRollFraming();
            _wasAiming = aiming;

            // 2. Charge started -> beat B. NOTE: BallLauncher does not reset
            //    ChargingPower on release, it stays true until the next
            //    BeginAim(), so we must use a rising EDGE here and not the raw
            //    value — otherwise every frame after a release would retrigger B.
            bool charging = launcher.ChargingPower;
            if (charging && !_wasCharging && IsPreThrowBeat(_beat))
                EnterBeat(Beat.ChargeIn, config.BZoomDuration, config.BZoomCurve, BlendMode.Cylindrical);
            _wasCharging = charging;

            // 2b. RESYNC GUARD. The player is aiming and NOT charging, yet the
            //     camera is parked in a throw-phase beat — the two have got out
            //     of step, so ease back to the aim framing.
            //
            //     The case that actually causes this: pressing the sandbox F key
            //     WHILE holding SPACE. BowlingMatchFlow.ResetCurrentFrame ->
            //     BeginRoll -> launcher.BeginAim() clears ChargingPower but leaves
            //     IsAiming already true, so NEITHER of the rising edges above
            //     fires and the camera would stay pushed in on the thrower for
            //     the whole next aim phase. Checking the STATE rather than an
            //     edge catches it however it arose.
            if (aiming && !charging && _beat != Beat.Off && !IsPreThrowBeat(_beat))
                EnterBeat(Beat.TakeStance, config.ReturnDuration, config.A2Curve, BlendMode.Linear);

            // 3. Release. Checked AFTER the charge edge so an instant tap still
            //    passes through B for a frame instead of skipping it entirely.
            if (_throwPending)
            {
                _throwPending = false;
                BeginThrowFraming();
            }

            if (_beat == Beat.Off) return;

            UpdateRollWatchdogs();
            AdvanceBeat();
            PushPose();
        }

        /// <summary>True for the beats that happen before the ball leaves the hand.</summary>
        private static bool IsPreThrowBeat(Beat beat) =>
            beat == Beat.YoureUp || beat == Beat.TakeStance || beat == Beat.TakeStanceHold;

        /// <summary>
        /// A roll is beginning (BowlingMatchFlow.BeginRoll called
        /// launcher.BeginAim()). Ease from wherever the camera is — the pin deck
        /// after an impact, a half-finished beat after an F-key reset, the Nuke's
        /// framing after a powerup throw — back to the start of the move. Never a
        /// snap cut.
        /// </summary>
        private void BeginRollFraming()
        {
            // If we were NOT driving the camera (first roll ever, or we stood
            // down for a Nuke), our cached pose is stale — read the real one.
            if (!_hasPushedPose)
            {
                _currentPosition = transform.position;
                _currentLookPoint = transform.position + transform.forward * LOOK_POINT_DISTANCE;
            }

            _rollInFlight = false;
            _ballSettled = false;
            _impactForceComplete = false;

            PlayerData current = game.Turns != null ? game.Turns.CurrentPlayer : null;
            // ReferenceEquals, not ==: we care whether it is literally the same
            // PlayerData object, not whether two players compare equal somehow.
            bool newTurn = current == null || !ReferenceEquals(current, _lastTurnPlayer);
            _lastTurnPlayer = current;

            if (newTurn)
                // New player at the line: play the full "you're up" intro. Note
                // the ease INTO A uses ReturnDuration; config.ADuration is how
                // long A then HOLDS before swinging round to the stance.
                EnterBeat(Beat.YoureUp, config.ReturnDuration, config.ACurve, BlendMode.Linear);
            else
                // Same player's second roll (or an F-key reset): skip the intro,
                // go straight back to the aim framing. Straight-line, not a
                // swing — a swing all the way back from the pin deck would be an
                // enormous arc rather than a recovery.
                EnterBeat(Beat.TakeStance, config.ReturnDuration, config.A2Curve, BlendMode.Linear);
        }

        /// <summary>The ball has just left the hand.</summary>
        private void BeginThrowFraming()
        {
            // Nuke Shot: NukeShotResolver drives the camera itself (FollowRising
            // then CutToBehindAbove), so the sequence stands down for the whole
            // roll and does not come back until the next roll begins. Those two
            // ThrowCamera calls also clear the sequence flag themselves — belt
            // and braces, because a camera fought over by two systems is the
            // worst kind of bug to read on screen.
            if (game.ActiveBallConfig != null && game.ActiveBallConfig.IsNuke)
            {
                StandDown();
                return;
            }

            _rollInFlight = true;
            _ballSettled = false;
            _impactForceComplete = false;
            _throwStartTime = Time.time;
            _lastProgressTime = Time.time;
            _maxBallZ = ball.transform.position.z;

            // SidePass, NOT Cylindrical. Beat D ends OUT OVER THE LANE, which is
            // on the opposite side of the thrower from where beat C was standing,
            // so orbiting them is a ~170 degree swing — it reads as the camera
            // being flung sideways rather than pushing down the lane. A plain
            // straight line is the right SHAPE but passes through the thrower's
            // body. SidePass is the straight push with a sideways bulge that
            // slips past them; see ThrowCameraFraming.SidePassLerp.
            EnterBeat(Beat.Release, config.DDuration, config.DCurve, BlendMode.SidePass);
        }

        private void StandDown()
        {
            _beat = Beat.Off;
            _rollInFlight = false;
            _hasPushedPose = false;
            _throwCamera.EndSequenceFraming();
        }

        /// <summary>
        /// Safety nets for every way a throw can fail to reach the pins: a gutter
        /// ball that wedges, a backward fumble, a ball that stops dead mid-lane,
        /// or BallConfig.ThrowTimeout firing. Without these the camera would sit
        /// forever waiting for an impact that is never coming.
        /// </summary>
        private void UpdateRollWatchdogs()
        {
            if (!_rollInFlight) return;

            float z = ball.transform.position.z;
            if (z > _maxBallZ + config.StallProgressEpsilon)
            {
                _maxBallZ = z;
                _lastProgressTime = Time.time;
            }

            bool stalled = Time.time - _lastProgressTime > config.StallTimeoutSeconds;
            bool hardTimeout = Time.time - _throwStartTime > config.HardTimeoutSeconds;
            if (!_ballSettled && !stalled && !hardTimeout) return;

            _rollInFlight = false;

            if (_beat == Beat.Impact)
            {
                // Already on the impact approach but the ball died just short of
                // the rack — finish the move on a short timer rather than freeze
                // halfway there. See UpdateImpactProgress.
                _impactForceComplete = true;
            }
            else if (_beat != Beat.ImpactHold)
            {
                // Never got near the pins. Ease back to the stance framing and
                // stop — flying off to film an untouched rack reads as a bug,
                // whereas staying with the disaster IS the joke.
                EnterBeat(Beat.TakeStance, config.ReturnDuration, config.A2Curve, BlendMode.Linear);
            }
            // ImpactHold: the move already finished properly. Nothing to do.
        }

        private void AdvanceBeat()
        {
            _beatElapsed += Time.deltaTime;

            switch (_beat)
            {
                case Beat.YoureUp:
                    // Ease in, then hold on the thrower's face for ADuration.
                    if (_beatElapsed >= _blendDuration + config.ADuration)
                        EnterBeat(Beat.TakeStance, config.A2Duration, config.A2Curve, BlendMode.Cylindrical);
                    break;

                case Beat.TakeStance:
                    // Holds INDEFINITELY once it arrives — the aim phase has no
                    // time limit, so neither does this beat.
                    if (_beatElapsed >= _blendDuration) _beat = Beat.TakeStanceHold;
                    break;

                case Beat.ChargeIn:
                    // Holds indefinitely once it arrives. Charge time is
                    // player-controlled and unbounded, so this beat has to absorb
                    // a 5-second hold and a 0.2-second tap equally well: it keeps
                    // creeping forward until it lands, then simply waits. (This
                    // is why there is no longer a separate beat C — one slow push
                    // that then waits does the job both beats used to share.)
                    if (_beatElapsed >= _blendDuration) _beat = Beat.ChargeInHold;
                    break;

                case Beat.Release:
                    if (TryEnterImpact()) break;
                    if (_beatElapsed >= _blendDuration)
                        EnterBeat(Beat.Travel, config.EBlendDuration, config.EBlendCurve, BlendMode.Linear);
                    break;

                case Beat.Travel:
                    // No timer, on purpose. Travel ends when the BALL gets near
                    // the pins, never after N seconds — ball speed varies from 4
                    // to 14 m/s with power, so a timer would desync on every
                    // throw that wasn't average.
                    TryEnterImpact();
                    break;

                case Beat.Impact:
                    UpdateImpactProgress();
                    break;
            }
        }

        /// <summary>
        /// Beat F's trigger: the ball is within FImpactLeadDistance of the pins.
        /// </summary>
        private bool TryEnterImpact()
        {
            if (pinDeck == null || laneConfig == null) return false;

            // Sanity gate: a throw that never really went anywhere (backward
            // fumble) must not fly to the pin deck. Not normally reachable —
            // the distance check below already implies real progress — but it
            // makes the intent explicit and survives someone retuning the lead.
            if (_maxBallZ < _laneOriginZ + config.MinProgressZ) return false;

            float ballZ = ball.transform.position.z;
            if (ballZ < pinDeck.position.z - config.FImpactLeadDistance) return false;

            _impactStartZ = ballZ;
            _impactU = 0f;
            _impactForceComplete = false;
            // Duration is unused for this beat — its progress comes from the
            // ball's position, not from a clock. See UpdateImpactProgress.
            EnterBeat(Beat.Impact, 1f, config.FCurve, BlendMode.Linear);
            return true;
        }

        /// <summary>
        /// Beat F is parameterised by HOW FAR THE BALL STILL HAS TO GO, not by
        /// time. u = 0 the instant the beat triggers, u = 1 when the ball reaches
        /// the pin deck — so the camera arrives exactly as the ball arrives, at
        /// any ball speed, with no tuning per power level.
        /// </summary>
        private void UpdateImpactProgress()
        {
            float pinZ = pinDeck != null ? pinDeck.position.z : _impactStartZ;

            if (_impactForceComplete)
            {
                // The ball died short of the rack. Wind the move out on a timer
                // so the shot still lands instead of freezing part-way.
                _impactU = Mathf.Clamp01(_impactU + Time.deltaTime / Mathf.Max(0.0001f, config.FCompleteDuration));
            }
            else if (pinZ > _impactStartZ)
            {
                // Mathf.Max keeps u monotonic: a ball that bounces back off a pin
                // must not drag the camera backwards through the move.
                _impactU = Mathf.Max(_impactU, Mathf.InverseLerp(_impactStartZ, pinZ, ball.transform.position.z));
            }
            else
            {
                // Degenerate (triggered at or past the deck) — just be there.
                _impactU = 1f;
            }

            if (_impactU >= 1f) _beat = Beat.ImpactHold;
        }

        private void EnterBeat(Beat beat, float duration, AnimationCurve curve, BlendMode mode)
        {
            _beat = beat;
            _beatElapsed = 0f;
            _blendDuration = Mathf.Max(0.0001f, duration); // never divide by zero
            _blendCurve = curve;
            _blendMode = mode;
            // Capture where the camera is RIGHT NOW as this beat's starting pose.
            // This is what makes the whole thing one continuous move: no beat
            // ever assumes where the previous beat finished.
            _fromPosition = _currentPosition;
            _fromLookPoint = _currentLookPoint;
        }

        private void PushPose()
        {
            ComputeTargetPose(_beat, out Vector3 targetPosition, out Vector3 targetLookPoint);

            float eased = BlendFactor();

            Vector3 position;
            switch (_blendMode)
            {
                case BlendMode.Cylindrical:
                    // Swing around the thrower rather than sliding through them —
                    // see ThrowCameraFraming.CylindricalLerp for why this matters.
                    // Also the right path for a straight dolly in/out (B), which is
                    // just a change of radius with the angle held constant.
                    position = ThrowCameraFraming.CylindricalLerp(_fromPosition, targetPosition,
                                                                  thrower.position, eased,
                                                                  config.MinThrowerClearance);
                    break;

                case BlendMode.SidePass:
                    // Straight push down the lane, bulged sideways to slip past
                    // the thrower. The bulge is zero at both ends, so this still
                    // starts and finishes at exactly the poses the beats asked for.
                    position = ThrowCameraFraming.SidePassLerp(_fromPosition, targetPosition,
                                                               eased, config.DPassSideOffset);
                    break;

                default:
                    position = Vector3.Lerp(_fromPosition, targetPosition, eased);
                    break;
            }

            // Blending the LOOK POINT (rather than the rotation) keeps the
            // subject anchored in frame through the move and always leaves the
            // horizon level, which is what a real camera operator gives you.
            Vector3 lookPoint = Vector3.Lerp(_fromLookPoint, targetLookPoint, eased);

            Vector3 direction = lookPoint - position;
            Quaternion rotation = direction.sqrMagnitude > 0.000001f
                ? Quaternion.LookRotation(direction, Vector3.up)
                : transform.rotation; // degenerate: keep last frame's aim

            _currentPosition = position;
            _currentLookPoint = lookPoint;
            _hasPushedPose = true;

            // The single hand-off point. ThrowCamera.LateUpdate applies this and
            // then adds screen shake on top — we never touch the transform here.
            _throwCamera.SetSequenceFraming(position, rotation);
        }

        private float BlendFactor()
        {
            switch (_beat)
            {
                // "Hold" beats have arrived: pin them at exactly their target.
                case Beat.TakeStanceHold:
                case Beat.ChargeInHold:
                case Beat.ImpactHold:
                    return 1f;

                // Beat F reads the BALL's progress, not a clock.
                case Beat.Impact:
                    return Evaluate(_blendCurve, _impactU);

                default:
                    return Evaluate(_blendCurve, Mathf.Clamp01(_beatElapsed / _blendDuration));
            }
        }

        /// <summary>Curve evaluation that survives an empty curve slot (falls back to linear).</summary>
        private static float Evaluate(AnimationCurve curve, float t) =>
            curve != null && curve.length > 0 ? curve.Evaluate(t) : t;

        /// <summary>
        /// Stops the camera from travelling past a fixed point down the lane —
        /// the Wii Sports move, where the camera hands the ball over and lets it
        /// run the last stretch to the pins alone rather than riding it in.
        ///
        /// Only the camera's POSITION is capped; the look point is left alone by
        /// the callers, so the shot keeps watching down-lane and the ball visibly
        /// pulls away into the rack. Only ever pulls the camera BACK (Mathf.Min),
        /// never pushes it forward, so with the cap at 1 this does nothing at all.
        ///
        /// Note the interaction this creates with beat F: F's distance is solved
        /// from FLanesInFrame, and when the cap is the stricter of the two it
        /// simply wins. You cannot both stop short AND frame tight on the pins
        /// without widening the field of view, which this camera never does
        /// (every "zoom" here is a dolly). The cap is the honest trade.
        /// </summary>
        private Vector3 ClampToTravelCap(Vector3 position)
        {
            if (!config.CapCameraTravel) return position;

            // Measure the cap against the real pin deck when we have one, so it
            // stays correct if the deck moves; fall back to the configured lane
            // length otherwise.
            float pinZ = pinDeck != null
                ? pinDeck.position.z
                : _laneOriginZ + (laneConfig != null ? laneConfig.Length : 0f);

            float capZ = Mathf.Lerp(_laneOriginZ, pinZ, config.TravelCapLane01);
            return new Vector3(position.x, position.y, Mathf.Min(position.z, capZ));
        }

        /// <summary>
        /// Where each beat wants the camera, and what it wants the camera to look
        /// at. Recomputed EVERY frame rather than cached at beat start, so beats
        /// anchored to something that moves (E follows the ball) work, and so
        /// retuning a knob in the Inspector while the game is running updates the
        /// shot live.
        /// </summary>
        private void ComputeTargetPose(Beat beat, out Vector3 position, out Vector3 lookPoint)
        {
            Vector3 throwerPos = thrower.position;

            switch (beat)
            {
                case Beat.YoureUp:
                    // World-axis offsets, not thrower-local: the thrower proxy
                    // does not meaningfully rotate, and world offsets are far
                    // easier for a human to reason about in the Inspector.
                    position = throwerPos + config.APositionOffset;
                    lookPoint = throwerPos + config.ALookOffset;
                    return;

                case Beat.TakeStance:
                case Beat.TakeStanceHold:
                {
                    // BUILT ON the aim view ThrowCamera already uses, plus small
                    // nudge knobs. Deliberately not a hard-coded second copy of
                    // those numbers: the aim view is what AimPreview's guide line
                    // was authored against, so drifting from it would break the
                    // Wii-Sports aim read.
                    position = _throwCamera.AimViewPosition + config.A2PositionOffset;
                    Quaternion aimRotation = Quaternion.Euler(_throwCamera.AimViewEuler + config.A2EulerOffset);
                    lookPoint = position + aimRotation * Vector3.forward * LOOK_POINT_DISTANCE;
                    return;
                }

                case Beat.ChargeIn:
                case Beat.ChargeInHold:
                    // A slow dolly FORWARD onto the thrower, not a pull-back. The
                    // radius shrinks from the aim view's (~2.9m) to BZoomRadius,
                    // and with BZoomAngleDegrees left at 180 the angle does not
                    // change at all, so this is a straight push in with zero
                    // sideways rotation. The cylindrical blend is still the right
                    // path here: it interpolates the RADIUS, which is exactly
                    // what "zoom in on the character" means.
                    position = ThrowCameraFraming.PointAround(throwerPos, config.BZoomAngleDegrees,
                                                              config.BZoomRadius, config.BZoomHeight);
                    lookPoint = throwerPos + new Vector3(0f, config.BZoomLookHeight, config.BZoomLookAheadDistance);
                    return;

                case Beat.Release:
                    position = new Vector3(_laneCentreX + config.DLateralOffset,
                                           config.DHeight,
                                           _laneOriginZ + config.DDistanceDownLane);
                    // This beat's pose is authored in world space rather than as
                    // a swing, so the thrower-clearance floor has to be applied
                    // by hand here (the cylindrical beats get it for free).
                    position = ThrowCameraFraming.EnforceRadialClearance(position, throwerPos,
                                                                         config.MinThrowerClearance);
                    lookPoint = new Vector3(_laneCentreX, config.DLookHeight,
                                            position.z + config.DLookAheadDistance);
                    return;

                case Beat.Travel:
                {
                    Vector3 ballPos = ball.transform.position;
                    // Lateral tracking: 0 keeps the camera on the lane centreline
                    // so a hook visibly drifts across frame; 1 glues the ball to
                    // the centre of frame and swings the lane around instead.
                    float x = Mathf.Lerp(_laneCentreX, ballPos.x, config.ELateralFollow01)
                              + config.ETravelOffset.x;
                    position = ClampToTravelCap(new Vector3(x,
                                                            ballPos.y + config.ETravelOffset.y,
                                                            ballPos.z + config.ETravelOffset.z));
                    // The LOOK point is deliberately NOT capped. That is the whole
                    // effect: once the camera stops, it keeps watching down-lane
                    // while the ball runs away from it into the pins.
                    lookPoint = ballPos + Vector3.forward * config.ELookAheadDistance;
                    return;
                }

                case Beat.Impact:
                case Beat.ImpactHold:
                {
                    Vector3 deckCentre = pinDeck != null
                        ? pinDeck.position
                        : new Vector3(_laneCentreX, 0f, _laneOriginZ);
                    float laneWidth = laneConfig != null ? laneConfig.Width : 1f;

                    // NOT an authored distance: solved so exactly FLanesInFrame
                    // lane-widths span the screen, whatever the player's monitor
                    // is. "Zoom" here is always a dolly — we never write
                    // Camera.fieldOfView, only read it.
                    float distance = ThrowCameraFraming.DistanceForLanesInFrame(
                        laneWidth, config.FLanesInFrame,
                        sequenceCamera.fieldOfView, sequenceCamera.aspect);

                    // The travel cap wins over the solved distance when it is the
                    // stricter of the two — see ClampToTravelCap. With the cap on,
                    // FLanesInFrame stops governing the shot and the impact is
                    // framed from wherever the camera was allowed to stop.
                    position = ClampToTravelCap(deckCentre + new Vector3(0f, config.FImpactHeight, -distance));
                    lookPoint = deckCentre + new Vector3(0f, config.FLookHeight, 0f);
                    return;
                }

                default:
                    position = _currentPosition;
                    lookPoint = _currentLookPoint;
                    return;
            }
        }
    }
}
