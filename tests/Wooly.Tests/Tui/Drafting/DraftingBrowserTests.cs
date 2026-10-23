using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Drafting;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     The file browser's flow (#376), asked of drafting (ADR-0029, #421): opened over a compose or a reply where it has
///     room, in the folder last attached from this session or else the one Wooly was launched from, walked folder to
///     folder, and what is chosen in it attached to the compose under it exactly as a drop is (#375).
/// </summary>
public class DraftingBrowserTests : IDisposable
{
    private static readonly Post Somebody = APost.With(id: "220", account: "ben@hachyderm.io");

    private readonly TemporaryDirectory _files = new();

    public void Dispose() => _files.Dispose();

    /// <summary><c>ctrl-o</c> on a compose or a reply pushes the browser over it.</summary>
    [Theory]
    [InlineData(ShellKey.C)]
    [InlineData(ShellKey.R)]
    public void CtrlOOpensTheBrowserOverTheCompose(ShellKey opening)
    {
        var drafting = Composing(opening);
        var compose = drafting.Composing;

        Assert.Equal(Drafted.Used, drafting.Press(ShellKey.CtrlO));

        Assert.Equal<Screen>([compose, Assert.IsType<FileBrowserScreen>(drafting.Stack.Top)], drafting.Stack.Screens.Skip(1));
    }

    /// <summary>An edit has nothing to attach to, so <c>ctrl-o</c> opens nothing there.</summary>
    [Fact]
    public void AnEditOpensNoBrowser()
    {
        var drafting = new ADrafting
        {
            Bottom = ADrafting.Home(APost.With(account: "jeff@mastodon.social")),
            Fakes = { LaunchedFrom = _files.Path },
        }.Build();

        drafting.Press(ShellKey.E);
        var compose = drafting.Composing;

        drafting.Press(ShellKey.CtrlO);

        Assert.Same(compose, drafting.Stack.Top);
    }

    /// <summary>A post that carries all it can opens no browser, and says why.</summary>
    [Fact]
    public void AFullPostOpensNoBrowser()
    {
        var drafting = Composing(limits: PostLimits.Default with { Attachments = 1 });
        var compose = drafting.Composing;

        drafting.Drafting.Paste(_files.WriteFile("cat.png"));
        drafting.Press(ShellKey.CtrlO);

        Assert.Same(compose, drafting.Stack.Top);
        Assert.Equal("This post carries all it can — 1 of 1.", drafting.Notice);
    }

    /// <summary>
    ///     The first time it opens in the folder Wooly was launched from; from then on, in the folder the author last
    ///     attached from this session — and leaving it attaches nothing and moves nothing.
    /// </summary>
    [Fact]
    public void ItOpensWhereWoolyWasLaunchedThenWhereTheLastAttachCameFrom()
    {
        var holiday = Folder("holiday");
        File.WriteAllText(Path.Combine(holiday, "beach.png"), "sand");
        var drafting = Composing();

        drafting.Press(ShellKey.CtrlO);

        Assert.Equal(_files.Path, Browser(drafting).Folder);

        drafting.Stack.TakeOff(Browser(drafting));
        drafting.Press(ShellKey.CtrlO);

        Assert.Equal(_files.Path, Browser(drafting).Folder);

        Browser(drafting).Pick(At(drafting, "holiday/"));
        drafting.Press(ShellKey.Right);

        Assert.Equal(holiday, Browser(drafting).Folder);

        drafting.Press(ShellKey.Enter);

        Assert.Equal(Path.Combine(holiday, "beach.png"), Assert.Single(drafting.Fakes.Author.Attaching).Path);

        drafting.Press(ShellKey.CtrlO);

        Assert.Equal(holiday, Browser(drafting).Folder);
    }

    /// <summary>
    ///     <c>space</c> chooses — and again lets go; choices outlast a change of filter, and <c>⏎</c> attaches them, in
    ///     the order chosen, as pending attachments sent up at once.
    /// </summary>
    [Fact]
    public void WhatIsChosenIsAttachedInTheOrderChosen()
    {
        _files.WriteFile("a.png");
        _files.WriteFile("b.png");
        _files.WriteFile("c.png");
        var (drafting, compose) = Browsing(PostLimits.Default);

        Browser(drafting).Pick(At(drafting, "b.png"));
        Choose(drafting);
        Choose(drafting);
        Browser(drafting).Pick(At(drafting, "c.png"));
        Choose(drafting);
        Browser(drafting).Type('a');

        Assert.Equal("a.png", Names(drafting)[1]);

        Choose(drafting);
        Browser(drafting).Backspace();

        Assert.Equal(Drafted.Used, drafting.Press(ShellKey.Enter));

        Assert.Same(compose, drafting.Stack.Top);
        Assert.Equal(["c.png", "a.png"], compose.Media.Attachments.Select(attachment => attachment.Name));
        Assert.Equal(2, drafting.Fakes.Author.Attaching.Count);
    }

    /// <summary>Choosing stops at what the post still has room for, counting what is attached already.</summary>
    [Fact]
    public void ChoosingStopsAtThePostsRoom()
    {
        var drafting = Composing(limits: PostLimits.Default with { Attachments = 3 });
        var compose = drafting.Composing;

        drafting.Drafting.Paste(_files.WriteFile("first.png"));
        _files.WriteFile("a.png");
        _files.WriteFile("b.png");
        _files.WriteFile("c.png");

        drafting.Press(ShellKey.CtrlO);

        foreach (var name in new[] { "a.png", "b.png", "c.png" })
        {
            Browser(drafting).Pick(At(drafting, name));
            Choose(drafting);
        }

        Assert.Contains("2 chosen · 0 more fit", Rows(drafting)[0], StringComparison.Ordinal);

        drafting.Press(ShellKey.Enter);

        Assert.Equal(["first.png", "a.png", "b.png"], compose.Media.Attachments.Select(attachment => attachment.Name));
        Assert.Equal(3, drafting.Fakes.Author.Attaching.Count);
    }

    /// <summary>
    ///     <c>⏎</c> with files chosen attaches them wherever the cursor is — on <c>..</c> or a folder too, which it would
    ///     otherwise open: what is chosen comes first, then the file under the cursor, then a folder (review of #372).
    /// </summary>
    [Theory]
    [InlineData("..")]
    [InlineData("holiday/")]
    public void EnterWithFilesChosenAttachesThemWhereverTheCursorIs(string on)
    {
        Folder("holiday");
        _files.WriteFile("a.png");
        var (drafting, compose) = Browsing(PostLimits.Default);

        Browser(drafting).Pick(At(drafting, "a.png"));
        Choose(drafting);
        Browser(drafting).Pick(At(drafting, on));
        drafting.Press(ShellKey.Enter);

        Assert.Same(compose, drafting.Stack.Top);
        Assert.Equal("a.png", Assert.Single(compose.Media.Attachments).Name);
    }

    /// <summary><c>⏎</c> with nothing chosen attaches the file under the cursor.</summary>
    [Fact]
    public void EnterWithNothingChosenAttachesTheFileUnderTheCursor()
    {
        _files.WriteFile("a.png");
        _files.WriteFile("b.png");
        var (drafting, compose) = Browsing(PostLimits.Default);

        Browser(drafting).Pick(At(drafting, "b.png"));
        drafting.Press(ShellKey.Enter);

        Assert.Equal("b.png", Assert.Single(compose.Media.Attachments).Name);
    }

    /// <summary>
    ///     <c>⏎</c> or <c>→</c> on a folder opens it, on its first entry; <c>←</c> goes up a folder, landing on the
    ///     folder just left — and either clears the filter, and is drawn again.
    /// </summary>
    [Fact]
    public void FoldersOpenAndGoUpLandingWhereTheyShould()
    {
        var holiday = Folder("holiday");
        Folder("work");
        File.WriteAllText(Path.Combine(holiday, "beach.png"), "sand");
        File.WriteAllText(Path.Combine(holiday, "pier.png"), "wood");
        var (drafting, _) = Browsing(PostLimits.Default);

        foreach (var letter in "hol")
        {
            Browser(drafting).Type(letter);
        }

        var changes = drafting.Changes;

        drafting.Press(ShellKey.Enter);

        Assert.Equal(holiday, Browser(drafting).Folder);
        Assert.Equal(["..", "beach.png", "pier.png"], Names(drafting));
        Assert.StartsWith(" ▌☐ beach.png", Rows(drafting)[4], StringComparison.Ordinal);
        Assert.StartsWith("  type to filter", Rows(drafting)[1], StringComparison.Ordinal);
        Assert.Equal(changes + 1, drafting.Changes);

        drafting.Press(ShellKey.Left);

        Assert.Equal(_files.Path, Browser(drafting).Folder);
        Assert.StartsWith(
            " ▌▸ holiday/",
            Rows(drafting).Single(row => row.Contains("holiday", StringComparison.Ordinal)),
            StringComparison.Ordinal);

        Browser(drafting).Pick(At(drafting, "work/"));
        drafting.Press(ShellKey.Right);

        Assert.Equal(Path.Combine(_files.Path, "work"), Browser(drafting).Folder);
    }

    /// <summary>
    ///     Opened from the attachments screen a short terminal lists the rows on (story 58), what is chosen is attached
    ///     to the compose under that, and the attachments screen is back in front.
    /// </summary>
    [Fact]
    public void FromTheAttachmentsScreenWhatIsChosenGoesOnTheComposeUnderIt()
    {
        var drafting = Composing();
        var compose = drafting.Composing;

        drafting.Drafting.Paste(_files.WriteFile("one.png"));
        var listing = new AttachmentsScreen(compose);
        drafting.Stack.Push(listing);
        _files.WriteFile("two.png");

        drafting.Press(ShellKey.CtrlO);
        Browser(drafting).Pick(At(drafting, "two.png"));
        drafting.Press(ShellKey.Enter);

        Assert.Same(listing, drafting.Stack.Top);
        Assert.Equal(["one.png", "two.png"], compose.Media.Attachments.Select(attachment => attachment.Name));
    }

    /// <summary>A folder in the test's own, made and named.</summary>
    private string Folder(string name) => Directory.CreateDirectory(Path.Combine(_files.Path, name)).FullName;

    /// <summary>
    ///     Drafting launched from the test's own folder, with a compose open by <paramref name="opening" /> over
    ///     somebody's post, on an instance setting <paramref name="limits" />.
    /// </summary>
    private ADrafting Composing(ShellKey opening = ShellKey.C, PostLimits? limits = null)
    {
        var drafting = new ADrafting
        {
            Bottom = ADrafting.Home(Somebody),
            Fakes = { LaunchedFrom = _files.Path, Limits = FakeInstanceLimits.Setting(limits) },
        }.Build();

        drafting.Press(opening);
        drafting.Fakes.Host.Drain();

        return drafting;
    }

    /// <summary>
    ///     The browser opened over a fresh compose on an instance setting <paramref name="limits" />, and that compose.
    /// </summary>
    private (ADrafting Drafting, ComposeScreen Compose) Browsing(PostLimits limits)
    {
        var drafting = Composing(limits: limits);
        var compose = drafting.Composing;

        drafting.Press(ShellKey.CtrlO);

        return (drafting, compose);
    }

    private static FileBrowserScreen Browser(ADrafting drafting) => Assert.IsType<FileBrowserScreen>(drafting.Stack.Top);

    /// <summary><c>space</c>, which is the browser's own, answered by it.</summary>
    private static void Choose(ADrafting drafting) => Browser(drafting).Answer(Verb.Choose, drafting.Reach);

    private static IReadOnlyList<string> Rows(ADrafting drafting) =>
        [.. drafting.Stack.Top.Lines(new Drawing(78, AShell.Now)).Select(line => line.Text)];

    /// <summary>The names the browser lists, in order, as drawn.</summary>
    private static IReadOnlyList<string> Names(ADrafting drafting) =>
    [
        .. Rows(drafting).Skip(3)
                         .Where(row => row.Length > 4 && row[2] is '◂' or '▸' or '☐' or '☑')
                         .Select(row => row[4..].Split("  ")[0].Trim()),
    ];

    /// <summary>Which entry, counted from <c>..</c>, is named <paramref name="name" />.</summary>
    private static int At(ADrafting drafting, string name) => Names(drafting).ToList().IndexOf(name);
}
