using UnityEngine;

namespace WeeSpurts.Bowling
{
    /// <summary>
    /// Every knob for the scripted Wii-Sports-style throw camera lives here, so
    /// the whole cinematic move can be retuned in the Inspector with the game
    /// running and ZERO code changes (CodingStandards: config = ScriptableObject).
    ///
    /// THE SEVEN BEATS, in the order they play:
    ///   A   YOU'RE UP      front-on close-up of the thrower  (start of a TURN only)
    ///   A2  TAKE STANCE    swing behind them, down-lane aim view (holds until you charge)
    ///   B   PULL BACK      swing back and up to the widest shot (starts on SPACE down)
    ///   C   SWING THROUGH  ease lower + closer, holds until you release
    ///   D   RELEASE        drop low, push out over the lane (starts on SPACE up)
    ///   E   TRAVEL         ride down the lane with the ball (no timer — see below)
    ///   F   IMPACT         arrive at the pins exactly as the ball does. Move ends.
    ///
    /// A note on DURATIONS: beats A, A2, B, C and D are timed. Beats E and F are
    /// NOT — they are driven by where the BALL is. That is deliberate: ball speed
    /// changes with power (4 to 14 m/s), so any timer would desync from the ball
    /// on a soft or a monster throw. E follows the ball's position; F is
    /// parameterised by how far the ball still has to go.
    /// </summary>
    [CreateAssetMenu(fileName = "ThrowCameraSequenceConfig", menuName = "WeeSpurts/Throw Camera Sequence Config")]
    public class ThrowCameraSequenceConfig : ScriptableObject
    {
        // -----------------------------------------------------------------
        [Header("Shared")]
        // -----------------------------------------------------------------

        [Tooltip("Seconds to ease back to the start-of-roll framing (beat A or A2) when a new roll begins, and after a fumble that never reached the pins. Raise it for a lazier, more cinematic reset; lower it if the game feels like it's waiting for the camera. Never a snap cut — that's the whole point of this knob.")]
        public float ReturnDuration = 0.75f;

        [Tooltip("Meters. The camera is never allowed closer than this to the thrower on the ground plane during the swing beats (A2/B/C) or the release beat (D). Raise it if the camera clips through the thrower's body; lower it for a tighter, more claustrophobic swing.")]
        public float MinThrowerClearance = 0.9f;

        // -----------------------------------------------------------------
        [Header("Beat A — YOU'RE UP (start of a turn only)")]
        // -----------------------------------------------------------------

        [Tooltip("Seconds beat A HOLDS on the thrower's face after easing in, before it automatically swings around to beat A2. This is the 'you're up' pause. Raise it to let the moment breathe (and to give a clip a proper opening shot); lower it if turns feel slow. Note: the ease INTO this pose uses Return Duration above.")]
        public float ADuration = 0.9f;

        [Tooltip("Shape of the ease into beat A. Flat-then-steep = a lazy drift that arrives fast; the default S-curve is a smooth camera-operator move. Leave it alone unless the move feels mechanical.")]
        public AnimationCurve ACurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("Camera position for beat A, as a world-space offset FROM the thrower. +Z is down the lane (in front of them), +Y is up, +X is to their right. The default puts the camera down-lane looking back at them, so the venue is behind their head. Push Z up for a wider 'you're up'; push it down to get right in their face.")]
        public Vector3 APositionOffset = new Vector3(0.7f, 0.5f, 1.8f);

        [Tooltip("What beat A looks at, as a world-space offset FROM the thrower. The thrower proxy's pivot sits at the floor+1m, so a small +Y here is roughly chest/chin height. Raise Y to frame the face, lower it to frame the ball in their hands.")]
        public Vector3 ALookOffset = new Vector3(0f, 0.35f, 0f);

        // -----------------------------------------------------------------
        [Header("Beat A2 — TAKE STANCE (the aim view; holds until you charge)")]
        // -----------------------------------------------------------------

        [Tooltip("Seconds for the swing from beat A around behind the thrower into the aim view. Raise it for a grander arc; lower it to get to aiming faster. (Entering A2 at the START of a roll uses Return Duration instead.)")]
        public float A2Duration = 0.8f;

        [Tooltip("Shape of the swing into beat A2.")]
        public AnimationCurve A2Curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("NUDGE ONLY. Added on top of ThrowCamera's existing Aim View Position. Leave at zero and beat A2 lands exactly on the aim framing the game already used, so AimPreview's guide line reads correctly. Use small values (±0.3) to fine-tune; don't retype the whole aim view here.")]
        public Vector3 A2PositionOffset = Vector3.zero;

        [Tooltip("NUDGE ONLY. Added on top of ThrowCamera's existing Aim View Euler (degrees). X tilts down/up, Y pans left/right. Leave at zero to match the existing aim view exactly.")]
        public Vector3 A2EulerOffset = Vector3.zero;

        // -----------------------------------------------------------------
        [Header("Beat B — SLOW PUSH IN (starts when you hold SPACE, runs till release)")]
        // -----------------------------------------------------------------
        // This beat replaced the old "pull back to a wide shot" AND the old
        // beat C ("swing through"). It is now ONE slow dolly forward onto the
        // thrower that keeps creeping in for as long as the player holds the
        // power button, which is why there is no beat C any more.

        [Tooltip("Seconds for the slow push in. Charge time is player-controlled, so this is deliberately LONGER than a typical hold: the camera should still be creeping forward at the moment most players release. Once it does arrive it simply holds, so an extra-long hold never runs out of move. Lower it for a quicker, more aggressive push.")]
        public float BZoomDuration = 2.5f;

        [Tooltip("Shape of the push in. Keep this close to linear — the whole point is a slow steady creep. A steep curve makes the camera lunge and then stop, which kills the tension.")]
        public AnimationCurve BZoomCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("Degrees around the thrower. 0 = camera down-lane IN FRONT of them, 180 = directly BEHIND them. Leave this at 180 to push straight in with no rotation at all. Only change it if you want the push to curve round to one shoulder as it closes — every degree away from 180 adds sideways swing.")]
        public float BZoomAngleDegrees = 180f;

        [Tooltip("Meters behind the thrower the push ENDS at. This is the main knob for this beat: the camera starts at the aim view (about 2.9m back) and closes to this. Lower it to end tighter on the character's back; raise it towards 2.9 for barely any push at all. Cannot go closer than Min Thrower Clearance.")]
        public float BZoomRadius = 1.5f;

        [Tooltip("Meters above the thrower's pivot that the push ends at. Slightly below the aim view's height reads as the camera dropping in behind their shoulder as it closes.")]
        public float BZoomHeight = 1f;

        [Tooltip("Meters down the lane (from the thrower) that this beat aims at. Keep it well down-lane so the shot stays pointed at the pins while it closes in, rather than staring at the back of the character's head.")]
        public float BZoomLookAheadDistance = 9f;

        [Tooltip("Height (relative to the thrower's pivot) of this beat's look point. Lower it to point further down at the lane surface.")]
        public float BZoomLookHeight = 0.2f;

        // -----------------------------------------------------------------
        [Header("Beat D — RELEASE (starts the instant you let go)")]
        // -----------------------------------------------------------------

        [Tooltip("Seconds for the whip around the thrower and out over the lane. This beat swings roughly 170 degrees, so it is the fastest camera move in the game — LOWER it for a snappier, more violent release; RAISE it (0.6+) if the whip makes anyone queasy. Too long and the camera is still leaving the thrower while the ball is halfway down the lane.")]
        public float DDuration = 0.45f;

        [Tooltip("Shape of the release move. A steep-early curve reads as the camera being 'thrown' out over the lane with the ball.")]
        public AnimationCurve DCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("Meters above the floor the camera drops to for the release. LOW is the point — this is the shot that makes the lane fill the bottom of the frame. Raise it if the lane surface reads as a wall.")]
        public float DHeight = 0.7f;

        [Tooltip("Meters down the lane (from the ball's spawn point) the camera pushes out to. Raise it to get further out over the lane ahead of the ball; lower it to stay near the foul line. Note the ball will overtake the camera here on a powerful throw — that whoosh-past is intentional.")]
        public float DDistanceDownLane = 1.4f;

        [Tooltip("Meters sideways from the lane centre for the release shot. 0 keeps it dead centre. A small offset (±0.3) gives the shot a bit of attitude.")]
        public float DLateralOffset = 0f;

        [Tooltip("Meters ahead of the CAMERA that beat D looks. Raise it to point further down the lane (flatter, more 'here it comes'); lower it to look down at the lane just ahead.")]
        public float DLookAheadDistance = 7f;

        [Tooltip("Height of beat D's look point above the floor. Low values tip the camera down onto the lane surface.")]
        public float DLookHeight = 0.3f;

        [Tooltip("How far SIDEWAYS the release move bulges as it passes the thrower. The camera has to get from behind them to out in front, and a dead-straight path would go through their body — this pushes the MIDDLE of that path out to one side so it slips past cleanly. Positive = pass on their right, negative = pass on their left, 0 = straight through them (don't). Raise it if the camera clips the thrower; lower it if the sidestep is distracting. It is a bulge, not a swing: zero at both ends, so it never changes where the beat starts or finishes.")]
        public float DPassSideOffset = 1.2f;

        // -----------------------------------------------------------------
        [Header("Beat E — TRAVEL (no timer — driven by the ball)")]
        // -----------------------------------------------------------------

        [Tooltip("Seconds to blend from beat D's fixed pose into ball-tracking, so there is no pop. After this the camera is locked to the ball's position and there is NO timer at all — a slow ball and a rocket both stay framed. Raise it for a softer hand-off; lower it to lock onto the ball sooner.")]
        public float EBlendDuration = 0.5f;

        [Tooltip("Shape of the hand-off from beat D into ball-tracking.")]
        public AnimationCurve EBlendCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("Where the camera sits RELATIVE TO THE BALL while travelling. -Z is behind the ball, +Y is above it. The default sits just behind and slightly above, low to the lane. Push Z more negative to hang back (ball reads smaller, pins read bigger); raise Y for a more god's-eye chase.")]
        public Vector3 ETravelOffset = new Vector3(0f, 0.75f, -2.6f);

        [Tooltip("Meters ahead of the BALL that the travel shot looks. Small values stare at the ball; larger values look down the lane past it, so the pins grow in frame. 1.5 matches the camera the prototype already used.")]
        public float ELookAheadDistance = 1.5f;

        [Tooltip("How much the travel shot follows the ball SIDEWAYS. 0 = camera stays locked to the lane centreline (calm, lets the ball drift across frame — good for reading a hook). 1 = camera fully tracks the ball's X (the ball stays glued to the centre of frame, the lane swings around instead). Lower it if hooks feel invisible; raise it if the ball leaves frame.")]
        [Range(0f, 1f)] public float ELateralFollow01 = 0.6f;

        // -----------------------------------------------------------------
        [Header("Travel cap — how far down the lane the camera may EVER go")]
        // -----------------------------------------------------------------

        [Tooltip("Wii Sports-style: the camera stops part-way down the lane and lets the ball run the rest of the way to the pins on its own, rather than riding it all the way in. Reads as 'the camera let it go' and keeps the pin deck at a watchable middle distance. Switch off to let beat F dolly right up to the rack instead.")]
        public bool CapCameraTravel = true;

        [Tooltip("How far down the lane the camera is allowed to travel, as a fraction of the distance from the foul line to the pins. 0.75 = three quarters (the Wii-ish default), 1 = all the way to the rack. The camera STOPS here but keeps looking down-lane, so the ball visibly runs away from it into the pins. NOTE: while this cap is doing anything, it overrides the distance solved from 'F Lanes In Frame' below — you cannot both stop short and frame tight without changing the field of view, which this camera deliberately never does.")]
        [Range(0.1f, 1f)] public float TravelCapLane01 = 0.75f;

        // -----------------------------------------------------------------
        [Header("Beat F — IMPACT (driven by the ball's distance to the pins)")]
        // -----------------------------------------------------------------

        [Tooltip("Meters short of the pin deck at which the impact move begins. The camera then arrives at its final pose EXACTLY as the ball reaches the pins, at any ball speed, because this beat is measured in distance-to-go rather than seconds. Raise it for a longer, grander approach; lower it if the move feels like it starts too early.")]
        public float FImpactLeadDistance = 5.5f;

        [Tooltip("Shape of the impact approach, evaluated against how far the ball still has to travel (0 = just triggered, 1 = ball at the pins). A late-steep curve makes the camera hang back then rush in on the last metre.")]
        public AnimationCurve FCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("How many LANE WIDTHS fill the screen at impact. The camera's distance from the pins is SOLVED from this plus the camera's field of view and the screen's aspect ratio, so the shot frames identically on any monitor. 3.25 = our lane centred with slivers of the neighbours at the edges. Lower it (2.5) to go tight on the pins; raise it (4.5) to show off the whole alley.")]
        public float FLanesInFrame = 3.25f;

        [Tooltip("Height above the floor for the impact shot. Around pin-top height reads as 'in among them'; raise it for a scoreboard-style look-down.")]
        public float FImpactHeight = 1.05f;

        [Tooltip("Height above the pin deck floor that the impact shot aims at. Roughly half a pin height keeps the whole rack in frame.")]
        public float FLookHeight = 0.55f;

        [Tooltip("Seconds to finish the impact move if the ball stops just short of the pins (a dying roll that never quite arrives). Without this the camera would freeze mid-move waiting for a ball that isn't coming. Rarely needs changing.")]
        public float FCompleteDuration = 0.35f;

        // -----------------------------------------------------------------
        [Header("Safety nets — stop the camera hanging on a bad throw")]
        // -----------------------------------------------------------------

        [Tooltip("Meters down the lane the ball must have travelled before the impact beat is allowed to fire at all. A backward fumble or a ball that barely leaves the hand should stay with the disaster (that's the joke), not fly off to film an untouched rack (that reads as a bug). Raise it to be stricter about what counts as a real throw.")]
        public float MinProgressZ = 2f;

        [Tooltip("Seconds without the ball making any forward progress before the camera gives up and eases back to the stance framing. Covers gutter balls that wedge, backward fumbles, and balls that stop dead. Lower it to recover faster; raise it to linger on the disaster longer.")]
        public float StallTimeoutSeconds = 2.5f;

        [Tooltip("Meters of forward movement that counts as 'still making progress' for the stall watchdog above. Tiny by design — this only exists so physics jitter doesn't read as movement.")]
        public float StallProgressEpsilon = 0.05f;

        [Tooltip("Hard ceiling in seconds on the whole post-release part of the move, no matter what the ball is doing. The last line of defence against the camera getting stuck. Should comfortably exceed BallConfig's Throw Timeout (9s by default).")]
        public float HardTimeoutSeconds = 12f;
    }
}
