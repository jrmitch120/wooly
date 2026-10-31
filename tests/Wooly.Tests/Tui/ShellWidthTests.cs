using Terminal.Gui.Input;
using Wooly.Core.Accounts;
using Wooly.Core.Configuration;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;
using Color = Terminal.Gui.Drawing.Color;

namespace Wooly.Tests.Tui;

/// <summary>
///     The <b>Shell width</b> (#414): <c>-</c> narrows the shell a step, <c>+</c> and <c>=</c> widen it, and the shell
///     is drawn centred at that share of the terminal, never under 80 columns, with the terminal's own cells either
///     side. Pressed and read back off a headless terminal, since the promise is about where the cells land.
/// </summary>
public class ShellWidthTests
{
    /// <summary>The terminal's own foreground and background, which is what a cell outside the shell is left at.</summary>
    private static readonly Attribute TheTerminalsOwn = new(Color.None, Color.None);

    /// <summary>The escapes that say "the terminal's own colours", rather than any of this client's.</summary>
    private const string OwnColours = "\u001b[39m\u001b[49m";

    /// <summary>What is typed on a screen taking typing: each of the three keys, between letters.</summary>
    private const string Typed = "a-b+c=d";

    /// <summary>
    ///     The first column inside the content panel of a shell narrowed to 180 columns on a 200-column terminal: the
    ///     margin's 10, the rail's 20, and the panel's left edge.
    /// </summary>
    private const int InsideTheContent = 10 + RailLines.Width + 1;

    /// <summary>The panel's right edge in that shell, the shell's last column.</summary>
    private const int ContentsRightEdge = 189;

    [Fact]
    public async Task MinusNarrowsTheShellAStepCentredInTheTerminal()
    {
        using var drawn = await DrawnShell.Of(200, 24, Midnight());

        drawn.Press(new Key('-'));

        // 90% of 200 is 180, which leaves 20 to share: 10 either side.
        Assert.Equal((10, 180), Shell(drawn));
        Assert.Contains("Width 90%", drawn.Rows()[^1], StringComparison.Ordinal);
    }

    /// <summary>
    ///     What a width key says is said for a moment and then let go of, so the status row goes back to its keys
    ///     rather than holding a remark about a press long since made — a step taken, or one refused at either end.
    /// </summary>
    [Theory]
    [InlineData('-', "Width 90%")]
    [InlineData('+', "Full width")]
    public async Task WhatAWidthKeySaysIsSaidForAMomentAndThenClears(char key, string said)
    {
        using var drawn = await DrawnShell.Of(200, 24, Midnight());

        drawn.Press(new Key(key));

        Assert.Contains(said, drawn.Rows()[^1], StringComparison.Ordinal);
        Assert.Contains(ShellTiming.Default.WidthSaidFor, drawn.Built.Host.Delays);

        drawn.Built.Host.Settle();
        drawn.Redraw();

        Assert.Null(drawn.Shell.Notice);
        Assert.DoesNotContain(said, drawn.Rows()[^1], StringComparison.Ordinal);
    }

    /// <summary><c>+</c> and <c>=</c> each widen a step back, saying where they landed.</summary>
    [Theory]
    [InlineData('+')]
    [InlineData('=')]
    public async Task PlusAndEqualsWidenTheShellAStep(char wider)
    {
        using var drawn = await DrawnShell.Of(200, 24, Midnight());

        drawn.Press(new Key('-'));
        drawn.Press(new Key('-'));
        drawn.Press(new Key(wider));

        Assert.Equal((10, 180), Shell(drawn));
        Assert.Contains("Width 90%", drawn.Rows()[^1], StringComparison.Ordinal);

        drawn.Press(new Key(wider));

        Assert.Equal((0, 200), Shell(drawn));
        Assert.Contains("Width 100%", drawn.Rows()[^1], StringComparison.Ordinal);
    }

    /// <summary>At the whole terminal there is no wider, and <c>+</c> says so rather than doing nothing silently.</summary>
    [Fact]
    public async Task PlusAtTheWholeTerminalSaysFullWidth()
    {
        using var drawn = await DrawnShell.Of(200, 24, Midnight());

        drawn.Press(new Key('+'));

        Assert.Equal((0, 200), Shell(drawn));
        Assert.Contains("Full width", drawn.Rows()[^1], StringComparison.Ordinal);
    }

    /// <summary>Half the terminal is the narrowest step, and <c>-</c> there says so and leaves it.</summary>
    [Fact]
    public async Task MinusAtHalfTheTerminalSaysNarrowest()
    {
        using var drawn = await DrawnShell.Of(200, 24, Midnight());

        foreach (var percent in new[] { 90, 80, 70, 60, 50 })
        {
            drawn.Press(new Key('-'));

            Assert.Contains($"Width {percent}%", drawn.Rows()[^1], StringComparison.Ordinal);
        }

        drawn.Press(new Key('-'));

        Assert.Equal((50, 100), Shell(drawn));
        Assert.Contains("Narrowest", drawn.Rows()[^1], StringComparison.Ordinal);
    }

    /// <summary>
    ///     Nor is the shell ever narrowed under the 80 columns it is laid out for: on a 120-column terminal 70% is 84
    ///     columns and 60% would be 72, so <c>-</c> stops at 70% and says so.
    /// </summary>
    [Fact]
    public async Task MinusThatWouldGoUnderEightyColumnsSaysNarrowest()
    {
        using var drawn = await DrawnShell.Of(120, 24, Midnight());

        drawn.Press(new Key('-'));
        drawn.Press(new Key('-'));
        drawn.Press(new Key('-'));

        Assert.Equal((18, 84), Shell(drawn));

        drawn.Press(new Key('-'));

        Assert.Equal((18, 84), Shell(drawn));
        Assert.Contains("Narrowest", drawn.Rows()[^1], StringComparison.Ordinal);
    }

    /// <summary>On a terminal 80 columns wide there is no room for less, so the shell is the whole of it.</summary>
    [Fact]
    public async Task OnAnEightyColumnTerminalMinusSaysNarrowest()
    {
        using var drawn = await DrawnShell.Of(80, 24, Midnight());

        drawn.Press(new Key('-'));

        Assert.Equal((0, 80), Shell(drawn));
        Assert.Contains("Narrowest", drawn.Rows()[^1], StringComparison.Ordinal);
    }

    /// <summary>
    ///     A resized terminal keeps the share and re-centres the shell in it — floored at 80 columns on one too narrow for
    ///     the share, and the whole of one 80 wide or less — without the share itself changing: back on a wide terminal,
    ///     it is what it was.
    /// </summary>
    [Theory]
    [InlineData(300, 45, 210)]
    [InlineData(100, 10, 80)]
    [InlineData(80, 0, 80)]
    [InlineData(70, 0, 70)]
    public async Task AResizedTerminalKeepsTheShareAndReCentresTheShell(int terminal, int left, int columns)
    {
        using var drawn = await DrawnShell.Of(200, 24, Midnight());

        drawn.Press(new Key('-'));
        drawn.Press(new Key('-'));
        drawn.Press(new Key('-'));

        drawn.Resize(terminal, 24);

        Assert.Equal((left, columns), Shell(drawn));
        AssertMargins(drawn, left, terminal - left - columns);

        drawn.Resize(200, 24);

        Assert.Equal((30, 140), Shell(drawn));
    }

    /// <summary>
    ///     With the rail hidden — signing a profile whose token is missing in again, the add screen alone on a launch — the
    ///     shell narrows and centres the same way, the add screen's panel taking all of it.
    /// </summary>
    [Fact]
    public async Task TheRailHiddenShellIsCentredTheSameWay()
    {
        var registry = FakeProfileRegistry.Holding(
            "work",
            FakeProfileRegistry.Profile("work", "mastodon.social", "jeff@mastodon.social"));
        registry.Tokenless.Add("work");

        using var drawn = await DrawnShell.Of(200, 24, Midnight(), new AShell { Profiles = registry }, launch: true);

        Assert.False(drawn.Shell.ShowsRail);

        drawn.Press(new Key('-'));

        Assert.Equal((10, 180), Shell(drawn));
        Assert.StartsWith("╭ Sign in again ─", drawn.Rows()[0][10..], StringComparison.Ordinal);
        Assert.Contains("Width 90%", drawn.Rows()[^1], StringComparison.Ordinal);
        AssertMargins(drawn, left: 10, right: 10);
    }

    /// <summary>The keymap lists the three keys with the frame's, everywhere.</summary>
    [Fact]
    public async Task TheKeymapListsTheKeysWithTheFrame()
    {
        using var drawn = await DrawnShell.Of(80, 40, Themes.Plain);

        drawn.Press(new Key('?'));

        var rows = drawn.Rows();
        var everywhere = Array.FindIndex(rows, row => row.Contains("Everywhere", StringComparison.Ordinal));
        var keys = Array.FindIndex(rows, row => row.Contains("- / + or =", StringComparison.Ordinal));

        Assert.InRange(everywhere, 0, keys - 1);
        Assert.Contains("narrow or widen the shell", rows[keys], StringComparison.Ordinal);
    }

    /// <summary>
    ///     And the status row does not: they are set and forgotten, and would take room on every screen. On a terminal
    ///     wide enough for every key the feed offers, none of them is a width key.
    /// </summary>
    [Fact]
    public async Task TheStatusRowDoesNotOfferTheKeys()
    {
        using var drawn = await DrawnShell.Of(300, 24, Themes.Plain);

        var status = drawn.Rows()[^1];

        Assert.DoesNotContain("…+", status, StringComparison.Ordinal);
        Assert.DoesNotContain(": -", status, StringComparison.Ordinal);
        Assert.DoesNotContain(": +", status, StringComparison.Ordinal);
        Assert.DoesNotContain(": =", status, StringComparison.Ordinal);
    }

    /// <summary>
    ///     On a screen taking typing the three keys are typed, as <c>?</c> and <c>/</c> are: a search for
    ///     <c>c++</c> or <c>a=b</c> is a search, never a narrower shell.
    /// </summary>
    [Fact]
    public async Task SearchTakesTheKeysAsLetters()
    {
        using var drawn = await Narrowed();

        drawn.Press(new Key('/'));
        drawn.Settle();
        Type(drawn, Typed);

        Assert.Equal(Typed, Assert.IsType<SearchScreen>(drawn.Shell.Screen).Query);
        Assert.Equal((10, 180), Shell(drawn));
    }

    /// <summary>
    ///     And a filter: on the people someone follows, with the filter prompt open, the three keys are what the filter
    ///     reads, never a step of the shell.
    /// </summary>
    [Fact]
    public async Task AFilterTakesTheKeysAsLetters()
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "110", account: "ben@hachyderm.io")),
            Accounts = FakeAccountRelationships.Holding(
                AnAccount.With(address: "ben@hachyderm.io"),
                AnAccount.With(id: "1", address: "ann@hachyderm.io", author: "Ann")),
        };
        using var drawn = await Narrowed(built);

        await drawn.Shell.OpenAuthor();
        built.Host.Drain();
        drawn.Press(Key.W);
        built.Host.Drain();
        drawn.Press(Key.F);

        var follows = Assert.IsType<FollowsScreen>(drawn.Shell.Screen);

        Assert.True(follows.IsTyping);

        Type(drawn, Typed);

        Assert.Equal(Typed, follows.Filter);
        Assert.Equal((10, 180), Shell(drawn));

        // Only the step that narrowed it to begin with.
        Assert.Equal([90], built.ShellPreferences.Widths);
    }

    /// <summary>
    ///     And on compose, whose post takes them as it takes every other letter — and which sits inside the narrowed
    ///     shell's content panel, as it sits inside the panel at full width.
    /// </summary>
    [Fact]
    public async Task ComposeTakesTheKeysAsLettersInsideTheNarrowedShell()
    {
        using var drawn = await Narrowed();

        ComposeRows.Open(drawn.Shell, ComposeFor.Post);
        drawn.Redraw();
        Type(drawn, Typed);

        AssertTypedInsideTheContentPanel(drawn);
    }

    /// <summary>And on the description editor pushed over it, inside the same panel.</summary>
    [Fact]
    public async Task TheDescriptionEditorTakesTheKeysAsLettersInsideTheNarrowedShell()
    {
        using var files = new TemporaryDirectory();
        using var drawn = await Narrowed();

        drawn.Shell.Press(ShellKey.C);
        drawn.Shell.Paste(files.WriteFile("cat.png"));
        drawn.Settle();
        drawn.Press(Key.CursorUp);
        drawn.Press(Key.Enter);

        Assert.IsType<DescriptionScreen>(drawn.Shell.Screen);

        Type(drawn, Typed);

        AssertTypedInsideTheContentPanel(drawn);
    }

    /// <summary>And on the public profile form, whose display name is the field it opens on, inside the same panel.</summary>
    [Fact]
    public async Task ThePublicProfileFormTakesTheKeysAsLettersInsideTheNarrowedShell()
    {
        var me = AnAccount.With(address: "jeff@mastodon.social", author: "Jeff", id: "7");

        using var drawn = await Narrowed(new AShell
        {
            Accounts = FakeAccountRelationships.Holding(me),
            PublicProfile = FakePublicProfileEditor.Holding(APublicProfile.With(displayName: string.Empty, account: me)),
        });

        drawn.Shell.Rail.GoTo(drawn.Shell.Rail.Of(DestinationKind.Profile));
        drawn.Settle();
        drawn.Press(new Key('E'));
        drawn.Settle();

        Assert.IsType<PublicProfileScreen>(drawn.Shell.Screen);

        Type(drawn, Typed);

        AssertTypedInsideTheContentPanel(drawn);
    }

    /// <summary>
    ///     A resize that leaves the shell where it was — 70% of 100 and of 101 are both under the floor, so 80 columns from
    ///     the eleventh either way — still leaves the column it adds to the terminal, not to a black Terminal.Gui cleared
    ///     it to.
    /// </summary>
    [Fact]
    public async Task AResizeThatLeavesTheShellWhereItWasLeavesTheNewColumnsToTheTerminal()
    {
        using var drawn = await DrawnShell.Of(200, 24, Midnight());

        drawn.Press(new Key('-'));
        drawn.Press(new Key('-'));
        drawn.Press(new Key('-'));
        drawn.Resize(100, 24);
        drawn.Resize(101, 24);

        Assert.Equal((10, 80), Shell(drawn));
        AssertMargins(drawn, left: 10, right: 11);
    }

    /// <summary>
    ///     The margins are the terminal's: blank, at its default attribute, under a theme with a page of its own — no
    ///     role, so no theme entry, painted them. An odd column left over falls on the right, so the left edge moves only
    ///     when the width does.
    /// </summary>
    [Fact]
    public async Task TheMarginsAreLeftAtTheTerminalsDefaultWithTheSpareColumnOnTheRight()
    {
        using var drawn = await DrawnShell.Of(201, 24, Midnight());

        drawn.Press(new Key('-'));

        // 90% of 201 is 180.9, so 180 columns, and 21 to share: 10 on the left, 11 on the right.
        Assert.Equal((10, 180), Shell(drawn));
        AssertMargins(drawn, left: 10, right: 11);
    }

    /// <summary>
    ///     Under every theme, and with no colour at all: the margins are written as the terminal's own colours, never as
    ///     any of the theme's — nor as the white on black Terminal.Gui clears a screen to.
    /// </summary>
    [Theory]
    [InlineData("dark")]
    [InlineData("light")]
    [InlineData("plain")]
    public async Task TheMarginsAreTheTerminalsOwnUnderEveryTheme(string name)
    {
        var theme = name switch
        {
            "dark" => Themes.Dark,
            "light" => Themes.Light,
            _ => Themes.Plain,
        };

        using var drawn = await DrawnShell.Of(200, 24, theme);

        drawn.Press(new Key('-'));

        AssertMargins(drawn, left: 10, right: 10, theme);
        Assert.StartsWith($"{OwnColours}          ", drawn.Ansi(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     And as the running loop draws a press, onto the frame before rather than a cleared screen: what the shell drew
    ///     where the margins now are is gone, not left behind beside it.
    /// </summary>
    [Fact]
    public async Task NarrowingAsTheLoopDrawsLeavesNothingOfTheWiderShellInTheMargins()
    {
        using var drawn = await DrawnShell.Of(200, 24, Midnight());

        drawn.PressAsTheLoopDraws(new Key('-'));

        Assert.Equal((10, 180), Shell(drawn));
        AssertMargins(drawn, left: 10, right: 10);
    }

    /// <summary>The shell on a 200-column terminal, narrowed a step to 180 columns from the eleventh.</summary>
    private static async Task<DrawnShell> Narrowed(AShell? built = null)
    {
        var drawn = await DrawnShell.Of(200, 30, Midnight(), built);

        drawn.Press(new Key('-'));

        Assert.Equal((10, 180), Shell(drawn));

        return drawn;
    }

    /// <summary>
    ///     <see cref="Typed" /> is drawn whole, all three keys typed rather than acted on, inside the narrowed shell's
    ///     content panel — and the shell is the width it was. The edge beside it is <c>│</c> whatever the screen's rows:
    ///     every screen typed into here has widgets of its own and draws no scrollbar (#500).
    /// </summary>
    private static void AssertTypedInsideTheContentPanel(DrawnShell drawn)
    {
        var rows = drawn.Rows();
        var row = Array.FindIndex(rows, text => text.Contains(Typed, StringComparison.Ordinal));

        Assert.True(row >= 0, string.Join('\n', rows));
        Assert.InRange(rows[row].IndexOf(Typed, StringComparison.Ordinal), InsideTheContent, ContentsRightEdge - Typed.Length);
        Assert.True(drawn.Shell.Screen.HasWidgetsOfItsOwn);
        Assert.Equal('│', rows[row][ContentsRightEdge]);
        Assert.Equal((10, 180), Shell(drawn));
    }

    private static void Type(DrawnShell drawn, string text)
    {
        foreach (var letter in text)
        {
            drawn.Press(new Key(letter));
        }
    }

    /// <summary>
    ///     Where the shell is drawn, as its first column and how many columns it takes: from the rail's top-left corner to
    ///     the content panel's top-right one.
    /// </summary>
    internal static (int Left, int Columns) Shell(DrawnShell drawn)
    {
        var top = drawn.Rows()[0];
        var left = top.IndexOf('╭', StringComparison.Ordinal);
        var right = top.LastIndexOf('╮');

        return (left, right - left + 1);
    }

    /// <summary>
    ///     Every cell of every row in the <paramref name="left" /> columns at the start and the <paramref name="right" /> at
    ///     the end is blank and at the terminal's default attribute — and, under <paramref name="theme" />, the columns
    ///     just inside them are the shell's.
    /// </summary>
    private static void AssertMargins(DrawnShell drawn, int left, int right, ITheme? theme = null)
    {
        var rows = drawn.Rows();
        var width = rows[0].Length;
        var page = (theme ?? Midnight()).For(Role.Body);

        for (var row = 0; row < rows.Length; row++)
        {
            foreach (var column in Enumerable.Range(0, left).Concat(Enumerable.Range(width - right, right)))
            {
                Assert.Equal(' ', rows[row][column]);
                Assert.Equal(TheTerminalsOwn, drawn.Cell(row, column));
            }

            Assert.Equal(page.Background, drawn.Cell(row, left).Background);
            Assert.Equal(page.Background, drawn.Cell(row, width - right - 1).Background);
        }
    }

    /// <summary>A theme with a page of its own, so that a cell the shell drew is told from one the terminal kept.</summary>
    internal static ITheme Midnight() => Themes.Chosen(
        new WoolyConfig
        {
            Theme = "midnight",
            Themes = new Dictionary<string, ThemeConfig> { ["midnight"] = new() { Background = "#12111a" } },
        },
        "/somewhere/config.toml");
}
