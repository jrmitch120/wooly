using Terminal.Gui.Input;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;

namespace Wooly.Tests.Tui;

/// <summary>
///     Typing <c>@</c> at the start of a word in the post opens a list of people to mention under it (#318), driven
///     the way a reader drives it: keys into the real window, and what is drawn read back off the terminal.
/// </summary>
public class MentionListTests
{
    /// <summary>The people every test has already seen on its home timeline.</summary>
    private static readonly AShell Seen = new()
    {
        Timelines = FakeTimelineReader.Holding(
            APost.With(id: "1", account: "maria@fosstodon.org", author: "Maria Gonzalez"),
            APost.With(id: "2", account: "mark@mastodon.social", author: "Mark"),
            APost.With(id: "3", account: "ben@hachyderm.io", author: "Ben")),
    };

    /// <summary>An @-word opens the list on the row under it, left-aligned on its <c>@</c>, naming everybody it matches.</summary>
    [Fact]
    public async Task AnAtWordOpensTheListUnderIt()
    {
        using var drawn = await Composing();

        Type(drawn, "hi @ma");

        var list = List(drawn);
        var editor = Editor(drawn).Frame;
        var rows = ListRows(drawn);

        Assert.True(list.Visible);
        Assert.Equal(editor.Y + 1, list.Frame.Y);
        Assert.Equal(editor.X + 3, list.Frame.X);
        Assert.Contains(rows, row => row.Contains("Maria Gonzalez  @maria@fosstodon.org", StringComparison.Ordinal));
        Assert.Contains(rows, row => row.Contains("Mark  @mark@mastodon.social", StringComparison.Ordinal));
        Assert.DoesNotContain(rows, row => row.Contains("@ben", StringComparison.Ordinal));
        Assert.Contains("↑↓ pick  tab insert  esc close", rows[^1], StringComparison.Ordinal);
    }

    /// <summary>An <c>@</c> inside a word, as in an email address, opens nothing; nor does a word nobody matches.</summary>
    [Theory]
    [InlineData("name@ma")]
    [InlineData("@zzz")]
    [InlineData("@ben@hachyderm.io")]
    public async Task NoListOpensWhereThereIsNothingToSuggest(string typed)
    {
        using var drawn = await Composing();

        Type(drawn, typed);

        Assert.False(List(drawn).Visible);
    }

    /// <summary><c>↓</c> and <c>↑</c> move the pick, and wrap round at either end.</summary>
    [Fact]
    public async Task TheArrowsMoveThePickAndWrap()
    {
        using var drawn = await Composing();

        Type(drawn, "@ma");

        Assert.StartsWith("│▌ Maria", ListRows(drawn)[1], StringComparison.Ordinal);

        drawn.Press(Key.CursorDown);

        Assert.StartsWith("│▌ Mark", ListRows(drawn)[2], StringComparison.Ordinal);

        drawn.Press(Key.CursorDown);

        Assert.StartsWith("│▌ Maria", ListRows(drawn)[1], StringComparison.Ordinal);

        drawn.Press(Key.CursorUp);

        Assert.StartsWith("│▌ Mark", ListRows(drawn)[2], StringComparison.Ordinal);
        Assert.Equal("@ma", Editor(drawn).Text);
    }

    /// <summary>
    ///     <c>tab</c> puts the full address of somebody on another instance in place of the word, with a space to carry
    ///     on after; <c>enter</c> the short one of somebody on the profile's own. Either way the list closes.
    /// </summary>
    [Fact]
    public async Task TabAndEnterInsertTheAddressWithASpace()
    {
        using var drawn = await Composing();

        Type(drawn, "hi @ma");
        drawn.Press(Key.Tab);

        Assert.Equal("hi @maria@fosstodon.org ", Editor(drawn).Text);
        Assert.False(List(drawn).Visible);

        Type(drawn, "and @ma");
        drawn.Press(Key.CursorDown);
        drawn.Press(Key.Enter);

        Assert.Equal("hi @maria@fosstodon.org and @mark ", Editor(drawn).Text);
        Assert.Equal("hi @maria@fosstodon.org and @mark ", Assert.IsType<ComposeScreen>(drawn.Shell.Screen).Text);
    }

    /// <summary>One undo takes an inserted mention back out, and puts back the word it replaced.</summary>
    [Fact]
    public async Task UndoTakesTheInsertionBackOut()
    {
        using var drawn = await Composing();

        Type(drawn, "hi @ma");
        drawn.Press(Key.Tab);
        drawn.Press(Key.Z.WithCtrl);

        Assert.Equal("hi @ma", Editor(drawn).Text);
    }

    /// <summary>The word is replaced where it is, on a second line or a row the line has wrapped onto.</summary>
    [Fact]
    public async Task TheWordIsReplacedOnALaterLineOrAWrappedRow()
    {
        using var drawn = await Composing();
        var wide = new string('x', Editor(drawn).Frame.Width - 4);

        Type(drawn, "first");
        drawn.Press(Key.Enter);
        Type(drawn, $"{wide} then @ma");
        drawn.Press(Key.Tab);

        Assert.Equal(
            ["first", $"{wide} then @maria@fosstodon.org "],
            Editor(drawn).Text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'));
    }

    /// <summary>
    ///     <c>esc</c> closes the list and nothing else: the draft is still there, and the list stays shut for the rest
    ///     of that word — until a new @-word is started.
    /// </summary>
    [Fact]
    public async Task EscClosesTheListAndLeavesTheDraft()
    {
        using var drawn = await Composing();

        Type(drawn, "@ma");
        drawn.Press(Key.Esc);

        Assert.IsType<ComposeScreen>(drawn.Shell.Screen);
        Assert.Equal("@ma", Editor(drawn).Text);
        Assert.False(List(drawn).Visible);

        Type(drawn, "r");

        Assert.False(List(drawn).Visible);

        Type(drawn, " @b");

        Assert.True(List(drawn).Visible);
    }

    /// <summary>With the list closed, <c>esc</c> throws the draft away as it always has.</summary>
    [Fact]
    public async Task EscWithTheListClosedThrowsTheDraftAway()
    {
        using var drawn = await Composing();

        Type(drawn, "hello");
        drawn.Press(Key.Esc);

        Assert.IsNotType<ComposeScreen>(drawn.Shell.Screen);
    }

    /// <summary>With the list closed, <c>enter</c> starts a new line.</summary>
    [Fact]
    public async Task EnterWithTheListClosedAddsALine()
    {
        using var drawn = await Composing();

        Type(drawn, "hi");
        drawn.Press(Key.Enter);
        Type(drawn, "there");

        Assert.Equal(["hi", "there"], Editor(drawn).Text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'));
    }

    /// <summary>No list in the warning, which stays a plain line.</summary>
    [Fact]
    public async Task NoListOpensInTheWarning()
    {
        using var drawn = await Composing();

        drawn.Press(Key.W.WithCtrl);
        Type(drawn, "@ma");

        Assert.Equal("@ma", Assert.IsType<ComposeScreen>(drawn.Shell.Screen).Warning);
        Assert.False(List(drawn).Visible);
    }

    /// <summary>On a line that has wrapped, the list hangs under the wrapped row being typed on.</summary>
    [Fact]
    public async Task OnAWrappedLineTheListIsUnderTheRowBeingTyped()
    {
        using var drawn = await Composing();
        var editor = Editor(drawn).Frame;

        Type(drawn, $"{new string('x', editor.Width - 4)} more words and @ma");

        Assert.Equal(1, Editor(drawn).CurrentRow);
        Assert.Equal(editor.Y + 2, List(drawn).Frame.Y);
    }

    /// <summary>Near the editor's foot, where there is no room under the line, the list opens over it.</summary>
    [Fact]
    public async Task NearTheFootTheListOpensAbove()
    {
        using var drawn = await Composing();
        var editor = Editor(drawn).Frame;

        for (var line = 0; line < editor.Height - 2; line++)
        {
            drawn.Press(Key.Enter);
        }

        Type(drawn, "@ma");

        var caret = editor.Y + editor.Height - 2;

        Assert.Equal(caret, editor.Y + Editor(drawn).CurrentRow - Editor(drawn).Viewport.Y);
        Assert.Equal(caret, List(drawn).Frame.Bottom);
    }

    /// <summary>Every cell of the open list is a role the theme answers.</summary>
    [Theory]
    [InlineData("dark")]
    [InlineData("light")]
    public async Task EveryCellOfTheListIsARole(string name)
    {
        var theme = name == "dark" ? Themes.Dark : Themes.Light;

        using var drawn = await Composing(theme);

        Type(drawn, "@ma");

        var answered = Enum.GetValues<Role>()
                           .SelectMany(role => new[] { theme.For(role), theme.Banded(role) })
                           .ToHashSet();

        Assert.True(List(drawn).Visible);
        Assert.All(drawn.Cells(), cell => Assert.Contains(cell, answered));
    }

    /// <summary>
    ///     The profile's follows join an open list as they land (#321): the first <c>@</c> asks for them, and the list
    ///     already open under the word redraws with them in it, after the people on screen.
    /// </summary>
    [Fact]
    public async Task FollowsJoinAnOpenListAsTheyLand()
    {
        using var drawn = await Composing(
            accounts: FakeAccountRelationships.Holding(
                null,
                AnAccount.With(id: "9", address: "mabel@c.social", author: "Mabel")));

        Type(drawn, "@ma");

        Assert.DoesNotContain(ListRows(drawn), row => row.Contains("@mabel", StringComparison.Ordinal));

        drawn.Settle();

        var rows = ListRows(drawn);

        Assert.Contains("Mark", rows[2], StringComparison.Ordinal);
        Assert.Contains("Mabel  @mabel@c.social", rows[3], StringComparison.Ordinal);
    }

    private static async Task<DrawnShell> Composing(ITheme? theme = null, FakeAccountRelationships? accounts = null)
    {
        var built = new AShell { Timelines = Seen.Timelines, Accounts = accounts ?? FakeAccountRelationships.HoldingNobody() };
        var drawn = await DrawnShell.Of(80, 24, theme ?? Themes.Dark, built);

        drawn.Shell.Compose();
        drawn.Redraw();

        return drawn;
    }

    private static void Type(DrawnShell drawn, string text)
    {
        foreach (var letter in text)
        {
            drawn.Window.NewKeyDownEvent(new Key(letter));
        }

        drawn.Redraw();
    }

    private static ComposeEditor Editor(DrawnShell drawn) => drawn.Window.SubViews.OfType<ComposeEditor>().Single();

    private static PaintedView List(DrawnShell drawn) =>
        drawn.Window.SubViews.OfType<PaintedView>().Single(view => view.Id == MentionList.Id);

    /// <summary>The list's rows as drawn on the terminal.</summary>
    private static IReadOnlyList<string> ListRows(DrawnShell drawn)
    {
        var at = List(drawn).FrameToScreen();

        return [.. drawn.Rows()[at.Y..at.Bottom].Select(row => row.Substring(at.X, at.Width))];
    }
}
