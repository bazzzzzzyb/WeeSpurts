using System;
using UnityEngine;

namespace WeeSpurts.Bowling
{
    /// <summary>
    /// Turns keyboard input into LaunchParameters. Three-step arcade flow:
    ///
    ///   AIM    — ←/→ (or A/D) slide across the lane, Shift+←/→ to angle,
    ///            and dial 2D spin: drag the on-screen spin ball with the
    ///            mouse (SpinSelectorHud), or I/J/K/L to nudge, C to centre.
    ///   POWER  — hold SPACE: power meter rises once from 0 to 1 and caps
    ///            there (no cycling). Spin is locked in at this point.
    ///   THROW  — release SPACE.
    ///
    /// This component never touches the ball directly — it only produces a
    /// LaunchParameters and raises OnThrow. That separation is what makes
    /// networking trivial later: the networked version just sends the struct.
    ///
    /// NOTE: uses the classic Input class. Project Settings > Player >
    /// Active Input Handling must be "Both" (PLAYBOOK Stage B covers this).
    /// Controller support arrives with the real UI pass (Roadmap [5]).
    /// </summary>
    public class BallLauncher : MonoBehaviour
    {
        /// <summary>The finished throw. BowlingGameController listens.</summary>
        public event Action<LaunchParameters> OnThrow;

        [Tooltip("How fast the aim slides across the lane, in half-lanes per second.")]
        [SerializeField] private float aimSpeed = 1.2f;

        [Tooltip("Max aim angle in degrees.")]
        [SerializeField] private float maxAngle = 25f;

        [Tooltip("Power gained per second while holding SPACE (fraction of the 0..1 range). Faster = harder to time.")]
        [SerializeField] private float powerRiseSpeed = 0.9f;

        [Tooltip("How fast I/J/K/L move the spin dot, in spin units per second. " +
                 "Keyboard is the fallback — mouse drag on the spin ball is the primary input.")]
        [SerializeField] private float spinNudgeSpeed = 1.2f;

        [Tooltip("Power range (0..1) that counts as a perfect release.")]
        [SerializeField] private float greenZoneMin = 0.80f;
        [SerializeField] private float greenZoneMax = 0.85f;

        [Tooltip("Release power below this counts as a total fumble — an accidental tap that sends the ball backward instead of forward (Wii Sports-style gag). Deliberately tiny.")]
        [SerializeField] private float backwardFumbleThreshold = 0.05f;

        public bool IsAiming { get; private set; }

        // Exposed for the debug HUD.
        public float CurrentLateral { get; private set; }
        public float CurrentAngle { get; private set; }
        public float CurrentPower { get; private set; }

        /// <summary>
        /// The dialled 2D spin, always inside the unit circle. X = side spin
        /// (the hook), Y = topspin/backspin. See LaunchParameters.Spin.
        /// </summary>
        public Vector2 CurrentSpin { get; private set; }

        public bool ChargingPower { get; private set; }
        public float GreenZoneMin => greenZoneMin;
        public float GreenZoneMax => greenZoneMax;

        /// <summary>
        /// True while the player is allowed to change spin: the AIM phase only.
        /// Once SPACE is held the throw is committed to whatever was dialled, so
        /// the player watches the meter instead of fiddling with two things at
        /// once. SpinSelectorHud reads this to grey the widget out.
        /// </summary>
        public bool CanEditSpin => IsAiming && !ChargingPower;

        /// <summary>
        /// Sets spin absolutely, clamped into the unit circle. This is how the
        /// mouse-drag widget (SpinSelectorHud) talks to the launcher — the UI
        /// owns the pixels, the launcher owns the value.
        /// </summary>
        public void SetSpin(Vector2 spin)
        {
            if (!CanEditSpin) return;
            CurrentSpin = SpinModel.Clamp(spin);
        }

        /// <summary>Called by the game controller when it's someone's turn to aim.</summary>
        public void BeginAim()
        {
            IsAiming = true;
            ChargingPower = false;
            CurrentLateral = 0f;
            CurrentAngle = 0f;
            CurrentPower = 0f;
            CurrentSpin = Vector2.zero;
        }

        public void CancelAim() => IsAiming = false;

        /// <summary>
        /// Overrides the green zone for the NEXT release — same
        /// ComputeTimingError computation underneath, just parameterized.
        /// Lets BowlingGameController push a tighter zone for a powerup ball
        /// (e.g. Nuke Shot) without adding a second timing signal.
        /// </summary>
        public void SetGreenZone(float min, float max)
        {
            greenZoneMin = min;
            greenZoneMax = max;
        }

        private void Update()
        {
            if (!IsAiming) return;

            float steer = Input.GetAxisRaw("Horizontal"); // arrows + A/D, no setup needed

            if (!ChargingPower)
            {
                // AIM phase: steering slides position; holding LeftShift steers angle instead.
                if (Input.GetKey(KeyCode.LeftShift))
                    CurrentAngle = Mathf.Clamp(CurrentAngle + steer * maxAngle * Time.deltaTime, -maxAngle, maxAngle);
                else
                    CurrentLateral = Mathf.Clamp(CurrentLateral + steer * aimSpeed * Time.deltaTime, -1f, 1f);

                UpdateSpinKeyboard();

                if (Input.GetKeyDown(KeyCode.Space))
                {
                    ChargingPower = true;
                }
            }
            else
            {
                // POWER phase: meter rises once from 0 to 1 and caps there (no
                // cycling) — holding longer never falls back down, it just waits.
                CurrentPower = Mathf.Clamp01(CurrentPower + Time.deltaTime * powerRiseSpeed);

                if (Input.GetKeyUp(KeyCode.Space))
                {
                    IsAiming = false;

                    var p = new LaunchParameters
                    {
                        LateralPosition01 = CurrentLateral,
                        AngleDegrees = CurrentAngle,
                        Power01 = CurrentPower,
                        Spin = CurrentSpin,
                        // Seed picked at throw time; later this exact value is
                        // what every networked client uses to replay chaos.
                        Seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue),
                        TimingError01 = ComputeTimingError(CurrentPower),
                        // Hand-placed gag archetype at the extreme low end of the
                        // meter, deliberately NOT derived from TimingError01's
                        // smooth curve — see LaunchParameters.IsBackwardFumble.
                        IsBackwardFumble = CurrentPower < backwardFumbleThreshold
                    };
                    OnThrow?.Invoke(p);
                }
            }
        }

        /// <summary>
        /// Keyboard fallback for the spin selector, so the game is playable with
        /// no mouse (and maps cleanly onto a stick when controller support lands
        /// with the real UI pass).
        ///
        /// I/J/K/L rather than the arrows or WASD: those are already the aim
        /// slide, and Shift+arrows is the angle. Spin is a different axis from
        /// aim, so it gets different keys rather than a third modifier stacked
        /// onto the same ones (Tony's call).
        /// </summary>
        private void UpdateSpinKeyboard()
        {
            if (Input.GetKeyDown(KeyCode.C))
            {
                CurrentSpin = Vector2.zero;
                return;
            }

            var nudge = new Vector2(
                (Input.GetKey(KeyCode.L) ? 1f : 0f) - (Input.GetKey(KeyCode.J) ? 1f : 0f),
                (Input.GetKey(KeyCode.I) ? 1f : 0f) - (Input.GetKey(KeyCode.K) ? 1f : 0f));

            if (nudge == Vector2.zero) return;

            // Clamped to the CIRCLE, not per-axis, so holding I and L together
            // can't reach a stronger diagonal than the mouse widget allows.
            CurrentSpin = SpinModel.Clamp(CurrentSpin + nudge * (spinNudgeSpeed * Time.deltaTime));
        }

        /// <summary>
        /// Distance from the green zone's nearest edge, signed by the existing
        /// convention (+1 early/undershoot .. 0 perfect .. -1 late/overshoot).
        /// Inside the zone it's always exactly 0 (a flat "perfect" plateau,
        /// not a single peak instant like the old ping-pong meter).
        /// </summary>
        private float ComputeTimingError(float power)
        {
            if (power < greenZoneMin)
                // Undershoot ("early" — released before reaching the zone): 0 right
                // at the zone's near edge, ramping up to +1 at 0% power.
                return Mathf.InverseLerp(greenZoneMin, 0f, power);
            if (power > greenZoneMax)
                // Overshoot ("late" — held past the zone): 0 right at the zone's
                // far edge, ramping down to -1 at 100% power.
                return -Mathf.InverseLerp(greenZoneMax, 1f, power);
            return 0f; // inside the green zone: perfect release
        }
    }
}
