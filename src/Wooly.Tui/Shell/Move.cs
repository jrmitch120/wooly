namespace Wooly.Tui.Shell;

/// <summary>
///     How a screen comes up from its <see cref="Subject" />, which settles what it does to the stack: the three ways
///     of getting somewhere, and the one way of getting more of where you already are (#233).
/// </summary>
internal enum Move
{
    /// <summary>Arriving from the rail: the stack is put back to one screen, since this is a different here.</summary>
    Arrive,

    /// <summary>Drilling in: the screen goes on top of what is showing, and <c>esc</c> walks back out of it.</summary>
    Drill,

    /// <summary>
    ///     Standing a fresher copy in place of what is showing — a refresh, or a swap to the other side of the same
    ///     thing — so that the way the reader got here is still under them.
    /// </summary>
    Refresh,

    /// <summary>
    ///     Reading more into the screen that is already showing, which moves nothing on the stack: the rest of a list
    ///     held whole, or the next page of one browsed.
    /// </summary>
    Page,
}
