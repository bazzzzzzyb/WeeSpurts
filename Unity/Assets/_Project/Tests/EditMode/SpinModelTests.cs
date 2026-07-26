using NUnit.Framework;
using UnityEngine;
using WeeSpurts.Bowling;

namespace WeeSpurts.Tests
{
    /// <summary>
    /// Guards the spin maths. SpinModel is pure static float maths with no
    /// Unity objects, which is exactly why it was split out of BowlingBall —
    /// the physics claims ("topspin curves earlier", "backspin curves later and
    /// harder", "shape changes when, not how much") are checkable here without
    /// a scene, a Rigidbody, or a play session.
    ///
    /// The important ones are the INVARIANTS, not the specific numbers: those
    /// are what stop a feel-tuning session from quietly breaking the model.
    /// </summary>
    public class SpinModelTests
    {
        private const float Tolerance = 0.0005f;

        // ---------- Clamp: a circle, not a square ----------

        [Test]
        public void Clamp_LeavesSpinInsideTheCircleAlone()
        {
            var spin = new Vector2(0.3f, -0.4f); // magnitude 0.5
            Assert.AreEqual(spin, SpinModel.Clamp(spin));
        }

        [Test]
        public void Clamp_PullsAFullDiagonalBackOntoTheCircle()
        {
            // The whole reason the widget is round: (1,1) must NOT be allowed
            // through as a spin 1.41x stronger than full sideways.
            Vector2 clamped = SpinModel.Clamp(new Vector2(1f, 1f));
            Assert.AreEqual(1f, clamped.magnitude, Tolerance);
            Assert.AreEqual(clamped.x, clamped.y, Tolerance, "a diagonal must stay diagonal");
        }

        // ---------- RampShape: WHEN the curve happens ----------

        [Test]
        public void RampShape_NeutralSpinIsFlatEverywhere()
        {
            // Y = 0 must reproduce the pre-2D behaviour exactly: one constant
            // sideways force for the whole roll.
            for (float u = 0f; u <= 1f; u += 0.1f)
                Assert.AreEqual(1f, SpinModel.RampShape(u, 0f), Tolerance);
        }

        [Test]
        public void RampShape_TopspinIsFrontLoaded()
        {
            Assert.AreEqual(2f, SpinModel.RampShape(0f, 1f), Tolerance, "bites hardest at release");
            Assert.AreEqual(0f, SpinModel.RampShape(1f, 1f), Tolerance, "spent by the pins");
        }

        [Test]
        public void RampShape_BackspinIsBackLoaded()
        {
            Assert.AreEqual(0f, SpinModel.RampShape(0f, -1f), Tolerance, "skids first");
            Assert.AreEqual(2f, SpinModel.RampShape(1f, -1f), Tolerance, "breaks at the pins");
        }

        [Test]
        public void RampShape_AveragesToOneForEveryVerticalSpin()
        {
            // THE load-bearing invariant. RampShape only ever redistributes the
            // curve over the lane; how MUCH curve there is belongs to GripScale.
            // If this drifts, tuning the ramp secretly changes hook strength too
            // and the two knobs stop being independent.
            for (float y = -1f; y <= 1f; y += 0.25f)
            {
                float sum = 0f;
                const int Samples = 2000;
                for (int i = 0; i < Samples; i++)
                    sum += SpinModel.RampShape((i + 0.5f) / Samples, y);

                Assert.AreEqual(1f, sum / Samples, 0.002f, $"mean drifted at spinY {y}");
            }
        }

        [Test]
        public void RampShape_IsNeverNegative()
        {
            // A negative shape would flip the hook mid-roll — the ball would
            // curve left, then right, off one dialled spin. Not a feature.
            for (float y = -1f; y <= 1f; y += 0.1f)
                for (float u = 0f; u <= 1f; u += 0.05f)
                    Assert.GreaterOrEqual(SpinModel.RampShape(u, y), -Tolerance);
        }

        // ---------- GripScale: HOW MUCH curve there is ----------

        [Test]
        public void GripScale_TopspinCurvesLessAndBackspinCurvesMore()
        {
            const float K = 0.6f; // BallConfig.RollSkidHookScale default
            Assert.AreEqual(0.4f, SpinModel.GripScale(1f, K), Tolerance, "topspin grips and runs straighter");
            Assert.AreEqual(1f, SpinModel.GripScale(0f, K), Tolerance, "neutral is unchanged");
            Assert.AreEqual(1.6f, SpinModel.GripScale(-1f, K), Tolerance, "backspin skids then bites harder");
        }

        [Test]
        public void GripScale_ZeroKnobMakesVerticalSpinPurelyAboutTiming()
        {
            // Documented as a legitimate feel choice, so it's worth pinning:
            // at 0 the vertical axis still moves the curve around but never
            // changes its total.
            for (float y = -1f; y <= 1f; y += 0.25f)
                Assert.AreEqual(1f, SpinModel.GripScale(y, 0f), Tolerance);
        }

        // ---------- LateralForce: player spin only ----------

        [Test]
        public void LateralForce_CentredSpinProducesNoCurve()
        {
            for (float u = 0f; u <= 1f; u += 0.1f)
                Assert.AreEqual(0f, SpinModel.LateralForce(Vector2.zero, u, 6f, 0.6f), Tolerance);
        }

        [Test]
        public void LateralForce_SideSpinSignPicksTheHookDirection()
        {
            Assert.Less(SpinModel.LateralForce(new Vector2(-1f, 0f), 0.5f, 6f, 0.6f), 0f, "left spin hooks left");
            Assert.Greater(SpinModel.LateralForce(new Vector2(1f, 0f), 0.5f, 6f, 0.6f), 0f, "right spin hooks right");
        }

        [Test]
        public void LateralForce_NeutralSpinMatchesTheOldFlatModel()
        {
            // Regression guard on the schema change: a purely horizontal dial
            // must behave exactly as the old single-float Spin did — spin * force,
            // constant for the whole roll.
            var spin = new Vector2(0.5f, 0f);
            for (float u = 0f; u <= 1f; u += 0.1f)
                Assert.AreEqual(3f, SpinModel.LateralForce(spin, u, 6f, 0.6f), Tolerance);
        }

        [Test]
        public void LateralForce_BackspinBitesHardestAtThePins()
        {
            var backspin = new Vector2(1f, -1f);
            float atRelease = SpinModel.LateralForce(backspin, 0f, 6f, 0.6f);
            float atPins = SpinModel.LateralForce(backspin, 1f, 6f, 0.6f);

            Assert.AreEqual(0f, atRelease, Tolerance, "backspin skids: no curve off the hand");
            Assert.Greater(atPins, SpinModel.LateralForce(new Vector2(1f, 0f), 1f, 6f, 0.6f),
                "the late break must beat a neutral roll where it matters");
        }

        [Test]
        public void LateralForce_TopspinBitesHardestOffTheHand()
        {
            var topspin = new Vector2(1f, 1f);
            Assert.Greater(SpinModel.LateralForce(topspin, 0f, 6f, 0.6f),
                           SpinModel.LateralForce(topspin, 1f, 6f, 0.6f),
                           "topspin front-loads its curve, then rolls out");
        }

        // ---------- DriveForce: topspin only, never a power penalty ----------

        [Test]
        public void DriveForce_OnlyTopspinDrives()
        {
            Assert.AreEqual(3f, SpinModel.DriveForce(new Vector2(0f, 1f), 3f), Tolerance);
            Assert.AreEqual(0f, SpinModel.DriveForce(Vector2.zero, 3f), Tolerance);
        }

        [Test]
        public void DriveForce_BackspinIsNeverPenalised()
        {
            // Tony's rule: spin must never fight the timing meter for control of
            // speed. Drive may add, never subtract.
            for (float y = -1f; y < 0f; y += 0.1f)
                Assert.AreEqual(0f, SpinModel.DriveForce(new Vector2(0f, y), 3f), Tolerance);
        }

        // ---------- NormalizedDrift: the preview must match the physics ----------

        [Test]
        public void NormalizedDrift_MatchesTheClosedFormAtThePins()
        {
            // These three are the analytic double integrals of RampShape. If the
            // preview curve stops matching them it has started lying about where
            // the ball goes.
            Assert.AreEqual(0.5f, SpinModel.NormalizedDrift(1f, 0f), Tolerance, "neutral: u^2/2");
            Assert.AreEqual(2f / 3f, SpinModel.NormalizedDrift(1f, 1f), Tolerance, "topspin: u^2 - u^3/3");
            Assert.AreEqual(1f / 3f, SpinModel.NormalizedDrift(1f, -1f), Tolerance, "backspin: u^3/3");
        }

        [Test]
        public void NormalizedDrift_IsTheIntegralOfRampShape()
        {
            // The claim that ties preview to physics: drift is what you get by
            // integrating the ramp force twice. Verified numerically rather than
            // trusted, because a wrong integral would make the preview
            // confidently wrong instead of obviously broken.
            foreach (float y in new[] { -1f, -0.5f, 0f, 0.5f, 1f })
            {
                const int Steps = 4000;
                const float Dt = 1f / Steps;
                float velocity = 0f, position = 0f;

                for (int i = 0; i < Steps; i++)
                {
                    velocity += SpinModel.RampShape((i + 0.5f) * Dt, y) * Dt;
                    position += velocity * Dt;
                }

                Assert.AreEqual(SpinModel.NormalizedDrift(1f, y), position, 0.002f,
                    $"closed form and numerical integration disagree at spinY {y}");
            }
        }

        [Test]
        public void NormalizedDrift_BackspinStaysStraighterEarlyThanTopspin()
        {
            // The visible signature of a late break: a third of the way down the
            // lane, the skidding ball has barely moved sideways compared to the
            // one that gripped immediately.
            float top = SpinModel.NormalizedDrift(0.33f, 1f);
            float neutral = SpinModel.NormalizedDrift(0.33f, 0f);
            float back = SpinModel.NormalizedDrift(0.33f, -1f);

            Assert.Greater(top, neutral);
            Assert.Less(back, neutral);
        }

        [Test]
        public void NormalizedDrift_NeverGoesBackwards()
        {
            // Drift is a distance travelled sideways under a one-directional
            // force, so it can only ever grow. A dip here would draw a preview
            // line that curves back on itself.
            foreach (float y in new[] { -1f, -0.5f, 0f, 0.5f, 1f })
            {
                float previous = -1f;
                for (float u = 0f; u <= 1f; u += 0.02f)
                {
                    float drift = SpinModel.NormalizedDrift(u, y);
                    Assert.GreaterOrEqual(drift, previous - Tolerance, $"drift went backwards at spinY {y}");
                    previous = drift;
                }
            }
        }

        // ---------- Progress01 ----------

        [Test]
        public void Progress01_RunsZeroToOneOverTheRampAndStaysClamped()
        {
            Assert.AreEqual(0f, SpinModel.Progress01(0f, 18f), Tolerance);
            Assert.AreEqual(0.5f, SpinModel.Progress01(9f, 18f), Tolerance);
            Assert.AreEqual(1f, SpinModel.Progress01(18f, 18f), Tolerance);
            Assert.AreEqual(1f, SpinModel.Progress01(100f, 18f), Tolerance, "past the ramp stays rolled out");
            Assert.AreEqual(0f, SpinModel.Progress01(-5f, 18f), Tolerance, "a backward fumble never goes negative");
        }

        [Test]
        public void Progress01_SurvivesAZeroRampDistance()
        {
            // A mistuned config must not divide by zero and hand PhysX a NaN
            // force, which would fling the ball to infinity.
            float p = SpinModel.Progress01(1f, 0f);
            Assert.IsFalse(float.IsNaN(p));
            Assert.IsFalse(float.IsInfinity(p));
        }
    }
}
