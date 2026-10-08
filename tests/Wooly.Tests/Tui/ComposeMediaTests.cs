using Wooly.Core.Errors;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The Media header on a compose and a reply screen, and the pending attachments under it (#375): a file dropped onto
///     the terminal is attached, goes up to the instance at once (ADR-0026), and its row says how far it has got.
/// </summary>
public class ComposeMediaTests : IDisposable
{
    /// <summary>The content panel's inside at an 80 × 24 terminal.</summary>
    private const int Width = 78;

    private const int Height = 21;

    /// <summary>
    ///     Every header has a word for a label, right-aligned in a column five wide — Warn in place of the feed's mark —
    ///     and Media sits under Warn, saying there is nothing attached and the key that adds something.
    /// </summary>
    [Theory]
    [InlineData(ComposeFor.Post)]
    [InlineData(ComposeFor.Reply)]
    public async Task TheHeadersReadAsOneBlockWithMediaUnderWarn(ComposeFor purpose)
    {
        var compose = ComposeRows.Open(await new AShell().Opened(), purpose);
        var rows = Texts(compose);

        Assert.Equal("   From  @jeff · mastodon.social", rows[1]);
        Assert.Equal("   Lang  none · the instance decides", rows[3]);
        Assert.Equal(
            [ComposeRows.NoWarning, ComposeRows.NoMedia],
            rows.SkipWhile(row => !row.Contains("Warn", StringComparison.Ordinal)).Take(2));
    }

    /// <summary>Media's words are muted, as Warn's are, until something is attached.</summary>
    [Fact]
    public async Task AnEmptyMediaHeaderIsMuted()
    {
        var compose = ComposeRows.Open(await new AShell().Opened(), ComposeFor.Post);
        var media = Lines(compose).Single(line => line.Text.StartsWith("  Media", StringComparison.Ordinal));

        Assert.All(media.Spans.Where(span => span.Text.Trim().Length > 0), span => Assert.Equal(Role.Muted, span.Role));
    }

    /// <summary>
    ///     A paste made of one existing file of a type the instance accepts is a drop: it attaches the file rather than
    ///     typing its path, and the file starts going up as a pending attachment at once (ADR-0026).
    /// </summary>
    [Theory]
    [InlineData(ComposeFor.Post)]
    [InlineData(ComposeFor.Reply)]
    public async Task DroppingAnAcceptedFileAttachesItAndSendsItUp(ComposeFor purpose)
    {
        var (shell, built, compose) = await Composing(purpose);
        var cat = _files.WriteFile("cat.png");

        Assert.True(shell.Paste(cat));

        Assert.Equal(cat, Assert.Single(built.Author.Attaching).Path);
        Assert.Equal("cat.png", Assert.Single(compose.Attachments).Name);
        Assert.Equal("personal", built.Author.Attaching[0].Profile);
        Assert.Contains("token-personal", built.Tokens);
    }

    /// <summary>
    ///     A row says where its attachment has got to, in one column: the upload's gauge and share, then that the
    ///     instance is processing it, then — ready, with nothing written about it — the quiet mark.
    /// </summary>
    [Fact]
    public async Task ARowMovesThroughUploadingProcessingAndReady()
    {
        var (shell, built, compose) = await Composing();

        shell.Paste(_files.WriteFile("cat.png"));

        var sending = built.Author.Attaching.Single();

        sending.Progress(0.54);
        built.Host.Drain();

        Assert.EndsWith("x  ████░░░░  54%", Row(compose, "cat.png"), StringComparison.Ordinal);

        sending.Processing();
        built.Host.Drain();

        Assert.EndsWith("x  processing", Row(compose, "cat.png"), StringComparison.Ordinal);

        sending.Ready();
        built.Host.Drain();

        Assert.EndsWith("x  no alt text", Row(compose, "cat.png"), StringComparison.Ordinal);
    }

    /// <summary>
    ///     A row at 80 columns: the grip, the picture's column, the name cut at its own width, the size, <c>x</c> and the
    ///     status — the kind given up first, there being too little room for it with the status column's least.
    /// </summary>
    [Fact]
    public async Task ARowAtEightyColumnsGivesTheKindUpFirst()
    {
        var (shell, built, compose) = await Composing();

        shell.Paste($"'{_files.WriteFile("Screenshot 2024-02-29 at 9.31.03 AM.png", new string('x', 222_000))}'");
        built.Author.Attaching.Single().Ready();
        built.Host.Drain();

        Assert.Equal(
            "     ⠶   Screenshot 2024-02-29 at 9.31.0…    217 KB  x  no alt text",
            Row(compose, "Screenshot"));
    }

    /// <summary>On a wide terminal the kind has its own column, between the name and the size.</summary>
    [Fact]
    public async Task ARowOnAWideTerminalSaysTheKind()
    {
        var (shell, built, compose) = await Composing();

        shell.Paste(_files.WriteFile("Copper.jpeg", new string('x', 3_800_000)));
        built.Author.Attaching.Single().Ready();
        built.Host.Drain();

        Assert.Equal(
            "     ⠶   Copper.jpeg                       picture    3.6 MB  x  no alt text",
            Row(compose, "Copper", width: 120));
    }

    /// <summary>
    ///     The header counts what is attached against the instance's limit, and drops its key once the post is full.
    /// </summary>
    [Fact]
    public async Task TheHeaderCountsAgainstTheInstancesLimit()
    {
        var (shell, _, compose) = await Composing(limits: PostLimits.Default with { Attachments = 2 });

        shell.Paste(_files.WriteFile("one.png"));

        Assert.Contains("  Media  1 of 2 · ctrl-o to add", Texts(compose));

        shell.Paste(_files.WriteFile("two.png"));

        Assert.Contains("  Media  2 of 2", Texts(compose));
    }

    /// <summary>A drop of more files than the post has room for attaches as many as fit, in the order dropped.</summary>
    [Fact]
    public async Task ADropPastTheLimitAttachesWhatFits()
    {
        var (shell, built, compose) = await Composing(limits: PostLimits.Default with { Attachments = 2 });
        var paths = new[] { "a.png", "b.png", "c.png" }.Select(name => _files.WriteFile(name)).ToList();

        Assert.True(shell.Paste(string.Join(' ', paths)));

        Assert.Equal(["a.png", "b.png"], compose.Attachments.Select(attachment => attachment.Name));
        Assert.Equal(2, built.Author.Attaching.Count);
    }

    /// <summary>
    ///     Paths come the ways terminals paste a drop: several at once, spaces escaped with a backslash, or in quotes —
    ///     and every one of them is attached, in order.
    /// </summary>
    [Fact]
    public async Task DroppedPathsMayBeEscapedOrQuoted()
    {
        var (shell, _, compose) = await Composing();
        var spaced = _files.WriteFile("a cat.png");
        var quoted = _files.WriteFile("a dog.jpg");
        var plain = _files.WriteFile("bird.gif");

        Assert.True(shell.Paste($"{spaced.Replace(" ", "\\ ", StringComparison.Ordinal)} '{quoted}' {plain}"));

        Assert.Equal(["a cat.png", "a dog.jpg", "bird.gif"], compose.Attachments.Select(attachment => attachment.Name));
        Assert.Equal(["picture", "picture", "animation"], compose.Attachments.Select(attachment => attachment.KindWord));
    }

    /// <summary>
    ///     Anything that is not entirely existing files the instance accepts is text, left for the field to paste: a
    ///     sentence with a path in it, a file of a type the instance does not take, a path with nothing there, a name
    ///     that is not a whole path.
    /// </summary>
    [Fact]
    public async Task AnyOtherPasteIsText()
    {
        var (shell, built, compose) = await Composing(limits: PostLimits.Default with { MediaTypes = ["image/png"] });
        var cat = _files.WriteFile("cat.png");

        Assert.False(shell.Paste($"look at {cat}"));
        Assert.False(shell.Paste(_files.WriteFile("notes.txt")));
        Assert.False(shell.Paste(_files.WriteFile("dog.jpg")));
        Assert.False(shell.Paste($"{cat} {Path.Combine(_files.Path, "gone.png")}"));
        Assert.False(shell.Paste("cat.png"));
        Assert.False(shell.Paste("   "));

        Assert.Empty(compose.Attachments);
        Assert.Empty(built.Author.Attaching);
    }

    /// <summary>An edit carries the post's attachments through as they were, and a drop on one is text (ADR-0008).</summary>
    [Fact]
    public async Task ADropOnAnEditIsText()
    {
        var built = new AShell { Timelines = FakeTimelineReader.Holding(APost.With(account: "jeff@mastodon.social")) };
        var shell = await built.Opened();

        ComposeRows.Open(shell, ComposeFor.Edit);

        Assert.False(shell.Paste(_files.WriteFile("cat.png")));
        Assert.Empty(built.Author.Attaching);
    }

    /// <summary>
    ///     <c>ctrl-s</c> with everything ready publishes the post carrying the pending attachments by id, in the order
    ///     they were attached.
    /// </summary>
    [Fact]
    public async Task SendingPublishesTheAttachmentsInOrder()
    {
        var (shell, built, compose) = await Composing();

        shell.Paste(_files.WriteFile("first.png"));
        shell.Paste(_files.WriteFile("second.mp4"));
        compose.Rewrite("Two things");

        built.Author.Attaching[1].Ready(MediaKind.Video);
        built.Author.Attaching[0].Ready();
        built.Host.Drain();

        await shell.Send();
        built.Host.Drain();

        var draft = Assert.Single(built.Author.Published).Draft;

        Assert.Equal(["m1", "m2"], draft.Attached.Select(attached => attached.Id));
        Assert.Equal("Two things", draft.Text);
        Assert.IsNotType<ComposeScreen>(shell.Screen);
    }

    /// <summary>A post made of nothing but what is attached sends, as Mastodon allows.</summary>
    [Fact]
    public async Task APostWithOnlyAttachmentsSends()
    {
        var (shell, built, _) = await Composing();

        shell.Paste(_files.WriteFile("cat.png"));
        built.Author.Attaching.Single().Ready();
        built.Host.Drain();

        await shell.Send();

        var draft = Assert.Single(built.Author.Published).Draft;

        Assert.Equal(string.Empty, draft.Text);
        Assert.Equal("m1", Assert.Single(draft.Attached).Id);
    }

    /// <summary>
    ///     <c>ctrl-s</c> with something still going up keeps the screen up and says it will send once it has finished —
    ///     sends nothing meanwhile — and then sends.
    /// </summary>
    [Fact]
    public async Task SendingWaitsForWhatIsStillGoingUp()
    {
        var (shell, built, compose) = await Composing();

        shell.Paste(_files.WriteFile("clip.mp4"));
        compose.Rewrite("Watch this");

        await shell.Send();

        Assert.Same(compose, shell.Screen);
        Assert.Empty(built.Author.Published);
        Assert.Equal("Will send once 1 attachment finishes — esc to stop.", shell.Notice);
        Assert.False(shell.NoticeIsError);

        built.Author.Attaching.Single().Processing();
        built.Host.Drain();

        Assert.Empty(built.Author.Published);

        built.Author.Attaching.Single().Ready(MediaKind.Video);
        built.Host.Drain();

        Assert.Equal("m1", Assert.Single(Assert.Single(built.Author.Published).Draft.Attached).Id);
        Assert.Equal("Sent.", shell.Notice);
    }

    /// <summary>
    ///     <c>esc</c> while a send waits calls the send off and leaves the draft as it was, on screen — and nothing goes
    ///     out once the attachment is ready.
    /// </summary>
    [Fact]
    public async Task EscCallsAWaitingSendOff()
    {
        var (shell, built, compose) = await Composing();

        shell.Paste(_files.WriteFile("clip.mp4"));
        compose.Rewrite("Watch this");

        await shell.Send();
        shell.Back();

        Assert.Same(compose, shell.Screen);
        Assert.Null(shell.Asking);
        Assert.Equal("Not sent — the draft is as it was.", shell.Notice);

        built.Author.Attaching.Single().Ready(MediaKind.Video);
        built.Host.Drain();

        Assert.Empty(built.Author.Published);
        Assert.Same(compose, shell.Screen);
        Assert.Equal("Watch this", compose.Text);
        Assert.Single(compose.Attachments);
    }

    /// <summary>
    ///     An attachment the instance refused says why on its row, and a send will not go out without it — nor wait on
    ///     it.
    /// </summary>
    [Fact]
    public async Task ARefusedAttachmentSaysWhyAndStopsTheSend()
    {
        var (shell, built, compose) = await Composing();

        shell.Paste(_files.WriteFile("huge.png"));
        built.Author.Attaching.Single().Refuse(new AttachmentRefusedException("File is too large"));
        built.Host.Drain();

        Assert.EndsWith("x  File is too large", Row(compose, "huge.png"), StringComparison.Ordinal);

        await shell.Send();

        Assert.Empty(built.Author.Published);
        Assert.Same(compose, shell.Screen);
        Assert.True(shell.NoticeIsError);
    }

    /// <summary>A dropped connection says so on its row, as the one failure trying again could mend.</summary>
    [Fact]
    public async Task ADroppedConnectionSaysSo()
    {
        var (shell, built, compose) = await Composing();

        shell.Paste(_files.WriteFile("cat.png"));
        built.Author.Attaching.Single().Refuse(
            new TransientNetworkException(new Uri("https://mastodon.social/api/v2/media"), 3, new HttpRequestException()));
        built.Host.Drain();

        Assert.EndsWith("x  connection lost", Row(compose, "cat.png"), StringComparison.Ordinal);
    }

    /// <summary>Anything attached touches the draft, so leaving it asks first (#373).</summary>
    [Fact]
    public async Task AttachmentsCountAsTouched()
    {
        var (shell, built, compose) = await Composing();

        Assert.False(compose.Touched);

        shell.Paste(_files.WriteFile("cat.png"));
        shell.Back();

        Assert.NotNull(shell.Asking);
        Assert.Same(compose, shell.Screen);

        await shell.Answer(ShellKey.Y);

        Assert.IsNotType<ComposeScreen>(shell.Screen);
        Assert.True(built.Author.Attaching.Single().CalledOff);
    }

    /// <summary>
    ///     Where a row each would leave the editor fewer than three, the rows fold into the header's line before any
    ///     other header gives way, and unfold when there is room again.
    /// </summary>
    [Fact]
    public async Task OnAShortTerminalTheRowsFoldIntoTheHeader()
    {
        var (shell, built, compose) = await Composing();

        shell.Paste(_files.WriteFile("one.png"));
        shell.Paste(_files.WriteFile("two.png"));
        shell.Paste(_files.WriteFile("three.png"));
        built.Author.Attaching[2].Refuse(new AttachmentRefusedException("File is too large"));
        built.Host.Drain();

        // Blank, From, To, Lang, Warn, Media, three rows, hairline, blank: eleven above, two below and three to write.
        var roomy = Texts(compose, height: 16);
        var folded = Texts(compose, height: 15);

        Assert.Contains(roomy, row => row.Contains("three.png", StringComparison.Ordinal));
        Assert.Contains("  Media  3 of 4 · ctrl-o to add", roomy);
        Assert.DoesNotContain(folded, row => row.Contains("three.png", StringComparison.Ordinal));
        Assert.Contains("  Media  3 of 4 · 3 no alt text · 1 failed · ctrl-o to add", folded);
        Assert.Contains("   From  @jeff · mastodon.social", folded);
        Assert.Equal(3, compose.EditorAt(new System.Drawing.Size(Width, 16)).Height);
        Assert.Equal(5, compose.EditorAt(new System.Drawing.Size(Width, 15)).Height);

        var failed = Lines(compose, height: 15).Single(line => line.Text.StartsWith("  Media", StringComparison.Ordinal));

        Assert.Equal(Role.Error, Assert.Single(failed.Spans, span => span.Text == "1 failed").Role);
    }

    /// <summary>
    ///     In the window, a row drawn under the Media header pushes the editor down by a row, so that what is attached
    ///     is never written over (ADR-0015: the screen says where the editor goes, and the view puts it there).
    /// </summary>
    [Fact]
    public async Task InTheWindowTheEditorSitsUnderTheRows()
    {
        using var drawn = await DrawnShell.Of(80, 24, Themes.Plain, new AShell());

        drawn.Shell.Compose();
        drawn.Redraw();

        var editor = drawn.Window.ComposeField<Wooly.Tui.Views.ComposeEditor>();
        var before = editor.FrameToScreen().Y;

        drawn.Shell.Paste(_files.WriteFile("cat.png"));
        drawn.Redraw();

        var rows = drawn.Rows();
        var row = Array.FindIndex(rows, text => text.Contains("cat.png", StringComparison.Ordinal));

        Assert.Contains(rows, text => text.Contains("  Media  1 of 4 · ctrl-o to add", StringComparison.Ordinal));
        Assert.Equal(before + 1, editor.FrameToScreen().Y);
        Assert.True(editor.FrameToScreen().Y > row);
    }

    private readonly TemporaryDirectory _files = new();

    public void Dispose() => _files.Dispose();

    /// <summary>
    ///     A shell opened on a compose screen for <paramref name="purpose" />, its instance setting
    ///     <paramref name="limits" />.
    /// </summary>
    private static async Task<(Wooly.Tui.Shell.Shell Shell, AShell Built, ComposeScreen Compose)> Composing(
        ComposeFor purpose = ComposeFor.Post,
        PostLimits? limits = null)
    {
        var built = new AShell { Limits = FakeInstanceLimits.Setting(limits) };
        var shell = await built.Opened();
        var compose = ComposeRows.Open(shell, purpose);

        built.Host.Drain();

        return (shell, built, compose);
    }

    /// <summary>The row naming <paramref name="name" />, drawn <paramref name="width" /> wide.</summary>
    private static string Row(ComposeScreen compose, string name, int width = Width) =>
        Texts(compose, width).Single(row => row.Contains(name, StringComparison.Ordinal));

    private static IReadOnlyList<Line> Lines(ComposeScreen compose, int width = Width, int height = Height) =>
        compose.Lines(new Drawing(width, AShell.Now, Height: height));

    private static IReadOnlyList<string> Texts(ComposeScreen compose, int width = Width, int height = Height) =>
        [.. Lines(compose, width, height).Select(line => line.Text)];
}
