using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;
using Key = Terminal.Gui.Input.Key;

namespace Wooly.Tests.Tui;

/// <summary>
///     Compose says who a post goes to (ADR-0024, #338): a <b>To</b> header under From, a row of radio buttons with
///     one per visibility, starting on what would go out and sending what it shows.
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

        Assert.Equal("    To  ◂ account default ▸", Texts(compose)[2]);
        Assert.Null(compose.Visibility);
        Assert.Null(Publishing(compose).Visibility);
    }

    /// <summary>
    ///     The arrows walk up from the post through the warning into To, and there <c>→</c> and <c>←</c> move the
    ///     choice — and what is sent is what is shown, as chosen.
    /// </summary>
    [Fact]
    public async Task TheArrowsChangeTheChoiceAndWhatIsSentMatches()
    {
        using var drawn = await Drawn(PostVisibility.Unlisted);
        var compose = Compose(drawn);

        drawn.Press(Key.CursorUp);
        drawn.Press(Key.CursorUp);

        Assert.Equal(ComposeField.To, compose.Typing);

        drawn.Press(Key.CursorRight);

        Assert.Equal(PostVisibility.Followers, compose.Visibility);
        Assert.Contains(drawn.Rows(), row => row.Contains("To  ○ public  ○ unlisted  ● followers  ○ direct", StringComparison.Ordinal));

        drawn.Press(Key.CursorLeft);
        drawn.Press(Key.CursorLeft);

        compose.Text = "hello";

        var draft = Publishing(compose);

        Assert.Equal(PostVisibility.Public, draft.Visibility);
        Assert.True(draft.VisibilityChosen);
    }

    /// <summary>Off either end of the row the arrows go nowhere, and letters typed on To are nobody's.</summary>
    [Fact]
    public async Task TheArrowsStopAtTheEndsAndLettersDoNothing()
    {
        using var drawn = await Drawn(PostVisibility.Public);
        var compose = Compose(drawn);

        drawn.Press(Key.CursorUp);
        drawn.Press(Key.CursorUp);
        drawn.Press(Key.CursorLeft);

        Assert.Equal(PostVisibility.Public, compose.Visibility);

        foreach (var letter in "crb")
        {
            drawn.Press(new Key(letter));
        }

        Assert.Same(compose, drawn.Shell.Screen);
        Assert.Equal(string.Empty, compose.Text);
        Assert.Equal(ComposeField.To, compose.Typing);
    }

    /// <summary>From account default, <c>→</c> chooses the widest and <c>←</c> the narrowest.</summary>
    [Theory]
    [InlineData(true, PostVisibility.Public)]
    [InlineData(false, PostVisibility.Direct)]
    public async Task FromAccountDefaultTheArrowsChooseAnEnd(bool right, PostVisibility chosen)
    {
        using var drawn = await Drawn(null);
        var compose = Compose(drawn);

        drawn.Press(Key.CursorUp);
        drawn.Press(Key.CursorUp);
        drawn.Press(right ? Key.CursorRight : Key.CursorLeft);

        Assert.Equal(chosen, compose.Visibility);
    }

    /// <summary>On To the status row offers the choosing ahead of the walk.</summary>
    [Fact]
    public async Task OnToTheStatusRowOffersChoosing()
    {
        using var drawn = await Drawn(PostVisibility.Public);

        drawn.Press(Key.CursorUp);
        drawn.Press(Key.CursorUp);

        Assert.Contains("Choose: ←→ | Field: ↑↓", drawn.Rows()[^1], StringComparison.Ordinal);
    }

    /// <summary>
    ///     A click on a value chooses it and gives To the typing, and the arrows carry on from there.
    /// </summary>
    [Fact]
    public async Task AClickChoosesAValueAndTheArrowsCarryOnFromThere()
    {
        using var drawn = await Drawn(PostVisibility.Public);
        var compose = Compose(drawn);

        ClickOn(drawn, "○ followers");

        Assert.Equal(PostVisibility.Followers, compose.Visibility);
        Assert.Equal(ComposeField.To, compose.Typing);
        Assert.True(To(drawn).HasFocus);

        drawn.Press(Key.CursorRight);

        Assert.Equal(PostVisibility.Direct, compose.Visibility);

        compose.Text = "hello";

        Assert.True(Publishing(compose).VisibilityChosen);
    }

    /// <summary>
    ///     A reply to a followers-only post opens on followers, with public and unlisted dimmed: neither the keys nor a
    ///     click can choose them, and direct, which is narrower, still can.
    /// </summary>
    [Fact]
    public async Task AReplyToAFollowersPostCannotGoWider()
    {
        var followers = APost.With(id: "220", account: "ben@hachyderm.io", visibility: PostVisibility.Followers);

        using var drawn = await Drawn(PostVisibility.Public, followers, ComposeFor.Reply);
        var compose = Compose(drawn);

        Assert.Equal(PostVisibility.Followers, compose.Visibility);

        var to = Assert.Single(compose.Lines(new Drawing(Width, AShell.Now, Height: Height)), line => line.Text.StartsWith("    To", StringComparison.Ordinal));

        Assert.Equal(Role.Muted, Assert.Single(to.Spans, span => span.Text == "○ public").Role);
        Assert.Equal(Role.Muted, Assert.Single(to.Spans, span => span.Text == "○ unlisted").Role);
        Assert.Equal(Role.Body, Assert.Single(to.Spans, span => span.Text == "● followers").Role);
        Assert.Equal(Role.Body, Assert.Single(to.Spans, span => span.Text == "○ direct").Role);

        ClickOn(drawn, "○ public");

        Assert.Equal(PostVisibility.Followers, compose.Visibility);

        ClickOn(drawn, "● followers");
        drawn.Press(Key.CursorLeft);

        Assert.Equal(PostVisibility.Followers, compose.Visibility);

        drawn.Press(Key.CursorRight);

        Assert.Equal(PostVisibility.Direct, compose.Visibility);

        ClickOn(drawn, "○ unlisted");

        Assert.Equal(PostVisibility.Direct, compose.Visibility);
    }

    /// <summary>A reply to a direct message can only be direct.</summary>
    [Fact]
    public async Task AReplyToADirectMessageCanOnlyBeDirect()
    {
        var direct = APost.With(id: "220", account: "ben@hachyderm.io", visibility: PostVisibility.Direct);

        using var drawn = await Drawn(PostVisibility.Public, direct, ComposeFor.Reply);
        var compose = Compose(drawn);

        Assert.Equal(PostVisibility.Direct, compose.Visibility);

        ClickOn(drawn, "○ followers");
        drawn.Press(Key.CursorLeft);

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

        using var drawn = await Drawn(PostVisibility.Public, mine, ComposeFor.Edit);
        var compose = Compose(drawn);

        Assert.Equal(PostVisibility.Unlisted, compose.Visibility);

        var to = Assert.Single(compose.Lines(new Drawing(Width, AShell.Now, Height: Height)), line => line.Text.StartsWith("    To", StringComparison.Ordinal));

        Assert.Equal("    To  ○ public  ● unlisted  ○ followers  ○ direct", to.Text);
        Assert.All(to.Spans.Where(span => span.Text.Trim().Length > 0 && span.Text != "To"), span => Assert.Equal(Role.Muted, span.Role));

        ClickOn(drawn, "○ followers");

        Assert.Equal(PostVisibility.Unlisted, compose.Visibility);
        Assert.Equal(ComposeField.Post, compose.Typing);

        drawn.Press(Key.CursorUp);
        drawn.Press(Key.CursorUp);

        Assert.True(compose.WritingTheWarning);

        drawn.Press(Key.CursorLeft);
        drawn.Press(Key.CursorRight);

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
    [InlineData("dark", 80)]
    [InlineData("light", 80)]
    [InlineData("dark", 50)]
    public async Task EveryCellOfToIsARoleTheThemeAnswers(string name, int columns)
    {
        var theme = name == "dark" ? Themes.Dark : Themes.Light;
        var followers = APost.With(id: "220", account: "ben@hachyderm.io", visibility: PostVisibility.Followers);

        using var drawn = await Drawn(PostVisibility.Public, followers, ComposeFor.Reply, theme, columns);

        drawn.Press(Key.CursorUp);
        drawn.Press(Key.CursorUp);
        drawn.Redraw();

        var answered = Enum.GetValues<Role>()
                           .SelectMany(role => new[] { theme.For(role), theme.Banded(role) })
                           .ToHashSet();

        Assert.All(drawn.Cells(), cell => Assert.Contains(cell, answered));
    }

    /// <summary>
    ///     With no colour the row still reads: the filled bubble says which is chosen, and while To has the typing the
    ///     chosen one is drawn reversed, as selected text is.
    /// </summary>
    [Fact]
    public async Task WithColourOffTheRowStillReads()
    {
        using var drawn = await Drawn(PostVisibility.Unlisted, theme: Themes.Plain);

        drawn.Press(Key.CursorUp);
        drawn.Press(Key.CursorUp);
        drawn.Redraw();

        var row = ContentClicks.RowOf(drawn, "● unlisted");
        var column = drawn.Rows()[row].IndexOf("● unlisted", StringComparison.Ordinal);

        Assert.Contains("○ public  ● unlisted  ○ followers  ○ direct", drawn.Rows()[row], StringComparison.Ordinal);
        Assert.Equal(Themes.Plain.For(Role.SelectedText), drawn.Cell(row, column));
        Assert.NotEqual(Themes.Plain.For(Role.SelectedText), drawn.Cell(row, column - 3));
    }

    private static void ClickOn(DrawnShell drawn, string text)
    {
        var row = ContentClicks.RowOf(drawn, text);

        drawn.Click(drawn.Rows()[row].IndexOf(text, StringComparison.Ordinal), row);
        drawn.Redraw();
    }

    private static ComposeToField To(DrawnShell drawn) => drawn.Window.SubViews.OfType<ComposeToField>().Single();

    private static async Task<DrawnShell> Drawn(
        PostVisibility? preferred,
        Post? post = null,
        ComposeFor opening = ComposeFor.Post,
        ITheme? theme = null,
        int columns = 80)
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(post ?? APost.With(id: "220", account: "ben@hachyderm.io")),
            Accounts = FakeAccountRelationships.HoldingNobody(),
            DefaultVisibility = preferred,
        };

        var drawn = await DrawnShell.Of(columns, 24, theme ?? Themes.Dark, built);

        ComposeRows.Open(drawn.Shell, opening);
        drawn.Redraw();

        return drawn;
    }

    private static ComposeScreen Compose(DrawnShell drawn) => Assert.IsType<ComposeScreen>(drawn.Shell.Screen);

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
