namespace WeeSpurts.Core
{
    /// <summary>
    /// Every sound id a script in this project is allowed to call by name.
    /// One place, so the editor tools that seed AudioCatalog and the call
    /// sites that play these sounds can never drift apart — add a sound here
    /// first, then wire the call site, then drop the clip in.
    /// </summary>
    public static class SoundId
    {
        public const string BallRoll = "ball_roll";
        public const string BallLaneImpact = "ball_lane_impact";
        public const string BallGutter = "ball_gutter";
        public const string PinCrash = "pin_crash";
        public const string TicketDispense = "ticket_dispense";
        public const string SlotReelStop = "slot_reel_stop";
        public const string SlotJackpot = "slot_jackpot";
        public const string UiClick = "ui_click";
        public const string AmbienceAlley = "ambience_alley";

        public static readonly string[] All =
        {
            BallRoll, BallLaneImpact, BallGutter, PinCrash, TicketDispense,
            SlotReelStop, SlotJackpot, UiClick, AmbienceAlley
        };
    }
}
