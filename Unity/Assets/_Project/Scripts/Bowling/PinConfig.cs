using UnityEngine;

namespace WeeSpurts.Bowling
{
    /// <summary>
    /// All pin-feel tunables in one asset, same pattern as BallConfig — Tony
    /// can tweak how pins fly without touching code (CodingStandards: config
    /// lives in ScriptableObjects). GreyboxSceneBuilder creates a default
    /// instance and builds a matching PinBounce physics material from it.
    /// </summary>
    [CreateAssetMenu(fileName = "PinConfig", menuName = "WeeSpurts/Pin Config")]
    public class PinConfig : ScriptableObject
    {
        [Header("Shape")]
        [Tooltip("Distance between neighboring pins in a row. Real ~0.3.")]
        public float PinSpacing = 0.3f;

        [Tooltip("Pin height in meters. Real 0.38.")]
        public float PinHeight = 0.38f;

        [Header("Body")]
        [Tooltip("Kilograms. Real ~1.6. Lighter pins fly further = funnier.")]
        public float PinMass = 1.4f;

        [Tooltip("A pin tilted past this many degrees counts as knocked down.")]
        public float KnockedAngleDegrees = 40f;

        [Tooltip("Where the pin's weight sits, as a fraction of its height: 0 = all in the base, " +
                 "0.5 = uniform (Unity's default, and what the game did before this existed), " +
                 "1 = all in the neck. Real ten-pins are BOTTOM-HEAVY, which is why they wobble, " +
                 "occasionally right themselves, and topple about their base instead of pivoting " +
                 "around their middle — this is the single biggest knob for whether pins feel like " +
                 "pins. LOWER = harder to knock down (more of a shove needed to get past the " +
                 "balance point) and more wobble, so expect scores to drop a little as you lower " +
                 "it. Set to 0.5 to get the old behaviour back exactly.")]
        [Range(0f, 1f)] public float CenterOfMassHeight01 = 0.38f;

        [Header("Shape — collider")]
        [Tooltip("ON: the pin's physical body is a round CAPSULE on a small flat BASE PAD, like a real " +
                 "pin. OFF: one plain box the full size of the mesh (what the game did before). " +
                 "The box is why a ball 'runs up' pins instead of scattering them — it presents a flat " +
                 "wall at full width right down to the lane, and once fallen it is a 12cm ramp the ball " +
                 "climbs. A capsule glances the ball off and ROLLS when it lands. Turn off only to " +
                 "compare.")]
        public bool UseShapedCollider = true;

        [Tooltip("Width of the flat base pad the pin stands on, as a fraction of the pin's full width. " +
                 "Real pins have a narrow base — that is what makes them tip readily instead of standing " +
                 "like a brick. SMALLER = tips more easily (more scatter, more accidental falls); LARGER " +
                 "= more stable. Below about 0.3 pins start toppling on their own. Only used when " +
                 "UseShapedCollider is on.")]
        [Range(0.2f, 1f)] public float BaseDiameter01 = 0.45f;

        [Header("Feel")]
        [Tooltip("0 = dead pin, 1 = superball. Higher makes strikes look more chaotic.")]
        [Range(0f, 1f)] public float Bounciness = 0.2f;

        [Tooltip("0 = ice rink, 1 = velcro. Affects how pins slide/scatter off each other and the lane.")]
        [Range(0f, 1f)] public float Friction = 0.6f;
    }
}
