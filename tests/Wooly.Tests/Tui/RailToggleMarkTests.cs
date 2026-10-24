using Terminal.Gui.Input;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The mark a click collapses and expands the rail by (#443): <c>«</c> on the expanded rail and <c>»</c> on the
///     <b>collapsed rail</b>, at the end of the first rail group's top edge, or of the first heading in view where the
///     rail is compact. A left click on it is <c>\</c>. Clicked at cells of a headless terminal, as the rail-click
///     tests click.
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
    ///     So short that the scrolled rail keeps two rows of the cursor's group over the gauge, and its heading is not
    ///     one of them.
    /// </summary>
    private const int Headless = 4;

    /// <summary>
    ///     A double click on the scrolled mark is the one toggle its first click made and nothing more: its second
    ///     click, landing where the content panel now starts beside the collapsed rail, opens nothing there.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ADoubleClickOnTheScrolledMarkTogglesTheRailOnceAndOpensNothing(bool coloured)
    {
        var conversations = Enumerable.Range(1, 6)
            .Select(at => AConversation.With(id: $"c{at}", unread: false))
            .ToArray();
        var built = new AShell { Messages = FakeDirectMessages.Holding(conversations) };
        using var drawn = await DrawnShell.Of(80, Scrolled, Theme(coloured), built);

        drawn.Shell.Rail.GoTo(DestinationKind.Messages);
        drawn.Built.Host.Drain();
        drawn.Redraw();

        var explore = Array.FindIndex(drawn.Rail(), row => row.StartsWith("Explore", StringComparison.Ordinal));
        var messages = drawn.Shell.Screen;

        drawn.DoubleClick(RailLines.Width - 1, explore);
        drawn.Built.Host.Drain();
        drawn.Redraw();

        Assert.Equal(RailState.Hidden, drawn.Shell.RailState);
        Assert.Same(messages, drawn.Shell.Screen);
        Assert.Equal(1, drawn.Shell.Depth);
        Assert.Equal([RailState.Hidden], built.ShellPreferences.Rails);
    }

    /// <summary>
    ///     Where <c>\</c> means nothing the click means nothing either — on compose, where it is typed — so the rail
    ///     stays as it was, nothing is saved, and the draft is still in front.
    /// </summary>
    [Fact]
    public async Task OnComposeAClickOnTheMarkDoesNothing()
    {
        var built = new AShell();
        using var drawn = await DrawnShell.Of(80, Framed, Themes.Plain, built);

        ComposeRows.Open(drawn.Shell, ComposeFor.Post);
        drawn.Redraw();

        var compose = drawn.Shell.Screen;

        drawn.Click(RailLines.Width - 2, 0);

        Assert.Equal(RailState.Shown, drawn.Shell.RailState);
        Assert.Empty(built.ShellPreferences.Rails);
        Assert.Same(compose, drawn.Shell.Screen);
        Assert.Equal(RailLines.Width, drawn.Content.Frame.X);
    }

    /// <summary>
    ///     An open confirmation wins, as it does over any click and over <c>\</c>: the click declines it, and the rail
    ///     is left as it was.
    /// </summary>
    [Fact]
    public async Task AClickOnTheMarkDeclinesAnOpenConfirmationAndTogglesNothing()
    {
        var answers = Enumerable.Range(1, 3).Select(at => APost.AnAnswer($"Answer {at}", at)).ToList();
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "110", poll: APost.APoll(options: answers))),
        };
        using var drawn = await DrawnShell.Of(80, Framed, Themes.Plain, built);

        drawn.Press(new Key('2'));
        drawn.Press(Key.V);

        Assert.NotNull(drawn.Shell.Asking);

        drawn.Click(RailLines.Width - 2, 0);

        Assert.Null(drawn.Shell.Asking);
        Assert.Equal(RailState.Shown, drawn.Shell.RailState);
        Assert.Empty(built.ShellPreferences.Rails);
    }

    /// <summary>
    ///     Only the mark's own cell answers: the rest of the top edge it is on, the title and the corner after it,
    ///     ignore the click as they did, and a click on a destination still arrives there, the rail left expanded.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EveryOtherClickOnTheRailIsAsItWas(bool coloured)
    {
        var built = new AShell();
        using var drawn = await DrawnShell.Of(80, Framed, Theme(coloured), built);

        foreach (var column in new[] { 0, 3, RailLines.Width - 3, RailLines.Width - 1 })
        {
            drawn.Click(column, 0);
        }

        drawn.Built.Host.Settle();

        Assert.Equal(RailState.Shown, drawn.Shell.RailState);
        Assert.Equal(0, drawn.Shell.Rail.Current);

        var notifications =
            Array.FindIndex(drawn.Rail(), row => row.Contains("Notifications", StringComparison.Ordinal));

        drawn.Click(RailLines.Width / 2, notifications);
        drawn.Built.Host.Drain();
        drawn.Redraw();

        Assert.IsType<NotificationsScreen>(drawn.Shell.Screen);
        Assert.Equal(RailState.Shown, drawn.Shell.RailState);
        Assert.Empty(built.ShellPreferences.Rails);
    }

    /// <summary>
    ///     Nothing but a left click: the pointer passing over the mark, a press dragged off it, and the wheel over it
    ///     leave the rail as it was, and the right button is <c>esc</c> there as anywhere.
    /// </summary>
    [Fact]
    public async Task NoHoverNoDragAndNoWheelOverTheMark()
    {
        var built = new AShell();
        using var drawn = await DrawnShell.Of(80, Framed, Themes.Dark, built);

        var rows = drawn.Rows();
        var mark = RailLines.Width - 2;

        drawn.Point(mark, 0, MouseFlags.PositionReport);
        drawn.Wheel(mark, 0);
        drawn.Wheel(mark, 0, down: false);
        drawn.Point(mark, 0, MouseFlags.LeftButtonPressed);
        drawn.Point(mark, 3, MouseFlags.LeftButtonPressed | MouseFlags.PositionReport);
        drawn.Point(mark, 3, MouseFlags.LeftButtonReleased);
        drawn.RightClick(mark, 0);

        Assert.Equal(RailState.Shown, drawn.Shell.RailState);
        Assert.Empty(built.ShellPreferences.Rails);
        Assert.Equal(rows, drawn.Rows());
    }

    /// <summary>The <c>?</c> screen's mouse summary says a click on the mark collapses or expands the rail.</summary>
    [Fact]
    public async Task TheHelpScreenSaysTheMarkTogglesTheRail()
    {
        using var drawn = await DrawnShell.Of(80, 40, Themes.Plain);

        drawn.Press(new Key('?'));

        Assert.Contains(
            drawn.Rows(),
            row => row.Contains("click «/» to collapse or expand the rail", StringComparison.Ordinal));
    }

    private static ITheme Theme(bool coloured) => coloured ? Themes.Dark : Themes.Plain;
}
