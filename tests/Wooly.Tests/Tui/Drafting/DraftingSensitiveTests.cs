using Wooly.Tests.Fakes;
using Wooly.Tui.Drafting;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The sensitive toggle at the end of the Media header's line (#379), asked of drafting (ADR-0029, #421): the
///     author's own while there is no warning, on and locked while there is one (ADR-0008), and sent with the post.
/// </summary>
public class DraftingSensitiveTests : IDisposable
{
    /// <summary>The content panel's inside at an 80 × 24 terminal.</summary>
    private const int Width = 78;

    private const int Height = 21;

    private readonly TemporaryDirectory _files = new();

    public void Dispose() => _files.Dispose();

    /// <summary>Nothing attached, there is nothing to hide and no toggle; once something is, it ends the line, muted.</summary>
    [Fact]
    public void TheToggleShowsOnceSomethingIsAttached()
    {
        var (drafting, compose) = Composing();

        Assert.Equal(ComposeRows.NoMedia, MediaRow(compose).Text);

        drafting.Drafting.Paste(_files.WriteFile("cat.png"));

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
        var (drafting, compose) = Attached();

        drafting.Press(ShellKey.Up);

        Assert.Equal(ComposeField.Media, compose.Typing);
        Assert.Equal(Drafted.Used, drafting.Press(ShellKey.S));

        var on = MediaRow(compose);

        Assert.Equal("  Media  1 of 4 · ctrl-o to add · ■ sensitive", on.Text);
        Assert.Equal(Role.ContentWarning, Assert.Single(on.Spans, span => span.Text == "■ sensitive").Role);

        await drafting.Drafting.Send();

        Assert.True(Assert.Single(drafting.Fakes.Author.Published).Draft.Sensitive);
    }
    /// <summary>Toggled on and off again, the post goes out with its attachments in plain view.</summary>
    [Fact]
    public async Task S_Twice_SendsItUnmarked()
    {
        var (drafting, compose) = Attached();

        drafting.Press(ShellKey.Up);
        drafting.Press(ShellKey.S);
        drafting.Press(ShellKey.S);

        Assert.EndsWith("□ sensitive", MediaRow(compose).Text, StringComparison.Ordinal);

        await drafting.Drafting.Send();

        Assert.False(Assert.Single(drafting.Fakes.Author.Published).Draft.Sensitive);
    }
    /// <summary><c>s</c> anywhere but on the Media header is nobody's: the post's and the warning's fields type it.</summary>
    [Fact]
    public void S_OffTheMediaHeader_TogglesNothing()
    {
        var (drafting, compose) = Attached();

        Assert.Equal(Drafted.NotMine, drafting.Press(ShellKey.S));
        Assert.EndsWith("□ sensitive", MediaRow(compose).Text, StringComparison.Ordinal);
    }
    /// <summary>
    ///     With something attached, walking on up from the Media header reaches Warn, and back down reaches the post.
    /// </summary>
    [Fact]
    public void TheMediaHeaderIsWalkedBetweenWarnAndThePost()
    {
        var (drafting, compose) = Attached();

        drafting.Press(ShellKey.Up);
        Assert.Equal(ComposeField.Media, compose.Typing);

        drafting.Press(ShellKey.Up);
        Assert.Equal(ComposeField.Warning, compose.Typing);

        drafting.Press(ShellKey.Down);
        drafting.Press(ShellKey.Down);
        Assert.Equal(ComposeField.Post, compose.Typing);
    }
    /// <summary>On the Media header, with something attached, the status row offers the toggle.</summary>
    [Fact]
    public void TheStatusRowOffersTheToggleOnTheHeader()
    {
        var (drafting, _) = Attached();

        Assert.DoesNotContain(new KeyHint("s", "sensitive"), drafting.Stack.Top.Keys);

        drafting.Press(ShellKey.Up);

        Assert.Contains(new KeyHint("s", "sensitive"), drafting.Stack.Top.Keys);
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
        var (drafting, compose) = Attached();

        drafting.Press(ShellKey.Up);

        if (chosen)
        {
            drafting.Press(ShellKey.S);
        }

        compose.RewriteWarning("spoilers");

        var locked = MediaRow(compose);

        Assert.Equal("  Media  1 of 4 · ctrl-o to add · ■ sensitive (warning)", locked.Text);
        Assert.Equal(Role.ContentWarning, Assert.Single(locked.Spans, span => span.Text.StartsWith('■')).Role);

        drafting.Press(ShellKey.S);

        Assert.EndsWith("■ sensitive (warning)", MediaRow(compose).Text, StringComparison.Ordinal);
        Assert.Equal("The warning already hides what is attached.", drafting.Notice);

        compose.RewriteWarning(string.Empty);

        Assert.EndsWith(chosen ? "■ sensitive" : "□ sensitive", MediaRow(compose).Text, StringComparison.Ordinal);

        await drafting.Drafting.Send();

        Assert.Equal(chosen, Assert.Single(drafting.Fakes.Author.Published).Draft.Sensitive);
    }
    /// <summary>A post sent with a warning goes out with it, which marks the post sensitive (ADR-0008).</summary>
    [Fact]
    public async Task AWarnedPostGoesOutWithItsWarning()
    {
        var (drafting, compose) = Attached();

        compose.RewriteWarning("spoilers");

        await drafting.Drafting.Send();

        Assert.Equal("spoilers", Assert.Single(drafting.Fakes.Author.Published).Draft.ContentWarning);
    }
    /// <summary>Folded into one line on a short terminal, the header still ends with the toggle.</summary>
    [Fact]
    public void TheFoldedLineEndsWithTheToggle()
    {
        var (drafting, compose) = Attached();

        drafting.Drafting.Paste(_files.WriteFile("dog.png"));
        drafting.Drafting.Paste(_files.WriteFile("bird.png"));
        drafting.Fakes.Author.Attaching[1].Ready();
        drafting.Fakes.Author.Attaching[2].Ready();
        drafting.Fakes.Host.Drain();

        Assert.Equal(
            "  Media  3 of 4 · 3 no alt text · ctrl-o to add · □ sensitive",
            MediaRow(compose, height: 15).Text);
    }
    /// <summary>Drafting with a fresh compose open.</summary>
    private static (ADrafting Drafting, ComposeScreen Compose) Composing()
    {
        var drafting = new ADrafting().Build();

        drafting.Press(ShellKey.C);
        drafting.Fakes.Host.Drain();

        return (drafting, drafting.Composing);
    }

    /// <summary>Drafting with a fresh compose open, holding one attachment, ready.</summary>
    private (ADrafting Drafting, ComposeScreen Compose) Attached()
    {
        var (drafting, compose) = Composing();

        drafting.Drafting.Paste(_files.WriteFile("cat.png"));
        drafting.Fakes.Author.Attaching.Single().Ready();
        drafting.Fakes.Host.Drain();

        return (drafting, compose);
    }

    /// <summary>The Media header's line, drawn <paramref name="height" /> tall.</summary>
    private static Line MediaRow(ComposeScreen compose, int height = Height) =>
        compose.Lines(new Drawing(Width, AShell.Now, Height: height))
               .Single(line => line.Text.StartsWith("  Media", StringComparison.Ordinal));
}
