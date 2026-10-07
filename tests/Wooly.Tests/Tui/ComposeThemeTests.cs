using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;
using Key = Terminal.Gui.Input.Key;

namespace Wooly.Tests.Tui;

/// <summary>
///     The compose screen drawn in the theme (#316): the editor's text in the body role on the terminal's own page
///     rather than in Terminal.Gui's saturated default, selected text in a role of its own, and a dim placeholder in an
///     empty editor. Drawn by <see cref="ComposeView" /> over the screen's own rows (#365).
/// </summary>
public class ComposeThemeTests
{
    private const string Placeholder = ComposeScreen.EmptyPostHint;

    /// <summary>
    ///     Every cell of a compose screen is a role the theme answers — on the page or on a band — and none is a colour
    ///     Terminal.Gui chose for itself.
    /// </summary>
    [Theory]
    [InlineData("dark")]
    [InlineData("light")]
    public async Task EveryCellOfComposeIsARoleTheThemeAnswers(string name)
    {
        var theme = name == "dark" ? Themes.Dark : Themes.Light;

        using var view = await Composing(theme);

        view.Type("Hello there");

        var answered = Enum.GetValues<Role>()
                           .SelectMany(role => new[] { theme.For(role), theme.Banded(role) })
                           .ToHashSet();

        Assert.All(view.Cells(), cell => Assert.Contains(cell, answered));
    }

    /// <summary>
    ///     And on a reply to a warned post, which draws every header there is (#317): From, the reply header and its
    ///     quote, the lit warning, and both hairlines.
    /// </summary>
    [Theory]
    [InlineData("dark")]
    [InlineData("light")]
    public async Task EveryCellOfAWarnedReplyIsARoleTheThemeAnswers(string name)
    {
        var theme = name == "dark" ? Themes.Dark : Themes.Light;
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "220", account: "ben@hachyderm.io", contentWarning: "spoilers")),
        };

        using var view = await ComposedView.Of(built, ComposeFor.Reply, theme: theme);

        var answered = Enum.GetValues<Role>()
                           .SelectMany(role => new[] { theme.For(role), theme.Banded(role) })
                           .ToHashSet();

        Assert.Contains(view.Rows(), row => row.Contains("⚠  spoilers", StringComparison.Ordinal));
        Assert.All(view.Cells(), cell => Assert.Contains(cell, answered));
    }

    /// <summary>The editor's text and the empty rows under it are the body role on the page, edge to edge.</summary>
    [Fact]
    public async Task TheEditorIsTheBodyOnThePage()
    {
        using var view = await Composing();

        view.Type("Hello there");

        var body = Themes.Dark.For(Role.Body);

        Assert.All(EditorCells(view), cell => Assert.Equal(body, cell));
    }

    /// <summary>Selected text is drawn in the selection role rather than the band or Terminal.Gui's own.</summary>
    [Fact]
    public async Task SelectedTextIsDrawnInItsOwnRole()
    {
        using var view = await Composing();

        view.Type("Hello");

        view.Editor.SelectAll();
        view.Redraw();

        var at = view.Editor.FrameToScreen();

        Assert.All(
            Enumerable.Range(at.X, "Hello".Length),
            column => Assert.Equal(Themes.Dark.For(Role.SelectedText), view.Cell(at.Y, column)));
    }

    /// <summary>
    ///     An empty editor says where the first letter goes, dimly; the hint goes the moment one is typed and comes
    ///     back when the post is cleared.
    /// </summary>
    [Fact]
    public async Task AnEmptyEditorShowsAPlaceholderUntilALetterIsTyped()
    {
        using var view = await Composing();

        Assert.Equal(Placeholder, FirstRow(view)[..Placeholder.Length]);
        Assert.All(
            Enumerable.Range(view.Editor.FrameToScreen().X, Placeholder.Length),
            column => Assert.Equal(
                Themes.Dark.For(Role.Muted),
                view.Cell(view.Editor.FrameToScreen().Y, column)));

        view.Type("a");

        Assert.Equal("a", FirstRow(view).TrimEnd());

        view.Press(Key.Backspace);

        Assert.Equal(Placeholder, FirstRow(view)[..Placeholder.Length]);
    }

    /// <summary>A reply opens on the mention it is addressed with, so it opens with no placeholder over it.</summary>
    [Fact]
    public async Task AnEditorWithTextInItShowsNoPlaceholder()
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "220", account: "ben@hachyderm.io")),
        };

        using var view = await ComposedView.Of(built, ComposeFor.Reply);

        Assert.Equal("@ben@hachyderm.io ", view.Editor.Text);

        Assert.DoesNotContain(Placeholder, FirstRow(view), StringComparison.Ordinal);
    }

    private static Task<ComposedView> Composing(ITheme? theme = null) => ComposedView.Of(theme: theme);

    /// <summary>The editor's first row, as drawn.</summary>
    private static string FirstRow(ComposedView view) => view.Shown(view.Editor);

    private static IEnumerable<Attribute> EditorCells(ComposedView view)
    {
        var at = view.Editor.FrameToScreen();

        for (var row = at.Top; row < at.Bottom; row++)
        {
            for (var column = at.Left; column < at.Right; column++)
            {
                yield return view.Cell(row, column);
            }
        }
    }
}
