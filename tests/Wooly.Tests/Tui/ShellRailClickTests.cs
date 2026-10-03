using Terminal.Gui.Input;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     A click on the rail, sent at a cell of a headless terminal: a destination is arrived at at once, through the
///     rail's immediate path, and everything on the rail that is not a destination ignores it (#288).
/// </summary>
public class ShellRailClickTests
{
    /// <summary>A column inside the rail, clear of its left edge and its mark column.</summary>
    private const int OverRail = RailLines.Width / 2;

    /// <summary>
    ///     Tall enough that every group is framed: ten destinations, four frames of two rows, and the API panel's four,
    ///     with the status row under them.
    /// </summary>
    private const int Framed = 24;

    /// <summary>Too short to frame the groups, tall enough for the compact rail whole (15 to 21 rail rows).</summary>
    private const int Compact = 18;

    /// <summary>Too short even for the compact rail, which scrolls to keep the cursor's group in view.</summary>
    private const int Scrolled = 12;

    /// <summary>
    ///     A click on a destination puts the rail's cursor and its selection there together and arrives at once: no
    ///     settle window is waited out, and that destination is read.
    /// </summary>
    [Fact]
    public async Task ClickingADestinationArrivesAtItAtOnce()
    {
        using var drawn = await DrawnShell.Of(80, Framed, Themes.Plain);

        var read = drawn.Built.Notifications.Reads.Count;

        drawn.Click(OverRail, RowOf(drawn, "Notifications"));
        drawn.Built.Host.Drain();
        drawn.Redraw();

        var at = IndexOf(drawn, DestinationKind.Notifications);

        Assert.Equal(at, drawn.Shell.Rail.Cursor);
        Assert.Equal(at, drawn.Shell.Rail.Current);
        Assert.Equal(0, drawn.Built.Host.Waiting);
        Assert.IsType<NotificationsScreen>(drawn.Shell.Screen);
        Assert.Equal(read + 1, drawn.Built.Notifications.Reads.Count);
    }

    /// <summary>
    ///     A click part-way through tabbing abandons the landing the tabbing left waiting, so the destination clicked is
    ///     the only one read — not the one the cursor had got to as well.
    /// </summary>
    [Fact]
    public async Task AClickAbandonsTheLandingTabbingLeftWaiting()
    {
        using var drawn = await DrawnShell.Of(80, Framed, Themes.Plain);

        var timelines = drawn.Built.Timelines.Reads.Count;
        var requests = drawn.Built.Requests;

        drawn.Press(Key.Tab);
        drawn.Press(Key.Tab);

        drawn.Click(OverRail, RowOf(drawn, "Notifications"));
        drawn.Built.Host.Settle();
        drawn.Redraw();

        Assert.Equal(IndexOf(drawn, DestinationKind.Notifications), drawn.Shell.Rail.Current);
        Assert.Equal(timelines, drawn.Built.Timelines.Reads.Count);
        Assert.Equal(requests + 1, drawn.Built.Requests);
        Assert.IsType<NotificationsScreen>(drawn.Shell.Screen);
    }

    /// <summary>
    ///     A click on the destination already shown arrives nowhere new: the screen, the rail and the instance are left
    ///     as they were.
    /// </summary>
    [Fact]
    public async Task ClickingTheDestinationShownAsksForNothing()
    {
        using var drawn = await DrawnShell.Of(80, Framed, Themes.Plain);

        var screen = drawn.Shell.Screen;
        var requests = drawn.Built.Requests;

        drawn.Click(OverRail, RowOf(drawn, "Home"));
        drawn.Built.Host.Settle();
        drawn.Redraw();

        Assert.Same(screen, drawn.Shell.Screen);
        Assert.Equal(0, drawn.Shell.Rail.Current);
        Assert.Equal(0, drawn.Shell.Rail.Cursor);
        Assert.Equal(requests, drawn.Built.Requests);
    }

    /// <summary>
    ///     Whichever layout the rail is in, the click lands on the entry drawn under the pointer: framed, compact, and
    ///     compact scrolled to keep the cursor's group in view.
    /// </summary>
    [Theory]
    [InlineData(Framed, "Direct messages")]
    [InlineData(Framed, "Local")]
    [InlineData(Compact, "Search")]
    [InlineData(Compact, "Follow requests")]
    [InlineData(Scrolled, "Notifications")]
    [InlineData(Scrolled, "Follow requests")]
    public async Task TheClickLandsOnTheEntryUnderThePointer(int height, string label)
    {
        using var drawn = await DrawnShell.Of(80, height, Themes.Plain);

        if (height == Scrolled)
        {
            // Down the rail far enough that it has scrolled, so the rows no longer start at the first destination.
            drawn.Shell.Rail.GoTo(DestinationKind.Messages);
            drawn.Built.Host.Drain();
            drawn.Redraw();

            Assert.DoesNotContain(drawn.Rail(), row => row.Contains("Home", StringComparison.Ordinal));
        }

        drawn.Click(OverRail, RowOf(drawn, label));
        drawn.Built.Host.Drain();
        drawn.Redraw();

        Assert.Equal(label, drawn.Shell.Rail.Showing.Label);
        Assert.Equal(drawn.Shell.Rail.Current, drawn.Shell.Rail.Cursor);
    }

    /// <summary>
    ///     Only destinations answer a click: a group's title on its frame, a compact heading, and the API panel at the
    ///     foot change nothing and ask for nothing.
    /// </summary>
    [Theory]
    [InlineData(Framed, "Inbox")]
    [InlineData(Framed, "API")]
    [InlineData(Compact, "Explore")]
    [InlineData(Compact, null)]
    public async Task AClickOnWhatIsNoDestinationChangesNothing(int height, string? title)
    {
        using var drawn = await DrawnShell.Of(80, height, Themes.Plain);

        // The foot's last row — the gauge — where no title is named.
        var row = title is null ? drawn.Rail().Length - 1 : RowOf(drawn, title);
        var rail = drawn.Rail();
        var requests = drawn.Built.Requests;

        drawn.Click(OverRail, row);
        drawn.Built.Host.Settle();
        drawn.Redraw();

        Assert.Equal(0, drawn.Shell.Rail.Cursor);
        Assert.Equal(0, drawn.Shell.Rail.Current);
        Assert.Equal(rail, drawn.Rail());
        Assert.Equal(requests, drawn.Built.Requests);
    }

    /// <summary>
    ///     An open confirmation wins: a click on the rail declines it, as any key but the agreeing one does, and arrives
    ///     nowhere.
    /// </summary>
    [Fact]
    public async Task AClickDeclinesAnOpenConfirmationAndArrivesNowhere()
    {
        var answers = Enumerable.Range(1, 3).Select(at => APost.AnAnswer($"Answer {at}", at)).ToList();

        using var drawn = await DrawnShell.Of(
            80,
            Framed,
            Themes.Plain,
            new AShell
            {
                Timelines = FakeTimelineReader.Holding(APost.With(id: "110", poll: APost.APoll(options: answers))),
            });

        drawn.Press(new Key('2'));
        drawn.Press(Key.V);

        Assert.NotNull(drawn.Shell.Asking);

        var read = drawn.Built.Notifications.Reads.Count;

        drawn.Click(OverRail, RowOf(drawn, "Notifications"));
        drawn.Built.Host.Settle();
        drawn.Redraw();

        Assert.Null(drawn.Shell.Asking);
        Assert.Equal(0, drawn.Shell.Rail.Current);
        Assert.Empty(drawn.Built.Engagement.Votes);
        Assert.Equal(read, drawn.Built.Notifications.Reads.Count);
    }

    /// <summary>
    ///     And an open filter prompt: a click closes it, as a key the prompt does not take would, and arrives nowhere.
    /// </summary>
    [Fact]
    public async Task AClickClosesAnOpenFilterPromptAndArrivesNowhere()
    {
        using var drawn = await OnAFollowList();

        drawn.Press(Key.F);
        drawn.Press(Key.M);

        var follows = Assert.IsType<FollowsScreen>(drawn.Shell.Screen);

        Assert.True(follows.IsTyping);

        drawn.Click(OverRail, RowOf(drawn, "Notifications"));
        drawn.Built.Host.Settle();
        drawn.Redraw();

        Assert.Same(follows, drawn.Shell.Screen);
        Assert.False(follows.IsTyping);
        Assert.Equal("m", follows.Filter);
        Assert.Equal(0, drawn.Shell.Rail.Current);
    }

    /// <summary>
    ///     With nobody to act as there is no rail to click: a click where it would be is a click on the add screen,
    ///     which asks nothing and goes nowhere.
    /// </summary>
    [Fact]
    public async Task WithNobodyActedAsARailClickDoesNothing()
    {
        var built = new AShell { Profiles = FakeProfileRegistry.Holding(current: null) };

        using var drawn = await DrawnShell.Of(80, Framed, Themes.Plain, built, launch: true);

        var screen = drawn.Shell.Screen;

        for (var row = 1; row < Framed - 1; row++)
        {
            drawn.Click(OverRail, row);
        }

        drawn.Built.Host.Settle();

        Assert.Same(screen, drawn.Shell.Screen);
        Assert.Equal(0, drawn.Built.Requests);
    }

    /// <summary>
    ///     Drilled from Home into a post and on into its author, a click on Home walks back out to Home's own screen:
    ///     the stack down to its one bottom screen, that screen's page and pick where the reader left them, and nothing
    ///     asked of the instance (#289).
    /// </summary>
    [Fact]
    public async Task ClickingTheCurrentDestinationWalksBackToItsRoot()
    {
        using var drawn = await DrawnShell.Of(80, Framed, Themes.Plain, Four());

        drawn.Press(Key.K);
        drawn.Press(Key.CursorDown);

        var home = drawn.Shell.Screen;
        var top = drawn.Content.Top;

        Assert.True(top > 0);

        await Drill(drawn);

        Assert.Equal(3, drawn.Shell.Depth);

        var requests = drawn.Built.Requests;

        drawn.Click(OverRail, RowOf(drawn, "Home"));
        drawn.Built.Host.Settle();
        drawn.Redraw();

        Assert.Same(home, drawn.Shell.Screen);
        Assert.Equal(["Home"], drawn.Shell.Crumbs);
        Assert.Equal("220", drawn.Shell.Screen.Picked?.Id);
        Assert.Equal(top, drawn.Content.Top);
        Assert.Equal(requests, drawn.Built.Requests);
        Assert.Equal(0, drawn.Shell.Rail.Current);
        Assert.Equal(0, drawn.Shell.Rail.Cursor);
    }

    /// <summary>On the destination's own screen already, a click on it changes nothing and asks for nothing.</summary>
    [Fact]
    public async Task ClickingTheCurrentDestinationAtItsRootDoesNothing()
    {
        using var drawn = await DrawnShell.Of(80, Framed, Themes.Plain, Four());

        drawn.Press(Key.K);

        var home = drawn.Shell.Screen;
        var rows = drawn.Rows();
        var requests = drawn.Built.Requests;

        drawn.Click(OverRail, RowOf(drawn, "Home"));
        drawn.Built.Host.Settle();
        drawn.Redraw();

        Assert.Same(home, drawn.Shell.Screen);
        Assert.Equal(rows, drawn.Rows());
        Assert.Equal(requests, drawn.Built.Requests);
    }

    /// <summary>
    ///     The keys get no walk back: tabbing off the current destination and back onto it, drilled in, leaves the stack
    ///     where it was.
    /// </summary>
    [Fact]
    public async Task TabbingBackOntoTheCurrentDestinationLeavesTheDrillAlone()
    {
        using var drawn = await DrawnShell.Of(80, Framed, Themes.Plain, Four());

        await Drill(drawn);

        var crumbs = drawn.Shell.Crumbs;

        drawn.Press(Key.Tab);
        drawn.Press(Key.Tab.WithShift);
        drawn.Built.Host.Settle();
        drawn.Redraw();

        Assert.Equal(crumbs, drawn.Shell.Crumbs);
    }

    /// <summary>The rail row the first entry, title or panel reading <paramref name="text" /> is drawn on.</summary>
    private static int RowOf(DrawnShell drawn, string text)
    {
        var rows = drawn.Rail();
        var row = Array.FindIndex(rows, line => line.Contains(text, StringComparison.Ordinal));

        Assert.True(row >= 0, $"Nothing on the rail reads {text}:\n{string.Join('\n', rows)}");

        return row;
    }

    /// <summary>Where on the rail <paramref name="kind" /> is, as its cursor and selection count it.</summary>
    private static int IndexOf(DrawnShell drawn, DestinationKind kind) =>
        drawn.Shell.Rail.Destinations.ToList().FindIndex(destination => destination.Kind == kind);

    /// <summary>Four posts on Home, by people other than the reader.</summary>
    private static AShell Four() => new()
    {
        Timelines = FakeTimelineReader.Holding(
            APost.With(id: "110"),
            APost.With(id: "220"),
            APost.With(id: "330"),
            APost.With(id: "440")),
    };

    /// <summary>Two screens deep from Home: the picked post opened, and its author opened off it.</summary>
    private static async Task Drill(DrawnShell drawn)
    {
        drawn.Press(Key.Enter);
        drawn.Built.Host.Drain();
        await drawn.Shell.OpenAuthor();
        drawn.Built.Host.Drain();
        drawn.Redraw();
    }

    /// <summary>The shell drawn framed and drilled into a follow list of one, from the author of the post on Home.</summary>
    private static async Task<DrawnShell> OnAFollowList()
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "110", account: "ben@hachyderm.io")),
            Accounts = FakeAccountRelationships.Holding(
                AnAccount.With(address: "ben@hachyderm.io"),
                [AnAccount.With(address: "maria@here.social")]),
        };

        var drawn = await DrawnShell.Of(80, Framed, Themes.Plain, built);

        await drawn.Shell.OpenAuthor();
        built.Host.Drain();
        drawn.Press(Key.W);
        built.Host.Drain();
        drawn.Redraw();

        Assert.IsType<FollowsScreen>(drawn.Shell.Screen);

        return drawn;
    }
}
