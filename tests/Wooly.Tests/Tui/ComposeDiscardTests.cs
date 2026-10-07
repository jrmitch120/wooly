using Terminal.Gui.Input;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;
using static Wooly.Tests.Tui.ContentClicks;

namespace Wooly.Tests.Tui;

/// <summary>
///     A compose screen that differs from how it opened asks before it is thrown away, on every way out of it (#373):
///     <c>esc</c>, a right click, <c>tab</c> and <c>shift-tab</c>, a click on the rail, <c>ctrl-p</c> and quitting. The
///     question is the confirmation row's, <c>y</c> and only <c>y</c> goes ahead with the way out it interrupted, and
///     anything else keeps the draft and does nothing more. An untouched one leaves without asking.
/// </summary>
/// <remarks>
///     Pressed through a real window, since the keys a question has to take are the ones the compose fields would
///     otherwise take first.
/// </remarks>
public class ComposeDiscardTests
{
    private const string Asked = " Discard this post?  y / n";

    private const int OverRail = RailLines.Width / 2;

    /// <summary>A post by somebody else, behind a warning, so that a reply opens on a mention and that warning.</summary>
    private static AShell Answering() => new()
    {
        Timelines = FakeTimelineReader.Holding(
            APost.With(id: "220", account: "ben@hachyderm.io", contentWarning: "spoilers")),
    };

    private static AShell Built(ComposeFor purpose) => purpose == ComposeFor.Reply ? Answering() : new AShell();

    private static async Task<DrawnShell> Composing(
        ComposeFor purpose = ComposeFor.Post,
        Action? quit = null,
        AShell? built = null)
    {
        var drawn = await DrawnShell.Of(80, Tall, Themes.Plain, built ?? Built(purpose), quit: quit);

        ComposeRows.Open(drawn.Shell, purpose);
        drawn.Redraw();

        return drawn;
    }

    private static void Type(DrawnShell drawn, string text)
    {
        foreach (var letter in text)
        {
            drawn.Press(new Key(letter));
        }
    }

    private static string Status(DrawnShell drawn) => drawn.Rows()[^1].TrimEnd();

    private static int RailRowOf(DrawnShell drawn, string label) =>
        Array.FindIndex(drawn.Rail(), row => row.Contains(label, StringComparison.Ordinal));

    /// <summary>The ways out, as pressed.</summary>
    public static TheoryData<string> WaysOut => new("esc", "right-click", "tab", "shift-tab", "rail", "ctrl-p", "ctrl-q");

    private static void Leave(DrawnShell drawn, string way)
    {
        switch (way)
        {
            case "esc":
                drawn.Press(Key.Esc);
                break;

            case "right-click":
                drawn.RightClick(drawn.Window.ComposeField<ComposeEditor>().FrameToScreen().X, Tall / 2);
                break;

            case "tab":
                drawn.Press(Key.Tab);
                break;

            case "shift-tab":
                drawn.Press(Key.Tab.WithShift);
                break;

            case "rail":
                drawn.Click(OverRail, RailRowOf(drawn, "Notifications"));
                break;

            case "ctrl-p":
                drawn.Press(Key.P.WithCtrl);
                break;

            case "ctrl-q":
                drawn.Press(Key.Q.WithCtrl);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(way), way, null);
        }
    }

    /// <summary>
    ///     A fresh empty compose, a reply holding only its mention and the warning it opened on, and an unchanged edit are
    ///     all untouched: <c>esc</c> leaves each without asking.
    /// </summary>
    [Theory]
    [InlineData(ComposeFor.Post)]
    [InlineData(ComposeFor.Reply)]
    [InlineData(ComposeFor.Edit)]
    public async Task AnUntouchedCompose_LeavesWithoutAsking(ComposeFor purpose)
    {
        using var drawn = await Composing(purpose);
        var compose = Assert.IsType<ComposeScreen>(drawn.Shell.Screen);

        Assert.False(compose.Touched);

        drawn.Press(Key.Esc);

        Assert.Null(drawn.Shell.Asking);
        Assert.IsNotType<ComposeScreen>(drawn.Shell.Screen);
    }

    /// <summary>
    ///     Typed into and then put back the way it opened, a compose is untouched again: it is what it holds that counts,
    ///     not whether a key was ever pressed.
    /// </summary>
    [Fact]
    public async Task ACompose_PutBackAsItOpened_IsUntouched()
    {
        using var drawn = await Composing();

        Type(drawn, "a");
        drawn.Press(Key.Backspace);

        Assert.False(Assert.IsType<ComposeScreen>(drawn.Shell.Screen).Touched);
    }

    /// <summary>Changing only the warning, the visibility or the language counts as touched, as the text does.</summary>
    [Theory]
    [InlineData("text")]
    [InlineData("warning")]
    [InlineData("visibility")]
    [InlineData("language")]
    public async Task ChangingAnyOneField_Touches(string field)
    {
        using var drawn = await Composing(ComposeFor.Reply);
        var compose = Assert.IsType<ComposeScreen>(drawn.Shell.Screen);

        switch (field)
        {
            case "text":
                Type(drawn, "hi");
                break;

            case "warning":
                drawn.Press(Key.W.WithCtrl);
                Type(drawn, "!");
                break;

            case "visibility":
                drawn.Shell.ChangeCompose(screen => screen.TypeInto(ComposeField.To));
                drawn.Redraw();
                drawn.Press(Key.CursorRight);
                break;

            case "language":
                drawn.Shell.ChangeCompose(screen => screen.TypeInto(ComposeField.Lang));
                drawn.Redraw();
                Type(drawn, "fr");
                break;
        }

        Assert.True(compose.Touched, field);

        // Back to the post, so that esc is a way out rather than the close of a list a field opened.
        drawn.Shell.ChangeCompose(screen => screen.TypeInto(ComposeField.Post));
        drawn.Redraw();
        drawn.Press(Key.Esc);

        Assert.NotNull(drawn.Shell.Asking);
        Assert.Same(compose, drawn.Shell.Screen);
    }

    /// <summary>
    ///     On a touched compose every way out asks on the status row, and leaves nothing: the draft stands in front, the
    ///     rail is where it was, no profiles screen is opened and nothing quits.
    /// </summary>
    [Theory]
    [MemberData(nameof(WaysOut))]
    public async Task EveryWayOut_Asks(string way)
    {
        var quits = 0;
        using var drawn = await Composing(quit: () => quits++);
        var compose = drawn.Shell.Screen;

        Type(drawn, "sheep");
        Leave(drawn, way);
        drawn.Settle();

        Assert.Equal(Asked, Status(drawn));
        Assert.Same(compose, drawn.Shell.Screen);
        Assert.Equal(0, drawn.Shell.Rail.Cursor);
        Assert.Equal(0, drawn.Shell.Rail.Current);
        Assert.Equal(0, quits);
        Assert.Equal("sheep", drawn.Window.ComposeField<ComposeEditor>().Text);
    }

    /// <summary>
    ///     <c>y</c> throws the draft away and finishes the way out it interrupted — once: <c>tab</c> moves one
    ///     destination, not two. A step of the rail arrives there and then, with no settle to wait out, so the screen
    ///     under the draft is never what is in front between the answer and the destination.
    /// </summary>
    [Theory]
    [MemberData(nameof(WaysOut))]
    public async Task Y_DiscardsAndGoesThatWayOnce(string way)
    {
        var quits = 0;
        using var drawn = await Composing(quit: () => quits++);

        Type(drawn, "sheep");
        Leave(drawn, way);
        drawn.Press(Key.Y);

        switch (way)
        {
            case "tab":
                Assert.Equal(1, drawn.Shell.Rail.Current);
                break;

            case "shift-tab":
                Assert.Equal(drawn.Shell.Rail.Destinations.Count - 1, drawn.Shell.Rail.Current);
                break;
        }

        drawn.Built.Host.SettleAll();
        drawn.Redraw();

        Assert.Null(drawn.Shell.Asking);

        switch (way)
        {
            case "esc" or "right-click":
                Assert.IsType<FeedScreen>(drawn.Shell.Screen);
                Assert.Equal(1, drawn.Shell.Depth);
                break;

            case "tab":
                Assert.Equal(1, drawn.Shell.Rail.Current);
                Assert.Equal(1, drawn.Shell.Depth);
                break;

            case "shift-tab":
                Assert.Equal(drawn.Shell.Rail.Destinations.Count - 1, drawn.Shell.Rail.Current);
                Assert.Equal(1, drawn.Shell.Depth);
                break;

            case "rail":
                Assert.Equal(DestinationKind.Notifications, drawn.Shell.Rail.Showing.Kind);
                Assert.Equal(1, drawn.Shell.Depth);
                break;

            case "ctrl-p":
                Assert.IsType<ProfilesScreen>(drawn.Shell.Screen);
                Assert.Equal(["Home", "Profiles"], drawn.Shell.Crumbs);
                break;

            case "ctrl-q":
                Assert.Equal(1, quits);
                break;
        }

        if (way != "ctrl-q")
        {
            Assert.DoesNotContain(drawn.Shell.Crumbs, crumb => crumb == "Compose");
        }
    }

    /// <summary>
    ///     The way out taken again does not agree: <c>esc esc</c>, <c>tab tab</c> and the rest keep the draft, the
    ///     screen and the rail where they were, so a key pressed twice never throws a draft away.
    /// </summary>
    [Theory]
    [MemberData(nameof(WaysOut))]
    public async Task TheSameWayAgain_KeepsTheDraft(string way)
    {
        var quits = 0;
        using var drawn = await Composing(quit: () => quits++);
        var compose = drawn.Shell.Screen;

        Type(drawn, "sheep");
        Leave(drawn, way);
        Leave(drawn, way);
        drawn.Built.Host.SettleAll();
        drawn.Redraw();

        Assert.Null(drawn.Shell.Asking);
        Assert.Same(compose, drawn.Shell.Screen);
        Assert.Equal(0, drawn.Shell.Rail.Cursor);
        Assert.Equal(0, drawn.Shell.Rail.Current);
        Assert.Equal(0, quits);
        Assert.Equal("sheep", drawn.Window.ComposeField<ComposeEditor>().Text);
    }


    /// <summary>
    ///     Any other key keeps the draft and the screen, and does nothing else — not even what it would have done with no
    ///     question open: a letter is not typed, and a stray <c>tab</c> after an <c>esc</c> moves nothing.
    /// </summary>
    [Theory]
    [InlineData("n")]
    [InlineData("x")]
    [InlineData("tab")]
    [InlineData("enter")]
    public async Task AnyOtherKey_KeepsTheDraftAndDoesNothingElse(string pressed)
    {
        using var drawn = await Composing();
        var compose = drawn.Shell.Screen;
        var editor = drawn.Window.ComposeField<ComposeEditor>();

        Type(drawn, "sheep");
        drawn.Press(Key.Esc);

        drawn.Press(pressed switch
        {
            "tab" => Key.Tab,
            "enter" => Key.Enter,
            _ => new Key(pressed[0]),
        });
        drawn.Settle();

        Assert.Null(drawn.Shell.Asking);
        Assert.Same(compose, drawn.Shell.Screen);
        Assert.Equal("sheep", editor.Text);
        Assert.Equal(0, drawn.Shell.Rail.Cursor);
        Assert.Equal(0, drawn.Shell.Rail.Current);
        Assert.DoesNotContain("Discard", Status(drawn), StringComparison.Ordinal);
    }

    /// <summary>
    ///     A click or a wheel notch over the draft while the question is open is a press the question does not take, as
    ///     anywhere else (open questions win): it keeps the draft, and the editor neither moves its caret nor scrolls.
    /// </summary>
    [Theory]
    [InlineData("click")]
    [InlineData("wheel")]
    public async Task APointerOverTheDraft_KeepsItAndDoesNothingElse(string pointer)
    {
        using var drawn = await Composing();
        var compose = drawn.Shell.Screen;
        var editor = drawn.Window.ComposeField<ComposeEditor>();
        var at = editor.FrameToScreen();

        Type(drawn, "sheep");
        drawn.Press(Key.Esc);

        var caret = editor.CurrentColumn;

        if (pointer == "click")
        {
            drawn.Point(at.X, at.Y, MouseFlags.LeftButtonPressed);
            drawn.Point(at.X, at.Y, MouseFlags.LeftButtonReleased);
            drawn.Click(at.X, at.Y);
        }
        else
        {
            drawn.Wheel(at.X, at.Y);
        }

        drawn.Settle();

        Assert.Null(drawn.Shell.Asking);
        Assert.Same(compose, drawn.Shell.Screen);
        Assert.Equal("sheep", editor.Text);
        Assert.Equal(caret, editor.CurrentColumn);
    }

    /// <summary>The question asked from the warning field is answered there too, and the field takes no letter of it.</summary>
    [Fact]
    public async Task FromTheWarningField_TheQuestionIsAnsweredAndNothingIsTyped()
    {
        using var drawn = await Composing();
        var warning = drawn.Window.ComposeField<ComposeWarningField>();

        drawn.Press(Key.W.WithCtrl);
        Type(drawn, "cw");
        drawn.Press(Key.Esc);

        Assert.Equal(Asked, Status(drawn));

        drawn.Press(new Key('x'));

        Assert.Equal("cw", warning.Text);
        Assert.IsType<ComposeScreen>(drawn.Shell.Screen);

        drawn.Press(Key.Esc);
        drawn.Press(Key.Y);

        Assert.IsType<FeedScreen>(drawn.Shell.Screen);
    }

    /// <summary>
    ///     On an untouched compose the other ways out go at once, as they always have — a step of the rail arriving
    ///     there and then too, with no settle to wait out, so the screen under the draft is never what is in front.
    /// </summary>
    [Theory]
    [MemberData(nameof(WaysOut))]
    public async Task OnAnUntouchedCompose_EveryWayOutGoesAtOnce(string way)
    {
        var quits = 0;
        using var drawn = await Composing(quit: () => quits++);

        Leave(drawn, way);

        switch (way)
        {
            case "tab":
                Assert.Equal(1, drawn.Shell.Rail.Current);
                break;

            case "shift-tab":
                Assert.Equal(drawn.Shell.Rail.Destinations.Count - 1, drawn.Shell.Rail.Current);
                break;
        }

        drawn.Built.Host.SettleAll();
        drawn.Redraw();

        Assert.Null(drawn.Shell.Asking);

        if (way == "ctrl-q")
        {
            Assert.Equal(1, quits);
        }
        else
        {
            Assert.DoesNotContain(drawn.Shell.Crumbs, crumb => crumb == "Compose");
        }
    }

    /// <summary>
    ///     Only the step that takes the draft off arrives at once: tabbing on from where it arrived settles as ever, the
    ///     cursor moving ahead of the selection.
    /// </summary>
    [Fact]
    public async Task TabbingOnFromWhereADraftWasLeft_Settles()
    {
        using var drawn = await Composing();

        drawn.Press(Key.Tab);
        drawn.Press(Key.Tab);

        Assert.Equal(2, drawn.Shell.Rail.Cursor);
        Assert.Equal(1, drawn.Shell.Rail.Current);

        drawn.Built.Host.SettleAll();

        Assert.Equal(2, drawn.Shell.Rail.Current);
    }

    /// <summary>A sent post leaves without asking: sending is not throwing it away.</summary>
    [Fact]
    public async Task Sending_DoesNotAsk()
    {
        using var drawn = await Composing();

        Type(drawn, "sheep");
        drawn.Press(Key.S.WithCtrl);
        drawn.Settle();

        Assert.Null(drawn.Shell.Asking);
        Assert.IsType<FeedScreen>(drawn.Shell.Screen);
        Assert.Single(drawn.Built.Author.Published);
    }
}
