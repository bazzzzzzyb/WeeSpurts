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

        [Header("Feel")]
        [Tooltip("0 = dead pin, 1 = superball. Higher makes strikes look more chaotic.")]
        [Range(0f, 1f)] public float Bounciness = 0.2f;

        [Tooltip("0 = ice rink, 1 = velcro. Affects how pins slide/scatter off each other and the lane.")]
        [Range(0f, 1f)] public float Friction = 0.6f;
    }
}
