using Terminal.Gui.Input;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;
using static Wooly.Tests.Tui.ContentClicks;

namespace Wooly.Tests.Tui;

/// <summary>
///     A right click, sent at a cell of a headless terminal: it is <c>esc</c> through the <see cref="Keymap" /> on the
///     screen in front, wherever the pointer is, and so up one level of whichever kind is open (#307).
/// </summary>
public class ShellRightClickTests
{
    /// <summary>A column inside the rail, clear of its left edge and its mark column.</summary>
    private const int OverRail = RailLines.Width / 2;

    /// <summary>
    ///     A right click lands the shell where <c>esc</c> does on each screen with a level open: a drilled-in post pops,
    ///     a picked reference and an uncast toggle are let go without a pop, a confirmation is declined and the filter
    ///     prompt's filter is cleared.
    /// </summary>
    [Theory]
    [InlineData("post")]
    [InlineData("reference")]
    [InlineData("toggle")]
    [InlineData("confirmation")]
    [InlineData("filter")]
    public async Task ARightClickIsEsc(string state)
    {
        using var keyed = await In(state);
        using var clicked = await In(state);

        var shown = clicked.Shell.Screen;

        keyed.Press(Key.Esc);
        keyed.Settle();

        clicked.RightClick(OverContent, ContentRowOf(clicked, 0));
        clicked.Settle();

        Assert.Equal(keyed.Shell.Screen.GetType(), clicked.Shell.Screen.GetType());
        Assert.Equal(keyed.Shell.Asking, clicked.Shell.Asking);
        Assert.Equal(keyed.Rows(), clicked.Rows());

        switch (state)
        {
            case "post":
                Assert.IsType<FeedScreen>(clicked.Shell.Screen);
                break;

            case "reference":
                Assert.Same(shown, clicked.Shell.Screen);
                Assert.Null(clicked.Shell.Screen.Reference);
                break;

            case "toggle":
                Assert.Same(shown, clicked.Shell.Screen);
                Assert.Empty(clicked.Shell.Screen.Chosen);
                break;

            case "confirmation":
                Assert.Same(shown, clicked.Shell.Screen);
                Assert.Null(clicked.Shell.Asking);
                Assert.Empty(clicked.Built.Engagement.Votes);
                break;

            case "filter":
                var follows = Assert.IsType<FollowsScreen>(clicked.Shell.Screen);
                Assert.Same(shown, follows);
                Assert.False(follows.IsTyping);
                Assert.Empty(follows.Filter);
                break;
        }
    }

    /// <summary>On Add a profile with a sign-in in flight, a right click calls it off, as <c>esc</c> does.</summary>
    [Fact]
    public async Task ARightClickCallsOffASignInInFlight()
    {
        var built = new AShell { Authorizer = FakeBrowserAuthorizer.Holding() };

        using var drawn = await DrawnShell.Of(80, Tall, Themes.Plain, built);

        drawn.Press(Key.P.WithCtrl);
        drawn.Press(Key.A);

        foreach (var letter in "hachyderm.io")
        {
            drawn.Press(new Key(letter));
        }

        drawn.Press(Key.Enter);
        drawn.Settle();

        Assert.True(drawn.Shell.Fetching);

        drawn.RightClick(OverContent, Tall / 2);
        drawn.Settle();

        Assert.True(built.Authorizer.Cancelled);
        Assert.IsType<ProfilesScreen>(drawn.Shell.Screen);
    }

    /// <summary>On a destination's own screen there is nothing to go up out of, and a right click does nothing.</summary>
    [Fact]
    public async Task OnADestinationsOwnScreenARightClickDoesNothing()
    {
        using var drawn = await On("feed");

        var feed = drawn.Shell.Screen;
        var rows = drawn.Rows();

        drawn.RightClick(OverContent, ContentRowOf(drawn, 1));
        drawn.Settle();

        Assert.Same(feed, drawn.Shell.Screen);
        Assert.Equal(rows, drawn.Rows());
    }

    /// <summary>
    ///     Over the rail, the breadcrumb and the status row a right click is <c>esc</c> too: it pops the post, and does
    ///     not arrive at the destination under the pointer.
    /// </summary>
    [Theory]
    [InlineData("rail")]
    [InlineData("breadcrumb")]
    [InlineData("status")]
    public async Task ARightClickOffTheContentIsEscToo(string over)
    {
        using var drawn = await On("post");

        var notifications = drawn.Built.Notifications.Reads.Count;

        var (column, row) = over switch
        {
            "rail" => (OverRail, Array.FindIndex(drawn.Rail(), line => line.Contains("Notifications"))),
            "breadcrumb" => (OverContent, 0),
            _ => (OverContent, Tall - 1),
        };

        drawn.RightClick(column, row);
        drawn.Settle();

        Assert.IsType<FeedScreen>(drawn.Shell.Screen);
        Assert.Equal(DestinationKind.Home, drawn.Shell.Rail.Showing.Kind);
        Assert.Equal(notifications, drawn.Built.Notifications.Reads.Count);
    }

    /// <summary>
    ///     With compose in front a right click does nothing anywhere, since <c>esc</c> there throws the draft away: the
    ///     draft stands, and the editor's own context menu does not open.
    /// </summary>
    [Theory]
    [InlineData("editor")]
    [InlineData("rail")]
    [InlineData("breadcrumb")]
    [InlineData("status")]
    public async Task WithComposeInFrontARightClickDoesNothing(string over)
    {
        using var drawn = await On("feed");

        drawn.Shell.Compose();
        drawn.Redraw();

        var compose = drawn.Shell.Screen;
        var editor = drawn.Window.SubViews.OfType<ComposeEditor>().Single();

        editor.Text = "A draft about sheep";
        drawn.Redraw();

        var (column, row) = over switch
        {
            "editor" => (OverContent, Tall / 2),
            "rail" => (OverRail, 2),
            "breadcrumb" => (OverContent, 0),
            _ => (OverContent, Tall - 1),
        };

        drawn.RightClick(column, row, times: 3);
        drawn.Settle();

        Assert.Same(compose, drawn.Shell.Screen);
        Assert.Equal("A draft about sheep", editor.Text);
        Assert.False(editor.ContextMenu?.Visible ?? false);
        Assert.Null(drawn.Application.Popovers?.GetActivePopover());
    }

    /// <summary>
    ///     Every right click the terminal reports is one <c>esc</c>, its double and triple included: three quick right
    ///     clicks walk back three levels.
    /// </summary>
    [Fact]
    public async Task ThreeQuickRightClicksWalkBackThreeLevels()
    {
        using var drawn = await On("post");

        drawn.Press(Key.A);
        drawn.Settle();
        drawn.Press(new Key('?'));
        drawn.Settle();

        Assert.IsType<HelpScreen>(drawn.Shell.Screen);

        drawn.RightClick(OverContent, Tall / 2, times: 2);
        drawn.Settle();

        Assert.IsType<PostScreen>(drawn.Shell.Screen);

        drawn.Press(Key.A);
        drawn.Settle();
        drawn.Press(new Key('?'));
        drawn.Settle();

        drawn.RightClick(OverContent, Tall / 2, times: 3);
        drawn.Settle();

        Assert.IsType<FeedScreen>(drawn.Shell.Screen);
    }

    /// <summary>A ctrl+left click is still a left click, and picks what it is on rather than going back.</summary>
    [Fact]
    public async Task ACtrlClickIsStillALeftClick()
    {
        using var drawn = await On("post");

        var post = drawn.Shell.Screen;

        drawn.Pointed(OverContent, ContentRowOf(drawn, 1), MouseFlags.LeftButtonClicked | MouseFlags.Ctrl);
        drawn.Settle();

        Assert.Same(post, drawn.Shell.Screen);
        Assert.Equal("120", drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>The middle button means nothing.</summary>
    [Fact]
    public async Task TheMiddleButtonDoesNothing()
    {
        using var drawn = await On("post");

        var post = drawn.Shell.Screen;
        var rows = drawn.Rows();

        drawn.Pointed(OverContent, ContentRowOf(drawn, 1), MouseFlags.MiddleButtonClicked);
        drawn.Pointed(OverContent, ContentRowOf(drawn, 1), MouseFlags.MiddleButtonDoubleClicked);
        drawn.Settle();

        Assert.Same(post, drawn.Shell.Screen);
        Assert.Equal(rows, drawn.Rows());
    }

    /// <summary>The <c>?</c> screen's mouse line says what a right click is.</summary>
    [Fact]
    public async Task TheHelpScreenSaysARightClickIsBack()
    {
        using var drawn = await DrawnShell.Of(200, Tall, Themes.Plain);

        drawn.Press(new Key('?'));

        Assert.Contains(drawn.Rows(), row => row.Contains("mouse") && row.Contains("right click back"));
    }

    /// <summary>
    ///     The shell drawn tall in <paramref name="state" />: on a drilled-in post, or with a level of its own open — a
    ///     reference picked inside a post, a poll's toggle uncast on one, a confirmation asked, or a filter typed.
    /// </summary>
    private static async Task<DrawnShell> In(string state)
    {
        switch (state)
        {
            case "post":
                return await On("post");

            case "filter":
            {
                var drawn = await On("follows");

                drawn.Press(Key.F);
                drawn.Press(new Key('b'));

                return drawn;
            }
        }

        var answers = Enumerable.Range(1, 3).Select(at => APost.AnAnswer($"Answer {at}", at)).ToList();

        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "110", content: "<p>Shearing day #sheep</p>", poll: APost.APoll(options: answers)),
                APost.With(id: "220")),
        };

        var opened = await DrawnShell.Of(80, Tall, Themes.Plain, built);

        opened.Press(Key.Enter);
        opened.Settle();

        switch (state)
        {
            case "reference":
                opened.Press(Key.CursorRight);
                Assert.NotNull(opened.Shell.Screen.Reference);
                break;

            case "toggle":
                opened.Press(new Key('2'));
                Assert.NotEmpty(opened.Shell.Screen.Chosen);
                break;

            case "confirmation":
                opened.Press(new Key('2'));
                opened.Press(Key.V);
                Assert.NotNull(opened.Shell.Asking);
                break;
        }

        return opened;
    }
}
