using System.Collections;
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
        // Slower than followSmoothTime on purpose: the Nuke Shot's rising-sphere
        // chase (FollowRising) should read as a distinct, more deliberate camera
        // move than the fast normal ball-chase (FollowBall), not identical to it.
        [SerializeField] private float nukeRiseFollowSmoothTime = 0.6f;

        private bool _following;
        // True while a hard-cut static framing (CutToBehindAbove) should persist
        // as-is instead of being reset to aimViewPosition every frame — see the
        // LateUpdate else-branch below.
        private bool _staticHold;
        // Which SmoothDamp smooth time the follow branch in LateUpdate should use
        // this frame. Set by whichever follow-starting method (FollowBall /
        // FollowRising) was called last, so each can pick its own pace without
        // duplicating the LateUpdate follow branch.
        private float _activeFollowSmoothTime;
        // The real ball, cached once at scene-build time via ConfigureAimView.
        // FollowRising temporarily repoints followTarget at the Nuke sphere;
        // FollowBall uses this to hand followTarget back to the real ball
        // afterwards instead of leaking the sphere as a permanent follow target.
        private Transform _ballTarget;
        private Vector3 _velocity;
        private Vector3 _shakeOffset;
        private Coroutine _shakeRoutine;
        // The camera's position with shake NOT yet applied. Tracked separately
        // from transform.position so shake can be added fresh each frame
        // instead of accumulating into the value SmoothDamp/the static aim
        // view read back next frame (which would make the camera drift away
        // permanently instead of returning to anchor once a shake decays —
        // this matters for e.g. the Nuke Shot, whose Shake() calls happen
        // while the camera is still in the STATIC aim view, not following).
        private Vector3 _basePosition;

        // --- Scripted-sequence hand-off (ThrowCameraSequence) --------------
        // When a scripted camera move is running it computes a pose in Update()
        // and pushes it here; this component stays the ONLY thing that writes
        // transform.position, so shake still layers on top of a clean anchor.
        // If ThrowCameraSequence isn't in the scene these stay false/identity
        // and every line below behaves exactly as it did before it existed.
        private bool _sequenceActive;
        private Vector3 _sequencePosition;
        private Quaternion _sequenceRotation = Quaternion.identity;

        /// <summary>The static aim-phase framing. ThrowCameraSequence builds its
        /// "take stance" beat ON these values (plus small nudge offsets) rather
        /// than hard-coding a second copy of them.</summary>
        public Vector3 AimViewPosition => aimViewPosition;
        public Vector3 AimViewEuler => aimViewEuler;

        /// <summary>
        /// Called every frame by a running scripted camera move. Stores the pose
        /// for LateUpdate to apply, and takes precedence over the follow/static
        /// branches until EndSequenceFraming (or a Nuke camera call) hands it back.
        /// </summary>
        public void SetSequenceFraming(Vector3 position, Quaternion rotation)
        {
            _sequencePosition = position;
            _sequenceRotation = rotation;
            _basePosition = position;
            _following = false;
            _staticHold = false;
            _sequenceActive = true;
        }

        /// <summary>Hands the camera back to the normal follow/static behaviour.</summary>
        public void EndSequenceFraming() => _sequenceActive = false;
        // -------------------------------------------------------------------

        public void ConfigureAimView(Vector3 position, Vector3 euler, Transform ball)
        {
            aimViewPosition = position;
            aimViewEuler = euler;
            followTarget = ball;
            _ballTarget = ball;
            SnapToAimView();
        }

        public void SnapToAimView()
        {
            // Deliberately does NOT clear _sequenceActive. BowlingMatchFlow
            // calls this at the start of every roll, and a scripted camera move
            // wants to EASE back to the aim framing from wherever it is — if this
            // stole the camera back, every new roll would snap the shot out from
            // under a live move. LateUpdate's sequence branch re-applies the
            // sequence's own pose, so the write below is harmlessly overridden
            // for that one frame while a move is running.
            _following = false;
            _staticHold = false;
            _basePosition = aimViewPosition;
            transform.SetPositionAndRotation(_basePosition + _shakeOffset, Quaternion.Euler(aimViewEuler));
        }

        public void FollowBall()
        {
            // Hand followTarget back to the real ball in case a Nuke throw's
            // FollowRising() last repointed it at the sphere — otherwise the
            // NEXT normal throw would chase the sphere instead of the ball.
            followTarget = _ballTarget;
            _activeFollowSmoothTime = followSmoothTime;
            _staticHold = false;
            _following = true;
        }

        /// <summary>
        /// Nuke Shot only: slow deliberate chase of the rising sphere, distinct
        /// from the fast normal ball-chase (FollowBall). Temporarily repoints
        /// followTarget at the sphere — FollowBall() restores it to the real
        /// ball afterwards, see the comment there.
        /// </summary>
        public void FollowRising(Transform target)
        {
            followTarget = target;
            _activeFollowSmoothTime = nukeRiseFollowSmoothTime;
            _staticHold = false;
            // Unlike SnapToAimView/FollowBall, the Nuke explicitly RECLAIMS the
            // camera: its canned sequence is the shot, so any scripted move must
            // get out of the way. (ThrowCameraSequence also stands itself down on
            // a Nuke throw — this is the second layer of defence.)
            _sequenceActive = false;
            _following = true;
        }

        /// <summary>
        /// Nuke Shot only: hard cut (not a smooth move) to a fixed shot behind
        /// and above the sphere's current (peak-height) position, looking at it.
        /// The dramatic "it's about to come down" anticipation beat.
        /// </summary>
        public void CutToBehindAbove(Vector3 targetPosition)
        {
            _following = false;
            // Held static (not reset to aimViewPosition) until the next
            // SnapToAimView/FollowBall/FollowRising call — see _staticHold.
            _staticHold = true;
            // Same reason as FollowRising: the Nuke owns the camera outright.
            _sequenceActive = false;
            Vector3 offset = new Vector3(0f, 2.5f, -4f); // above and behind; tune if it looks wrong, this is a first guess
            _basePosition = targetPosition + offset;
            transform.position = _basePosition + _shakeOffset;
            // LookAt (not Quaternion.LookRotation) so this stays robust even if
            // the offset above is ever tuned to something whose look direction
            // could end up parallel to Vector3.up (LookRotation's default up
            // vector), which would otherwise risk a degenerate rotation.
            transform.LookAt(targetPosition);
        }

        /// <summary>
        /// Deterministic screen shake (juice for e.g. the Nuke Shot explosion).
        /// Purely a local camera-view cosmetic — never synced, never affects
        /// LaunchParameters/scoring — so a sine wiggle instead of live Random
        /// is fine even though the rest of throw-chaos avoids live randomness.
        /// </summary>
        public void Shake(float duration, float magnitude)
        {
            if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
            _shakeRoutine = StartCoroutine(ShakeRoutine(duration, magnitude));
        }

        private IEnumerator ShakeRoutine(float duration, float magnitude)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float decay = 1f - (t / duration);
                // Two dephased sine waves — deterministic wiggle, not UnityEngine.Random.
                _shakeOffset = new Vector3(Mathf.Sin(t * 55f), Mathf.Sin(t * 71f + 1.3f), 0f) * magnitude * decay;
                yield return null;
            }
            _shakeOffset = Vector3.zero;
        }

        private void LateUpdate()
        {
            if (_sequenceActive)
            {
                // A scripted camera move (ThrowCameraSequence) owns the framing
                // this frame. It already handed us the pose in Update(), and
                // Unity runs every Update() before any LateUpdate(), so this is
                // always the CURRENT frame's pose — no execution-order tweaking.
                // Re-applying _basePosition here (rather than trusting the value
                // SetSequenceFraming wrote) matters because BowlingMatchFlow
                // may call SnapToAimView from a coroutine in between, which runs
                // after Update but before LateUpdate.
                _basePosition = _sequencePosition;
                transform.rotation = _sequenceRotation;
            }
            else if (_following && followTarget != null)
            {
                Vector3 desired = followTarget.position + followOffset;
                _basePosition = Vector3.SmoothDamp(_basePosition, desired, ref _velocity, _activeFollowSmoothTime);
                transform.LookAt(followTarget.position + Vector3.forward * 1.5f);
            }
            else if (!_staticHold)
            {
                // Not following and no static-cut framing to hold (e.g.
                // CutToBehindAbove) — default back to the static aim view.
                _basePosition = aimViewPosition;
            }
            // else: _staticHold true — leave _basePosition exactly as the last
            // static-cut call set it, so that framing persists frame to frame
            // instead of snapping back to aimViewPosition.

            // Additive on top of whatever follow/static logic above already
            // computed, so shake never fights the existing positioning — and
            // recomputed from the clean _basePosition every frame (not
            // transform.position, which already has last frame's shake baked
            // in) so the camera always returns exactly to anchor once a shake
            // decays, instead of permanently drifting.
            transform.position = _basePosition + _shakeOffset;
        }
    }
}
