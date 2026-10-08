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
///     Fixing and rearranging what is attached to a compose (#378): walking its rows from the editor, a refusal that
///     says why and what a retry can mend, taking one off and bringing it back, putting them in another order, and a
///     drop past the instance's limit.
/// </summary>
public class ComposeAttachmentRowsTests : IDisposable
{
    /// <summary>The content panel's inside at an 80 × 24 terminal.</summary>
    private const int Width = 78;

    private const int Height = 21;

    /// <summary>
    ///     <c>↑</c> from the editor walks the rows from the bottom, then the Media header, then Warn; <c>↓</c> walks
    ///     back. The row walked to carries the selection bar against its grip, and the header its words highlighted.
    /// </summary>
    [Fact]
    public async Task UpFromTheEditorWalksTheRowsThenTheHeaderThenWarn()
    {
        var (shell, _, compose) = await Composing("one.png", "two.png");

        shell.Press(ShellKey.Up);

        Assert.StartsWith("    ▌⠶     two.png", Row(compose, "two.png"), StringComparison.Ordinal);
        Assert.StartsWith("     ⠶     one.png", Row(compose, "one.png"), StringComparison.Ordinal);

        shell.Press(ShellKey.Up);

        Assert.StartsWith("    ▌⠶     one.png", Row(compose, "one.png"), StringComparison.Ordinal);

        shell.Press(ShellKey.Up);

        Assert.DoesNotContain(Texts(compose), row => row.Contains('▌', StringComparison.Ordinal));
        Assert.Equal(
            Role.SelectedText,
            Media(compose).Spans.Single(span => span.Text.StartsWith("2 of 4", StringComparison.Ordinal)).Role);

        shell.Press(ShellKey.Up);

        Assert.Equal(ComposeField.Warning, compose.Typing);

        shell.Press(ShellKey.Down);
        shell.Press(ShellKey.Down);
        shell.Press(ShellKey.Down);
        shell.Press(ShellKey.Down);

        Assert.Equal(ComposeField.Post, compose.Typing);
        Assert.DoesNotContain(Texts(compose), row => row.Contains('▌', StringComparison.Ordinal));
    }

    /// <summary>
    ///     A dropped connection offers <c>retry (r)</c> after its reason, and <c>r</c> on its row sends the file up
    ///     again — which, once it is through, lets the post go.
    /// </summary>
    [Fact]
    public async Task RetryingADroppedConnectionThatGoesThroughUnblocksTheSend()
    {
        var (shell, built, compose) = await Composing("cat.png");

        built.Author.Attaching.Single().Refuse(Dropped());
        built.Host.Drain();

        Assert.EndsWith("x  connection lost  retry (r)", Row(compose, "cat.png", width: 120), StringComparison.Ordinal);

        await shell.Send();

        Assert.Empty(built.Author.Published);

        shell.Press(ShellKey.Up);
        shell.Press(ShellKey.R);

        Assert.Equal(2, built.Author.Attaching.Count);
        Assert.Equal(built.Author.Attaching[0].Path, built.Author.Attaching[1].Path);
        Assert.EndsWith("x  ░░░░░░░░   0%", Row(compose, "cat.png"), StringComparison.Ordinal);

        built.Author.Attaching[1].Ready();
        built.Host.Drain();
        await shell.Send();

        Assert.Equal("m2", Assert.Single(Assert.Single(built.Author.Published).Draft.Attached).Id);
    }

    /// <summary>
    ///     A file the instance will not take is never offered a retry, which could not mend it: <c>r</c> on its row sends
    ///     nothing up, and the send stays blocked until it is taken off.
    /// </summary>
    [Fact]
    public async Task ARefusalARetryCannotMendOffersNone()
    {
        var (shell, built, compose) = await Composing("huge.png");

        built.Author.Attaching.Single().Refuse(new AttachmentRefusedException("File is too large"));
        built.Host.Drain();

        Assert.EndsWith("x  File is too large", Row(compose, "huge.png", width: 120), StringComparison.Ordinal);

        shell.Press(ShellKey.Up);
        shell.Press(ShellKey.R);

        Assert.Single(built.Author.Attaching);

        await shell.Send();

        Assert.Empty(built.Author.Published);
        Assert.True(shell.NoticeIsError);
    }

    /// <summary>
    ///     Every failure trying again could mend offers <c>retry (r)</c> after its reason, as a dropped connection does:
    ///     a rate limit, which passes; an instance that failed to answer; and a call the client gave up waiting on
    ///     (review of #372).
    /// </summary>
    [Theory]
    [MemberData(nameof(Passing))]
    public async Task EveryFailureARetryCanMendOffersOne(Exception failure, string said)
    {
        var (shell, built, compose) = await Composing("cat.png");

        built.Author.Attaching.Single().Refuse(failure);
        built.Host.Drain();

        Assert.EndsWith($"x  {said}  retry (r)", Row(compose, "cat.png", width: 120), StringComparison.Ordinal);

        shell.Press(ShellKey.Up);
        shell.Press(ShellKey.R);

        Assert.Equal(2, built.Author.Attaching.Count);
    }

    /// <summary>The failures a retry can mend, and what the row says of each.</summary>
    public static TheoryData<Exception, string> Passing => new()
    {
        { new RateLimitedException("mastodon.social", resetsAt: null), "rate limited" },
        { new InstanceFailedException("mastodon.social", System.Net.HttpStatusCode.BadGateway), "instance failed (502)" },
        { new TaskCanceledException("The request timed out."), "timed out" },
    };

    /// <summary>
    ///     A failure nothing here expected — a bare HTTP error, a file that could not be read — still ends the row as
    ///     refused, saying what it was, rather than leaving it going up forever; and a send waiting on it is not left
    ///     waiting, but stopped and said, as a refusal stops it (review of #372).
    /// </summary>
    [Fact]
    public async Task AFailureNothingExpectedEndsTheRowAndTheWaitingSend()
    {
        var (shell, built, compose) = await Composing("cat.png");

        await shell.Send();

        Assert.StartsWith("Will send once", shell.Notice, StringComparison.Ordinal);

        built.Author.Attaching.Single().Refuse(new IOException("The file could not be read"));
        built.Host.Drain();

        Assert.EndsWith("x  The file could not be read", Row(compose, "cat.png", width: 120), StringComparison.Ordinal);
        Assert.Empty(built.Author.Published);
        Assert.True(shell.NoticeIsError);

        shell.Press(ShellKey.Up);
        shell.Press(ShellKey.R);

        Assert.Single(built.Author.Attaching);
    }

    /// <summary>
    ///     At 80 columns there is not room for the reason and <c>retry (r)</c> both: the offer shortens to <c>(r)</c>,
    ///     and a reason too long for what is left is cut.
    /// </summary>
    [Fact]
    public async Task OnANarrowTerminalTheRetryShortensAndTheReasonIsCut()
    {
        var (_, built, compose) = await Composing("cat.png");

        built.Author.Attaching.Single().Refuse(Dropped());
        built.Host.Drain();

        Assert.EndsWith("x  connection l…  (r)", Row(compose, "cat.png"), StringComparison.Ordinal);
    }

    /// <summary>
    ///     <c>del</c> or <c>backspace</c> on a row takes it off the post that is sent, and the walk stays on the row that
    ///     took its place.
    /// </summary>
    [Theory]
    [InlineData(ShellKey.Delete)]
    [InlineData(ShellKey.Backspace)]
    public async Task RemovingTakesItOffThePostThatIsSent(ShellKey key)
    {
        var (shell, built, compose) = await Composing("one.png", "two.png", "three.png");

        AllReady(built);
        shell.Press(ShellKey.Up);
        shell.Press(ShellKey.Up);
        shell.Press(key);

        Assert.Equal(["one.png", "three.png"], Names(compose));
        Assert.StartsWith("    ▌⠶     three.png", Row(compose, "three.png"), StringComparison.Ordinal);
        Assert.Contains("  Media  2 of 4 · ctrl-o to add · □ sensitive", Texts(compose));

        await shell.Send();

        Assert.Equal(["m1", "m3"], Assert.Single(built.Author.Published).Draft.Attached.Select(attached => attached.Id));
    }

    /// <summary>
    ///     Taking off the last thing attached leaves the walk on the Media header, where <c>ctrl-z</c> still brings it
    ///     back.
    /// </summary>
    [Fact]
    public async Task RemovingTheLastOneLeavesTheWalkOnTheHeader()
    {
        var (shell, _, compose) = await Composing("one.png");

        shell.Press(ShellKey.Up);
        shell.Press(ShellKey.Delete);

        Assert.Empty(compose.Attachments);
        Assert.Equal(ComposeField.Media, compose.Typing);

        shell.Press(ShellKey.CtrlZ);

        Assert.Equal(["one.png"], Names(compose));
    }

    /// <summary><c>s</c> on a row toggles sensitive, as it does on the header over it (#379).</summary>
    [Fact]
    public async Task SOnARowTogglesSensitive()
    {
        var (shell, _, compose) = await Composing("one.png");

        shell.Press(ShellKey.Up);
        shell.Press(ShellKey.S);

        Assert.Contains("  Media  1 of 4 · ctrl-o to add · ■ sensitive", Texts(compose));
        Assert.Equal(ComposeField.Attachment, compose.Typing);
    }

    /// <summary>
    ///     <c>ctrl-z</c> on the Media rows brings back the one last taken off, in its place and as far as it had got —
    ///     one deep — and the walk lands on it.
    /// </summary>
    [Fact]
    public async Task CtrlZBringsBackTheLastRemovalInItsPlace()
    {
        var (shell, built, compose) = await Composing("one.png", "two.png", "three.png");

        shell.Press(ShellKey.Up);
        shell.Press(ShellKey.Up);
        shell.Press(ShellKey.Delete);
        shell.Press(ShellKey.Delete);

        Assert.Equal(["one.png"], Names(compose));

        // What was taken off still hears how its upload went, so that it comes back as far as it had got.
        built.Author.Attaching[2].Ready();
        built.Host.Drain();

        shell.Press(ShellKey.CtrlZ);

        Assert.Equal(["one.png", "three.png"], Names(compose));
        Assert.StartsWith("    ▌⠶     three.png", Row(compose, "three.png"), StringComparison.Ordinal);
        Assert.EndsWith("x  no alt text", Row(compose, "three.png"), StringComparison.Ordinal);

        shell.Press(ShellKey.CtrlZ);

        Assert.Equal(["one.png", "three.png"], Names(compose));
    }

    /// <summary>
    ///     <c>ctrl-z</c> is the Media rows' only while the walk is on them: from the post it is the editor's own undo,
    ///     and brings nothing back.
    /// </summary>
    [Fact]
    public async Task CtrlZOffTheMediaRowsBringsNothingBack()
    {
        var (shell, _, compose) = await Composing("one.png", "two.png");

        shell.Press(ShellKey.Up);
        shell.Press(ShellKey.Delete);
        shell.Press(ShellKey.Down);
        shell.Press(ShellKey.Down);

        Assert.Equal(ComposeField.Post, compose.Typing);

        shell.Press(ShellKey.CtrlZ);

        Assert.Equal(["one.png"], Names(compose));
    }

    /// <summary>Nothing comes back onto a post that has no room for it.</summary>
    [Fact]
    public async Task CtrlZBringsNothingBackOntoAFullPost()
    {
        var (shell, _, compose) = await Composing(PostLimits.Default with { Attachments = 2 }, "one.png", "two.png");

        shell.Press(ShellKey.Up);
        shell.Press(ShellKey.Delete);
        shell.Paste(_files.WriteFile("three.png"));
        shell.Press(ShellKey.CtrlZ);

        Assert.Equal(["one.png", "three.png"], Names(compose));
    }

    /// <summary>
    ///     <c>shift-↑</c> and <c>shift-↓</c> move the picked row a place earlier or later, still picked, and the post
    ///     goes out in the order shown.
    /// </summary>
    [Fact]
    public async Task ShiftArrowsReorderAndThePostGoesOutInTheOrderShown()
    {
        var (shell, built, compose) = await Composing("one.png", "two.png", "three.png");

        AllReady(built);
        shell.Press(ShellKey.Up);
        shell.Press(ShellKey.ShiftUp);
        shell.Press(ShellKey.ShiftUp);
        shell.Press(ShellKey.ShiftUp);

        Assert.Equal(["three.png", "one.png", "two.png"], Names(compose));
        Assert.StartsWith("    ▌⠶     three.png", Row(compose, "three.png"), StringComparison.Ordinal);

        shell.Press(ShellKey.ShiftDown);

        Assert.Equal(["one.png", "three.png", "two.png"], Names(compose));

        await shell.Send();

        Assert.Equal(
            ["m1", "m3", "m2"],
            Assert.Single(built.Author.Published).Draft.Attached.Select(attached => attached.Id));
    }

    /// <summary>
    ///     On a row the status row offers what can be done there: moving it, taking it off, a retry only where one can
    ///     help, and bringing back what was taken off.
    /// </summary>
    [Fact]
    public async Task OnARowTheStatusRowOffersItsKeys()
    {
        var (shell, built, _) = await Composing("one.png", "two.png");

        built.Author.Attaching[1].Refuse(Dropped());
        built.Host.Drain();
        shell.Press(ShellKey.Up);

        Assert.Contains(new KeyHint("r", "retry"), shell.Keys);
        Assert.Contains(new KeyHint("del", "remove"), shell.Keys);
        Assert.Contains(new KeyHint("shift-↑↓", "move"), shell.Keys);
        Assert.DoesNotContain(shell.Keys, hint => hint.Key == "ctrl-z");

        shell.Press(ShellKey.Up);

        Assert.DoesNotContain(shell.Keys, hint => hint.Key == "r");

        shell.Press(ShellKey.Delete);

        Assert.Contains(new KeyHint("ctrl-z", "bring back"), shell.Keys);
    }

    /// <summary>
    ///     A drop of more than the post has room for attaches up to the limit, and the status row says how many were
    ///     left out; a drop onto a full post attaches nothing and says so.
    /// </summary>
    [Fact]
    public async Task ADropPastTheLimitSaysHowManyWereLeftOut()
    {
        var (shell, _, compose) = await Composing(PostLimits.Default with { Attachments = 3 });
        var paths = new[] { "a.png", "b.png", "c.png", "d.png", "e.png" }.Select(name => _files.WriteFile(name));

        Assert.True(shell.Paste(string.Join(' ', paths)));

        Assert.Equal(["a.png", "b.png", "c.png"], Names(compose));
        Assert.Equal("2 left out — 3 is the most a post can carry.", shell.Notice);

        Assert.True(shell.Paste(_files.WriteFile("f.png")));

        Assert.Equal(3, compose.Attachments.Count);
        Assert.Equal("Nothing attached — 3 is the most a post can carry.", shell.Notice);
    }

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

    /// <summary>
    ///     A shell opened on a fresh compose with <paramref name="names" /> dropped onto it, each one still going up, its
    ///     instance setting <paramref name="limits" />.
    /// </summary>
    private async Task<(Wooly.Tui.Shell.Shell Shell, AShell Built, ComposeScreen Compose)> Composing(
        params string[] names) =>
        await Composing(null, names);

    private async Task<(Wooly.Tui.Shell.Shell Shell, AShell Built, ComposeScreen Compose)> Composing(
        PostLimits? limits,
        params string[] names)
    {
        var built = new AShell { Limits = FakeInstanceLimits.Setting(limits) };
        var shell = await built.Opened();
        var compose = ComposeRows.Open(shell, ComposeFor.Post);

        built.Host.Drain();

        foreach (var name in names)
        {
            Assert.True(shell.Paste(_files.WriteFile(name)));
        }

        // Drawn once, as a terminal would have before anything is pressed: the walk keeps to what was drawn.
        _ = Lines(compose);

        return (shell, built, compose);
    }

    /// <summary>A whole window at 80 × 24 on a fresh compose with <paramref name="names" /> dropped onto it.</summary>
    private Task<DrawnShell> Drawn(params string[] names) => Drawn(new AShell(), names);

    private async Task<DrawnShell> Drawn(AShell built, params string[] names)
    {
        var drawn = await DrawnShell.Of(80, 24, Themes.Plain, built);

        drawn.Shell.Compose();
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

    /// <summary>Every attachment ready, as the instance answered them.</summary>
    private static void AllReady(AShell built)
    {
        foreach (var sending in built.Author.Attaching)
        {
            sending.Ready();
        }

        built.Host.Drain();
    }

    /// <summary>The row naming <paramref name="name" />.</summary>
    private static string Row(ComposeScreen compose, string name, int width = Width) =>
        Texts(compose, width).Single(row => row.Contains(name, StringComparison.Ordinal));

    /// <summary>The names on the rows, top to bottom.</summary>
    private static IEnumerable<string> Names(ComposeScreen compose) =>
        compose.Attachments.Select(attachment => attachment.Name);

    private static Line Media(ComposeScreen compose) =>
        Lines(compose).Single(line => line.Text.StartsWith("  Media", StringComparison.Ordinal));

    private static IReadOnlyList<Line> Lines(ComposeScreen compose, int width = Width) =>
        compose.Lines(new Drawing(width, AShell.Now, Height: Height));

    private static IReadOnlyList<string> Texts(ComposeScreen compose, int width = Width) =>
        [.. Lines(compose, width).Select(line => line.Text)];
}
