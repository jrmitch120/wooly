namespace Wooly.Tests.Tui;

/// <summary>
///     The terminal heights the rail is drawn at in each of its three ways — framed, compact and scrolled — so that the
///     tests clicking, toggling and hiding it draw the same three rails. Measured for a rail with no tag sets, which is
///     the one these tests draw; each set takes a row of its own more, and its group two (ADR-0033).
/// </summary>
internal static class RailHeights
{
    /// <summary>
    ///     Tall enough that every group is framed: nine destinations, four frames of two rows, and the API panel's four,
    ///     with the status row under them.
    /// </summary>
    public const int Framed = 24;

    /// <summary>Too short to frame the groups, tall enough for the compact rail whole (14 to 20 rail rows).</summary>
    public const int Compact = 18;

    /// <summary>Too short even for the compact rail, which scrolls to keep the cursor's group in view.</summary>
    public const int Scrolled = 12;
}
