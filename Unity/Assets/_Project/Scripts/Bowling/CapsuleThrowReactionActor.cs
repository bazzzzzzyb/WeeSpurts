using System.Collections;
using UnityEngine;

namespace WeeSpurts.Bowling
{
    /// <summary>
    /// Greybox "Body English" reaction: a capsule standing in for the
    /// thrower leans/faceplants based on how the release went. Purely
    /// cosmetic — reads LaunchParameters, never writes them. A pure
    /// function of TimingError01/IsBackwardFumble plus the tunables below;
    /// no live randomness, so every client renders the same target pose.
    ///
    /// SETUP: sits on a Capsule primitive near the foul line.
    /// GreyboxSceneBuilder creates it and wires it into BowlingGameController.
    /// </summary>
    public class CapsuleThrowReactionActor : MonoBehaviour, IThrowReactionActor
    {
        [Tooltip("How hard (0..1 timing error -> degrees) a bad-timing release leans the capsule sideways, at TimingError01 = ±1.")]
        [SerializeField] private float maxLeanAngleDegrees = 35f;

        [Tooltip("Shapes the lean curve: 1 = linear, >1 = mild near 0 and dramatic near the extremes (matches the Hook/Cone non-linear feel).")]
        [SerializeField] private float severityExponent = 2f;

        [Tooltip("Seconds to tween from neutral into the reaction pose.")]
        [SerializeField] private float leanDuration = 0.15f;

        [Tooltip("Seconds to hold the reaction pose before recovering.")]
        [SerializeField] private float holdDuration = 0.4f;

        [Tooltip("Seconds to tween back to neutral (identity rotation).")]
        [SerializeField] private float recoverDuration = 0.5f;

        [Tooltip("Forward pitch (degrees) for the backward-fumble faceplant gag.")]
        [SerializeField] private float faceplantAngleDegrees = 80f;

        private Coroutine _running;

        public void PlayReaction(LaunchParameters p)
        {
            // Stop any in-flight tween so a rapid re-trigger (e.g. sandbox F
            // frame-reset) doesn't stack multiple tweens fighting each other —
            // same reason BowlingGameController.ResetCurrentFrame calls
            // StopAllCoroutines().
            if (_running != null) StopCoroutine(_running);

            Quaternion target = p.IsBackwardFumble
                ? Quaternion.Euler(faceplantAngleDegrees, 0f, 0f)
                : Quaternion.Euler(0f, 0f, ComputeLeanDegrees(p.TimingError01));

            _running = StartCoroutine(PlayPoseTween(target));
        }

        /// <summary>
        /// Lean angle around Z (roll), signed to match the ball's Hook
        /// direction convention — the capsule visibly leans the same way it
        /// "yanked" the ball. 0 at perfect timing (confident, upright).
        /// Negated to match BowlingBall.Launch()'s Hook sign fix (Docs/
        /// OpenQuestions.md: hard release hooks RIGHT, soft hooks LEFT) — this
        /// must stay in lockstep with that computation or the thrower's lean
        /// and the ball's hook would visibly disagree.
        /// </summary>
        private float ComputeLeanDegrees(float timingError01)
        {
            return -Mathf.Sign(timingError01) * Mathf.Pow(Mathf.Abs(timingError01), severityExponent) * maxLeanAngleDegrees;
        }

        private IEnumerator PlayPoseTween(Quaternion target)
        {
            yield return TweenRotation(transform.localRotation, target, leanDuration);
            yield return new WaitForSeconds(holdDuration);
            yield return TweenRotation(transform.localRotation, Quaternion.identity, recoverDuration);
            _running = null;
        }

        private IEnumerator TweenRotation(Quaternion from, Quaternion to, float duration)
        {
            if (duration <= 0f)
            {
                transform.localRotation = to;
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                transform.localRotation = Quaternion.Slerp(from, to, Mathf.Clamp01(t / duration));
                yield return null;
            }
            transform.localRotation = to;
        }
    }
}
