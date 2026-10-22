using Terminal.Gui.Input;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     Every way of putting the stack back to one screen from inside a draft, or from a screen over one, asks before a
///     touched draft goes with it, as the rail does (#373, #426). Keep leaves everything where it was; discard lets the
///     draft's uploads go and goes where it was asked to.
/// </summary>
public class DraftResetTests : IDisposable
{
    private const string Asked = " Discard this post?  y / n";

    /// <summary>Short enough that a compose's attachment rows fold into the Media header's line (story 58).</summary>
    private const int Short = 18;

    private readonly TemporaryDirectory _files = new();

    public void Dispose() => _files.Dispose();

    /// <summary>
    ///     <c>/</c> on the attachments screen over a compose with something attached asks first, and goes nowhere while
    ///     it does: the list stays in front, over the compose, and the rail stays where it was.
    /// </summary>
    [Fact]
    public async Task SlashOnTheAttachmentsScreen_AsksBeforeGoingToSearch()
    {
        using var drawn = await Listing();
        var listed = drawn.Shell.Screen;

        drawn.Press(new Key('/'));
        drawn.Settle();

        Assert.Equal(Asked, drawn.Rows()[^1].TrimEnd());
        Assert.Same(listed, drawn.Shell.Screen);
        Assert.Equal(["Home", "Compose", "Media"], drawn.Shell.Crumbs);
        Assert.Equal(DestinationKind.Home, drawn.Shell.Rail.Showing.Kind);
    }

    /// <summary>A drawn compose with two files attached, their rows folded, and the attachments screen listing them.</summary>
    private async Task<DrawnShell> Listing()
    {
        var drawn = await DrawnShell.Of(80, Short, Themes.Plain, new AShell { LaunchedFrom = _files.Path });

        drawn.Shell.Compose();
        drawn.Redraw();

        foreach (var name in new[] { "one.png", "two.png", "three.png" })
        {
            Assert.True(drawn.Shell.Paste(_files.WriteFile(name)));
        }

        drawn.Redraw();
        drawn.Press(Key.CursorUp);
        drawn.Press(Key.Enter);
        drawn.Redraw();

        Assert.IsType<AttachmentsScreen>(drawn.Shell.Screen);

        return drawn;
    }
}
