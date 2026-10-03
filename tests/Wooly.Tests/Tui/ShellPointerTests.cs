using Terminal.Gui.Input;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
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
    /// <summary>A column well inside the content panel at an 80-column terminal, clear of its edges.</summary>
    private const int OverContent = 40;

    /// <summary>A column inside the rail.</summary>
    private const int OverRail = 5;

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

        drawn.Wheel(OverContent, 10, down);

        var wheeled = drawn.Content.Top;

        // Back to where it was, and the key in its place.
        drawn.Press(down ? Key.CursorUp : Key.CursorDown);

        Assert.Equal(from, drawn.Content.Top);

        drawn.Press(down ? Key.CursorDown : Key.CursorUp);

        Assert.Equal(drawn.Content.Top, wheeled);
        Assert.NotEqual(from, wheeled);
    }

    /// <summary>The wheel moves the page and nothing else: what the keys act on stays where it was put.</summary>
    [Fact]
    public async Task TheWheelNeverMovesThePick()
    {
        using var drawn = await Four();

        drawn.Press(Key.K);

        for (var notch = 0; notch < 30; notch++)
        {
            drawn.Wheel(OverContent, 10);
        }

        Assert.Equal("220", drawn.Shell.Screen.Picked?.Id);

        for (var notch = 0; notch < 30; notch++)
        {
            drawn.Wheel(OverContent, 10, down: false);
        }

        Assert.Equal("220", drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>
    ///     And the state it leaves is the one the arrows leave: with the pick wheeled off the page, <c>k</c> takes back
    ///     the topmost post on it rather than stepping on from one nobody can see.
    /// </summary>
    [Fact]
    public async Task K_ReclaimsThePostOnThePageAfterTheWheelHasCarriedThePickOff()
    {
        using var drawn = await Four();

        for (var notch = 0; notch < 30; notch++)
        {
            drawn.Wheel(OverContent, 10);
        }

        drawn.Press(Key.K);

        Assert.Equal("440", drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>The wheel stops at the ends of the list rather than scrolling into nothing, as the arrows do.</summary>
    [Fact]
    public async Task TheWheelClampsAtEitherEnd()
    {
        using var drawn = await Four();

        drawn.Wheel(OverContent, 10, down: false);

        Assert.Equal(0, drawn.Content.Top);

        for (var notch = 0; notch < 50; notch++)
        {
            drawn.Wheel(OverContent, 10);
        }

        var foot = drawn.Content.Top;

        Assert.True(foot > 0);

        drawn.Wheel(OverContent, 10);

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
            drawn.Wheel(OverContent, 10);
            notches++;
        }

        Assert.Contains(drawn.Rows(), row => row.Contains("Underneath"));
        Assert.Equal("110", drawn.Shell.Screen.Picked?.Id);
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
        // Short enough that the rail is compact and scrolled, so that it has a scroll to keep.
        using var drawn = await Four(height: 12);

        drawn.Press(Key.CursorDown);

        var rail = drawn.Rail();
        var cursor = drawn.Shell.Rail.Cursor;
        var current = drawn.Shell.Rail.Current;
        var top = drawn.Content.Top;
        var picked = drawn.Shell.Screen.Picked?.Id;

        for (var notch = 0; notch < 5; notch++)
        {
            drawn.Wheel(OverRail, 3, down);
        }

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

        drawn.Wheel(OverContent, 5);

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

        drawn.Wheel(30, 4);

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

        drawn.Wheel(OverContent, 10);

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
