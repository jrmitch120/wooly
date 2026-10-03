using Terminal.Gui.Input;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;

namespace Wooly.Tests.Tui;

/// <summary>
///     The mouse on the whole shell, sent at a cell of a headless terminal and read back as a reader would see it: what
///     is picked, where the page begins, and what the rail says (#286). Every gesture ends in a move the keys already
///     make, so each is asserted against the key it stands in for, on the same shell.
/// </summary>
public class ShellPointerTests
{
    /// <summary>A column inside the content panel, clear of its left edge at any terminal these tests draw.</summary>
    private const int OverContent = RailLines.Width + 4;

    /// <summary>A column inside the rail, clear of its left edge.</summary>
    private const int OverRail = RailLines.Width / 2;

    /// <summary>A row inside both panels, below their top edges at any terminal these tests draw.</summary>
    private const int Inside = 3;

    /// <summary>
    ///     A wheel notch moves the page exactly as far as one <c>↓</c> or <c>↑</c> does: the window's three-row step,
    ///     no more and no less (#287).
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ANotchLeavesThePageWhereOneArrowWould(bool down)
    {
        using var drawn = await Four();

        // Two rows' worth down first, so that a notch up has somewhere to go.
        drawn.Press(Key.CursorDown);
        drawn.Press(Key.CursorDown);

        var from = drawn.Content.Top;

        drawn.Wheel(OverContent, Inside, down);

        var wheeled = drawn.Content.Top;

        // Back to where it was, and the key in its place.
        drawn.Press(down ? Key.CursorUp : Key.CursorDown);

        Assert.Equal(from, drawn.Content.Top);

        drawn.Press(down ? Key.CursorDown : Key.CursorUp);

        Assert.Equal(wheeled, drawn.Content.Top);
        Assert.NotEqual(from, wheeled);
    }

    /// <summary>The wheel moves the page and nothing else: what the keys act on stays where it was put.</summary>
    [Fact]
    public async Task TheWheelNeverMovesThePick()
    {
        using var drawn = await Four();

        drawn.Press(Key.K);

        Notches(drawn, 30);

        Assert.Equal("220", drawn.Shell.Screen.Picked?.Id);

        Notches(drawn, 30, down: false);

        Assert.Equal("220", drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>
    ///     And the state it leaves is the one the arrows leave: with the pick wheeled off the page, <c>j</c> and
    ///     <c>k</c> take back the topmost post on it rather than stepping on from one nobody can see — the same post
    ///     the same press takes after the arrows have carried the page as far.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task JAndK_ReclaimWhatTheyWouldAfterTheArrows(bool next)
    {
        using var wheeled = await Four();
        using var arrowed = await Four();

        Notches(wheeled, 6);

        for (var pressed = 0; pressed < 6; pressed++)
        {
            arrowed.Press(Key.CursorDown);
        }

        Assert.Equal(arrowed.Content.Top, wheeled.Content.Top);

        wheeled.Press(next ? Key.K : Key.J);
        arrowed.Press(next ? Key.K : Key.J);

        // Off the first post, which is where a step from the pick the page left behind would have stayed or landed.
        Assert.NotEqual("110", wheeled.Shell.Screen.Picked?.Id);
        Assert.Equal(arrowed.Shell.Screen.Picked?.Id, wheeled.Shell.Screen.Picked?.Id);
    }

    /// <summary>The wheel stops at the ends of the list rather than scrolling into nothing, as the arrows do.</summary>
    [Fact]
    public async Task TheWheelClampsAtEitherEnd()
    {
        using var drawn = await Four();

        drawn.Wheel(OverContent, Inside, down: false);

        Assert.Equal(0, drawn.Content.Top);

        Notches(drawn, 50);

        var foot = drawn.Content.Top;

        Assert.True(foot > 0);

        drawn.Wheel(OverContent, Inside);

        Assert.Equal(foot, drawn.Content.Top);

        drawn.Press(Key.CursorDown);

        Assert.Equal(foot, drawn.Content.Top);
    }

    /// <summary>
    ///     A post whose pictures are taller than the terminal is read to its foot by the wheel alone — the post the
    ///     three-row step exists for (#51).
    /// </summary>
    [Fact]
    public async Task TheWheelReachesTheFootOfAPostTallerThanTheTerminal()
    {
        var pictures = FakePictures.With()
            .Holding("m1", 800, 600)
            .Holding("m2", 800, 600)
            .Holding("m3", 800, 600);

        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(
                    id: "110",
                    content: "Three sheep",
                    media: [APost.APicture("m1"), APost.APicture("m2"), APost.APicture("m3", "The last sheep")]),
                APost.With(id: "220", content: "Underneath")),
        };

        using var drawn = await DrawnShell.Of(80, 24, Themes.Plain, built, pictures: pictures);

        Assert.DoesNotContain(drawn.Rows(), row => row.Contains("Underneath"));

        // Past the foot of the tall post, onto the one below it, a notch at a time.
        var notches = 0;

        while (notches < 30 && !drawn.Rows().Any(row => row.Contains("Underneath")))
        {
            drawn.Wheel(OverContent, Inside);
            notches++;
        }

        Assert.Contains(drawn.Rows(), row => row.Contains("Underneath"));
        Assert.Equal("110", drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>
    ///     A wheel over a picture scrolls the page like a wheel anywhere else in the content. Terminal.Gui's image view
    ///     takes the wheel for a zoom of its own, which left the picture growing in its box and the page standing still.
    /// </summary>
    [Fact]
    public async Task AWheelOverAPictureScrollsThePageAndLeavesThePictureAlone()
    {
        var pictures = FakePictures.With().Holding("m1", 800, 600);

        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "110", media: [APost.APicture("m1")]),
                APost.With(id: "220"),
                APost.With(id: "330")),
        };

        using var drawn = await DrawnShell.Of(80, 24, Themes.Plain, built, pictures: pictures, drawsPictures: true);

        var box = Assert.Single(drawn.Content.SubViews.OfType<PictureView>(), view => view.Visible);
        var over = box.FrameToScreen();

        drawn.Wheel(over.X + (over.Width / 2), over.Y + (over.Height / 2));

        Assert.Equal(1, box.ZoomLevel);
        Assert.Equal(3, drawn.Content.Top);
    }

    /// <summary>
    ///     A wheel over the rail changes nothing at all: not the rail's cursor, not what it has selected, not how far
    ///     it is scrolled, and not the page beside it.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AWheelOverTheRailChangesNothing(bool down)
    {
        // Short enough that the rail is compact, and its cursor far enough down it to have scrolled it — so that it
        // has a scroll to keep.
        using var drawn = await Four(height: 12);

        drawn.Shell.Rail.GoTo(DestinationKind.Profile);
        drawn.Built.Host.Drain();
        drawn.Redraw();
        drawn.Press(Key.CursorDown);

        Assert.DoesNotContain(drawn.Rail(), row => row.Contains("Home", StringComparison.Ordinal));

        var rail = drawn.Rail();
        var cursor = drawn.Shell.Rail.Cursor;
        var current = drawn.Shell.Rail.Current;
        var top = drawn.Content.Top;
        var picked = drawn.Shell.Screen.Picked?.Id;

        Notches(drawn, 5, down, OverRail);

        Assert.Equal(rail, drawn.Rail());
        Assert.Equal(cursor, drawn.Shell.Rail.Cursor);
        Assert.Equal(current, drawn.Shell.Rail.Current);
        Assert.Equal(top, drawn.Content.Top);
        Assert.Equal(picked, drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>The help screen is often taller than the terminal, and the wheel reads down it as <c>↓</c> does.</summary>
    [Fact]
    public async Task TheWheelScrollsTheHelpScreen()
    {
        using var drawn = await Four(height: 12);

        drawn.Press(new Key('?'));

        Assert.IsType<HelpScreen>(drawn.Shell.Screen);

        drawn.Wheel(OverContent, Inside);

        var wheeled = drawn.Content.Top;

        drawn.Press(Key.CursorUp);
        drawn.Press(Key.CursorDown);

        Assert.True(wheeled > 0);
        Assert.Equal(wheeled, drawn.Content.Top);
    }

    /// <summary>
    ///     And a notice: the rail's hashtag with none named, wrapped narrow enough to be taller than the room it has.
    /// </summary>
    [Fact]
    public async Task TheWheelScrollsANotice()
    {
        using var drawn = await Four(width: 36, height: 8);

        drawn.Shell.Rail.GoTo(DestinationKind.Hashtag);
        drawn.Built.Host.Drain();
        drawn.Redraw();

        Assert.IsType<NoticeScreen>(drawn.Shell.Screen);

        drawn.Wheel(OverContent, Inside);

        var wheeled = drawn.Content.Top;

        drawn.Press(Key.CursorUp);
        drawn.Press(Key.CursorDown);

        Assert.True(wheeled > 0);
        Assert.Equal(wheeled, drawn.Content.Top);
    }

    /// <summary>
    ///     Over the compose editor the wheel is the editor's own, as Terminal.Gui handles it: a long draft scrolls its
    ///     text. Verified rather than built (#286).
    /// </summary>
    [Fact]
    public async Task TheWheelOverTheComposeEditorScrollsItsText()
    {
        using var drawn = await Four();

        drawn.Shell.Compose();
        drawn.Redraw();

        var editor = drawn.Window.SubViews.OfType<ComposeEditor>().Single();

        editor.Text = string.Join('\n', Enumerable.Range(1, 60).Select(line => $"Line {line}"));
        editor.MoveHome();
        drawn.Redraw();

        Assert.Equal(0, editor.Viewport.Y);

        drawn.Wheel(OverContent, Inside + 2);

        Assert.True(editor.Viewport.Y > 0);
        Assert.IsType<ComposeScreen>(drawn.Shell.Screen);
    }

    /// <summary>
    ///     Nothing turns the mouse off: the reader's terminal keeps reporting it, and selecting text goes through the
    ///     terminal's own modifier bypass until selection is a feature of its own.
    /// </summary>
    [Fact]
    public async Task MouseTrackingStaysOn()
    {
        using var drawn = await Four();

        Assert.False(drawn.Application.Mouse.IsMouseDisabled);
    }

    /// <summary>
    ///     And nothing in the TUI reaches for the switch: the window above is the one a test builds, and the run that
    ///     a reader starts is built elsewhere, so the rule is held over the source as well.
    /// </summary>
    [Fact]
    public void NothingInTheTuiTurnsTheMouseOff()
    {
        var sources = Directory.EnumerateFiles(
                Path.Combine(RepositoryRoot.Path, "src", "Wooly.Tui"),
                "*.cs",
                SearchOption.AllDirectories)
            .ToList();

        Assert.Contains(sources, file => file.EndsWith("ShellWindow.cs", StringComparison.Ordinal));
        Assert.DoesNotContain(sources, file => File.ReadAllText(file).Contains("IsMouseDisabled", StringComparison.Ordinal));
    }

    /// <summary>The keymap says in one line, among the frame's keys, what the mouse does.</summary>
    [Fact]
    public async Task Help_SaysWhatTheMouseDoesInOneLine()
    {
        var opened = await new AShell().Opened();

        var everywhere = new HelpScreen(opened.Screen).Lines(new Drawing(80, AShell.Now))
            .Select(line => line.Text)
            .SkipWhile(text => text != "Everywhere")
            .ToList();

        var mouse = Assert.Single(everywhere, text => text.StartsWith("mouse ", StringComparison.Ordinal));

        Assert.Contains("wheel", mouse, StringComparison.Ordinal);
    }

    /// <summary>
    ///     A notch is the arrow it stands for with a question open too: anything but the agreeing key declines one, so
    ///     a notch declines it and scrolls nothing behind it, as <c>↓</c> does (story 43).
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ANotchDeclinesAnOpenQuestionAsTheArrowDoes(bool wheel)
    {
        var answers = Enumerable.Range(1, 3).Select(at => APost.AnAnswer($"Answer {at}", at)).ToList();

        using var drawn = await DrawnShell.Of(
            80,
            12,
            Themes.Plain,
            new AShell
            {
                Timelines = FakeTimelineReader.Holding(
                    APost.With(id: "110", poll: APost.APoll(options: answers)),
                    APost.With(id: "220")),
            });

        drawn.Press(new Key('2'));
        drawn.Press(Key.V);

        Assert.NotNull(drawn.Shell.Asking);

        if (wheel)
        {
            drawn.Wheel(OverContent, Inside);
        }
        else
        {
            drawn.Press(Key.CursorDown);
        }

        Assert.Null(drawn.Shell.Asking);
        Assert.Equal(0, drawn.Content.Top);
        Assert.Empty(drawn.Built.Engagement.Votes);
    }

    /// <summary><paramref name="count" /> notches of the wheel over the content, or wherever <paramref name="column" /> says.</summary>
    private static void Notches(DrawnShell drawn, int count, bool down = true, int column = OverContent)
    {
        for (var notch = 0; notch < count; notch++)
        {
            drawn.Wheel(column, Inside, down);
        }
    }

    /// <summary>Four posts on a terminal of the given size, drawn.</summary>
    private static Task<DrawnShell> Four(int width = 80, int height = 24) =>
        DrawnShell.Of(
            width,
            height,
            Themes.Plain,
            new AShell
            {
                Timelines = FakeTimelineReader.Holding(
                    APost.With(id: "110"),
                    APost.With(id: "220"),
                    APost.With(id: "330"),
                    APost.With(id: "440")),
            });
}
