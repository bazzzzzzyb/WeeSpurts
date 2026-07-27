using UnityEngine;

namespace WeeSpurts.Player
{
    /// <summary>
    /// Every tunable for walking around the alley in first person. A
    /// ScriptableObject rather than constants in the controller so Tony can
    /// retune the feel in the Inspector, in Play mode, without a recompile
    /// (CLAUDE.md: "config/tunables are ScriptableObjects, not hard-coded
    /// constants").
    ///
    /// SCALE CONTEXT for the speed numbers: the venue greybox is roughly
    /// 30 m wide by 42 m deep (AlleyLayoutConfig). At 3.2 m/s a full crossing
    /// is about 13 seconds, which is brisk enough not to be tedious but still
    /// slow enough that the space reads as a place rather than a menu. Sprint
    /// exists for the "I'm bored, my turn is three players away" case.
    ///
    /// SETUP: RoamingSetupTool creates Assets/_Project/ScriptableObjects/
    /// RoamConfig.asset on first run and never overwrites your tuning after
    /// that (same create-once rule as Wobbler/Nuke in GreyboxSceneBuilder).
    /// </summary>
    [CreateAssetMenu(fileName = "RoamConfig", menuName = "WeeSpurts/Roam Config")]
    public class RoamConfig : ScriptableObject
    {
        [Header("Movement")]
        [Tooltip("Meters per second on the flat. The venue is ~30x42m, so this is tuned so crossing it isn't tedious — a real walk is ~1.4 m/s, which felt like wading. Raise for arcade snap, lower to make the alley feel bigger.")]
        public float WalkSpeed = 3.2f;

        [Tooltip("WalkSpeed is multiplied by this while Left Shift is held. 1 = no sprint at all.")]
        public float SprintMultiplier = 1.8f;

        [Tooltip("Meters per second of DOWNWARD velocity held while standing on the ground. A tiny push into the floor is what keeps CharacterController.isGrounded reliably true — with exactly zero, it flickers between grounded and airborne on every seam and step, and the walk stutters. Must be negative.")]
        public float GroundedStickVelocity = -2f;

        [Tooltip("Meters per second squared, negative = down. Deliberately stronger than real gravity (-9.81): game-feel gravity that snaps you back to the floor reads better than a floaty moon drop off a step.")]
        public float Gravity = -18f;

        [Tooltip("Meters. How high a standing jump goes (SPACE). Expressed as a HEIGHT, not a launch speed, so it stays honest when you retune Gravity — the controller solves the take-off speed from the two. 0 disables jumping. For reference the settee pits are 0.4m deep, so anything above that lets you hop straight out of one instead of using the steps.")]
        public float JumpHeight = 1f;

        [Header("Look")]
        [Tooltip("Degrees of rotation per unit of mouse movement. Mouse input is already a per-frame delta, so this is NOT multiplied by delta time — raising it makes the mouse faster, and it stays consistent whatever the frame rate.")]
        public float MouseSensitivity = 2.5f;

        [Tooltip("Degrees. How far up and down you can look before the view stops. 85 rather than 90 so you can never quite reach straight down and see the inside of your own neck.")]
        [Range(0f, 89f)] public float PitchClampDegrees = 85f;

        [Tooltip("Meters above the avatar's feet that the first-person camera sits. The character model is ~1.75m, so eyes belong a little below the top of the head.")]
        public float EyeHeight = 1.6f;

        [Header("Character controller shape")]
        [Tooltip("Meters. How fat you are for collision. THIS IS THE NUMBER Docs/OpenQuestions.md is waiting on: the venue's 1.2 / 2.5 / 4.0m corridor minimums are guesses 'until there is a character controller with a real radius'. A 0.3 radius means a 0.6m-wide body, so a 1.2m threshold is two body-widths.")]
        public float ControllerRadius = 0.3f;

        [Tooltip("Meters. Total capsule height — roughly the character's real height, so you can't walk under things you should bump into.")]
        public float ControllerHeight = 1.75f;

        [Tooltip("Meters. How tall a ledge you can walk straight up without jumping. The settee pit steps are the thing this has to clear.")]
        public float StepOffset = 0.4f;

        [Tooltip("Degrees. Slopes steeper than this stop you instead of letting you climb them.")]
        [Range(0f, 90f)] public float SlopeLimit = 48f;

        [Header("Animation")]
        [Tooltip("Seconds for the Animator's Speed parameter to catch up to your real speed (SmoothDamp). Stops the walk cycle popping on and off when you tap a key. This matters even though YOU can't see your own body in first person — OTHER players see this model walking.")]
        public float AnimatorSpeedDampTime = 0.12f;
    }
}
