using UnityEngine;

namespace WeeSpurts.Bowling
{
    /// <summary>
    /// One pin. "Knocked down" = tilted past a threshold angle OR thrown off
    /// its spot. Simple and robust — no collision bookkeeping needed.
    ///
    /// SETUP: created automatically by PinDeck from a template. Needs a
    /// Rigidbody + collider (the builder uses a cylinder body).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Pin : MonoBehaviour
    {
        private Vector3 _homePosition;
        private Quaternion _homeRotation;
        private float _knockedAngle = 40f;
        private Rigidbody _rb;

        public bool IsStanding
        {
            get
            {
                // Angle between the pin's up axis and world up. Upright = 0.
                float tilt = Vector3.Angle(transform.up, Vector3.up);
                // Also count "launched into orbit" as down even if upright mid-air.
                float displaced = Vector3.Distance(transform.position, _homePosition);
                return tilt < _knockedAngle && displaced < 0.6f;
            }
        }

        private void Awake() => _rb = GetComponent<Rigidbody>();

        public void Configure(Vector3 homePosition, float pinMass, float knockedAngleDegrees)
        {
            _homePosition = homePosition;
            _homeRotation = Quaternion.identity;
            _knockedAngle = knockedAngleDegrees;
            _rb.mass = pinMass;
        }

        /// <summary>Stand the pin back up on its spot, motionless.</summary>
        public void ResetToHome()
        {
            _rb.isKinematic = true;
            transform.SetPositionAndRotation(_homePosition, _homeRotation);
            _rb.isKinematic = false;
        }

        /// <summary>Freeze physics (used while clearing dead wood between rolls).</summary>
        public void SetFrozen(bool frozen) => _rb.isKinematic = frozen;

        /// <summary>
        /// Powerup support (Nuke Shot): radial physics blast on this pin only.
        /// ForceMode.Impulse is deliberate, not the AddExplosionForce default
        /// (ForceMode.Force): Force is scaled for a force applied every
        /// FixedUpdate tick, but this is a single one-shot call — with Force
        /// mode a one-shot call only nets a velocity change of
        /// (force/mass)*fixedDeltaTime, ~50x smaller than intended. Impulse
        /// applies deltaV = force/mass directly, no timestep scaling, which is
        /// the correct semantics for an instantaneous "hit this thing right
        /// now" explosion. upwardsModifier gives every pin a bit of pop
        /// (rather than a flat outward slide) and, as a side benefit, gives
        /// the head pin — which sits exactly at the explosion origin, a
        /// degenerate zero-distance case — a well-defined upward push instead
        /// of an undefined/zero direction vector.
        /// </summary>
        public void ApplyExplosion(Vector3 origin, float radius, float force)
        {
            _rb.AddExplosionForce(force, origin, radius, upwardsModifier: 0.3f, mode: ForceMode.Impulse);
        }
    }
}
