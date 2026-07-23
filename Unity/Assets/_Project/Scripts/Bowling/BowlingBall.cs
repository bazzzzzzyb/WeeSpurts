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
        private BallConfig _config;
        private bool _inFlight;
        private float _throwStartTime;
        private float _slowSince = -1f;
        private float _spin;

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
            // Fast small object vs thin pins: continuous collision stops the
            // ball tunneling straight through a pin at high power.
            _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

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

            float speed = Mathf.Lerp(config.MinLaunchSpeed, config.MaxLaunchSpeed, Mathf.Clamp01(p.Power01));
            Quaternion aim = Quaternion.Euler(0f, p.AngleDegrees, 0f);
            Vector3 dir = aim * Vector3.forward;

            BallVelocity = dir * speed;
            // Rolling spin around the travel axis makes the curve feel physical.
            _rb.angularVelocity = new Vector3(speed / Mathf.Max(0.01f, _config.Radius), 0f, 0f);

            _spin = Mathf.Clamp(p.Spin, -1f, 1f);
            _inFlight = true;
            _throwStartTime = Time.time;
            _slowSince = -1f;
        }

        private void FixedUpdate()
        {
            if (!_inFlight) return;

            // Spin = a steady sideways force while the ball is moving forward.
            // Simple, tunable, and reproducible from LaunchParameters alone.
            if (Mathf.Abs(_spin) > 0.01f && BallVelocity.z > 0.5f)
                _rb.AddForce(Vector3.right * (_spin * _config.SpinCurveForce), ForceMode.Force);

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
