using Terminal.Gui.Input;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Clipboard;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;

namespace Wooly.Tests.Tui;

/// <summary>
///     <c>ctrl-v</c> on a compose screen (#380): a picture or files copied on this machine are attached as a drop is, and
///     anything else is left to the field's own paste, as it always was.
/// </summary>
public class ComposeClipboardTests : IDisposable
{
    /// <summary>A small PNG's worth of bytes — what a screenshot on the clipboard reads as.</summary>
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];

    private readonly TemporaryDirectory _files = new();

    public void Dispose() => _files.Dispose();

    /// <summary>
    ///     A picture on the clipboard is written to a file of its own, named for the paste, and attached from there — going
    ///     up to the instance at once, as anything attached does (ADR-0026).
    /// </summary>
    [Theory]
    [InlineData(ComposeFor.Post)]
    [InlineData(ComposeFor.Reply)]
    public async Task APictureIsAttachedAsPastedOne(ComposeFor purpose)
    {
        var (shell, built, compose) = await Composing(purpose);

        built.Clipboard.Holding = new Clipped.Picture(Png);

        Assert.True(shell.PasteFromTheClipboard());

        var sent = Assert.Single(built.Author.Attaching);

        Assert.Equal("pasted-1.png", Path.GetFileName(sent.Path));
        Assert.Equal(Png, File.ReadAllBytes(sent.Path));
        Assert.Equal("pasted-1.png", Assert.Single(compose.Attachments).Name);
        Assert.Contains(Texts(compose), row => row.Contains("pasted-1.png", StringComparison.Ordinal));
    }

    /// <summary>Each picture pasted is named for its turn, so that two rows never read the same.</summary>
    [Fact]
    public async Task PicturesAreNumberedInTurn()
    {
        var (shell, built, compose) = await Composing();

        built.Clipboard.Holding = new Clipped.Picture(Png);

        shell.PasteFromTheClipboard();
        shell.PasteFromTheClipboard();

        Assert.Equal(["pasted-1.png", "pasted-2.png"], compose.Attachments.Select(attachment => attachment.Name));
    }

    /// <summary>
    ///     Files copied in a file manager are attached as if dropped: in the order copied, the ones the instance does
    ///     not take left behind, and no more than the post has room for.
    /// </summary>
    [Fact]
    public async Task CopiedFilesAttachAsADropDoes()
    {
        var (shell, built, compose) = await Composing(
            limits: PostLimits.Default with { Attachments = 2, MediaTypes = ["image/png", "image/gif"] });

        built.Clipboard.Holding = new Clipped.Files(
        [
            _files.WriteFile("a.png"), _files.WriteFile("notes.txt"), _files.WriteFile("b.gif"),
            _files.WriteFile("c.png"),
        ]);

        Assert.True(shell.PasteFromTheClipboard());

        Assert.Equal(["a.png", "b.gif"], compose.Attachments.Select(attachment => attachment.Name));
        Assert.Equal(2, built.Author.Attaching.Count);
    }

    /// <summary>
    ///     Copied files the instance takes none of are said so on the status row, and nothing is attached — nor is
    ///     anything pasted for them.
    /// </summary>
    [Fact]
    public async Task CopiedFilesOfNoAcceptedTypeAreSaidSo()
    {
        var (shell, built, compose) = await Composing();

        built.Clipboard.Holding = new Clipped.Files([_files.WriteFile("notes.txt")]);

        Assert.True(shell.PasteFromTheClipboard());

        Assert.Empty(compose.Attachments);
        Assert.Equal("None of the copied files is a type this instance takes.", shell.Notice);
        Assert.True(shell.NoticeIsError);
    }

    /// <summary>Text on the clipboard — or nothing — is not taken, which leaves the paste to the field as it was.</summary>
    [Fact]
    public async Task TextIsLeftToTheField()
    {
        var (shell, built, compose) = await Composing();

        Assert.False(shell.PasteFromTheClipboard());

        Assert.Equal(1, built.Clipboard.Reads);
        Assert.Empty(compose.Attachments);
        Assert.Null(shell.Notice);
    }

    /// <summary>
    ///     With nothing to read the clipboard with, the status row says what picture paste needs — once a session, not
    ///     at every paste — and the paste is left to the field, so text still pastes.
    /// </summary>
    [Fact]
    public async Task AMissingToolIsSaidOnce()
    {
        var (shell, built, _) = await Composing();
        const string why = "Pasting a picture or files needs wl-paste or xclip installed.";

        built.Clipboard.Holding = new Clipped.NoTool(why);

        Assert.False(shell.PasteFromTheClipboard());
        built.Host.Drain();
        Assert.Equal(why, shell.Notice);
        Assert.False(shell.NoticeIsError);

        shell.Back();
        ComposeRows.Open(shell, ComposeFor.Post);

        Assert.False(shell.PasteFromTheClipboard());
        built.Host.Drain();
        Assert.NotEqual(why, shell.Notice);
    }

    /// <summary>
    ///     An edit carries its attachments through as they were (ADR-0008), so <c>ctrl-v</c> on one is the field's
    ///     paste, and the clipboard is not even read.
    /// </summary>
    [Fact]
    public async Task OnAnEditTheClipboardIsNotRead()
    {
        var built = new AShell { Pasted = new PastedPictures(_files.Path), Timelines = FakeTimelineReader.Holding(APost.With(account: "jeff@mastodon.social")) };
        var shell = await built.Opened();

        ComposeRows.Open(shell, ComposeFor.Edit);
        built.Clipboard.Holding = new Clipped.Picture(Png);

        Assert.False(shell.PasteFromTheClipboard());
        Assert.Equal(0, built.Clipboard.Reads);
        Assert.Empty(built.Author.Attaching);
    }

    /// <summary>
    ///     <c>ctrl-v</c> in the post, or in the warning over it, attaches a picture on the clipboard, and puts nothing
    ///     into the field it was pressed in.
    /// </summary>
    [Fact]
    public async Task CtrlVInAnyFieldAttachesAPicture()
    {
        var built = new AShell { Pasted = new PastedPictures(_files.Path) };

        built.Clipboard.Holding = new Clipped.Picture(Png);

        using var composed = await ComposedView.Of(built);

        composed.Type("Look");
        composed.Press(Key.V.WithCtrl);
        composed.Press(Key.W.WithCtrl);
        composed.Press(Key.V.WithCtrl);

        Assert.Equal(["pasted-1.png", "pasted-2.png"], composed.Compose.Attachments.Select(attachment => attachment.Name));
        Assert.Equal("Look", composed.Editor.Text);
        Assert.Equal(string.Empty, composed.Warning.Text);
    }

    /// <summary>
    ///     <c>ctrl-v</c> with text on the clipboard is the field's own paste, as it was before anything could be
    ///     attached.
    /// </summary>
    [Fact]
    public async Task CtrlVWithTextIsTheFieldsOwnPaste()
    {
        using var composed = await ComposedView.Of();

        composed.Application.Clipboard!.TrySetClipboardData("hello");
        composed.Press(Key.V.WithCtrl);

        Assert.Equal("hello", composed.Editor.Text);
        Assert.Empty(composed.Compose.Attachments);
        Assert.Equal(1, composed.Built.Clipboard.Reads);
    }

    /// <summary>
    ///     With nothing to read the clipboard with, <c>ctrl-v</c> still pastes text — and the status row still says
    ///     what picture paste needs, the paste it said it over notwithstanding.
    /// </summary>
    [Fact]
    public async Task CtrlVWithNoToolPastesTextAndSaysWhy()
    {
        var built = new AShell { Pasted = new PastedPictures(_files.Path) };
        const string why = "Pasting a picture or files needs wl-paste or xclip installed.";

        built.Clipboard.Holding = new Clipped.NoTool(why);

        using var composed = await ComposedView.Of(built);

        composed.Application.Clipboard!.TrySetClipboardData("hello");
        composed.Press(Key.V.WithCtrl);
        composed.Settle();

        Assert.Equal("hello", composed.Editor.Text);
        Assert.Equal(why, composed.Shell.Notice);
    }

    /// <summary>A shell opened on a compose screen for <paramref name="purpose" />.</summary>
    private async Task<(Wooly.Tui.Shell.Shell Shell, AShell Built, ComposeScreen Compose)> Composing(
        ComposeFor purpose = ComposeFor.Post,
        PostLimits? limits = null)
    {
        var built = new AShell { Pasted = new PastedPictures(_files.Path), Limits = FakeInstanceLimits.Setting(limits) };
        var shell = await built.Opened();
        var compose = ComposeRows.Open(shell, purpose);

        built.Host.Drain();

        return (shell, built, compose);
    }

    private static IReadOnlyList<string> Texts(ComposeScreen compose) =>
        [.. compose.Lines(new Drawing(78, AShell.Now, Height: 21)).Select(line => line.Text)];
}
