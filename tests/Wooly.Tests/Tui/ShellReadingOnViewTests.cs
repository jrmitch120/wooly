using Terminal.Gui.Input;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     Reading on started by the view, whatever moved it, and not only by the pick (#461): <c>↓</c>, <c>PgDn</c> and the
///     wheel move the page and leave the pick where it was, and a page come within a screenful of the end of Home reads
///     on all the same. On a real window drawn on a headless terminal, because the page is settled inside the draw and
///     the foot row is only worth saying where it reaches the terminal.
/// </summary>
public class ShellReadingOnViewTests
{
    /// <summary>
    ///     <c>↓</c>, <c>PgDn</c> and the wheel ask nothing while the page is further than a screenful from the end, and
    ///     read on the first time it comes within one — with the pick still on the first post, where it was before the
    ///     page moved.
    /// </summary>
    [Theory]
    [InlineData("↓")]
    [InlineData("PgDn")]
    [InlineData("wheel")]
    public async Task MovingThePage_ReadsOnOnceItIsWithinAScreenfulOfTheEndAndLeavesThePick(string pressed)
    {
        using var drawn = await Laid(Enumerable.Range(1, 100));

        var picked = drawn.Shell.Screen.Picked?.Id;

        Moved(drawn, pressed);

        Assert.Single(drawn.Built.Timelines.ReadsOn);
        Assert.Equal("1", picked);
        Assert.Equal(picked, drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>
    ///     Moving the page on while the read is out asks nothing more — one read on at a time — and the foot row saying
    ///     so is on the terminal under the last post held, not off the page or under the status row.
    /// </summary>
    [Theory]
    [InlineData("↓")]
    [InlineData("PgDn")]
    [InlineData("wheel")]
    public async Task MovingThePageWhileAReadIsOut_AsksNothingMoreAndShowsItReadingOn(string pressed)
    {
        using var drawn = await Laid(Enumerable.Range(1, 100));

        drawn.Built.Timelines.HoldReadsOn();
        Moved(drawn, pressed);

        ToTheFoot(drawn, pressed);

        Assert.Single(drawn.Built.Timelines.ReadsOn);
        Assert.Contains(drawn.Rows(), row => row.Contains(ReadingOn.InFlightSays));
    }

    /// <summary>
    ///     A list read whole asks nothing however far the page goes, and says so under the last post — on the terminal,
    ///     where the reader can see it.
    /// </summary>
    [Theory]
    [InlineData("↓")]
    [InlineData("PgDn")]
    [InlineData("wheel")]
    public async Task MovingThePageOverAWholeList_AsksNothingAndShowsThatsEverything(string pressed)
    {
        using var drawn = await Laid(Enumerable.Range(1, 30));

        ToTheFoot(drawn, pressed);

        Assert.Empty(drawn.Built.Timelines.ReadsOn);
        Assert.Contains(drawn.Rows(), row => row.Contains(ReadingOn.WholeSays));
    }

    /// <summary>
    ///     The page moved down once, by what the shell's docs name: a key pressed, or a notch of the wheel over the
    ///     content.
    /// </summary>
    private static void Move(DrawnShell drawn, string pressed)
    {
        switch (pressed)
        {
            case "↓":
                drawn.Press(Key.CursorDown);

                break;

            case "PgDn":
                drawn.Press(Key.PageDown);

                break;

            case "wheel":
                drawn.Wheel(drawn.Content.Frame.X + 2, 3);

                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(pressed), pressed, "Not a way to move the page.");
        }
    }

    /// <summary>
    ///     Moves the page down by <paramref name="pressed" /> until a read on goes out, proving each time before it that
    ///     the page was still further than a screenful from the end.
    /// </summary>
    private static void Moved(DrawnShell drawn, string pressed)
    {
        for (var moved = 0; moved < 500 && drawn.Built.Timelines.ReadsOn.Count == 0; moved++)
        {
            Assert.True(
                Under(drawn) > Height(drawn),
                $"Nothing was read on with {Under(drawn)} rows under a page of {Height(drawn)}.");

            Move(drawn, pressed);
        }
    }

    /// <summary>Moves the page down by <paramref name="pressed" /> as far as it goes: until a move leaves it where it was.</summary>
    private static void ToTheFoot(DrawnShell drawn, string pressed)
    {
        for (int top = -1, moved = 0; top != drawn.Content.Top && moved < 500; moved++)
        {
            top = drawn.Content.Top;
            Move(drawn, pressed);
        }
    }

    /// <summary>How many rows the page is.</summary>
    private static int Height(DrawnShell drawn) => drawn.Content.Viewport.Height;

    /// <summary>How many of the screen's rows lie under the bottom of the page.</summary>
    private static int Under(DrawnShell drawn) =>
        Rows(drawn).Count - (drawn.Content.Top + Height(drawn));

    /// <summary>Every row the screen on top draws, at the width the region draws them.</summary>
    private static IReadOnlyList<Line> Rows(DrawnShell drawn) =>
        drawn.Shell.Screen.Lines(new Drawing(drawn.Content.Viewport.Width, drawn.Built.Clock.GetUtcNow()));

    /// <summary>A shell on Home, which reads on through the posts named, forty at a time, drawn eighty by twenty.</summary>
    private static Task<DrawnShell> Laid(IEnumerable<int> posts) =>
        DrawnShell.Of(
            80,
            20,
            Themes.Plain,
            new AShell
            {
                Timelines = FakeTimelineReader.ReadingOn([.. posts.Select(at => APost.With(id: $"{at}"))]),
            });
}
