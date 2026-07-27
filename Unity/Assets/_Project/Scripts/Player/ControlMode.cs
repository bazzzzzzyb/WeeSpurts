namespace WeeSpurts.Player
{
    /// <summary>
    /// What a single player's avatar is currently doing. This is PER-PLAYER
    /// state, never global: in a networked match one player can be at the line
    /// bowling while everyone else is still walking around the alley heckling
    /// (Docs/OpenQuestions.md — "free-roam continues for everyone even
    /// mid-game; only the active player gets pulled").
    ///
    /// It lives on <see cref="PlayerAvatar"/>, and BowlingMatchFlow owns
    /// the MATCH, never anyone's mode. If you ever find yourself writing
    /// "the game is in bowling mode", that's the bug this enum exists to stop.
    /// </summary>
    public enum ControlMode
    {
        /// <summary>Walking around the venue in first person. You own your camera.</summary>
        Roaming,

        /// <summary>At the foul line. The bowling camera and ThrowerAimSlide own you.</summary>
        Bowling
    }
}
