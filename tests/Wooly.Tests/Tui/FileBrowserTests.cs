using Terminal.Gui.Input;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The file browser (#376): a screen pushed over a compose or a reply by <c>ctrl-o</c>, <c>⏎</c> on the Media header
///     or a click on its words, listing the folders and the files the instance accepts, filtered fuzzily as the author
///     types, and attaching what is chosen exactly as a drop does (#375).
/// </summary>
public class FileBrowserTests : IDisposable
{
    /// <summary>The content panel's inside at an 80 × 24 terminal.</summary>
    private const int Width = 78;

    private readonly TemporaryDirectory _files = new();

    public void Dispose() => _files.Dispose();

    /// <summary><c>ctrl-o</c> on a compose or a reply pushes the browser, crumbed under the screen it attaches to.</summary>
    [Theory]
    [InlineData(ComposeFor.Post)]
    [InlineData(ComposeFor.Reply)]
    public async Task CtrlOOpensTheBrowser(ComposeFor purpose)
    {
        var (shell, _, compose) = await Composing(purpose);

        shell.Press(ShellKey.CtrlO);

        Assert.IsType<FileBrowserScreen>(shell.Screen);
        Assert.Equal([compose.Crumb, "Attach"], shell.Crumbs.TakeLast(2));
    }

    /// <summary>
    ///     The status row offers <c>ctrl-o</c> on a compose or a reply wherever the typing is, as it works there — except on
    ///     the Media header, whose <c>⏎</c> is offered instead for the same thing — and not once the post carries all it
    ///     can, nor on an edit, which takes no attachments.
    /// </summary>
    [Theory]
    [InlineData(ComposeFor.Post)]
    [InlineData(ComposeFor.Reply)]
    public async Task TheStatusRowOffersCtrlOWhereItAdds(ComposeFor purpose)
    {
        var (shell, _, compose) = await Composing(purpose, new PostLimits(500, 23) { Attachments = 1 });

        Assert.Contains(compose.Keys, key => key is { Key: "ctrl-o", Does: "add media" });

        shell.Press(ShellKey.Up);

        Assert.Equal(ComposeField.Media, compose.Typing);
        Assert.DoesNotContain(compose.Keys, key => key.Key == "ctrl-o");
        Assert.Contains(compose.Keys, key => key is { Key: "⏎", Does: "add media" });

        shell.Press(ShellKey.Down);
        Assert.True(shell.Paste(_files.WriteFile("cat.png")));

        Assert.DoesNotContain(compose.Keys, key => key.Key == "ctrl-o");
    }

    /// <summary>An edit offers no <c>ctrl-o</c>: it takes no attachments (#381).</summary>
    [Fact]
    public async Task AnEditOffersNoCtrlO()
    {
        var (_, _, compose) = await Composing(ComposeFor.Edit);

        Assert.DoesNotContain(compose.Keys, key => key.Key == "ctrl-o");
    }

    /// <summary><c>⏎</c> on the Media header, walked to from the post with <c>↑</c>, opens it too.</summary>
    [Fact]
    public async Task EnterOnTheMediaHeaderOpensTheBrowser()
    {
        var (shell, _, compose) = await Composing();

        shell.Press(ShellKey.Up);

        Assert.Equal(ComposeField.Media, compose.Typing);
        Assert.Contains(compose.Keys, key => key is { Key: "⏎", Does: "add media" });

        shell.Press(ShellKey.Up);

        Assert.Equal(ComposeField.Warning, compose.Typing);

        shell.Press(ShellKey.Down);
        shell.Press(ShellKey.Enter);

        Assert.IsType<FileBrowserScreen>(shell.Screen);
    }

    /// <summary>The Media header's words take the highlight while it has the cursor, as the other headers' values do.</summary>
    [Fact]
    public async Task TheMediaHeaderHighlightsItsWordsWhileItHasTheCursor()
    {
        var (shell, _, compose) = await Composing();

        shell.Press(ShellKey.Up);

        var media = compose.Lines(new Drawing(Width, AShell.Now, Height: 21))
                           .Single(line => line.Text.StartsWith("  Media", StringComparison.Ordinal));

        Assert.Equal(Role.SelectedText, Assert.Single(media.Spans, span => span.Text == "none").Role);
        Assert.DoesNotContain("▌", media.Text, StringComparison.Ordinal);
    }

    /// <summary>An edit has nothing to attach to, so <c>ctrl-o</c> opens nothing there.</summary>
    [Fact]
    public async Task AnEditOpensNoBrowser()
    {
        var built = new AShell { Timelines = FakeTimelineReader.Holding(APost.With(account: "jeff@mastodon.social")) };
        var shell = await built.Opened();
        var compose = ComposeRows.Open(shell, ComposeFor.Edit);

        shell.Press(ShellKey.CtrlO);

        Assert.Same(compose, shell.Screen);
    }

    /// <summary>A post that carries all it can opens no browser, and says why.</summary>
    [Fact]
    public async Task AFullPostOpensNoBrowser()
    {
        var (shell, _, compose) = await Composing(limits: PostLimits.Default with { Attachments = 1 });

        shell.Paste(_files.WriteFile("cat.png"));
        shell.Press(ShellKey.CtrlO);

        Assert.Same(compose, shell.Screen);
        Assert.Equal("This post carries all it can — 1 of 1.", shell.Notice);
    }

    /// <summary>
    ///     The first time it opens in the folder Wooly was launched from; from then on, in the folder the author last
    ///     attached from this session — and esc out of it attaches nothing and moves nothing.
    /// </summary>
    [Fact]
    public async Task ItOpensWhereWoolyWasLaunchedThenWhereTheLastAttachCameFrom()
    {
        var holiday = Folder("holiday");
        File.WriteAllText(Path.Combine(holiday, "beach.png"), "sand");
        var (shell, built, _) = await Composing();

        shell.Press(ShellKey.CtrlO);

        Assert.Equal(_files.Path, Browser(shell).Folder);

        shell.Back();
        shell.Press(ShellKey.CtrlO);

        Assert.Equal(_files.Path, Browser(shell).Folder);

        Browser(shell).Pick(At(shell, "holiday/"));
        shell.Press(ShellKey.Right);

        Assert.Equal(holiday, Browser(shell).Folder);

        shell.Press(ShellKey.Enter);

        Assert.Equal(Path.Combine(holiday, "beach.png"), Assert.Single(built.Author.Attaching).Path);

        shell.Press(ShellKey.CtrlO);

        Assert.Equal(holiday, Browser(shell).Folder);
    }

    /// <summary>
    ///     It lists <c>..</c>, the folders and then the files the instance accepts, by name, with the cursor on the first
    ///     entry under <c>..</c> — and nothing hidden.
    /// </summary>
    [Fact]
    public async Task ItListsFoldersAndAcceptedFilesAndNothingHidden()
    {
        Folder("zebra");
        Folder(".git");
        _files.WriteFile("b.png");
        _files.WriteFile("a.mp4");
        _files.WriteFile("notes.txt");
        _files.WriteFile(".secret.png");
        var (shell, _, _) = await Browsing(PostLimits.Default);

        Assert.Equal(["..", "zebra/", "a.mp4", "b.png"], Names(shell));
        Assert.StartsWith("  ◂ ..", Rows(shell)[3], StringComparison.Ordinal);
        Assert.StartsWith(" ▌▸ zebra/", Rows(shell)[4], StringComparison.Ordinal);
        Assert.StartsWith("  ☐ a.mp4", Rows(shell)[5], StringComparison.Ordinal);
        Assert.Contains("video", Rows(shell)[5], StringComparison.Ordinal);
        Assert.Contains("pictures, video, sound · ctrl-a every file", Rows(shell)[1], StringComparison.Ordinal);
    }

    /// <summary>
    ///     Only the types the instance accepts show, until <c>ctrl-a</c> shows every file — hidden ones still never — and
    ///     again puts the accepted ones back.
    /// </summary>
    [Fact]
    public async Task CtrlAShowsEveryFileButNeverAHiddenOne()
    {
        _files.WriteFile("cat.png");
        _files.WriteFile("clip.mp4");
        _files.WriteFile("notes.txt");
        _files.WriteFile(".secret.txt");
        var (shell, _, _) = await Browsing(PostLimits.Default with { MediaTypes = ["image/png"] });

        Assert.Equal(["..", "cat.png"], Names(shell));
        Assert.Contains("pictures · ctrl-a every file", Rows(shell)[1], StringComparison.Ordinal);

        shell.Press(ShellKey.CtrlA);

        Assert.Equal(["..", "cat.png", "clip.mp4", "notes.txt"], Names(shell));
        Assert.Contains("every file · ctrl-a accepted only", Rows(shell)[1], StringComparison.Ordinal);

        shell.Press(ShellKey.CtrlA);

        Assert.Equal(["..", "cat.png"], Names(shell));
    }

    /// <summary>
    ///     Typing filters the way fzf scores: the letters in order anywhere in the name, the closest first — so
    ///     <c>ss0229</c> puts the screenshot of the 29th of February first, <c>0419</c> finds only its own, and the
    ///     cursor lands on the closest match.
    /// </summary>
    [Fact]
    public async Task TypingFiltersFuzzilyClosestFirst()
    {
        _files.WriteFile("Screenshot 2024-04-19 at 9.00.53 AM.png");
        _files.WriteFile("Screenshot 2024-02-29 at 9.31.03 AM.png");
        _files.WriteFile("sunset 2022.png");
        _files.WriteFile("cat.png");
        var (shell, _, _) = await Browsing(PostLimits.Default);

        Type(shell, "ss0229");

        Assert.Equal("Screenshot 2024-02-29 at 9.31.03 AM.png", Names(shell)[1]);
        Assert.StartsWith(" ▌☐ Screenshot 2024-02-29", Rows(shell)[4], StringComparison.Ordinal);
        Assert.StartsWith("  filter ss0229", Rows(shell)[1], StringComparison.Ordinal);

        Clear(shell);
        Type(shell, "0419");

        Assert.Equal(["..", "Screenshot 2024-04-19 at 9.00.53 AM.png"], Names(shell));
    }

    /// <summary>A filter nothing matches says so, rather than leaving a blank list.</summary>
    [Fact]
    public async Task AFilterNothingMatchesSaysSo()
    {
        _files.WriteFile("cat.png");
        var (shell, _, _) = await Browsing(PostLimits.Default);

        Type(shell, "dog");

        Assert.Contains("    nothing here matches \"dog\"", Rows(shell));
    }

    /// <summary>
    ///     <c>space</c> chooses — so a name's spaces never need typing — and again lets go; choices outlast a change of
    ///     filter, and <c>⏎</c> attaches them, in the order chosen, as pending attachments sent up at once.
    /// </summary>
    [Fact]
    public async Task SpaceChoosesSeveralAndEnterAttachesThemInOrder()
    {
        _files.WriteFile("a.png");
        _files.WriteFile("b.png");
        _files.WriteFile("c.png");
        var (shell, built, compose) = await Browsing(PostLimits.Default);

        Browser(shell).Pick(At(shell, "b.png"));
        shell.Press(ShellKey.Space);
        shell.Press(ShellKey.Space);
        Browser(shell).Pick(At(shell, "c.png"));
        shell.Press(ShellKey.Space);
        Type(shell, "a");

        Assert.Equal("a.png", Names(shell)[1]);

        shell.Press(ShellKey.Space);

        Assert.Contains("2 chosen · 2 more fit", Rows(shell)[0], StringComparison.Ordinal);

        shell.Backspace();

        Assert.StartsWith("  ☑ c.png", Rows(shell).Single(row => row.Contains("c.png", StringComparison.Ordinal)), StringComparison.Ordinal);

        shell.Press(ShellKey.Enter);

        Assert.Same(compose, shell.Screen);
        Assert.Equal(["c.png", "a.png"], compose.Attachments.Select(attachment => attachment.Name));
        Assert.Equal(2, built.Author.Attaching.Count);
    }

    /// <summary>Choosing stops at what the post still has room for, counting what is attached already.</summary>
    [Fact]
    public async Task ChoosingStopsAtThePostsRoom()
    {
        var (shell, built, compose) = await Composing(limits: PostLimits.Default with { Attachments = 3 });
        shell.Paste(_files.WriteFile("first.png"));
        _files.WriteFile("a.png");
        _files.WriteFile("b.png");
        _files.WriteFile("c.png");

        shell.Press(ShellKey.CtrlO);
        foreach (var name in new[] { "a.png", "b.png", "c.png" })
        {
            Browser(shell).Pick(At(shell, name));
            shell.Press(ShellKey.Space);
        }

        Assert.Contains("2 chosen · 0 more fit", Rows(shell)[0], StringComparison.Ordinal);

        shell.Press(ShellKey.Enter);

        Assert.Equal(["first.png", "a.png", "b.png"], compose.Attachments.Select(attachment => attachment.Name));
        Assert.Equal(3, built.Author.Attaching.Count);
    }

    /// <summary>
    ///     <c>⏎</c> with files chosen attaches them wherever the cursor is — on <c>..</c> or a folder too, which it would
    ///     otherwise open: what is chosen comes first, then the file under the cursor, then a folder (review of #372).
    /// </summary>
    [Theory]
    [InlineData("..")]
    [InlineData("holiday/")]
    public async Task EnterWithFilesChosenAttachesThemWhereverTheCursorIs(string on)
    {
        Folder("holiday");
        _files.WriteFile("a.png");
        var (shell, _, compose) = await Browsing(PostLimits.Default);

        Browser(shell).Pick(At(shell, "a.png"));
        shell.Press(ShellKey.Space);
        Browser(shell).Pick(At(shell, on));
        shell.Press(ShellKey.Enter);

        Assert.Same(compose, shell.Screen);
        Assert.Equal("a.png", Assert.Single(compose.Attachments).Name);
    }

    /// <summary><c>⏎</c> with nothing chosen attaches the file under the cursor.</summary>
    [Fact]
    public async Task EnterWithNothingChosenAttachesTheFileUnderTheCursor()
    {
        _files.WriteFile("a.png");
        _files.WriteFile("b.png");
        var (shell, _, compose) = await Browsing(PostLimits.Default);

        Browser(shell).Pick(At(shell, "b.png"));
        shell.Press(ShellKey.Enter);

        Assert.Equal("b.png", Assert.Single(compose.Attachments).Name);
    }

    /// <summary>
    ///     <c>⏎</c> or <c>→</c> on a folder opens it, on its first entry; <c>←</c> goes up a folder, landing on the
    ///     folder just left — and either clears the filter.
    /// </summary>
    [Fact]
    public async Task FoldersOpenAndGoUpLandingWhereTheyShould()
    {
        var holiday = Folder("holiday");
        Folder("work");
        File.WriteAllText(Path.Combine(holiday, "beach.png"), "sand");
        File.WriteAllText(Path.Combine(holiday, "pier.png"), "wood");
        var (shell, _, _) = await Browsing(PostLimits.Default);

        Type(shell, "hol");
        shell.Press(ShellKey.Enter);

        Assert.Equal(holiday, Browser(shell).Folder);
        Assert.Equal(["..", "beach.png", "pier.png"], Names(shell));
        Assert.StartsWith(" ▌☐ beach.png", Rows(shell)[4], StringComparison.Ordinal);
        Assert.StartsWith("  type to filter", Rows(shell)[1], StringComparison.Ordinal);

        shell.Press(ShellKey.Left);

        Assert.Equal(_files.Path, Browser(shell).Folder);
        Assert.StartsWith(" ▌▸ holiday/", Rows(shell).Single(row => row.Contains("holiday", StringComparison.Ordinal)), StringComparison.Ordinal);

        Browser(shell).Pick(At(shell, "work/"));
        shell.Press(ShellKey.Right);

        Assert.Equal(Path.Combine(_files.Path, "work"), Browser(shell).Folder);
    }

    /// <summary>
    ///     <c>backspace</c> only ever edits the filter: held down, it stops at empty rather than walking up the folders.
    /// </summary>
    [Fact]
    public async Task BackspaceStopsAtAnEmptyFilter()
    {
        _files.WriteFile("cat.png");
        var (shell, _, _) = await Browsing(PostLimits.Default);

        Type(shell, "ca");
        shell.Backspace();
        shell.Backspace();
        shell.Backspace();
        shell.Backspace();

        Assert.Equal(_files.Path, Browser(shell).Folder);
        Assert.Equal(["..", "cat.png"], Names(shell));
    }

    /// <summary>
    ///     <c>esc</c> clears the filter first, and then goes back to the compose screen as it was: nothing attached,
    ///     the draft as written, and no question asked.
    /// </summary>
    [Fact]
    public async Task EscClearsTheFilterThenGoesBackUnchanged()
    {
        _files.WriteFile("cat.png");
        var (shell, built, compose) = await Composing();

        compose.Rewrite("A cat");
        shell.Press(ShellKey.CtrlO);
        Browser(shell).Pick(At(shell, "cat.png"));
        shell.Press(ShellKey.Space);
        Type(shell, "cat");
        shell.Back();

        Assert.IsType<FileBrowserScreen>(shell.Screen);
        Assert.StartsWith("  type to filter", Rows(shell)[1], StringComparison.Ordinal);

        shell.Back();

        Assert.Same(compose, shell.Screen);
        Assert.Null(shell.Asking);
        Assert.Empty(compose.Attachments);
        Assert.Empty(built.Author.Attaching);
        Assert.Equal("A cat", compose.Text);
    }

    /// <summary>The status row offers the browser's own keys, and esc as clear while there is a filter.</summary>
    [Fact]
    public async Task TheStatusRowOffersTheBrowsersKeys()
    {
        _files.WriteFile("cat.png");
        var (shell, _, _) = await Browsing(PostLimits.Default);

        Assert.Equal(
            ["space:choose", "⏎:attach", "←→:folder", "ctrl-a:every file", "esc:back"],
            shell.Screen.Keys.Select(key => key.ToString()));

        Type(shell, "c");

        Assert.Contains("esc:clear", shell.Screen.Keys.Select(key => key.ToString()));
    }

    /// <summary>In the window, <c>ctrl-o</c> typed in the post's editor opens the browser.</summary>
    [Fact]
    public async Task CtrlOInTheEditorOpensTheBrowser()
    {
        using var drawn = await DrawnShell.Of(80, 24, Themes.Plain, Launched());

        drawn.Shell.Compose();
        drawn.Redraw();
        drawn.Press(Key.O.WithCtrl);

        Assert.IsType<FileBrowserScreen>(drawn.Shell.Screen);
        Assert.Contains(drawn.Rows(), row => row.Contains("type to filter", StringComparison.Ordinal));
    }

    /// <summary>A click on the Media header's words opens the browser.</summary>
    [Fact]
    public async Task AClickOnTheMediaHeadersWordsOpensTheBrowser()
    {
        using var drawn = await DrawnShell.Of(80, 24, Themes.Plain, Launched());

        drawn.Shell.Compose();
        drawn.Redraw();

        var (column, row) = Find(drawn, "none · ctrl-o to add", after: "Media");

        drawn.Click(column + 2, row);

        Assert.IsType<FileBrowserScreen>(drawn.Shell.Screen);
    }

    /// <summary>
    ///     Coming back from the browser, the compose screen's fields are where they were and hold what they held.
    /// </summary>
    [Fact]
    public async Task InTheWindowEscGoesBackToTheDraftAsItWas()
    {
        using var drawn = await DrawnShell.Of(80, 24, Themes.Plain, Launched());

        drawn.Shell.Compose();
        drawn.Redraw();
        drawn.Press(Key.H);
        drawn.Press(Key.I);
        drawn.Press(Key.O.WithCtrl);
        drawn.Press(Key.Esc);

        Assert.IsType<ComposeScreen>(drawn.Shell.Screen);
        Assert.Equal("hi", ((ComposeScreen)drawn.Shell.Screen).Text);
        Assert.Contains(drawn.Rows(), row => row.Contains("hi", StringComparison.Ordinal));
    }

    /// <summary>
    ///     The mouse: a click on a row moves the cursor, a click on its box chooses it, a ctrl-click chooses too, and a
    ///     click on <c>ctrl-a every file</c> shows every file.
    /// </summary>
    [Fact]
    public async Task ClicksMoveTheCursorChooseAndShowEveryFile()
    {
        _files.WriteFile("a.png");
        _files.WriteFile("b.png");
        _files.WriteFile("c.png");
        _files.WriteFile("notes.txt");
        using var drawn = await DrawnShell.Of(80, 24, Themes.Plain, Launched());

        drawn.Shell.Compose();
        drawn.Redraw();
        drawn.Press(Key.O.WithCtrl);

        var (column, row) = Find(drawn, "c.png");

        drawn.Click(column + 1, row);

        Assert.Contains(" ▌☐ c.png", drawn.Rows()[row], StringComparison.Ordinal);

        drawn.Click(column - 2, row);

        Assert.Contains("☑ c.png", drawn.Rows()[row], StringComparison.Ordinal);

        var (aAt, aRow) = Find(drawn, "a.png");

        drawn.Click(aAt + 1, aRow, MouseFlags.Ctrl);

        Assert.Contains("☑ a.png", drawn.Rows()[aRow], StringComparison.Ordinal);

        var (every, everyRow) = Find(drawn, "ctrl-a every file");

        drawn.Click(every + 3, everyRow);

        Assert.Contains(drawn.Rows(), text => text.Contains("notes.txt", StringComparison.Ordinal));
    }

    /// <summary>
    ///     In the window, <c>space</c> chooses the file under the cursor rather than going into the filter, and the arrows
    ///     walk the list (review of #372).
    /// </summary>
    [Fact]
    public async Task InTheWindowSpaceChoosesAndTheArrowsWalk()
    {
        _files.WriteFile("a.png");
        _files.WriteFile("b.png");
        using var drawn = await DrawnShell.Of(80, 24, Themes.Plain, Launched());

        drawn.Shell.Compose();
        drawn.Redraw();
        drawn.Press(Key.O.WithCtrl);
        drawn.Press(Key.Space);

        Assert.Contains(drawn.Rows(), row => row.Contains("▌☑ a.png", StringComparison.Ordinal));
        Assert.Contains(drawn.Rows(), row => row.Contains("type to filter", StringComparison.Ordinal));

        drawn.Press(Key.CursorDown);
        drawn.Press(Key.Space);

        Assert.Contains(drawn.Rows(), row => row.Contains("▌☑ b.png", StringComparison.Ordinal));
        Assert.Contains(drawn.Rows(), row => row.Contains("2 chosen", StringComparison.Ordinal));
    }

    /// <summary>A double click on a file attaches it; on a folder, opens it.</summary>
    [Fact]
    public async Task ADoubleClickOpensAFolderOrAttachesAFile()
    {
        var holiday = Folder("holiday");
        File.WriteAllText(Path.Combine(holiday, "beach.png"), "sand");
        using var drawn = await DrawnShell.Of(80, 24, Themes.Plain, Launched());

        drawn.Shell.Compose();
        drawn.Redraw();
        drawn.Press(Key.O.WithCtrl);

        var (column, row) = Find(drawn, "holiday/");

        drawn.DoubleClick(column + 1, row);

        Assert.Equal(holiday, Assert.IsType<FileBrowserScreen>(drawn.Shell.Screen).Folder);

        (column, row) = Find(drawn, "beach.png");
        drawn.DoubleClick(column + 1, row);

        var compose = Assert.IsType<ComposeScreen>(drawn.Shell.Screen);

        Assert.Equal("beach.png", Assert.Single(compose.Attachments).Name);
    }

    /// <summary>The wheel scrolls a list longer than the terminal.</summary>
    [Fact]
    public async Task TheWheelScrollsTheList()
    {
        for (var at = 0; at < 40; at++)
        {
            _files.WriteFile($"picture {at:00}.png");
        }

        using var drawn = await DrawnShell.Of(80, 24, Themes.Plain, Launched());

        drawn.Shell.Compose();
        drawn.Redraw();
        drawn.Press(Key.O.WithCtrl);

        Assert.DoesNotContain(drawn.Rows(), row => row.Contains("picture 30", StringComparison.Ordinal));

        var (column, row) = Find(drawn, "picture 05");

        for (var notch = 0; notch < 25; notch++)
        {
            drawn.Wheel(column, row);
        }

        Assert.Contains(drawn.Rows(), text => text.Contains("picture 30", StringComparison.Ordinal));
    }

    /// <summary>A shell launched from the test's own folder, as Wooly is launched from wherever the author is.</summary>
    private AShell Launched() => new() { LaunchedFrom = _files.Path };

    /// <summary>A folder in the test's own, made and named.</summary>
    private string Folder(string name) => Directory.CreateDirectory(Path.Combine(_files.Path, name)).FullName;

    /// <summary>A shell opened on a compose screen for <paramref name="purpose" />, launched from the test's folder.</summary>
    private async Task<(Wooly.Tui.Shell.Shell Shell, AShell Built, ComposeScreen Compose)> Composing(
        ComposeFor purpose = ComposeFor.Post,
        PostLimits? limits = null)
    {
        var built = Launched();

        built.Limits = FakeInstanceLimits.Setting(limits);

        var shell = await built.Opened();
        var compose = ComposeRows.Open(shell, purpose);

        built.Host.Drain();

        return (shell, built, compose);
    }

    /// <summary>The browser opened over a fresh compose on an instance setting <paramref name="limits" />.</summary>
    private async Task<(Wooly.Tui.Shell.Shell Shell, AShell Built, ComposeScreen Compose)> Browsing(PostLimits limits)
    {
        var composing = await Composing(limits: limits);

        composing.Shell.Press(ShellKey.CtrlO);

        return composing;
    }

    private static FileBrowserScreen Browser(Wooly.Tui.Shell.Shell shell) => Assert.IsType<FileBrowserScreen>(shell.Screen);

    private static void Type(Wooly.Tui.Shell.Shell shell, string typed)
    {
        foreach (var letter in typed)
        {
            shell.Type(letter);
        }
    }

    private static void Clear(Wooly.Tui.Shell.Shell shell)
    {
        while (Rows(shell)[1].StartsWith("  filter", StringComparison.Ordinal))
        {
            shell.Backspace();
        }
    }

    private static IReadOnlyList<string> Rows(Wooly.Tui.Shell.Shell shell) =>
        [.. shell.Screen.Lines(new Drawing(Width, AShell.Now)).Select(line => line.Text)];

    /// <summary>The names the browser lists, in order, as drawn.</summary>
    private static IReadOnlyList<string> Names(Wooly.Tui.Shell.Shell shell) =>
    [
        .. Rows(shell).Skip(3)
                      .Where(row => row.Length > 4 && row[2] is '◂' or '▸' or '☐' or '☑')
                      .Select(row => row[4..].Split("  ")[0].Trim()),
    ];

    /// <summary>Which entry, counted from <c>..</c>, is named <paramref name="name" />.</summary>
    private static int At(Wooly.Tui.Shell.Shell shell, string name) => Names(shell).ToList().IndexOf(name);

    /// <summary>Where <paramref name="text" /> is drawn in the window, on the first row with it (after <paramref name="after" />).</summary>
    private static (int Column, int Row) Find(DrawnShell drawn, string text, string? after = null)
    {
        var rows = drawn.Rows();

        for (var row = 0; row < rows.Length; row++)
        {
            var from = after is null ? 0 : rows[row].IndexOf(after, StringComparison.Ordinal);
            var column = from < 0 ? -1 : rows[row].IndexOf(text, from, StringComparison.Ordinal);

            if (column >= 0)
            {
                return (column, row);
            }
        }

        throw new InvalidOperationException($"\"{text}\" is not drawn:\n{string.Join('\n', rows)}");
    }
}
