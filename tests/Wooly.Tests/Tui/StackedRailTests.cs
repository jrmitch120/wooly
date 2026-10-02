using Terminal.Gui.App;
using Terminal.Gui.Input;
using Wooly.Core.Http;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;

namespace Wooly.Tests.Tui;

/// <summary>
///     The stacked rail on a terminal (#272, ADR-0021): the window tells the rail whether colour is drawn, the rail
///     steps down with the terminal's height, tabbing is still one selection and one fetch whichever way the rail is
///     marked, and the gauge reads the budget the shell reports. Drawn headless and read back off the terminal's cells.
/// </summary>
/// <remarks>What the rail's rows are is <see cref="RailLinesTests" />'s; this is that they reach the terminal.</remarks>
public class StackedRailTests
{
    /// <summary>
    ///     Whether colour is drawn is the theme's to say: a built-in draws it, and the theme a terminal with no colour
    ///     gets does not.
    /// </summary>
    [Fact]
    public void ATheme_SaysWhetherItDrawsColour()
    {
        Assert.True(Themes.Dark.DrawsColour);
        Assert.True(Themes.Light.DrawsColour);
        Assert.False(Themes.Plain.DrawsColour);
    }

    /// <summary>
    ///     At 80×24 the rail has 23 rows, one more than framing every group needs: each group is a panel, and the API
    ///     panel sits on the row above the status row.
    /// </summary>
    [Fact]
    public async Task AtEightyByTwentyFourEveryGroupIsFramed()
    {
        using var drawn = await Draw(80, 24, Themes.Plain);

        var rail = drawn.Rail();

        Assert.Equal("╭ Timelines ───────╮", rail[0]);
        Assert.Equal("│▶ Home            │", rail[1]);
        Assert.Equal("╭ Explore ─────────╮", rail[6]);
        Assert.Equal("╭ Inbox ───────────╮", rail[10]);
        Assert.Equal("╭ You ─────────────╮", rail[15]);
        Assert.Equal("╭ API ─────────────╮", rail[20]);
        Assert.Equal("╰──────────────────╯", rail[22]);
    }

    /// <summary>On a terminal too short to frame them, the groups are headings rather than panels.</summary>
    [Fact]
    public async Task OnAShortTerminalTheRailIsCompact()
    {
        using var drawn = await Draw(80, 18, Themes.Plain);

        var rail = drawn.Rail();

        Assert.Equal("Timelines", rail[0].TrimEnd());
        Assert.DoesNotContain(rail, row => row.Contains('╭', StringComparison.Ordinal));
    }

    /// <summary>
    ///     In colour no rail row carries a mark, at rest or mid-tab; without, the cursor's row carries <c>▶</c> and the
    ///     lagging selection's <c>▷</c>.
    /// </summary>
    [Fact]
    public async Task TheMarksAreDrawnOnlyWithoutColour()
    {
        using var coloured = await Draw(80, 24, Themes.Dark);
        using var plain = await Draw(80, 24, Themes.Plain);

        coloured.Press(Key.Tab);
        plain.Press(Key.Tab);

        Assert.DoesNotContain(coloured.Rail(), row => row.Contains('▶', StringComparison.Ordinal) || row.Contains('▷', StringComparison.Ordinal));
        Assert.Equal("│▷ Home            │", plain.Rail()[1]);
        Assert.Equal("│▶ Local           │", plain.Rail()[2]);
    }

    /// <summary>
    ///     In colour the cursor's row is drawn on <c>rail-cursor</c> mid-tab and the selected row stays on
    ///     <c>rail-current</c> — and a jump into the next group lights that group's frame on the press, before the
    ///     selection has followed.
    /// </summary>
    [Fact]
    public async Task InColourTheCursorAndTheSelectionAreBandsAndTheCursorsGroupIsLit()
    {
        var built = new AShell();

        using var drawn = await Draw(80, 24, Themes.Dark, built);

        drawn.Press(Key.Tab);

        Assert.Equal(Themes.Dark.For(Role.RailCurrent), drawn.Cell(1, 1));
        Assert.Equal(Themes.Dark.For(Role.RailCursor), drawn.Cell(2, 1));
        Assert.Equal(Themes.Dark.For(Role.PanelBorderActive), drawn.Cell(0, 0));

        drawn.Press((Key)'`');

        Assert.Equal(0, drawn.Shell.Rail.Current);
        Assert.Equal(Themes.Dark.For(Role.PanelBorder), drawn.Cell(0, 0));
        Assert.Equal(Themes.Dark.For(Role.PanelBorderActive), drawn.Cell(6, 0));
        Assert.Equal(Themes.Dark.For(Role.RailCursor), drawn.Cell(7, 1));
    }

    /// <summary>
    ///     Six quick tabs are six cursor moves, one selection and one fetch, whether the rail marks them with bands or
    ///     with glyphs — how the rail is drawn has no say in what a press costs (ADR-0014).
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SixQuickTabsAreOneSelectionAndOneFetch(bool colour)
    {
        var built = new AShell();

        using var drawn = await Draw(80, 24, colour ? Themes.Dark : Themes.Plain, built);

        var reads = built.Timelines.Reads.Count;
        var inbox = built.Notifications.Reads.Count;

        for (var press = 0; press < 6; press++)
        {
            drawn.Press(Key.Tab);
        }

        Assert.Equal(1, built.Host.Waiting);
        Assert.Equal(6, drawn.Shell.Rail.Cursor);
        Assert.Equal(0, drawn.Shell.Rail.Current);

        built.Host.Settle();
        drawn.Redraw();

        Assert.Equal(6, drawn.Shell.Rail.Current);
        Assert.Equal(reads, built.Timelines.Reads.Count);
        Assert.Equal(inbox + 1, built.Notifications.Reads.Count);
    }

    /// <summary>
    ///     The gauge reads the budget the shell reports, and that includes the moment just after a switch: the old
    ///     profile's budget is not drawn as the new one's, and the new one's is drawn once its instance says it (#257).
    /// </summary>
    [Fact]
    public async Task TheGaugeReadsTheBudgetTheShellReports()
    {
        var built = new AShell
        {
            Profiles = FakeProfileRegistry.Holding(
                "personal",
                FakeProfileRegistry.Profile("personal", "mastodon.social", "jeff@mastodon.social"),
                FakeProfileRegistry.Profile("work", "hachyderm.io", "jeff@hachyderm.io")),
            RateLimit = FakeRateLimitReport.Of(250),
        };

        using var drawn = await Draw(80, 24, Themes.Plain, built);

        Assert.Equal(drawn.Shell.Quota, built.RateLimit.Latest);
        Assert.Contains("%", drawn.Rail()[21], StringComparison.Ordinal);

        drawn.Shell.Press(ShellKey.CtrlP);
        Assert.IsType<ProfilesScreen>(drawn.Shell.Screen).Pick("work");
        drawn.Shell.Press(ShellKey.Enter);
        built.Host.Drain();
        drawn.Redraw();

        Assert.Null(drawn.Shell.Quota);
        Assert.Equal(" hachyderm.io", drawn.Rail()[20].Trim('│').TrimEnd());
        Assert.Equal("│                  │", drawn.Rail()[21]);

        built.RateLimit.Latest = new RateLimitQuota(150, 300, null);
        drawn.Redraw();

        Assert.Equal("│ ██████░░░░░░  50%│", drawn.Rail()[21]);
    }

    private static async Task<Drawn> Draw(int width, int height, ITheme theme, AShell? built = null)
    {
        built ??= new AShell();

        var shell = await built.Opened();

        var application = Application.Create();
        application.Init("ansi");
        application.Driver!.SetScreenSize(width, height);

        var window = new ShellWindow(shell, theme, built.Clock, () => { }, FakePictures.DrawingNothing());

        application.Begin(window);
        application.LayoutAndDraw(true);

        return new Drawn(application, window, shell);
    }

    private sealed record Drawn(IApplication Application, ShellWindow Window, Wooly.Tui.Shell.Shell Shell) : IDisposable
    {
        public void Redraw() => Application.LayoutAndDraw(true);

        public void Press(Key key)
        {
            Window.NewKeyDownEvent(key);
            Redraw();
        }

        /// <summary>The rail's columns of every row above the status row.</summary>
        public string[] Rail()
        {
            var cells = Application.Driver!.Contents!;

            return
            [
                .. Enumerable.Range(0, cells.GetLength(0) - 1).Select(row => string.Concat(
                    Enumerable.Range(0, RailLines.Width).Select(column => cells[row, column].Grapheme))),
            ];
        }

        public Terminal.Gui.Drawing.Attribute Cell(int row, int column) =>
            Application.Driver!.Contents![row, column].Attribute!.Value;

        public void Dispose()
        {
            Window.Dispose();
            Application.Dispose();
        }
    }
}
