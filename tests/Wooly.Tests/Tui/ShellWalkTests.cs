using Wooly.Tests.Fakes;

namespace Wooly.Tests.Tui;

/// <summary>
///     Walking posts with <c>j</c> and <c>k</c>, once the arrows have been given the job of walking rows (#51). The
///     press asks two things at once — move, or reclaim a selection the reader has scrolled away from — and which of
///     the two it is depends on what the view can see, so the view is what says and this is what acts on it.
/// </summary>
public class ShellWalkTests
{
    /// <summary>The ordinary case: the selection is on the page, and <c>j</c> moves from it.</summary>
    [Theory]
    [InlineData(1, "330")]
    [InlineData(-1, "110")]
    public async Task Walk_MovesFromTheSelectionWhileItIsStillOnScreen(int by, string expected)
    {
        var opened = await Feed();

        opened.Walk(1, reclaiming: null);
        opened.Walk(by, reclaiming: null);

        Assert.Equal(expected, opened.Screen.Picked?.Id);
    }

    /// <summary>
    ///     A reader who has scrolled somewhere and presses <c>j</c> carries on from what they are looking at, rather
    ///     than being thrown back to a post that is no longer on the page.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public async Task Walk_ReclaimsTheTopmostPostWhereTheSelectionHasScrolledOffTheScreen(int by)
    {
        var opened = await Feed();

        opened.Walk(by, reclaiming: 2);

        Assert.Equal("330", opened.Screen.Picked?.Id);
    }

    /// <summary>And the press after that moves from what was reclaimed, which is where the reader now is.</summary>
    [Fact]
    public async Task Walk_MovesOnFromThePostItReclaimed()
    {
        var opened = await Feed();

        opened.Walk(1, reclaiming: 1);
        opened.Walk(1, reclaiming: null);

        Assert.Equal("330", opened.Screen.Picked?.Id);
    }

    /// <summary>A feed of four posts, opened, with the first of them picked out.</summary>
    private static Task<Wooly.Tui.Shell.Shell> Feed() =>
        new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "110"),
                APost.With(id: "220"),
                APost.With(id: "330"),
                APost.With(id: "440")),
        }.Opened();
}
