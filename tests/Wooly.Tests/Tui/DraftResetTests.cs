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

    /// <summary>
    ///     Keep, any key but <c>y</c>, leaves the reader exactly where they were: the list in front over the compose, the
    ///     row they had picked still picked, and nothing attached called off.
    /// </summary>
    [Fact]
    public async Task KeepingAfterSlash_LeavesTheListAsItWas()
    {
        using var drawn = await Listing();
        var listed = Assert.IsType<AttachmentsScreen>(drawn.Shell.Screen);

        drawn.Press(Key.CursorDown);

        var picked = listed.Compose.Media.Picked;

        drawn.Press(new Key('/'));
        drawn.Press(new Key('n'));
        drawn.Settle();

        Assert.Null(drawn.Shell.Asking);
        Assert.Same(listed, drawn.Shell.Screen);
        Assert.Equal(["Home", "Compose", "Media"], drawn.Shell.Crumbs);
        Assert.Equal("two.png", picked?.Name);
        Assert.Same(picked, listed.Compose.Media.Picked);
        Assert.Equal(DestinationKind.Home, drawn.Shell.Rail.Showing.Kind);
        Assert.DoesNotContain(drawn.Built.Author.Attaching, sending => sending.CalledOff);
    }

    /// <summary><c>y</c> throws the draft away, calls off what it was sending up, and goes to Search.</summary>
    [Fact]
    public async Task DiscardingAfterSlash_LetsTheUploadsGoAndGoesToSearch()
    {
        using var drawn = await Listing();

        drawn.Press(new Key('/'));
        drawn.Press(Key.Y);
        drawn.Built.Host.SettleAll();
        drawn.Redraw();

        Assert.Null(drawn.Shell.Asking);
        Assert.IsType<SearchScreen>(drawn.Shell.Screen);
        Assert.Equal(1, drawn.Shell.Depth);
        Assert.Equal(DestinationKind.Search, drawn.Shell.Rail.Showing.Kind);
        Assert.Equal(3, drawn.Built.Author.Attaching.Count);
        Assert.All(drawn.Built.Author.Attaching, sending => Assert.True(sending.CalledOff));
    }

    /// <summary>
    ///     An untouched compose asks nothing: <c>/</c> from it goes to Search at once, as every other way out of one does,
    ///     and from Search itself it puts a fresh prompt up in its place.
    /// </summary>
    [Theory]
    [InlineData(DestinationKind.Home)]
    [InlineData(DestinationKind.Search)]
    public async Task SlashOffAnUntouchedCompose_GoesWithoutAsking(DestinationKind from)
    {
        var built = new AShell();
        var shell = await built.Opened();

        shell.Rail.GoTo(from);
        built.Host.Settle();
        shell.Press(ShellKey.C);

        Assert.False(Assert.IsType<ComposeScreen>(shell.Screen).Touched);

        shell.Press(ShellKey.Slash);
        built.Host.Settle();

        Assert.Null(shell.Asking);
        Assert.IsType<SearchScreen>(shell.Screen);
        Assert.Equal(1, shell.Depth);
        Assert.Equal(DestinationKind.Search, shell.Rail.Showing.Kind);
    }

    /// <summary>A drawn compose with three files attached, their rows folded, and the attachments screen listing them.</summary>
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
