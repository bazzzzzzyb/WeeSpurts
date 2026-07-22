using UnityEngine;

namespace WeeSpurts.Bowling
{
    /// <summary>
    /// Two-mode camera: a fixed behind-the-lane view while aiming, and a
    /// smooth chase of the ball once thrown. Deliberately simple — real
    /// juice (shake, hit-pause, dramatic pin cam) is a physics-tech-artist
    /// session later; this just makes the prototype watchable.
    ///
    /// SETUP: on the Main Camera. GreyboxSceneBuilder wires the references.
    /// </summary>
    public class ThrowCamera : MonoBehaviour
    {
        [SerializeField] private Transform followTarget;   // the ball
        [SerializeField] private Vector3 aimViewPosition;
        [SerializeField] private Vector3 aimViewEuler;
        [SerializeField] private Vector3 followOffset = new Vector3(0f, 1.6f, -2.5f);
        [SerializeField] private float followSmoothTime = 0.25f;

        private bool _following;
        private Vector3 _velocity;

        public void ConfigureAimView(Vector3 position, Vector3 euler, Transform ball)
        {
            aimViewPosition = position;
            aimViewEuler = euler;
            followTarget = ball;
            SnapToAimView();
        }

        public void SnapToAimView()
        {
            _following = false;
            transform.SetPositionAndRotation(aimViewPosition, Quaternion.Euler(aimViewEuler));
        }

        public void FollowBall() => _following = true;

        private void LateUpdate()
        {
            if (!_following || followTarget == null) return;

            Vector3 desired = followTarget.position + followOffset;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _velocity, followSmoothTime);
            transform.LookAt(followTarget.position + Vector3.forward * 1.5f);
        }
    }
}
