using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;
using Key = Terminal.Gui.Input.Key;

namespace Wooly.Tests.Tui;

/// <summary>
///     Compose says what language a post is in (ADR-0024, #340): a <b>Lang</b> header under To, a one-line field with
///     the shared list under it, starting on the author's own language and sending what it holds. The field and the
///     list under it are driven in <see cref="ComposeView" /> over a shell, which owns both (#365, #366).
/// </summary>
public class ComposeLangTests
{
    /// <summary>The content panel's inside at an 80 × 20 terminal.</summary>
    private const int Width = 60;

    private const int Height = 17;

    /// <summary>A fresh post opens on <c>default_language</c>, shown as its code and own name, and publishes in it.</summary>
    [Fact]
    public async Task AFreshPostOpensOnTheDefaultLanguageAndPublishesInIt()
    {
        var compose = await Opening(ComposeFor.Post, preferred: "fr", account: "de");

        compose.Text = "bonjour";

        Assert.Equal("   Lang  fr  Français", Texts(compose)[3]);
        Assert.Equal("fr", Publishing(compose).Language);
    }

    /// <summary>With no preference, Lang opens on the account's posting language.</summary>
    [Fact]
    public async Task WithNoPreferenceAFreshPostOpensOnTheAccountsLanguage()
    {
        var compose = await Opening(ComposeFor.Post, account: "de");

        compose.Text = "hallo";

        Assert.Equal("   Lang  de  Deutsch", Texts(compose)[3]);
        Assert.Equal("de", Publishing(compose).Language);
    }

    /// <summary>With neither, Lang opens empty and the post goes out with no language, left to the instance.</summary>
    [Fact]
    public async Task WithNeitherLangIsEmptyAndSendsNoLanguage()
    {
        var compose = await Opening(ComposeFor.Post);

        compose.Text = "hello";

        Assert.StartsWith("   Lang  ", Texts(compose)[3], StringComparison.Ordinal);
        Assert.DoesNotContain("   Lang  ", Texts(compose)[3][8..], StringComparison.Ordinal);
        Assert.Null(Publishing(compose).Language);
    }

    /// <summary>A reply to a post in another language opens on the author's own, not the answered post's.</summary>
    [Fact]
    public async Task AReplyOpensOnTheAuthorsOwnLanguage()
    {
        var german = APost.With(id: "220", account: "ben@hachyderm.io") with { Language = "de" };

        var compose = await Opening(ComposeFor.Reply, preferred: "en", post: german);

        compose.Text += "hello";

        Assert.Equal("en", Publishing(compose).Language);
    }

    /// <summary>An edit opens on the post's own language whatever the preference, and sends it back.</summary>
    [Fact]
    public async Task AnEditOpensOnThePostsOwnLanguage()
    {
        var mine = APost.With(id: "110", account: "jeff@mastodon.social") with { Language = "de" };

        var compose = await Opening(ComposeFor.Edit, preferred: "fr", post: mine);

        Assert.Equal("   Lang  de  Deutsch", Texts(compose)[3]);
        Assert.Equal("de", Saving(compose).Language);
    }

    /// <summary>
    ///     <c>↑</c> from the post walks the headers in the order they are drawn — the warning, Lang, To — and <c>↓</c>
    ///     walks back down through Lang into the post.
    /// </summary>
    [Fact]
    public async Task TheArrowsWalkThroughLangInTheOrderItIsDrawn()
    {
        using var view = await ComposedView.Of(
            new AShell { Accounts = FakeAccountRelationships.HoldingNobody(), DefaultLanguage = "fr" });
        var compose = view.Compose;

        view.Press(Key.CursorUp);
        Assert.Equal(ComposeField.Warning, compose.Typing);

        view.Press(Key.CursorUp);
        Assert.Equal(ComposeField.Lang, compose.Typing);
        Assert.True(view.Lang.HasFocus);

        view.Press(Key.CursorUp);
        Assert.Equal(ComposeField.To, compose.Typing);

        view.Press(Key.CursorDown);
        Assert.Equal(ComposeField.Lang, compose.Typing);

        view.Press(Key.CursorDown);
        view.Press(Key.CursorDown);
        Assert.Equal(ComposeField.Post, compose.Typing);
        Assert.Equal("fr  Français", view.Lang.Text);
    }

    /// <summary>
    ///     Lang sits in its header's value column, opens on what the draft holds, and shows it there in its own name —
    ///     not a list of languages, which only typing in Lang opens.
    /// </summary>
    [Fact]
    public async Task LangOpensOnTheDraftsLanguageInItsValueColumn()
    {
        using var view = await ComposedView.Of(new AShell { DefaultLanguage = "de" });
        var at = view.Lang.FrameToScreen();

        Assert.Equal("Lang  ", view.Rows()[at.Y].Substring(at.X - 6, 6));
        Assert.StartsWith("de  Deutsch", view.Shown(view.Lang), StringComparison.Ordinal);
        Assert.Equal("de", view.Compose.Lang.Language?.Code);
        Assert.Equal(ComposeField.Post, view.Compose.Typing);
    }

    /// <summary>
    ///     A code, the start of a code, or part of a name typed into Lang opens the list on French, rows written as its
    ///     code and own name; <c>tab</c> picks it, Lang says <c>fr  Français</c>, and the post goes out in French.
    /// </summary>
    [Theory]
    [InlineData("fr")]
    [InlineData("fre")]
    [InlineData("Fran")]
    public async Task TypingACodeOrANameFiltersTheListAndTabPicksIt(string typed)
    {
        using var drawn = await Drawn();
        var compose = Compose(drawn);

        OnLang(drawn);
        Type(drawn, typed);

        Assert.True(List(drawn).Visible);
        Assert.StartsWith("│▌ fr  Français", ListRows(drawn)[1], StringComparison.Ordinal);

        drawn.Press(Key.Tab);

        Assert.False(List(drawn).Visible);
        Assert.Equal("fr  Français", Field(drawn).Text);
        Assert.Contains(drawn.Rows(), row => row.Contains("Lang  fr  Français", StringComparison.Ordinal));
        Assert.Equal(ComposeField.Lang, compose.Typing);

        compose.Text = "bonjour";

        Assert.Equal("fr", Publishing(compose).Language);
    }

    /// <summary>
    ///     The list's keys are the mention list's: <c>↑</c>/<c>↓</c> move the pick, <c>⏎</c> picks, and <c>esc</c>
    ///     closes the list and leaves the draft where it was.
    /// </summary>
    [Fact]
    public async Task TheListsKeysWorkAsTheMentionListsDo()
    {
        using var drawn = await Drawn();
        var compose = Compose(drawn);

        compose.Text = "a draft";
        OnLang(drawn);
        Type(drawn, "Fran");
        drawn.Press(Key.CursorDown);

        Assert.StartsWith("│▌ lfn", ListRows(drawn)[2], StringComparison.Ordinal);

        drawn.Press(Key.CursorUp);
        drawn.Press(Key.Esc);

        Assert.False(List(drawn).Visible);
        Assert.Same(compose, drawn.Shell.Screen);
        Assert.Equal("Fran", Field(drawn).Text);
        Assert.Equal(ComposeField.Lang, compose.Typing);

        Field(drawn).Text = string.Empty;
        Type(drawn, "de");
        drawn.Press(Key.Enter);

        Assert.Equal("de  Deutsch", Field(drawn).Text);
        Assert.Equal("de", Publishing(compose).Language);
    }

    /// <summary>
    ///     A compose opening on a language fills Lang with it and opens no list: only typing in Lang does, and the
    ///     typing opens in the post.
    /// </summary>
    [Theory]
    [InlineData("de", "de  Deutsch")]
    [InlineData("eng", "eng")]
    public async Task OpeningOnALanguageOpensNoList(string code, string held)
    {
        var mine = APost.With(id: "110", account: "jeff@mastodon.social") with { Language = code };

        using var drawn = await Drawn(post: mine, opening: ComposeFor.Edit);

        Assert.Equal(held, Field(drawn).Text);
        Assert.False(List(drawn).Visible);
        Assert.Equal(ComposeField.Post, Compose(drawn).Typing);
        Assert.DoesNotContain("Pick: ↑↓", drawn.Status(), StringComparison.Ordinal);
    }

    /// <summary>While the list is open, the status row offers its keys.</summary>
    [Fact]
    public async Task TheStatusRowOffersTheListsKeysWhileItIsOpen()
    {
        using var drawn = await Drawn();

        OnLang(drawn);

        Assert.DoesNotContain("Pick: ↑↓", drawn.Status(), StringComparison.Ordinal);

        Type(drawn, "fr");

        Assert.Contains("Pick: ↑↓ | Choose: tab | Close: esc", drawn.Status(), StringComparison.Ordinal);

        drawn.Press(Key.Esc);

        Assert.DoesNotContain("Pick: ↑↓", drawn.Status(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     A click on Lang opens the list and gives Lang the typing, the wheel moves the pick down it, and a click on a
    ///     row picks that language.
    /// </summary>
    [Fact]
    public async Task AClickOpensTheListTheWheelScrollsItAndAClickPicks()
    {
        using var drawn = await Drawn();
        var compose = Compose(drawn);

        ClickOn(drawn, "Lang");

        Assert.True(List(drawn).Visible);
        Assert.Equal(ComposeField.Lang, compose.Typing);

        var at = List(drawn).FrameToScreen();

        Assert.StartsWith("│▌ aa  Afaraf", ListRows(drawn)[1], StringComparison.Ordinal);

        drawn.Wheel(at.X + 4, at.Y + 1);

        Assert.StartsWith("│▌", ListRows(drawn)[2], StringComparison.Ordinal);

        var second = ListRows(drawn)[2][3..].Trim('│', ' ');

        drawn.Click(at.X + 4, at.Y + 2);

        Assert.False(List(drawn).Visible);
        Assert.Equal("ab  аҧсуа бызшәа", second);
        Assert.Equal(second, Field(drawn).Text);
        Assert.Equal("ab", compose.Lang.Language?.Code);
    }

    /// <summary>Sending with the list open leaves compose and closes it, so the next compose opens on no list.</summary>
    [Fact]
    public async Task LeavingComposeClosesTheList()
    {
        using var drawn = await Drawn();

        Compose(drawn).Text = "bonjour";
        OnLang(drawn);
        Type(drawn, "fr");

        Assert.True(List(drawn).Visible);

        drawn.Press(Key.S.WithCtrl);
        drawn.Settle();

        Assert.IsNotType<ComposeScreen>(drawn.Shell.Screen);

        ComposeRows.Open(drawn.Shell, ComposeFor.Post);
        drawn.Redraw();

        Assert.False(List(drawn).Visible);
        Assert.False(Compose(drawn).OfferingLanguages);
        Assert.DoesNotContain("Pick: ↑↓", drawn.Status(), StringComparison.Ordinal);
    }

    /// <summary>A click outside the open list closes it, and Lang keeps what it held.</summary>
    [Fact]
    public async Task AClickOutsideTheListClosesItUnchanged()
    {
        using var drawn = await Drawn(preferred: "de");
        var compose = Compose(drawn);

        ClickOn(drawn, "Lang");

        Assert.True(List(drawn).Visible);

        ClickOn(drawn, "From");

        Assert.False(List(drawn).Visible);
        Assert.Equal("de  Deutsch", Field(drawn).Text);

        compose.Text = "hallo";

        Assert.Equal("de", Publishing(compose).Language);
    }

    /// <summary>An edit changed to another language sends the new one; cleared, it sends the clearing.</summary>
    [Theory]
    [InlineData("fr", "fr")]
    [InlineData("", "")]
    public async Task AnEditSendsWhatLangHolds(string typed, string sent)
    {
        var mine = APost.With(id: "110", account: "jeff@mastodon.social") with { Language = "de" };

        using var drawn = await Drawn(post: mine, opening: ComposeFor.Edit);
        var compose = Compose(drawn);

        OnLang(drawn);
        Field(drawn).Text = string.Empty;
        Type(drawn, typed);

        if (typed.Length > 0)
        {
            drawn.Press(Key.Tab);
        }

        await drawn.Shell.Send();
        drawn.Settle();

        Assert.Equal(sent, Assert.Single(drawn.Built.Author.Edits).Edit.Language);
    }

    /// <summary>A Lang holding something that is not a language refuses the send, saying why, and sends nothing.</summary>
    [Fact]
    public async Task NonsenseInLangRefusesTheSend()
    {
        using var drawn = await Drawn();
        var compose = Compose(drawn);

        compose.Text = "hello";
        OnLang(drawn);
        Type(drawn, "klingonish");

        Assert.False(List(drawn).Visible);

        await drawn.Shell.Send();
        drawn.Settle();

        Assert.Same(compose, drawn.Shell.Screen);
        Assert.Empty(drawn.Built.Author.Published);
        Assert.Equal(PostLanguageName.Rejection("klingonish"), drawn.Shell.Notice);
        Assert.True(drawn.Shell.NoticeIsError);
    }

    /// <summary>
    ///     On a short terminal Lang gives way along with From, while To, the warning and three rows of editor stay.
    /// </summary>
    [Fact]
    public async Task OnAShortTerminalLangGivesWayWithFrom()
    {
        var compose = await Opening(ComposeFor.Post, preferred: "fr");
        var rows = compose.Lines(new Drawing(Width, AShell.Now, Height: 6)).Select(line => line.Text).ToList();

        Assert.DoesNotContain(rows, row => row.Contains("Lang", StringComparison.Ordinal));
        Assert.DoesNotContain(rows, row => row.Contains("From", StringComparison.Ordinal));
        Assert.Contains(rows, row => row.StartsWith("     To", StringComparison.Ordinal));
        Assert.Contains(rows, row => row.StartsWith(ComposeRows.NoWarning[..8], StringComparison.Ordinal));
        Assert.True(compose.EditorAt(new System.Drawing.Size(Width, 6)).Height >= 3);
        Assert.Equal(System.Drawing.Rectangle.Empty, compose.LangAt(new System.Drawing.Size(Width, 6)));

        var taller = compose.Lines(new Drawing(Width, AShell.Now, Height: 9)).Select(line => line.Text).ToList();

        Assert.Contains(taller, row => row.Contains("Lang  fr  Français", StringComparison.Ordinal));
    }

    /// <summary>
    ///     An edit of a post in a language this client does not list opens on its code, and sends it back untouched
    ///     rather than refusing it.
    /// </summary>
    [Fact]
    public async Task AnEditOfAPostInALanguageNotListedSendsItBack()
    {
        var mine = APost.With(id: "110", account: "jeff@mastodon.social") with { Language = "xx" };

        var compose = await Opening(ComposeFor.Edit, post: mine);

        Assert.Equal("   Lang  xx", Texts(compose)[3]);
        Assert.Null(compose.Lang.Refusal);
        Assert.Equal("xx", Saving(compose).Language);
    }

    /// <summary>Every cell of Lang and its open list is a role the theme answers.</summary>
    [Theory]
    [InlineData("dark")]
    [InlineData("light")]
    public async Task EveryCellOfLangAndItsListIsARole(string name)
    {
        var theme = name == "dark" ? Themes.Dark : Themes.Light;

        using var drawn = await Drawn(theme: theme);

        OnLang(drawn);
        Type(drawn, "fr");
        drawn.Redraw();

        var answered = Enum.GetValues<Role>()
                           .SelectMany(role => new[] { theme.For(role), theme.Banded(role) })
                           .ToHashSet();

        Assert.True(List(drawn).Visible);
        Assert.All(drawn.Cells(), cell => Assert.Contains(cell, answered));
    }

    private static void OnLang(ComposedView drawn)
    {
        drawn.Press(Key.CursorUp);
        drawn.Press(Key.CursorUp);
    }

    private static void Type(ComposedView drawn, string text) => drawn.Type(text);

    /// <summary>A click in the value column of the header reading <paramref name="text" />.</summary>
    private static void ClickOn(ComposedView drawn, string text) => drawn.ClickOn(text, text.Length + 3);

    private static ComposeLangField Field(ComposedView drawn) => drawn.Lang;

    private static PaintedView List(ComposedView drawn) => drawn.Languages;

    private static IReadOnlyList<string> ListRows(ComposedView drawn) => drawn.RowsOf(drawn.Languages);

    private static ComposeScreen Compose(ComposedView drawn) => drawn.Compose;

    private static Task<ComposedView> Drawn(
        string? preferred = null,
        Post? post = null,
        ComposeFor opening = ComposeFor.Post,
        ITheme? theme = null) =>
        ComposedView.Of(
            new AShell
            {
                Timelines = FakeTimelineReader.Holding(post ?? APost.With(id: "220", account: "ben@hachyderm.io")),
                Accounts = FakeAccountRelationships.HoldingNobody(),
                DefaultLanguage = preferred,
            },
            opening,
            theme: theme);

    private static PostDraft Publishing(ComposeScreen compose) =>
        Assert.IsType<Outgoing.Publishing>(compose.Outgoing).Draft;

    private static PostEdit Saving(ComposeScreen compose) => Assert.IsType<Outgoing.Saving>(compose.Outgoing).Edit;

    private static IReadOnlyList<string> Texts(ComposeScreen compose) =>
        [.. compose.Lines(new Drawing(Width, AShell.Now, Height: Height)).Select(line => line.Text)];

    private static async Task<ComposeScreen> Opening(
        ComposeFor opening,
        string? preferred = null,
        string? account = null,
        Post? post = null)
    {
        var shell = await new AShell
        {
            Timelines = FakeTimelineReader.Holding(post ?? APost.With(id: "220", account: "ben@hachyderm.io")),
            Defaults = FakeAccountDefaults.Setting(new PostDefaults(null, account)),
            DefaultLanguage = preferred,
        }.Opened();

        return ComposeRows.Open(shell, opening);
    }
}
