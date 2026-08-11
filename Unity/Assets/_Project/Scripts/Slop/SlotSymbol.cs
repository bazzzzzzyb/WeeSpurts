namespace WeeSpurts.Slop
{
    /// <summary>
    /// The six faces on the Thunder Lanes house machine. An int would work
    /// just as well at runtime, but a named enum is what keeps
    /// <see cref="SlotRules.Paytable"/> and the reel strips readable in the
    /// Inspector and in test failures.
    /// </summary>
    public enum SlotSymbol { Pin, Beer, Ball, Shoe, Ticket, Mascot }
}
