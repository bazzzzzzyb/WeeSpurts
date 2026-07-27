using UnityEngine;

namespace WeeSpurts.Environment
{
    /// <summary>
    /// PURE DATA for the bowling alley venue greybox — the room around the lanes
    /// (approach, ball returns, settee pits, concourse, service spine, casino,
    /// card dealer, cosmetics, entrance). No logic lives here; AlleyGreyboxBuilder
    /// reads these numbers and builds boxes from them, so Tony can shove the whole
    /// venue around by feel in the Inspector and re-run the menu item.
    ///
    /// COORDINATE FRAME (read from GreyboxSceneBuilder — do not "fix" this):
    ///   origin  = the FOUL LINE, on the PLAYABLE lane's centreline
    ///   +Z      = down-lane toward the pins (PinDeck sits at z = LaneConfig.Length)
    ///   -Z      = the main venue floor, behind the thrower
    ///   y = 0   = the walking surface (the greybox floor's top face is y = -0.001)
    ///   x = 0   = the PLAYABLE lane's centre — NOT the centre of the room.
    ///
    /// Compass mapping used by the blueprint this was built from:
    ///   north = -X (lane 1)   south = +X (lane 12)
    ///   west  = -Z (floor)    east  = +Z (pins)
    ///
    /// LAYOUT v2 — THE VENUE IS BANDED. Everything west of the foul line is a
    /// stack of Z bands, each with its own depth field. Walking east from the west
    /// wall you cross: the west band (service spine + casino + card dealer), the
    /// concourse, the settee pits, the ball returns, and finally the approach —
    /// which is COMPLETELY CLEAR because that is where the throw camera lives.
    /// The band Z extents are DERIVED from the depths (see the properties at the
    /// bottom), so moving one depth slides everything west of it and nothing can
    /// silently overlap.
    ///
    /// This is a RUNTIME ScriptableObject (not editor-only) on purpose: the anchor
    /// markers the builder drops are meant to be referenced later by real gameplay
    /// (the walk-up-to-a-lane game start and the drag-to-the-line turn transition
    /// in OpenQuestions.md), so the type has to exist in a build.
    /// </summary>
    [CreateAssetMenu(fileName = "AlleyLayout", menuName = "WeeSpurts/Alley Layout Config")]
    public class AlleyLayoutConfig : ScriptableObject
    {
        // ---------------------------------------------------------------- room

        [Header("Room shell (metres, in the lane's coordinate frame)")]
        [Tooltip("North wall. Lane 1 sits at the north edge of the bank, so this must stay north (more negative) of it.")]
        public float RoomXMin = -13.7f;

        [Tooltip("South wall. Everything south of lane 12 — the cosmetics counter and the entrance — lives between the bank and this.")]
        public float RoomXMax = 17f;

        [Tooltip("West wall, behind the throwers. MUST be west of (more negative than) WestBandZMin or the band stack does not fit inside the room — the builder errors if it doesn't. At the v2 defaults the bands sum to exactly 19.8m, so this sits right on WestBandZMin with zero slack.")]
        public float RoomZMin = -20.3f;

        [Tooltip("East wall, behind the pins. Must clear GreyboxSceneBuilder's Backstop, which sits at LaneConfig.Length + 2.5.")]
        public float RoomZMax = 21.5f;

        [Tooltip("Wall height. Tall enough to read as a room from the throw camera without boxing in a debug fly-cam.")]
        public float WallHeight = 3.5f;

        [Tooltip("Wall thickness. Walls are centred on the Room bounds, so they straddle the line by half this.")]
        public float WallThickness = 0.3f;

        [Tooltip("Top face of the venue floor. Deliberately a few mm BELOW the greybox floor's top (-0.001) so the two overlapping slabs never z-fight. Invisible as a step; a character controller will not notice.")]
        public float FloorTopY = -0.005f;

        // -------------------------------------------------------- band structure

        [Header("Band structure (depths in metres, west of the foul line)")]
        [Tooltip("Depth of the APPROACH band, measured west from the foul line. NOTHING may be built inside it — not a collider, not a renderer, not an anchor. This is where the throw camera lives: the aim view sits at z = -3.7 and the widest sequence beat reaches z = -2.3, so this must stay comfortably deeper than 3.7. The builder ERRORS if it doesn't.")]
        public float ApproachClearDepth = 4.5f;

        [Tooltip("Safety margin the approach band must keep behind the aim camera at z = -3.7. Build-time error if ApproachClearDepth < 3.7 + this.")]
        public float ApproachCameraMargin = 0.6f;

        [Tooltip("Depth of the BALL RETURN band — the strip of paired ball-return islands at the head of each settee pit, exactly where a real alley puts them.")]
        public float BallReturnBandDepth = 1.8f;

        [Tooltip("Depth of the SETTEE PIT band. Note the pit steps eat into its west edge, so usable sunken depth is this minus SetteePitStepCount * SetteePitStepRun.")]
        public float SetteePitDepth = 3.5f;

        [Tooltip("Depth of the CONCOURSE — the primary walking route, running the full width of the room. Tony's circulation rules give this a 4.0m clear minimum, which the audit enforces.")]
        public float ConcourseDepth = 4f;

        [Tooltip("Depth of the WEST BAND, which holds the service spine, the casino nook and the card dealer alcove side by side in X. The spine only uses ~2.85m of it; the rest is the lobby floor in front of the counters.")]
        public float WestBandDepth = 6.5f;

        // ----------------------------------------------------------- lane bank

        [Header("Lane bank")]
        [Tooltip("How many lanes the venue shows, including the real playable one. Settee pits and ball returns are built per PAIR, so an odd count leaves the last lane without one (the builder warns).")]
        [Range(1, 24)] public int LaneCount = 12;

        [Tooltip("Which slot (1-based, counting from the north edge) is the REAL playable lane at x = 0. Everything else is backdrop.")]
        [Range(1, 24)] public int PlayableLaneIndex = 6;

        [Tooltip("Metres between lane centres. Backdrop lanes use slim gutter rails (see BackdropGutterWidth) so this can go much tighter than LaneConfig.NeighbourLaneSpacing, which has to clear the real lane's chunky rails. Also sets the PAIR pitch (2x this), which is what the settee pits are sized against.")]
        public float LanePitch = 2f;

        [Tooltip("Gutter width either side of a BACKDROP lane surface. The real lane keeps its own wider rail geometry from GreyboxSceneBuilder — nobody reads the difference at backdrop distance.")]
        public float BackdropGutterWidth = 0.3f;

        [Tooltip("Height of a backdrop lane's gutter rail. Lower than the real lane's 0.35 so the slimmer rails do not look like walls.")]
        public float BackdropRailHeight = 0.28f;

        // ---------------------------------------------------------- settee pits

        [Header("Settee pits (one per lane PAIR, sunken)")]
        [Tooltip("Build the pits at all. Off gives a flat pit band and a much emptier venue.")]
        public bool BuildSetteePits = true;

        [Tooltip("Width in X of a pit. The gap left between two adjacent pits is (2 * LanePitch) - this, and that gap is registered as a THRESHOLD (a pit entrance, 1.2m minimum) rather than a corridor — see Tony's circulation rules.")]
        public float SetteePitWidth = 2.4f;

        [Tooltip("How far the pit floor is sunk below the walking surface. The main floor slab is TILED around the pits rather than holed, so this can move freely.")]
        public float SetteePitRecess = 0.4f;

        [Tooltip("Steps down into the pit, cut into the pit band's WEST edge. Rise per step is SetteePitRecess / (count + 1).")]
        [Range(1, 4)] public int SetteePitStepCount = 2;

        [Tooltip("Depth in Z of one step tread.")]
        public float SetteePitStepRun = 0.35f;

        [Tooltip("How far the U-bench stands off the pit wall it runs along. The U opens WEST, toward the steps.")]
        public float SetteePitBenchDepth = 0.55f;

        [Tooltip("Seat height above the PIT floor. At the default 0.45 with a 0.4 recess the seat top lands at y = +0.05, i.e. flush with the concourse — which is exactly how a real settee pit reads: you sit down onto what looks like the ground.")]
        public float SetteePitBenchHeight = 0.45f;

        [Tooltip("Radius of the low table in the middle of the pit.")]
        public float SetteePitTableRadius = 0.5f;

        [Tooltip("Height of the score-console panel above the PIT floor. Replaces v1's per-lane approach kiosk, which could not stay now that the approach band has to be empty.")]
        public float SetteePitConsoleHeight = 0.65f;

        // --------------------------------------------------------- ball returns

        [Header("Ball returns (one per lane PAIR)")]
        [Tooltip("Width in X of a return island, centred on the pair boundary.")]
        public float BallReturnWidth = 1.1f;

        [Tooltip("Height of the return's main body.")]
        public float BallReturnBodyHeight = 0.55f;

        [Tooltip("Height of the hood above the body.")]
        public float BallReturnHoodHeight = 0.4f;

        // -------------------------------------------------------------- casino

        [Header("Casino nook (NW, raised, inside the west band)")]
        public float CasinoXMin = -13.7f;
        public float CasinoXMax = -6f;

        [Tooltip("Kept as explicit fields rather than derived from the west band, so the nook can be pulled off the wall by feel. The builder warns if they fall outside the west band.")]
        public float CasinoZMin = -20.3f;
        public float CasinoZMax = -13.8f;

        [Tooltip("How far the casino floor is raised above the main floor.")]
        public float CasinoPlatformHeight = 0.6f;

        [Tooltip("Size of the 45-degree cut across the nook's SOUTH-EAST corner. 0 gives a square corner.")]
        public float CasinoChamfer = 2.5f;

        [Tooltip("Number of steps down the nook's east edge.")]
        [Range(2, 10)] public int CasinoStairSteps = 5;

        [Tooltip("Total depth (in Z) of the staircase, measured east from the nook's east edge — so the flight lands ON THE CONCOURSE and eats into it. The circulation audit reports what that leaves. Deliberately not 'fixed' by moving the flight inside the nook: that is Tony's call, not the builder's.")]
        public float CasinoStairRun = 1.6f;

        [Tooltip("Width (in X) of the staircase flight.")]
        public float CasinoStairWidth = 3f;

        [Tooltip("X of the staircase's north edge. Keep the whole flight (this + CasinoStairWidth) north of CasinoXMax - CasinoChamfer, or the top step will hang off the chamfered corner with no platform above it.")]
        public float CasinoStairXMin = -12f;

        [Tooltip("Slot machines along the nook's north wall.")]
        [Range(0, 8)] public int SlotMachineCount = 4;

        [Tooltip("Centre of the semicircular blackjack table, on the raised floor.")]
        public Vector2 BlackjackCentreXZ = new Vector2(-8.5f, -16.5f);

        [Tooltip("Radius of the blackjack table.")]
        public float BlackjackRadius = 1.4f;

        [Tooltip("How many chairs around the players' arc of the blackjack table.")]
        [Range(0, 10)] public int BlackjackChairCount = 5;

        // --------------------------------------------------------- card dealer

        [Header("Card dealer alcove (west band, opens EAST onto the lobby)")]
        public float CardDealerXMin = -5.4f;
        public float CardDealerXMax = -2f;
        public float CardDealerZMin = -19.8f;
        public float CardDealerZMax = -16.6f;

        [Tooltip("Height of the alcove's side walls. It is open to the EAST only (v1 opened south; the band restructure turned it 90 degrees).")]
        public float CardDealerWallHeight = 2.6f;

        // ------------------------------------------------------- service spine

        [Header("Service spine (front desk / snack bar / bar, one back-of-house)")]
        [Tooltip("Depth of the continuous back-shelving run behind ALL three stations. One unbroken box, not three — that is what makes the spine read as one building instead of a strip of kiosks.")]
        public float ServiceBackShelfDepth = 0.4f;

        [Tooltip("Height of the back-shelving run.")]
        public float ServiceBackShelfHeight = 1.9f;

        [Tooltip("Clear staff walkway between the shelving and the counters. Continuous along the whole spine, so one member of staff reaches all three stations without going round the front. No public anchor is ever placed in it, so the circulation audit never routes a customer through it.")]
        public float ServiceStaffWalkwayDepth = 1.6f;

        [Tooltip("Width of the walk-in gap at each station's NORTH end — a break in the counter run that lets staff into the walkway. Registered as a THRESHOLD (1.2m minimum), not a corridor.")]
        public float ServiceWalkInWidth = 1f;

        [Tooltip("Open gap in X between two adjacent stations. You can see straight through it into the staff walkway, which is what stops the three counters reading as one long bench.")]
        public float ServiceStationGap = 1.2f;

        [Tooltip("Put each station's walk-in gap at its north (-X) end. Off puts them at the south end.")]
        public bool ServiceWalkInAtNorthEnd = true;

        [Header("Service spine — station X extents")]
        [Tooltip("Bar sits next to the casino: drinks by the slots.")]
        public float BarXMin = -1f;
        public float BarXMax = 3.4f;

        public float SnackBarXMin = 4.6f;
        public float SnackBarXMax = 8f;

        [Tooltip("Front desk sits nearest the entrance, which is the SE corner — door, then desk, like a real alley.")]
        public float FrontDeskXMin = 9.2f;
        public float FrontDeskXMax = 13f;

        [Header("Counters (shared)")]
        [Tooltip("Counter height for the front desk, snack bar and cosmetics counter.")]
        public float CounterHeight = 1.1f;

        [Tooltip("Bar counter height — deliberately taller than the others so the spine's silhouette steps.")]
        public float BarCounterHeight = 1.15f;

        [Tooltip("Counter depth — how far a counter stands off the wall it runs along.")]
        public float CounterDepth = 0.85f;

        [Tooltip("How many stools along the bar counter's east (lobby-facing) side.")]
        [Range(0, 12)] public int BarStoolCount = 6;

        // -------------------------------------------------------------- tables

        [Header("Open floor seating")]
        [Tooltip("Centres of free-standing round tables, as (x, z). EMPTY BY DEFAULT IN v2 ON PURPOSE: the settee pits now hold the seating, and the lobby band left in front of the service spine is only 3.5m deep — a table plus its chair ring is ~2.6m across, which would break Tony's 2.5m secondary-route minimum. Drop coordinates in here and the circulation + furniture-spacing audits will tell you immediately whether they fit.")]
        public Vector2[] TableCentresXZ = new Vector2[0];

        [Tooltip("Radius of a round table.")]
        public float TableRadius = 0.75f;

        [Tooltip("Chairs placed evenly around each round table.")]
        [Range(0, 8)] public int ChairsPerTable = 4;

        // ------------------------------------------------- cosmetics + entrance

        [Header("Cosmetics counter (south wall, facing north)")]
        public float CosmeticsZMin = -11.5f;
        public float CosmeticsZMax = -6f;

        [Tooltip("Height of the sign box hung above the counter.")]
        public float CosmeticsSignHeight = 0.7f;

        [Header("Entrance (SE corner, double doors through the south wall)")]
        [Tooltip("The doorway is a gap cut out of the south wall between these two Z values. v2 moved it north of v1's position so it opens into the entrance vestibule rather than straight into the bar.")]
        public float EntranceZMin = -19f;
        public float EntranceZMax = -17f;

        [Tooltip("Height of the door leaves.")]
        public float DoorHeight = 2.2f;

        // ------------------------------------------------ safety: ball volume

        [Header("Safety: BALL exclusion volume (hard error)")]
        [Tooltip("Half-width in X of the volume around the real lane that NOTHING built by this script may put a collider inside. Covers the lane plus both of its rails.")]
        public float PlayVolumeHalfWidth = 1.6f;

        [Tooltip("West end of the BALL volume. v1 used -9 and that number was doing two different jobs at once: protecting the ball AND protecting the camera corridor. They are now separate checks.\n\nIMPORTANT — -1.5 is NOT a claim that the ball can never travel backwards. GreyboxSceneBuilder strips the thrower's collider precisely so a throw sent toward -Z is possible: the BACKWARD-FUMBLE GAG. A ball that rolls back off the approach and into a settee pit, scattering spectators, is Pillar 1 working as designed — intended comedy, not an oversight. Do NOT push this west to 'protect' the pits and returns from it. What this volume protects is the stretch the ball MUST traverse cleanly on every normal throw.")]
        public float PlayVolumeZMin = -1.5f;

        [Tooltip("East end of the exclusion volume — past the pin deck.")]
        public float PlayVolumeZMax = 20f;

        [Tooltip("Floor of the exclusion volume, just above the lane surface (top face y = 0.001). Below this the venue floor slab is allowed, since it is the ground the lane itself rests on.")]
        public float PlayVolumeYMin = 0.02f;

        [Tooltip("Ceiling of the exclusion volume.")]
        public float PlayVolumeYMax = 4f;

        // --------------------------------------------- safety: approach + camera

        [Header("Safety: APPROACH clear band (hard error)")]
        [Tooltip("Half-width of the strict approach check, which fails on ANY created object — collider, renderer OR anchor. Capped at 1.1 because the real lane's own rails (from GreyboxSceneBuilder, inner face at 1.125) legitimately run through this band and must not trip it. A second, wider check at PlayVolumeHalfWidth catches anything SOLID out to the rails.")]
        public float ApproachClearHalfWidth = 1.1f;

        [Header("Safety: camera corridor (advisory only)")]
        [Tooltip("Half-width of the informational listing of what Beat A frames behind the thrower at turn start. ~5.5m matches roughly what a 1.8m-back camera sees at pit distance.")]
        public float CameraCorridorHalfWidth = 5.5f;

        [Tooltip("West end of the camera-corridor listing. Objects in here are LEGAL and WANTED — this is the turn-start reveal of the social floor (GameBible §7, Tony's call 2026-07-26). The listing exists so a change to the venue never silently changes the opening shot.")]
        public float CameraCorridorZMin = -11f;

        [Tooltip("Log the camera-corridor contents each build. Informational only — it is never a defect.")]
        public bool WarnOnCameraCorridorContents = true;

        // -------------------------------------------------- circulation audit

        [Tooltip("How far a service-station gap's declared THRESHOLD zone reaches EAST past the counter line into the lobby floor. Without it the pinch cluster at each gap straddles the counter line, under half of it lands in the declared zone, and the audit reports it UNCLASSIFIED. Tony categorised these as thresholds (2026-07-26); this is what makes the declaration actually cover them.")]
        public float ServiceStationGapLobbyBleed = 0.8f;

        [Header("Circulation audit (Tony's five route categories)")]
        [Tooltip("Run the walkable-floor flood fill at all.")]
        public bool RunCirculationAudit = true;

        [Tooltip("Grid cell size for the flood fill. 0.25m over the social floor is ~9,600 cells — milliseconds. MEASURED WIDTHS CARRY ROUGHLY ONE CELL OF PESSIMISM: a route measures about cellSize narrower than it really is, verified by halving this (concourse read 3.75m at 0.25 and 3.88m at 0.125 against a true 4.00m). So a MARGINAL within one cell of its minimum is a measurement artefact, not a real violation — halve this and re-run to confirm before moving any geometry.")]
        public float CirculationCellSize = 0.25f;

        [Tooltip("Biggest height change a character is assumed to walk up. Deliberately BELOW SetteePitRecess (0.4) so a pit is only reachable via its steps — if a step is ever mis-sized the audit reports the pit unreachable instead of silently passing.")]
        public float CirculationStepHeight = 0.25f;

        [Tooltip("Headroom a cell needs to count as walkable.")]
        public float CirculationHeadroom = 1.8f;

        [Tooltip("PRIMARY ROUTE minimum clear width — the concourse.")]
        public float CirculationPrimaryMinWidth = 4f;

        [Tooltip("SECONDARY ROUTE minimum clear width — paths across the open floor, between the service spine and furniture, around the amenities.")]
        public float CirculationSecondaryMinWidth = 2.5f;

        [Tooltip("THRESHOLD minimum clear width — short pass-throughs, not travel routes: pit entrances, counter walk-ins, doorways, alcove mouths. EXEMPT from the secondary rule.")]
        public float CirculationThresholdMinWidth = 1.2f;

        [Tooltip("AMENITY INTERIOR minimum clear width — the space INSIDE a destination, between its furniture: the casino nook's slots and blackjack table, the card dealer alcove, the arcade. Tony added this category after v2's first audit reported those gaps as UNCLASSIFIED; the reasoning was that four players need to get AROUND a blackjack table, so an amenity you can see but not move inside is not a destination.")]
        public float CirculationAmenityInteriorMinWidth = 1.2f;

        [Tooltip("BACK-OF-HOUSE minimum clear width — the staff walkway behind a service counter. Getting behind a counter is a stunt, not a commute, so this is threshold-grade rather than a secondary route. Tony added this category after v2's first audit reported these gaps as UNCLASSIFIED.")]
        public float CirculationBackOfHouseMinWidth = 1.2f;

        [Tooltip("Minimum clear gap between two pieces of open-floor furniture.")]
        public float CirculationFurnitureMinGap = 2f;

        [Tooltip("Smallest medial-axis cluster reported as a pinch. Every 90-degree corner in the building emits a short diagonal spike on the medial axis — mathematically a real ridge, practically just the corner — and those spikes are only a few cells long. Six cells prunes them without hiding a real corridor. Drop it to 1 to see everything, including the corners.")]
        [Range(1, 40)] public int CirculationMinPinchCells = 6;

        [Tooltip("Measurement slack when comparing a measured width to a category minimum. Set to ONE GRID CELL: the chamfer distance transform under-reports even-width gaps by up to one cell (it never over-reports). A gap within this of its minimum is reported MARGINAL rather than FAIL, so a true 4.00m concourse measured as 3.75m does not read as a defect. Shrink CirculationCellSize if you want tighter numbers.")]
        public float CirculationWidthTolerance = 0.25f;

        [Tooltip("North edge of the analysed floor. Left at 0 (the foul line) on purpose: east of it is lane, not floor. Players ARE allowed to walk onto backdrop lanes (Tony's call) — capping the grid here is not a claim that they cannot, it is a statement that the lane bank is not a circulation route and including it would swamp every number.")]
        public float CirculationGridZMax = 0f;

        [Tooltip("Name of the anchor the flood fill seeds from. Everything is measured as 'reachable from the front door'.")]
        public string CirculationSeedAnchor = "Anchor_Entrance";

        [Tooltip("Anchors allowed to be dead ends — Tony's circulation rules exempt intentional ones. The casino has one staircase and the card dealer is an alcove; both are meant to be culs-de-sac.")]
        public string[] CirculationAllowedDeadEnds = { "Anchor_Casino", "Anchor_CardDealer" };

        [Tooltip("Route zones EXEMPT from their category's width minimum. The audit still measures and reports them — it just does not count the shortfall as a defect.\n\nCardDealerInterior is exempt by Tony's decision (2026-07-26), and this is NOT an oversight to tidy up later. The brief called the card dealer 'a deliberate dead-end, a good hiding spot'. At 0.83m you SLIP IN — you do not stroll in — and that squeeze is the joke. Widening it to a compliant 1.4m would make it read as just another shop and delete the thing it is for.\n\nTODO (blocked on characters existing): verify a player can get IN and back OUT of this alcove. A squeeze is a joke; two players permanently wedged in it is a bug. Re-check once there is a character controller with a real radius.")]
        public string[] CirculationWidthExemptZones = { "CardDealerInterior" };

        [Tooltip("Drop named empties in the scene at each reported pinch so you can click one in the Hierarchy and the Scene view frames it.")]
        public bool DropPinchMarkers = true;

        // ------------------------------------------------------ derived: bands

        /// <summary>West wall's INNER face — where wall-hugging geometry starts.</summary>
        public float WallInnerZ => RoomZMin + WallThickness * 0.5f;

        public float ApproachZMax => 0f;
        public float ApproachZMin => -ApproachClearDepth;

        public float BallReturnZMax => ApproachZMin;
        public float BallReturnZMin => BallReturnZMax - BallReturnBandDepth;

        public float SetteePitZMax => BallReturnZMin;
        public float SetteePitZMin => SetteePitZMax - SetteePitDepth;

        public float ConcourseZMax => SetteePitZMin;
        public float ConcourseZMin => ConcourseZMax - ConcourseDepth;

        public float WestBandZMax => ConcourseZMin;
        public float WestBandZMin => WestBandZMax - WestBandDepth;

        // ---------------------------------------------- derived: service spine

        public float ServiceBackShelfZMin => WallInnerZ;
        public float ServiceBackShelfZMax => WallInnerZ + ServiceBackShelfDepth;
        public float ServiceCounterZMin => ServiceBackShelfZMax + ServiceStaffWalkwayDepth;
        public float ServiceCounterZMax => ServiceCounterZMin + CounterDepth;

        /// <summary>East face of the counters — the customer side, and the west edge
        /// of the open lobby floor.</summary>
        public float LobbyZMin => ServiceCounterZMax;
        public float LobbyZMax => WestBandZMax;

        // -------------------------------------------------- derived: lane pairs

        /// <summary>World X of a lane slot's centreline. Slot indices are 1-based
        /// from the north edge, and the playable slot always lands on x = 0.</summary>
        public float LaneCentreX(int oneBasedIndex)
        {
            return (oneBasedIndex - PlayableLaneIndex) * LanePitch;
        }

        /// <summary>How many complete lane PAIRS the bank has. An odd LaneCount
        /// leaves the last lane without a pit or a return; the builder warns.</summary>
        public int LanePairCount => LaneCount / 2;

        /// <summary>World X of the boundary between the two lanes of a pair — the
        /// centreline a settee pit and its ball return are built on. Pair 0 is
        /// lanes 1+2, pair 1 is lanes 3+4, and so on.</summary>
        public float LanePairBoundaryX(int zeroBasedPair)
        {
            int laneA = zeroBasedPair * 2 + 1;
            return (LaneCentreX(laneA) + LaneCentreX(laneA + 1)) * 0.5f;
        }

        /// <summary>Rise of one settee-pit step. Matches the casino staircase's
        /// convention: count + 1 rises get you from the pit floor to the surface.</summary>
        public float SetteePitStepRise => SetteePitRecess / (SetteePitStepCount + 1f);
    }
}
