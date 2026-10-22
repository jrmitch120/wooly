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
            "     ⠶     Screenshot 2024-02-29 at 9.31.0…    217 KB  x  no alt text",
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
            "     ⠶     Copper.jpeg                       picture    3.6 MB  x  no alt text",
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

        Assert.Contains("  Media  1 of 2 · ctrl-o to add · □ sensitive", Texts(compose));

        shell.Paste(_files.WriteFile("two.png"));

        Assert.Contains("  Media  2 of 2 · □ sensitive", Texts(compose));
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
        Assert.Contains("  Media  3 of 4 · ctrl-o to add · □ sensitive", roomy);
        Assert.DoesNotContain(folded, row => row.Contains("three.png", StringComparison.Ordinal));
        Assert.Contains("  Media  3 of 4 · 3 no alt text · 1 failed · ctrl-o to add · □ sensitive", folded);
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

        drawn.Shell.Press(ShellKey.C);
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
