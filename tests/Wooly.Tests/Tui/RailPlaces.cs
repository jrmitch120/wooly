using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     Where on the rail a destination is, found by what it is — its kind, or the label it is drawn under — rather
///     than by its position, so that a test stepping to Notifications or clicking the profile says so, and the rail can
///     grow without touching it (#559). What the rail holds and in what order is
///     <see cref="RailFetchTests.Rail_ListsTheFixedDestinationsInTheirGroups_WhereThereAreNoTagSets" />'s alone.
/// </summary>
internal static class RailPlaces
{
    /// <summary>Where on the rail the one destination of <paramref name="kind" /> is, as its cursor counts it.</summary>
    public static int IndexOf(this Rail rail, DestinationKind kind) => Index(rail, rail.Of(kind));

    /// <summary>Where on the rail the destination called <paramref name="label" /> is, as its cursor counts it.</summary>
    public static int IndexOf(this Rail rail, string label) =>
        Index(rail, rail.Destinations.Single(destination => destination.Label == label));

    /// <summary>
    ///     Steps the rail's cursor onto the one destination of <paramref name="kind" /> in a single step, as a run of
    ///     presses that ended there would, and settles as any step does.
    /// </summary>
    public static void StepTo(this Rail rail, DestinationKind kind) => rail.Step(rail.IndexOf(kind) - rail.Cursor);

    /// <summary>Steps the rail's cursor onto the destination called <paramref name="label" />, as above.</summary>
    public static void StepTo(this Rail rail, string label) => rail.Step(rail.IndexOf(label) - rail.Cursor);

    /// <summary>
    ///     Steps the shell's rail onto the one destination of <paramref name="kind" /> in a single step, through the
    ///     shell — so a draft in front is asked about, as a press of the rail would have it.
    /// </summary>
    public static void StepTo(this Shell shell, DestinationKind kind) =>
        shell.Step(shell.Rail.IndexOf(kind) - shell.Rail.Cursor);

    /// <summary>Steps the shell's rail onto the destination called <paramref name="label" />, as above.</summary>
    public static void StepTo(this Shell shell, string label) =>
        shell.Step(shell.Rail.IndexOf(label) - shell.Rail.Cursor);

    /// <summary>
    ///     The terminal row the first rail entry, group title or panel reading <paramref name="text" /> is drawn on —
    ///     where a click on it lands, read off what the terminal drew.
    /// </summary>
    public static int RailRowOf(DrawnShell drawn, string text)
    {
        var rows = drawn.Rail();
        var row = Array.FindIndex(rows, line => line.Contains(text, StringComparison.Ordinal));

        Assert.True(row >= 0, $"Nothing on the rail reads {text}:\n{string.Join('\n', rows)}");

        return row;
    }

    private static int Index(Rail rail, Destination destination) =>
        rail.Destinations.ToList().IndexOf(destination);
}
