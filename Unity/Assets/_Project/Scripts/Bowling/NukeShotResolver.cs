using System.Collections;
using UnityEngine;

namespace WeeSpurts.Bowling
{
    /// <summary>
    /// The Nuke Shot's presentation layer: plays the up/lock-on/rocket-down
    /// tween (a hit) or the in-hand fizzle (a miss). The tween is intentionally
    /// NOT physics — pure Transform.position interpolation, deterministic and
    /// identical on every future client, same as PlayHitSequence/PlayMissSequence
    /// just being ordinary coroutines a host and clients can all run from the
    /// same LaunchParameters. The explosion (PinDeck.ApplyExplosion, which
    /// itself calls Rigidbody.AddExplosionForce) is the ONE point in this whole
    /// sequence where real physics resolves the actual outcome — everything
    /// else here is cosmetic.
    ///
    /// SETUP: sits on a "NukeShot" GameObject. GreyboxSceneBuilder creates the
    /// nuke sphere + poof ParticleSystem and wires them in; only these two
    /// references are editor-wired, everything else this needs comes in as
    /// method parameters from BowlingPresentation to keep the wiring surface
    /// small.
    /// </summary>
    public class NukeShotResolver : MonoBehaviour
    {
        [SerializeField] private Transform nukeSphere;
        [SerializeField] private ParticleSystem poofEffect;

        /// <summary>
        /// Defensive cleanup: forces the nuke sphere hidden, the ball visible, and
        /// any in-progress/lingering poof cleared immediately. Called at the start
        /// of every new roll (BowlingMatchFlow.BeginRoll) so an interrupted
        /// nuke sequence — e.g. the sandbox F-key frame-reset firing mid-tween —
        /// never leaves stale visual state bleeding into the next roll.
        ///
        /// Why F-key mid-tween needs this: ResetCurrentFrame() calls
        /// StopAllCoroutines() on BowlingMatchFlow. PlayHitSequence/
        /// PlayMissSequence are never started with their OWN StartCoroutine call —
        /// ResolveNukeThrow reaches them via `yield return
        /// _presentation.PlayNukeHitSequence(...)`, which hands back this very
        /// enumerator and nests it inside the SAME coroutine
        /// BowlingMatchFlow started for ResolveThrow. So StopAllCoroutines()
        /// aborts them too, wherever they happened to be (sphere mid-tween, ball
        /// hidden, poof mid-play) — hence needing a hard reset here, not just relying
        /// on the sequences to finish and clean up after themselves.
        ///
        /// Stop(true, StopEmittingAndClear) alone (not a separate Clear() call) is
        /// enough: that stop behavior already removes all existing particles
        /// immediately as part of the same call, so it covers "stop and clear now"
        /// in one step.
        /// </summary>
        public void ResetVisualState(BowlingBall ball)
        {
            nukeSphere.gameObject.SetActive(false);
            poofEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ball.SetVisible(true);
        }

        public IEnumerator PlayHitSequence(BallConfig nukeConfig, Vector3 spawnPosition, Vector3 pinClusterCenter, PinDeck pinDeck, BowlingBall ball, ThrowCamera throwCamera)
        {
            ball.SetVisible(false);
            nukeSphere.gameObject.SetActive(true);
            nukeSphere.position = spawnPosition;

            Vector3 upPos = spawnPosition + Vector3.up * 6f;
            throwCamera.FollowRising(nukeSphere);
            // TODO(sound): rising "power up" hum/charge SFX starts here.
            yield return TweenPosition(nukeSphere, spawnPosition, upPos, nukeConfig.NukeTweenDuration);

            throwCamera.CutToBehindAbove(upPos);
            // TODO(sound): "lock-on" beacon beep/lock SFX starts here.
            // TODO(vfx): lock-on beacon light/glow effect on the nuke sphere here (currently a plain colored sphere).
            yield return new WaitForSeconds(nukeConfig.NukeLockOnPauseDuration);

            // TODO(sound): rocket-down whoosh SFX starts here.
            yield return TweenPosition(nukeSphere, upPos, pinClusterCenter, nukeConfig.NukeTweenDuration);
            // Camera intentionally stays at the CutToBehindAbove framing through the
            // rocket-down and explosion — no new camera call here, holding the shot
            // steady for the impact reads better than cutting again right at the payoff.

            poofEffect.transform.position = pinClusterCenter;
            poofEffect.Play();
            // TODO(sound): explosion boom SFX here.
            // TODO(vfx): real explosion particle effect to replace this placeholder poof.
            pinDeck.ApplyExplosion(pinClusterCenter, nukeConfig.NukeBlastRadius, nukeConfig.NukeExplosionForce);
            throwCamera.Shake(0.4f, 0.15f);

            nukeSphere.gameObject.SetActive(false);
            ball.SetVisible(true);
        }

        public IEnumerator PlayMissSequence(BallConfig nukeConfig, Vector3 chestPosition, BowlingBall ball, ThrowCamera throwCamera)
        {
            ball.SetVisible(false);
            poofEffect.transform.position = chestPosition;
            poofEffect.Play();
            throwCamera.Shake(0.25f, 0.08f);
            yield return new WaitForSeconds(0.3f);
            ball.SetVisible(true);
        }

        private IEnumerator TweenPosition(Transform t, Vector3 from, Vector3 to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                t.position = Vector3.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            t.position = to;
        }
    }
}
