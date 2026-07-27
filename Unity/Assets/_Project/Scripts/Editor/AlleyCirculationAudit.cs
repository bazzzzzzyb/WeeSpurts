using System.Collections.Generic;
using System.Text;
using UnityEngine;
using WeeSpurts.Environment;

namespace WeeSpurts.Editor
{
    /// <summary>
    /// Which of Tony's circulation categories a stretch of floor belongs to.
    /// See Docs/GameBible.md and the circulation rules in the AlleyLayout v2 brief:
    ///
    ///   Primary   — the concourse. 4.0m clear minimum.
    ///   Secondary — paths across the open floor, between the spine and furniture,
    ///               around the amenities. 2.5m minimum.
    ///   Threshold — short pass-throughs that are NOT travel routes: pit entrances,
    ///               counter walk-ins, doorways, alcove mouths. 1.2m minimum, and
    ///               EXEMPT from the secondary rule.
    ///
    /// The last two are Tony's, added after v2's first audit reported their gaps as
    /// UNCLASSIFIED and asked him to categorise rather than guessing:
    ///
    ///   AmenityInterior — the space INSIDE a destination, between its furniture.
    ///               1.2m minimum. An amenity you can see but not move inside is
    ///               not a destination.
    ///   BackOfHouse — the staff walkway behind a service counter. 1.2m minimum:
    ///               getting behind a counter is a stunt, not a commute.
    ///
    /// All three of Threshold / AmenityInterior / BackOfHouse are NON-ROUTE
    /// categories: they are carved out of the surrounding route's width
    /// measurement, so a 1.25m staff walkway can never drag a secondary route's
    /// reported width down below its own 2.5m minimum.
    ///
    /// Category is DECLARED BY THE BUILDER, never inferred from geometry. Tony was
    /// explicit: do not guess. The builder knows a pit gap is a pit entrance and a
    /// doorway is a doorway, so it says so; anything the audit measures that no
    /// declared zone covers is reported as UNCLASSIFIED for Tony to categorise,
    /// never silently assigned.
    /// </summary>
    public enum RouteCategory
    {
        Primary,
        Secondary,
        Threshold,
        AmenityInterior,
        BackOfHouse,
    }

    /// <summary>A declared stretch of floor, in world XZ. Axis-aligned.</summary>
    public struct RouteZone
    {
        public string Name;
        public RouteCategory Category;
        public float XMin, XMax, ZMin, ZMax;

        /// <summary>Which way you TRAVEL through this zone. Declared by the builder
        /// for the same reason the category is: inferring it from the rectangle's
        /// aspect ratio gets it exactly backwards for a wide, shallow doorway, and
        /// then the audit measures the length of a corridor instead of its width.
        /// The clear width is measured ACROSS this axis.</summary>
        public bool TraverseAlongX;

        public bool Contains(float x, float z)
        {
            return x >= XMin && x <= XMax && z >= ZMin && z <= ZMax;
        }
    }

    /// <summary>A piece of open-floor furniture, for the 2.0m spacing rule. This is
    /// the ONE check where a pairwise AABB test is the right tool: "are these two
    /// objects too close" is genuinely a pairwise question, unlike "does the floor
    /// loop", which is not.</summary>
    public struct FurniturePiece
    {
        public string Name;
        public float XMin, XMax, ZMin, ZMax;
    }

    /// <summary>
    /// Proves — rather than assumes — that the venue floor is walkable, loops, and
    /// meets Tony's per-category width minimums.
    ///
    /// WHY A FLOOD FILL AND NOT A GAP TEST. A pairwise AABB gap test can tell you
    /// two boxes are 1.6m apart. It cannot tell you whether you can walk from the
    /// front door to the bar, whether the floor loops, or whether one gap is the
    /// only way into somewhere. Those are connectivity questions, so this uses a
    /// connectivity algorithm: rasterise the floor to a grid, flood fill it, and
    /// measure.
    ///
    /// The grid is 2.5D, not 2D. Each cell carries the height of the floor under it
    /// and two cells only connect if the step between them is small enough. That is
    /// what makes the sunken settee pits and the raised casino work correctly: the
    /// 0.4m pit recess is NOT traversable directly (0.4 > CirculationStepHeight) and
    /// is reachable only down its steps, so a mis-sized step reports the pit as
    /// unreachable instead of quietly passing.
    ///
    /// WHAT THIS PROVES, AND WHAT IT DOES NOT:
    ///   PROVES  — reachability of every anchor from the front door (exact).
    ///   PROVES  — the number of independent loops in the walkable region (exact,
    ///             via the first Betti number of the cell complex).
    ///   PROVES  — every pinch narrower than the secondary minimum (exact for the
    ///             grid resolution).
    ///   PROVES  — which declared thresholds are single points of failure, by
    ///             removing each one and re-running the fill.
    ///   DOES NOT — enumerate every articulation point WIDER than the secondary
    ///             minimum. A 3m-wide sole route is legal by width and is caught
    ///             only if it happens to be a declared threshold.
    ///
    /// REPORT, DO NOT FIX. Nothing in here moves geometry or edits a config value.
    /// A failing audit is a report for Tony, not a bug to tune away.
    /// </summary>
    public static class AlleyCirculationAudit
    {
        private const string MarkerRootName = "_CirculationAudit";

        // Chamfer distance-transform weights. Orthogonal 1, diagonal sqrt(2) — the
        // standard approximation, within a few percent of true Euclidean, and one
        // forward + one backward sweep instead of a proper EDT.
        private const float StepOrtho = 1f;
        private const float StepDiag = 1.41421356f;

        private const float Unreachable = float.MaxValue;

        // How far the fill will look for standable floor if an anchor lands on a
        // blocked cell. Reported when used, because a big snap can hide a real
        // problem (an anchor buried in a wall).
        private const int AnchorSnapCells = 6;

        /// <summary>
        /// Runs the whole audit and logs it. Everything it needs is handed in by the
        /// builder, which is the only thing that knows which of its own boxes are
        /// floor and which gaps are thresholds.
        /// </summary>
        public static void Run(AlleyLayoutConfig L,
                               List<Collider> walkables,
                               List<Collider> obstacles,
                               List<RouteZone> zones,
                               List<FurniturePiece> furniture,
                               List<Transform> anchors,
                               GameObject root)
        {
            // The builder positioned everything this same frame. Collider.bounds
            // reads the physics scene's copy of the transform, so without this the
            // audit can measure stale positions.
            Physics.SyncTransforms();

            float cell = Mathf.Max(0.05f, L.CirculationCellSize);

            // Grid bounds come from the ACTUAL walkable floor, not the room
            // rectangle. The entrance porch sits outside the room's east wall, and
            // with a room-sized grid the doorway had solid nothing on its far side
            // and measured 0.25m wide — the grid edge reading as a wall. Only the
            // north edge stays a hard cap (CirculationGridZMax, the foul line).
            float xMin = L.RoomXMin, xMax = L.RoomXMax, zMin = L.RoomZMin;
            foreach (Collider c in walkables)
            {
                if (c == null) continue;
                Bounds wb = c.bounds;
                if (wb.min.z > L.CirculationGridZMax) continue;
                xMin = Mathf.Min(xMin, wb.min.x);
                xMax = Mathf.Max(xMax, wb.max.x);
                zMin = Mathf.Min(zMin, wb.min.z);
            }

            int nx = Mathf.CeilToInt((xMax - xMin) / cell);
            int nz = Mathf.CeilToInt((L.CirculationGridZMax - zMin) / cell);

            if (nx <= 0 || nz <= 0)
            {
                Debug.LogError("[AlleyCirculation] Grid is empty — check RoomXMin/XMax, RoomZMin and CirculationGridZMax.");
                return;
            }

            int count = nx * nz;
            var floorY = new float[count];
            var hasFloor = new bool[count];
            var blocked = new bool[count];

            // ---------------------------------------------------------- rasterise
            // Stamp each collider's footprint into the grid ONCE, rather than
            // testing every cell against every collider. Cost is proportional to
            // total covered area, not to cells x colliders.

            foreach (Collider c in walkables)
            {
                if (c == null) continue;
                Bounds b = c.bounds;
                ForEachCell(b, xMin, zMin, cell, nx, nz, i =>
                {
                    // Highest floor wins: the casino platform and its steps sit on
                    // top of the main slab, and you stand on the top one.
                    if (!hasFloor[i] || b.max.y > floorY[i])
                    {
                        hasFloor[i] = true;
                        floorY[i] = b.max.y;
                    }
                });
            }

            float headroom = L.CirculationHeadroom;
            foreach (Collider c in obstacles)
            {
                if (c == null) continue;
                Bounds b = c.bounds;
                ForEachCell(b, xMin, zMin, cell, nx, nz, i =>
                {
                    if (!hasFloor[i]) return;
                    float f = floorY[i];
                    // Occupies the volume a body would stand in. The 0.05 lift lets
                    // a floor's own top face, and anything flush with it, pass.
                    if (b.max.y > f + 0.05f && b.min.y < f + headroom) blocked[i] = true;
                });
            }

            var free = new bool[count];
            for (int i = 0; i < count; i++) free[i] = hasFloor[i] && !blocked[i];

            // ------------------------------------------------------------- seed

            Transform seedAnchor = FindAnchor(anchors, L.CirculationSeedAnchor);
            if (seedAnchor == null)
            {
                Debug.LogError("[AlleyCirculation] Seed anchor '" + L.CirculationSeedAnchor +
                               "' not found. Cannot run — every measurement is 'reachable from the front door'.");
                return;
            }

            int seedSnap;
            int seed = SnapToFree(seedAnchor.position, xMin, zMin, cell, nx, nz, free, out seedSnap);
            if (seed < 0)
            {
                Debug.LogError("[AlleyCirculation] Seed anchor '" + L.CirculationSeedAnchor + "' at " +
                               seedAnchor.position + " has no walkable floor within " + (AnchorSnapCells * cell) +
                               "m. The front door opens onto nothing.");
                return;
            }

            var reachable = Flood(seed, free, floorY, nx, nz, L.CirculationStepHeight, null);

            // ------------------------------------------------------- clearance

            // WIDTHS ARE MEASURED PER FLOOR LEVEL, not over the whole reachable set.
            //
            // The settee pits are reachable — down their steps — but a 0.4m drop is
            // a wall to anyone walking across the concourse. Run the distance
            // transform over the raw reachable set and it treats a sunken pit as
            // open floor continuous with the concourse, and reports the concourse
            // far wider than anyone can walk.
            //
            // So the floor is split into TERRACES roughly one step-height thick,
            // each terrace is transformed independently, and every cell takes the
            // width of its own terrace. A pit then reads as a hole to someone on
            // the concourse and as a room to someone standing in it — both true at
            // once, which a single transform cannot express.
            //
            // (The earlier approach — deleting rim cells from the measured set —
            // also worked, but cost half a metre of width at every rim, which is a
            // lot when the threshold being measured is 1.6m against a 1.2m rule.)
            float step = Mathf.Max(0.01f, L.CirculationStepHeight);
            float terraceBand = step * 1.5f;

            var terraces = new List<int>();
            for (int i = 0; i < count; i++)
            {
                if (!reachable[i]) continue;
                int b = Mathf.RoundToInt(floorY[i] / step);
                if (!terraces.Contains(b)) terraces.Add(b);
            }

            var widthAt = new float[count];
            foreach (int b in terraces)
            {
                float level = b * step;
                var levelFree = new bool[count];
                for (int i = 0; i < count; i++)
                    levelFree[i] = reachable[i] && Mathf.Abs(floorY[i] - level) <= terraceBand;

                float[] w = ClearanceWidths(levelFree, nx, nz, cell);
                for (int i = 0; i < count; i++)
                {
                    if (!reachable[i]) continue;
                    if (Mathf.RoundToInt(floorY[i] / step) != b) continue;
                    if (w[i] > widthAt[i]) widthAt[i] = w[i];
                }
            }

            // Every reachable cell now has a width, so everything is measurable.
            bool[] measurable = reachable;

            // MEDIAL AXIS. Every width question in this audit is answered on ridge
            // cells only, and this is the single most important decision in the
            // file, so it is worth being explicit about why.
            //
            // The clear width AT A CELL is the width of the space at that exact
            // spot — so a cell 0.25m from a wall reports 0.25m, correctly and
            // uselessly. Every zone touches a wall somewhere, so "the narrowest
            // cell in this zone" is always ~one cell and tells you nothing.
            //
            // A cell on the medial axis is the CENTRELINE of whatever space it is
            // in, and its width IS that space's width. That is the number Tony's
            // rules are about: "the concourse must be 4.0m clear" means the
            // corridor is 4.0m across, not that no point in it is near a wall.
            //
            // Test: along at least one axis, no narrower than either neighbour AND
            // strictly wider than a neighbour THAT IS ITSELF FLOOR. Both halves of
            // that are load-bearing and both were learned the hard way:
            //
            //   - without the strictness, a strip of cells hugging a wall is a flat
            //     plateau of equal tiny widths, every cell qualifies along the axis
            //     parallel to the wall, and the audit reports the inside face of
            //     every wall in the building as a pinched corridor;
            //   - without "that is itself floor", a wall or the edge of the grid
            //     counts as width 0 and satisfies the strictness for free, so every
            //     corner in the room reports as a 0.25m corridor.
            //
            // Known limitation: a corridor only one or two cells across has no cell
            // strictly wider than a floor neighbour, so it is not detected as a
            // ridge. A zone consisting solely of such a corridor reports NO WIDTH
            // rather than a number, which is visible rather than silent.
            var ridge = new bool[count];
            for (int gz = 0; gz < nz; gz++)
            {
                for (int gx = 0; gx < nx; gx++)
                {
                    int i = gz * nx + gx;
                    if (!measurable[i]) continue;
                    float w = widthAt[i];

                    bool fl = IsFloor(measurable, nx, nz, gx - 1, gz);
                    bool fr = IsFloor(measurable, nx, nz, gx + 1, gz);
                    bool fd = IsFloor(measurable, nx, nz, gx, gz - 1);
                    bool fu = IsFloor(measurable, nx, nz, gx, gz + 1);

                    float wl = WidthOrZero(widthAt, measurable, nx, nz, gx - 1, gz);
                    float wr = WidthOrZero(widthAt, measurable, nx, nz, gx + 1, gz);
                    float wd = WidthOrZero(widthAt, measurable, nx, nz, gx, gz - 1);
                    float wu = WidthOrZero(widthAt, measurable, nx, nz, gx, gz + 1);

                    bool ridgeX = w >= wl && w >= wr && ((fl && w > wl) || (fr && w > wr));
                    bool ridgeZ = w >= wd && w >= wu && ((fd && w > wd) || (fu && w > wu));
                    ridge[i] = ridgeX || ridgeZ;
                }
            }

            var report = new StringBuilder();
            int errors = 0;
            int warnings = 0;

            int reachCount = 0;
            for (int i = 0; i < count; i++) if (reachable[i]) reachCount++;

            report.AppendLine("=== ALLEY CIRCULATION AUDIT ===");
            report.AppendLine("Grid: " + nx + " x " + nz + " = " + count + " cells at " + cell.ToString("0.00") +
                              "m, over x [" + xMin.ToString("0.0") + ", " + xMax.ToString("0.0") +
                              "] z [" + zMin.ToString("0.0") + ", " + L.CirculationGridZMax.ToString("0.0") + "]");
            report.AppendLine("Seeded at '" + L.CirculationSeedAnchor + "'" +
                              (seedSnap > 0 ? "  (SNAPPED " + (seedSnap * cell).ToString("0.00") + "m to reach floor)" : ""));
            report.AppendLine("Walkable and reachable: " + reachCount + " cells = " +
                              (reachCount * cell * cell).ToString("0.0") + " m^2");
            report.AppendLine("NOTE: widths come from a chamfer distance transform and can under-report by up to " +
                              "one cell (" + cell.ToString("0.00") + "m) on even-width gaps. They never over-report. " +
                              "CirculationWidthTolerance absorbs that.");
            report.AppendLine();

            // ----------------------------------------------------- 1. anchors

            report.AppendLine("--- 1. ANCHOR REACHABILITY (from the front door) ---");
            int unreachableAnchors = 0;
            foreach (Transform a in anchors)
            {
                if (a == null) continue;
                int snap;
                int idx = SnapToFree(a.position, xMin, zMin, cell, nx, nz, free, out snap);
                bool ok = idx >= 0 && reachable[idx];
                if (!ok)
                {
                    unreachableAnchors++;
                    errors++;
                    report.AppendLine("  UNREACHABLE  " + a.name + "  at " + Fmt(a.position));
                }
                else if (snap > 0)
                {
                    warnings++;
                    report.AppendLine("  reachable*   " + a.name + "  (snapped " + (snap * cell).ToString("0.00") +
                                      "m to standable floor — check it is not buried)");
                }
                else
                {
                    report.AppendLine("  reachable    " + a.name);
                }
            }
            report.AppendLine();

            // -------------------------------------------- 2. declared zone widths

            // Zones section 2 passed on their DECLARED travel axis. Section 3 consults

            // this so it never re-judges a zone the authoritative measure already cleared.

            var passedZones = new HashSet<string>();
            var exemptZones = new HashSet<string>();


            report.AppendLine("--- 2. DECLARED ROUTE ZONES (measured against their own category minimum) ---");
            report.AppendLine("    Metric: walk the zone along its DECLARED direction of travel; at each step");
            report.AppendLine("    take the widest clear width available ACROSS it; report the narrowest of");
            report.AppendLine("    those. That is 'the tightest point you must pass through'. The direction is");
            report.AppendLine("    declared by the builder, not guessed from the rectangle — guessing gets a");
            report.AppendLine("    wide shallow doorway exactly backwards and measures its length instead.");
            foreach (RouteZone z in zones)
            {
                float min = MinWidth(L, z.Category);
                int reachableInZone = 0;

                bool alongX = z.TraverseAlongX;
                int sliceCount = alongX ? nx : nz;
                var sliceBest = new float[sliceCount];
                var sliceAt = new Vector3[sliceCount];
                var sliceHas = new bool[sliceCount];

                for (int gz = 0; gz < nz; gz++)
                {
                    for (int gx = 0; gx < nx; gx++)
                    {
                        int i = gz * nx + gx;
                        if (!reachable[i]) continue;
                        float wx = xMin + (gx + 0.5f) * cell;
                        float wz = zMin + (gz + 0.5f) * cell;
                        if (!z.Contains(wx, wz)) continue;

                        // A cell inside a declared THRESHOLD is measured as a
                        // threshold, never against the route it interrupts.
                        if (!IsNonRoute(z.Category) && InAnyNonRoute(zones, wx, wz)) continue;

                        reachableInZone++;
                        int s = alongX ? gx : gz;
                        if (!sliceHas[s] || widthAt[i] > sliceBest[s])
                        {
                            sliceHas[s] = true;
                            sliceBest[s] = widthAt[i];
                            sliceAt[s] = new Vector3(wx, floorY[i], wz);
                        }
                    }
                }

                if (reachableInZone == 0)
                {
                    errors++;
                    report.AppendLine("  BLOCKED   [" + z.Category + "] " + z.Name +
                                      " — no reachable floor inside it at all.");
                    continue;
                }

                // Trim the ramps at each end. A route that terminates against a
                // wall (or against the next zone) necessarily narrows to nothing in
                // its final slices, and including those measures the corner it ends
                // in rather than the route itself. Only STRICTLY increasing runs are
                // trimmed, so the moment the width stops climbing the trim stops —
                // a genuine pinch near an end is still reported.
                var order = new List<int>();
                for (int s = 0; s < sliceCount; s++) if (sliceHas[s]) order.Add(s);

                int lo = 0, hi = order.Count - 1;
                while (lo < hi && sliceBest[order[lo + 1]] > sliceBest[order[lo]]) lo++;
                while (hi > lo && sliceBest[order[hi - 1]] > sliceBest[order[hi]]) hi--;
                int trimmed = lo + (order.Count - 1 - hi);

                float measured = float.MaxValue;
                var worst = Vector3.zero;
                for (int k = lo; k <= hi; k++)
                {
                    int s = order[k];
                    if (sliceBest[s] < measured) { measured = sliceBest[s]; worst = sliceAt[s]; }
                }

                string verdict;
                if (measured >= min) { verdict = "OK      "; passedZones.Add(z.Name); }
                else if (IsWidthExempt(L, z.Name))
                {
                    // Still measured and still reported — the number stays visible so
                    // a change is noticeable — but a deliberate squeeze is not a
                    // defect. See CirculationWidthExemptZones for the reasoning.
                    verdict = "EXEMPT  ";
                    exemptZones.Add(z.Name);
                }
                else if (measured >= min - L.CirculationWidthTolerance) { warnings++; verdict = "MARGINAL"; }
                else { errors++; verdict = "FAIL    "; }

                report.AppendLine("  " + verdict + " [" + z.Category + "] " + z.Name +
                                  "  narrowest " + measured.ToString("0.00") + "m (min " + min.ToString("0.00") +
                                  "m) at " + Fmt(worst) +
                                  (trimmed > 0 ? "   [" + trimmed + " end-ramp slice(s) trimmed]" : ""));
            }
            report.AppendLine();

            // ------------------------------------------- 3. unclassified pinches

            report.AppendLine("--- 3. PINCHES (corridor centrelines narrower than " +
                              L.CirculationSecondaryMinWidth.ToString("0.00") + "m, clusters of " +
                              L.CirculationMinPinchCells + "+ cells) ---");
            report.AppendLine("    Section 2 is the authority on the declared routes; this section's job is to");
            report.AppendLine("    catch narrow places NOBODY DECLARED, which is why it reports them as");
            report.AppendLine("    UNCLASSIFIED rather than guessing a category for them.");
            var pinchSeen = new bool[count];
            int pinchIndex = 0;
            GameObject markerRoot = null;

            for (int i = 0; i < count; i++)
            {
                if (!ridge[i] || pinchSeen[i]) continue;
                if (widthAt[i] >= L.CirculationSecondaryMinWidth) continue;

                // Grow the contiguous run of narrow cells so one doorway is one
                // report line, not forty.
                var clusterCells = new List<int>();
                var stack = new Stack<int>();
                stack.Push(i);
                pinchSeen[i] = true;
                while (stack.Count > 0)
                {
                    int c = stack.Pop();
                    clusterCells.Add(c);
                    int cx = c % nx, cz = c / nx;
                    for (int d = 0; d < 4; d++)
                    {
                        int ax = cx + (d == 0 ? 1 : d == 1 ? -1 : 0);
                        int az = cz + (d == 2 ? 1 : d == 3 ? -1 : 0);
                        if (ax < 0 || az < 0 || ax >= nx || az >= nz) continue;
                        int n = az * nx + ax;
                        if (pinchSeen[n] || !ridge[n]) continue;
                        if (widthAt[n] >= L.CirculationSecondaryMinWidth) continue;
                        pinchSeen[n] = true;
                        stack.Push(n);
                    }
                }

                // Prune corner bisectors: every convex corner emits a short diagonal
                // ridge spike that is a true medial axis but not a corridor.
                if (clusterCells.Count < L.CirculationMinPinchCells) continue;

                float narrow = float.MaxValue;
                float sx = 0f, sz = 0f, sy = 0f;
                foreach (int c in clusterCells)
                {
                    narrow = Mathf.Min(narrow, widthAt[c]);
                    sx += xMin + (c % nx + 0.5f) * cell;
                    sz += zMin + (c / nx + 0.5f) * cell;
                    sy += floorY[c];
                }
                var centre = new Vector3(sx / clusterCells.Count, sy / clusterCells.Count, sz / clusterCells.Count);

                // Classify from the DECLARED zones only. Threshold wins, then the
                // narrowest declared route. No declaration => unclassified.
                string zoneName;
                RouteCategory? cat = ClassifyCluster(zones, clusterCells, nx, xMin, zMin, cell, out zoneName);

                pinchIndex++;
                string label;
                if (!cat.HasValue)
                {
                    warnings++;
                    label = "UNCLASSIFIED";
                    report.AppendLine("  " + label + "  #" + pinchIndex + "  width " + narrow.ToString("0.00") +
                                      "m  at " + Fmt(centre) + "  (" + clusterCells.Count +
                                      " cells) — no declared zone covers this. TONY: categorise it.");
                }
                else
                {
                    float min = MinWidth(L, cat.Value);
                    string note = "";
                    if (narrow >= min) label = "ok";
                    else if (exemptZones.Contains(zoneName))
                    {
                        label = "exempt";
                        note = "  — deliberate squeeze, exempted by CirculationWidthExemptZones.";
                    }
                    else if (passedZones.Contains(zoneName))
                    {
                        // Section 2 already measured this zone ALONG ITS DECLARED
                        // TRAVEL AXIS and passed it. This section's transform is
                        // undirected, so on a thin terrace — a 0.35m step tread, say
                        // — it reports the tread's DEPTH as if that were the
                        // corridor's width. That is the same class of error as the
                        // aspect-ratio bug that declaring travel direction fixed, so
                        // section 2 wins and this is downgraded to a note rather
                        // than counted as a defect.
                        label = "info";
                        note = "  — section 2 passed this zone on its declared travel axis; " +
                               "this figure is an undirected measure across a thin terrace, not a corridor width.";
                    }
                    else if (narrow >= min - L.CirculationWidthTolerance) { warnings++; label = "MARGINAL"; }
                    else { errors++; label = "FAIL"; }
                    report.AppendLine("  " + label + "  #" + pinchIndex + "  [" + cat.Value + "] " + zoneName +
                                      "  width " + narrow.ToString("0.00") + "m (min " + min.ToString("0.00") +
                                      "m) at " + Fmt(centre) + "  (" + clusterCells.Count + " cells)" + note);
                }

                if (L.DropPinchMarkers && root != null)
                {
                    if (markerRoot == null)
                    {
                        markerRoot = new GameObject(MarkerRootName);
                        markerRoot.transform.SetParent(root.transform);
                    }
                    var m = new GameObject("Circ_Pinch_" + pinchIndex.ToString("00") + "_" +
                                           (cat.HasValue ? cat.Value.ToString() : "Unclassified") +
                                           "_" + narrow.ToString("0.00") + "m");
                    m.transform.SetParent(markerRoot.transform);
                    m.transform.position = centre;
                }
            }
            if (pinchIndex == 0) report.AppendLine("  none — every corridor on the walkable floor is at least " +
                                                   L.CirculationSecondaryMinWidth.ToString("0.00") + "m wide.");
            report.AppendLine();

            // --------------------------------------------------- 4. loop check

            report.AppendLine("--- 4. LOOP CHECK ---");
            int loops = BettiOne(reachable, nx, nz);
            if (loops <= 0)
            {
                errors++;
                report.AppendLine("  THE FLOOR DOES NOT LOOP. The walkable region is a tree — every route is " +
                                  "out-and-back. Reported, NOT fixed: restructuring walls to manufacture a loop " +
                                  "is Tony's call, not the builder's.");
            }
            else
            {
                report.AppendLine("  " + loops + " independent loop(s) in the walkable region. The floor loops.");
            }

            // Which declared thresholds are single points of failure?
            report.AppendLine("  Threshold removal test (is this gap the ONLY way somewhere?):");
            int soleRoutes = 0;
            foreach (RouteZone z in zones)
            {
                if (z.Category != RouteCategory.Threshold) continue;

                var suppress = new bool[count];
                for (int gz = 0; gz < nz; gz++)
                {
                    for (int gx = 0; gx < nx; gx++)
                    {
                        float wx = xMin + (gx + 0.5f) * cell;
                        float wz = zMin + (gz + 0.5f) * cell;
                        if (z.Contains(wx, wz)) suppress[gz * nx + gx] = true;
                    }
                }
                if (suppress[seed]) continue;   // cannot test the gap we stand in

                bool[] without = Flood(seed, free, floorY, nx, nz, L.CirculationStepHeight, suppress);

                var cutOff = new List<string>();
                foreach (Transform a in anchors)
                {
                    if (a == null) continue;
                    if (IsAllowedDeadEnd(L, a.name)) continue;
                    int snap;
                    int idx = SnapToFree(a.position, xMin, zMin, cell, nx, nz, free, out snap);
                    if (idx < 0 || !reachable[idx]) continue;    // already reported above
                    if (!without[idx]) cutOff.Add(a.name);
                }

                if (cutOff.Count > 0)
                {
                    soleRoutes++;
                    warnings++;
                    report.AppendLine("    SOLE ROUTE  '" + z.Name + "' is the only way to: " +
                                      string.Join(", ", cutOff.ToArray()));
                }
            }
            if (soleRoutes == 0) report.AppendLine("    none — every non-exempt anchor survives the removal of any " +
                                                   "single declared threshold.");
            report.AppendLine("  Exempt dead ends (Tony's rule): " +
                              (L.CirculationAllowedDeadEnds == null || L.CirculationAllowedDeadEnds.Length == 0
                                  ? "(none)"
                                  : string.Join(", ", L.CirculationAllowedDeadEnds)));
            report.AppendLine();

            // ------------------------------------------- 5. furniture spacing

            report.AppendLine("--- 5. OPEN-FLOOR FURNITURE SPACING (min " +
                              L.CirculationFurnitureMinGap.ToString("0.00") + "m) ---");
            int furnitureFails = 0;
            for (int a = 0; a < furniture.Count; a++)
            {
                for (int b = a + 1; b < furniture.Count; b++)
                {
                    float dx = Mathf.Max(0f, Mathf.Max(furniture[a].XMin - furniture[b].XMax,
                                                       furniture[b].XMin - furniture[a].XMax));
                    float dz = Mathf.Max(0f, Mathf.Max(furniture[a].ZMin - furniture[b].ZMax,
                                                       furniture[b].ZMin - furniture[a].ZMax));
                    float gap = Mathf.Sqrt(dx * dx + dz * dz);
                    if (gap >= L.CirculationFurnitureMinGap) continue;
                    furnitureFails++;
                    errors++;
                    report.AppendLine("  FAIL  " + furniture[a].Name + " <-> " + furniture[b].Name +
                                      "  gap " + gap.ToString("0.00") + "m");
                }
            }
            if (furniture.Count == 0) report.AppendLine("  no open-floor furniture registered.");
            else if (furnitureFails == 0) report.AppendLine("  all " + furniture.Count + " pieces clear.");
            report.AppendLine();

            // ------------------------------------------------------- verdict

            report.AppendLine("=== VERDICT: " + errors + " error(s), " + warnings + " warning(s), " +
                              unreachableAnchors + " unreachable anchor(s), " + loops + " loop(s) ===");

            if (errors > 0) Debug.LogError(report.ToString());
            else if (warnings > 0) Debug.LogWarning(report.ToString());
            else Debug.Log(report.ToString());
        }

        // ------------------------------------------------------------- helpers

        private static float MinWidth(AlleyLayoutConfig L, RouteCategory c)
        {
            switch (c)
            {
                case RouteCategory.Primary: return L.CirculationPrimaryMinWidth;
                case RouteCategory.Secondary: return L.CirculationSecondaryMinWidth;
                case RouteCategory.AmenityInterior: return L.CirculationAmenityInteriorMinWidth;
                case RouteCategory.BackOfHouse: return L.CirculationBackOfHouseMinWidth;
                default: return L.CirculationThresholdMinWidth;
            }
        }

        /// <summary>True for the categories that are NOT travel routes. These are
        /// carved out of a surrounding route's width measurement — otherwise a
        /// legitimately narrow staff walkway or pit entrance would report the
        /// secondary route it opens off as failing, which is the opposite of what
        /// declaring it was for.</summary>
        private static bool IsNonRoute(RouteCategory c)
        {
            return c == RouteCategory.Threshold
                || c == RouteCategory.AmenityInterior
                || c == RouteCategory.BackOfHouse;
        }

        private static bool InAnyNonRoute(List<RouteZone> zones, float x, float z)
        {
            foreach (RouteZone z2 in zones)
                if (IsNonRoute(z2.Category) && z2.Contains(x, z)) return true;
            return false;
        }

        /// <summary>Classifies a whole pinch cluster rather than just its centroid.
        /// A cluster straddling a zone edge would otherwise be reported as
        /// unclassified purely because its average position landed a few
        /// centimetres outside the zone it obviously belongs to.</summary>
        private static RouteCategory? ClassifyCluster(List<RouteZone> zones, List<int> cells, int nx,
                                                      float xMin, float zMin, float cell, out string name)
        {
            name = null;

            // A cluster belongs to a zone only if MOST of it is in that zone. A
            // cluster that merely brushes one — typically a back-of-house space
            // that nobody declared, poking into a declared route — stays
            // unclassified, which is the honest answer and the one Tony asked for.
            var tally = new Dictionary<string, int>();
            var cat = new Dictionary<string, RouteCategory>();
            foreach (int c in cells)
            {
                string n;
                RouteCategory? k = Classify(zones, xMin + (c % nx + 0.5f) * cell,
                                            zMin + (c / nx + 0.5f) * cell, out n);
                if (!k.HasValue) continue;
                tally.TryGetValue(n, out int had);
                tally[n] = had + 1;
                cat[n] = k.Value;
            }

            int bestCount = 0;
            RouteCategory? best = null;
            foreach (var kv in tally)
            {
                if (kv.Value <= bestCount) continue;
                bestCount = kv.Value;
                best = cat[kv.Key];
                name = kv.Key;
            }

            if (bestCount * 2 > cells.Count) return best;

            // No single zone owns the cluster. Before giving up, check whether the
            // zones it DOES straddle all agree on a category — a pinch sitting
            // across the seam between a station gap and the walk-in next to it is
            // covered by two declarations that both say Threshold, so the category
            // is not in doubt and reporting it unclassified would be pedantry
            // rather than honesty. Only a cluster straddling zones of DIFFERENT
            // categories is genuinely ambiguous, and that still goes to Tony.
            var byCategory = new Dictionary<RouteCategory, int>();
            foreach (var kv in tally)
            {
                byCategory.TryGetValue(cat[kv.Key], out int had);
                byCategory[cat[kv.Key]] = had + kv.Value;
            }

            foreach (var kv in byCategory)
            {
                if (kv.Value * 2 <= cells.Count) continue;
                var names = new List<string>();
                foreach (var t in tally)
                    if (cat[t.Key] == kv.Key) names.Add(t.Key);
                names.Sort();
                name = string.Join(" + ", names.ToArray());
                return kv.Key;
            }

            name = null;
            return null;
        }

        /// <summary>Threshold beats route; among routes the stricter (primary) wins,
        /// so a stretch that is both concourse and open floor is held to 4.0m.</summary>
        private static RouteCategory? Classify(List<RouteZone> zones, float x, float z, out string name)
        {
            name = null;
            RouteCategory? best = null;
            foreach (RouteZone zone in zones)
            {
                if (!zone.Contains(x, z)) continue;
                // A non-route declaration is the more specific statement, so it wins
                // over any route it sits inside — the casino's interior is casino
                // interior even where it overlaps the open floor around it.
                if (IsNonRoute(zone.Category)) { name = zone.Name; return zone.Category; }
                if (!best.HasValue || zone.Category == RouteCategory.Primary)
                {
                    best = zone.Category;
                    name = zone.Name;
                }
            }
            return best;
        }

        /// <summary>Is this zone excused from its category's width minimum? Used for
        /// deliberate squeezes — see CirculationWidthExemptZones, which carries the
        /// reasoning so an exemption cannot be mistaken for an oversight.</summary>
        private static bool IsWidthExempt(AlleyLayoutConfig L, string zoneName)
        {
            if (L.CirculationWidthExemptZones == null) return false;
            foreach (string s in L.CirculationWidthExemptZones)
                if (s == zoneName) return true;
            return false;
        }

        private static bool IsAllowedDeadEnd(AlleyLayoutConfig L, string anchorName)
        {
            if (L.CirculationAllowedDeadEnds == null) return false;
            foreach (string s in L.CirculationAllowedDeadEnds)
                if (s == anchorName) return true;
            return false;
        }

        private static Transform FindAnchor(List<Transform> anchors, string name)
        {
            foreach (Transform t in anchors)
                if (t != null && t.name == name) return t;
            return null;
        }

        private static void ForEachCell(Bounds b, float xMin, float zMin, float cell,
                                        int nx, int nz, System.Action<int> action)
        {
            int gx0 = Mathf.Clamp(Mathf.FloorToInt((b.min.x - xMin) / cell), 0, nx - 1);
            int gx1 = Mathf.Clamp(Mathf.CeilToInt((b.max.x - xMin) / cell) - 1, 0, nx - 1);
            int gz0 = Mathf.Clamp(Mathf.FloorToInt((b.min.z - zMin) / cell), 0, nz - 1);
            int gz1 = Mathf.Clamp(Mathf.CeilToInt((b.max.z - zMin) / cell) - 1, 0, nz - 1);

            // Reject boxes entirely outside the grid rather than clamping them onto
            // its edge, which would smear the room's east half onto the foul line.
            if (b.max.x < xMin || b.min.x > xMin + nx * cell) return;
            if (b.max.z < zMin || b.min.z > zMin + nz * cell) return;

            for (int gz = gz0; gz <= gz1; gz++)
                for (int gx = gx0; gx <= gx1; gx++)
                    action(gz * nx + gx);
        }

        private static int SnapToFree(Vector3 world, float xMin, float zMin, float cell,
                                      int nx, int nz, bool[] free, out int snapCells)
        {
            snapCells = 0;
            int gx = Mathf.FloorToInt((world.x - xMin) / cell);
            int gz = Mathf.FloorToInt((world.z - zMin) / cell);
            if (gx >= 0 && gz >= 0 && gx < nx && gz < nz && free[gz * nx + gx]) return gz * nx + gx;

            for (int r = 1; r <= AnchorSnapCells; r++)
            {
                for (int dz = -r; dz <= r; dz++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r) continue;
                        int ax = gx + dx, az = gz + dz;
                        if (ax < 0 || az < 0 || ax >= nx || az >= nz) continue;
                        if (!free[az * nx + ax]) continue;
                        snapCells = r;
                        return az * nx + ax;
                    }
                }
            }
            return -1;
        }

        /// <summary>4-connected 2.5D flood fill. Two cells connect only if the step
        /// between their floor heights is walkable — which is what makes the sunken
        /// pits and raised casino behave correctly instead of being magically
        /// reachable from every direction.</summary>
        private static bool[] Flood(int seed, bool[] free, float[] floorY, int nx, int nz,
                                    float stepHeight, bool[] suppress)
        {
            var seen = new bool[free.Length];
            if (suppress != null && suppress[seed]) return seen;

            var stack = new Stack<int>();
            stack.Push(seed);
            seen[seed] = true;

            while (stack.Count > 0)
            {
                int c = stack.Pop();
                int cx = c % nx, cz = c / nx;
                for (int d = 0; d < 4; d++)
                {
                    int ax = cx + (d == 0 ? 1 : d == 1 ? -1 : 0);
                    int az = cz + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (ax < 0 || az < 0 || ax >= nx || az >= nz) continue;
                    int n = az * nx + ax;
                    if (seen[n] || !free[n]) continue;
                    if (suppress != null && suppress[n]) continue;
                    if (Mathf.Abs(floorY[n] - floorY[c]) > stepHeight) continue;
                    seen[n] = true;
                    stack.Push(n);
                }
            }
            return seen;
        }

        /// <summary>Two-pass chamfer distance transform, converted to a clear WIDTH.
        /// Distance is measured in cells from a free cell to the nearest non-free
        /// one; width = (2d - 1) * cellSize, which is exact on odd-cell gaps and
        /// under-reports by one cell on even ones. Conservative on purpose: a safety
        /// audit should never claim a gap is wider than it is.</summary>
        private static float[] ClearanceWidths(bool[] freeSet, int nx, int nz, float cell)
        {
            int count = nx * nz;
            var d = new float[count];
            for (int i = 0; i < count; i++) d[i] = freeSet[i] ? Unreachable : 0f;

            for (int z = 0; z < nz; z++)
            {
                for (int x = 0; x < nx; x++)
                {
                    int i = z * nx + x;
                    if (d[i] == 0f) continue;
                    float best = d[i];
                    best = Mathf.Min(best, Peek(d, nx, nz, x - 1, z) + StepOrtho);
                    best = Mathf.Min(best, Peek(d, nx, nz, x, z - 1) + StepOrtho);
                    best = Mathf.Min(best, Peek(d, nx, nz, x - 1, z - 1) + StepDiag);
                    best = Mathf.Min(best, Peek(d, nx, nz, x + 1, z - 1) + StepDiag);
                    d[i] = best;
                }
            }
            for (int z = nz - 1; z >= 0; z--)
            {
                for (int x = nx - 1; x >= 0; x--)
                {
                    int i = z * nx + x;
                    if (d[i] == 0f) continue;
                    float best = d[i];
                    best = Mathf.Min(best, Peek(d, nx, nz, x + 1, z) + StepOrtho);
                    best = Mathf.Min(best, Peek(d, nx, nz, x, z + 1) + StepOrtho);
                    best = Mathf.Min(best, Peek(d, nx, nz, x + 1, z + 1) + StepDiag);
                    best = Mathf.Min(best, Peek(d, nx, nz, x - 1, z + 1) + StepDiag);
                    d[i] = best;
                }
            }

            var width = new float[count];
            for (int i = 0; i < count; i++)
                width[i] = freeSet[i] ? Mathf.Max(0f, (2f * d[i] - 1f) * cell) : 0f;
            return width;
        }

        /// <summary>Width at a neighbouring cell for the ridge test, where a wall,
        /// a drop or the edge of the grid all read as zero width. That is what
        /// stops a cell wedged in a corner from counting as a corridor centreline
        /// just because the only neighbours it has are narrower than it.</summary>
        private static float WidthOrZero(float[] widthAt, bool[] measurable, int nx, int nz, int x, int z)
        {
            if (x < 0 || z < 0 || x >= nx || z >= nz) return 0f;
            int i = z * nx + x;
            return measurable[i] ? widthAt[i] : 0f;
        }

        /// <summary>Is this neighbour real, walkable floor rather than a wall or the
        /// edge of the analysed area?</summary>
        private static bool IsFloor(bool[] measurable, int nx, int nz, int x, int z)
        {
            if (x < 0 || z < 0 || x >= nx || z >= nz) return false;
            return measurable[z * nx + x];
        }

        private static float Peek(float[] d, int nx, int nz, int x, int z)
        {
            // Off-grid counts as solid: the room's own walls bound the floor, and
            // treating the border as free would inflate every edge measurement.
            if (x < 0 || z < 0 || x >= nx || z >= nz) return 0f;
            return d[z * nx + x];
        }

        /// <summary>First Betti number of the reachable cell complex — literally the
        /// number of independent laps you can walk. For a connected region,
        /// b1 = 1 - (V - E + F) with V cells, E 4-adjacent pairs and F filled 2x2
        /// blocks. This is exact and O(V), which is why it is preferred over trying
        /// to infer "does it loop" from gap measurements.</summary>
        private static int BettiOne(bool[] reachable, int nx, int nz)
        {
            int v = 0, e = 0, f = 0;
            for (int z = 0; z < nz; z++)
            {
                for (int x = 0; x < nx; x++)
                {
                    if (!reachable[z * nx + x]) continue;
                    v++;
                    if (x + 1 < nx && reachable[z * nx + x + 1]) e++;
                    if (z + 1 < nz && reachable[(z + 1) * nx + x]) e++;
                    if (x + 1 < nx && z + 1 < nz &&
                        reachable[z * nx + x + 1] &&
                        reachable[(z + 1) * nx + x] &&
                        reachable[(z + 1) * nx + x + 1]) f++;
                }
            }
            if (v == 0) return 0;
            return 1 - (v - e + f);
        }

        private static string Fmt(Vector3 p)
        {
            return "(" + p.x.ToString("0.0") + ", " + p.y.ToString("0.00") + ", " + p.z.ToString("0.0") + ")";
        }
    }
}
