using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;
using Key = Terminal.Gui.Input.Key;

namespace Wooly.Tests.Tui;

/// <summary>
///     The arrow keys walk compose's fields the way a mail client's do (ADR-0024, #337): <c>↑</c> off the post's first
///     line goes up into the headers, <c>↑</c>/<c>↓</c> move between them, and <c>↓</c> off the last comes back to the
///     post — while <c>tab</c> keeps the meaning it has on every screen.
/// </summary>
public class ComposeFieldWalkTests
{
    private static readonly AShell Seen = new()
    {
        Timelines = FakeTimelineReader.Holding(
            APost.With(id: "1", account: "maria@fosstodon.org", author: "Maria Gonzalez"),
            APost.With(id: "2", account: "mark@mastodon.social", author: "Mark")),
    };

    /// <summary><c>↑</c> on the post's first line moves the typing up into the warning, and what is typed lands there.</summary>
    [Fact]
    public async Task UpOnTheFirstLineMovesTheTypingIntoTheWarning()
    {
        using var drawn = await Composing();
        var compose = Compose(drawn);

        Type(drawn, "hi");
        drawn.Press(Key.CursorUp);

        Assert.True(compose.WritingTheWarning);
        Assert.True(Field(drawn).HasFocus);

        Type(drawn, "cw");

        Assert.Equal("cw", compose.Warning);
        Assert.Equal("hi", compose.Text);
    }

    /// <summary><c>↑</c> on any later line moves the caret up a line, as it always has.</summary>
    [Fact]
    public async Task UpOnALaterLineMovesTheCaret()
    {
        using var drawn = await Composing();
        var compose = Compose(drawn);

        Type(drawn, "one");
        drawn.Press(Key.Enter);
        Type(drawn, "two");
        drawn.Press(Key.CursorUp);

        Assert.False(compose.WritingTheWarning);
        Assert.True(Editor(drawn).HasFocus);

        Type(drawn, "!");

        Assert.Equal(["one!", "two"], Lines(compose.Text));
    }

    /// <summary>
    ///     A row a long line has wrapped onto is not the first line either: <c>↑</c> there moves the caret up onto the
    ///     row above.
    /// </summary>
    [Fact]
    public async Task UpOnAWrappedRowMovesTheCaret()
    {
        using var drawn = await Composing();
        var compose = Compose(drawn);
        var words = string.Join(' ', Enumerable.Repeat("word", Editor(drawn).Frame.Width / 4));

        Type(drawn, words);
        drawn.Press(Key.CursorUp);

        Assert.False(compose.WritingTheWarning);
        Assert.True(Editor(drawn).HasFocus);
    }

    /// <summary><c>↓</c> in the warning, the last header, hands the typing back to the post.</summary>
    [Fact]
    public async Task DownInTheWarningReturnsTheTypingToThePost()
    {
        using var drawn = await Composing();
        var compose = Compose(drawn);

        drawn.Press(Key.CursorUp);
        Type(drawn, "cw");
        drawn.Press(Key.CursorDown);

        Assert.False(compose.WritingTheWarning);
        Assert.True(Editor(drawn).HasFocus);

        Type(drawn, "hi");

        Assert.Equal("cw", compose.Warning);
        Assert.Equal("hi", compose.Text);
    }

    /// <summary>
    ///     The walk goes through the headers in the order they are drawn — To, then the warning, then the post (#338) —
    ///     and <c>↑</c> on To, the top header, has nowhere further up to go and leaves the typing where it is.
    /// </summary>
    [Fact]
    public async Task TheWalkGoesThroughToAndTheWarningAsDrawnAndStopsAtTheTop()
    {
        using var drawn = await Composing();
        var compose = Compose(drawn);

        drawn.Press(Key.CursorUp);
        drawn.Press(Key.CursorUp);

        Assert.Equal(ComposeField.To, compose.Typing);
        Assert.True(To(drawn).HasFocus);

        drawn.Press(Key.CursorUp);

        Assert.Equal(ComposeField.To, compose.Typing);
        Assert.True(To(drawn).HasFocus);

        drawn.Press(Key.CursorDown);

        Assert.True(compose.WritingTheWarning);
        Assert.True(Field(drawn).HasFocus);

        drawn.Press(Key.CursorDown);

        Assert.Equal(ComposeField.Post, compose.Typing);
        Assert.True(Editor(drawn).HasFocus);
    }

    /// <summary>
    ///     <c>ctrl-w</c> still jumps between the warning and the post, wherever the arrows left the typing, and the
    ///     status row still says which way it goes next.
    /// </summary>
    [Fact]
    public async Task CtrlWStillJumpsBetweenTheWarningAndThePost()
    {
        using var drawn = await Composing();
        var compose = Compose(drawn);

        drawn.Press(Key.CursorUp);

        Assert.Contains("Back to the post: ctrl-w", drawn.Rows()[^1], StringComparison.Ordinal);

        drawn.Press(Key.W.WithCtrl);

        Assert.False(compose.WritingTheWarning);
        Assert.True(Editor(drawn).HasFocus);
        Assert.Contains("Content warning: ctrl-w", drawn.Rows()[^1], StringComparison.Ordinal);

        drawn.Press(Key.W.WithCtrl);

        Assert.True(compose.WritingTheWarning);
        Assert.True(Field(drawn).HasFocus);
    }

    /// <summary>On a header the status row offers the walk; in the post, where <c>↑</c> is mostly the caret's, it does not.</summary>
    [Fact]
    public async Task OnAHeaderTheStatusRowOffersTheWalk()
    {
        using var drawn = await Composing();

        Assert.DoesNotContain("Field: ↑↓", drawn.Rows()[^1], StringComparison.Ordinal);

        drawn.Press(Key.CursorUp);

        Assert.Contains("Field: ↑↓", drawn.Rows()[^1], StringComparison.Ordinal);
    }

    /// <summary><c>tab</c> and <c>shift-tab</c> move the rail's cursor from compose, from either field, as everywhere.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TabMovesTheRailFromCompose(bool fromTheWarning)
    {
        using var drawn = await Composing();
        var compose = Compose(drawn);

        if (fromTheWarning)
        {
            drawn.Press(Key.CursorUp);
        }

        var was = drawn.Shell.Rail.Cursor;

        drawn.Press(Key.Tab);

        Assert.Equal(was + 1, drawn.Shell.Rail.Cursor);
        Assert.Equal(fromTheWarning, compose.WritingTheWarning);
        Assert.Equal(string.Empty, compose.Text);
        Assert.Equal(string.Empty, compose.Warning);

        drawn.Press(Key.Tab.WithShift);

        Assert.Equal(was, drawn.Shell.Rail.Cursor);
    }

    /// <summary>While the list of people to mention is open, <c>↑</c> is the list's, even on the post's first line.</summary>
    [Fact]
    public async Task WhileTheMentionListIsOpenUpIsTheLists()
    {
        using var drawn = await Composing();
        var compose = Compose(drawn);

        Type(drawn, "@ma");
        drawn.Press(Key.CursorUp);

        Assert.False(compose.WritingTheWarning);
        Assert.True(Editor(drawn).HasFocus);
        Assert.Equal("@ma", compose.Text);
    }

    private static async Task<DrawnShell> Composing()
    {
        var built = new AShell { Timelines = Seen.Timelines, Accounts = FakeAccountRelationships.HoldingNobody() };
        var drawn = await DrawnShell.Of(80, 24, Themes.Dark, built);

        drawn.Shell.Compose();
        drawn.Redraw();

        return drawn;
    }

    private static ComposeScreen Compose(DrawnShell drawn) => Assert.IsType<ComposeScreen>(drawn.Shell.Screen);

    private static string[] Lines(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

    private static void Type(DrawnShell drawn, string text)
    {
        foreach (var letter in text)
        {
            drawn.Window.NewKeyDownEvent(new Key(letter));
        }

        drawn.Redraw();
    }

    private static ComposeEditor Editor(DrawnShell drawn) =>
        drawn.Window.SubViews.OfType<ComposeEditor>().Single();

    private static ComposeToField To(DrawnShell drawn) => drawn.Window.SubViews.OfType<ComposeToField>().Single();

    private static ComposeWarningField Field(DrawnShell drawn) =>
        drawn.Window.SubViews.OfType<ComposeWarningField>().Single();
}
