using Terminal.Gui.Input;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The Media header on an edit (#381): the attachments the post already carries, listed read-only so that the author
///     can see they stay, with nothing on the header that adds, takes off or reorders one — what an edit carries through
///     is ADR-0008's, unchanged.
/// </summary>
public class ComposeEditMediaTests : IDisposable
{
    /// <summary>The content panel's inside at an 80 × 24 terminal.</summary>
    private const int Width = 78;

    private const int Height = 21;

    private static readonly Post Illustrated = APost.With(
        account: "jeff@mastodon.social",
        media:
        [
            APost.APicture("m1", "A cartoon sheep"),
            APost.Attached(MediaKind.Video, "m2", description: null),
        ]);

    /// <summary>
    ///     An edit of a post with attachments lists them under the Media header, which counts them and says they stay:
    ///     a row each, in a pending attachment's columns — its kind, and its description in quotes or the quiet mark
    ///     where it has none, in the status column (review of #372). At 80 columns the kind column has given way, and
    ///     the kind takes the name's, there being no name to show.
    /// </summary>
    [Fact]
    public async Task AnEditListsThePostsAttachmentsWithTheirDescriptions()
    {
        var (_, _, compose) = await Editing(Illustrated);

        Assert.Equal(
            [
                ComposeRows.NoWarning,
                "  Media  2 · kept as they are",
                "           picture                                        “A cartoon sheep”",
                "           video                                          no alt text",
                ComposeRows.Hairline(Width),
            ],
            Texts(compose).SkipWhile(row => !row.StartsWith("   Warn", StringComparison.Ordinal))
                          .Take(5)
                          .Select(row => row.TrimEnd()));
    }

    /// <summary>
    ///     On a wide terminal the kind is in the kind column, and the description in the status column, each where a
    ///     pending attachment's row has them: a row of the one lines up with a row of the other.
    /// </summary>
    [Fact]
    public async Task OnAWideTerminalTheColumnsAreAPendingRowsColumns()
    {
        var (_, _, edit) = await Editing(Illustrated);
        var built = new AShell();
        var shell = await built.Opened();
        var fresh = ComposeRows.Open(shell, ComposeFor.Post);

        built.Host.Drain();
        shell.Paste(_files.WriteFile("cat.png"));
        built.Author.Attaching.Single().Ready();
        built.Host.Drain();

        var kept = Texts(edit, width: 120).Single(row => row.Contains("A cartoon sheep", StringComparison.Ordinal));
        var pending = Texts(fresh, width: 120).Single(row => row.Contains("cat.png", StringComparison.Ordinal));

        Assert.Equal(pending.IndexOf("picture", StringComparison.Ordinal), kept.IndexOf("picture", StringComparison.Ordinal));
        Assert.Equal(pending.IndexOf("no alt text", StringComparison.Ordinal), kept.IndexOf('“'));
    }

    /// <summary>
    ///     On a terminal that draws, an attachment the post carries shows a small picture in the picture column, sent for
    ///     from the instance's preview of it through the feed's picture path; one with no picture to draw — a sound —
    ///     wants none, and its column is blank.
    /// </summary>
    [Fact]
    public async Task AKeptPictureIsDrawnFromItsPreview()
    {
        var sound = APost.Attached(MediaKind.Audio, "m3", description: "A sheep") with { Preview = null };
        var (_, _, compose) = await Editing(APost.With(
            account: "jeff@mastodon.social",
            media: [APost.APicture("m1", "A cartoon sheep"), sound]));
        var sheep = Drawn.Kept(APost.APicture("m1", "A cartoon sheep"));
        var pictures = new FakePictures().Holding(sheep.Id, 40, 30);

        var lines = compose.Lines(new Drawing(Width, AShell.Now, pictures, ARaster.Sixel(), Height: Height));
        var picture = lines.Single(line => line.Text.Contains("A cartoon sheep", StringComparison.Ordinal));
        var audio = lines.Single(line => line.Text.Contains("A sheep”", StringComparison.Ordinal));

        Assert.Equal("https://files.mastodon.social/m1/small.png", sheep.Address);
        Assert.Equal(sheep, picture.Wants);
        Assert.Equal(new Inset(sheep, 7, 3, 1), Assert.Single(picture.Insets));
        Assert.Null(audio.Wants);
        Assert.Empty(audio.Insets);
    }

    /// <summary>
    ///     An edit of a post with nothing attached shows the header as a fresh post does with nothing attached, but
    ///     without the key that would add something, there being no way to on an edit.
    /// </summary>
    [Fact]
    public async Task AnEditWithNothingAttachedSaysNoneAndNoKey()
    {
        var (_, _, compose) = await Editing(APost.With(account: "jeff@mastodon.social"));
        var rows = Texts(compose);

        Assert.Equal(
            [ComposeRows.NoWarning, "  Media  none", ComposeRows.Hairline(Width)],
            rows.SkipWhile(row => !row.StartsWith("   Warn", StringComparison.Ordinal)).Take(3));
    }

    /// <summary>
    ///     The three compose screens stay alike (ADR-0015): with the same headers, the editor starts at the same row on
    ///     an edit as on a fresh post — with nothing attached, and with as many rows under Media.
    /// </summary>
    [Fact]
    public async Task TheEditorStartsWhereAFreshPostsDoes()
    {
        var size = new System.Drawing.Size(Width, Height);
        var (_, _, bare) = await Editing(APost.With(account: "jeff@mastodon.social"));
        var (_, _, illustrated) = await Editing(Illustrated);
        var built = new AShell();
        var shell = await built.Opened();
        var fresh = ComposeRows.Open(shell, ComposeFor.Post);

        Assert.Equal(fresh.EditorAt(size).Y, bare.EditorAt(size).Y);

        shell.Paste(_files.WriteFile("one.png"));
        shell.Paste(_files.WriteFile("two.png"));

        Assert.Equal(fresh.EditorAt(size).Y, illustrated.EditorAt(size).Y);
    }

    /// <summary>
    ///     On a terminal too short for a row each, the rows fold into the header's line as a fresh post's do, counting
    ///     what has no description.
    /// </summary>
    [Fact]
    public async Task OnAShortTerminalTheRowsFoldIntoTheHeader()
    {
        var (_, _, compose) = await Editing(Illustrated);
        var folded = Texts(compose, height: 14);

        Assert.Contains("  Media  2 · 1 no alt text · kept as they are", folded);
        Assert.DoesNotContain(folded, row => row.Contains("A cartoon sheep", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Nothing on an edit's Media header adds an attachment, takes one off or opens anything: not a click on its
    ///     words or on a row, not <c>ctrl-o</c>, not <c>enter</c> or <c>del</c> on it, and not a dropped file.
    /// </summary>
    [Fact]
    public async Task NothingOnTheHeaderAddsRemovesOrOpens()
    {
        var built = new AShell { Timelines = FakeTimelineReader.Holding(Illustrated) };

        using var drawn = await DrawnShell.Of(80, 24, Themes.Plain, built);

        drawn.Shell.Edit();
        drawn.Redraw();

        var compose = Assert.IsType<ComposeScreen>(drawn.Shell.Screen);
        var before = drawn.Rows();
        var header = Array.FindIndex(before, row => row.Contains("Media  2", StringComparison.Ordinal));
        var picture = Array.FindIndex(before, row => row.Contains("“A cartoon", StringComparison.Ordinal));

        drawn.Click(before[header].IndexOf("Media", StringComparison.Ordinal) + 8, header);
        drawn.Click(before[picture].IndexOf("picture", StringComparison.Ordinal), picture);
        drawn.Click(before[picture].IndexOf("A cartoon", StringComparison.Ordinal), picture);
        drawn.Press(Key.O.WithCtrl);
        drawn.Press(Key.CursorUp);
        drawn.Press(Key.Enter);
        drawn.Press(Key.Delete);
        drawn.Shell.Paste(_files.WriteFile("cat.png"));
        drawn.Redraw();

        var after = drawn.Rows();

        Assert.Same(compose, drawn.Shell.Screen);
        Assert.Empty(compose.Media.Attachments);
        Assert.Empty(built.Author.Attaching);
        Assert.Contains(after, row => row.Contains("Media  2 · kept as they are", StringComparison.Ordinal));
        Assert.Contains(after, row => row.Contains("“A cartoon", StringComparison.Ordinal));
        Assert.Contains(after, row => row.Contains("no alt text", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Saving the edit sends the change it always did — the text and the warning, nothing about attachments — so the
    ///     post's own carry through as ADR-0008 has them, and nothing is sent up or published afresh.
    /// </summary>
    [Fact]
    public async Task SavingCarriesTheAttachmentsThroughUnchanged()
    {
        var (shell, built, compose) = await Editing(Illustrated);

        compose.Rewrite("Hello again");
        await shell.Send();
        built.Host.Drain();

        var changed = Assert.Single(built.Author.Edits);

        Assert.Equal("110", changed.PostId);
        Assert.Equal("Hello again", changed.Edit.Text);
        Assert.Empty(built.Author.Attaching);
        Assert.Empty(built.Author.Published);
    }

    private readonly TemporaryDirectory _files = new();

    public void Dispose() => _files.Dispose();

    /// <summary>A shell opened on an edit of <paramref name="post" />, the profile's own.</summary>
    private static async Task<(Wooly.Tui.Shell.Shell Shell, AShell Built, ComposeScreen Compose)> Editing(Post post)
    {
        var built = new AShell { Timelines = FakeTimelineReader.Holding(post) };
        var shell = await built.Opened();
        var compose = ComposeRows.Open(shell, ComposeFor.Edit);

        built.Host.Drain();

        return (shell, built, compose);
    }

    private static IReadOnlyList<Line> Lines(ComposeScreen compose, int height = Height, int width = Width) =>
        compose.Lines(new Drawing(width, AShell.Now, Height: height));

    private static IReadOnlyList<string> Texts(ComposeScreen compose, int height = Height, int width = Width) =>
        [.. Lines(compose, height, width).Select(line => line.Text)];
}
