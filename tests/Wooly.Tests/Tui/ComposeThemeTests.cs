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
///     empty editor.
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

        using var drawn = await Composing(theme);

        Type(drawn, "Hello there");

        var answered = Enum.GetValues<Role>()
                           .SelectMany(role => new[] { theme.For(role), theme.Banded(role) })
                           .ToHashSet();

        Assert.All(drawn.Cells(), cell => Assert.Contains(cell, answered));
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

        using var drawn = await DrawnShell.Of(80, 24, theme, built);

        drawn.Shell.Reply();
        drawn.Redraw();

        var answered = Enum.GetValues<Role>()
                           .SelectMany(role => new[] { theme.For(role), theme.Banded(role) })
                           .ToHashSet();

        Assert.Contains(drawn.Rows(), row => row.Contains("⚠  spoilers", StringComparison.Ordinal));
        Assert.All(drawn.Cells(), cell => Assert.Contains(cell, answered));
    }

    /// <summary>The editor's text and the empty rows under it are the body role on the page, edge to edge.</summary>
    [Fact]
    public async Task TheEditorIsTheBodyOnThePage()
    {
        using var drawn = await Composing();

        Type(drawn, "Hello there");

        var body = Themes.Dark.For(Role.Body);

        Assert.All(EditorCells(drawn), cell => Assert.Equal(body, cell));
    }

    /// <summary>Selected text is drawn in the selection role rather than the band or Terminal.Gui's own.</summary>
    [Fact]
    public async Task SelectedTextIsDrawnInItsOwnRole()
    {
        using var drawn = await Composing();

        Type(drawn, "Hello");

        Editor(drawn).SelectAll();
        drawn.Redraw();

        var at = Editor(drawn).FrameToScreen();

        Assert.All(
            Enumerable.Range(at.X, "Hello".Length),
            column => Assert.Equal(Themes.Dark.For(Role.SelectedText), drawn.Cell(at.Y, column)));
    }

    /// <summary>
    ///     An empty editor says where the first letter goes, dimly; the hint goes the moment one is typed and comes
    ///     back when the post is cleared.
    /// </summary>
    [Fact]
    public async Task AnEmptyEditorShowsAPlaceholderUntilALetterIsTyped()
    {
        using var drawn = await Composing();

        Assert.Equal(Placeholder, FirstRow(drawn)[..Placeholder.Length]);
        Assert.All(
            Enumerable.Range(Editor(drawn).FrameToScreen().X, Placeholder.Length),
            column => Assert.Equal(
                Themes.Dark.For(Role.Muted),
                drawn.Cell(Editor(drawn).FrameToScreen().Y, column)));

        Type(drawn, "a");

        Assert.Equal("a", FirstRow(drawn).TrimEnd());

        drawn.Press(Key.Backspace);

        Assert.Equal(Placeholder, FirstRow(drawn)[..Placeholder.Length]);
    }

    /// <summary>A reply opens on the mention it is addressed with, so it opens with no placeholder over it.</summary>
    [Fact]
    public async Task AnEditorWithTextInItShowsNoPlaceholder()
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "220", account: "ben@hachyderm.io")),
        };

        using var drawn = await DrawnShell.Of(80, 24, Themes.Dark, built);

        drawn.Shell.Reply();
        drawn.Redraw();

        Assert.Equal("@ben@hachyderm.io ", Editor(drawn).Text);

        Assert.DoesNotContain(Placeholder, FirstRow(drawn), StringComparison.Ordinal);
    }

    private static async Task<DrawnShell> Composing(ITheme? theme = null)
    {
        var drawn = await DrawnShell.Of(80, 24, theme ?? Themes.Dark);

        drawn.Shell.Compose();
        drawn.Redraw();

        return drawn;
    }

    private static void Type(DrawnShell drawn, string text)
    {
        foreach (var letter in text)
        {
            drawn.Window.NewKeyDownEvent(new Key(letter));
        }

        drawn.Redraw();
    }

    private static ComposeEditor Editor(DrawnShell drawn) =>
        drawn.Window.SubViews.OfType<ComposeEditor>().Single();

    /// <summary>The editor's first row, as drawn.</summary>
    private static string FirstRow(DrawnShell drawn)
    {
        var at = Editor(drawn).FrameToScreen();

        return drawn.Rows()[at.Y].Substring(at.X, at.Width);
    }

    private static IEnumerable<Attribute> EditorCells(DrawnShell drawn)
    {
        var at = Editor(drawn).FrameToScreen();

        for (var row = at.Top; row < at.Bottom; row++)
        {
            for (var column = at.Left; column < at.Right; column++)
            {
                yield return drawn.Cell(row, column);
            }
        }
    }
}
