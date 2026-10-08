using Wooly.Core.Errors;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     Descriptions on pending attachments (#377): walking onto an attachment's row, the description editor it opens,
///     and the description going up to the instance — once the attachment is there to put it on, and again whenever it
///     changes — before the post that names it is published.
/// </summary>
public class ComposeDescriptionTests : IDisposable
{
    /// <summary>The content panel's inside at an 80 × 24 terminal.</summary>
    private const int Width = 78;

    private const int Height = 21;

    private readonly TemporaryDirectory _files = new();

    /// <summary>
    ///     <c>enter</c> on a row opens the description editor over the draft, named after the attachment, with the
    ///     instance's limit to count against.
    /// </summary>
    [Fact]
    public async Task EnterOnARowOpensTheDescriptionEditor()
    {
        var (shell, _, compose) = await Composing();

        shell.Paste(_files.WriteFile("cat.png"));
        Walk(shell, ShellKey.Up);
        shell.Press(ShellKey.Enter);

        var describing = Assert.IsType<DescriptionScreen>(shell.Screen);

        Assert.Equal(["Home", "Compose", "Describe cat.png"], shell.Crumbs);
        Assert.Same(compose.Attachments[0], describing.Attachment);

        var rows = Texts(describing);

        Assert.Contains(rows, row => row.Trim() == "Description (alt text)");
        Assert.Equal("0 / 1500", rows[^1].Trim());
    }

    /// <summary>
    ///     <c>enter</c> on the Media header above the rows opens the file browser (#376), not the description editor:
    ///     there is nothing there to describe.
    /// </summary>
    [Fact]
    public async Task EnterOnTheHeaderDescribesNothing()
    {
        var (shell, _, _) = await Composing();

        shell.Paste(_files.WriteFile("cat.png"));
        Walk(shell, ShellKey.Up);
        Walk(shell, ShellKey.Up);
        shell.Press(ShellKey.Enter);

        Assert.IsType<FileBrowserScreen>(shell.Screen);
    }

    /// <summary>The editor's counter is out of the instance's own description limit, where it gives one.</summary>
    [Fact]
    public async Task TheCounterIsOutOfTheInstancesLimit()
    {
        var (shell, _, _) = await Composing(PostLimits.Default with { Descriptions = 420 });

        shell.Paste(_files.WriteFile("cat.png"));
        Walk(shell, ShellKey.Up);
        shell.Press(ShellKey.Enter);
        shell.WriteDescription("A cat");

        Assert.Equal("5 / 420", Texts(shell.Screen)[^1].Trim());
    }

    /// <summary>
    ///     Past the limit the counter turns to the error's colour, so a description the instance would refuse is seen
    ///     before it is sent.
    /// </summary>
    [Fact]
    public async Task ACounterPastTheLimitIsAnError()
    {
        var (shell, _, _) = await Composing(PostLimits.Default with { Descriptions = 10 });

        shell.Paste(_files.WriteFile("cat.png"));
        Walk(shell, ShellKey.Up);
        shell.Press(ShellKey.Enter);
        shell.WriteDescription("A cat on a mat");

        var counter = Lines(shell.Screen)[^1];

        Assert.Equal(Role.Error, Assert.Single(counter.Spans, span => span.Text == "14 / 10").Role);
    }

    /// <summary>
    ///     <c>esc</c> is done, not cancel: it goes back to the draft keeping what was typed, which the row says in quotes
    ///     once the attachment is ready.
    /// </summary>
    [Theory]
    [InlineData(ShellKey.Escape)]
    [InlineData(ShellKey.CtrlS)]
    public async Task DoneKeepsTheDescriptionAndTheRowQuotesIt(ShellKey done)
    {
        var (shell, built, compose) = await Composing();

        shell.Paste(_files.WriteFile("cat.png"));
        built.Author.Attaching.Single().Ready();
        built.Host.Drain();

        Walk(shell, ShellKey.Up);
        shell.Press(ShellKey.Enter);
        shell.WriteDescription("A cat asleep on a mat");
        shell.Press(done);

        Assert.Same(compose, shell.Screen);
        Assert.Null(shell.Asking);
        Assert.EndsWith("x  “A cat asleep on a mat”", Row(compose, "cat.png", width: 120), StringComparison.Ordinal);
    }

    /// <summary>
    ///     A description longer than the status column is cut inside its closing quote, so the row still reads as one
    ///     quotation.
    /// </summary>
    [Fact]
    public async Task ALongDescriptionIsCutInsideItsClosingQuote()
    {
        var (shell, built, compose) = await Composing();

        shell.Paste(_files.WriteFile("cat.png"));
        built.Author.Attaching.Single().Ready();
        built.Host.Drain();
        Walk(shell, ShellKey.Up);
        shell.Press(ShellKey.Enter);
        shell.WriteDescription("A dog in a red knitted coat sitting on the steps of a house in the rain");
        shell.Press(ShellKey.Escape);

        Assert.EndsWith("x  “A dog in a red knitted coat sitting o…”", Row(compose, "cat.png", width: 120), StringComparison.Ordinal);
    }

    /// <summary>
    ///     A description written while the file is still going up waits for the attachment to be there, then goes to
    ///     the instance — and the post published names an attachment that carries it.
    /// </summary>
    [Fact]
    public async Task ADescriptionWrittenDuringTheUploadGoesUpOnceItIsReady()
    {
        var (shell, built, _) = await Composing();

        shell.Paste(_files.WriteFile("cat.png"));
        Walk(shell, ShellKey.Up);
        shell.Press(ShellKey.Enter);
        shell.WriteDescription("A cat");
        shell.Press(ShellKey.Escape);
        built.Host.Drain();

        Assert.Empty(built.Author.Descriptions);

        built.Author.Attaching.Single().Ready();
        built.Host.Drain();

        var described = Assert.Single(built.Author.Descriptions);

        Assert.Equal(("personal", "m1", "A cat"), (described.Profile, described.Id, described.Description));

        await shell.Send();
        built.Host.Drain();

        Assert.Equal("m1", Assert.Single(Assert.Single(built.Author.Published).Draft.Attached).Id);
    }

    /// <summary>A description written once the attachment is ready goes up as the editor is left.</summary>
    [Fact]
    public async Task ADescriptionWrittenOnceReadyGoesUpWhenTheEditorIsLeft()
    {
        var (shell, built, _) = await Composing();

        shell.Paste(_files.WriteFile("cat.png"));
        built.Author.Attaching.Single().Ready();
        built.Host.Drain();

        Walk(shell, ShellKey.Up);
        shell.Press(ShellKey.Enter);
        shell.WriteDescription("A cat");

        Assert.Empty(built.Author.Descriptions);

        shell.Press(ShellKey.Escape);
        built.Host.Drain();

        Assert.Equal("A cat", Assert.Single(built.Author.Descriptions).Description);
    }

    /// <summary>
    ///     A description changed after it went up goes up again, before the post is published — and one left as it was
    ///     is not sent twice.
    /// </summary>
    [Fact]
    public async Task AChangedDescriptionIsSentAgainBeforeThePost()
    {
        var (shell, built, _) = await Composing();

        shell.Paste(_files.WriteFile("cat.png"));
        built.Author.Attaching.Single().Ready();
        built.Host.Drain();

        Describe(shell, "A cat");
        Describe(shell, "A cat");
        Describe(shell, "A grey cat");

        await shell.Send();
        built.Host.Drain();

        Assert.Equal(["A cat", "A grey cat"], built.Author.Descriptions.Select(described => described.Description));
        Assert.Single(built.Author.Published);
    }

    /// <summary>
    ///     <c>ctrl-s</c> while a description is still on its way waits for it, as it waits for an upload, and then
    ///     sends.
    /// </summary>
    [Fact]
    public async Task SendingWaitsForADescriptionOnItsWay()
    {
        var (shell, built, compose) = await Composing();

        built.Author.HoldingDescriptions = true;
        shell.Paste(_files.WriteFile("cat.png"));
        built.Author.Attaching.Single().Ready();
        built.Host.Drain();
        Describe(shell, "A cat");

        await shell.Send();
        built.Host.Drain();

        Assert.Same(compose, shell.Screen);
        Assert.Empty(built.Author.Published);
        Assert.Equal("Will send once 1 attachment finishes — esc to stop.", shell.Notice);

        built.Author.Descriptions.Single().Land();
        built.Host.Drain();

        Assert.Single(built.Author.Published);
        Assert.Equal("Sent.", shell.Notice);
    }

    /// <summary>
    ///     A description the instance refused is said on the status row, and stops a waiting send rather than letting
    ///     the post go out without it; the next <c>ctrl-s</c> sends it again.
    /// </summary>
    [Fact]
    public async Task ARefusedDescriptionIsSaidAndStopsTheSend()
    {
        var (shell, built, compose) = await Composing();

        built.Author.HoldingDescriptions = true;
        shell.Paste(_files.WriteFile("cat.png"));
        built.Author.Attaching.Single().Ready();
        built.Host.Drain();
        Describe(shell, "A cat");

        await shell.Send();
        built.Author.Descriptions.Single().Refuse(new AttachmentRefusedException("Description is too long"));
        built.Host.Drain();

        Assert.Empty(built.Author.Published);
        Assert.Same(compose, shell.Screen);
        Assert.True(shell.NoticeIsError);
        Assert.Equal("The description of cat.png was refused: Description is too long", shell.Notice);

        built.Author.HoldingDescriptions = false;

        await shell.Send();
        built.Host.Drain();

        Assert.Equal(2, built.Author.Descriptions.Count);
        Assert.Single(built.Author.Published);
    }

    /// <summary>
    ///     A description that failed for a reason nothing here expected — a bare HTTP error — is said as a refusal is, and
    ///     stops a waiting send rather than leaving it waiting on a description that will never arrive (review of #372).
    /// </summary>
    [Fact]
    public async Task ADescriptionThatFailedAnyWayIsSaidAndStopsTheSend()
    {
        var (shell, built, compose) = await Composing();

        built.Author.HoldingDescriptions = true;
        shell.Paste(_files.WriteFile("cat.png"));
        built.Author.Attaching.Single().Ready();
        built.Host.Drain();
        Describe(shell, "A cat");

        await shell.Send();
        built.Author.Descriptions.Single().Refuse(new HttpRequestException("Response status code does not indicate success"));
        built.Host.Drain();

        Assert.Empty(built.Author.Published);
        Assert.Same(compose, shell.Screen);
        Assert.True(shell.NoticeIsError);
        Assert.Equal(
            "The description of cat.png was refused: Response status code does not indicate success",
            shell.Notice);

        built.Author.HoldingDescriptions = false;

        await shell.Send();
        built.Host.Drain();

        Assert.Single(built.Author.Published);
    }

    /// <summary>
    ///     A row with no description carries the quiet mark, and the post sends with no question about it and no
    ///     description sent.
    /// </summary>
    [Fact]
    public async Task SendingWithoutDescriptionsAsksNothing()
    {
        var (shell, built, compose) = await Composing();

        shell.Paste(_files.WriteFile("cat.png"));
        built.Author.Attaching.Single().Ready();
        built.Host.Drain();

        Assert.EndsWith("x  no alt text", Row(compose, "cat.png"), StringComparison.Ordinal);

        await shell.Send();
        built.Host.Drain();

        Assert.Null(shell.Asking);
        Assert.Single(built.Author.Published);
        Assert.Empty(built.Author.Descriptions);
    }

    /// <summary>
    ///     In the window: a click on a row picks it out, a click on its description opens the editor, and what is typed
    ///     there is the description.
    /// </summary>
    [Fact]
    public async Task InTheWindowAClickOnTheMarkOpensTheEditor()
    {
        using var drawn = await DrawnShell.Of(100, 30, Themes.Plain, new AShell());

        drawn.Shell.Compose();
        drawn.Redraw();
        drawn.Shell.Paste(_files.WriteFile("cat.png"));
        drawn.Built.Author.Attaching.Single().Ready();
        drawn.Settle();

        var (row, name) = Find(drawn, "cat.png");

        drawn.Click(name, row);

        var compose = Assert.IsType<ComposeScreen>(drawn.Shell.Screen);

        Assert.Equal("cat.png", compose.PickedAttachment?.Name);

        drawn.Click(Find(drawn, "no alt text").Column, row);

        Assert.IsType<DescriptionScreen>(drawn.Shell.Screen);

        foreach (var letter in "A cat")
        {
            drawn.Press(letter);
        }

        drawn.Press(Terminal.Gui.Input.Key.Esc);
        drawn.Settle();

        Assert.Same(compose, drawn.Shell.Screen);
        Assert.Contains(drawn.Rows(), text => text.Contains("“A cat”", StringComparison.Ordinal));
        Assert.Equal("A cat", Assert.Single(drawn.Built.Author.Descriptions).Description);
    }

    /// <summary>A double click anywhere on a row opens the editor.</summary>
    [Fact]
    public async Task InTheWindowADoubleClickOnARowOpensTheEditor()
    {
        using var drawn = await DrawnShell.Of(100, 30, Themes.Plain, new AShell());

        drawn.Shell.Compose();
        drawn.Redraw();
        drawn.Shell.Paste(_files.WriteFile("cat.png"));
        drawn.Settle();

        var (row, name) = Find(drawn, "cat.png");

        drawn.DoubleClick(name, row);

        Assert.IsType<DescriptionScreen>(drawn.Shell.Screen);
    }

    /// <summary>In the window, <c>↑</c> from the post walks onto the last row and <c>enter</c> there opens the editor.</summary>
    [Fact]
    public async Task InTheWindowTheKeysWalkOntoARowAndOpenTheEditor()
    {
        using var drawn = await DrawnShell.Of(100, 30, Themes.Plain, new AShell());

        drawn.Shell.Compose();
        drawn.Redraw();
        drawn.Shell.Paste(_files.WriteFile("cat.png"));
        drawn.Settle();

        drawn.PressThroughTheApplication(Terminal.Gui.Input.Key.CursorUp);

        Assert.Contains(drawn.Rows(), text => text.Contains("▌⠶     cat.png", StringComparison.Ordinal));

        drawn.PressThroughTheApplication(Terminal.Gui.Input.Key.Enter);

        Assert.IsType<DescriptionScreen>(drawn.Shell.Screen);
    }

    public void Dispose() => _files.Dispose();

    /// <summary>
    ///     <paramref name="key" /> pressed on the screen in front once it has been drawn, as a terminal would have drawn it
    ///     before anything is pressed: the walk keeps to the rows that were drawn (#378).
    /// </summary>
    private static void Walk(Wooly.Tui.Shell.Shell shell, ShellKey key)
    {
        _ = Lines(shell.Screen);

        shell.Press(key);
    }

    /// <summary>Opens the description editor on the only attachment, writes <paramref name="description" /> and is done.</summary>
    private static void Describe(Wooly.Tui.Shell.Shell shell, string description)
    {
        Walk(shell, ShellKey.Up);
        shell.Press(ShellKey.Enter);
        shell.WriteDescription(description);
        shell.Press(ShellKey.Escape);
        Walk(shell, ShellKey.Down);
    }

    /// <summary>The row and column <paramref name="text" /> is first drawn at in the window.</summary>
    private static (int Row, int Column) Find(DrawnShell drawn, string text)
    {
        var rows = drawn.Rows();
        var row = Array.FindIndex(rows, drawnRow => drawnRow.Contains(text, StringComparison.Ordinal));

        return (row, rows[row].IndexOf(text, StringComparison.Ordinal));
    }

    private async Task<(Wooly.Tui.Shell.Shell Shell, AShell Built, ComposeScreen Compose)> Composing(
        PostLimits? limits = null)
    {
        var built = new AShell { Limits = FakeInstanceLimits.Setting(limits) };
        var shell = await built.Opened();
        var compose = ComposeRows.Open(shell, ComposeFor.Post);

        built.Host.Drain();

        return (shell, built, compose);
    }

    private static string Row(ComposeScreen compose, string name, int width = Width) =>
        Texts(compose, width).Single(row => row.Contains(name, StringComparison.Ordinal));

    private static IReadOnlyList<Line> Lines(Screen screen, int width = Width, int height = Height) =>
        screen.Lines(new Drawing(width, AShell.Now, Height: height));

    private static IReadOnlyList<string> Texts(Screen screen, int width = Width, int height = Height) =>
        [.. Lines(screen, width, height).Select(line => line.Text)];
}
