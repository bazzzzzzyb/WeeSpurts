using UnityEngine;

namespace WeeSpurts.Bowling
{
    /// <summary>
    /// All ball-feel tunables in one asset so Tony can tweak comedy without
    /// touching code (CodingStandards: config lives in ScriptableObjects).
    /// GreyboxSceneBuilder creates a default instance; duplicate the asset to
    /// experiment with wildly different balls later (that's a feature idea, too).
    /// </summary>
    [CreateAssetMenu(fileName = "BallConfig", menuName = "WeeSpurts/Ball Config")]
    public class BallConfig : ScriptableObject
    {
        [Header("Body")]
        [Tooltip("Kilograms. Real bowling balls are ~7. Heavier = pins fly less.")]
        public float Mass = 6f;

        [Tooltip("Ball radius in meters. Real: 0.108. Bigger is funnier.")]
        public float Radius = 0.11f;

        [Header("Launch")]
        [Tooltip("Forward speed in m/s at minimum power.")]
        public float MinLaunchSpeed = 4f;

        [Tooltip("Forward speed in m/s at full power. Real pro throws ~9 m/s. Go higher for comedy.")]
        public float MaxLaunchSpeed = 14f;

        [Tooltip("Sideways curve force at FULL side spin (LaunchParameters.Spin.x = ±1). " +
                 "This is the PLAYER'S dialled hook only — the mistiming Hook from a bad " +
                 "release is a separate force with its own knob (HookForceMagnitude below), " +
                 "and the two are added together so they can fight each other. Raise this " +
                 "relative to HookForceMagnitude to make skill beat fumbles; lower it to let " +
                 "chaos win.")]
        public float SpinCurveForce = 6f;

        [Tooltip("How far down the lane (meters) the spin takes to fully 'roll out'. The " +
                 "top/bottom spin ramp is measured against this distance, so a shorter value " +
                 "front-loads every curve and a longer one stretches the late break out past " +
                 "the pins. ~18 is about foul line to headpin.")]
        public float SpinRampDistance = 18f;

        [Tooltip("How much vertical spin changes the TOTAL amount of hook (see SpinModel). " +
                 "0.6 means topspin curves 0.4x as much (grips and drives straight) and " +
                 "backspin 1.6x as much (skids, then breaks hard and late). Set to 0 to make " +
                 "the vertical axis purely about WHEN the curve happens, not how much.")]
        [Range(0f, 1f)] public float RollSkidHookScale = 0.6f;

        [Tooltip("Extra forward force from TOPSPIN at full up-spin ('grips and drives'). " +
                 "Backspin gets no penalty — spin must never fight the timing meter for " +
                 "control of speed. Keep this small; it is flavour, not a second power system.")]
        public float SpinDriveForce = 3f;

        [Tooltip("Height (meters) the ball starts at, measured to its center. " +
                 "~1.3 = chest height, so the ball drops onto the lane when thrown. " +
                 "Set to Radius + a hair (~0.13) to start it resting on the lane instead.")]
        public float SpawnHeight = 1.3f;

        [Header("Feel")]
        [Tooltip("0 = dead ball, 1 = superball. Around 0.3 reads as 'heavy but lively'.")]
        [Range(0f, 1f)] public float Bounciness = 0.25f;

        [Tooltip("Linear drag while rolling. Lower = slicker lane.")]
        public float RollingDrag = 0.12f;

        [Tooltip("Seconds after which a throw is force-ended even if the ball is still wandering.")]
        public float ThrowTimeout = 9f;

        [Tooltip("Ball is considered settled when slower than this (m/s) for SettleDuration.")]
        public float SettleSpeed = 0.25f;

        [Tooltip("How long the ball must stay slow to count as settled (seconds).")]
        public float SettleDuration = 0.75f;

        [Header("Feel — Timing Chaos (prototype)")]
        [Tooltip("Maps |TimingError01| (0 = perfect timing, 1 = max possible miss) to a " +
                 "0..1 chaos intensity. This is where 'worse is funnier — non-linearly' " +
                 "(GameBible §8) actually lives: shape it flat near 0 (small misses barely " +
                 "register) and steep near 1 (big misses go spectacular) so Tony can retune " +
                 "the escalation curve in the Inspector without touching code.")]
        public AnimationCurve TimingErrorCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(1f, 1f, 2f, 2f));

        [Tooltip("Max continuous sideways 'Hook' force (same units as SpinCurveForce) " +
                 "applied while the ball rolls, at full chaos intensity. Direction comes " +
                 "from the sign of TimingError01 (early vs. late release hook opposite ways). " +
                 "SEPARATE from the player's dialled spin on purpose: this is the fumble, " +
                 "that is the intent, and they are ADDED so a hard release still slices right " +
                 "even if you dialled left. Compare against SpinCurveForce to set the balance — " +
                 "at 4 vs 6, intent wins but a big whiff guts it.")]
        public float HookForceMagnitude = 4f;

        [Tooltip("Max one-time launch-angle offset (degrees) baked in at release, at full " +
                 "chaos intensity. Represents the throw itself coming out slightly wrong.")]
        public float ConeAngleJitterDegrees = 6f;

        [Tooltip("Max one-time SIDE-spin offset (same -1..1 scale as LaunchParameters.Spin.x) " +
                 "baked in at release, at full chaos intensity. Applies to the horizontal axis " +
                 "only: a fumble shoves the ball sideways, it doesn't change whether you rolled " +
                 "it or skidded it.")]
        public float ConeSpinJitterMagnitude = 0.3f;

        [Header("Feel — Wobbler (prototype)")]
        [Tooltip("Sideways force amplitude (same units as SpinCurveForce/HookForceMagnitude) for a continuous sinusoidal weave while rolling. 0 = no wobble — leave this at 0 for every ball EXCEPT Wobbler. Note: unlike Hook, this is a sinusoidal (not constant) force, so its effect on peak velocity scales as Magnitude / Frequency — lowering frequency grows the weave more than raising force alone.")]
        public float WobbleForceMagnitude = 0f;

        [Tooltip("Full side-to-side cycles per second while rolling. Higher = faster snake, lower = one long lazy weave — and a LOWER value makes the weave more visible (see WobbleForceMagnitude), not less.")]
        public float WobbleFrequencyHz = 2f;

        [Header("Feel — Nuke Shot (prototype)")]
        [Tooltip("Marks this BallConfig as the Nuke Shot powerup. Every field below is ignored unless this is true.")]
        public bool IsNuke = false;

        [Tooltip("Radius (meters) of the pin-explosion blast. Nuke only. The 10-pin " +
                 "triangle's farthest corner pin sits ~0.9m from the blast origin at " +
                 "default PinSpacing (0.3) — this radius should stay comfortably above " +
                 "that so falloff doesn't go to zero before reaching the back row.")]
        public float NukeBlastRadius = 4f;

        [Tooltip("Rigidbody.AddExplosionForce magnitude, applied via ForceMode.Impulse " +
                 "(see Pin.ApplyExplosion) — this is a direct deltaV = force/PinMass at " +
                 "the blast origin, NOT a continuous force, so don't confuse this scale " +
                 "with a ForceMode.Force number. At default PinMass 1.4, 14 gives a peak " +
                 "~10 m/s pop near the origin, falling off with distance/radius. Nuke only.")]
        public float NukeExplosionForce = 14f;

        [Tooltip("Seconds each tween (up, and separately down) takes. Nuke only.")]
        public float NukeTweenDuration = 0.5f;

        [Tooltip("Seconds the 'locking on' beacon pause holds between the up-tween and down-tween. Nuke only.")]
        public float NukeLockOnPauseDuration = 0.6f;

        [Tooltip("Green-zone power range (0..1) for a Nuke release specifically — may be tighter than the normal throw's default. Nuke only.")]
        public float NukeGreenZoneMin = 0.82f;
        public float NukeGreenZoneMax = 0.85f;
    }
}
