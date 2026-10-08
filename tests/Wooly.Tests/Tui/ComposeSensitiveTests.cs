using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The sensitive toggle at the end of the Media header's line (#379): the author's own while there is no warning,
///     on and locked while there is one (ADR-0008), and sent with the post.
/// </summary>
public class ComposeSensitiveTests : IDisposable
{
    /// <summary>The content panel's inside at an 80 × 24 terminal.</summary>
    private const int Width = 78;

    private const int Height = 21;

    /// <summary>Nothing attached, there is nothing to hide and no toggle; once something is, it ends the line, muted.</summary>
    [Fact]
    public async Task TheToggleShowsOnceSomethingIsAttached()
    {
        var (shell, _, compose) = await Composing();

        Assert.Equal(ComposeRows.NoMedia, MediaRow(compose).Text);

        shell.Paste(_files.WriteFile("cat.png"));

        var media = MediaRow(compose);

        Assert.Equal("  Media  1 of 4 · ctrl-o to add · □ sensitive", media.Text);
        Assert.Equal(Role.Muted, Assert.Single(media.Spans, span => span.Text == "□ sensitive").Role);
    }

    /// <summary>
    ///     <c>↑</c> from the post reaches the Media header, then Warn; <c>s</c> there puts what is attached behind a
    ///     click, in the warning's colour, and the post goes out marked so.
    /// </summary>
    [Fact]
    public async Task S_OnTheMediaHeader_TogglesWhatIsSent()
    {
        var (shell, built, compose) = await Attached();

        shell.Press(ShellKey.Up);

        Assert.Equal(ComposeField.Media, compose.Typing);
        Assert.True(shell.Press(ShellKey.S));

        var on = MediaRow(compose);

        Assert.Equal("  Media  1 of 4 · ctrl-o to add · ■ sensitive", on.Text);
        Assert.Equal(Role.ContentWarning, Assert.Single(on.Spans, span => span.Text == "■ sensitive").Role);

        await shell.Send();

        Assert.True(Assert.Single(built.Author.Published).Draft.Sensitive);
    }

    /// <summary>Toggled on and off again, the post goes out with its attachments in plain view.</summary>
    [Fact]
    public async Task S_Twice_SendsItUnmarked()
    {
        var (shell, built, compose) = await Attached();

        shell.Press(ShellKey.Up);
        shell.Press(ShellKey.S);
        shell.Press(ShellKey.S);

        Assert.EndsWith("□ sensitive", MediaRow(compose).Text, StringComparison.Ordinal);

        await shell.Send();

        Assert.False(Assert.Single(built.Author.Published).Draft.Sensitive);
    }

    /// <summary><c>s</c> anywhere but on the Media header is nobody's: the post's and the warning's fields type it.</summary>
    [Fact]
    public async Task S_OffTheMediaHeader_TogglesNothing()
    {
        var (shell, _, compose) = await Attached();

        Assert.False(shell.Press(ShellKey.S));
        Assert.EndsWith("□ sensitive", MediaRow(compose).Text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     With something attached, walking on up from the Media header reaches Warn, and back down reaches the post.
    /// </summary>
    [Fact]
    public async Task TheMediaHeaderIsWalkedBetweenWarnAndThePost()
    {
        var (shell, _, compose) = await Attached();

        shell.Press(ShellKey.Up);
        Assert.Equal(ComposeField.Media, compose.Typing);

        shell.Press(ShellKey.Up);
        Assert.Equal(ComposeField.Warning, compose.Typing);

        shell.Press(ShellKey.Down);
        shell.Press(ShellKey.Down);
        Assert.Equal(ComposeField.Post, compose.Typing);
    }

    /// <summary>An edit's Media header is read-only (#381), so the walk goes from the post straight to Warn.</summary>
    [Fact]
    public async Task AnEditWalksPastWhereMediaWouldBe()
    {
        var built = new AShell { Timelines = FakeTimelineReader.Holding(APost.With(account: "jeff@mastodon.social")) };
        var compose = ComposeRows.Open(await built.Opened(), ComposeFor.Edit);

        compose.Walk(-1);

        Assert.Equal(ComposeField.Warning, compose.Typing);
    }

    /// <summary>On the Media header, with something attached, the status row offers the toggle.</summary>
    [Fact]
    public async Task TheStatusRowOffersTheToggleOnTheHeader()
    {
        var (shell, _, _) = await Attached();

        Assert.DoesNotContain(new KeyHint("s", "sensitive"), shell.Keys);

        shell.Press(ShellKey.Up);

        Assert.Contains(new KeyHint("s", "sensitive"), shell.Keys);
    }

    /// <summary>
    ///     A warning written over the post holds the toggle on and says why, in the warning's colour; <c>s</c> cannot
    ///     change it then, and the status row says what is holding it. Clearing the warning gives back the author's own.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AWarningLocksItOnAndClearingItRestoresTheAuthors(bool chosen)
    {
        var (shell, built, compose) = await Attached();

        shell.Press(ShellKey.Up);

        if (chosen)
        {
            shell.Press(ShellKey.S);
        }

        compose.RewriteWarning("spoilers");

        var locked = MediaRow(compose);

        Assert.Equal("  Media  1 of 4 · ctrl-o to add · ■ sensitive (warning)", locked.Text);
        Assert.Equal(Role.ContentWarning, Assert.Single(locked.Spans, span => span.Text.StartsWith('■')).Role);

        shell.Press(ShellKey.S);

        Assert.EndsWith("■ sensitive (warning)", MediaRow(compose).Text, StringComparison.Ordinal);
        Assert.Equal("The warning already hides what is attached.", shell.Notice);

        compose.RewriteWarning(string.Empty);

        Assert.EndsWith(chosen ? "■ sensitive" : "□ sensitive", MediaRow(compose).Text, StringComparison.Ordinal);

        await shell.Send();

        Assert.Equal(chosen, Assert.Single(built.Author.Published).Draft.Sensitive);
    }

    /// <summary>A post sent with a warning goes out with it, which marks the post sensitive (ADR-0008).</summary>
    [Fact]
    public async Task AWarnedPostGoesOutWithItsWarning()
    {
        var (shell, built, compose) = await Attached();

        compose.RewriteWarning("spoilers");

        await shell.Send();

        Assert.Equal("spoilers", Assert.Single(built.Author.Published).Draft.ContentWarning);
    }

    /// <summary>Folded into one line on a short terminal, the header still ends with the toggle.</summary>
    [Fact]
    public async Task TheFoldedLineEndsWithTheToggle()
    {
        var (shell, built, compose) = await Attached();

        shell.Paste(_files.WriteFile("dog.png"));
        shell.Paste(_files.WriteFile("bird.png"));
        built.Author.Attaching[1].Ready();
        built.Author.Attaching[2].Ready();
        built.Host.Drain();

        Assert.Equal(
            "  Media  3 of 4 · 3 no alt text · ctrl-o to add · □ sensitive",
            MediaRow(compose, height: 15).Text);
    }

    /// <summary>
    ///     A click on the toggle flips it, as a click on To chooses (#338), and what goes out says so; a click on it
    ///     while a warning holds it on changes nothing.
    /// </summary>
    [Fact]
    public async Task AClickOnTheToggleFlipsIt()
    {
        var built = new AShell();
        using var view = await ComposedView.Of(built);

        view.Shell.Paste(_files.WriteFile("cat.png"));
        built.Author.Attaching.Single().Ready();
        view.Settle();

        view.ClickOn("□ sensitive");

        Assert.Contains(view.Rows(), row => row.Contains("· ■ sensitive", StringComparison.Ordinal));

        view.Compose.RewriteWarning("spoilers");
        view.Redraw();
        view.ClickOn("■ sensitive (warning)");
        view.Compose.RewriteWarning(string.Empty);
        view.Redraw();

        Assert.Contains(view.Rows(), row => row.TrimEnd().EndsWith("· ■ sensitive", StringComparison.Ordinal));

        await view.Shell.Send();

        Assert.True(Assert.Single(built.Author.Published).Draft.Sensitive);
    }

    /// <summary>
    ///     In the window, <c>↑</c> off the post's first line lands on the Media header and <c>s</c> there flips the
    ///     toggle rather than typing anywhere; the header's words light up while it has the typing.
    /// </summary>
    [Fact]
    public async Task InTheWindowSOnTheHeaderFlipsIt()
    {
        var built = new AShell();
        using var view = await ComposedView.Of(built);

        view.Shell.Paste(_files.WriteFile("cat.png"));
        built.Author.Attaching.Single().Ready();
        view.Settle();

        // Up onto the row, then onto the header over it (#378).
        view.Press(Terminal.Gui.Input.Key.CursorUp);
        view.Press(Terminal.Gui.Input.Key.CursorUp);
        view.Press(Terminal.Gui.Input.Key.S);

        Assert.Equal(ComposeField.Media, view.Compose.Typing);
        Assert.Contains(view.Rows(), row => row.TrimEnd().EndsWith("· ■ sensitive", StringComparison.Ordinal));
        Assert.Equal(string.Empty, view.Compose.Text);
        Assert.Equal(Role.SelectedText, MediaRow(view.Compose).Spans.Single(span => span.Text == "1 of 4").Role);
    }

    private readonly TemporaryDirectory _files = new();

    public void Dispose() => _files.Dispose();

    /// <summary>A shell opened on a compose screen for <paramref name="purpose" />.</summary>
    private static async Task<(Wooly.Tui.Shell.Shell Shell, AShell Built, ComposeScreen Compose)> Composing(
        ComposeFor purpose = ComposeFor.Post)
    {
        var built = new AShell();
        var shell = await built.Opened();
        var compose = ComposeRows.Open(shell, purpose);

        built.Host.Drain();

        return (shell, built, compose);
    }

    /// <summary>A shell opened on a compose screen holding one attachment, ready.</summary>
    private async Task<(Wooly.Tui.Shell.Shell Shell, AShell Built, ComposeScreen Compose)> Attached()
    {
        var (shell, built, compose) = await Composing();

        shell.Paste(_files.WriteFile("cat.png"));
        built.Author.Attaching.Single().Ready();
        built.Host.Drain();

        return (shell, built, compose);
    }

    /// <summary>The Media header's line, drawn <paramref name="height" /> tall.</summary>
    private static Line MediaRow(ComposeScreen compose, int height = Height) =>
        compose.Lines(new Drawing(Width, AShell.Now, Height: height))
               .Single(line => line.Text.StartsWith("  Media", StringComparison.Ordinal));
}
