using UnityEngine;

namespace WeeSpurts.Player
{
    /// <summary>
    /// The camera arbiter for ONE player. Listens to
    /// <see cref="PlayerAvatar.OnModeChanged"/> and makes sure exactly one
    /// Camera and exactly one AudioListener are enabled: the first-person one
    /// while roaming, the bowling one while bowling.
    ///
    /// TOGGLE THE COMPONENTS, NEVER THE GAMEOBJECT. This is load-bearing, not
    /// style. The bowling camera also carries ThrowCamera and
    /// ThrowCameraSequence, and ThrowCameraSequence subscribes to
    /// launcher.OnThrow / ball.OnSettled in OnEnable and unsubscribes in
    /// OnDisable. Deactivating that GameObject would churn those subscriptions
    /// every time anyone walks to or from the line, and OnDisable also calls
    /// EndSequenceFraming() and resets its beat — so a scripted camera move
    /// would be torn down mid-shot. Disabling only the Camera COMPONENT stops it
    /// rendering and leaves every one of those scripts running untouched.
    ///
    /// This class does not know ThrowCamera, ThrowCameraSequence or
    /// NukeShotResolver exist, and must not learn. It touches two Camera
    /// components and two AudioListener components. That's the whole job.
    ///
    /// SETUP: on the Player root next to PlayerAvatar. RoamingSetupTool wires it.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerCameraDirector : MonoBehaviour
    {
        // [SerializeField] on all of these — wired by an editor tool, so they
        // must survive a scene reload (the AimPreview lesson).
        [Tooltip("The avatar whose mode changes we follow. Normally the PlayerAvatar on this same object.")]
        [SerializeField] private PlayerAvatar avatar;

        [Header("Roaming")]
        [Tooltip("The Camera COMPONENT on the FirstPersonCamera child.")]
        [SerializeField] private Camera firstPersonCamera;

        [Tooltip("The AudioListener COMPONENT on the FirstPersonCamera child.")]
        [SerializeField] private AudioListener firstPersonListener;

        [Header("Bowling")]
        [Tooltip("The Camera COMPONENT on the scene's bowling camera (the one carrying ThrowCamera). Its GameObject is NEVER deactivated — see the class comment.")]
        [SerializeField] private Camera bowlingCamera;

        [Tooltip("The AudioListener COMPONENT on the bowling camera, if it has one.")]
        [SerializeField] private AudioListener bowlingListener;

        /// <summary>
        /// SPIKE Step 4: wires the bowling camera/listener at RUNTIME, not
        /// edit time. RoamingSetupTool wires these on a scene-baked player
        /// via SerializedObject because that player and the bowling camera
        /// are both fixed objects in the SAME saved scene — but a networked
        /// player is a PREFAB instantiated per connection, and a prefab asset
        /// cannot hold a reference to a scene-only object (there is no scene
        /// yet when the prefab is saved). So the one shared scene camera gets
        /// found and handed to each spawned avatar in code instead.
        /// </summary>
        public void Configure(Camera bowlingCam, AudioListener bowlingListen)
        {
            bowlingCamera = bowlingCam;
            bowlingListener = bowlingListen;
        }

        private void OnEnable()
        {
            // Subscribe in OnEnable, unsubscribe in OnDisable. Unity runs every
            // OnEnable during scene load BEFORE any Start, and PlayerAvatar
            // applies its starting mode in Start — so we are always listening in
            // time for the first mode change and never miss it.
            if (avatar != null) avatar.OnModeChanged += Apply;
        }

        private void OnDisable()
        {
            // "-=" on a handler that was never added is a safe no-op, so this is
            // correct even if avatar was null when we enabled.
            if (avatar != null) avatar.OnModeChanged -= Apply;
        }

        /// <summary>
        /// Enable exactly one camera and one listener for the given mode.
        /// Public so a future system (a spectator cam, a replay) can drive it
        /// directly, but PlayerAvatar's event is the normal path.
        /// </summary>
        public void Apply(ControlMode mode)
        {
            // A remote player's avatar must never switch THIS machine's camera.
            // Same guard as the cursor in PlayerAvatar, for the same reason.
            if (avatar != null && !avatar.isLocalPlayer) return;

            bool roaming = mode == ControlMode.Roaming;

            // Order does not matter for rendering, but enabling the incoming
            // listener before disabling the outgoing one would briefly leave two
            // AudioListeners alive, which Unity warns about. Off first, then on.
            if (firstPersonListener != null) firstPersonListener.enabled = false;
            if (bowlingListener != null) bowlingListener.enabled = false;

            if (firstPersonCamera != null) firstPersonCamera.enabled = roaming;
            if (bowlingCamera != null) bowlingCamera.enabled = !roaming;

            if (roaming)
            {
                if (firstPersonListener != null) firstPersonListener.enabled = true;
            }
            else
            {
                if (bowlingListener != null) bowlingListener.enabled = true;
            }
        }
    }
}
