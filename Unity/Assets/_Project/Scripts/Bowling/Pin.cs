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

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();

            // REQUIRED FOR THE BALL TO HIT THIS PIN AT ALL. BowlingBall sets
            // itself to ContinuousDynamic, and Unity only does swept collision
            // between two rigidbodies when the other one is also Continuous or
            // ContinuousDynamic — a Discrete pin (the default) is tested by
            // overlap at the end of each step, which a ball travelling 0.28m per
            // step can skip straight over. See the long note in BowlingBall.Awake.
            // Also covers pin-vs-pin at speed, which matters after a Nuke.
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        public void Configure(Vector3 homePosition, float pinMass, float knockedAngleDegrees,
                              float centerOfMassHeight01, float pinHeight,
                              bool useShapedCollider, float baseDiameter01)
        {
            _homePosition = homePosition;
            _homeRotation = Quaternion.identity;
            _knockedAngle = knockedAngleDegrees;
            _rb.mass = pinMass;

            if (useShapedCollider) BuildShapedCollider(baseDiameter01);

            // BOTTOM-HEAVY, like a real pin. This is the single biggest reason
            // pins feel like pins rather than like skittles: a real ten-pin is
            // widest and heaviest low down, so it WOBBLES when clipped, sometimes
            // rights itself, and topples about its base rather than pivoting
            // around its middle.
            //
            // Unity puts the centre of mass at the collider's centre by default,
            // which for our box collider is half way up. That makes a pin behave
            // like a uniform rod: it tips over cleanly and predictably, with none
            // of the hesitation that makes a wobbling pin worth shouting at.
            //
            // WHY InverseTransformVector AND NOT JUST THE METRES: Rigidbody
            // .centerOfMass is in LOCAL space, and the pin's transform is scaled
            // to (0.12, 0.19, 0.12) — the cylinder primitive is 2 units tall, so
            // its Y scale is height/2. Assigning a value in metres therefore gets
            // multiplied by 0.19 on the way into world space, so a 4.6cm offset
            // silently became 0.9cm on a 38cm pin. That is why moving this knob
            // seemed to do nothing in either direction. InverseTransformVector
            // converts a world-space displacement into the local units the
            // property actually wants, so the config value means what it says at
            // any scale.
            float worldOffset = (centerOfMassHeight01 - 0.5f) * pinHeight;
            _rb.centerOfMass = transform.InverseTransformVector(Vector3.up * worldOffset);
        }

        /// <summary>
        /// Replaces the pin's single full-size box with a real pin's shape: a
        /// round CAPSULE body sitting on a small flat BASE PAD. Two colliders,
        /// one Rigidbody — an ordinary Unity compound collider.
        ///
        /// WHY THIS IS THE FIX for "the ball runs up the pins":
        ///
        /// The old box was the pin's full width all the way down to the lane,
        /// with square 90-degree corners. A 0.22m ball whose centre rides at
        /// 0.11m meets that as a flat vertical wall, so instead of glancing off
        /// it ploughs forward into the space the pin is vacating and rides up
        /// the corner. Worse, a FALLEN box is a 0.12m-tall ramp lying on the
        /// lane, and a ball of that size rolling into an obstacle that tall
        /// climbs it — which is exactly the "rolls over them and gets stuck"
        /// case. A box also cannot roll: it tips onto a face and stops dead,
        /// which is the "slamming into the ground" half.
        ///
        /// A capsule fixes all three at once. The ball meets a curved surface,
        /// so contact is off-centre and the pin is flung sideways rather than
        /// squashed forward. A fallen capsule ROLLS out of the way instead of
        /// being a wedge. And pins that clip each other skitter rather than
        /// slapping flat.
        ///
        /// WHY THE BASE PAD IS STILL A BOX: a capsule has hemispherical ends, so
        /// a pin standing on one touches the lane at a single point — an unstable
        /// equilibrium that topples on its own no matter how bottom-heavy it is.
        /// (That is the real reason the original code deleted the capsule and
        /// used a box; the box was solving a genuine problem, just at the cost of
        /// every collision.) A small flat pad underneath gives a proper contact
        /// patch to stand on, and its width sets how far the pin can lean before
        /// it goes — narrow base, tips readily, exactly like the real thing.
        ///
        /// EVERYTHING HERE IS IN LOCAL COLLIDER UNITS, deliberately. The pin mesh
        /// is Unity's cylinder primitive, which is 1 wide and 2 tall in local
        /// space whatever the transform scale is (the builder uses a non-uniform
        /// 0.12 / 0.19 / 0.12). Authoring in local units means these proportions
        /// hold at any pin size, and none of them need to know PinHeight.
        /// </summary>
        private void BuildShapedCollider(float baseDiameter01)
        {
            // Local Y runs -1 (the pin's foot, resting on the lane) to +1 (its
            // crown), because the cylinder primitive is 2 units tall.
            const float FootY = -1f;
            const float BasePadHeight = 0.12f;   // local; a shallow disc, not a plinth
            const float BodyRadius = 0.46f;      // local; just inside the mesh's 0.5 silhouette
            const float BodyLength = 1.74f;      // local; total capsule length including both caps
            const float BodyCentreY = 0.05f;     // local; nudged up so the pad is the lowest point

            // Reuse the box that GameObject.CreatePrimitive/the builder already
            // put here rather than destroying and re-adding — a collider swapped
            // out from under a live Rigidbody is a good way to lose contacts mid
            // roll. Re-purposed as the base pad.
            var pad = GetComponent<BoxCollider>();
            if (pad == null) pad = gameObject.AddComponent<BoxCollider>();

            pad.size = new Vector3(baseDiameter01, BasePadHeight, baseDiameter01);
            // Sit the pad's underside exactly on the pin's foot, so the pin still
            // stands at the same height it always did and nothing needs re-spotting.
            pad.center = new Vector3(0f, FootY + BasePadHeight * 0.5f, 0f);

            var body = GetComponent<CapsuleCollider>();
            if (body == null) body = gameObject.AddComponent<CapsuleCollider>();

            body.direction = 1; // 1 = Y. The pin is upright; 0 would lay it on its side.
            body.radius = BodyRadius;
            body.height = BodyLength;
            body.center = new Vector3(0f, BodyCentreY, 0f);

            // Both colliders must share the pin's bounce/friction material, or
            // the ball would get one answer off the body and a different one off
            // the base. GreyboxSceneBuilder stamped PinBounce onto the box; carry
            // it across rather than letting the capsule default to no material.
            body.sharedMaterial = pad.sharedMaterial;
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
