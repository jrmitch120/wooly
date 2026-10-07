using Terminal.Gui.Input;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;

namespace Wooly.Tests.Tui;

/// <summary>
///     Typing <c>@</c> at the start of a word in the post opens a list of people to mention under it (#318), driven
///     the way a reader drives it: keys and clicks into <see cref="ComposeView" /> over a shell, which owns the list
///     (#366), and what is drawn read back off the terminal.
/// </summary>
public class MentionListTests
{
    /// <summary>A content viewport short enough that the compose editor has no room for a list of five.</summary>
    private const int Short = 10;

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

        var list = List(drawn).FrameToScreen();
        var editor = Editor(drawn).FrameToScreen();
        var rows = ListRows(drawn);

        Assert.True(List(drawn).Visible);
        Assert.Equal(editor.Y + 1, list.Y);
        Assert.Equal(editor.X + 3, list.X);
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
        var wide = new string('x', Editor(drawn).FrameToScreen().Width - 4);

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
        var editor = Editor(drawn).FrameToScreen();

        Type(drawn, $"{new string('x', editor.Width - 4)} more words and @ma");

        Assert.Equal(1, Editor(drawn).CurrentRow);
        Assert.Equal(editor.Y + 2, List(drawn).FrameToScreen().Y);
    }

    /// <summary>Near the editor's foot, where there is no room under the line, the list opens over it.</summary>
    [Fact]
    public async Task NearTheFootTheListOpensAbove()
    {
        using var drawn = await Composing();
        var editor = Editor(drawn).FrameToScreen();

        for (var line = 0; line < editor.Height - 2; line++)
        {
            drawn.Press(Key.Enter);
        }

        Type(drawn, "@ma");

        var caret = editor.Y + editor.Height - 2;

        Assert.Equal(caret, editor.Y + Editor(drawn).CurrentRow - Editor(drawn).Viewport.Y);
        Assert.Equal(caret, List(drawn).FrameToScreen().Bottom);
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

    /// <summary>A click on a person inserts them, as <c>tab</c> does on the picked one, and closes the list.</summary>
    [Fact]
    public async Task AClickOnAPersonInsertsThem()
    {
        using var drawn = await Composing();

        Type(drawn, "hi @ma");

        var at = List(drawn).FrameToScreen();

        drawn.Click(at.X + 4, at.Y + 2);

        Assert.Equal("hi @mark ", Editor(drawn).Text);
        Assert.False(List(drawn).Visible);
    }

    /// <summary>
    ///     A click outside the open list closes it and does nothing else — not even move the caret, so the next letter
    ///     typed carries on the word — whether the terminal reports it as one click or as the press, release and click
    ///     it is made of.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AClickOutsideTheListClosesItAndLeavesTheDraft(bool asAPressAndRelease)
    {
        using var drawn = await Composing();

        Type(drawn, "hi @ma");

        var editor = Editor(drawn).FrameToScreen();
        var (column, row) = (editor.X + 1, editor.Bottom - 1);

        if (asAPressAndRelease)
        {
            drawn.Point(column, row, MouseFlags.LeftButtonPressed);
            drawn.Point(column, row, MouseFlags.LeftButtonReleased);
        }

        drawn.Click(column, row);

        Assert.False(List(drawn).Visible);
        Assert.IsType<ComposeScreen>(drawn.Shell.Screen);

        Type(drawn, "x");

        Assert.Equal("hi @max", Editor(drawn).Text);
        Assert.False(List(drawn).Visible);
    }

    /// <summary>
    ///     A click on what is behind the view while the list is open — the From header, painted by the panel under it —
    ///     closes the list, and goes nowhere: the typing stays in the post.
    /// </summary>
    [Fact]
    public async Task AClickBehindTheViewOnlyClosesTheList()
    {
        using var drawn = await Composing();

        Type(drawn, "hi @ma");
        drawn.ClickOn("From");

        Assert.Equal(ComposeField.Post, Assert.IsType<ComposeScreen>(drawn.Shell.Screen).Typing);

        Assert.False(List(drawn).Visible);
        Assert.Equal("hi @ma", Assert.IsType<ComposeScreen>(drawn.Shell.Screen).Text);
    }

    /// <summary>
    ///     A notch of the wheel over the list moves the pick a person at a time, and stops at either end rather than
    ///     going round; the draft is untouched.
    /// </summary>
    [Fact]
    public async Task TheWheelOverTheListMovesThePick()
    {
        using var drawn = await Composing();

        Type(drawn, "@ma");

        var at = List(drawn).FrameToScreen();

        drawn.Wheel(at.X + 4, at.Y + 1);

        Assert.StartsWith("│▌ Mark", ListRows(drawn)[2], StringComparison.Ordinal);

        drawn.Wheel(at.X + 4, at.Y + 1);

        Assert.StartsWith("│▌ Mark", ListRows(drawn)[2], StringComparison.Ordinal);

        drawn.Wheel(at.X + 4, at.Y + 1, down: false);

        Assert.StartsWith("│▌ Maria", ListRows(drawn)[1], StringComparison.Ordinal);
        Assert.Equal("@ma", Editor(drawn).Text);
    }

    /// <summary>
    ///     Where the editor has no room for everybody the list holds, it shows as many as fit, and the wheel and the
    ///     arrows scroll the rest into view under the pick.
    /// </summary>
    [Fact]
    public async Task AListTallerThanItsRoomScrollsToThePick()
    {
        var five = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "1", account: "maa@a.social", author: "Maa"),
                APost.With(id: "2", account: "mab@a.social", author: "Mab"),
                APost.With(id: "3", account: "mac@a.social", author: "Mac"),
                APost.With(id: "4", account: "mad@a.social", author: "Mad"),
                APost.With(id: "5", account: "mae@a.social", author: "Mae")),
            Accounts = FakeAccountRelationships.HoldingNobody(),
        };

        using var drawn = await ComposedView.Of(five, height: Short);

        Type(drawn, "@ma");

        var rows = ListRows(drawn);
        var shown = rows.Count - 2;

        Assert.True(shown is > 0 and < 5, $"{shown} of 5 shown:\n{string.Join('\n', drawn.Rows())}");
        Assert.StartsWith("│▌ Maa", rows[1], StringComparison.Ordinal);

        var at = List(drawn).FrameToScreen();

        for (var notch = 0; notch < 4; notch++)
        {
            drawn.Wheel(at.X + 4, at.Y + 1);
        }

        rows = ListRows(drawn);

        Assert.StartsWith("│▌ Mae", rows[shown], StringComparison.Ordinal);
        Assert.DoesNotContain(rows, row => row.Contains("Maa", StringComparison.Ordinal));

        drawn.Press(Key.CursorDown);

        Assert.StartsWith("│▌ Maa", ListRows(drawn)[1], StringComparison.Ordinal);

        drawn.Press(Key.Tab);

        Assert.Equal("@maa@a.social ", Editor(drawn).Text);
    }

    /// <summary>A right click on the list is nothing: no-one is inserted, the list stays open, and the draft stays (#307).</summary>
    [Fact]
    public async Task ARightClickOnTheListDoesNothing()
    {
        using var drawn = await Composing();

        Type(drawn, "hi @ma");

        var at = List(drawn).FrameToScreen();

        drawn.RightClick(at.X + 4, at.Y + 2);

        Assert.True(List(drawn).Visible);
        Assert.Equal("hi @ma", Assert.IsType<ComposeScreen>(drawn.Shell.Screen).Text);
    }

    /// <summary>
    ///     While the list is open, <c>↑</c> is the list's, even on the post's first line: it walks no field (#337).
    /// </summary>
    [Fact]
    public async Task WhileTheListIsOpenUpIsTheListsEvenOnTheFirstLine()
    {
        using var drawn = await Composing();
        var compose = Assert.IsType<ComposeScreen>(drawn.Shell.Screen);

        Type(drawn, "@ma");
        drawn.Press(Key.CursorUp);

        Assert.False(compose.WritingTheWarning);
        Assert.True(Editor(drawn).HasFocus);
        Assert.Equal("@ma", compose.Text);
    }

    private static Task<ComposedView> Composing(ITheme? theme = null, FakeAccountRelationships? accounts = null) =>
        ComposedView.Of(
            new AShell { Timelines = Seen.Timelines, Accounts = accounts ?? FakeAccountRelationships.HoldingNobody() },
            theme: theme);

    private static void Type(ComposedView drawn, string text) => drawn.Type(text);

    private static ComposeEditor Editor(ComposedView drawn) => drawn.Editor;

    private static PaintedView List(ComposedView drawn) => drawn.Mentions;

    /// <summary>The list's rows as drawn on the terminal.</summary>
    private static IReadOnlyList<string> ListRows(ComposedView drawn) => drawn.RowsOf(drawn.Mentions);
}
