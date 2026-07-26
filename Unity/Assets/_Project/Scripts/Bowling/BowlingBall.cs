using System;
using UnityEngine;

namespace WeeSpurts.Bowling
{
    /// <summary>
    /// The physical ball. Given LaunchParameters + a BallConfig, it launches
    /// itself deterministically-ish and reports when it has settled.
    ///
    /// Networking note (important): PhysX is NOT bit-identical across
    /// machines, so remote clients replaying these parameters may drift a few
    /// centimeters. That's fine — Docs/Networking.md says the host's confirmed
    /// pin count is the authority; the replay is just for show.
    ///
    /// SETUP: a Sphere with Rigidbody + this component. GreyboxSceneBuilder
    /// does it for you.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class BowlingBall : MonoBehaviour
    {
        /// <summary>Fired once per throw when the ball stops (or times out).</summary>
        public event Action OnSettled;

        private Rigidbody _rb;
        private Collider _collider;
        private Renderer _renderer;
        private BallConfig _config;
        private bool _inFlight;
        private float _throwStartTime;
        private float _slowSince = -1f;
        private Vector2 _spin;      // player-dialled 2D spin (see SpinModel)
        private float _launchZ;     // down-lane origin for the spin ramp, captured at release
        private Vector3 _launchDir; // stored so topspin's forward drive can't feed back on itself
        private float _hookForce; // precomputed sign+magnitude Hook force, set once in Launch()
        private float _wobblePhase; // radians, seeded per-throw so the weave differs per Seed
        private float _wobbleElapsed; // seconds, accumulated from Time.fixedDeltaTime (NOT Time.time — see FixedUpdate)

        // --- Unity version compatibility -------------------------------
        // Unity 6 renamed Rigidbody.velocity → linearVelocity and
        // drag → linearDamping. These helpers keep us compiling on either.
        private Vector3 BallVelocity
        {
#if UNITY_6000_0_OR_NEWER
            get => _rb.linearVelocity;
            set => _rb.linearVelocity = value;
#else
            get => _rb.velocity;
            set => _rb.velocity = value;
#endif
        }

        private float BallDrag
        {
#if UNITY_6000_0_OR_NEWER
            set => _rb.linearDamping = value;
#else
            set => _rb.drag = value;
#endif
        }
        // ---------------------------------------------------------------

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _collider = GetComponent<Collider>();
            _renderer = GetComponent<Renderer>();
            // Fast small object vs thin pins: continuous collision stops the
            // ball tunneling straight through a pin at high power.
            _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        /// <summary>
        /// Powerup support (Nuke Shot): hides/shows the real ball while a canned
        /// tween (a separate nuke sphere) takes over visually. Cosmetic only —
        /// physics/collider are untouched.
        /// </summary>
        public void SetVisible(bool visible) => _renderer.enabled = visible;

        /// <summary>Place the ball at the start position, frozen, ready to throw.</summary>
        public void ResetForThrow(Vector3 position)
        {
            _inFlight = false;
            _rb.isKinematic = true; // frozen while aiming
            transform.position = position;
            transform.rotation = Quaternion.identity;
        }

        public void Launch(LaunchParameters p, BallConfig config)
        {
            _config = config;
            _rb.isKinematic = false;
            _rb.mass = config.Mass;
            BallDrag = config.RollingDrag;
            // ".material" (not "sharedMaterial") instantiates a per-ball copy the
            // first time it's touched, so this can't leak bounciness changes back
            // into the shared BallBounce.asset — each config swap just restamps
            // that instance. Without this, switching BallConfig (e.g. the sandbox
            // switcher's BouncyBall slot) changed mass/speed but not bounce, since
            // GreyboxSceneBuilder only bakes bounciness onto the collider once,
            // at scene-build time, from whatever config was default then.
            _collider.material.bounciness = config.Bounciness;

            if (p.IsBackwardFumble)
            {
                // Wii Sports-style gag: barely tapped the meter, so instead of the
                // Hook/Cone forward-mistiming curve, the ball just flies backward.
                // Hand-placed, not derived from the smooth chaos curve — skip that
                // machinery entirely for this branch.
                Vector3 backDir = Quaternion.Euler(0f, p.AngleDegrees, 0f) * Vector3.back;
                BallVelocity = backDir * config.MinLaunchSpeed;
                _rb.angularVelocity = new Vector3(-config.MinLaunchSpeed / Mathf.Max(0.01f, config.Radius), 0f, 0f);
                _spin = Vector2.zero;
                _launchZ = transform.position.z;
                _launchDir = backDir;
                _hookForce = 0f;
                // State hygiene: this early-return branch skips the non-fumble path
                // below that normally resets these. Currently harmless (FixedUpdate's
                // wobble force is gated on BallVelocity.z > 0.5f, and a fumble's
                // velocity.z is negative, so stale values are never read) — but reset
                // anyway so this stays correct if that guard ever changes.
                _wobblePhase = 0f;
                _wobbleElapsed = 0f;
                _inFlight = true;
                _throwStartTime = Time.time;
                _slowSince = -1f;
                return;
            }

            // Timing chaos (GameBible §8): bad release timing = a human fumble, not
            // a game malfunction. TimingErrorCurve turns |TimingError01| (0 = perfect,
            // 1 = max miss) into a 0..1 intensity that's deliberately non-linear —
            // mild near 0, spectacular near 1 — so small misses barely register.
            float intensity = config.TimingErrorCurve.Evaluate(Mathf.Abs(p.TimingError01));

            // Cone: a single seeded jitter baked in at release ("the throw came out
            // wrong"), not continuous. System.Random (NOT UnityEngine.Random) seeded
            // from LaunchParameters.Seed keeps this reproducible across clients.
            System.Random coneRng = new System.Random(p.Seed);
            float angleJitter = ((float)coneRng.NextDouble() * 2f - 1f) * config.ConeAngleJitterDegrees * intensity;
            // Horizontal axis only — a fumble shoves the ball sideways, it doesn't
            // change whether the player rolled it or skidded it. Kept to ONE draw,
            // in the same position in the RNG stream as before the 2D spin change,
            // so an existing Seed still produces the identical wobble phase below.
            float spinJitter = ((float)coneRng.NextDouble() * 2f - 1f) * config.ConeSpinJitterMagnitude * intensity;

            // Wobbler (ball personality, not timing chaos): a third draw from the
            // same seeded RNG gives each throw its own weave phase so identical
            // Seed values always weave identically, but different Seeds don't
            // look copy-pasted. Harmless no-op for balls with WobbleForceMagnitude 0.
            _wobblePhase = (float)(coneRng.NextDouble() * Mathf.PI * 2f);
            _wobbleElapsed = 0f;

            // Hook: a continuous sideways force for the whole flight. Sign follows
            // early-vs-late release (TimingError01's sign); magnitude scales with the
            // same non-linear intensity. At perfect timing intensity is 0, so this is
            // 0 regardless of Mathf.Sign(0f) returning +1 rather than 0.
            // Negated: Docs/OpenQuestions.md + BowlingFeelIdeas.md spec hard (over-
            // powered/late release) -> hooks RIGHT, soft (under-powered/early) ->
            // hooks LEFT. TimingError01 is negative on a hard/late release (see
            // BallLauncher.ComputeTimingError's overshoot branch), so a bare
            // Mathf.Sign(p.TimingError01) hooked LEFT on hard/RIGHT on soft — the
            // opposite of spec. This fixes a previously-inverted direction, not a
            // new design change.
            _hookForce = -Mathf.Sign(p.TimingError01) * intensity * config.HookForceMagnitude;

            float speed = Mathf.Lerp(config.MinLaunchSpeed, config.MaxLaunchSpeed, Mathf.Clamp01(p.Power01));
            Quaternion aim = Quaternion.Euler(0f, p.AngleDegrees + angleJitter, 0f);
            Vector3 dir = aim * Vector3.forward;

            BallVelocity = dir * speed;
            // Rolling spin around the travel axis makes the curve feel physical.
            _rb.angularVelocity = new Vector3(speed / Mathf.Max(0.01f, _config.Radius), 0f, 0f);

            // The spin ramp is measured from where this throw actually started,
            // not from the world origin, so a throw released from the far left of
            // the lane ramps identically to one from the middle.
            _launchZ = transform.position.z;
            _launchDir = dir;

            // Player spin + the release fumble, clamped back into the unit circle
            // so a jittered diagonal can't exceed full spin.
            _spin = SpinModel.Clamp(p.Spin + new Vector2(spinJitter, 0f));
            _inFlight = true;
            _throwStartTime = Time.time;
            _slowSince = -1f;
        }

        private void FixedUpdate()
        {
            if (!_inFlight) return;

            // How far into the spin ramp we are: 0 at release, 1 once the ball has
            // rolled out. This is what makes topspin bite EARLY and backspin break
            // LATE — the same dialled spin produces a different force depending on
            // where the ball is down the lane. See SpinModel.RampShape.
            float rampProgress = SpinModel.Progress01(transform.position.z - _launchZ,
                                                      _config.SpinRampDistance);

            // Player spin: a sideways force shaped by the ramp above.
            if (Mathf.Abs(_spin.x) > 0.01f && BallVelocity.z > 0.5f)
            {
                float playerSpinForce = SpinModel.LateralForce(
                    _spin, rampProgress, _config.SpinCurveForce, _config.RollSkidHookScale);
                _rb.AddForce(Vector3.right * playerSpinForce, ForceMode.Force);
            }

            // Topspin drives forward ("grips and rolls out"). Along the STORED
            // launch direction, not current velocity, so it pushes the ball along
            // its original line instead of amplifying whatever the hook has already
            // done to it — that feedback loop would make topspin curve MORE, which
            // is the opposite of the point.
            if (_spin.y > 0.01f && BallVelocity.z > 0.5f)
                _rb.AddForce(_launchDir * SpinModel.DriveForce(_spin, _config.SpinDriveForce),
                             ForceMode.Force);

            // Hook (GameBible §8 — the mistiming fumble): a SEPARATE sideways force,
            // driven purely by release timing, set once in Launch() and constant for
            // the whole roll.
            //
            // THIS IS ADDITIVE WITH PLAYER SPIN, AND THAT IS THE DESIGN. The two
            // compete: dial full left spin, then release hard, and the fumble still
            // drags the ball right — your intent survives but gets mauled. Neither
            // force scales the other, and each has its own config knob
            // (SpinCurveForce vs HookForceMagnitude) precisely so one can't quietly
            // drown out the other when the other is tuned.
            //
            // Deliberately NOT ramped like player spin: the fumble reads as a
            // constant wrongness from the instant of release, which keeps it
            // visually distinguishable from a dialled late break.
            if (_hookForce != 0f && BallVelocity.z > 0.5f)
                _rb.AddForce(Vector3.right * _hookForce, ForceMode.Force);

            // Wobbler: continuous sinusoidal weave for the WHOLE throw, unlike Hook
            // (which is 0 at perfect timing). Elapsed time accumulates every tick
            // in flight, unconditionally, from Time.fixedDeltaTime — not Time.time —
            // so the phase tracks fixed-timestep ticks exactly, which is the more
            // rigorously deterministic pattern for a future networked replay.
            _wobbleElapsed += Time.fixedDeltaTime;
            if (_config.WobbleForceMagnitude != 0f && BallVelocity.z > 0.5f)
            {
                float wobble = Mathf.Sin(_wobbleElapsed * _config.WobbleFrequencyHz * Mathf.PI * 2f + _wobblePhase) * _config.WobbleForceMagnitude;
                _rb.AddForce(Vector3.right * wobble, ForceMode.Force);
            }

            // Settled = slow for long enough, or timed out entirely.
            bool timedOut = Time.time - _throwStartTime > _config.ThrowTimeout;
            bool slow = BallVelocity.magnitude < _config.SettleSpeed;

            if (slow)
            {
                if (_slowSince < 0f) _slowSince = Time.time;
            }
            else _slowSince = -1f;

            bool settled = _slowSince > 0f && Time.time - _slowSince > _config.SettleDuration;

            if (settled || timedOut)
            {
                _inFlight = false;
                _rb.isKinematic = true; // stop it twitching while pins are counted
                OnSettled?.Invoke();
            }
        }
    }
}
