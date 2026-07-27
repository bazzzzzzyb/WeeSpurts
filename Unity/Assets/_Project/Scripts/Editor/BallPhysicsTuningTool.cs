using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using WeeSpurts.Bowling;

namespace WeeSpurts.Editor
{
    /// <summary>
    /// ONE CLICK re-tunes every BallConfig and the PinConfig to values that are
    /// physically coherent, and prints a before/after table so nothing changes
    /// behind your back.
    ///
    /// Menu: WeeSpurts -> Retune Ball + Pin Physics
    ///
    /// WHY A TOOL AND NOT JUST EDITING THE ASSETS: Unity holds .asset files in
    /// memory while it is open and writes its own copy back on save, so a text
    /// edit made underneath a running editor is liable to be silently discarded.
    /// Going through SerializedObject is the only way to change an asset that is
    /// guaranteed to stick, and it is the same pattern GreyboxSceneBuilder and
    /// RoamingSetupTool already use.
    ///
    /// THIS ONLY TOUCHES NUMBERS THAT WERE WRONG, not numbers that were merely
    /// to taste. Speeds, green zones, timing-chaos curves, the Nuke's staging
    /// and Wobbler's weave are all FEEL, they belong to Tony, and they are left
    /// alone. What gets corrected is values that are internally inconsistent —
    /// a ball wider than the lane, a "cannonball" that weighs a third more than
    /// a normal ball, pins bouncier than the ball that hits them.
    ///
    /// SAFE TO RE-RUN, and safe to ignore: it is a one-shot corrective, not
    /// something any other system depends on.
    /// </summary>
    public static class BallPhysicsTuningTool
    {
        private const string ProjectRoot = "Assets/_Project";
        private const string ConfigFolder = ProjectRoot + "/ScriptableObjects";

        /// <summary>
        /// One field to stamp, with the reasoning attached. Keeping the "why"
        /// next to the number is the whole point — a bare table of floats is
        /// exactly how these drifted out of sync in the first place.
        /// </summary>
        private readonly struct Tweak
        {
            public readonly string Field;
            public readonly float Value;
            public readonly string Reason;

            public Tweak(string field, float value, string reason)
            {
                Field = field; Value = value; Reason = reason;
            }
        }

        [MenuItem("WeeSpurts/Retune Ball + Pin Physics")]
        public static void Retune()
        {
            var log = new StringBuilder();
            log.AppendLine("[Physics] Retuned ball + pin configs. Feel knobs (speeds, green zones, " +
                           "timing curves, Nuke staging, Wobbler weave) were NOT touched.\n");

            // ---------------- the default ball ----------------
            // Reference: a real ten-pin ball is 6.4-7.3kg at 0.1085m radius.
            // Mass 6 is a 13lb ball, which is a legitimate choice and is left
            // alone. Only the radius was broken.
            Apply("BallConfig", log, new[]
            {
                new Tweak("Radius", 0.11f,
                    "was 1 — a 2m-wide ball. Made HalfLaneWidth negative (0.7 - 1 = -0.3), which " +
                    "INVERTED left/right for the thrower, the aim preview and the resolved throw, " +
                    "and also wrecked the roll: Launch sets angularVelocity = speed/Radius, so at " +
                    "Radius 1 the ball span at 14 rad/s instead of 127 and skidded down the lane"),
            });

            // ---------------- BouncyBall ----------------
            // Identity: a rubber superball. Rubber is LIGHT — that is what makes
            // it ping off things. At 6kg (same as the real ball) it had a
            // superball's bounce with a bowling ball's momentum, so it ploughed
            // through the rack instead of caroming around it.
            Apply("BouncyBall", log, new[]
            {
                new Tweak("Mass", 3f,
                    "was 6, identical to the standard ball. A 6kg superball carries so much " +
                    "momentum it barely deflects, which is the opposite of the joke. At 3kg it is " +
                    "~3.75x a pin instead of 7.5x, so it visibly caroms off the rack"),
                new Tweak("RollingDrag", 0.08f,
                    "was 0.12. A light lively ball should keep its energy for the ricochets"),
            });

            // ---------------- Cannonball ----------------
            // Identity: a lump of iron. Dense means HEAVY and SMALL, not big —
            // it was set to a 12-metre radius, which is not a ball, it is a
            // district. Mass 8 also made it barely heavier than the normal ball,
            // so it did not read as a cannonball at all.
            Apply("Cannonball", log, new[]
            {
                new Tweak("Radius", 0.1f,
                    "was 6 — a 12m-wide ball, which also forced HalfLaneWidth to -5.3 and inverted " +
                    "aim. Slightly SMALLER than standard is what reads as dense"),
                new Tweak("Mass", 20f,
                    "was 8, only a third heavier than the standard ball. 20kg is 25x a pin, so it " +
                    "ploughs straight through the rack without deflecting — which IS the powerup. " +
                    "Note this also makes it curve far less for the same spin force (a = F/m), " +
                    "which is correct: you should not be able to bend a cannonball"),
                new Tweak("Bounciness", 0.05f,
                    "was 0.25. Iron does not bounce"),
                new Tweak("RollingDrag", 0.08f,
                    "was 0.12. Heavy things hold their momentum"),
            });

            // ---------------- Wobbler + Nuke ----------------
            // Wobbler is a normal ball with a weave: physically fine, untouched.
            // Nuke's ball physics barely matter (the shot is a scripted tween),
            // so its body is left at the standard ball's values.
            log.AppendLine("Wobbler: no change — physically it is a standard ball, and the weave " +
                           "(WobbleForceMagnitude 12 / WobbleFrequencyHz 0.5) is a feel knob.");
            log.AppendLine("Nuke: no change — the shot is a scripted tween, so the ball body barely " +
                           "participates. Blast values left as tuned.\n");

            // ---------------- pins ----------------
            // Reference: a real ten-pin is 1.53kg, 0.381m tall, and made of
            // maple with a plastic coat — heavy-ish and quite dead.
            ApplyPins(log, new[]
            {
                new Tweak("PinMass", 0.9f,
                    "a real pin is 1.53kg, which against a 6kg ball is a 4:1 ratio — REAL bowling, " +
                    "and it is the thing that felt wrong. Tony's read: Cannonball (20kg, a 17:1 " +
                    "ratio) is the one that feels right. Rather than make every ball a cannonball, " +
                    "lighten the PIN — which is also what this config's own design note has said all " +
                    "along ('lighter pins fly further = funnier'). 0.9 against 6kg is ~7:1, most of " +
                    "the way to Cannonball's punch while keeping enough mass to carry into neighbours"),
                new Tweak("Friction", 0.25f,
                    "was 0.494. THIS IS PROBABLY THE 'SLAMMING INTO THE GROUND' CULPRIT: a pin that " +
                    "lands on a high-friction lane grips and stops dead the instant it touches. Real " +
                    "lanes are OILED and slick, which is why real pins slide, spin and skitter away " +
                    "after they fall. Dropping friction lets them keep travelling instead of " +
                    "arriving and sticking"),
                new Tweak("Bounciness", 0.35f,
                    "was 0.463, briefly 0.25. 0.25 was too dead now that pins are meant to skitter — " +
                    "0.35 lets them hop off the lane and each other without the rubber-ball trampoline " +
                    "that 0.463 gave"),
                new Tweak("CenterOfMassHeight01", 0.38f,
                    "bottom-heavy, like a real pin: it wobbles, sometimes rights itself, and topples " +
                    "about its base rather than pivoting around its middle. NOTE this knob was " +
                    "BROKEN until now — centerOfMass is in local space and the pin transform is " +
                    "scaled 0.19 on Y, so the value was being shrunk to about a fifth of what it " +
                    "said and moving it did almost nothing. Now honest. 0.5 = uniform = old behaviour"),
                new Tweak("BaseDiameter01", 0.45f,
                    "NEW FIELD. Width of the flat pad the pin stands on, as a fraction of pin width, " +
                    "now that the collider is a capsule on a base pad rather than one full-size box. " +
                    "Narrow base = tips readily, like the real thing. Below ~0.3 pins start falling " +
                    "over on their own"),
            });

            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());
        }

        private static void Apply(string assetName, StringBuilder log, IEnumerable<Tweak> tweaks)
        {
            string path = $"{ConfigFolder}/{assetName}.asset";
            var config = AssetDatabase.LoadAssetAtPath<BallConfig>(path);
            if (config == null)
            {
                log.AppendLine($"{assetName}: NOT FOUND at {path} — skipped.\n");
                return;
            }
            Stamp(config, assetName, log, tweaks);
        }

        private static void ApplyPins(StringBuilder log, IEnumerable<Tweak> tweaks)
        {
            string path = $"{ConfigFolder}/PinConfig.asset";
            var config = AssetDatabase.LoadAssetAtPath<PinConfig>(path);
            if (config == null)
            {
                log.AppendLine($"PinConfig: NOT FOUND at {path} — skipped.\n");
                return;
            }
            Stamp(config, "PinConfig", log, tweaks);
        }

        /// <summary>
        /// Writes the values through SerializedObject and reports what actually
        /// changed. A field that is already correct is reported as unchanged
        /// rather than skipped silently, so re-running gives you a clean bill of
        /// health instead of an empty log you have to interpret.
        /// </summary>
        private static void Stamp(Object config, string label, StringBuilder log, IEnumerable<Tweak> tweaks)
        {
            log.AppendLine($"--- {label} ---");

            var so = new SerializedObject(config);
            foreach (Tweak t in tweaks)
            {
                SerializedProperty prop = so.FindProperty(t.Field);
                if (prop == null)
                {
                    log.AppendLine($"  {t.Field}: NO SUCH FIELD — skipped. Was it renamed?");
                    continue;
                }

                float before = prop.floatValue;
                if (Mathf.Approximately(before, t.Value))
                {
                    log.AppendLine($"  {t.Field}: already {t.Value:0.###} — unchanged.");
                    continue;
                }

                prop.floatValue = t.Value;
                log.AppendLine($"  {t.Field}: {before:0.###} -> {t.Value:0.###}");
                log.AppendLine($"      WHY: {t.Reason}.");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);

            log.AppendLine();
        }
    }
}
