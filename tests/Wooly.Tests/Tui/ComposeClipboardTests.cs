using Terminal.Gui.Input;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Clipboard;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     <c>ctrl-v</c> on a compose screen (#380), pressed in its fields and in the window: a picture or files copied on this
///     machine are attached as a drop is, and anything else is left to the field's own paste, as it always was. What
///     drafting makes of the clipboard is <see cref="DraftingClipboardTests" />'.
/// </summary>
public class ComposeClipboardTests : IDisposable
{
    /// <summary>A small PNG's worth of bytes — what a screenshot on the clipboard reads as.</summary>
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];

    private readonly TemporaryDirectory _files = new();

    public void Dispose() => _files.Dispose();

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

        Assert.Equal(["pasted-1.png", "pasted-2.png"], composed.Compose.Media.Attachments.Select(attachment => attachment.Name));
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

        Copied(composed, "hello");
        composed.Press(Key.V.WithCtrl);

        Assert.Equal("hello", composed.Editor.Text);
        Assert.Empty(composed.Compose.Media.Attachments);
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

        Copied(composed, "hello");
        composed.Press(Key.V.WithCtrl);
        composed.Settle();

        Assert.Equal("hello", composed.Editor.Text);
        Assert.Equal(why, composed.Shell.Notice);
    }

    /// <summary>
    ///     <c>alt-v</c> is <c>ctrl-v</c> too, for the terminals that keep <c>ctrl-v</c> for their own paste — Windows
    ///     Terminal and the console host among them — where <c>ctrl-v</c> never reaches Wooly: in the post, the warning
    ///     and Lang it attaches a picture on the clipboard and puts nothing into the field.
    /// </summary>
    [Fact]
    public async Task AltVInAnyFieldAttachesAPicture()
    {
        var built = new AShell { Pasted = new PastedPictures(_files.Path) };

        built.Clipboard.Holding = new Clipped.Picture(Png);

        using var composed = await ComposedView.Of(built);

        composed.Type("Look");
        composed.Press(Key.V.WithAlt);
        composed.Press(Key.W.WithCtrl);
        composed.Press(Key.V.WithAlt);
        composed.Click(composed.Lang);
        composed.Press(Key.V.WithAlt);

        Assert.Equal(
            ["pasted-1.png", "pasted-2.png", "pasted-3.png"],
            composed.Compose.Media.Attachments.Select(attachment => attachment.Name));
        Assert.Equal("Look", composed.Editor.Text);
        Assert.Equal(string.Empty, composed.Warning.Text);
        Assert.Equal(string.Empty, composed.Lang.Text);
    }

    /// <summary>
    ///     <c>alt-v</c> with text on the clipboard is the field's own paste, as <c>ctrl-v</c> is — in the post and in a
    ///     header.
    /// </summary>
    [Fact]
    public async Task AltVWithTextIsTheFieldsOwnPaste()
    {
        using var composed = await ComposedView.Of();

        Copied(composed, "hello");
        composed.Press(Key.V.WithAlt);
        composed.Press(Key.W.WithCtrl);
        composed.Press(Key.V.WithAlt);

        Assert.Equal("hello", composed.Editor.Text);
        Assert.Equal("hello", composed.Warning.Text);
        Assert.Empty(composed.Compose.Media.Attachments);
    }

    /// <summary>
    ///     In the window, on the Media header — which takes no typing, and so no paste of its own — <c>ctrl-v</c> and
    ///     <c>alt-v</c> each attach a picture on the clipboard, on a fresh post and on a reply.
    /// </summary>
    [Theory]
    [InlineData(ComposeFor.Post)]
    [InlineData(ComposeFor.Reply)]
    public async Task OnTheMediaHeaderBothKeysAttach(ComposeFor purpose)
    {
        var built = new AShell { Pasted = new PastedPictures(_files.Path) };

        built.Clipboard.Holding = new Clipped.Picture(Png);

        using var drawn = await DrawnShell.Of(80, 24, Themes.Plain, built);
        var compose = ComposeRows.Open(drawn.Shell, purpose);

        drawn.Redraw();
        drawn.Press(Key.CursorUp);

        Assert.Equal(ComposeField.Media, compose.Typing);

        drawn.Press(Key.V.WithCtrl);
        drawn.Press(Key.V.WithAlt);

        Assert.Equal(["pasted-1.png", "pasted-2.png"], compose.Media.Attachments.Select(attachment => attachment.Name));
        Assert.Equal(2, built.Author.Attaching.Count);
    }

    /// <summary>
    ///     In the window, on the attachments screen a short terminal lists the rows on, <c>ctrl-v</c> and <c>alt-v</c>
    ///     attach to the compose under it, as <c>ctrl-o</c> there does (story 58).
    /// </summary>
    [Fact]
    public async Task OnTheAttachmentsScreenBothKeysAttach()
    {
        var built = new AShell { Pasted = new PastedPictures(_files.Path) };

        using var drawn = await DrawnShell.Of(80, 18, Themes.Plain, built);
        var compose = ComposeRows.Open(drawn.Shell, ComposeFor.Post);

        foreach (var name in new[] { "one.png", "two.png", "three.png" })
        {
            Assert.True(drawn.Shell.Paste(_files.WriteFile(name)));
        }

        drawn.Redraw();
        drawn.Press(Key.CursorUp);
        drawn.Press(Key.Enter);

        Assert.IsType<AttachmentsScreen>(drawn.Shell.Screen);
        Assert.Contains(drawn.Shell.Screen.Keys, key => key is { Key: "ctrl-v/alt-v", Does: "paste" });

        built.Clipboard.Holding = new Clipped.Picture(Png);
        drawn.Press(Key.V.WithCtrl);

        Assert.IsType<AttachmentsScreen>(drawn.Shell.Screen);
        Assert.Equal(
            ["one.png", "two.png", "three.png", "pasted-1.png"],
            compose.Media.Attachments.Select(attachment => attachment.Name));
        Assert.Contains(drawn.Rows(), row => row.Contains("pasted-1.png", StringComparison.Ordinal));

        // Room for one more, the post carrying all four it can.
        drawn.Press(Key.Delete);
        drawn.Press(Key.V.WithAlt);

        Assert.Equal(
            ["two.png", "three.png", "pasted-1.png", "pasted-2.png"],
            compose.Media.Attachments.Select(attachment => attachment.Name));
    }

    /// <summary>
    ///     The status row offers the paste where attaching is what the place is for — on the Media header, and on the
    ///     attachments screen — naming both keys, since which of them reaches Wooly is the terminal's to say.
    /// </summary>
    [Fact]
    public async Task TheStatusRowOffersBothKeysOnTheMediaHeader()
    {
        var (shell, _, compose) = await Composing();

        Assert.DoesNotContain(compose.Keys, key => key.Key == "ctrl-v/alt-v");

        shell.Press(ShellKey.Up);

        Assert.Equal(ComposeField.Media, compose.Typing);
        Assert.Contains(compose.Keys, key => key is { Key: "ctrl-v/alt-v", Does: "paste" });
    }

    /// <summary>A shell opened on a compose screen for <paramref name="purpose" />.</summary>
    private async Task<(Wooly.Tui.Shell.Shell Shell, AShell Built, ComposeScreen Compose)> Composing(
        ComposeFor purpose = ComposeFor.Post)
    {
        var built = new AShell { Pasted = new PastedPictures(_files.Path) };
        var shell = await built.Opened();
        var compose = ComposeRows.Open(shell, purpose);

        built.Host.Drain();

        return (shell, built, compose);
    }

    /// <summary>
    ///     Puts <paramref name="text" /> on the clipboard a field pastes from — or skips, where it cannot be written.
    /// </summary>
    /// <remarks>
    ///     On macOS, Terminal.Gui finds its clipboard by running <c>which pbcopy</c>, which it never does with output
    ///     redirected, as a test runner's always is: so the write is refused, while a field's paste still reads the
    ///     real pasteboard — whatever was last copied on the machine. Linux, where CI runs, falls back to an in-memory
    ///     clipboard that takes the write.
    /// </remarks>
    private static void Copied(ComposedView composed, string text) =>
        Assert.SkipUnless(
            composed.Application.Clipboard!.TrySetClipboardData(text),
            "This platform's clipboard cannot be written under a test runner, so a paste would read the real one.");
}
