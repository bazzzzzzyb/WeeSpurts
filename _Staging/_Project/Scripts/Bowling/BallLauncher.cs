using System;
using UnityEngine;

namespace WeeSpurts.Bowling
{
    /// <summary>
    /// Turns keyboard input into LaunchParameters. Three-step arcade flow:
    ///
    ///   AIM    — ←/→ (or A/D) slide across the lane, hold to also angle.
    ///   POWER  — hold SPACE: power meter oscillates. Q/E while holding adds spin.
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

        [Tooltip("Full power-meter cycles per second. Faster = harder to time.")]
        [SerializeField] private float powerCycleSpeed = 0.9f;

        public bool IsAiming { get; private set; }

        // Exposed for the debug HUD.
        public float CurrentLateral { get; private set; }
        public float CurrentAngle { get; private set; }
        public float CurrentPower { get; private set; }
        public float CurrentSpin { get; private set; }
        public bool ChargingPower { get; private set; }

        private float _powerPhase;

        /// <summary>Called by the game controller when it's someone's turn to aim.</summary>
        public void BeginAim()
        {
            IsAiming = true;
            ChargingPower = false;
            CurrentLateral = 0f;
            CurrentAngle = 0f;
            CurrentPower = 0f;
            CurrentSpin = 0f;
            _powerPhase = 0f;
        }

        public void CancelAim() => IsAiming = false;

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

                if (Input.GetKeyDown(KeyCode.Space))
                {
                    ChargingPower = true;
                    _powerPhase = 0f;
                }
            }
            else
            {
                // POWER phase: meter oscillates 0→1→0; skill is releasing at the top.
                _powerPhase += Time.deltaTime * powerCycleSpeed;
                CurrentPower = Mathf.PingPong(_powerPhase * 2f, 1f);

                if (Input.GetKey(KeyCode.Q)) CurrentSpin = Mathf.Clamp(CurrentSpin - Time.deltaTime, -1f, 0f);
                if (Input.GetKey(KeyCode.E)) CurrentSpin = Mathf.Clamp(CurrentSpin + Time.deltaTime, 0f, 1f);

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
                        Seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue)
                    };
                    OnThrow?.Invoke(p);
                }
            }
        }
    }
}
