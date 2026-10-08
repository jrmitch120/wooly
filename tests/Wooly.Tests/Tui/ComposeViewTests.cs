using Terminal.Gui.ViewBase;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Views;
using Key = Terminal.Gui.Input.Key;

namespace Wooly.Tests.Tui;

/// <summary>
///     <see cref="ComposeView" />, compose's fields in one view over the content viewport (#365): shown while compose is
///     on top and filled from its draft, gone once it is left, and placed where the compose screen lays them.
/// </summary>
public class ComposeViewTests
{
    private static readonly AShell Answering = new()
    {
        Timelines = FakeTimelineReader.Holding(
            APost.With(id: "220", account: "ben@hachyderm.io", contentWarning: "spoilers")),
    };

    /// <summary>Over a shell showing no compose, the view and every field in it are hidden, and draw nothing.</summary>
    [Fact]
    public async Task WithNoComposeOnTopNothingShows()
    {
        using var view = await ComposedView.Of(Answering, opening: null);

        Assert.False(view.View.Visible);
        Assert.All(Fields(view), field => Assert.False(field.Visible));
        Assert.DoesNotContain(view.Rows(), row => row.Contains(ComposeScreen.EmptyPostHint, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Compose coming on top shows every field, filled from the draft — a reply opens on its mention and the answered
    ///     post's warning — with the typing in the post, after what it opened with.
    /// </summary>
    [Fact]
    public async Task ComposeOnTopShowsTheFieldsFilledFromTheDraft()
    {
        using var view = await ComposedView.Of(Answering, ComposeFor.Reply);

        Assert.True(view.View.Visible);
        Assert.All(Fields(view), field => Assert.True(field.Visible));
        Assert.Equal("@ben@hachyderm.io ", view.Editor.Text);
        Assert.Equal("spoilers", view.Warning.Text);
        Assert.True(view.Editor.HasFocus);

        view.Type("hi");

        Assert.Equal("@ben@hachyderm.io hi", view.Compose.Text);
    }

    /// <summary>
    ///     Not only typing: a paste and an undo are edits too, and the screen's text follows both (#315). How much of a
    ///     paste one undo takes back is the editor's own business, so what is pinned is that the screen ends up where
    ///     the editor does.
    /// </summary>
    [Fact]
    public async Task TheDraftFollowsAPasteAndItsUndo()
    {
        using var view = await ComposedView.Of(Answering, ComposeFor.Reply);

        view.Paste("thanks!");

        Assert.Equal("@ben@hachyderm.io thanks!", view.Compose.Text);

        view.Press(Key.Z.WithCtrl);

        Assert.NotEqual("@ben@hachyderm.io thanks!", view.Editor.Text);
        Assert.Equal(view.Editor.Text, view.Compose.Text);
    }

    /// <summary>A view built over a shell already composing shows the fields from the start.</summary>
    [Fact]
    public async Task AViewBuiltOverAnOpenComposeShowsItsFields()
    {
        var built = new AShell { Timelines = Answering.Timelines };
        var shell = await built.Opened();

        ComposeRows.Open(shell, ComposeFor.Reply);

        using var view = new ComposeView(Wooly.Tui.Theme.Themes.Dark, shell) { Width = 58, Height = 21 };

        Assert.True(view.Visible);
        Assert.Equal("spoilers", view.SubViews.OfType<ComposeWarningField>().Single().Text);
    }

    /// <summary>
    ///     Leaving compose — here from the warning, which had the typing — takes every field off the screen and the
    ///     focus out of the view, and what is under it shows again.
    /// </summary>
    [Fact]
    public async Task LeavingComposeTakesTheFieldsAway()
    {
        using var view = await ComposedView.Of(Answering, ComposeFor.Reply);

        view.Press(Key.W.WithCtrl);
        view.Press(Key.Esc);

        Assert.IsNotType<ComposeScreen>(view.Shell.Screen);
        Assert.False(view.View.Visible);
        Assert.False(view.View.HasFocus);
        Assert.All(Fields(view), field => Assert.False(field.Visible));
        Assert.DoesNotContain(view.Rows(), row => row.Contains("From", StringComparison.Ordinal));
        Assert.DoesNotContain(view.Rows(), row => row.Contains("⚠  ", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Each field sits where the compose screen lays it in the viewport the view is laid over, whatever that
    ///     viewport's size.
    /// </summary>
    [Theory]
    [InlineData(ComposeFor.Post, ComposedView.Width, ComposedView.Height)]
    [InlineData(ComposeFor.Reply, ComposedView.Width, ComposedView.Height)]
    [InlineData(ComposeFor.Reply, 98, 30)]
    public async Task EachFieldSitsWhereTheScreenLaysIt(ComposeFor opening, int width, int height)
    {
        using var view = await ComposedView.Of(Answering, opening, width, height);
        var compose = view.Compose;
        var viewport = new System.Drawing.Size(width, height);

        Assert.Equal(compose.EditorAt(viewport), view.Editor.Frame);
        Assert.Equal(compose.WarningAt(viewport), view.Warning.Frame);
        Assert.Equal(compose.ToAt(viewport), view.To.Frame);
        Assert.Equal(compose.LangAt(viewport), view.Lang.Frame);
    }

    /// <summary>
    ///     A click on a field moves the typing there, and the typing moved by a key moves the focus: the two are kept as
    ///     one fact, the screen's.
    /// </summary>
    [Fact]
    public async Task FocusAndTheTypingFollowEachOther()
    {
        using var view = await ComposedView.Of(new AShell { Accounts = FakeAccountRelationships.HoldingNobody() });
        var compose = view.Compose;

        view.Click(view.Lang);

        Assert.Equal(ComposeField.Lang, compose.Typing);
        Assert.True(view.Lang.HasFocus);

        // The click on Lang opened the list of languages under it, and the first click outside that is spent on
        // closing it (#335, #366): it takes the second to move the typing.
        view.Click(view.Editor);

        Assert.False(view.Languages.Visible);
        Assert.Equal(ComposeField.Lang, compose.Typing);

        view.Click(view.Editor);

        Assert.Equal(ComposeField.Post, compose.Typing);
        Assert.True(view.Editor.HasFocus);

        view.Shell.ChangeCompose(screen => screen.Walk(-2));
        view.Redraw();

        Assert.True(view.Warning.HasFocus);
    }

    /// <summary>A click between the fields is not the view's: it reaches whatever is behind, as on the content panel.</summary>
    [Fact]
    public async Task AClickBetweenTheFieldsGoesThrough()
    {
        using var view = await ComposedView.Of(Answering, ComposeFor.Reply);
        var compose = view.Compose;

        view.ClickOn("From");

        Assert.Equal(ComposeField.Post, compose.Typing);
        Assert.True(view.Editor.HasFocus);
        Assert.NotNull(view.Application.Mouse.CachedViewsUnderMouse);
        Assert.DoesNotContain(view.View, view.Application.Mouse.CachedViewsUnderMouse!);
    }

    private static IEnumerable<View> Fields(ComposedView view) => [view.Editor, view.Warning, view.To, view.Lang];
}
