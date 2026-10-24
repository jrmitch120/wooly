using Terminal.Gui.Input;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The <b>collapsed rail</b> (#441): <c>\</c> switches the rail between the expanded rail and a strip of one mark a
///     destination, for the session, and the content panel takes the columns the labels gave up. Pressed and read back
///     off a headless terminal, since the promise is about where the cells land.
/// </summary>
public class CollapsedRailTests
{
    /// <summary>Tall enough that every group is framed, as <see cref="ShellRailClickTests" /> counts it.</summary>
    private const int Framed = 24;

    /// <summary>Too short to frame the groups, tall enough for the compact rail whole.</summary>
    private const int Compact = 18;

    /// <summary>Too short even for the compact rail, which scrolls to keep the cursor's group in view.</summary>
    private const int Scrolled = 12;

    /// <summary>Each destination's mark, in the rail's order.</summary>
    private const string Marks = "HLF#D/NMR@";

    /// <summary>The key that collapses and expands the rail.</summary>
    private static readonly Key Toggle = new('\\');

    /// <summary>
    ///     <c>\</c> collapses the rail to a strip of fixed width — seven columns where colour is drawn, nine where it is
    ///     not and the mark column is kept — and the content panel starts where the strip ends. Pressed again, the rail is
    ///     as it was.
    /// </summary>
    [Theory]
    [InlineData(true, 7)]
    [InlineData(false, 9)]
    public async Task BackslashCollapsesTheRailToAStripAndExpandsItAgain(bool coloured, int strip)
    {
        using var drawn = await DrawnShell.Of(80, 24, Theme(coloured));

        drawn.Press(Toggle);

        Assert.Equal(strip, ContentStarts(drawn));
        Assert.Equal(80 - strip, drawn.Content.Frame.Width);

        drawn.Press(Toggle);

        Assert.Equal(RailLines.Width, ContentStarts(drawn));
        Assert.Equal(80 - RailLines.Width, drawn.Content.Frame.Width);
    }

    /// <summary>
    ///     Each destination is one fixed mark on the strip, the Hashtag's <c>#</c> whatever tag is kept and You's
    ///     <c>@</c> whatever the handle — drawn on the row its label was drawn on, inside its group's frame and after
    ///     the cursor's mark where colour is not drawn.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EachDestinationIsItsMarkOnTheRowItsLabelWasOn(bool coloured)
    {
        using var drawn = await DrawnShell.Of(80, Framed, Theme(coloured), new AShell { Hashtag = "dotnet" });

        var expanded = drawn.Rail();
        var labels = drawn.Shell.Rail.Destinations.Select(destination => destination.Label).ToList();

        drawn.Press(Toggle);

        var strip = drawn.Rail(Strip(coloured));
        var marks = labels.Select(label => strip[RowOf(expanded, label)][coloured ? 1 : 3]);

        Assert.Equal(Marks, string.Concat(marks));
        Assert.Equal(coloured ? "│H    │" : "│▶ H    │", strip[1]);
    }

    /// <summary>
    ///     The counts are drawn as they are expanded — none at nothing, the number, or <c>40+</c> for a read that came
    ///     back full — after the mark and ending where the strip's inside ends, and the strip is as wide whatever they say.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CountsAreDrawnAsExpandedAndTheStripKeepsItsWidth(bool coloured)
    {
        using var drawn = await DrawnShell.Of(80, Framed, Theme(coloured));

        drawn.Press(Toggle);
        Counted(drawn, DestinationKind.Notifications, 40, full: true);
        Counted(drawn, DestinationKind.Messages, 4);
        Counted(drawn, DestinationKind.Requests, 0);

        var lead = coloured ? string.Empty : "  ";

        Assert.Equal($"│{lead}N 40+│", Row(drawn, DestinationKind.Notifications, coloured));
        Assert.Equal($"│{lead}M   4│", Row(drawn, DestinationKind.Messages, coloured));
        Assert.Equal($"│{lead}R    │", Row(drawn, DestinationKind.Requests, coloured));
        Assert.Equal(Strip(coloured), ContentStarts(drawn));

        Counted(drawn, DestinationKind.Notifications, 0);

        Assert.Equal($"│{lead}N    │", Row(drawn, DestinationKind.Notifications, coloured));
        Assert.Equal(Strip(coloured), ContentStarts(drawn));
    }

    /// <summary>
    ///     Where colour is drawn the selection and the cursor are bands across the whole of the strip's inside, and a
    ///     count off the selected row is in the unread role.
    /// </summary>
    [Fact]
    public async Task TheSelectionAndTheCursorAreBandsAcrossTheStrip()
    {
        var theme = Themes.Dark;
        using var drawn = await DrawnShell.Of(80, Framed, theme);

        drawn.Press(Toggle);
        Counted(drawn, DestinationKind.Messages, 4);
        drawn.Press(Key.Tab);

        var strip = drawn.Rail(Strip(true));
        var home = RowOf(drawn.Shell, DestinationKind.Home, strip);
        var local = RowOf(drawn.Shell, DestinationKind.Local, strip);
        var messages = RowOf(drawn.Shell, DestinationKind.Messages, strip);

        Assert.All(Enumerable.Range(1, 5), column => Assert.Equal(theme.For(Role.RailCurrent), drawn.Cell(home, column)));
        Assert.All(Enumerable.Range(1, 5), column => Assert.Equal(theme.For(Role.RailCursor), drawn.Cell(local, column)));
        Assert.Equal(theme.For(Role.RailUnread), drawn.Cell(messages, 5));
    }

    /// <summary>Without colour, the kept mark column carries them: the cursor's mark, and the selection's while it lags.</summary>
    [Fact]
    public async Task WithoutColourTheMarkColumnCarriesTheCursorAndTheSelection()
    {
        using var drawn = await DrawnShell.Of(80, Framed, Themes.Plain);

        drawn.Press(Toggle);
        drawn.Press(Key.Tab);

        var strip = drawn.Rail(Strip(false));

        Assert.Equal("│▷ H    │", strip[1]);
        Assert.Equal("│▶ L    │", strip[2]);
    }

    /// <summary>
    ///     Every destination is on the same row collapsed as expanded, and every panel's edges too — framed, compact and
    ///     scrolled alike, since the strip steps down at the expanded rail's heights — so a toggle moves the rail only
    ///     sideways.
    /// </summary>
    [Theory]
    [InlineData(Framed, true)]
    [InlineData(Framed, false)]
    [InlineData(Compact, true)]
    [InlineData(Compact, false)]
    [InlineData(Scrolled, true)]
    [InlineData(Scrolled, false)]
    public async Task EveryDestinationIsOnTheSameRowInBothStates(int height, bool coloured)
    {
        using var drawn = await DrawnShell.Of(80, height, Theme(coloured), Twice());

        if (height == Scrolled)
        {
            // Far enough down that the scrolled rail no longer starts at Home.
            drawn.Shell.Rail.GoTo(DestinationKind.Messages);
            drawn.Built.Host.Drain();
            drawn.Redraw();
        }

        var expanded = drawn.Rail();
        var rows = drawn.Shell.Rail.Destinations.Select(destination => RowOf(expanded, destination.Label)).ToList();

        drawn.Press(Toggle);

        var strip = drawn.Rail(Strip(coloured));

        Assert.Equal(rows, drawn.Shell.Rail.Destinations.Select(destination => RowOf(drawn.Shell, destination.Kind, strip)));
        Assert.Equal(expanded.Select(Framing), strip.Select(Framing));
        Assert.Equal(height == Scrolled, rows.Contains(-1));
    }

    /// <summary>
    ///     The four groups stay panels of their own, untitled, and the one holding the cursor is framed active — moving
    ///     with the cursor as it does expanded.
    /// </summary>
    [Fact]
    public async Task TheGroupHoldingTheCursorIsFramedActive()
    {
        var theme = Themes.Dark;
        using var drawn = await DrawnShell.Of(80, Framed, theme);

        drawn.Press(Toggle);

        var strip = drawn.Rail(Strip(true));
        var tops = Enumerable.Range(0, strip.Length).Where(row => strip[row].StartsWith('╭')).ToList();

        Assert.Equal(5, tops.Count);
        Assert.All(tops, row => Assert.Equal("╭─────╮", strip[row]));
        Assert.Equal(theme.For(Role.PanelBorderActive), drawn.Cell(tops[0], 0));
        Assert.Equal(theme.For(Role.PanelBorder), drawn.Cell(tops[1], 0));

        drawn.Press(new Key('`'));

        Assert.Equal(theme.For(Role.PanelBorder), drawn.Cell(tops[0], 0));
        Assert.Equal(theme.For(Role.PanelBorderActive), drawn.Cell(tops[1], 0));
    }

    /// <summary>
    ///     The <c>API</c> panel is its percentage alone, ending where a count ends, with no bar and no instance — blank
    ///     before any quota is known, and in the nearly spent role at a tenth or less.
    /// </summary>
    [Theory]
    [InlineData(Framed, true, "│  71%│")]
    [InlineData(Framed, false, "│    71%│")]
    [InlineData(Compact, true, "   71% ")]
    [InlineData(Compact, false, "     71% ")]
    public async Task TheApiPanelIsItsPercentageAlone(int height, bool coloured, string drawnAt71)
    {
        var theme = Theme(coloured);
        var built = Twice();
        using var drawn = await DrawnShell.Of(80, height, theme, built);

        drawn.Press(Toggle);

        Assert.DoesNotContain(drawn.Rail(Strip(coloured)), row => row.Contains('%', StringComparison.Ordinal));

        built.RateLimit.Said("mastodon.social", "token-personal", 213);
        drawn.Redraw();

        Assert.Equal(drawnAt71, Gauge(drawn, coloured));
        Assert.DoesNotContain(drawn.Rail(Strip(coloured)), row => row.Contains("mastodon", StringComparison.Ordinal));
        Assert.DoesNotContain(drawn.Rail(Strip(coloured)), row => row.Contains('█', StringComparison.Ordinal));

        built.RateLimit.Said("mastodon.social", "token-personal", 30);
        drawn.Redraw();

        var gauge = Gauge(drawn, coloured);
        var row = Array.IndexOf(drawn.Rail(Strip(coloured)), gauge);

        Assert.Contains("10%", gauge, StringComparison.Ordinal);
        Assert.Equal(theme.For(Role.QuotaLow), drawn.Cell(row, gauge.IndexOf('%', StringComparison.Ordinal)));
    }

    /// <summary>
    ///     On compose, the description editor and the public profile form <c>\</c> is typed, as <c>-</c> and <c>+</c>
    ///     are: a backslash in the text, never a rail collapsed under the draft.
    /// </summary>
    [Theory]
    [InlineData("compose")]
    [InlineData("description")]
    [InlineData("public profile")]
    public async Task OnTheScreensWithFieldsBackslashIsTyped(string where)
    {
        using var files = new TemporaryDirectory();
        var me = AnAccount.With(address: "jeff@mastodon.social", author: "Jeff", id: "7");
        using var drawn = await DrawnShell.Of(80, 30, Themes.Plain, new AShell
        {
            Accounts = FakeAccountRelationships.Holding(me),
            PublicProfile = FakePublicProfileEditor.Holding(APublicProfile.With(displayName: string.Empty, account: me)),
        });

        switch (where)
        {
            case "compose":
                ComposeRows.Open(drawn.Shell, ComposeFor.Post);
                drawn.Redraw();
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

        foreach (var letter in "a\\b")
        {
            drawn.Press(new Key(letter));
        }

        Assert.Contains(drawn.Rows(), row => row.Contains("a\\b", StringComparison.Ordinal));
        Assert.False(drawn.Shell.RailCollapsed);
        Assert.Equal(RailLines.Width, drawn.Content.Frame.X);
    }

    /// <summary>
    ///     Where there is no rail — nobody acted as, the add screen alone on a launch — <c>\</c> collapses nothing, so
    ///     that a rail coming back once a profile is signed in is the one the reader last saw.
    /// </summary>
    [Fact]
    public async Task WithNoRailBackslashCollapsesNothing()
    {
        var registry = FakeProfileRegistry.Holding(
            "work",
            FakeProfileRegistry.Profile("work", "mastodon.social", "jeff@mastodon.social"));
        registry.Tokenless.Add("work");

        using var drawn = await DrawnShell.Of(80, 24, Themes.Plain, new AShell { Profiles = registry }, launch: true);

        Assert.False(drawn.Shell.ShowsRail);

        drawn.Press(Key.Esc);
        drawn.Press(Toggle);

        Assert.False(drawn.Shell.RailCollapsed);
        Assert.Equal(0, drawn.Content.Frame.X);
    }

    /// <summary>The keymap lists <c>\</c> with the frame's keys, everywhere.</summary>
    [Fact]
    public async Task TheKeymapListsBackslashWithTheFrame()
    {
        using var drawn = await DrawnShell.Of(80, 40, Themes.Plain);

        drawn.Press(new Key('?'));

        var rows = drawn.Rows();
        var everywhere = Array.FindIndex(rows, row => row.Contains("Everywhere", StringComparison.Ordinal));
        var key = Array.FindIndex(rows, row => row.Contains("│\\ ", StringComparison.Ordinal));

        Assert.InRange(everywhere, 0, key - 1);
        Assert.Contains("collapse or expand the rail", rows[key], StringComparison.Ordinal);
    }

    /// <summary>
    ///     And the status row does not, as it does not list the Shell width keys: set and forgotten, it would take room
    ///     on every screen.
    /// </summary>
    [Fact]
    public async Task TheStatusRowDoesNotOfferBackslash()
    {
        using var drawn = await DrawnShell.Of(300, 24, Themes.Plain);

        Assert.DoesNotContain("\\", drawn.Rows()[^1], StringComparison.Ordinal);

        drawn.Press(Toggle);

        Assert.DoesNotContain("\\", drawn.Rows()[^1], StringComparison.Ordinal);
    }

    /// <summary>
    ///     Which of <paramref name="rail" />'s rows <paramref name="label" /> is drawn on, found by its start, since a
    ///     long handle is clipped.
    /// </summary>
    private static int RowOf(string[] rail, string label) =>
        Array.FindIndex(rail, row => row.Contains(label[..Math.Min(6, label.Length)], StringComparison.Ordinal));

    /// <summary>Which row of <paramref name="strip" /> is <paramref name="kind" />'s, read off the strip by its mark.</summary>
    private static int RowOf(Shell shell, DestinationKind kind, string[] strip)
    {
        var mark = Marks[shell.Rail.Destinations.ToList().FindIndex(destination => destination.Kind == kind)];

        return Array.FindIndex(strip, row => row.Length > 3 && (row[1] == mark || row[3] == mark));
    }

    /// <summary>The strip's row for <paramref name="kind" />.</summary>
    private static string Row(DrawnShell drawn, DestinationKind kind, bool coloured)
    {
        var strip = drawn.Rail(Strip(coloured));

        return strip[RowOf(drawn.Shell, kind, strip)];
    }

    /// <summary>The strip's row the percentage is on.</summary>
    private static string Gauge(DrawnShell drawn, bool coloured) =>
        drawn.Rail(Strip(coloured)).Single(row => row.Contains('%', StringComparison.Ordinal));

    /// <summary><paramref name="kind" />'s unread count set to <paramref name="unread" />, and the shell drawn again.</summary>
    private static void Counted(DrawnShell drawn, DestinationKind kind, int unread, bool full = false)
    {
        var rail = drawn.Shell.Rail;

        rail.Update(rail.Destinations.Single(destination => destination.Kind == kind) with { Unread = unread, Full = full });
        drawn.Redraw();
    }

    /// <summary>
    ///     What a rail row is as frames go — a top edge, a bottom edge, a side, or none — so that the strip and the
    ///     expanded rail can be told to stand their panels on the same rows.
    /// </summary>
    private static char Framing(string row) => row[0] is '╭' or '╰' or '│' ? row[0] : ' ';

    /// <summary>Two profiles, so that the expanded rail's foot names the instance and the strip's must not.</summary>
    private static AShell Twice() => new()
    {
        Profiles = FakeProfileRegistry.Holding(
            "personal",
            FakeProfileRegistry.Profile("personal", "mastodon.social", "jeff@mastodon.social"),
            FakeProfileRegistry.Profile("work", "hachyderm.io", "jeff@hachyderm.io")),
    };

    /// <summary>Where the content panel's top-left corner is drawn, which is the column after the rail's last.</summary>
    private static int ContentStarts(DrawnShell drawn) => drawn.Rows()[0].IndexOf("╭ Home", StringComparison.Ordinal);

    /// <summary>How wide the strip is: seven columns in colour, and nine with the mark column kept.</summary>
    private static int Strip(bool coloured) => coloured ? 7 : 9;

    private static ITheme Theme(bool coloured) => coloured ? Themes.Dark : Themes.Plain;
}
