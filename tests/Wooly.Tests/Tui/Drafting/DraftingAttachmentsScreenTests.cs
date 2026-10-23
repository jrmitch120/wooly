using Wooly.Core.Errors;
using Wooly.Tests.Fakes;
using Wooly.Tui.Drafting;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     The attachments screen (story 58), asked of drafting (ADR-0029, #421): on a terminal too short for a row each,
///     the rows fold into the Media header's line, and <c>⏎</c> on it pushes a screen listing them — whose keys change
///     the compose under it as their rows under the header do, so that nothing attached is out of reach on a short
///     terminal (review of #372). Leaving it and the pointer on it are <see cref="ComposeAttachmentsScreenTests" />'.
/// </summary>
public class DraftingAttachmentsScreenTests : IDisposable
{
    /// <summary>The content panel's inside at an 80-column terminal.</summary>
    private const int Width = 78;

    /// <summary>Tall enough for three rows under the header with three left to write in, as the fold test has it.</summary>
    private const int Roomy = 16;

    /// <summary>A row short of that, which folds them.</summary>
    private const int Short = 15;

    private readonly TemporaryDirectory _files = new();

    public void Dispose() => _files.Dispose();

    /// <summary>
    ///     <c>⏎</c> on the folded header pushes the attachments screen over the compose, listing the header's
    ///     line unfolded and a row each — the same rows the header draws when there is room, the first one picked.
    /// </summary>
    [Fact]
    public void EnterOnTheFoldedHeaderListsTheRows()
    {
        var (drafting, compose) = Folded("one.png", "two.png", "three.png");

        drafting.Press(ShellKey.Enter);

        var listed = Assert.IsType<AttachmentsScreen>(drafting.Stack.Top);
        var rows = Texts(listed);

        Assert.Same(compose, listed.Compose);
        Assert.Equal<Screen>([compose, listed], drafting.Stack.Screens.Skip(1));
        Assert.Contains("  Media  3 of 4 · ctrl-o to add · □ sensitive", rows);
        Assert.StartsWith("    ▌⠶     one.png", Row(rows, "one.png"), StringComparison.Ordinal);
        Assert.StartsWith("     ⠶     two.png", Row(rows, "two.png"), StringComparison.Ordinal);
        Assert.Equal(
            Row(Texts(compose, Roomy), "three.png").TrimEnd(),
            Row(rows, "three.png").TrimEnd());
    }
    /// <summary>With room for the rows, <c>⏎</c> on the header still opens the file browser.</summary>
    [Fact]
    public void EnterOnTheUnfoldedHeaderStillBrowses()
    {
        var (drafting, compose) = Composing("one.png");

        _ = Texts(compose, Roomy);
        drafting.Press(ShellKey.Up);
        drafting.Press(ShellKey.Up);
        drafting.Press(ShellKey.Enter);

        Assert.IsType<FileBrowserScreen>(drafting.Stack.Top);
    }
    /// <summary>
    ///     The row keys work there as under the header: the arrows walk, <c>del</c> takes one off and <c>ctrl-z</c>
    ///     brings it back, <c>shift-↓</c> moves it later, and <c>s</c> toggles sensitive — each on the compose's own
    ///     draft, which is what is sent.
    /// </summary>
    [Fact]
    public void TheRowKeysWorkThere()
    {
        var (drafting, compose) = Folded("one.png", "two.png", "three.png");

        drafting.Press(ShellKey.Enter);
        drafting.Stack.Top.Move(1);

        Assert.StartsWith("    ▌⠶     two.png", Row(Texts(drafting.Stack.Top), "two.png"), StringComparison.Ordinal);

        drafting.Press(ShellKey.Delete);

        Assert.Equal(["one.png", "three.png"], Names(compose));

        drafting.Press(ShellKey.CtrlZ);

        Assert.Equal(["one.png", "two.png", "three.png"], Names(compose));

        drafting.Press(ShellKey.ShiftDown);

        Assert.Equal(["one.png", "three.png", "two.png"], Names(compose));
        Assert.Equal(["one.png", "three.png", "two.png"], Names(Texts(drafting.Stack.Top)));

        drafting.Press(ShellKey.S);

        Assert.True(compose.Sensitive);
        Assert.Contains("  Media  3 of 4 · ctrl-o to add · ■ sensitive", Texts(drafting.Stack.Top));
    }
    /// <summary><c>r</c> there sends a dropped connection up again.</summary>
    [Fact]
    public void RRetriesThere()
    {
        var (drafting, _) = Folded("one.png", "two.png", "three.png");

        drafting.Fakes.Author.Attaching[0].Refuse(Dropped());
        drafting.Fakes.Host.Drain();
        drafting.Press(ShellKey.Enter);
        drafting.Press(ShellKey.R);

        Assert.Equal(4, drafting.Fakes.Author.Attaching.Count);
        Assert.Equal(drafting.Fakes.Author.Attaching[0].Path, drafting.Fakes.Author.Attaching[3].Path);
    }
    /// <summary>
    ///     <c>ctrl-o</c> there opens the file browser, and what it attaches lands on the compose under the list, listed
    ///     there as it comes back.
    /// </summary>
    [Fact]
    public void CtrlOAttachesMoreFromThere()
    {
        var (drafting, compose) = Folded("one.png", "two.png", "three.png");

        _files.WriteFile("four.png");
        drafting.Press(ShellKey.Enter);
        drafting.Press(ShellKey.CtrlO);

        var browser = Assert.IsType<FileBrowserScreen>(drafting.Stack.Top);

        browser.Pick(1);
        drafting.Press(ShellKey.Enter);

        Assert.IsType<AttachmentsScreen>(drafting.Stack.Top);
        Assert.Equal(["one.png", "two.png", "three.png", "four.png"], Names(compose));
        Assert.Contains(Texts(drafting.Stack.Top), row => row.Contains("four.png", StringComparison.Ordinal));
    }
    /// <summary>A connection that dropped while a file was going up, the one failure a retry can mend.</summary>
    private static TransientNetworkException Dropped() =>
        new(new Uri("https://mastodon.social/api/v2/media"), 3, new HttpRequestException());

    /// <summary>
    ///     A compose with <paramref name="names" /> dropped onto it, drawn short enough that their rows fold, and the walk
    ///     on the Media header the rows folded into.
    /// </summary>
    private (ADrafting Drafting, ComposeScreen Compose) Folded(params string[] names)
    {
        var composing = Composing(names);

        Assert.DoesNotContain(Texts(composing.Compose, Short), row => row.Contains(names[0], StringComparison.Ordinal));

        Assert.Equal(Drafted.Used, composing.Drafting.Press(ShellKey.Up));
        Assert.Equal(ComposeField.Media, composing.Compose.Typing);

        return composing;
    }

    /// <summary>
    ///     Drafting launched from the test's own folder, with <paramref name="names" /> dropped onto a fresh compose.
    /// </summary>
    private (ADrafting Drafting, ComposeScreen Compose) Composing(params string[] names)
    {
        var drafting = new ADrafting { Fakes = { LaunchedFrom = _files.Path } }.Build();

        drafting.Press(ShellKey.C);
        drafting.Fakes.Host.Drain();

        var compose = drafting.Composing;

        foreach (var name in names)
        {
            Assert.True(drafting.Drafting.Paste(_files.WriteFile(name)));
        }

        return (drafting, compose);
    }

    private static IEnumerable<string> Names(ComposeScreen compose) =>
        compose.Media.Attachments.Select(attachment => attachment.Name);

    /// <summary>The names on the listed rows, top to bottom.</summary>
    private static IEnumerable<string> Names(IReadOnlyList<string> rows) =>
        rows.Where(row => row.Contains('⠶', StringComparison.Ordinal))
            .Select(row => row.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1]);

    private static string Row(IReadOnlyList<string> rows, string name) =>
        rows.Single(row => row.Contains(name, StringComparison.Ordinal));

    private static IReadOnlyList<string> Texts(Screen screen, int height = Roomy) =>
        [.. screen.Lines(new Drawing(Width, AShell.Now, Height: height)).Select(line => line.Text)];
}
