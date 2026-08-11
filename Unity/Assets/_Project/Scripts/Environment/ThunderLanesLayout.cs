using UnityEngine;

namespace WeeSpurts.Environment
{
    /// <summary>
    /// PURE DATA for THUNDER LANES — the second venue greybox, built to a
    /// different thesis from <see cref="AlleyLayoutConfig"/>. Both exist; walk
    /// them back to back and keep the one that feels right.
    ///
    /// COORDINATE FRAME — identical to AlleyLayoutConfig, deliberately, so the
    /// two venues are directly comparable and the real lane never moves:
    ///   origin = the FOUL LINE on the PLAYABLE lane's centreline
    ///   +Z     = down-lane toward the pins        -Z = the venue floor
    ///   y = 0  = the walking surface
    ///   x = 0  = the PLAYABLE lane's centre, NOT the middle of the room
    ///   north  = -X (lane 1)   south = +X (lane 8)
    ///
    /// THE DESIGN THESIS, because the numbers below only make sense with it:
    ///
    /// A bowling centre is a SHED. The lanes need a ~25m column-free run, so the
    /// building is a big-span box and everything else is fitted into what's left
    /// over. The lanes are the primitive; the amenities are residual. AlleyLayout
    /// v2 stacked everything into parallel Z bands, which is tidy and reads as
    /// bands. This one is organised as a STREET instead: one concourse running
    /// north-south, with the bar, snack, shoe desk and pro shop as SHOPFRONTS on
    /// it. You never walk into "the bar room" — you walk along a street and the
    /// bar is a frontage.
    ///
    /// Four moves carry it, and if you change nothing else, keep these:
    ///   1. CEILING HEIGHT ZONES, NOT WALLS. 5.5m over the lanes, 4.5m over the
    ///      concourse, 3.2m soffit over the counters, 2.8m in the arcade. You
    ///      feel the ceiling drop walking into the arcade. This does more for
    ///      "real place" than any prop.
    ///   2. THE STREET JOGS. It pinches to 4.0m at the shoe desk, opens to 7.5m
    ///      at the centre plaza, settles at 4.5m past the bar. A constant-width
    ///      route reads as a corridor; a varying one reads as a street.
    ///   3. THREE FLOOR LEVELS, no more. Concourse 0, pits -0.4, casino +0.45.
    ///   4. ONE 45-DEGREE ELEMENT. The entry vestibule is rotated, so your first
    ///      frame is a diagonal across the whole floor. One angled thing stops
    ///      the room reading as a box.
    ///
    /// EIGHT LANES, not twelve (Tony, 2026-08-04). Twelve at 2.0m pitch is 24m
    /// of building width where exactly ONE lane is playable. Eight still reads
    /// as a proper bank in the turn-start reveal and hands 8m back to the entry
    /// sequence and the arcade. PlayableLaneIndex stays 6, so five lanes sit
    /// north of you and two south — asymmetric on purpose, because a symmetric
    /// bank reads as a corridor in the reveal shot.
    /// </summary>
    [CreateAssetMenu(fileName = "ThunderLanesLayout", menuName = "WeeSpurts/Thunder Lanes Layout")]
    public class ThunderLanesLayout : ScriptableObject
    {
        // ----------------------------------------------------------- lane bank

        [Header("Lane bank")]
        [Tooltip("Lanes shown, including the one real playable lane. Eight is the considered number — see the class comment. Even, so ball returns pair up.")]
        [Range(2, 16)] public int LaneCount = 8;

        [Tooltip("Which slot (1-based, counting from the north wall) is the REAL playable lane at x = 0. Six of eight puts you two lanes from the south end, close to the entrance and the arcade, with the long view north.")]
        [Range(1, 16)] public int PlayableLaneIndex = 6;

        [Tooltip("Metres between lane centres. Kept at AlleyLayout's 2.0 on Tony's instruction so the two venues stay comparable.")]
        public float LanePitch = 2f;

        [Tooltip("Gutter width either side of a BACKDROP lane. The real lane keeps its own wider rails from GreyboxSceneBuilder.")]
        public float BackdropGutterWidth = 0.3f;

        [Tooltip("Height of a backdrop lane's gutter rail. Lower than the real lane's 0.35 so the slimmer rails don't read as walls.")]
        public float BackdropRailHeight = 0.28f;

        [Tooltip("Length of a backdrop lane. Should match LaneConfig.Length (18) or the bank looks ragged from the approach.")]
        public float BackdropLaneLength = 18f;

        // ------------------------------------------------------------- Z bands

        [Header("Z bands — west of the foul line (all POSITIVE depths)")]
        [Tooltip("Depth of the APPROACH, measured west from the foul line. NOTHING may be built in it: the throw camera's aim view sits at z = -3.7 and the widest sequence beat reaches -2.3. The builder ERRORS if anything lands here.")]
        public float ApproachDepth = 4.6f;

        [Tooltip("Safety margin the approach must keep behind the aim camera at z = -3.7. Build-time error if ApproachDepth is under 3.7 + this.")]
        public float ApproachCameraMargin = 0.6f;

        [Tooltip("Depth of the ball-return / score-console band, one unit per lane PAIR.")]
        public float BallReturnDepth = 2f;

        [Tooltip("Depth of the sunken seating band — the per-lane-pair pits with the semicircle table and swivel seats.")]
        public float PitBandDepth = 5f;

        [Tooltip("Nominal depth of the concourse. The street JOGS around this: it pinches to ConcoursePinchWidth and opens to ConcoursePlazaWidth. This is the baseline the jog is measured from.")]
        public float ConcourseDepth = 6.4f;

        [Tooltip("Narrowest the street gets, at the shoe desk. Held at exactly 4.0m — the primary-route minimum from AlleyLayout's circulation rules — so the pinch reads without breaking your own standard.")]
        public float ConcoursePinchWidth = 4f;

        [Tooltip("Widest the street gets, at the centre plaza where the ball racks and lounge clusters sit. The release after the pinch.")]
        public float ConcoursePlazaWidth = 7.5f;

        [Tooltip("Depth of the FRONTAGE band — the shopfronts. Counter plus the customer side plus the staff side of each counter.")]
        public float FrontageDepth = 7.5f;

        [Tooltip("Depth of the BACK-OF-HOUSE ribbon behind the frontages: one continuous service corridor linking bar cellar, dry store and staff room to a loading door. In a real building this is ONE spine, not three separate rooms — that is most of why v1 read as boxes.")]
        public float BackOfHouseDepth = 3f;

        [Header("Z bands — east of the foul line")]
        [Tooltip("Where the pin deck ends and the machine room begins. GreyboxSceneBuilder puts its backstop at LaneConfig.Length + 2.5, so this should stay at or past 20.5.")]
        public float PinDeckEndZ = 20.5f;

        [Tooltip("Depth of the PINSETTER ROOM behind the pin decks. Accessible (Tony's call) — bare block, fluoro tubes, a mechanic's catwalk. Runs the full width of the lane bank.")]
        public float PinsetterRoomDepth = 5f;

        // ------------------------------------------------------------- X zones

        [Header("X zones")]
        [Tooltip("Clear distance north of lane 1 to the north wall — a service walkway, and the mouth of the card seller's squeeze.")]
        public float NorthWalkwayWidth = 4f;

        [Tooltip("Distance south of the last lane to the south wall. This is the whole entry sequence plus the arcade alcove plus the restrooms, so it is the biggest single X allowance in the building.")]
        public float SouthZoneWidth = 14f;

        // ------------------------------------------------------------- heights

        [Header("Ceiling heights — this is what zones the building")]
        [Tooltip("Clear height over the lanes, approach and pits. Tall and long-span, structure exposed. The single most important number for making it read as a bowling centre rather than an office.")]
        public float CeilingLanes = 5.5f;

        [Tooltip("Clear height over the concourse street.")]
        public float CeilingConcourse = 4.5f;

        [Tooltip("Dropped soffit over the counters. The drop is what makes a frontage feel like a shopfront instead of a wall.")]
        public float CeilingFrontage = 3.2f;

        [Tooltip("Arcade alcove. DELIBERATELY LOW and the lowest number in the building — the ceiling drop as you step in is the whole effect.")]
        public float CeilingArcade = 2.8f;

        [Tooltip("Casino lounge, measured from its RAISED floor. Intimate.")]
        public float CeilingCasino = 3.6f;

        [Tooltip("Pinsetter room. Industrial.")]
        public float CeilingPinsetter = 4f;

        [Tooltip("Back-of-house corridor.")]
        public float CeilingBackOfHouse = 3f;

        [Tooltip("Wall thickness. Walls straddle their line by half this.")]
        public float WallThickness = 0.3f;

        // -------------------------------------------------------- floor levels

        [Header("Floor levels — exactly three, resist adding a fourth")]
        [Tooltip("How far the seating pits sit BELOW the concourse.")]
        public float PitRecess = 0.4f;

        [Tooltip("How far the casino lounge sits ABOVE the concourse. Elevated with a wood balustrade (Tony's call).")]
        public float CasinoRise = 0.45f;

        [Tooltip("Steps in each casino stair run. Rise per step is CasinoRise / count.")]
        [Range(2, 5)] public int CasinoStepCount = 3;

        [Tooltip("Tread depth of a casino step.")]
        public float CasinoStepRun = 0.3f;

        [Tooltip("Steps down into a seating pit, cut into the pit's west edge.")]
        [Range(1, 3)] public int PitStepCount = 2;

        // ---------------------------------------------------------- seating

        [Header("Seating — two KINDS, doing two different jobs")]
        [Tooltip("Build the sunken per-lane-pair pits: semicircle table, fixed swivel seats on posts, score console. This is YOUR GROUP's spot for the frame you're playing.")]
        public bool BuildLanePits = true;

        [Tooltip("Swivel seats around one pit's semicircle table.")]
        [Range(3, 8)] public int SwivelSeatsPerPit = 5;

        [Tooltip("Radius of a pit's semicircle table. 0.6 gives a 1.2m top — a real bowling settee table. The first build had this at 0.85 and a 1.7m disc reads as a mushroom, because it is almost as wide as the 2.0m lane pitch next to it.")]
        public float PitTableRadius = 0.6f;

        [Tooltip("Width in X of one pit. The gap left between adjacent pits is (2 * LanePitch) - this, and counts as a THRESHOLD not a corridor.")]
        public float PitWidth = 3.2f;

        [Tooltip("Free-standing lounge clusters on the concourse plaza — two three-seat couches facing each other with a low table between. NOT tied to any lane. This is the important one: it is where you sit to watch SOMEONE ELSE bowl, and right now every seat in the other venue points at one lane.")]
        [Range(0, 5)] public int LoungeClusterCount = 3;

        [Tooltip("Length of one couch in a lounge cluster.")]
        public float CouchLength = 2.1f;

        [Tooltip("Gap between the two facing couches — the low table sits in it.")]
        public float CouchFacingGap = 1.3f;

        // ---------------------------------------------------------- amenities

        [Header("Frontages — shopfronts on the street, north to south")]
        [Tooltip("X width of the raised casino lounge at the north end.")]
        public float CasinoWidth = 6f;

        [Tooltip("X width of the bar frontage.")]
        public float BarWidth = 6f;

        [Tooltip("X width of the snack bar / grill frontage.")]
        public float SnackWidth = 5f;

        [Tooltip("X width of the shoe rental + front desk. The street pinches to ConcoursePinchWidth opposite this, because a queue here is the one predictable crowd in the building.")]
        public float ShoeDeskWidth = 5f;

        [Tooltip("X width of the pro shop / cosmetics frontage.")]
        public float ProShopWidth = 4f;

        [Tooltip("Counter height for bar, snack, shoe desk and pro shop.")]
        public float CounterHeight = 1.05f;

        [Tooltip("Depth of a counter slab. The rest of the frontage band is customer side in front and staff side behind.")]
        public float CounterDepth = 0.7f;

        [Header("Arcade alcove")]
        [Tooltip("Build the arcade. It is an ALCOVE off the south end, not a row against a wall — its low ceiling and its own mouth are what make it a discovered place.")]
        public bool BuildArcade = true;

        [Tooltip("Cabinets along the arcade walls.")]
        [Range(3, 14)] public int ArcadeCabinetCount = 8;

        [Tooltip("Width of one cabinet.")]
        public float ArcadeCabinetWidth = 0.75f;

        [Tooltip("Depth of one cabinet.")]
        public float ArcadeCabinetDepth = 0.9f;

        [Tooltip("Height of one cabinet. Tall enough to block sightlines from inside the alcove, which is the point — the arcade should feel separate.")]
        public float ArcadeCabinetHeight = 1.8f;

        [Tooltip("Clear aisle inside the arcade. 2.5m secondary-route width, because people stand and queue at cabinets.")]
        public float ArcadeAisleWidth = 2.5f;

        [Header("Entry")]
        [Tooltip("Rotation of the entry vestibule in degrees. THE one angled element in the building — set it to 0 and the room goes back to reading as a box.")]
        [Range(0f, 60f)] public float EntryAngleDegrees = 45f;

        [Tooltip("Clear width of the entrance doors.")]
        public float EntryDoorWidth = 2f;

        [Tooltip("Door height, used for entry and every internal doorway.")]
        public float DoorHeight = 2.3f;

        [Header("Card seller")]
        [Tooltip("Clear width of the card seller's squeeze, off the north walkway behind the casino stair. DELIBERATELY under the 1.2m threshold minimum — the squeeze is the point, same exemption AlleyLayout already grants its card dealer alcove.")]
        public float CardSellerWidth = 0.9f;

        [Header("Audit thresholds — reported, never auto-fixed")]
        [Tooltip("Primary route minimum. The concourse is checked against this.")]
        public float MinPrimaryWidth = 4f;

        [Tooltip("Secondary route minimum — arcade aisle, north walkway.")]
        public float MinSecondaryWidth = 2.5f;

        [Tooltip("Threshold minimum — pit entrances, doorways, counter walk-ins.")]
        public float MinThresholdWidth = 1.2f;

        // ---------------------------------------------------------------
        // Derived geometry. The builder reads THESE, never raw fields, so
        // moving one depth slides everything west of it and nothing can
        // silently overlap.
        // ---------------------------------------------------------------

        public float ApproachZMin => -ApproachDepth;
        public float BallReturnZMax => ApproachZMin;
        public float BallReturnZMin => BallReturnZMax - BallReturnDepth;
        public float PitZMax => BallReturnZMin;
        public float PitZMin => PitZMax - PitBandDepth;
        public float ConcourseZMax => PitZMin;
        public float ConcourseZMin => ConcourseZMax - ConcourseDepth;
        public float FrontageZMax => ConcourseZMin;
        public float FrontageZMin => FrontageZMax - FrontageDepth;
        public float BackOfHouseZMax => FrontageZMin;
        public float RoomZMin => BackOfHouseZMax - BackOfHouseDepth;
        public float PinsetterZMin => PinDeckEndZ;
        public float RoomZMax => PinsetterZMin + PinsetterRoomDepth;

        /// <summary>Centre X of lane <paramref name="index"/> (1-based from the north wall).</summary>
        public float LaneCentreX(int index) => (index - PlayableLaneIndex) * LanePitch;

        public float LaneBankXMin => LaneCentreX(1) - LanePitch * 0.5f;
        public float LaneBankXMax => LaneCentreX(LaneCount) + LanePitch * 0.5f;
        public float RoomXMin => LaneBankXMin - NorthWalkwayWidth;
        public float RoomXMax => LaneBankXMax + SouthZoneWidth;

        public float RoomWidth => RoomXMax - RoomXMin;
        public float RoomDepth => RoomZMax - RoomZMin;

        /// <summary>Number of lane PAIRS, which is how many pits and ball returns get built.</summary>
        public int PairCount => LaneCount / 2;

        /// <summary>Tallest ceiling in the building — what the exterior walls are built to.</summary>
        public float MaxCeiling => Mathf.Max(CeilingLanes, Mathf.Max(CeilingConcourse, CeilingPinsetter));
    }
}
