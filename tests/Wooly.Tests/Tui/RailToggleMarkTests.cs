using Terminal.Gui.Input;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The mark a click collapses and expands the rail by (#443): <c>«</c> on the expanded rail and <c>»</c> on the
///     <b>collapsed rail</b>, at the end of the first rail group's top edge, or of the first heading in view where the
///     rail is compact. A left click on it is <c>\</c>. Clicked at cells of a headless terminal, as the rail-click tests
///     click.
/// </summary>
public class RailToggleMarkTests
{
    /// <summary>Tall enough that every group is framed, as <see cref="ShellRailClickTests" /> counts it.</summary>
    private const int Framed = 24;

    /// <summary>Too short to frame the groups, tall enough for the compact rail whole.</summary>
    private const int Compact = 18;

    /// <summary>Too short even for the compact rail, which scrolls to keep the cursor's group in view.</summary>
    private const int Scrolled = 12;

    /// <summary>
    ///     Framed, the mark is in the top-right corner of the Timelines panel's top edge, just inside the corner — <c>«</c>
    ///     expanded, and a click on it collapses the rail, which then has <c>»</c> in the same place on the strip, a click
    ///     on which expands it again. Each click is saved, as each <c>\</c> is.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AClickOnTheFramedMarkCollapsesTheRailAndAClickOnItsOtherExpandsIt(bool coloured)
    {
        var built = new AShell();
        using var drawn = await DrawnShell.Of(80, Framed, Theme(coloured), built);

        Assert.Equal("╭ Timelines ──────«╮", drawn.Rail()[0]);

        drawn.Click(RailLines.Width - 2, 0);

        Assert.True(drawn.Shell.RailCollapsed);
        Assert.Equal(coloured ? "╭────»╮" : "╭──────»╮", drawn.Rail(Strip(coloured))[0]);

        drawn.Click(Strip(coloured) - 2, 0);

        Assert.False(drawn.Shell.RailCollapsed);
        Assert.Equal("╭ Timelines ──────«╮", drawn.Rail()[0]);
        Assert.Equal([true, false], built.ShellPreferences.Rails);
    }

    /// <summary>
    ///     Compact, where there are no frames, the mark ends the Timelines heading, over where the counts end: the last
    ///     column expanded, and on the strip the column before its last, which its counts leave.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CompactTheMarkEndsTheTimelinesHeading(bool coloured)
    {
        var built = new AShell();
        using var drawn = await DrawnShell.Of(80, Compact, Theme(coloured), built);

        Assert.Equal("Timelines          «", drawn.Rail()[0]);

        drawn.Click(RailLines.Width - 1, 0);

        Assert.True(drawn.Shell.RailCollapsed);
        Assert.Equal(new string(' ', Strip(coloured) - 2) + "» ", drawn.Rail(Strip(coloured))[0]);

        drawn.Click(Strip(coloured) - 2, 0);

        Assert.False(drawn.Shell.RailCollapsed);
        Assert.Equal([true, false], built.ShellPreferences.Rails);
    }

    /// <summary>
    ///     Scrolled past the Timelines heading, the mark ends the first heading in view instead, so a click always has it
    ///     to land on — and the strip, scrolled alike, has it on the same row.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ScrolledTheMarkEndsTheFirstHeadingInView(bool coloured)
    {
        using var drawn = await DrawnShell.Of(80, Scrolled, Theme(coloured));

        drawn.Shell.Rail.GoTo(DestinationKind.Messages);
        drawn.Built.Host.Drain();
        drawn.Redraw();

        var rail = drawn.Rail();
        var explore = Array.FindIndex(rail, row => row.StartsWith("Explore", StringComparison.Ordinal));

        Assert.DoesNotContain(rail, row => row.StartsWith("Timelines", StringComparison.Ordinal));
        Assert.Equal("Explore            «", rail[explore]);
        Assert.Single(rail, row => row.Contains('«', StringComparison.Ordinal));

        drawn.Click(RailLines.Width - 1, explore);

        Assert.True(drawn.Shell.RailCollapsed);
        Assert.Equal(new string(' ', Strip(coloured) - 2) + "» ", drawn.Rail(Strip(coloured))[explore]);

        drawn.Click(Strip(coloured) - 2, explore);

        Assert.False(drawn.Shell.RailCollapsed);
    }

    /// <summary>How wide the strip is: seven columns in colour, and nine with the mark column kept.</summary>
    private static int Strip(bool coloured) => coloured ? 7 : 9;

    private static ITheme Theme(bool coloured) => coloured ? Themes.Dark : Themes.Plain;
}
