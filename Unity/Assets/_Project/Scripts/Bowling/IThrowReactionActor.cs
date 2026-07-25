namespace WeeSpurts.Bowling
{
    /// <summary>
    /// Reacts to how a throw went, purely cosmetically. A greybox capsule
    /// implements this now; a rigged Quaternius character can implement it
    /// later with real animations instead of transform tweens — callers
    /// never change. Deterministic: same LaunchParameters -> same target
    /// pose on every client (the TWEEN playback doesn't need to be
    /// frame-identical across clients, only the target pose does — same
    /// looseness PhysX itself already has per Docs/Networking.md).
    /// </summary>
    public interface IThrowReactionActor
    {
        /// <summary>Play the post-release reaction for one throw.</summary>
        void PlayReaction(LaunchParameters p);
    }
}
