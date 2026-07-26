using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WeeSpurts.Bowling;
using WeeSpurts.Environment;

namespace WeeSpurts.Editor
{
    /// <summary>
    /// Builds the VENUE around the bowling lanes as flat greybox boxes.
    ///
    /// Menu:
    ///   WeeSpurts -> Build Alley Venue Greybox (Standalone Scene)   [look first]
    ///   WeeSpurts -> Build Alley Venue Greybox                      [current scene]
    ///
    /// LAYOUT v2 — A BANDED, WALKABLE SOCIAL FLOOR. v1 was a room with props round
    /// the edges. v2 is a stack of Z bands you walk through, west to east:
    ///
    ///   west band   — service spine (bar / snack bar / front desk on ONE
    ///                 back-of-house), casino nook, card dealer alcove
    ///   concourse   — the primary route, full room width
    ///   pit band    — six sunken settee pits, one per lane pair
    ///   return band — six paired ball returns at the head of the pits
    ///   approach    — COMPLETELY CLEAR, because the throw camera lives here
    ///
    /// SCOPE: set dressing only. This script never touches GreyboxSceneBuilder,
    /// the lane, the pins, the ball, or any gameplay component. Everything it
    /// creates lives under ONE root object called "AlleyGreybox", so the whole
    /// venue can be toggled off with a single tickbox, and a re-run REBUILDS that
    /// root from scratch rather than merging into a stale previous run.
    ///
    /// COORDINATE FRAME: see AlleyLayoutConfig's doc comment. Short version —
    /// origin is the foul line on the playable lane's centreline, +Z runs
    /// down-lane to the pins, y = 0 is the floor, and x = 0 is the PLAYABLE lane
    /// rather than the middle of the room.
    ///
    /// SAFETY IS ENFORCED IN CODE, NOT BY DISCIPLINE: see AuditColliders (three
    /// checks) and AlleyCirculationAudit (the walkable-floor flood fill).
    /// </summary>
    public static class AlleyGreyboxBuilder
    {
        private const string ProjectRoot = "Assets/_Project";
        private const string LayoutAssetPath = ProjectRoot + "/ScriptableObjects/AlleyLayout.asset";
        private const string StandaloneScenePath = ProjectRoot + "/Scenes/AlleyVenueGreybox.unity";
        private const string RootName = "AlleyGreybox";

        // GreyboxSceneBuilder's real lane geometry, needed in two places: to draw
        // the stand-in lane in the standalone scene, and to work out where the
        // real lane's chunky rails would collide with a slim backdrop rail.
        // Mirrored as constants rather than shared, because this script is not
        // allowed to modify GreyboxSceneBuilder to expose them.
        private const float RealRailCentreOffset = 0.55f;   // beyond the lane's edge
        private const float RealRailHalfThickness = 0.125f;
        private const float RealRailHeight = 0.35f;

        // ThrowCamera.ConfigureAimView's Z, mirrored for the same reason. The
        // approach band has to be deeper than this or the venue occludes the aim.
        private const float KnownAimCameraZ = -3.7f;

        // Bounds.Intersects returns TRUE for merely TOUCHING bounds, and the bands
        // share exact edges by construction — a ball return's east face IS the
        // approach band's west edge. Without this every return would false-positive
        // the approach check on the first run. The audit VOLUMES shrink by it; the
        // geometry is never nudged to make a check pass.
        private const float AuditEpsilon = 0.002f;

        // Every collider created this run, so the run can assert none of them
        // landed anywhere they can hurt.
        private static readonly List<Collider> CreatedColliders = new List<Collider>();

        // The same colliders split by what the box IS, declared at creation time.
        // The circulation audit needs to know which boxes are ground and which are
        // things you bump into, and it must not guess that from height: a chair
        // seat and a step tread are the same height, and only the builder knows
        // which is which.
        private static readonly List<Collider> WalkableColliders = new List<Collider>();
        private static readonly List<Collider> ObstacleColliders = new List<Collider>();

        // Renderers and anchors too, because the approach-band check fails on ANY
        // created object, not only solid ones — a cosmetic box in the aim camera's
        // face is just as bad as a wall.
        private static readonly List<Renderer> CreatedRenderers = new List<Renderer>();
        private static readonly List<Transform> CreatedAnchors = new List<Transform>();

        // Declared circulation semantics. Tony was explicit that a gap's category
        // must never be inferred from its shape, so the builder — which knows a pit
        // gap is a pit entrance and a doorway is a doorway — says so as it builds.
        private static readonly List<RouteZone> RouteZones = new List<RouteZone>();
        private static readonly List<FurniturePiece> FurniturePieces = new List<FurniturePiece>();

        // ------------------------------------------------------------ menu items

        [MenuItem("WeeSpurts/Build Alley Venue Greybox (Standalone Scene)")]
        public static void BuildStandaloneScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // The real lane does not exist in this scene, so draw a cosmetic
            // stand-in for it. That is the whole point of the standalone scene:
            // you can see whether the venue lines up with where the lane WILL be,
            // with zero risk to BowlingAlley.unity.
            BuildVenue(includePlayableLaneStandIn: true);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.AddComponent<Camera>();
            // Parked high above the main floor looking down-lane, so the scene
            // opens on a readable view of the whole venue instead of inside a wall.
            camGo.transform.position = new Vector3(2f, 14f, -26f);
            camGo.transform.rotation = Quaternion.Euler(32f, 0f, 0f);

            AssetDatabase.SaveAssets();
            EnsureFolder(ProjectRoot + "/Scenes");
            EditorSceneManager.SaveScene(scene, StandaloneScenePath);

            // Deliberately NOT added to EditorBuildSettings: GreyboxSceneBuilder
            // owns that list and overwrites it wholesale, so touching it here
            // would just start a fight over which scene ships.
            Debug.Log("WeeSpurts: Alley venue greybox built and saved to " + StandaloneScenePath +
                      ". This scene is set dressing only — no lane, pins or ball logic.");
        }

        [MenuItem("WeeSpurts/Build Alley Venue Greybox")]
        public static void BuildIntoCurrentScene()
        {
            Scene scene = SceneManager.GetActiveScene();

            // The real lane is already here, so no stand-in.
            BuildVenue(includePlayableLaneStandIn: false);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);

            // NOT auto-saved on purpose. This drops ~350 objects into whatever
            // scene is open; you get to look at it and decide before it is
            // written to disk. Ctrl+S to keep, Ctrl+Z / reopen to discard.
            Debug.Log("WeeSpurts: Alley venue greybox built into '" + scene.name +
                      "' under the '" + RootName + "' root. The scene is DIRTY and NOT saved — " +
                      "look at it first, then save if you want to keep it.");
        }

        // --------------------------------------------------------------- build

        private static void BuildVenue(bool includePlayableLaneStandIn)
        {
            CreatedColliders.Clear();
            WalkableColliders.Clear();
            ObstacleColliders.Clear();
            CreatedRenderers.Clear();
            CreatedAnchors.Clear();
            RouteZones.Clear();
            FurniturePieces.Clear();

            AlleyLayoutConfig layout = LoadOrCreateAsset<AlleyLayoutConfig>(LayoutAssetPath);
            LaneConfig laneConfig = LoadOrCreateAsset<LaneConfig>(ProjectRoot + "/ScriptableObjects/LaneConfig.asset");

            RetireExistingGeometry();

            var root = new GameObject(RootName);
            var anchors = new GameObject("Anchors");
            anchors.transform.SetParent(root.transform);

            Materials mats = LoadMaterials();

            LogBandTable(layout);
            ValidateBands(layout);

            BuildShell(root, layout, mats);
            BuildLaneBank(root, layout, laneConfig, mats, includePlayableLaneStandIn);
            BuildSetteePits(root, anchors, layout, mats);
            BuildBallReturns(root, layout, mats);
            BuildCasino(root, anchors, layout, mats);
            BuildCardDealer(root, anchors, layout, mats);
            BuildServiceSpine(root, anchors, layout, mats);
            BuildTables(root, layout, mats);
            BuildCosmetics(root, anchors, layout, mats);
            BuildEntrance(root, anchors, layout, mats);

            RegisterRouteZones(layout);

            AuditColliders(layout);

            if (layout.RunCirculationAudit)
            {
                AlleyCirculationAudit.Run(layout, WalkableColliders, ObstacleColliders,
                                          RouteZones, FurniturePieces, CreatedAnchors, root);
            }
        }

        /// <summary>
        /// Clears the way for a clean rebuild. Two separate jobs:
        ///
        /// (1) Destroy any previous AlleyGreybox root. A re-run must REBUILD, not
        ///     merge — otherwise moving a wall in the layout config leaves the old
        ///     wall behind and you slowly accumulate a haunted double venue.
        ///
        /// (2) Destroy GreyboxSceneBuilder's "NeighbourLanes" root if it is here.
        ///     That builder spawns 4 cosmetic lanes for camera framing; this script
        ///     spawns the whole bank, and the two sets overlap and z-fight. This
        ///     script owns the backdrop now. GreyboxSceneBuilder itself is NOT
        ///     modified — LaneConfig already has a BuildNeighbourLanes tickbox for
        ///     exactly this, so the permanent fix is one untick in the asset.
        /// </summary>
        private static void RetireExistingGeometry()
        {
            foreach (GameObject go in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (go.name == RootName)
                {
                    Object.DestroyImmediate(go);
                }
                else if (go.name == "NeighbourLanes")
                {
                    Object.DestroyImmediate(go);
                    Debug.LogWarning("[AlleyGreybox] Removed GreyboxSceneBuilder's 'NeighbourLanes' root — the venue " +
                                     "builds the whole lane bank itself and the two would overlap. Untick " +
                                     "LaneConfig.BuildNeighbourLanes so rebuilding the bowling scene does not bring " +
                                     "them back.");
                }
            }
        }

        // ----------------------------------------------------------- band table

        /// <summary>Prints the derived band stack every build. The depths are the
        /// knobs; these Z values fall out of them, and seeing them written down is
        /// how you catch a band you did not mean to move.</summary>
        private static void LogBandTable(AlleyLayoutConfig L)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== ALLEY BAND TABLE (Z values derived from the depth fields) ===");
            sb.AppendLine("  band                  z from     z to     depth");
            sb.AppendLine("  lane surface           " + Col(0f) + "   " + Col(L.RoomZMax) + "    (east of the foul line)");
            sb.AppendLine("  APPROACH (clear)       " + Col(L.ApproachZMin) + "   " + Col(L.ApproachZMax) + "   " + Col(L.ApproachClearDepth));
            sb.AppendLine("  ball returns           " + Col(L.BallReturnZMin) + "   " + Col(L.BallReturnZMax) + "   " + Col(L.BallReturnBandDepth));
            sb.AppendLine("  settee pits            " + Col(L.SetteePitZMin) + "   " + Col(L.SetteePitZMax) + "   " + Col(L.SetteePitDepth));
            sb.AppendLine("  CONCOURSE              " + Col(L.ConcourseZMin) + "   " + Col(L.ConcourseZMax) + "   " + Col(L.ConcourseDepth));
            sb.AppendLine("  west band              " + Col(L.WestBandZMin) + "   " + Col(L.WestBandZMax) + "   " + Col(L.WestBandDepth));
            sb.AppendLine("    back shelving        " + Col(L.ServiceBackShelfZMin) + "   " + Col(L.ServiceBackShelfZMax) + "   " + Col(L.ServiceBackShelfDepth));
            sb.AppendLine("    staff walkway        " + Col(L.ServiceBackShelfZMax) + "   " + Col(L.ServiceCounterZMin) + "   " + Col(L.ServiceStaffWalkwayDepth));
            sb.AppendLine("    counters             " + Col(L.ServiceCounterZMin) + "   " + Col(L.ServiceCounterZMax) + "   " + Col(L.CounterDepth));
            sb.AppendLine("    lobby floor          " + Col(L.LobbyZMin) + "   " + Col(L.LobbyZMax) + "   " + Col(L.LobbyZMax - L.LobbyZMin));
            sb.AppendLine();
            sb.AppendLine("  band stack total       " + (L.ApproachClearDepth + L.BallReturnBandDepth + L.SetteePitDepth +
                                                         L.ConcourseDepth + L.WestBandDepth).ToString("0.00") + "m");
            sb.AppendLine("  west wall (RoomZMin)   " + L.RoomZMin.ToString("0.00") +
                          "     unused west padding " + (L.WestBandZMin - L.RoomZMin).ToString("0.00") + "m");
            sb.AppendLine("  room  x [" + L.RoomXMin.ToString("0.0") + ", " + L.RoomXMax.ToString("0.0") + "] = " +
                          (L.RoomXMax - L.RoomXMin).ToString("0.0") + "m     z [" + L.RoomZMin.ToString("0.0") + ", " +
                          L.RoomZMax.ToString("0.0") + "] = " + (L.RoomZMax - L.RoomZMin).ToString("0.0") + "m");
            sb.AppendLine("  lane pairs: " + L.LanePairCount + " at pair pitch " + (L.LanePitch * 2f).ToString("0.00") +
                          "m; pit width " + L.SetteePitWidth.ToString("0.00") + "m leaves " +
                          (L.LanePitch * 2f - L.SetteePitWidth).ToString("0.00") +
                          "m between adjacent pits (declared THRESHOLD, min " +
                          L.CirculationThresholdMinWidth.ToString("0.00") + "m).");
            Debug.Log(sb.ToString());
        }

        private static string Col(float v)
        {
            return v.ToString("0.00").PadLeft(7);
        }

        private static void ValidateBands(AlleyLayoutConfig L)
        {
            float needed = Mathf.Abs(KnownAimCameraZ) + L.ApproachCameraMargin;
            if (L.ApproachClearDepth < needed)
            {
                Debug.LogError("[AlleyGreybox] ApproachClearDepth is " + L.ApproachClearDepth.ToString("0.00") +
                               "m but the aim camera sits at z = " + KnownAimCameraZ + " and needs " +
                               needed.ToString("0.00") + "m of clear band (including ApproachCameraMargin). The venue " +
                               "would occlude every throw. Raise ApproachClearDepth.");
            }

            if (L.WestBandZMin < L.RoomZMin - 0.001f)
            {
                Debug.LogError("[AlleyGreybox] The band stack reaches z = " + L.WestBandZMin.ToString("0.00") +
                               " but the west wall is at " + L.RoomZMin.ToString("0.00") +
                               ". The bands do not fit inside the room — move RoomZMin west or shrink a band depth.");
            }

            float spineDepth = L.ServiceCounterZMax - L.WallInnerZ;
            float usableWest = L.WestBandZMax - L.WallInnerZ;
            if (spineDepth > usableWest)
            {
                Debug.LogError("[AlleyGreybox] The service spine needs " + spineDepth.ToString("0.00") +
                               "m but the west band only offers " + usableWest.ToString("0.00") +
                               "m inside the wall. Raise WestBandDepth.");
            }

            if (L.LaneCount % 2 != 0)
            {
                Debug.LogWarning("[AlleyGreybox] LaneCount " + L.LaneCount + " is odd, so lane " + L.LaneCount +
                                 " gets no settee pit and no ball return (both are built per PAIR).");
            }
        }

        // ---------------------------------------------------------------- shell

        private static void BuildShell(GameObject root, AlleyLayoutConfig L, Materials mats)
        {
            var shell = Group(root, "Shell");

            float t = L.WallThickness;
            float half = t * 0.5f;

            BuildFloor(shell, L, mats);

            // Walls straddle the room bounds. North / east / west are single runs.
            Slab(shell, "WallNorth", L.RoomXMin - half, L.RoomXMin + half,
                 L.RoomZMin - half, L.RoomZMax + half, 0f, L.WallHeight, mats.Wall, true);
            Slab(shell, "WallWest", L.RoomXMin - half, L.RoomXMax + half,
                 L.RoomZMin - half, L.RoomZMin + half, 0f, L.WallHeight, mats.Wall, true);
            Slab(shell, "WallEast", L.RoomXMin - half, L.RoomXMax + half,
                 L.RoomZMax - half, L.RoomZMax + half, 0f, L.WallHeight, mats.Wall, true);

            // The south wall is split around the entrance doorway.
            Slab(shell, "WallSouthWest", L.RoomXMax - half, L.RoomXMax + half,
                 L.RoomZMin - half, L.EntranceZMin, 0f, L.WallHeight, mats.Wall, true);
            Slab(shell, "WallSouthEast", L.RoomXMax - half, L.RoomXMax + half,
                 L.EntranceZMax, L.RoomZMax + half, 0f, L.WallHeight, mats.Wall, true);
            // Lintel over the doorway, so the gap reads as a door and not a hole.
            Slab(shell, "WallSouthLintel", L.RoomXMax - half, L.RoomXMax + half,
                 L.EntranceZMin, L.EntranceZMax, L.DoorHeight, L.WallHeight - L.DoorHeight, mats.Wall, true);
        }

        /// <summary>
        /// THE FLOOR IS TILED, NOT HOLED. A settee pit sits 0.4m below the walking
        /// surface, and you cannot cut a hole in a box — so instead of one slab
        /// under the whole room, the pit band's strip is split into the gaps
        /// BETWEEN the pits and each pit lays its own floor 0.4m lower. Nine slabs
        /// at the defaults instead of one.
        ///
        /// The tempting alternative — leave the big slab and drop a pit floor under
        /// it — puts solid geometry over a character's head while they sit in the
        /// pit, because the main slab's underside is only 0.2m down.
        /// </summary>
        private static void BuildFloor(GameObject shell, AlleyLayoutConfig L, Materials mats)
        {
            float half = L.WallThickness * 0.5f;
            float x0 = L.RoomXMin - half, x1 = L.RoomXMax + half;
            float z0 = L.RoomZMin - half, z1 = L.RoomZMax + half;
            float yBase = L.FloorTopY - 0.2f;

            int pairs = L.BuildSetteePits ? L.LanePairCount : 0;
            if (pairs <= 0)
            {
                Slab(shell, "Floor", x0, x1, z0, z1, yBase, 0.2f, mats.Floor, true, walkable: true);
                return;
            }

            float pz0 = L.SetteePitZMin, pz1 = L.SetteePitZMax;

            Slab(shell, "Floor_West", x0, x1, z0, pz0, yBase, 0.2f, mats.Floor, true, walkable: true);
            Slab(shell, "Floor_East", x0, x1, pz1, z1, yBase, 0.2f, mats.Floor, true, walkable: true);

            // Fill the pit band's X gaps, leaving each pit footprint empty.
            float cursor = x0;
            for (int p = 0; p < pairs; p++)
            {
                float b = L.LanePairBoundaryX(p);
                float pxMin = b - L.SetteePitWidth * 0.5f;
                float pxMax = b + L.SetteePitWidth * 0.5f;

                if (pxMin > cursor + 0.01f)
                    Slab(shell, "Floor_PitBandGap" + p.ToString("00"), cursor, pxMin, pz0, pz1,
                         yBase, 0.2f, mats.Floor, true, walkable: true);

                cursor = Mathf.Max(cursor, pxMax);
            }
            if (x1 > cursor + 0.01f)
                Slab(shell, "Floor_PitBandGapEnd", cursor, x1, pz0, pz1, yBase, 0.2f, mats.Floor, true, walkable: true);
        }

        // ------------------------------------------------------------ lane bank

        private static void BuildLaneBank(GameObject root, AlleyLayoutConfig L,
                                          LaneConfig laneConfig, Materials mats, bool includePlayableLaneStandIn)
        {
            var bank = Group(root, "LaneBank");

            float width = laneConfig.Width;
            float length = laneConfig.Length;

            // Match GreyboxSceneBuilder's real lane slab exactly (centre z = length/2,
            // scale z = length + 1.5, centre y = -0.05, scale y = 0.102) so the
            // backdrop lanes sit flush with the real one.
            float laneZMin = -0.75f;
            float laneZMax = length + 0.75f;
            const float LaneSurfaceTop = 0.001f;
            const float LaneSurfaceThickness = 0.102f;

            // Slim backdrop gutter rails, hugging the lane edge instead of the real
            // lane's 0.55m stand-off. This is what lets LanePitch go down to ~2m.
            float railCentre = width * 0.5f + L.BackdropGutterWidth * 0.5f;
            float railHalf = L.BackdropGutterWidth * 0.5f * 0.92f;   // small gap so neighbouring rails never touch

            // The real lane's rails are much wider than a backdrop lane's, so at a
            // tight pitch they spill into the adjacent slots. Any backdrop rail that
            // would intersect them is skipped and the real rail serves both lanes.
            float realRailInner = width * 0.5f + RealRailCentreOffset - RealRailHalfThickness;
            float realRailOuter = width * 0.5f + RealRailCentreOffset + RealRailHalfThickness;

            for (int i = 1; i <= L.LaneCount; i++)
            {
                float cx = L.LaneCentreX(i);
                bool isPlayable = (i == L.PlayableLaneIndex);
                string tag = i.ToString("00");

                // Skipped on the playable slot when the real lane is present; the
                // standalone scene draws a stand-in instead so alignment is visible.
                if (isPlayable && !includePlayableLaneStandIn) continue;

                var laneGroup = Group(bank, "Lane" + tag + (isPlayable ? "_StandIn" : ""));

                Cosmetic(laneGroup, "Surface", cx - width * 0.5f, cx + width * 0.5f,
                         laneZMin, laneZMax,
                         LaneSurfaceTop - LaneSurfaceThickness, LaneSurfaceThickness, mats.Lane);

                if (isPlayable)
                {
                    // Stand-in uses the REAL rail geometry, not the slim backdrop
                    // rails — the point is to show what the actual lane occupies.
                    foreach (int side in new[] { -1, 1 })
                    {
                        float rc = cx + side * (width * 0.5f + RealRailCentreOffset);
                        Cosmetic(laneGroup, side < 0 ? "RailNorth" : "RailSouth",
                                 rc - RealRailHalfThickness, rc + RealRailHalfThickness,
                                 -2f, length + 2f, -0.055f, RealRailHeight, mats.Rail);
                    }
                }
                else
                {
                    foreach (int side in new[] { -1, 1 })
                    {
                        float rc = cx + side * railCentre;

                        // Skip a rail that would intersect the real lane's rails.
                        // Its lane surface still overlaps them very slightly at a
                        // tight pitch, but that sliver is buried inside the taller
                        // opaque rail box and the two are not coplanar, so it
                        // neither shows nor z-fights.
                        float inner = Mathf.Abs(rc) - railHalf;
                        bool clashesWithRealRail = inner < realRailOuter && Mathf.Abs(rc) + railHalf > realRailInner;
                        if (clashesWithRealRail) continue;

                        Cosmetic(laneGroup, side < 0 ? "RailNorth" : "RailSouth",
                                 rc - railHalf, rc + railHalf, laneZMin - 1.25f, laneZMax + 1.25f,
                                 -0.055f, L.BackdropRailHeight, mats.Rail);
                    }
                }
            }

            // NOTE: v1's per-lane approach cluster (ball-return rack, kiosk, monitor
            // behind the foul line) is GONE. It stood inside what is now the clear
            // approach band. Its jobs moved rather than vanished: the rack became
            // the paired ball-return islands, and the kiosk/monitor became the
            // settee pit's score console.
        }

        // ---------------------------------------------------------- settee pits

        /// <summary>
        /// One sunken pit per lane pair — the seating for the walkable-alley test
        /// in OpenQuestions.md, and the thing Beat A now reveals behind the thrower
        /// at turn start. Twelve boxes each: floor, three retaining walls, two
        /// steps, a three-sided U-bench, a low table and a console on a pedestal.
        ///
        /// The bench seat lands at y = +0.05 at the defaults — flush with the
        /// concourse — which is how a real settee pit reads: you sit down onto what
        /// looks like the ground.
        ///
        /// The gap left between two adjacent pits is registered as a THRESHOLD (a
        /// pit entrance), not a corridor, so the circulation audit holds it to
        /// 1.2m rather than the 2.5m secondary rule. Tony's category, not a guess.
        /// </summary>
        private static void BuildSetteePits(GameObject root, GameObject anchors, AlleyLayoutConfig L, Materials mats)
        {
            if (!L.BuildSetteePits || L.LanePairCount <= 0) return;

            var pits = Group(root, "SetteePits");

            float pitTop = -L.SetteePitRecess;
            float zMin = L.SetteePitZMin, zMax = L.SetteePitZMax;
            float rise = L.SetteePitStepRise;
            float stepsDepth = L.SetteePitStepCount * L.SetteePitStepRun;
            float usableZMin = zMin + stepsDepth;
            const float RetainThickness = 0.1f;

            for (int p = 0; p < L.LanePairCount; p++)
            {
                float b = L.LanePairBoundaryX(p);
                float xMin = b - L.SetteePitWidth * 0.5f;
                float xMax = b + L.SetteePitWidth * 0.5f;
                int laneA = p * 2 + 1, laneB = p * 2 + 2;
                string tag = laneA.ToString("00") + "_" + laneB.ToString("00");
                var g = Group(pits, "Pit" + tag);

                // (1) the sunken floor
                Slab(g, "PitFloor", xMin, xMax, zMin, zMax, pitTop - 0.2f, 0.2f, mats.Floor, true, walkable: true);

                // (2-4) retaining walls, sitting just OUTSIDE the pit footprint so
                // they close the void under the surrounding floor slab's cut edge.
                float retainH = L.FloorTopY - pitTop;
                Slab(g, "RetainNorth", xMin - RetainThickness, xMin, zMin, zMax, pitTop, retainH, mats.Wall, true);
                Slab(g, "RetainSouth", xMax, xMax + RetainThickness, zMin, zMax, pitTop, retainH, mats.Wall, true);
                Slab(g, "RetainEast", xMin - RetainThickness, xMax + RetainThickness,
                     zMax, zMax + RetainThickness, pitTop, retainH, mats.Wall, true);

                // (5-6) steps down the WEST edge, from the concourse into the pit.
                // Their rise is deliberately smaller than CirculationStepHeight and
                // the pit recess deliberately larger, so the audit can only reach
                // the pit floor through these treads.
                for (int s = 0; s < L.SetteePitStepCount; s++)
                {
                    float treadTop = -rise * (s + 1);
                    float sz0 = zMin + L.SetteePitStepRun * s;
                    Slab(g, "Step" + (s + 1), xMin, xMax, sz0, sz0 + L.SetteePitStepRun,
                         pitTop, treadTop - pitTop, mats.Floor, true, walkable: true);
                }

                // (7-9) the U-bench, opening WEST toward the steps.
                float benchEastFace = zMax - L.SetteePitBenchDepth;
                Slab(g, "BenchNorth", xMin, xMin + L.SetteePitBenchDepth, usableZMin, zMax,
                     pitTop, L.SetteePitBenchHeight, mats.Furniture, true);
                Slab(g, "BenchSouth", xMax - L.SetteePitBenchDepth, xMax, usableZMin, zMax,
                     pitTop, L.SetteePitBenchHeight, mats.Furniture, true);
                Slab(g, "BenchEast", xMin, xMax, benchEastFace, zMax,
                     pitTop, L.SetteePitBenchHeight, mats.Furniture, true);

                // (10) low table, centred between the steps and the east bench.
                float tableZ = (usableZMin + benchEastFace) * 0.5f;
                Post(g, "PitTable", b, tableZ, pitTop, L.SetteePitTableRadius, 0.42f, mats.Furniture, true);

                // (11-12) score console on a pedestal, at the pit's north-west
                // interior corner — the first thing you reach coming down the steps.
                // This replaces v1's per-lane approach kiosk, which cannot exist any
                // more now that the approach band has to be empty.
                float consoleX = xMin + L.SetteePitBenchDepth + 0.18f;
                float consoleZ = usableZMin + 0.2f;
                Slab(g, "ConsolePost", consoleX - 0.06f, consoleX + 0.06f, consoleZ - 0.06f, consoleZ + 0.06f,
                     pitTop, L.SetteePitConsoleHeight, mats.Machine, true);
                Slab(g, "ConsolePanel", consoleX - 0.3f, consoleX + 0.3f, consoleZ - 0.03f, consoleZ + 0.03f,
                     pitTop + L.SetteePitConsoleHeight, 0.35f, mats.Machine, true);

                // Spectator anchor: the middle of the pit, for the heckling and
                // betting layers to seat people at.
                Anchor(anchors, "Anchor_Pit" + tag, b, tableZ, pitTop, yaw: 180f);

                // Per-lane anchors, flanking the console. This is where "walk up to
                // a lane and type your name in" (OpenQuestions.md) will happen.
                float standZ = usableZMin + 0.18f;
                Anchor(anchors, "Anchor_Lane" + laneA.ToString("00"), b - 0.45f, standZ, pitTop, yaw: 0f);
                Anchor(anchors, "Anchor_Lane" + laneB.ToString("00"), b + 0.45f, standZ, pitTop, yaw: 0f);

                // Declared threshold: the pit's own MOUTH — the flight of steps you
                // come down to get in. Tony's rules name pit entrances explicitly.
                RegisterZone("PitEntrance_" + tag, RouteCategory.Threshold, traverseAlongX: false,
                             xMin, xMax, zMin - 0.3f, usableZMin + 0.3f);

                // Declared threshold: the gap between THIS pit and the next one.
                if (p + 1 < L.LanePairCount)
                {
                    float nextXMin = L.LanePairBoundaryX(p + 1) - L.SetteePitWidth * 0.5f;
                    RegisterZone("PitGap_after_" + tag, RouteCategory.Threshold, traverseAlongX: false,
                                 xMax + RetainThickness, nextXMin - RetainThickness, zMin, zMax);
                }
            }
        }

        // --------------------------------------------------------- ball returns

        /// <summary>One paired ball-return island per lane pair, on the pair
        /// boundary at the head of its settee pit — which is where a real alley
        /// puts them, and the reason the return band sits between the pits and the
        /// approach rather than inside either.</summary>
        private static void BuildBallReturns(GameObject root, AlleyLayoutConfig L, Materials mats)
        {
            if (L.LanePairCount <= 0) return;

            var returns = Group(root, "BallReturns");
            float zMin = L.BallReturnZMin, zMax = L.BallReturnZMax;

            for (int p = 0; p < L.LanePairCount; p++)
            {
                float b = L.LanePairBoundaryX(p);
                float xMin = b - L.BallReturnWidth * 0.5f;
                float xMax = b + L.BallReturnWidth * 0.5f;
                int laneA = p * 2 + 1, laneB = p * 2 + 2;
                var g = Group(returns, "Return" + laneA.ToString("00") + "_" + laneB.ToString("00"));

                Slab(g, "Body", xMin, xMax, zMin, zMax, 0f, L.BallReturnBodyHeight, mats.Machine, true);
                Slab(g, "Hood", xMin + 0.12f, xMax - 0.12f, zMin + 0.12f, zMax - 0.12f,
                     L.BallReturnBodyHeight, L.BallReturnHoodHeight, mats.Machine, true);
                // Lip on the east face so balls do not read as rolling off toward
                // the approach. Its east face lands exactly on the approach band's
                // west edge — see AuditEpsilon for why that matters.
                Slab(g, "RackLip", xMin, xMax, zMax - 0.12f, zMax,
                     L.BallReturnBodyHeight, 0.15f, mats.Machine, true);
            }
        }

        // --------------------------------------------------------------- casino

        private static void BuildCasino(GameObject root, GameObject anchors, AlleyLayoutConfig L, Materials mats)
        {
            var casino = Group(root, "CasinoNook");

            float h = L.CasinoPlatformHeight;
            float c = Mathf.Max(0f, L.CasinoChamfer);

            // The raised floor is a pentagon: square platform with the SOUTH-EAST
            // corner cut off at 45 degrees. Boxes cannot be subtracted from, so it
            // is assembled from two axis-aligned slabs that leave the corner empty,
            // plus one rotated slab that fills the diagonal.
            Slab(casino, "PlatformNorth", L.CasinoXMin, L.CasinoXMax - c,
                 L.CasinoZMin, L.CasinoZMax, 0f, h, mats.CasinoFloor, true, walkable: true);
            Slab(casino, "PlatformSouth", L.CasinoXMax - c, L.CasinoXMax,
                 L.CasinoZMin, L.CasinoZMax - c, 0f, h, mats.CasinoFloor, true, walkable: true);

            if (c > 0.01f)
            {
                // Triangle to fill: P1 (xMax-c, zMax), P2 (xMax, zMax-c), apex
                // P3 (xMax-c, zMax-c). A rotated box with one long face lying on
                // the P1-P2 diagonal covers it; the two corners that spill over
                // land on platform we already built, so nothing pokes outside.
                var p1 = new Vector2(L.CasinoXMax - c, L.CasinoZMax);
                var p2 = new Vector2(L.CasinoXMax, L.CasinoZMax - c);
                Vector2 mid = (p1 + p2) * 0.5f;
                float depth = c / Mathf.Sqrt(2f);           // apex distance from the diagonal
                Vector2 inward = new Vector2(-1f, -1f).normalized;
                Vector2 centre = mid + inward * (depth * 0.5f);

                // Raised 4mm taller than the platform: the box necessarily overlaps
                // the two slabs above (a rectangle cannot exactly fill a triangle),
                // and identical top faces at the same height would z-fight across
                // that whole overlap. A 4mm lip on a platform edge is invisible.
                SlabRotated(casino, "PlatformChamfer",
                            new Vector3(centre.x, h * 0.5f + 0.002f, centre.y),
                            // Local X spans the diagonal, local Z runs inward.
                            new Vector3(c * Mathf.Sqrt(2f), h + 0.004f, depth),
                            yaw: 225f, mat: mats.CasinoFloor, collider: true, walkable: true);
            }

            // ----- staircase down the nook's EAST edge -----
            // Each step is a solid block from the floor up to its own tread height,
            // descending eastward out of the platform. NOTE the flight LANDS ON the
            // concourse, i.e. on the primary route, and eats CasinoStairRun out of
            // it. The circulation audit reports what that leaves. Moving the flight
            // inside the nook would be a design change, and design changes are
            // Tony's — the builder reports, it does not fix.
            int steps = Mathf.Max(2, L.CasinoStairSteps);
            float rise = h / (steps + 1f);
            float run = L.CasinoStairRun / steps;
            for (int s = 0; s < steps; s++)
            {
                float treadTop = h - rise * (s + 1);
                float zMin = L.CasinoZMax + run * s;
                Slab(casino, "Step" + (s + 1), L.CasinoStairXMin, L.CasinoStairXMin + L.CasinoStairWidth,
                     zMin, zMin + run, 0f, treadTop, mats.CasinoFloor, true, walkable: true);
            }

            // ----- slot machines along the north wall -----
            float slotX = L.CasinoXMin + 0.75f;
            float span = (L.CasinoZMax - c) - L.CasinoZMin;
            for (int m = 0; m < L.SlotMachineCount; m++)
            {
                float z = L.CasinoZMin + span * (m + 0.6f) / (L.SlotMachineCount + 0.6f);
                Slab(casino, "SlotMachine" + (m + 1), slotX - 0.35f, slotX + 0.35f,
                     z - 0.45f, z + 0.45f, h, 1.8f, mats.Machine, true);
            }

            // ----- blackjack table -----
            // A cylinder stands in for the semicircle: chairs are placed around the
            // players' arc only, which is what actually reads as "semicircular" from
            // above. Swap for real geometry when the casino stops being greybox.
            Vector2 bj = L.BlackjackCentreXZ;
            Post(casino, "BlackjackTable", bj.x, bj.y, h, L.BlackjackRadius, 0.9f, mats.Furniture, true);
            for (int ch = 0; ch < L.BlackjackChairCount; ch++)
            {
                float a = Mathf.Lerp(120f, 240f, L.BlackjackChairCount == 1
                                     ? 0.5f
                                     : ch / (float)(L.BlackjackChairCount - 1)) * Mathf.Deg2Rad;
                float r = L.BlackjackRadius + 0.55f;
                Chair(casino, "BlackjackChair" + (ch + 1),
                      bj.x + Mathf.Cos(a) * r, bj.y + Mathf.Sin(a) * r, h, mats.Furniture);
            }

            // Placed at the head of the stairs, not the nook's centre — the centre is
            // occupied by the blackjack table, and an anchor a character is meant to
            // walk to must land on standable floor.
            Anchor(anchors, "Anchor_Casino",
                   L.CasinoStairXMin + L.CasinoStairWidth * 0.5f, L.CasinoZMax - 1.2f, h, yaw: 90f);

            // The staircase is the nook's only way in — a declared THRESHOLD, and an
            // intentional dead end (see CirculationAllowedDeadEnds).
            RegisterZone("CasinoStairMouth", RouteCategory.Threshold, traverseAlongX: false,
                         L.CasinoStairXMin, L.CasinoStairXMin + L.CasinoStairWidth,
                         L.CasinoZMax - 0.3f, L.CasinoZMax + L.CasinoStairRun + 0.3f);

            // The raised floor itself, between the slots and the blackjack table.
            // Tony's AMENITY INTERIOR category: you have to be able to move around
            // in here, or the casino is scenery you can look at but not use.
            RegisterZone("CasinoInterior", RouteCategory.AmenityInterior, traverseAlongX: true,
                         L.CasinoXMin, L.CasinoXMax, L.CasinoZMin, L.CasinoZMax);
        }

        // ---------------------------------------------------------- card dealer

        /// <summary>Recessed into the west band between the casino and the bar.
        /// v1's alcove opened SOUTH with walls east and west; the band restructure
        /// turned it 90 degrees, so it now opens EAST onto the lobby with walls
        /// north and south and the room's west wall closing the back.</summary>
        private static void BuildCardDealer(GameObject root, GameObject anchors, AlleyLayoutConfig L, Materials mats)
        {
            var alcove = Group(root, "CardDealerAlcove");

            // Darker floor patch, sitting just proud of the venue floor so it reads
            // as a distinct recessed room rather than z-fighting with it.
            Slab(alcove, "Floor", L.CardDealerXMin, L.CardDealerXMax,
                 L.CardDealerZMin, L.CardDealerZMax, L.FloorTopY, 0.007f, mats.CardDealerFloor, true, walkable: true);

            Slab(alcove, "WallNorth", L.CardDealerXMin, L.CardDealerXMin + 0.3f,
                 L.CardDealerZMin, L.CardDealerZMax, 0f, L.CardDealerWallHeight, mats.Wall, true);
            Slab(alcove, "WallSouth", L.CardDealerXMax - 0.3f, L.CardDealerXMax,
                 L.CardDealerZMin, L.CardDealerZMax, 0f, L.CardDealerWallHeight, mats.Wall, true);

            float tableX = (L.CardDealerXMin + L.CardDealerXMax) * 0.5f;
            float tableZ = L.CardDealerZMin + 1.4f;
            Slab(alcove, "DealerTable", tableX - 0.7f, tableX + 0.7f,
                 tableZ - 0.5f, tableZ + 0.5f, 0f, 0.95f, mats.Furniture, true);

            Anchor(anchors, "Anchor_CardDealer", tableX, tableZ + 1.4f, 0f, yaw: 180f);

            RegisterZone("CardDealerMouth", RouteCategory.Threshold, traverseAlongX: false,
                         L.CardDealerXMin + 0.3f, L.CardDealerXMax - 0.3f,
                         L.CardDealerZMax - 0.4f, L.CardDealerZMax + 0.4f);

            // Inside the alcove, around the dealer table. An intentional dead end
            // still has to be a room you can stand in.
            RegisterZone("CardDealerInterior", RouteCategory.AmenityInterior, traverseAlongX: false,
                         L.CardDealerXMin + 0.3f, L.CardDealerXMax - 0.3f,
                         L.CardDealerZMin, L.CardDealerZMax - 0.4f);
        }

        // ------------------------------------------------------- service spine

        /// <summary>
        /// The bar, the snack bar and the front desk as THREE STATIONS ON ONE
        /// SERVICE SPINE, not a strip of separate kiosks. What makes them read as
        /// one building:
        ///
        ///   - ONE continuous back-shelving run behind all three (a single box, not
        ///     three) — an unbroken wall of shelving.
        ///   - ONE continuous staff walkway in front of it, so one member of staff
        ///     reaches every station without coming round the front.
        ///   - open gaps BETWEEN the stations, through which you can see the
        ///     walkway and out the far side; that is what stops three counters
        ///     reading as one long bench.
        ///   - different counter heights, so the silhouette steps.
        ///
        /// FIVE BOXES PER STATION. Two of the features are made by NOT building a
        /// box, which is the trick worth learning here:
        ///   - the recessed under-counter void is the empty space between the front
        ///     panel and the back panel, under a full-depth top;
        ///   - the walk-in gap is the ABSENCE of an end cap at one end.
        /// </summary>
        private static void BuildServiceSpine(GameObject root, GameObject anchors, AlleyLayoutConfig L, Materials mats)
        {
            var spine = Group(root, "ServiceSpine");

            float spineXMin = Mathf.Min(L.BarXMin, Mathf.Min(L.SnackBarXMin, L.FrontDeskXMin));
            float spineXMax = Mathf.Max(L.BarXMax, Mathf.Max(L.SnackBarXMax, L.FrontDeskXMax));

            // ONE shelving run for the whole spine. This is the box that makes it a
            // building instead of three kiosks.
            Slab(spine, "BackShelving", spineXMin, spineXMax,
                 L.ServiceBackShelfZMin, L.ServiceBackShelfZMax,
                 0f, L.ServiceBackShelfHeight, mats.Machine, true);

            BuildServiceStation(spine, anchors, L, mats, "Bar",
                                L.BarXMin, L.BarXMax, L.BarCounterHeight, "Anchor_Bar");
            BuildServiceStation(spine, anchors, L, mats, "SnackBar",
                                L.SnackBarXMin, L.SnackBarXMax, L.CounterHeight, "Anchor_SnackBar");
            BuildServiceStation(spine, anchors, L, mats, "FrontDesk",
                                L.FrontDeskXMin, L.FrontDeskXMax, L.CounterHeight, "Anchor_FrontDesk");

            // Stools along the bar counter's lobby-facing side.
            float stoolZ = L.ServiceCounterZMax + 0.55f;
            float stoolXMin = (L.ServiceWalkInAtNorthEnd ? L.BarXMin + L.ServiceWalkInWidth : L.BarXMin) + 0.6f;
            float stoolXMax = (L.ServiceWalkInAtNorthEnd ? L.BarXMax : L.BarXMax - L.ServiceWalkInWidth) - 0.6f;
            for (int s = 0; s < L.BarStoolCount; s++)
            {
                float x = Mathf.Lerp(stoolXMin, stoolXMax, L.BarStoolCount == 1 ? 0.5f : s / (float)(L.BarStoolCount - 1));
                Post(spine, "BarStool" + (s + 1), x, stoolZ, 0f, 0.22f, 0.75f, mats.Furniture, true);
            }

            // The open gaps BETWEEN stations. Declared thresholds, not corridors.
            // Reaching ServiceStationGapLobbyBleed east of the counter line on
            // purpose: the walkable pinch at each gap straddles that line, and a
            // zone stopping exactly at it covers under half the cluster, so the
            // audit reported these UNCLASSIFIED. Tony categorised them as
            // thresholds; this is the declaration actually covering them.
            RegisterZone("ServiceStationGap_Bar_Snack", RouteCategory.Threshold, traverseAlongX: false,
                         L.BarXMax, L.SnackBarXMin,
                         L.ServiceCounterZMin, L.ServiceCounterZMax + L.ServiceStationGapLobbyBleed);
            RegisterZone("ServiceStationGap_Snack_Desk", RouteCategory.Threshold, traverseAlongX: false,
                         L.SnackBarXMax, L.FrontDeskXMin,
                         L.ServiceCounterZMin, L.ServiceCounterZMax + L.ServiceStationGapLobbyBleed);
        }

        private static void BuildServiceStation(GameObject spine, GameObject anchors, AlleyLayoutConfig L,
                                                Materials mats, string name, float xMin, float xMax,
                                                float height, string anchorName)
        {
            var g = Group(spine, name);

            float cz0 = L.ServiceCounterZMin;
            float cz1 = L.ServiceCounterZMax;
            const float TopThickness = 0.12f;
            const float Panel = 0.1f;

            // The walk-in gap is taken off one end of the counter RUN. The gap
            // itself is not a box — it is floor we simply do not build on.
            float runMin = L.ServiceWalkInAtNorthEnd ? xMin + L.ServiceWalkInWidth : xMin;
            float runMax = L.ServiceWalkInAtNorthEnd ? xMax : xMax - L.ServiceWalkInWidth;
            if (runMax - runMin < 0.5f)
            {
                Debug.LogWarning("[AlleyGreybox] Station '" + name + "' is only " + (xMax - xMin).ToString("0.00") +
                                 "m wide, which leaves nothing after a " + L.ServiceWalkInWidth.ToString("0.00") +
                                 "m walk-in gap. Widen the station or shrink ServiceWalkInWidth.");
                return;
            }

            // (1) counter top, full depth.
            Slab(g, "CounterTop", runMin, runMax, cz0, cz1, height - TopThickness, TopThickness, mats.Counter, true);
            // (2) customer-facing front panel.
            Slab(g, "CounterFront", runMin, runMax, cz1 - Panel, cz1, 0f, height - TopThickness, mats.Counter, true);
            // (3) staff-facing back panel. Between (2) and (3) is the RECESSED
            //     UNDER-COUNTER VOID — deliberately empty, which is what makes this
            //     read as a counter rather than a plinth.
            Slab(g, "CounterBack", runMin, runMax, cz0, cz0 + Panel, 0f, height - TopThickness, mats.Counter, true);
            // (4) end cap at the far end from the walk-in.
            float capMin = L.ServiceWalkInAtNorthEnd ? runMax - Panel : runMin;
            Slab(g, "EndCap", capMin, capMin + Panel, cz0, cz1, 0f, height - TopThickness, mats.Counter, true);
            // (5) sign, hung above and set back so it does not clip the front face.
            Slab(g, "Sign", runMin + 0.4f, runMax - 0.4f, cz1 - 0.35f, cz1 - 0.05f,
                 height + 0.9f, 0.6f, mats.Machine, true);

            // Anchor on the CUSTOMER side. No public anchor is ever placed in the
            // staff walkway, which is what stops the circulation audit routing a
            // customer through a back-of-house walk-in.
            Anchor(anchors, anchorName, (xMin + xMax) * 0.5f, cz1 + 1.3f, 0f, yaw: 180f);

            // The walk-in gap: a declared THRESHOLD, not a travel route.
            float gapMin = L.ServiceWalkInAtNorthEnd ? xMin : xMax - L.ServiceWalkInWidth;
            RegisterZone("ServiceWalkIn_" + name, RouteCategory.Threshold, traverseAlongX: false,
                         gapMin, gapMin + L.ServiceWalkInWidth, cz0, cz1);

            // The staff walkway behind this station. Tony's BACK-OF-HOUSE category:
            // it is a place you get to for the bit, not a route anyone travels, so
            // it is held to threshold width rather than the 2.5m a secondary route
            // would demand. Declaring it is also what stops it reporting
            // UNCLASSIFIED, which is how v2's first audit surfaced it.
            RegisterZone("StaffWalkway_" + name, RouteCategory.BackOfHouse, traverseAlongX: true,
                         xMin, xMax, L.ServiceBackShelfZMax, cz0);
        }

        // --------------------------------------------------------------- tables

        private static void BuildTables(GameObject root, AlleyLayoutConfig L, Materials mats)
        {
            var seating = Group(root, "Seating");
            if (L.TableCentresXZ == null || L.TableCentresXZ.Length == 0) return;

            float footprint = L.TableRadius + 0.55f + 0.22f;   // table + chair ring + chair depth

            for (int t = 0; t < L.TableCentresXZ.Length; t++)
            {
                Vector2 c = L.TableCentresXZ[t];
                var group = Group(seating, "Table" + (t + 1));

                Post(group, "Top", c.x, c.y, 0f, L.TableRadius, 0.75f, mats.Furniture, true);

                for (int ch = 0; ch < L.ChairsPerTable; ch++)
                {
                    float a = (ch / (float)Mathf.Max(1, L.ChairsPerTable)) * Mathf.PI * 2f;
                    float r = L.TableRadius + 0.55f;
                    Chair(group, "Chair" + (ch + 1), c.x + Mathf.Cos(a) * r, c.y + Mathf.Sin(a) * r, 0f, mats.Furniture);
                }

                // Registered for the 2.0m open-floor furniture spacing rule. Pairwise
                // AABB IS the right tool for "are these two too close" — unlike "does
                // the floor loop", which it cannot answer at all.
                FurniturePieces.Add(new FurniturePiece
                {
                    Name = "Table" + (t + 1),
                    XMin = c.x - footprint,
                    XMax = c.x + footprint,
                    ZMin = c.y - footprint,
                    ZMax = c.y + footprint,
                });
            }
        }

        // ---------------------------------------------------- cosmetics counter

        private static void BuildCosmetics(GameObject root, GameObject anchors, AlleyLayoutConfig L, Materials mats)
        {
            var shop = Group(root, "CosmeticsCounter");

            float wallInner = L.RoomXMax - L.WallThickness * 0.5f;
            float front = wallInner - L.CounterDepth;   // counter faces NORTH, into the room

            Slab(shop, "Counter", front, wallInner, L.CosmeticsZMin, L.CosmeticsZMax,
                 0f, L.CounterHeight, mats.Counter, true);

            // Sign hung above, set back so it does not clip the counter's front face.
            float signZInset = (L.CosmeticsZMax - L.CosmeticsZMin) * 0.15f;
            Slab(shop, "Sign", wallInner - 0.35f, wallInner - 0.05f,
                 L.CosmeticsZMin + signZInset, L.CosmeticsZMax - signZInset,
                 L.CounterHeight + 0.9f, L.CosmeticsSignHeight, mats.Machine, true);

            Anchor(anchors, "Anchor_CosmeticsCounter",
                   front - 1.1f, (L.CosmeticsZMin + L.CosmeticsZMax) * 0.5f, 0f, yaw: 90f);
        }

        // ------------------------------------------------------------- entrance

        private static void BuildEntrance(GameObject root, GameObject anchors, AlleyLayoutConfig L, Materials mats)
        {
            var entrance = Group(root, "Entrance");

            float wall = L.RoomXMax;
            float mid = (L.EntranceZMin + L.EntranceZMax) * 0.5f;
            float leaf = (L.EntranceZMax - L.EntranceZMin) * 0.5f;

            // Two leaves standing ajar, swung outward (south) through the wall.
            foreach (int side in new[] { -1, 1 })
            {
                float hingeZ = side < 0 ? L.EntranceZMin : L.EntranceZMax;
                float yaw = side < 0 ? 30f : -30f;
                var centre = new Vector3(wall + 0.28f, L.DoorHeight * 0.5f, hingeZ - side * leaf * 0.45f);
                SlabRotated(entrance, side < 0 ? "DoorWest" : "DoorEast",
                            centre, new Vector3(0.08f, L.DoorHeight, leaf), yaw, mats.Counter, collider: true);
            }

            // Small landing outside so the doors do not open onto void. It starts at
            // the wall's OUTER face, not its centreline: the venue floor slab runs to
            // that same face, and two coplanar top faces at y = FloorTopY would z-fight.
            Slab(entrance, "Porch", wall + L.WallThickness * 0.5f, wall + 2.5f,
                 L.EntranceZMin - 0.8f, L.EntranceZMax + 0.8f,
                 L.FloorTopY - 0.15f, 0.15f, mats.Floor, true, walkable: true);

            Anchor(anchors, "Anchor_Entrance", wall - 1.6f, mid, 0f, yaw: 180f);

            // Registered a metre INTO the room, not just across the wall's own
            // thickness: the wall is 0.3m thick and its inner face leaves only
            // 0.15m of floor, which is thinner than a grid cell, so a wall-only
            // zone would report "no reachable floor" instead of the doorway's
            // actual clear width.
            RegisterZone("EntranceDoorway", RouteCategory.Threshold, traverseAlongX: true,
                         wall - 1f, wall, L.EntranceZMin, L.EntranceZMax);
        }

        // -------------------------------------------------------- route zones

        /// <summary>
        /// Declares which stretch of floor is which of Tony's three categories.
        /// Thresholds are registered by whoever builds them (they know a pit gap is
        /// a pit entrance); the travel routes are registered here, straight off the
        /// bands.
        ///
        /// Anything NOT covered by a declaration and measured narrow is reported as
        /// UNCLASSIFIED rather than assigned a category. Tony was explicit: if you
        /// cannot confidently classify a gap, hand it back — do not guess.
        /// </summary>
        private static void RegisterRouteZones(AlleyLayoutConfig L)
        {
            // The bands that run BEHIND the lane bank stop at the bank's own edges:
            // beyond them you are on a flank, which is a different route with a
            // different width, and folding the two together would measure neither.
            float bankXMin = L.LaneCentreX(1) - L.LanePitch * 0.5f;
            float bankXMax = L.LaneCentreX(L.LaneCount) + L.LanePitch * 0.5f;

            // East-west routes: you travel along X, so the clear width is in Z.
            RegisterZone("Concourse", RouteCategory.Primary, traverseAlongX: true,
                         bankXMin, bankXMax, L.ConcourseZMin, L.ConcourseZMax);
            RegisterZone("ApproachBand", RouteCategory.Secondary, traverseAlongX: true,
                         bankXMin, bankXMax, L.ApproachZMin, L.ApproachZMax);
            // The lobby is the open floor in FRONT OF THE SPINE, so it starts where
            // the card dealer alcove ends. Running it to RoomXMin would drag the
            // raised casino platform into a measurement about the lobby.
            RegisterZone("LobbyFloor", RouteCategory.Secondary, traverseAlongX: true,
                         L.CardDealerXMax, L.RoomXMax, L.LobbyZMin, L.LobbyZMax);

            // North-south routes: you travel along Z, so the clear width is in X.
            // The ball-return band is crossed, not walked along — the islands sit
            // across it, and you pass between the pits and the approach.
            RegisterZone("BallReturnBand", RouteCategory.Secondary, traverseAlongX: false,
                         bankXMin, bankXMax, L.BallReturnZMin, L.BallReturnZMax);
            // The two flanks either side of the lane bank — the legs that turn the
            // concourse and the approach into a lap rather than two dead ends.
            RegisterZone("NorthFlank", RouteCategory.Secondary, traverseAlongX: false,
                         L.RoomXMin, bankXMin, L.ConcourseZMax, L.ApproachZMax);
            RegisterZone("SouthFlank", RouteCategory.Secondary, traverseAlongX: false,
                         bankXMax, L.RoomXMax, L.ConcourseZMax, L.ApproachZMax);
        }

        private static void RegisterZone(string name, RouteCategory category, bool traverseAlongX,
                                         float xMin, float xMax, float zMin, float zMax)
        {
            RouteZones.Add(new RouteZone
            {
                Name = name,
                Category = category,
                TraverseAlongX = traverseAlongX,
                XMin = Mathf.Min(xMin, xMax),
                XMax = Mathf.Max(xMin, xMax),
                ZMin = Mathf.Min(zMin, zMax),
                ZMax = Mathf.Max(zMin, zMax),
            });
        }

        // ---------------------------------------------------------------- audit

        /// <summary>
        /// THREE CHECKS, because v1's single exclusion volume was doing two
        /// different jobs at once — protecting the ball AND protecting the camera
        /// corridor — and that conflation is what made a legitimate settee pit
        /// illegal. They are now separated by what each actually protects:
        ///
        /// (1) BALL VOLUME — hard error, colliders only. The stretch the ball must
        ///     traverse cleanly on every normal throw.
        ///
        /// (2) APPROACH CLEAR BAND — hard error, and STRICTLY STRONGER than the v1
        ///     check it replaces: it fails on ANY created object — collider,
        ///     RENDERER or ANCHOR. A cosmetic box in the aim camera's face is just
        ///     as bad as a solid one and v1 would have waved it through. Run at two
        ///     widths: everything out to ApproachClearHalfWidth, and anything solid
        ///     out to the full lane-plus-rails width.
        ///
        /// (3) CAMERA CORRIDOR — INFORMATIONAL, never a defect. Lists what Beat A
        ///     frames behind the thrower at turn start. Tony decided on 2026-07-26
        ///     that this reveal STAYS, so it fires on every build forever and is
        ///     worded accordingly.
        /// </summary>
        private static void AuditColliders(AlleyLayoutConfig L)
        {
            // Collider.bounds is derived from the physics scene's copy of the
            // transform. Everything above was positioned this same frame, so force
            // the sync first — otherwise the audit can read stale bounds and pass
            // something it should have caught, which is the one failure mode a
            // safety check must not have.
            Physics.SyncTransforms();

            // ---- (1) ball volume ----
            Bounds ball = Shrink(MakeBounds(-L.PlayVolumeHalfWidth, L.PlayVolumeHalfWidth,
                                            L.PlayVolumeYMin, L.PlayVolumeYMax,
                                            L.PlayVolumeZMin, L.PlayVolumeZMax));
            int ballOffenders = 0;
            foreach (Collider col in CreatedColliders)
            {
                if (col == null || !col.bounds.Intersects(ball)) continue;
                ballOffenders++;
                Debug.LogError("[AlleyGreybox] BALL VOLUME: '" + PathOf(col.transform) + "' has a collider inside the " +
                               "playable lane volume (bounds " + col.bounds + "). It can deflect the ball or block a " +
                               "pin. Move it in AlleyLayoutConfig, or build it with Cosmetic() so it has no collider.", col);
            }
            Debug.Log("[AlleyGreybox] (1) BALL VOLUME  x +/-" + L.PlayVolumeHalfWidth + ", z [" + L.PlayVolumeZMin +
                      ", " + L.PlayVolumeZMax + "], y [" + L.PlayVolumeYMin + ", " + L.PlayVolumeYMax + "]: " +
                      CreatedColliders.Count + " colliders created, " + ballOffenders + " inside." +
                      (ballOffenders == 0 ? " Lane is clear." : " FIX THESE before playing."));

            // ---- (2) approach clear band ----
            Bounds narrow = Shrink(MakeBounds(-L.ApproachClearHalfWidth, L.ApproachClearHalfWidth,
                                              L.PlayVolumeYMin, L.PlayVolumeYMax,
                                              L.ApproachZMin, L.ApproachZMax));
            Bounds wide = Shrink(MakeBounds(-L.PlayVolumeHalfWidth, L.PlayVolumeHalfWidth,
                                            L.PlayVolumeYMin, L.PlayVolumeYMax,
                                            L.ApproachZMin, L.ApproachZMax));
            int approachOffenders = 0;

            foreach (Renderer r in CreatedRenderers)
            {
                if (r == null || !r.bounds.Intersects(narrow)) continue;
                approachOffenders++;
                Debug.LogError("[AlleyGreybox] APPROACH BAND: '" + PathOf(r.transform) + "' is inside the clear " +
                               "approach band (bounds " + r.bounds + "). The throw camera lives here — the aim view " +
                               "is at z = " + KnownAimCameraZ + " and the orbit beat reaches z = -2.3. Nothing may be " +
                               "built in this band, visible or not.", r);
            }
            foreach (Collider col in CreatedColliders)
            {
                if (col == null || !col.bounds.Intersects(wide)) continue;
                if (col.bounds.Intersects(narrow)) continue;   // already reported above via its renderer
                approachOffenders++;
                Debug.LogError("[AlleyGreybox] APPROACH BAND: '" + PathOf(col.transform) + "' puts a COLLIDER in the " +
                               "approach band out at the rails (bounds " + col.bounds + "). The thrower walks up " +
                               "here.", col);
            }
            foreach (Transform a in CreatedAnchors)
            {
                if (a == null || !narrow.Contains(a.position)) continue;
                approachOffenders++;
                Debug.LogError("[AlleyGreybox] APPROACH BAND: anchor '" + a.name + "' is inside the clear approach " +
                               "band at " + a.position + ". Anchors are places characters get sent to; nothing " +
                               "belongs in this band.", a);
            }
            Debug.Log("[AlleyGreybox] (2) APPROACH CLEAR BAND  z [" + L.ApproachZMin + ", " + L.ApproachZMax +
                      "], x +/-" + L.ApproachClearHalfWidth + " for ANY object (colliders + renderers + anchors), " +
                      "x +/-" + L.PlayVolumeHalfWidth + " for colliders: " + approachOffenders + " object(s) inside." +
                      (approachOffenders == 0 ? " Band is clear." : " FIX THESE.") +
                      "  [strictly stronger than the v1 check, which only ever looked at colliders]");

            // ---- (3) camera corridor: informational ----
            if (L.WarnOnCameraCorridorContents)
            {
                Bounds corridor = Shrink(MakeBounds(-L.CameraCorridorHalfWidth, L.CameraCorridorHalfWidth,
                                                    L.PlayVolumeYMin, L.PlayVolumeYMax,
                                                    L.CameraCorridorZMin, L.ApproachZMin));
                var names = new List<string>();
                foreach (Renderer r in CreatedRenderers)
                    if (r != null && r.bounds.Intersects(corridor)) names.Add(PathOf(r.transform));

                var sb = new StringBuilder();
                sb.AppendLine("[AlleyGreybox] (3) CAMERA CORRIDOR — INFORMATIONAL. This is not a defect and never " +
                              "will be; it fires on every build by design.");
                sb.AppendLine(names.Count + " object(s) sit behind the thrower in x +/-" + L.CameraCorridorHalfWidth +
                              ", z [" + L.CameraCorridorZMin + ", " + L.ApproachZMin + "] and WILL be in frame at " +
                              "turn start.");
                sb.AppendLine("EXPECTED — this is the turn-start reveal of the social floor. Beat A puts the camera " +
                              "at z ~ +1.0 looking BACK at the thrower, so the settee pit and the concourse behind " +
                              "him are the shot. Tony's call, 2026-07-26; see GameBible.md §7.");
                sb.AppendLine("Listed so a future change to the venue can never silently change the opening shot:");
                for (int i = 0; i < names.Count; i++) sb.AppendLine("   " + names[i]);
                Debug.Log(sb.ToString());
            }
        }

        private static Bounds MakeBounds(float xMin, float xMax, float yMin, float yMax, float zMin, float zMax)
        {
            var b = new Bounds();
            b.SetMinMax(new Vector3(xMin, yMin, zMin), new Vector3(xMax, yMax, zMax));
            return b;
        }

        /// <summary>Pulls an audit volume in by a hair. Bounds.Intersects counts
        /// MERELY TOUCHING as intersecting, and the bands share exact edges by
        /// construction — a ball return's east face lands precisely on the approach
        /// band's west edge — so without this every return false-positives on the
        /// first run. The VOLUME shrinks; the geometry never moves.</summary>
        private static Bounds Shrink(Bounds b)
        {
            b.SetMinMax(b.min + Vector3.one * AuditEpsilon, b.max - Vector3.one * AuditEpsilon);
            return b;
        }

        private static string PathOf(Transform t)
        {
            // Parent included because names like "Counter" and "Floor" repeat across
            // groups, and a bare name would not say which one to move.
            return (t.parent != null ? t.parent.name + "/" : "") + t.name;
        }

        // -------------------------------------------------------------- helpers

        private struct Materials
        {
            public Material Floor, Wall, CasinoFloor, CardDealerFloor, Counter, Furniture, Machine, Lane, Rail;
        }

        private static Materials LoadMaterials()
        {
            return new Materials
            {
                Floor = LoadOrCreateMaterial("AlleyFloorMat", new Color(0.82f, 0.76f, 0.66f)),
                Wall = LoadOrCreateMaterial("AlleyWallMat", new Color(0.22f, 0.20f, 0.20f)),
                CasinoFloor = LoadOrCreateMaterial("CasinoFloorMat", new Color(0.45f, 0.10f, 0.13f)),
                CardDealerFloor = LoadOrCreateMaterial("CardDealerFloorMat", new Color(0.24f, 0.20f, 0.28f)),
                Counter = LoadOrCreateMaterial("CounterMat", new Color(0.88f, 0.86f, 0.80f)),
                Furniture = LoadOrCreateMaterial("FurnitureMat", new Color(0.45f, 0.28f, 0.18f)),
                Machine = LoadOrCreateMaterial("MachineMat", new Color(0.16f, 0.16f, 0.18f)),
                // Reused from GreyboxSceneBuilder if they already exist, so the
                // backdrop lanes match the real one exactly.
                Lane = LoadOrCreateMaterial("LaneMat", new Color(0.85f, 0.65f, 0.35f)),
                Rail = LoadOrCreateMaterial("RailMat", new Color(0.25f, 0.25f, 0.3f)),
            };
        }

        private static GameObject Group(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform);
            return go;
        }

        /// <summary>
        /// A box described the way a floorplan describes it — by its extents —
        /// instead of by centre and scale. Every number in this file then reads
        /// straight off the blueprint, which is what makes the layout tweakable.
        ///
        /// <paramref name="walkable"/> declares that this box is GROUND rather than
        /// an obstacle. The circulation audit needs that distinction and must not
        /// infer it from height: a chair seat and a step tread are the same height,
        /// and only the builder knows which is which.
        /// </summary>
        private static GameObject Slab(GameObject parent, string name,
                                       float xMin, float xMax, float zMin, float zMax,
                                       float yBase, float height, Material mat, bool collider,
                                       bool walkable = false)
        {
            var centre = new Vector3((xMin + xMax) * 0.5f, yBase + height * 0.5f, (zMin + zMax) * 0.5f);
            var size = new Vector3(Mathf.Abs(xMax - xMin), height, Mathf.Abs(zMax - zMin));
            return MakeBox(parent, name, centre, size, Quaternion.identity, mat, collider, walkable);
        }

        private static GameObject SlabRotated(GameObject parent, string name, Vector3 centre, Vector3 size,
                                              float yaw, Material mat, bool collider, bool walkable = false)
        {
            return MakeBox(parent, name, centre, size, Quaternion.Euler(0f, yaw, 0f), mat, collider, walkable);
        }

        /// <summary>Slab with the collider stripped. Same lesson GreyboxSceneBuilder
        /// learned three times over: CreatePrimitive ALWAYS attaches a collider, and
        /// anything purely visual becomes an invisible wall unless you remove it.</summary>
        private static GameObject Cosmetic(GameObject parent, string name,
                                           float xMin, float xMax, float zMin, float zMax,
                                           float yBase, float height, Material mat)
        {
            return Slab(parent, name, xMin, xMax, zMin, zMax, yBase, height, mat, collider: false);
        }

        private static GameObject Post(GameObject parent, string name, float x, float z,
                                       float yBase, float radius, float height, Material mat, bool collider,
                                       bool walkable = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent.transform);
            go.transform.position = new Vector3(x, yBase + height * 0.5f, z);
            // A cylinder mesh is 2 units tall and 1 unit across at scale 1.
            go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            var rend = go.GetComponent<Renderer>();
            rend.sharedMaterial = mat;
            CreatedRenderers.Add(rend);
            FinishCollider(go, collider, walkable);
            return go;
        }

        /// <summary>Seat slab plus a back — enough silhouette to read as a chair from
        /// across the room, which is all ArtGuide's greybox rule asks for.</summary>
        private static GameObject Chair(GameObject parent, string name, float x, float z, float yBase, Material mat)
        {
            var chair = Group(parent, name);
            Slab(chair, "Seat", x - 0.22f, x + 0.22f, z - 0.22f, z + 0.22f, yBase, 0.45f, mat, true);
            Slab(chair, "Back", x - 0.22f, x + 0.22f, z + 0.14f, z + 0.22f, yBase + 0.45f, 0.45f, mat, true);
            return chair;
        }

        private static GameObject MakeBox(GameObject parent, string name, Vector3 centre, Vector3 size,
                                          Quaternion rotation, Material mat, bool collider, bool walkable)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent.transform);
            go.transform.position = centre;
            go.transform.rotation = rotation;
            go.transform.localScale = size;
            var rend = go.GetComponent<Renderer>();
            rend.sharedMaterial = mat;
            CreatedRenderers.Add(rend);
            FinishCollider(go, collider, walkable);
            return go;
        }

        private static void FinishCollider(GameObject go, bool keep, bool walkable)
        {
            var col = go.GetComponent<Collider>();
            if (keep)
            {
                if (col != null)
                {
                    CreatedColliders.Add(col);
                    if (walkable) WalkableColliders.Add(col);
                    else ObstacleColliders.Add(col);
                }
            }
            else if (col != null)
            {
                Object.DestroyImmediate(col);
            }
        }

        /// <summary>Named empty marking a place gameplay will need later — the
        /// entrance, each lane's console, each pit, the bar. Real transforms beat
        /// hard-coded numbers once the drag-to-the-line turn transition exists.</summary>
        private static GameObject Anchor(GameObject parent, string name, float x, float z, float y, float yaw)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform);
            go.transform.position = new Vector3(x, y, z);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            CreatedAnchors.Add(go.transform);
            return go;
        }

        private static Material LoadOrCreateMaterial(string name, Color color)
        {
            string path = ProjectRoot + "/Materials/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            // URP projects render "Standard" as pink; pick whichever exists.
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            var mat = new Material(shader) { color = color };
            EnsureFolder(ProjectRoot + "/Materials");
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static T LoadOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
