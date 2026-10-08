using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Key = Terminal.Gui.Input.Key;

namespace Wooly.Tests.Tui;

/// <summary>
///     The arrow keys walk compose's fields the way a mail client's do (ADR-0024, #337): <c>↑</c> off the post's first
///     line goes up into the headers, <c>↑</c>/<c>↓</c> move between them, and <c>↓</c> off the last comes back to the
///     post — while <c>tab</c> keeps the meaning it has on every screen. Walked in <see cref="Wooly.Tui.Views.ComposeView" />
///     over a shell (#365).
/// </summary>
public class ComposeFieldWalkTests
{
    /// <summary>
    ///     <c>↑</c> on the post's first line moves the typing up onto the Media header, where nothing is attached to
    ///     walk through (#378), and the next into the warning — and what is typed lands there.
    /// </summary>
    [Fact]
    public async Task UpOnTheFirstLineMovesTheTypingIntoTheWarning()
    {
        using var view = await Composing();
        var compose = view.Compose;

        view.Type("hi");
        view.Press(Key.CursorUp);

        Assert.Equal(ComposeField.Media, compose.Typing);

        view.Press(Key.CursorUp);

        Assert.True(compose.WritingTheWarning);
        Assert.True(view.Warning.HasFocus);

        view.Type("cw");

        Assert.Equal("cw", compose.Warning);
        Assert.Equal("hi", compose.Text);
    }

    /// <summary><c>↑</c> on any later line moves the caret up a line, as it always has.</summary>
    [Fact]
    public async Task UpOnALaterLineMovesTheCaret()
    {
        using var view = await Composing();
        var compose = view.Compose;

        view.Type("one");
        view.Press(Key.Enter);
        view.Type("two");
        view.Press(Key.CursorUp);

        Assert.False(compose.WritingTheWarning);
        Assert.True(view.Editor.HasFocus);

        view.Type("!");

        Assert.Equal(["one!", "two"], Lines(compose.Text));
    }

    /// <summary>
    ///     A row a long line has wrapped onto is not the first line either: <c>↑</c> there moves the caret up onto the
    ///     row above.
    /// </summary>
    [Fact]
    public async Task UpOnAWrappedRowMovesTheCaret()
    {
        using var view = await Composing();
        var compose = view.Compose;
        var words = string.Join(' ', Enumerable.Repeat("word", view.Editor.Frame.Width / 4));

        view.Type(words);
        view.Press(Key.CursorUp);

        Assert.False(compose.WritingTheWarning);
        Assert.True(view.Editor.HasFocus);
    }

    /// <summary><c>↓</c> in the warning, and on past the Media header, hands the typing back to the post.</summary>
    [Fact]
    public async Task DownInTheWarningReturnsTheTypingToThePost()
    {
        using var view = await Composing();
        var compose = view.Compose;

        view.Press(Key.CursorUp);
        view.Press(Key.CursorUp);
        view.Type("cw");
        view.Press(Key.CursorDown);
        view.Press(Key.CursorDown);

        Assert.False(compose.WritingTheWarning);
        Assert.True(view.Editor.HasFocus);

        view.Type("hi");

        Assert.Equal("cw", compose.Warning);
        Assert.Equal("hi", compose.Text);
    }

    /// <summary>
    ///     The walk goes through the headers in the order they are drawn — To, Lang, the warning, Media, the post (#338,
    ///     #340, #378) —
    ///     and <c>↑</c> on To, the top header, has nowhere further up to go and leaves the typing where it is.
    /// </summary>
    [Fact]
    public async Task TheWalkGoesThroughToAndTheWarningAsDrawnAndStopsAtTheTop()
    {
        using var view = await Composing();
        var compose = view.Compose;

        view.Press(Key.CursorUp);
        view.Press(Key.CursorUp);
        view.Press(Key.CursorUp);

        Assert.Equal(ComposeField.Lang, compose.Typing);
        Assert.True(view.Lang.HasFocus);

        view.Press(Key.CursorUp);

        Assert.Equal(ComposeField.To, compose.Typing);
        Assert.True(view.To.HasFocus);

        view.Press(Key.CursorUp);

        Assert.Equal(ComposeField.To, compose.Typing);
        Assert.True(view.To.HasFocus);

        view.Press(Key.CursorDown);

        Assert.Equal(ComposeField.Lang, compose.Typing);

        view.Press(Key.CursorDown);

        Assert.True(compose.WritingTheWarning);
        Assert.True(view.Warning.HasFocus);

        view.Press(Key.CursorDown);

        Assert.Equal(ComposeField.Media, compose.Typing);

        view.Press(Key.CursorDown);

        Assert.Equal(ComposeField.Post, compose.Typing);
        Assert.True(view.Editor.HasFocus);
    }

    /// <summary>
    ///     In a viewport too short for Lang's row, the walk steps over Lang rather than into a field nobody can see: the
    ///     warning and To are next to each other, as they are drawn.
    /// </summary>
    [Fact]
    public async Task InAShortViewportTheWalkStepsOverLang()
    {
        using var view = await Composing(height: 6);
        var compose = view.Compose;

        Assert.DoesNotContain(view.Rows(), row => row.Contains("Lang", StringComparison.Ordinal));
        Assert.Contains(view.Rows(), row => row.Contains("To  ", StringComparison.Ordinal));

        view.Press(Key.CursorUp);
        view.Press(Key.CursorUp);

        Assert.Equal(ComposeField.To, compose.Typing);
        Assert.True(view.To.HasFocus);

        view.Press(Key.CursorDown);

        Assert.True(compose.WritingTheWarning);
        Assert.True(view.Warning.HasFocus);
    }

    /// <summary>
    ///     <c>ctrl-w</c> still jumps between the warning and the post, wherever the arrows left the typing, and the
    ///     status row still says which way it goes next.
    /// </summary>
    [Fact]
    public async Task CtrlWStillJumpsBetweenTheWarningAndThePost()
    {
        using var view = await Composing();
        var compose = view.Compose;

        view.Press(Key.CursorUp);
        view.Press(Key.CursorUp);

        Assert.Contains("Back to the post: ctrl-w", view.Status(), StringComparison.Ordinal);

        view.Press(Key.W.WithCtrl);

        Assert.False(compose.WritingTheWarning);
        Assert.True(view.Editor.HasFocus);
        Assert.Contains("Content warning: ctrl-w", view.Status(), StringComparison.Ordinal);

        view.Press(Key.W.WithCtrl);

        Assert.True(compose.WritingTheWarning);
        Assert.True(view.Warning.HasFocus);
    }

    /// <summary>
    ///     The status row offers the walk only the ways it goes: <c>↑</c> in the post, both on a header between others,
    ///     and only <c>↓</c> on To, the top header.
    /// </summary>
    [Fact]
    public async Task TheStatusRowOffersTheWalkOnlyTheWaysItGoes()
    {
        using var view = await Composing();

        Assert.Contains("Field: ↑ ", view.Status(), StringComparison.Ordinal);

        view.Press(Key.CursorUp);

        Assert.Contains("Field: ↑↓", view.Status(), StringComparison.Ordinal);

        view.Press(Key.CursorUp);
        view.Press(Key.CursorUp);
        view.Press(Key.CursorUp);

        Assert.Contains("Field: ↓ ", view.Status(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     <c>↑</c> on To, the top header, stays there: it does not come round to the post, though Terminal.Gui would
    ///     carry the focus there if nothing answered the key.
    /// </summary>
    [Fact]
    public async Task UpOnToDoesNotComeRoundToThePost()
    {
        using var view = await Composing();
        var compose = view.Compose;

        view.Press(Key.CursorUp);
        view.Press(Key.CursorUp);
        view.Press(Key.CursorUp);
        view.Press(Key.CursorUp);
        view.PressThroughTheApplication(Key.CursorUp);

        Assert.Equal(ComposeField.To, compose.Typing);
        Assert.True(view.To.HasFocus);
    }

    /// <summary><c>↓</c> on the post's last line stays in the post: it does not come round to To.</summary>
    [Fact]
    public async Task DownOnThePostsLastLineDoesNotComeRoundToTo()
    {
        using var view = await Composing();
        var compose = view.Compose;

        view.Type("hi");
        view.PressThroughTheApplication(Key.CursorDown);

        Assert.Equal(ComposeField.Post, compose.Typing);
        Assert.True(view.Editor.HasFocus);
    }

    /// <summary><c>←</c> and <c>→</c> off either end of To stay on To, rather than carrying the focus elsewhere.</summary>
    [Fact]
    public async Task ChoosingOffEitherEndOfToStaysOnTo()
    {
        using var view = await Composing();
        var compose = view.Compose;

        view.Press(Key.CursorUp);
        view.Press(Key.CursorUp);
        view.Press(Key.CursorUp);
        view.Press(Key.CursorUp);

        foreach (var _ in Enumerable.Range(0, 6))
        {
            view.PressThroughTheApplication(Key.CursorRight);
        }

        Assert.Equal(ComposeField.To, compose.Typing);
        Assert.True(view.To.HasFocus);

        foreach (var _ in Enumerable.Range(0, 6))
        {
            view.PressThroughTheApplication(Key.CursorLeft);
        }

        Assert.Equal(ComposeField.To, compose.Typing);
        Assert.True(view.To.HasFocus);
    }

    /// <summary>
    ///     <c>tab</c> and <c>shift-tab</c> are the frame's on compose too, from the post or the warning: no field takes
    ///     them, so they reach the window — which moves the rail with them — and the draft is untouched.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TabIsLeftToTheWindow(bool fromTheWarning)
    {
        using var view = await Composing();
        var compose = view.Compose;

        if (fromTheWarning)
        {
            view.Press(Key.CursorUp);
            view.Press(Key.CursorUp);
        }

        view.Window.Reached.Clear();
        view.Press(Key.Tab);
        view.Press(Key.Tab.WithShift);

        Assert.Equal([Key.Tab, Key.Tab.WithShift], view.Window.Reached);
        Assert.Equal(fromTheWarning, compose.WritingTheWarning);
        Assert.Equal(string.Empty, compose.Text);
        Assert.Equal(string.Empty, compose.Warning);
    }

    private static async Task<ComposedView> Composing(int height = ComposedView.Height) =>
        await ComposedView.Of(
            new AShell { Accounts = FakeAccountRelationships.HoldingNobody() },
            height: height);

    private static string[] Lines(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
}
