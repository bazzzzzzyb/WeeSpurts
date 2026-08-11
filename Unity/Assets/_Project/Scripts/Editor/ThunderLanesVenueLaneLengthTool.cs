using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WeeSpurts.Editor
{
    /// <summary>
    /// Lengthens ALL 8 lanes in ThunderLanesVenue.unity from their shipped
    /// 9.2m foul-line-to-headpin distance up to 18.0m — matching
    /// LaneConfig.Length's default, i.e. the distance TestVenue/BowlingTestbed
    /// (and BallConfig's tuned speed/spin ramp, and ThrowCameraSequence's
    /// beat-F framing, which already reads laneConfig.Length) were built
    /// around. Before this pass the venue's REAL geometry was half that, so
    /// the ball, camera and pin deck were all quietly tuned for a lane twice
    /// as long as the one they were standing on. Run it once (it guards
    /// against being re-run — see the top of ExtendLaneBank).
    ///
    /// EVERYTHING BELOW WAS MEASURED FROM THE LIVE SCENE (Explore-agent
    /// survey, 2026-08-11), not guessed — same rule the rest of the
    /// ThunderLanesVenue*Tool family follows.
    ///
    /// WHAT MOVES, AND WHY:
    ///   - Each lane pair's PIN-SIDE objects (the 10-pin decorative cluster,
    ///     the pinsetter machine model) TRANSLATE by DeltaX. They're a small
    ///     cluster near the old headpin; sliding them keeps their own shape.
    ///   - Each lane's floor/gutter/neon strips STRETCH instead: their
    ///     foul-line-side edge is pinned (so the approach never moves) and
    ///     only the far edge grows by DeltaX, so the lane surface actually
    ///     reaches the new pin position instead of leaving a gap.
    ///   - Foul line, approach deck, ball return, name-entry console, and
    ///     the settee/lounge furniture are ALL west of the foul line —
    ///     genuinely untouched, not just "assumed fine."
    ///
    /// THE SHARED-WALL PROBLEM: Wall_Lane_Pinsetter_MaskingFacade and
    /// Pinsetter_Mechanical_Room sit directly behind the pins and span the
    /// WHOLE 8-lane bank (one object each), so each moves ONCE, whole-bank.
    /// OuterWall_East, though, spans the ENTIRE building depth — it's also
    /// the arcade/party room's east wall, and Tony explicitly asked to leave
    /// that area alone. So it's SPLIT (see SplitOuterEastWall): the original
    /// object is trimmed to just the untouched south remainder, a new
    /// segment covers the lane-bank width and moves with the pinsetter room,
    /// a north remainder stays put, and two short "step" walls close the
    /// notch this opens in the building's east profile — the building gets a
    /// rectangular bump-out behind the lanes, not a gap in its own wall.
    ///
    /// NOT MOVED (flagged, not silently fixed): each lane pair's
    /// Return_Track_Underground ball-return prop still ends at its old x
    /// and will read ~8.8m short of the new pin deck. It's a mostly-hidden
    /// decorative prop under the lane — cosmetic follow-up only if it turns
    /// out to be visible in play. Also not touched: Overhead_Strike_TVs and
    /// Bowler_NameEntry_Console, which are empty unpopulated placeholders in
    /// every lane pair (no children, nothing to move).
    /// </summary>
    public static class ThunderLanesVenueLaneLengthTool
    {
        private const string ScenePath = "Assets/_Project/Scenes/ThunderLanesVenue.unity";

        // 18.0 - 9.2 = 8.8. Chosen so the new foul-to-headpin distance lands
        // exactly on LaneConfig.Length's default — see class comment.
        private const float DeltaX = 8.8f;

        private static readonly string[] LanePairContainerNames =
        {
            "VIP_Lane_Pair_1_2", "Std_Lane_Pair_3_4", "Std_Lane_Pair_5_6", "Std_Lane_Pair_7_8"
        };

        // Per pair: the far (pin-side) edge of these stretches to meet the
        // new pin position; the near (foul-line-side) edge stays fixed.
        private static readonly string[] StretchNamePrefixes =
        {
            "Lane_Bed_", "Gutter_Left_", "Gutter_Right_", "Lane_Neon_Accent"
        };

        [MenuItem("WeeSpurts/Thunder Lanes Venue/Extend Lane Bank (+8.8m)")]
        public static void ExtendLaneBank()
        {
            if (GameObject.Find("OuterWall_East_LaneBank") != null)
            {
                Debug.LogError("[ThunderLanesVenue] 'OuterWall_East_LaneBank' already exists — this tool already " +
                                "ran on this scene. Aborting to avoid duplicating wall geometry. Nothing changed.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"[ThunderLanesVenue] Could not open {ScenePath} — aborting.");
                return;
            }

            int stretched = 0, translated = 0;

            foreach (string containerName in LanePairContainerNames)
            {
                GameObject container = GameObject.Find(containerName);
                if (container == null)
                {
                    Debug.LogWarning($"[ThunderLanesVenue] Lane-pair container '{containerName}' not found — skipped.");
                    continue;
                }

                // Snapshot children first — we're not adding/removing any,
                // but iterating transform.childCount while editing transforms
                // in place is asking for an off-by-one, so copy the list.
                var children = new List<Transform>();
                foreach (Transform child in container.transform) children.Add(child);

                foreach (Transform child in children)
                {
                    string n = child.name;

                    if (n.StartsWith("Pin_") || n.StartsWith("Pinsetter_Machine_"))
                    {
                        child.position += new Vector3(DeltaX, 0f, 0f);
                        translated++;
                        continue;
                    }

                    bool isStretch = false;
                    foreach (string prefix in StretchNamePrefixes)
                    {
                        if (n.StartsWith(prefix)) { isStretch = true; break; }
                    }
                    if (isStretch)
                    {
                        Vector3 pos = child.localPosition;
                        Vector3 scale = child.localScale;
                        pos.x += DeltaX * 0.5f;
                        scale.x += DeltaX;
                        child.localPosition = pos;
                        child.localScale = scale;
                        stretched++;
                    }

                    // Everything else (Foul_Line, Approach_Deck,
                    // Ball_Return_Machine, Bowler_NameEntry_Console,
                    // Overhead_Strike_TVs, all Settee_* furniture) sits west
                    // of the foul line or is an empty placeholder — left alone.
                }

                EditorUtility.SetDirty(container);
            }

            // Pinsetter_Mechanical_Room is the parent of all 4
            // Pinsetter_Mech_Pair_N containers and (transitively) all 4
            // Spare_Pin_Rack instances — one move relocates the whole room.
            MoveByDeltaX("Wall_Lane_Pinsetter_MaskingFacade");
            MoveByDeltaX("Pinsetter_Mechanical_Room");
            MoveByDeltaX("Floor_Pinsetter_BOH_Room");

            SplitOuterEastWall();
            StretchSubfloorEastEdge();
            RepositionGameplayObjects();

            AssetDatabase.SaveAssets();
            bool saved = EditorSceneManager.SaveScene(scene);

            Debug.Log(
                (saved ? $"[ThunderLanesVenue] Lane bank extended by {DeltaX}m and saved {ScenePath}.\n"
                       : $"[ThunderLanesVenue] Lane bank extended but FAILED TO SAVE {ScenePath}.\n") +
                $"Stretched {stretched} lane-surface strips, translated {translated} pin/pinsetter-machine objects " +
                "across all 4 lane pairs.\n" +
                "Lane 5's real foul-line-to-headpin distance is now 18.0m, matching LaneConfig.Length's default — " +
                "ThrowCameraSequence's beat framing and BallConfig's tuned speed/spin ramp (both authored against " +
                "18m) should now match the venue instead of fighting a lane half that long.\n" +
                "OuterWall_East split into OuterWall_East_South (untouched, party/arcade side), " +
                "OuterWall_East_North (untouched) and OuterWall_East_LaneBank (moved +8.8m), with " +
                "OuterWall_Step_South/North closing the notch this opens at the two z seams. Arcade and party " +
                "room geometry were not touched.\n" +
                "NOT moved (flagged, not fixed): each lane pair's Return_Track_Underground ball-return prop still " +
                "ends at its old position and will look ~8.8m short of the new pin deck — cosmetic, follow up only " +
                "if it's visible in play.\n" +
                "NOW TEST: press Play, walk to the lane 5 kiosk, bowl a frame. The ball should travel noticeably " +
                "further before reaching the pins, hook/wobble should have more room to develop, and all 7 camera " +
                "beats should still frame correctly. Also worth a walk down the concourse past the old pinsetter-" +
                "wall line (x≈24-33) to check nothing floats or clips at the new step walls.");
        }

        private static void MoveByDeltaX(string name)
        {
            GameObject go = GameObject.Find(name);
            if (go == null)
            {
                Debug.LogWarning($"[ThunderLanesVenue] '{name}' not found — skipped.");
                return;
            }
            go.transform.position += new Vector3(DeltaX, 0f, 0f);
            EditorUtility.SetDirty(go);
        }

        /// <summary>
        /// OuterWall_East (measured: local {30.2, 2.75, 0}, scale {0.4, 5.5,
        /// 80.4}) spans the WHOLE building depth (z about -40.2 to +40.2),
        /// not just the 8-lane bank (z 0-30, the same span
        /// Wall_Lane_Pinsetter_MaskingFacade already uses). Moving it whole
        /// would strand the arcade/party wing's own east wall 8.8m from
        /// everything else in that room. Instead: trim the original down to
        /// the untouched south remainder, add an untouched north remainder,
        /// add a lane-bank segment that moves with the pinsetter room, and
        /// close the resulting notch with two short "step" walls at the two
        /// z seams (z=0, z=30) so the building's east profile reads as a
        /// clean rectangular bump-out, not a hole.
        /// </summary>
        private static void SplitOuterEastWall()
        {
            GameObject original = GameObject.Find("OuterWall_East");
            if (original == null)
            {
                Debug.LogWarning("[ThunderLanesVenue] 'OuterWall_East' not found — outer wall not split. " +
                                  "The building's east profile will have a hole behind the extended lanes.");
                return;
            }

            const float wallX = 30.2f;
            const float wallY = 2.75f;
            const float wallHeight = 5.5f;
            const float wallThickness = 0.4f;
            float stepCenterX = wallX + DeltaX * 0.5f;

            GameObject north = Object.Instantiate(original, original.transform.parent);
            north.name = "OuterWall_East_North";
            north.transform.position = new Vector3(wallX, wallY, 35.1f);
            north.transform.localScale = new Vector3(wallThickness, wallHeight, 10.2f);

            GameObject laneBank = Object.Instantiate(original, original.transform.parent);
            laneBank.name = "OuterWall_East_LaneBank";
            laneBank.transform.position = new Vector3(wallX + DeltaX, wallY, 15f);
            laneBank.transform.localScale = new Vector3(wallThickness, wallHeight, 30f);

            GameObject stepSouth = Object.Instantiate(original, original.transform.parent);
            stepSouth.name = "OuterWall_Step_South";
            stepSouth.transform.position = new Vector3(stepCenterX, wallY, 0f);
            stepSouth.transform.localScale = new Vector3(DeltaX, wallHeight, wallThickness);

            GameObject stepNorth = Object.Instantiate(original, original.transform.parent);
            stepNorth.name = "OuterWall_Step_North";
            stepNorth.transform.position = new Vector3(stepCenterX, wallY, 30f);
            stepNorth.transform.localScale = new Vector3(DeltaX, wallHeight, wallThickness);

            // Trim the original in place last, after cloning from it, so it
            // becomes the (untouched) south remainder.
            original.name = "OuterWall_East_South";
            original.transform.position = new Vector3(wallX, wallY, -20.1f);
            original.transform.localScale = new Vector3(wallThickness, wallHeight, 40.2f);

            EditorUtility.SetDirty(original);
            EditorUtility.SetDirty(north);
            EditorUtility.SetDirty(laneBank);
            EditorUtility.SetDirty(stepSouth);
            EditorUtility.SetDirty(stepNorth);
        }

        /// <summary>
        /// Subfloor_Base is the single foundation slab under the entire
        /// building (measured: local {0, -0.1, 0}, scale {60.8, 0.2, 80.8} —
        /// spans x -30.4 to +30.4). Its west edge underlies rooms we're not
        /// touching, so it's pinned; only the east edge needs to reach far
        /// enough to stay under the relocated pinsetter room and new wall.
        /// Extending it slightly further than strictly needed under the
        /// (untouched) arcade/party wing is harmless — it's a below-floor
        /// slab, not a visible surface.
        /// </summary>
        private static void StretchSubfloorEastEdge()
        {
            GameObject subfloor = GameObject.Find("Subfloor_Base");
            if (subfloor == null)
            {
                Debug.LogWarning("[ThunderLanesVenue] 'Subfloor_Base' not found — foundation slab not extended.");
                return;
            }
            Vector3 pos = subfloor.transform.position;
            Vector3 scale = subfloor.transform.localScale;
            pos.x += DeltaX * 0.5f;
            scale.x += DeltaX;
            subfloor.transform.position = pos;
            subfloor.transform.localScale = scale;
            EditorUtility.SetDirty(subfloor);
        }

        /// <summary>
        /// The REAL physics rig on lane 5 — built by
        /// ThunderLanesVenueBowlingSetupTool.BuildAndWire, not art. PinDeck
        /// translates with the rest of the pin end; Lane5_Rails' two boxes
        /// stretch the same way the lane-bed/gutter art strips do (near edge
        /// pinned, far edge grows) since they're built from a start/end
        /// down-lane pair, not a fixed length.
        /// </summary>
        private static void RepositionGameplayObjects()
        {
            GameObject pinDeck = GameObject.Find("PinDeck");
            if (pinDeck != null)
            {
                pinDeck.transform.position += new Vector3(DeltaX, 0f, 0f);
                EditorUtility.SetDirty(pinDeck);
            }
            else
            {
                Debug.LogWarning("[ThunderLanesVenue] 'PinDeck' not found — real pin rig not repositioned. " +
                                  "Run ThunderLanesVenueBowlingSetupTool.BuildAndWire first if lane 5 isn't wired yet.");
            }

            GameObject rails = GameObject.Find("Lane5_Rails");
            if (rails == null)
            {
                Debug.LogWarning("[ThunderLanesVenue] 'Lane5_Rails' not found — invisible rails not extended.");
                return;
            }

            foreach (Transform rail in rails.transform) // RailLeft, RailRight
            {
                Vector3 pos = rail.position;
                Vector3 scale = rail.localScale;
                pos.x += DeltaX * 0.5f;
                scale.z += DeltaX; // local Z is the rail's length axis (LaneRotation rotates it onto world +X — see ThunderLanesVenueBowlingSetupTool's own comment on that identity)
                rail.position = pos;
                rail.localScale = scale;
                EditorUtility.SetDirty(rail.gameObject);
            }
        }
    }
}
