using Terminal.Gui.Input;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;
using static Wooly.Tests.Tui.RailHeights;

namespace Wooly.Tests.Tui;

/// <summary>
///     The mark a click hides and shows the rail by (#443, #445): one cell for both, column 1 of the shell's bottom
///     frame row — <c>«</c> on the shown rail's bottom edge, or the first cell of its gauge row where it is compact,
///     and <c>»</c> on the content panel's bottom-left corner once the rail is hidden. A left click on it is
///     <c>\</c>. Clicked at cells of a headless terminal, as the rail-click tests click.
/// </summary>
public class RailToggleMarkTests
{
    /// <summary>
    ///     So short that the scrolled rail keeps two rows of the cursor's group over the gauge, and no heading.
    /// </summary>
    private const int Shortest = 4;

    /// <summary>The column of the shell's bottom frame row the mark is in, shown or hidden.</summary>
    private const int Mark = 1;

    /// <summary>
    ///     Framed, the mark is just inside the bottom-left corner of the rail's foot, <c>╰«──</c>, and a click on it
    ///     hides the rail — whereupon <c>»</c> is in the same cell on the content panel's bottom-left corner, which now
    ///     starts in the shell's first column, and a click there shows the rail again. Each click is saved, as each
    ///     <c>\</c> is.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AClickOnTheMarkHidesTheRailAndAClickOnTheHiddenRailsMarkShowsIt(bool coloured)
    {
        var built = new AShell();
        using var drawn = await DrawnShell.Of(80, Framed, Theme(coloured), built);
        var bottom = Bottom(drawn);

        Assert.StartsWith("╰«──", drawn.Rows()[bottom], StringComparison.Ordinal);
        Assert.Equal('«', drawn.Rail()[^1][Mark]);
        Assert.Equal(drawn.Cell(bottom, 0), drawn.Cell(bottom, Mark));

        drawn.Click(Mark, bottom);

        Assert.Equal(RailState.Hidden, drawn.Shell.RailState);
        Assert.StartsWith("╰»──", drawn.Rows()[bottom], StringComparison.Ordinal);
        Assert.Equal(0, drawn.Content.Frame.X);
        // In the role of the edge it is on, the glyph being the whole of what it says, with colour or without.
        Assert.Equal(drawn.Cell(bottom, 0), drawn.Cell(bottom, Mark));

        drawn.Click(Mark, bottom);

        Assert.Equal(RailState.Shown, drawn.Shell.RailState);
        Assert.StartsWith("╰«──", drawn.Rows()[bottom], StringComparison.Ordinal);
        Assert.Equal([RailState.Hidden, RailState.Shown], built.ShellPreferences.Rails);
    }

    /// <summary>
    ///     The mark is no longer on the rail's top edge nor on the Timelines heading, framed or compact: the first
    ///     group's edge and heading read as they did before there was a mark.
    /// </summary>
    [Fact]
    public async Task TheTopOfTheRailCarriesNoMark()
    {
        using var framed = await DrawnShell.Of(80, Framed, Themes.Plain);
        using var compact = await DrawnShell.Of(80, Compact, Themes.Plain);

        Assert.Equal("╭ Timelines ───────╮", framed.Rail()[0]);
        Assert.Equal("Timelines", compact.Rail()[0].TrimEnd());
        Assert.Single(framed.Rows(), row => row.Contains('«', StringComparison.Ordinal));
        Assert.Single(compact.Rows(), row => row.Contains('«', StringComparison.Ordinal));
    }

    /// <summary>
    ///     Compact or scrolled, with no frame to carry it, the mark is the first cell of the gauge row after its space
    ///     in, the bar a cell shorter for it — the same cell of the shell's bottom frame row — and a click there hides
    ///     the rail, the content panel's bottom-left corner then carrying <c>»</c> in that cell, framed as ever.
    /// </summary>
    [Theory]
    [InlineData(Compact, true)]
    [InlineData(Compact, false)]
    [InlineData(Scrolled, true)]
    [InlineData(Scrolled, false)]
    public async Task OnTheCompactRailTheMarkIsTheFirstCellOfTheGaugeRow(int height, bool coloured)
    {
        var built = new AShell();
        using var drawn = await DrawnShell.Of(80, height, Theme(coloured), built);
        var bottom = Bottom(drawn);

        Assert.StartsWith(" «", drawn.Rail()[^1], StringComparison.Ordinal);

        built.RateLimit.Said("mastodon.social", "token-personal", 213);
        drawn.Redraw();

        Assert.Equal(" «█████████░░░░  71%", drawn.Rail()[^1]);

        drawn.Click(Mark, bottom);

        Assert.Equal(RailState.Hidden, drawn.Shell.RailState);
        Assert.StartsWith("╰»──", drawn.Rows()[bottom], StringComparison.Ordinal);

        drawn.Click(Mark, bottom);

        Assert.Equal(RailState.Shown, drawn.Shell.RailState);
        Assert.Equal([RailState.Hidden, RailState.Shown], built.ShellPreferences.Rails);
    }

    /// <summary>
    ///     A double click on the mark is one toggle, either way about, framed or compact: its first click hides or
    ///     shows the rail, and its second, landing on the mark the toggle drew in the same cell, is that toggle and
    ///     nothing more. The screen in front is still in front, and one toggle is saved.
    /// </summary>
    [Theory]
    [InlineData(Framed, RailState.Shown, true)]
    [InlineData(Framed, RailState.Shown, false)]
    [InlineData(Framed, RailState.Hidden, true)]
    [InlineData(Framed, RailState.Hidden, false)]
    [InlineData(Compact, RailState.Shown, true)]
    [InlineData(Compact, RailState.Hidden, false)]
    public async Task ADoubleClickOnTheMarkIsOneToggleEitherWay(int height, RailState from, bool coloured)
    {
        var toggled = from is RailState.Hidden ? RailState.Shown : RailState.Hidden;
        var built = new AShell { HiddenRail = from is RailState.Hidden };
        using var drawn = await DrawnShell.Of(80, height, Theme(coloured), built);
        var screen = drawn.Shell.Screen;

        drawn.DoubleClick(Mark, Bottom(drawn));
        drawn.Built.Host.Drain();
        drawn.Redraw();

        Assert.Equal(toggled, drawn.Shell.RailState);
        Assert.Same(screen, drawn.Shell.Screen);
        Assert.Equal([toggled], built.ShellPreferences.Rails);
    }

    /// <summary>
    ///     On a rail so short that two rows of the cursor's group are all it keeps over the gauge, the mark is still on
    ///     the gauge row, so a click always has it to land on, and every digit of <c>40+</c> is still drawn.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OnTheShortestRailTheMarkIsStillThereAndEveryCountIsWhole(bool coloured)
    {
        using var drawn = await DrawnShell.Of(80, Shortest, Theme(coloured));
        var rail = drawn.Shell.Rail;

        rail.GoTo(DestinationKind.Messages);
        rail.Update(rail.Destinations.Single(place => place.Kind == DestinationKind.Notifications) with
        {
            Unread = 40,
            Full = true,
        });
        drawn.Built.Host.Drain();
        drawn.Redraw();

        var rows = drawn.Rail();

        Assert.EndsWith("40+", rows[0], StringComparison.Ordinal);
        Assert.Equal('«', rows[^1][Mark]);

        drawn.Click(Mark, Bottom(drawn));

        Assert.Equal(RailState.Hidden, drawn.Shell.RailState);
    }

    /// <summary>
    ///     Where <c>\</c> means nothing the click means nothing either — on compose, the description editor and the
    ///     public profile form, whose fields type it — and nor does a double click: the rail stays as it was, shown or
    ///     hidden, nothing is saved, and the screen with the draft is still in front.
    /// </summary>
    [Theory]
    [InlineData("compose", RailState.Shown)]
    [InlineData("compose", RailState.Hidden)]
    [InlineData("description", RailState.Shown)]
    [InlineData("description", RailState.Hidden)]
    [InlineData("public profile", RailState.Shown)]
    [InlineData("public profile", RailState.Hidden)]
    public async Task OnTheScreensWithFieldsAClickOrADoubleClickOnTheMarkDoesNothing(string where, RailState rail)
    {
        using var files = new TemporaryDirectory();
        var me = AnAccount.With(address: "jeff@mastodon.social", author: "Jeff", id: "7");
        var built = new AShell
        {
            Accounts = FakeAccountRelationships.Holding(me),
            PublicProfile = FakePublicProfileEditor.Holding(APublicProfile.With(displayName: string.Empty, account: me)),
            HiddenRail = rail is RailState.Hidden,
        };
        using var drawn = await DrawnShell.Of(80, 30, Themes.Plain, built);

        switch (where)
        {
            case "compose":
                ComposeRows.Open(drawn.Shell, ComposeFor.Post);
                drawn.Redraw();
                Assert.IsType<ComposeScreen>(drawn.Shell.Screen);
                break;

            case "description":
                drawn.Shell.Press(ShellKey.C);
                drawn.Shell.Paste(files.WriteFile("cat.png"));
                drawn.Settle();
                drawn.Press(Key.CursorUp);
                drawn.Press(Key.Enter);
                Assert.IsType<DescriptionScreen>(drawn.Shell.Screen);
                break;

            default:
                drawn.Shell.Rail.GoTo(DestinationKind.Profile);
                drawn.Settle();
                drawn.Press(new Key('E'));
                drawn.Settle();
                Assert.IsType<PublicProfileScreen>(drawn.Shell.Screen);
                break;
        }

        var screen = drawn.Shell.Screen;

        drawn.Click(Mark, Bottom(drawn));

        Assert.Equal(rail, drawn.Shell.RailState);

        drawn.DoubleClick(Mark, Bottom(drawn));
        drawn.Built.Host.Drain();
        drawn.Redraw();

        Assert.Equal(rail, drawn.Shell.RailState);
        Assert.Empty(built.ShellPreferences.Rails);
        Assert.Same(screen, drawn.Shell.Screen);
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

        drawn.Click(Mark, Bottom(drawn));

        Assert.Null(drawn.Shell.Asking);
        Assert.Equal(RailState.Shown, drawn.Shell.RailState);
        Assert.Empty(built.ShellPreferences.Rails);
    }

    /// <summary>
    ///     Only the mark's own cell answers: the rest of the bottom edge it is on — the corner, the rule after the
    ///     mark — and the top edge where the mark used to be ignore the click, shown or hidden, and a click on a
    ///     destination still arrives there, the rail left shown.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AClickAnywhereButTheMarkIsAsItWas(bool coloured)
    {
        var built = new AShell();
        using var drawn = await DrawnShell.Of(80, Framed, Theme(coloured), built);
        var bottom = Bottom(drawn);

        foreach (var (column, row) in new[] { (0, bottom), (2, bottom), (RailLines.Width - 1, bottom), (18, 0) })
        {
            drawn.Click(column, row);
        }

        drawn.Built.Host.Settle();

        Assert.Equal(RailState.Shown, drawn.Shell.RailState);
        Assert.Equal(0, drawn.Shell.Rail.Current);

        drawn.Press(new Key('\\'));
        drawn.Click(0, bottom);
        drawn.Click(2, bottom);

        Assert.Equal(RailState.Hidden, drawn.Shell.RailState);

        drawn.Press(new Key('\\'));

        var notifications =
            Array.FindIndex(drawn.Rail(), row => row.Contains("Notifications", StringComparison.Ordinal));

        drawn.Click(RailLines.Width / 2, notifications);
        drawn.Built.Host.Drain();
        drawn.Redraw();

        Assert.IsType<NotificationsScreen>(drawn.Shell.Screen);
        Assert.Equal(RailState.Shown, drawn.Shell.RailState);
        Assert.Equal([RailState.Hidden, RailState.Shown], built.ShellPreferences.Rails);
    }

    /// <summary>
    ///     Nothing but a left click: the pointer passing over the mark, a press dragged off it, and the wheel over it
    ///     leave the rail as it was, and the right button is <c>esc</c> there as anywhere.
    /// </summary>
    [Fact]
    public async Task HoveringDraggingOrWheelingOverTheMarkTogglesNothing()
    {
        var built = new AShell();
        using var drawn = await DrawnShell.Of(80, Framed, Themes.Dark, built);
        var bottom = Bottom(drawn);
        var rows = drawn.Rows();

        drawn.Point(Mark, bottom, MouseFlags.PositionReport);
        drawn.Wheel(Mark, bottom);
        drawn.Wheel(Mark, bottom, down: false);
        drawn.Point(Mark, bottom, MouseFlags.LeftButtonPressed);
        drawn.Point(Mark, 3, MouseFlags.LeftButtonPressed | MouseFlags.PositionReport);
        drawn.Point(Mark, 3, MouseFlags.LeftButtonReleased);
        drawn.RightClick(Mark, bottom);

        Assert.Equal(RailState.Shown, drawn.Shell.RailState);
        Assert.Empty(built.ShellPreferences.Rails);
        Assert.Equal(rows, drawn.Rows());
    }

    /// <summary>
    ///     Inside a narrowed shell the mark is still column 1 of the shell's bottom frame row — past the margin, which
    ///     is the terminal's — shown and hidden.
    /// </summary>
    [Fact]
    public async Task InsideANarrowedShellTheMarkIsInTheShellsSecondColumn()
    {
        using var drawn = await DrawnShell.Of(200, Framed, Themes.Plain);

        drawn.Press(new Key('-'));

        var bottom = Bottom(drawn);

        Assert.Equal('«', drawn.Rows()[bottom][10 + Mark]);

        drawn.Click(10 + Mark, bottom);

        Assert.Equal(RailState.Hidden, drawn.Shell.RailState);
        Assert.Equal('»', drawn.Rows()[bottom][10 + Mark]);

        drawn.Click(10 + Mark, bottom);

        Assert.Equal(RailState.Shown, drawn.Shell.RailState);
    }

    /// <summary>
    ///     Where there is no rail — nobody acted as — there is no mark either: the content panel's bottom-left corner
    ///     is only a corner, and a click there does nothing.
    /// </summary>
    [Fact]
    public async Task WithNoRailThereIsNoMark()
    {
        var registry = FakeProfileRegistry.Holding(
            "work",
            FakeProfileRegistry.Profile("work", "mastodon.social", "jeff@mastodon.social"));
        registry.Tokenless.Add("work");

        var built = new AShell { Profiles = registry };
        using var drawn = await DrawnShell.Of(80, Framed, Themes.Plain, built, launch: true);

        Assert.False(drawn.Shell.ShowsRail);
        Assert.StartsWith("╰───", drawn.Rows()[Bottom(drawn)], StringComparison.Ordinal);

        drawn.Click(Mark, Bottom(drawn));

        Assert.Equal(RailState.Shown, drawn.Shell.RailState);
        Assert.Empty(built.ShellPreferences.Rails);
    }

    /// <summary>The <c>?</c> screen's mouse summary says where the mark is and what a click on it does.</summary>
    [Fact]
    public async Task TheHelpScreenSaysTheMarkTogglesTheRail()
    {
        using var drawn = await DrawnShell.Of(80, 40, Themes.Plain);

        drawn.Press(new Key('?'));

        Assert.Contains(
            drawn.Rows(),
            row => row.Contains("click «/» bottom left to toggle the rail", StringComparison.Ordinal));
    }

    /// <summary>The shell's bottom frame row: the last row above the status row.</summary>
    private static int Bottom(DrawnShell drawn) => drawn.Rows().Length - 2;

    private static ITheme Theme(bool coloured) => coloured ? Themes.Dark : Themes.Plain;
}
