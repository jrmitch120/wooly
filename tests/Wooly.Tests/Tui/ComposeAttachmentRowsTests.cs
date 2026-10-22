using Wooly.Core.Errors;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;
using Terminal.Gui.Input;

namespace Wooly.Tests.Tui;

/// <summary>
///     The keys and the pointer on what is attached to a compose, in the window (#378): walking its rows, taking one off,
///     retrying, picking and dragging one. What each does to the draft is drafting's
///     (<see cref="DraftingAttachmentRowsTests" />).
/// </summary>
public class ComposeAttachmentRowsTests : IDisposable
{
    /// <summary>
    ///     In the window, <c>↑</c> off the editor's first line walks onto the bottom row, and <c>del</c> there takes it
    ///     off — the keys reaching the shell's keymap from the rows as from anywhere.
    /// </summary>
    [Fact]
    public async Task InTheWindowTheKeysWalkTheRowsAndTakeOneOff()
    {
        using var drawn = await Drawn("one.png", "two.png");

        drawn.Press(Key.CursorUp);

        Assert.Contains(drawn.Rows(), row => row.Contains("▌⠶     two.png", StringComparison.Ordinal));

        drawn.Press(Key.Delete);

        Assert.Equal(["one.png"], Names(Compose(drawn)));

        drawn.Press(Key.Z.WithCtrl);

        Assert.Equal(["one.png", "two.png"], Names(Compose(drawn)));

        drawn.Press(Key.CursorUp.WithShift);

        Assert.Equal(["two.png", "one.png"], Names(Compose(drawn)));
    }

    /// <summary>
    ///     In the window, a letter that acts on a post elsewhere does nothing on a row — no compose behind the draft, no
    ///     row taken off — while the row's own letters, <c>s</c> and <c>r</c>, still reach the keymap (review of #372).
    /// </summary>
    [Fact]
    public async Task InTheWindowARowTakesOnlyItsOwnLetters()
    {
        using var drawn = await Drawn("one.png");
        var compose = Compose(drawn);

        drawn.Press(Key.CursorUp);
        drawn.Press(Key.C);
        drawn.Press(Key.X);
        drawn.Press(Key.B);

        Assert.Same(compose, drawn.Shell.Screen);
        Assert.Equal(["one.png"], Names(compose));
        Assert.Equal(string.Empty, compose.Text);

        drawn.Press(Key.S);

        Assert.True(compose.Sensitive);
    }

    /// <summary>A click on a row picks it, with the bar against its grip; the post is as it was.</summary>
    [Fact]
    public async Task AClickOnARowPicksIt()
    {
        using var drawn = await Drawn("one.png", "two.png");
        var (column, row) = At(drawn, "one.png", "one.png");

        Click(drawn, column, row);

        Assert.Equal(ComposeField.Attachment, Compose(drawn).Typing);
        Assert.Contains(drawn.Rows(), text => text.Contains("▌⠶     one.png", StringComparison.Ordinal));
        Assert.Equal(["one.png", "two.png"], Names(Compose(drawn)));
    }

    /// <summary>A click on a row's <c>x</c> takes it off the post.</summary>
    [Fact]
    public async Task AClickOnItsXTakesItOff()
    {
        using var drawn = await Drawn("one.png", "two.png");
        var (column, row) = At(drawn, "one.png", " x ");

        Click(drawn, column + 1, row);

        Assert.Equal(["two.png"], Names(Compose(drawn)));
    }

    /// <summary>A click on <c>retry (r)</c> sends a file whose connection dropped up again.</summary>
    [Fact]
    public async Task AClickOnRetrySendsItUpAgain()
    {
        var built = new AShell();
        using var drawn = await Drawn(built, "cat.png");

        built.Author.Attaching.Single().Refuse(Dropped());
        built.Host.Drain();
        drawn.Redraw();

        var (column, row) = At(drawn, "cat.png", "(r)");

        Click(drawn, column, row);

        Assert.Equal(2, built.Author.Attaching.Count);
    }

    /// <summary>
    ///     Dragging a row by the pointer moves it live: it takes each place the pointer reaches, the others making way,
    ///     and stays where it is let go — the post going out in that order. A drag is not also a click.
    /// </summary>
    [Fact]
    public async Task DraggingARowReordersItLive()
    {
        using var drawn = await Drawn("one.png", "two.png", "three.png");
        var (column, row) = At(drawn, "three.png", "three.png");

        drawn.Point(column, row, MouseFlags.LeftButtonPressed);
        drawn.Point(column, row - 1, MouseFlags.LeftButtonPressed | MouseFlags.PositionReport);

        Assert.Equal(["one.png", "three.png", "two.png"], Names(Compose(drawn)));

        drawn.Point(column, row - 2, MouseFlags.LeftButtonPressed | MouseFlags.PositionReport);
        drawn.Point(column, row - 2, MouseFlags.LeftButtonReleased);
        drawn.Point(column, row - 2, MouseFlags.LeftButtonClicked);

        Assert.Equal(["three.png", "one.png", "two.png"], Names(Compose(drawn)));
        Assert.Contains(drawn.Rows(), text => text.Contains("▌⠶     three.png", StringComparison.Ordinal));
    }

    /// <summary>
    ///     A click is not a drag, and nor is the pointer moving with no button pressed on the rows since: a row is only
    ///     ever carried by a press made on it. Some terminals report a pointer moving with no button held in the very
    ///     words they report one moving with the left button held, so a report of the button held is believed only
    ///     after a press.
    /// </summary>
    [Fact]
    public async Task OnlyAPressOnARowPicksItUp()
    {
        using var drawn = await Drawn("one.png", "two.png", "three.png");
        var (column, row) = At(drawn, "three.png", "three.png");

        Click(drawn, column, row);
        drawn.Point(column, row - 1, MouseFlags.LeftButtonPressed | MouseFlags.PositionReport);
        drawn.Point(column, row - 2, MouseFlags.LeftButtonPressed | MouseFlags.PositionReport);
        drawn.Point(column, row - 2, MouseFlags.PositionReport);

        Assert.Equal(["one.png", "two.png", "three.png"], Names(Compose(drawn)));

        // A click whose let-go the terminal never reported: the click still puts the row down.
        (column, row) = At(drawn, "two.png", "two.png");
        drawn.Point(column, row, MouseFlags.LeftButtonPressed);
        drawn.Point(column, row, MouseFlags.LeftButtonClicked);
        drawn.Point(column, row + 1, MouseFlags.LeftButtonPressed | MouseFlags.PositionReport);

        Assert.Equal(["one.png", "two.png", "three.png"], Names(Compose(drawn)));
    }

    private readonly TemporaryDirectory _files = new();

    public void Dispose() => _files.Dispose();

    /// <summary>A whole window at 80 × 24 on a fresh compose with <paramref name="names" /> dropped onto it.</summary>
    private Task<DrawnShell> Drawn(params string[] names) => Drawn(new AShell(), names);

    private async Task<DrawnShell> Drawn(AShell built, params string[] names)
    {
        var drawn = await DrawnShell.Of(80, 24, Themes.Plain, built);

        drawn.Shell.Press(ShellKey.C);
        drawn.Redraw();

        foreach (var name in names)
        {
            Assert.True(drawn.Shell.Paste(_files.WriteFile(name)));
        }

        drawn.Redraw();

        return drawn;
    }

    private static ComposeScreen Compose(DrawnShell drawn) => Assert.IsType<ComposeScreen>(drawn.Shell.Screen);

    /// <summary>Where <paramref name="text" /> starts on the drawn row naming <paramref name="name" />.</summary>
    private static (int Column, int Row) At(DrawnShell drawn, string name, string text)
    {
        var rows = drawn.Rows();
        var row = Array.FindIndex(rows, line => line.Contains(name, StringComparison.Ordinal));

        Assert.True(row >= 0, string.Join('\n', rows));

        return (rows[row].IndexOf(text, StringComparison.Ordinal), row);
    }

    /// <summary>A left click, pressed and let go first as a terminal reports one.</summary>
    private static void Click(DrawnShell drawn, int column, int row)
    {
        drawn.Point(column, row, MouseFlags.LeftButtonPressed);
        drawn.Point(column, row, MouseFlags.LeftButtonReleased);
        drawn.Point(column, row, MouseFlags.LeftButtonClicked);
    }

    /// <summary>A connection that dropped while a file was going up, the one failure a retry can mend.</summary>
    private static TransientNetworkException Dropped() =>
        new(new Uri("https://mastodon.social/api/v2/media"), 3, new HttpRequestException());

    /// <summary>The names on the rows, top to bottom.</summary>
    private static IEnumerable<string> Names(ComposeScreen compose) =>
        compose.Media.Attachments.Select(attachment => attachment.Name);
}
