using NUnit.Framework;
using UnityEngine;
using WeeSpurts.Bowling;

namespace WeeSpurts.Tests
{
    /// <summary>
    /// Unit tests for the scripted throw camera's pure framing math. Run in
    /// Unity: Window > General > Test Runner > EditMode > Run All.
    ///
    /// Only ThrowCameraFraming is tested here, and that is the point of it
    /// existing as a separate static class: it needs no Camera, no Transform
    /// and no scene, so the tricky arithmetic behind the camera move can be
    /// pinned down without entering Play mode. The beat sequencing itself
    /// (ThrowCameraSequence) is feel work and stays Tony's call by playing it.
    /// </summary>
    public class ThrowCameraFramingTests
    {
        private const float Tolerance = 0.001f;

        // ---------- horizontal FOV ----------

        [Test]
        public void HorizontalFov_EqualsVerticalFov_AtSquareAspect()
        {
            // At aspect 1 the frame is square, so both fields of view match.
            Assert.AreEqual(60f, ThrowCameraFraming.HorizontalFovDegrees(60f, 1f), Tolerance);
        }

        [Test]
        public void HorizontalFov_IsWiderThanVertical_OnAWidescreen()
        {
            float h = ThrowCameraFraming.HorizontalFovDegrees(60f, 16f / 9f);
            Assert.Greater(h, 60f);
        }

        [Test]
        public void HorizontalFov_SurvivesDegenerateAspect()
        {
            // An uninitialised camera can report aspect 0. Must not return NaN:
            // a NaN here would propagate into the camera's position and park it
            // nowhere at all.
            float h = ThrowCameraFraming.HorizontalFovDegrees(60f, 0f);
            Assert.IsFalse(float.IsNaN(h));
            Assert.Greater(h, 0f);
        }

        // ---------- lanes-in-frame -> dolly distance ----------

        [Test]
        public void DistanceForLanesInFrame_MatchesTheTrigonometryByHand()
        {
            // Square aspect keeps the arithmetic checkable on paper:
            // half-frame = 1.4 * 3.25 / 2 = 2.275, and tan(30 deg) = 0.57735,
            // so distance = 2.275 / 0.57735 = 3.9404...
            float d = ThrowCameraFraming.DistanceForLanesInFrame(1.4f, 3.25f, 60f, 1f);
            Assert.AreEqual(2.275f / Mathf.Tan(30f * Mathf.Deg2Rad), d, Tolerance);
        }

        [Test]
        public void DistanceForLanesInFrame_GrowsWithMoreLanes()
        {
            float tight = ThrowCameraFraming.DistanceForLanesInFrame(1.4f, 2.5f, 60f, 16f / 9f);
            float wide = ThrowCameraFraming.DistanceForLanesInFrame(1.4f, 4.5f, 60f, 16f / 9f);
            Assert.Greater(wide, tight); // more lanes in shot = stand further back
        }

        [Test]
        public void DistanceForLanesInFrame_ShrinksOnAWiderScreen()
        {
            // A wider screen already shows more, so the same framing needs less
            // distance. This is exactly why the shot is solved rather than
            // hand-authored — a fixed distance would frame differently per monitor.
            float narrow = ThrowCameraFraming.DistanceForLanesInFrame(1.4f, 3.25f, 60f, 4f / 3f);
            float ultrawide = ThrowCameraFraming.DistanceForLanesInFrame(1.4f, 3.25f, 60f, 21f / 9f);
            Assert.Less(ultrawide, narrow);
        }

        [Test]
        public void DistanceForLanesInFrame_ReturnsZeroForDegenerateInput()
        {
            Assert.AreEqual(0f, ThrowCameraFraming.DistanceForLanesInFrame(0f, 3.25f, 60f, 1.777f), Tolerance);
            Assert.AreEqual(0f, ThrowCameraFraming.DistanceForLanesInFrame(1.4f, 0f, 60f, 1.777f), Tolerance);
            Assert.AreEqual(0f, ThrowCameraFraming.DistanceForLanesInFrame(-5f, 3.25f, 60f, 1.777f), Tolerance);
        }

        // ---------- cylindrical (swing / dolly) blend ----------

        [Test]
        public void CylindricalLerp_HitsBothEndpoints()
        {
            Vector3 pivot = new Vector3(0f, 1f, -0.8f);
            Vector3 from = new Vector3(0f, 2.3f, -3.7f);
            Vector3 to = new Vector3(0f, 2f, -2.3f);

            Assert.AreEqual(0f, Vector3.Distance(from, ThrowCameraFraming.CylindricalLerp(from, to, pivot, 0f, 0f)), Tolerance);
            Assert.AreEqual(0f, Vector3.Distance(to, ThrowCameraFraming.CylindricalLerp(from, to, pivot, 1f, 0f)), Tolerance);
        }

        [Test]
        public void CylindricalLerp_KeepsMidpointOffTheChord_WhenSwinging()
        {
            // Swinging from one side of the thrower to the other must travel
            // along an ARC. A straight Vector3.Lerp would cut the chord — which
            // is what used to send the release beat through the thrower's body.
            Vector3 pivot = Vector3.zero;
            Vector3 from = new Vector3(0f, 0f, -4f);
            Vector3 to = new Vector3(4f, 0f, 0f);

            Vector3 arc = ThrowCameraFraming.CylindricalLerp(from, to, pivot, 0.5f, 0f);
            Vector3 chord = Vector3.Lerp(from, to, 0.5f);

            Assert.AreEqual(4f, new Vector2(arc.x, arc.z).magnitude, Tolerance); // radius preserved
            Assert.Greater(new Vector2(arc.x, arc.z).magnitude, new Vector2(chord.x, chord.z).magnitude);
        }

        [Test]
        public void CylindricalLerp_HonoursMinimumRadius()
        {
            // The thrower-clearance floor: the camera may never be pulled closer
            // to the thrower than this, however the beats are tuned.
            Vector3 pivot = Vector3.zero;
            Vector3 from = new Vector3(0f, 0f, -3f);
            Vector3 to = new Vector3(0f, 0f, -0.1f); // would end up inside them

            Vector3 result = ThrowCameraFraming.CylindricalLerp(from, to, pivot, 1f, 0.9f);
            Assert.GreaterOrEqual(new Vector2(result.x, result.z).magnitude, 0.9f - Tolerance);
        }

        // ---------- side-pass blend (beat D) ----------

        [Test]
        public void SidePassLerp_HitsBothEndpointsExactly()
        {
            // The bulge must be ZERO at both ends, or the release beat would no
            // longer start where the previous beat left the camera.
            Vector3 from = new Vector3(0f, 2f, -2.3f);
            Vector3 to = new Vector3(0f, 0.7f, 1.4f);

            Assert.AreEqual(0f, Vector3.Distance(from, ThrowCameraFraming.SidePassLerp(from, to, 0f, 1.2f)), Tolerance);
            Assert.AreEqual(0f, Vector3.Distance(to, ThrowCameraFraming.SidePassLerp(from, to, 1f, 1.2f)), Tolerance);
        }

        [Test]
        public void SidePassLerp_BulgesSidewaysInTheMiddle()
        {
            Vector3 from = new Vector3(0f, 2f, -2.3f);
            Vector3 to = new Vector3(0f, 0.7f, 1.4f);

            Vector3 mid = ThrowCameraFraming.SidePassLerp(from, to, 0.5f, 1.2f);
            Assert.AreEqual(1.2f, mid.x, Tolerance); // widest exactly halfway
        }

        [Test]
        public void SidePassLerp_ClearsThePivotItIsMeantToAvoid()
        {
            // The real-world case: beat C's pose to beat D's pose passes within
            // 0.21m of the thrower's axis as a straight line. With the default
            // bulge it must clear the 0.9m clearance floor instead.
            Vector3 thrower = new Vector3(0f, 1f, -0.8f);
            Vector3 from = new Vector3(0f, 2f, -2.3f);
            Vector3 to = new Vector3(0f, 0.7f, 1.4f);

            float closest = float.MaxValue;
            for (int step = 0; step <= 100; step++)
            {
                Vector3 p = ThrowCameraFraming.SidePassLerp(from, to, step / 100f, 1.2f);
                float flat = new Vector2(p.x - thrower.x, p.z - thrower.z).magnitude;
                if (flat < closest) closest = flat;
            }

            Assert.Greater(closest, 0.9f);
        }

        [Test]
        public void SidePassLerp_WithZeroOffset_IsAPlainStraightLine()
        {
            Vector3 from = new Vector3(0f, 2f, -2.3f);
            Vector3 to = new Vector3(0f, 0.7f, 1.4f);

            Assert.AreEqual(0f, Vector3.Distance(Vector3.Lerp(from, to, 0.37f),
                                                 ThrowCameraFraming.SidePassLerp(from, to, 0.37f, 0f)), Tolerance);
        }

        // ---------- radial clearance ----------

        [Test]
        public void EnforceRadialClearance_LeavesADistantPointAlone()
        {
            Vector3 pivot = Vector3.zero;
            Vector3 far = new Vector3(0f, 1f, 5f);
            Assert.AreEqual(0f, Vector3.Distance(far, ThrowCameraFraming.EnforceRadialClearance(far, pivot, 0.9f)), Tolerance);
        }

        [Test]
        public void EnforceRadialClearance_PushesOutButKeepsHeight()
        {
            Vector3 pivot = Vector3.zero;
            Vector3 tooClose = new Vector3(0.2f, 1.7f, 0f);

            Vector3 result = ThrowCameraFraming.EnforceRadialClearance(tooClose, pivot, 0.9f);

            Assert.AreEqual(0.9f, new Vector2(result.x, result.z).magnitude, Tolerance);
            Assert.AreEqual(1.7f, result.y, Tolerance); // height is never touched
        }

        [Test]
        public void EnforceRadialClearance_HandlesSittingExactlyOnThePivot()
        {
            // No direction to push along — must pick one rather than divide by zero.
            Vector3 result = ThrowCameraFraming.EnforceRadialClearance(Vector3.zero, Vector3.zero, 0.9f);
            Assert.IsFalse(float.IsNaN(result.x));
            Assert.AreEqual(0.9f, new Vector2(result.x, result.z).magnitude, Tolerance);
        }
    }
}
