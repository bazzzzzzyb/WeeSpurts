using UnityEngine;
using WeeSpurts.Bowling;

namespace WeeSpurts.Player
{
    /// <summary>
    /// Walk-around-the-alley first-person movement, driven by a
    /// CharacterController. Every number comes from <see cref="RoamConfig"/>.
    ///
    /// WHO TURNS THIS ON AND OFF: only <see cref="PlayerAvatar.ApplyMode"/>.
    /// Nothing else in the codebase may set `enabled` on this component. That
    /// rule is what makes "exactly one system owns your input at a time"
    /// structural instead of something we have to remember.
    ///
    /// YAW ON THE BODY, PITCH ON THE CAMERA. Left/right rotation turns this
    /// GameObject (so WASD is always relative to where you're facing, and so a
    /// future networked position+rotation sync carries your facing for free).
    /// Up/down rotation only tilts the camera child. Pitching the body would
    /// tip the character model over and, worse, tilt the direction WASD walks —
    /// look at the floor and you'd walk into it.
    ///
    /// NOT NETWORKED, deliberately. Docs/Networking.md syncs launch parameters,
    /// not per-frame physics; roaming movement is the same shape of problem and
    /// will get its own decision later. This class reads local input and moves a
    /// local transform, nothing more.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        // [SerializeField] on every reference, for the reason AimPreview taught
        // this project the hard way: these are wired by an EDITOR tool, not at
        // Play-mode startup, so a field Unity doesn't serialize comes back null
        // after a scene reload and the component silently does nothing.
        [Tooltip("All the tunables. Without it this component does nothing rather than guessing at numbers.")]
        [SerializeField] private RoamConfig config;

        [Tooltip("The first-person camera's transform. Only its PITCH is written here.")]
        [SerializeField] private Transform cameraPivot;

        [Tooltip("Optional. The character model's reaction actor — we push walk speed into its Animator so the walk cycle plays. Empty just means no animation.")]
        [SerializeField] private CharacterThrowReactionActor reactionActor;

        private CharacterController _controller;

        // Look pitch in degrees, negative = looking up (Unity's X rotation is
        // inverted relative to "up is positive"). Kept as our own number rather
        // than read back off the transform each frame, because localEulerAngles
        // reports 0..360 — a -10 degree pitch reads back as 350, and clamping
        // that against +/-85 snaps your view to the ceiling.
        private float _pitch;

        // Accumulated downward speed. Gravity is integrated by hand because a
        // CharacterController is not a Rigidbody — nothing applies gravity to it.
        private float _verticalVelocity;

        // Damped 0..1 walk speed pushed into the Animator, plus SmoothDamp's
        // running velocity (it owns that value between calls).
        private float _animatorSpeed;
        private float _animatorSpeedVelocity;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        private void OnEnable()
        {
            // Entering roaming mode is a fresh start: a vertical velocity or a
            // damping velocity carried over from before a bowling turn is a
            // stimulus several seconds stale, and would fling you on the first
            // frame back. (Same lesson ThrowerAimSlide learned about
            // _slideVelocity.)
            _verticalVelocity = 0f;
            _animatorSpeedVelocity = 0f;

            // Level the view. We could try to preserve the old pitch, but the
            // avatar may have been rotated by MoveToThrowingStance in between,
            // and "you come back looking straight ahead" is the predictable
            // behaviour.
            _pitch = 0f;
            if (cameraPivot != null) cameraPivot.localRotation = Quaternion.identity;
        }

        private void OnDisable()
        {
            // Hand the model over to the bowling systems standing still, not
            // frozen mid-stride with Speed stuck at 1.
            _animatorSpeed = 0f;
            _animatorSpeedVelocity = 0f;
            // "!= null" rather than "?." on purpose: Unity overloads == for
            // Objects so a DESTROYED component compares equal to null, which the
            // C# null-conditional operator would sail straight past.
            if (reactionActor != null) reactionActor.SetSpeed(0f);
        }

        private void Update()
        {
            if (config == null || _controller == null) return;

            Look();
            Move();
            DriveWalkAnimation();
        }

        private void Look()
        {
            // "Mouse X"/"Mouse Y" are default axes in Unity's Input Manager, and
            // they are already per-frame DELTAS — multiplying them by
            // Time.deltaTime is the classic beginner bug that makes mouse look
            // speed change with frame rate. Don't.
            float yawInput = Input.GetAxisRaw("Mouse X") * config.MouseSensitivity;
            float pitchInput = Input.GetAxisRaw("Mouse Y") * config.MouseSensitivity;

            // Yaw: the whole body turns.
            transform.Rotate(0f, yawInput, 0f, Space.Self);

            // Pitch: camera only, clamped so you can't backflip your own head.
            _pitch = Mathf.Clamp(_pitch - pitchInput, -config.PitchClampDegrees, config.PitchClampDegrees);
            if (cameraPivot != null) cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private void Move()
        {
            // GetAxisRaw, not GetAxis: raw is unsmoothed, so movement starts and
            // stops exactly when you press and release. Unity's built-in
            // smoothing on GetAxis reads as ice-skating.
            Vector3 input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
            // Clamp rather than Normalize: normalising a zero vector is fine but
            // normalising ANY input would make a half-pressed stick full speed.
            // Clamping only trims the diagonal, which is the actual bug (holding
            // W+D would otherwise be 1.41x faster than holding W).
            input = Vector3.ClampMagnitude(input, 1f);

            // TransformDirection turns "forward and right relative to me" into
            // world space, which is what makes WASD follow where you're looking.
            Vector3 worldDirection = transform.TransformDirection(input);

            float speed = config.WalkSpeed;
            if (Input.GetKey(KeyCode.LeftShift)) speed *= config.SprintMultiplier;

            if (_controller.isGrounded && _verticalVelocity < 0f)
            {
                // Standing on something: hold a small downward velocity instead
                // of zero. See RoamConfig.GroundedStickVelocity — this is what
                // keeps isGrounded from flickering on every floor seam.
                _verticalVelocity = config.GroundedStickVelocity;
            }
            else
            {
                // In the air (or being pushed up): integrate gravity by hand.
                _verticalVelocity += config.Gravity * Time.deltaTime;
            }

            // Jump. Checked AFTER the grounded branch above so it overwrites the
            // stick velocity rather than being overwritten by it.
            //
            // SPACE is also the bowling power meter, and that is FINE: this
            // component only runs in Roaming and BallLauncher only reads input
            // while IsAiming, so the two can never both see the key. That
            // separation is PlayerAvatar.ApplyMode's whole job.
            if (_controller.isGrounded && config.JumpHeight > 0f && Input.GetKeyDown(KeyCode.Space))
            {
                // Solve take-off speed from the height we actually want:
                // v = sqrt(-2 * g * h). Doing it this way means retuning Gravity
                // changes how SNAPPY the jump is without changing how HIGH it
                // goes, which is the knob you actually want to turn separately.
                // Gravity is negative, so -2 * g is positive and the root is real.
                _verticalVelocity = Mathf.Sqrt(-2f * config.Gravity * config.JumpHeight);
            }

            Vector3 velocity = worldDirection * speed + Vector3.up * _verticalVelocity;

            // Move() takes a DISPLACEMENT (meters this frame), not a velocity,
            // hence the deltaTime here — unlike the mouse deltas above.
            _controller.Move(velocity * Time.deltaTime);
        }

        /// <summary>
        /// Feeds the Animator's Speed float so the walk cycle plays. Worth doing
        /// even though the local player cannot see their own body: this same
        /// model is what OTHER players see once Mirror lands, and a character
        /// sliding around in a T-pose idle is the giveaway that movement was
        /// bolted on. CharacterSetupTool already built the Idle<->Walking
        /// transitions against this exact parameter (threshold 0.1).
        /// </summary>
        private void DriveWalkAnimation()
        {
            if (reactionActor == null) return;

            // CharacterController.velocity is the ACTUAL movement achieved last
            // frame, not what we asked for — so walking into a wall correctly
            // stops the walk cycle instead of moonwalking on the spot.
            Vector3 planar = _controller.velocity;
            planar.y = 0f;

            // Normalised against walk speed, so a walk reads as 0..1 and
            // sprinting reads distinctly higher (up to ~SprintMultiplier)
            // instead of both clamping to the same 1.0 — CharacterSetupTool's
            // Walking<->Sprint transition (SprintSpeedThreshold) needs the two
            // to actually be tellable apart. Mathf.Max(1f, ...) is defensive
            // only: SprintMultiplier is documented as ">= 1, 1 = no sprint",
            // but a stray retune below 1 would otherwise flip Clamp's bounds.
            float target = Mathf.Clamp(planar.magnitude / Mathf.Max(0.01f, config.WalkSpeed),
                                        0f, Mathf.Max(1f, config.SprintMultiplier));

            _animatorSpeed = Mathf.SmoothDamp(_animatorSpeed, target, ref _animatorSpeedVelocity,
                                              config.AnimatorSpeedDampTime);
            reactionActor.SetSpeed(_animatorSpeed);
        }
    }
}
