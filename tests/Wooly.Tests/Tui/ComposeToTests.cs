using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;
using Key = Terminal.Gui.Input.Key;

namespace Wooly.Tests.Tui;

/// <summary>
///     Compose says who a post goes to (ADR-0024, #338): a <b>To</b> header under From, a row of radio buttons with
///     one per visibility, starting on what would go out and sending what it shows. Chosen on in
///     <see cref="Wooly.Tui.Views.ComposeView" /> over a shell (#365).
/// </summary>
public class ComposeToTests
{
    /// <summary>The content panel's inside at an 80 × 20 terminal.</summary>
    private const int Width = 60;

    private const int Height = 17;

    /// <summary>A fresh post opens on the config's <c>default_visibility</c>, and publishes at it.</summary>
    [Fact]
    public async Task AFreshPostOpensOnTheDefaultVisibilityAndPublishesAtIt()
    {
        var compose = await Opening(ComposeFor.Post, PostVisibility.Followers);

        compose.Text = "hello";

        Assert.Equal("    To  ○ public  ○ unlisted  ● followers  ○ direct", Texts(compose)[2]);

        var draft = Publishing(compose);

        Assert.Equal(PostVisibility.Followers, draft.Visibility);
        Assert.False(draft.VisibilityChosen);
    }

    /// <summary>With no preference, To reads "account default" and sends no visibility, as compose always did.</summary>
    [Fact]
    public async Task AFreshPostWithNoPreferenceReadsAccountDefaultAndSendsNone()
    {
        var compose = await Opening(ComposeFor.Post);

        compose.Text = "hello";

        Assert.Equal("    To  ◂ ● account default ▸", Texts(compose)[2]);
        Assert.Null(compose.Visibility);
        Assert.Null(Publishing(compose).Visibility);
    }

    /// <summary>
    ///     The arrows walk up from the post through the warning and Lang into To, and there <c>→</c> and <c>←</c> move the
    ///     choice — and what is sent is what is shown, as chosen.
    /// </summary>
    [Fact]
    public async Task TheArrowsChangeTheChoiceAndWhatIsSentMatches()
    {
        using var view = await Drawn(PostVisibility.Unlisted);
        var compose = view.Compose;

        view.Press(Key.CursorUp);
        view.Press(Key.CursorUp);
        view.Press(Key.CursorUp);

        Assert.Equal(ComposeField.To, compose.Typing);

        view.Press(Key.CursorRight);

        Assert.Equal(PostVisibility.Followers, compose.Visibility);
        Assert.Contains(view.Rows(), row => row.Contains("To  ○ public  ○ unlisted  ● followers  ○ direct", StringComparison.Ordinal));

        view.Press(Key.CursorLeft);
        view.Press(Key.CursorLeft);

        compose.Text = "hello";

        var draft = Publishing(compose);

        Assert.Equal(PostVisibility.Public, draft.Visibility);
        Assert.True(draft.VisibilityChosen);
    }

    /// <summary>Off either end of the row the arrows go nowhere, and letters typed on To are nobody's.</summary>
    [Fact]
    public async Task TheArrowsStopAtTheEndsAndLettersDoNothing()
    {
        using var view = await Drawn(PostVisibility.Public);
        var compose = view.Compose;

        view.Press(Key.CursorUp);
        view.Press(Key.CursorUp);
        view.Press(Key.CursorUp);
        view.Press(Key.CursorLeft);

        Assert.Equal(PostVisibility.Public, compose.Visibility);

        foreach (var letter in "crb")
        {
            view.Press(new Key(letter));
        }

        Assert.Same(compose, view.Shell.Screen);
        Assert.Equal(string.Empty, compose.Text);
        Assert.Equal(ComposeField.To, compose.Typing);
    }

    /// <summary>
    ///     Account default is the first choice on a To that opened on it: <c>→</c> from it chooses the widest, and
    ///     <c>←</c> steps back onto it — after which the post sends no visibility again, as if To had never been touched.
    /// </summary>
    [Fact]
    public async Task TheArrowsStepBackOntoAccountDefaultWhichSendsNone()
    {
        using var view = await Drawn(null);
        var compose = view.Compose;

        view.Press(Key.CursorUp);
        view.Press(Key.CursorUp);
        view.Press(Key.CursorUp);
        view.Press(Key.CursorRight);

        Assert.Equal(PostVisibility.Public, compose.Visibility);

        view.Press(Key.CursorLeft);

        Assert.Null(compose.Visibility);
        Assert.Contains(view.Rows(), row => row.Contains("To  ◂ ● account default ▸", StringComparison.Ordinal));

        view.Press(Key.CursorLeft);

        Assert.Null(compose.Visibility);

        compose.Text = "hello";

        var draft = Publishing(compose);

        Assert.Null(draft.Visibility);
        Assert.False(draft.VisibilityChosen);
    }

    /// <summary>The mouse steps back onto account default too, by the arrow on the narrow row…</summary>
    [Fact]
    public async Task AClickOnTheArrowStepsBackOntoAccountDefault()
    {
        using var view = await Drawn(null);
        var compose = view.Compose;

        view.ClickOn("▸");

        Assert.Equal(PostVisibility.Public, compose.Visibility);

        view.ClickOn("◂");

        Assert.Null(compose.Visibility);

        compose.Text = "hello";

        Assert.Null(Publishing(compose).Visibility);
    }

    /// <summary>…and by its own radio button where the whole row fits.</summary>
    [Fact]
    public async Task AClickOnAccountDefaultChoosesItWhereTheRowFits()
    {
        using var view = await Drawn(null, width: 98);
        var compose = view.Compose;

        Assert.Contains(
            view.Rows(),
            row => row.Contains(
                "To  ● account default  ○ public  ○ unlisted  ○ followers  ○ direct",
                StringComparison.Ordinal));

        view.ClickOn("○ unlisted");

        Assert.Equal(PostVisibility.Unlisted, compose.Visibility);

        view.ClickOn("○ account default");

        Assert.Null(compose.Visibility);

        compose.Text = "hello";

        var draft = Publishing(compose);

        Assert.Null(draft.Visibility);
        Assert.False(draft.VisibilityChosen);
    }

    /// <summary>On To the status row offers the choosing ahead of the walk, which from the top header goes only down.</summary>
    [Fact]
    public async Task OnToTheStatusRowOffersChoosing()
    {
        using var view = await Drawn(PostVisibility.Public);

        view.Press(Key.CursorUp);
        view.Press(Key.CursorUp);
        view.Press(Key.CursorUp);

        Assert.Contains("Choose: ←→ | Field: ↓ ", view.Status(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     A click on a value chooses it and gives To the typing, and the arrows carry on from there.
    /// </summary>
    [Fact]
    public async Task AClickChoosesAValueAndTheArrowsCarryOnFromThere()
    {
        using var view = await Drawn(PostVisibility.Public);
        var compose = view.Compose;

        view.ClickOn("○ followers");

        Assert.Equal(PostVisibility.Followers, compose.Visibility);
        Assert.Equal(ComposeField.To, compose.Typing);
        Assert.True(view.To.HasFocus);

        view.Press(Key.CursorRight);

        Assert.Equal(PostVisibility.Direct, compose.Visibility);

        compose.Text = "hello";

        Assert.True(Publishing(compose).VisibilityChosen);
    }

    /// <summary>
    ///     A reply to a post for followers opens on followers, with public and unlisted dimmed: neither the keys nor a
    ///     click can choose them, and direct, which is narrower, still can.
    /// </summary>
    [Fact]
    public async Task AReplyToAFollowersPostCannotGoWider()
    {
        var followers = APost.With(id: "220", account: "ben@hachyderm.io", visibility: PostVisibility.Followers);

        using var view = await Drawn(PostVisibility.Public, followers, ComposeFor.Reply);
        var compose = view.Compose;

        Assert.Equal(PostVisibility.Followers, compose.Visibility);

        var to = Assert.Single(compose.Lines(new Drawing(Width, AShell.Now, Height: Height)), line => line.Text.StartsWith("    To", StringComparison.Ordinal));

        Assert.Equal(Role.Muted, Assert.Single(to.Spans, span => span.Text == "○ public").Role);
        Assert.Equal(Role.Muted, Assert.Single(to.Spans, span => span.Text == "○ unlisted").Role);
        Assert.Equal(Role.Body, Assert.Single(to.Spans, span => span.Text == "● followers").Role);
        Assert.Equal(Role.Body, Assert.Single(to.Spans, span => span.Text == "○ direct").Role);

        view.ClickOn("○ public");

        Assert.Equal(PostVisibility.Followers, compose.Visibility);

        view.ClickOn("● followers");
        view.Press(Key.CursorLeft);

        Assert.Equal(PostVisibility.Followers, compose.Visibility);

        view.Press(Key.CursorRight);

        Assert.Equal(PostVisibility.Direct, compose.Visibility);

        view.ClickOn("○ unlisted");

        Assert.Equal(PostVisibility.Direct, compose.Visibility);
    }

    /// <summary>A reply to a direct message can only be direct.</summary>
    [Fact]
    public async Task AReplyToADirectMessageCanOnlyBeDirect()
    {
        var direct = APost.With(id: "220", account: "ben@hachyderm.io", visibility: PostVisibility.Direct);

        using var view = await Drawn(PostVisibility.Public, direct, ComposeFor.Reply);
        var compose = view.Compose;

        Assert.Equal(PostVisibility.Direct, compose.Visibility);

        view.ClickOn("○ followers");
        view.Press(Key.CursorLeft);

        Assert.Equal(PostVisibility.Direct, compose.Visibility);
        Assert.All(
            Enum.GetValues<PostVisibility>().Where(visibility => visibility != PostVisibility.Direct),
            visibility => Assert.False(compose.Allows(visibility)));

        compose.Text += "hello";

        Assert.Equal(PostVisibility.Direct, Publishing(compose).Visibility);
    }

    /// <summary>A preference wider than the post being answered opens narrowed, and goes out unchosen.</summary>
    [Fact]
    public async Task AReplyOpensOnTheNarrowerOfThePreferenceAndThePost()
    {
        var unlisted = APost.With(id: "220", account: "ben@hachyderm.io", visibility: PostVisibility.Unlisted);

        var compose = await Opening(ComposeFor.Reply, PostVisibility.Public, unlisted);

        compose.Text += "hello";

        Assert.Equal(PostVisibility.Unlisted, Publishing(compose).Visibility);
        Assert.False(Publishing(compose).VisibilityChosen);
    }

    /// <summary>
    ///     An edit shows the post's own visibility, dimmed, and neither keys nor clicks change it or give To the typing.
    /// </summary>
    [Fact]
    public async Task AnEditShowsThePostsVisibilityReadOnly()
    {
        var mine = APost.With(id: "110", account: "jeff@mastodon.social", visibility: PostVisibility.Unlisted);

        using var view = await Drawn(PostVisibility.Public, mine, ComposeFor.Edit);
        var compose = view.Compose;

        Assert.Equal(PostVisibility.Unlisted, compose.Visibility);

        var to = Assert.Single(compose.Lines(new Drawing(Width, AShell.Now, Height: Height)), line => line.Text.StartsWith("    To", StringComparison.Ordinal));

        Assert.Equal("    To  ○ public  ● unlisted  ○ followers  ○ direct", to.Text);
        Assert.All(to.Spans.Where(span => span.Text.Trim().Length > 0 && span.Text != "To"), span => Assert.Equal(Role.Muted, span.Role));

        view.ClickOn("○ followers");

        Assert.Equal(PostVisibility.Unlisted, compose.Visibility);
        Assert.Equal(ComposeField.Post, compose.Typing);

        view.Press(Key.CursorUp);
        view.Press(Key.CursorUp);
        view.Press(Key.CursorUp);

        Assert.Equal(ComposeField.Lang, compose.Typing);

        view.Press(Key.CursorLeft);
        view.Press(Key.CursorRight);

        Assert.Equal(PostVisibility.Unlisted, compose.Visibility);
        Assert.IsType<Outgoing.Saving>(compose.Outgoing);
    }

    /// <summary>
    ///     Where the whole row does not fit, To falls back to the one value chosen with an arrow either side — and the
    ///     arrows take a click as a step.
    /// </summary>
    [Fact]
    public async Task WhereTheRowDoesNotFitToShowsOneValueBetweenArrows()
    {
        var compose = await Opening(ComposeFor.Post, PostVisibility.Followers);
        var narrow = compose.Lines(new Drawing(40, AShell.Now, Height: Height));

        Assert.Equal("    To  ◂ ● followers ▸", narrow[2].Text);
        Assert.All(narrow[2].Spans.Where(span => span.Text is "◂" or "▸"), span => Assert.Equal(Role.Muted, span.Role));

        var room = compose.ToAt(new System.Drawing.Size(40, Height)).Width;

        compose.ClickTo("◂ ● followers ".Length, room);

        Assert.Equal(PostVisibility.Direct, compose.Visibility);
        Assert.Equal(ComposeField.To, compose.Typing);

        compose.ClickTo(0, room);

        Assert.Equal(PostVisibility.Followers, compose.Visibility);
    }

    /// <summary>
    ///     On the shortest terminal To and the warning are still drawn — and narrow as well as short, To still shows
    ///     what is chosen.
    /// </summary>
    [Fact]
    public async Task AtTheShortestHeightsToAndTheWarningAreStillDrawn()
    {
        var compose = await Opening(ComposeFor.Post, PostVisibility.Followers);
        var rows = compose.Lines(new Drawing(40, AShell.Now, Height: 5)).Select(line => line.Text).ToList();

        Assert.Equal(["    To  ◂ ● followers ▸", ComposeRows.NoWarning[..rows[1].Length], string.Empty, string.Empty, string.Empty], rows);
    }

    /// <summary>
    ///     Every cell of To is a role the theme answers — dimmed values, the chosen one while To has the typing, and the
    ///     arrows of the narrow form alike.
    /// </summary>
    [Theory]
    [InlineData("dark", ComposedView.Width)]
    [InlineData("light", ComposedView.Width)]
    [InlineData("dark", 28)]
    public async Task EveryCellOfToIsARoleTheThemeAnswers(string name, int width)
    {
        var theme = name == "dark" ? Themes.Dark : Themes.Light;
        var followers = APost.With(id: "220", account: "ben@hachyderm.io", visibility: PostVisibility.Followers);

        using var view = await Drawn(PostVisibility.Public, followers, ComposeFor.Reply, theme, width);

        view.Press(Key.CursorUp);
        view.Press(Key.CursorUp);
        view.Press(Key.CursorUp);
        view.Redraw();

        var answered = Enum.GetValues<Role>()
                           .SelectMany(role => new[] { theme.For(role), theme.Banded(role) })
                           .ToHashSet();

        Assert.All(view.Cells(), cell => Assert.Contains(cell, answered));
    }

    /// <summary>
    ///     With no colour the row still reads: the filled bubble says which is chosen, and while To has the typing the
    ///     chosen one is drawn reversed, as selected text is.
    /// </summary>
    [Fact]
    public async Task WithColourOffTheRowStillReads()
    {
        using var view = await Drawn(PostVisibility.Unlisted, theme: Themes.Plain);

        view.Press(Key.CursorUp);
        view.Press(Key.CursorUp);
        view.Press(Key.CursorUp);
        view.Redraw();

        var at = view.To.FrameToScreen();
        var column = at.X + view.Shown(view.To).IndexOf("● unlisted", StringComparison.Ordinal);

        Assert.StartsWith("○ public  ● unlisted  ○ followers  ○ direct", view.Shown(view.To), StringComparison.Ordinal);
        Assert.Equal(Themes.Plain.For(Role.SelectedText), view.Cell(at.Y, column));
        Assert.NotEqual(Themes.Plain.For(Role.SelectedText), view.Cell(at.Y, column - 3));
    }

    /// <summary>Compose's fields over a compose opened for <paramref name="opening" />, <paramref name="width" /> wide.</summary>
    private static Task<ComposedView> Drawn(
        PostVisibility? preferred,
        Post? post = null,
        ComposeFor opening = ComposeFor.Post,
        ITheme? theme = null,
        int width = ComposedView.Width) =>
        ComposedView.Of(
            new AShell
            {
                Timelines = FakeTimelineReader.Holding(post ?? APost.With(id: "220", account: "ben@hachyderm.io")),
                Accounts = FakeAccountRelationships.HoldingNobody(),
                DefaultVisibility = preferred,
            },
            opening,
            width,
            theme: theme);

    private static PostDraft Publishing(ComposeScreen compose) =>
        Assert.IsType<Outgoing.Publishing>(compose.Outgoing).Draft;

    private static IReadOnlyList<Line> Lines(ComposeScreen compose) =>
        compose.Lines(new Drawing(Width, AShell.Now, Height: Height));

    private static IReadOnlyList<string> Texts(ComposeScreen compose) => [.. Lines(compose).Select(line => line.Text)];

    private static async Task<ComposeScreen> Opening(
        ComposeFor opening,
        PostVisibility? preferred = null,
        Post? post = null)
    {
        var shell = await new AShell
        {
            Timelines = FakeTimelineReader.Holding(post ?? APost.With(id: "220", account: "ben@hachyderm.io")),
            DefaultVisibility = preferred,
        }.Opened();

        return ComposeRows.Open(shell, opening);
    }
}
