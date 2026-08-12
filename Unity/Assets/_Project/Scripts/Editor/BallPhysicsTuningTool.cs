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
    /// Menu: WeeSpurts -> 1 Assets -> Retune Ball + Pin Physics
    ///
    /// WHY A TOOL AND NOT JUST EDITING THE ASSETS: Unity holds .asset files in
    /// memory while it is open and writes its own copy back on save, so a text
    /// edit made underneath a running editor is liable to be silently discarded.
    /// Going through SerializedObject is the only way to change an asset that is
    /// guaranteed to stick, and it is the same pattern GreyboxSceneBuilder and
    /// RoamingSetupTool already use.
    ///
    /// SCOPE: the standard ball (BallConfig) and the pins (PinConfig) are
    /// tuned toward REAL bowling physics as of the 2026-08-12 pass — mass,
    /// dimensions, restitution and friction now target actual USBC
    /// regulation numbers, on Tony's explicit direction, superseding the
    /// 2026-07-26 "realistic ratios read as limp" call recorded in those two
    /// Tweak blocks' comments. The powerup balls (BouncyBall, Cannonball,
    /// Wobbler, Nuke) are DELIBERATELY NOT realistic — a superball, a lump of
    /// iron and a snake-curving ball are jokes by design — and are left as
    /// pure feel, untouched by this pass. Speeds, green zones, timing-chaos
    /// curves, the Nuke's staging and Wobbler's weave stay FEEL everywhere,
    /// realism pass or not, and are left alone.
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

        [MenuItem("WeeSpurts/1 Assets/Retune Ball + Pin Physics")]
        public static void Retune()
        {
            var log = new StringBuilder();
            log.AppendLine("[Physics] Retuned ball + pin configs. Feel knobs (speeds, green zones, " +
                           "timing curves, Nuke staging, Wobbler weave) were NOT touched.\n");

            // ---------------- the default ball ----------------
            // REALISM PASS (2026-08-12, Tony's explicit call, overriding the
            // 2026-07-26 "Cannonball ratio feels better" decision below on the
            // pins — see PinConfig's own Tweak block for the other half of
            // this). Reference: USBC regulation — ball diameter 8.500-8.595in
            // (radius ~0.108-0.109m), max weight 16lb (7.26kg). "Real bowling
            // balls are ~7" was already this file's own field tooltip; this
            // pass just actually gets there instead of sitting at a 13lb ball.
            Apply("BallConfig", log, new[]
            {
                new Tweak("Radius", 0.108f,
                    "was 0.11 — close, but 0.108 is the exact regulation radius (8.5in diameter / 2), " +
                    "and Launch's angularVelocity = speed/Radius makes this worth getting precise"),
                new Tweak("Mass", 7.26f,
                    "was 6 (13lb) — a deliberate lighter-than-real choice from 2026-07-26 (see PinConfig's " +
                    "own note on why realistic ratios 'read as limp'). Tony's overriding that today: 7.26kg " +
                    "is the actual USBC max regulation weight (16lb), and the field tooltip already claimed " +
                    "this number — it just wasn't applied. Pins are getting realistically heavier alongside " +
                    "this (see PinConfig), so the RATIO changes together rather than the ball alone getting " +
                    "heavier against unchanged light pins, which is exactly the 'read as limp' failure mode"),
                new Tweak("Bounciness", 0.3f,
                    "was 0.25, briefly 0.5. 0.5 shipped once, live, and — because bounceCombine=Maximum " +
                    "means THIS value wins every ball-pin contact regardless of what PinConfig says — it " +
                    "made collisions bounce the ball rather than shove the pin, which combined with the " +
                    "pin-side changes to produce pins that wobble and right themselves instead of falling " +
                    "('mighty beans,' caught live in Play). 0.3 favours transferring the hit into the pin"),
                new Tweak("RollingDrag", 0.06f,
                    "was 0.12. Regulation lanes are oiled/waxed specifically to be low-friction — a real " +
                    "ball loses only a modest fraction of its speed over 60 feet. Halving the drag keeps " +
                    "more of the ball's momentum through to the pins, closer to a real lane's slickness"),
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
            // REALISM PASS (2026-08-12, Tony's explicit call, overriding the
            // 2026-07-26 tweaks directly below this comment, which were an
            // intentional departure from real bowling because the realistic
            // ratio "read as limp" at the time. That call is superseded, not
            // deleted — the old Tweak entries stay in the log/history as the
            // record of what was true then. Reference: USBC regulation — pin
            // height 15in (0.381m, already correct), weight 3lb 6oz-3lb 10oz
            // (1.531-1.644kg, centre ~1.588kg), and centre-of-gravity spec'd
            // between roughly 5.99-6.25in from the base of a 15in pin (a
            // 0.40-0.417 fraction of height). Ball went back to a real 7.26kg
            // in the same pass (see BallConfig above) — the mass RATIO is
            // what actually reads as real, so both sides move together.
            // CORRECTION, same session: the block above shipped once, live, and
            // produced pins that wobble and RIGHT THEMSELVES instead of falling
            // ("mighty beans" — Tony, watching it in Play). That's the textbook
            // signature of a body that's too stable to topple: a wider base
            // extends the tilt angle before its centre of mass crosses the edge
            // of support and gravity takes over, a heavier pin needs more
            // momentum to get there in the first place, and a more elastic
            // collision (bounceCombine=Maximum, so the BALL's bounciness wins
            // every contact) puts more of the hit into rebound and less into
            // actually shoving the pin past that point. All three numbers above
            // pushed the same direction at once. This pass pulls them back —
            // not all the way to the pre-realism values, but past the point
            // where pins reliably go down instead of settling for "physically
            // closer to real" over "is a bowling game."
            ApplyPins(log, new[]
            {
                new Tweak("PinMass", 1.1f,
                    "was 1.59 (USBC's literal spec, and the direct cause of pins resisting the hit — " +
                    "more mass needs more momentum to accelerate past its tipping point). 1.1kg against " +
                    "the ball's 7.26kg is a 6.6:1 ratio, close to the 6.67:1 (6kg/0.9kg) this project " +
                    "already knew worked, just carried by a heavier, more realistic ball instead of a " +
                    "light one — realism where it does not fight the knockdown, not everywhere"),
                new Tweak("Bounciness", 0.3f,
                    "was 0.6, and combined with the ball's own 0.5 (bounceCombine=Maximum picks the " +
                    "larger every time) that was a genuinely bouncy collision — energy spent rebounding " +
                    "the ball is energy NOT spent shoving the pin over. 0.3 favours transferring the hit " +
                    "into the pin rather than into an elastic bounce"),
                new Tweak("BaseDiameter01", 0.32f,
                    "was 0.55 — THE main culprit. A wider base means the pin can lean further before its " +
                    "centre of mass passes the edge of its own footprint and gravity finishes the topple; " +
                    "0.55 was wide enough that a solid hit could wobble it right back upright, the exact " +
                    "'mighty bean' effect. 0.32 sits just above this file's own documented self-toppling " +
                    "floor (~0.3) — narrow enough that a real hit reliably carries through"),
                new Tweak("CenterOfMassHeight01", 0.45f,
                    "was 0.4 (and 0.38 before that) — moving further from bottom-heavy, on purpose this " +
                    "time. Bottom-heavy is exactly what gives a weeble/mighty-bean toy its self-righting " +
                    "torque; less of it means less of that effect fighting a knockdown, while 0.45 is " +
                    "still on the bottom-heavy side of uniform (0.5), not neutral"),
                new Tweak("Friction", 0.3f,
                    "unchanged from the previous pass — not implicated in the self-righting symptom, " +
                    "still a reasonable middle value versus the 0.494 that made pins stick dead on " +
                    "landing before any of this"),
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
