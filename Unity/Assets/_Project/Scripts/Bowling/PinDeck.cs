using System.Collections.Generic;
using UnityEngine;

namespace WeeSpurts.Bowling
{
    /// <summary>
    /// Owns the 10 pins: spawns them in the classic triangle, counts what a
    /// roll knocked down, clears dead wood, and resets the rack.
    ///
    /// SETUP: an empty GameObject at the head-pin position with this
    /// component, plus a disabled child named "PinTemplate" that has a
    /// Pin component. GreyboxSceneBuilder wires all of this.
    /// </summary>
    public class PinDeck : MonoBehaviour
    {
        [Tooltip("Disabled template pin that gets cloned 10 times.")]
        [SerializeField] private Pin pinTemplate;

        [SerializeField] private PinConfig pinConfig;

        private readonly List<Pin> _pins = new List<Pin>();
        // Pins that were standing before the current roll — the comparison
        // set that tells us what THIS roll knocked down.
        private readonly List<Pin> _standingBeforeRoll = new List<Pin>();

        public void Initialize(PinConfig config, Pin template)
        {
            pinConfig = config;
            pinTemplate = template;
        }

        /// <summary>Build (or rebuild) a full fresh rack of 10.</summary>
        public void ResetFullRack()
        {
            // Lazy-create the 10 pins the first time.
            if (_pins.Count == 0)
            {
                foreach (Vector3 offset in PinOffsets(pinConfig.PinSpacing))
                {
                    Pin pin = Instantiate(pinTemplate, transform);
                    pin.gameObject.SetActive(true);

                    // + PinHeight * 0.5 BECAUSE A PIN'S ORIGIN IS ITS MIDDLE, NOT
                    // ITS FOOT. The template is a Unity cylinder, whose pivot sits
                    // at the centre of the mesh, and GreyboxSceneBuilder duly
                    // offsets it by half a pin height when it builds it. This line
                    // used to overwrite that with a bare lane-level position, so
                    // every pin was spawned buried up to its waist in the lane and
                    // then shoved back out by PhysX depenetration.
                    //
                    // That was survivable with one chunky box collider — pins
                    // popped up 19cm on spawn and settled, and the only visible
                    // cost was that IsStanding's 0.6m "displaced" budget was
                    // permanently 19cm in the hole before a ball was even thrown.
                    // It stopped being survivable the moment pins gained a 2.3cm
                    // base pad: burying a thin collider eight times its own
                    // thickness makes PhysX eject it hard, so pins launched, blew
                    // past the displaced threshold, read as knocked, and got
                    // hidden by ClearDeadWood — i.e. they vanished.
                    //
                    // THIS LINE AND Pin.BuildShapedCollider ARE A LOAD-BEARING
                    // PAIR — QA flagged this coupling has no test tying it
                    // together. Removing this offset while the shaped collider
                    // (or any future thin base pad) is still in use silently
                    // reintroduces "pins vanish on rack reset." If you ever see
                    // that symptom again, check THIS line first.
                    Vector3 home = transform.position + offset
                                   + Vector3.up * (pinConfig.PinHeight * 0.5f);
                    pin.transform.position = home;
                    pin.Configure(home, pinConfig.PinMass, pinConfig.KnockedAngleDegrees,
                                  pinConfig.CenterOfMassHeight01, pinConfig.PinHeight,
                                  pinConfig.UseShapedCollider, pinConfig.BaseDiameter01);
                    _pins.Add(pin);
                }
            }

            foreach (Pin pin in _pins)
            {
                pin.gameObject.SetActive(true);
                pin.ResetToHome();
            }
        }

        /// <summary>
        /// Remove knocked pins, leave standing ones exactly where they are
        /// (second roll of a frame plays at the leftovers).
        /// </summary>
        public void ClearDeadWood()
        {
            foreach (Pin pin in _pins)
                if (pin.gameObject.activeSelf && !pin.IsStanding)
                    pin.gameObject.SetActive(false);
        }

        /// <summary>Call right before a roll so we know what was standing.</summary>
        public void MarkRollStart()
        {
            _standingBeforeRoll.Clear();
            foreach (Pin pin in _pins)
                if (pin.gameObject.activeSelf && pin.IsStanding)
                    _standingBeforeRoll.Add(pin);
        }

        /// <summary>After the ball settles: how many previously-standing pins fell?</summary>
        public int CountKnockedThisRoll()
        {
            int knocked = 0;
            foreach (Pin pin in _standingBeforeRoll)
                if (!pin.IsStanding) knocked++;
            return knocked;
        }

        /// <summary>
        /// Powerup support (Nuke Shot): radial explosion force on every currently
        /// standing pin. Real Rigidbody physics — the SAME IsStanding/
        /// CountKnockedThisRoll machinery a normal ball collision already uses
        /// resolves the outcome afterward, so there's one pin-resolution path
        /// regardless of how pins got hit. Pins ONLY — never call this on anything
        /// else, and this method itself never touches the lane/rails.
        /// </summary>
        public void ApplyExplosion(Vector3 origin, float radius, float force)
        {
            foreach (Pin pin in _pins)
                if (pin.gameObject.activeSelf)
                    pin.ApplyExplosion(origin, radius, force);
        }

        /// <summary>Classic 1-2-3-4 triangle, head pin at local origin, rows going +Z.</summary>
        public static IEnumerable<Vector3> PinOffsets(float spacing)
        {
            for (int row = 0; row < 4; row++)
            {
                float z = row * spacing * 0.866f; // rows are spacing * cos(30°) apart
                float xStart = -row * spacing * 0.5f;
                for (int i = 0; i <= row; i++)
                    yield return new Vector3(xStart + i * spacing, 0f, z);
            }
        }
    }
}
