using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The sensitive toggle at the end of the Media header's line (#379), clicked and pressed in the window — and an
///     edit's walk past where it would be. What it does to the draft is drafting's
///     (<see cref="DraftingSensitiveTests" />).
/// </summary>
public class ComposeSensitiveTests : IDisposable
{
    /// <summary>The content panel's inside at an 80 × 24 terminal.</summary>
    private const int Width = 78;

    private const int Height = 21;

    /// <summary>An edit's Media header is read-only (#381), so the walk goes from the post straight to Warn.</summary>
    [Fact]
    public async Task AnEditWalksPastWhereMediaWouldBe()
    {
        var built = new AShell { Timelines = FakeTimelineReader.Holding(APost.With(account: "jeff@mastodon.social")) };
        var compose = ComposeRows.Open(await built.Opened(), ComposeFor.Edit);

        compose.Walk(-1);

        Assert.Equal(ComposeField.Warning, compose.Typing);
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

        await view.Shell.Drafting.Send();

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

    /// <summary>The Media header's line, drawn <paramref name="height" /> tall.</summary>
    private static Line MediaRow(ComposeScreen compose, int height = Height) =>
        compose.Lines(new Drawing(Width, AShell.Now, Height: height))
               .Single(line => line.Text.StartsWith("  Media", StringComparison.Ordinal));
}
