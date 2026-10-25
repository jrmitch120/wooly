using Terminal.Gui.Input;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     <c>Home</c> and <c>End</c> take the page to the top of the list and the foot of it, and leave the pick where it
///     was, as <c>↓</c>, <c>↑</c>, <c>PgUp</c> and <c>PgDn</c> do — on a Mac they are fn-← and fn-→, and a reader
///     there asked for them not to carry the pick along (asked in review of #480). Pressed on the whole shell drawn
///     headless, and read back off the page and what is picked.
/// </summary>
public class HomeEndPageTests
{
    /// <summary>Short enough that six posts run well past the foot of the page.</summary>
    private const int Short = 16;

    /// <summary><c>End</c> leaves the list's last row on the page's last row, and the first post picked.</summary>
    [Fact]
    public async Task End_TakesThePageToTheFootOfTheListAndLeavesThePick()
    {
        using var drawn = await Feed(Six());

        drawn.Press(Key.End);

        Assert.Equal(RowsOf(drawn) - drawn.Content.Viewport.Height, drawn.Content.Top);
        Assert.Equal("110", drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>
    ///     <c>Home</c> takes the page back to the top of the list from wherever <c>k</c> walked it, and leaves the pick
    ///     on the last post, now off the foot of the page.
    /// </summary>
    [Fact]
    public async Task Home_TakesThePageToTheTopOfTheListAndLeavesThePick()
    {
        using var drawn = await Feed(Six());

        for (var walked = 0; walked < 5; walked++)
        {
            drawn.Press(Key.K);
        }

        Assert.NotEqual(0, drawn.Content.Top);

        drawn.Press(Key.Home);

        Assert.Equal(0, drawn.Content.Top);
        Assert.Equal("660", drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>
    ///     With the pick left behind at the top, the next <c>j</c> after <c>End</c> reclaims from the page it came to —
    ///     the first post whose content starts on it (#472, asked in review of #480) — rather than stepping from the
    ///     post the reader can no longer see.
    /// </summary>
    [Fact]
    public async Task J_AfterEndReclaimsFromThePage()
    {
        using var drawn = await Feed(Six());

        drawn.Press(Key.End);
        drawn.Press(Key.J);

        // The fifth post, whose byline the foot of six scrolls off but whose text is on its second row — not the first,
        // which is where a step back from the pick left at the top would have stayed.
        Assert.Equal("550", drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>
    ///     <c>End</c> on a list that reads on takes the page within a screenful of its end, and so reads the next posts
    ///     on, as walking or scrolling there does (#469) — the pick still on the first post.
    /// </summary>
    [Fact]
    public async Task End_ReadsTheListOn()
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.ReadingOn(
                [.. Enumerable.Range(1, 100).Select(at => APost.With(id: $"{at}"))]),
        };

        using var drawn = await DrawnShell.Of(80, 20, Themes.Plain, built);

        drawn.Press(Key.End);
        drawn.Settle();

        Assert.Single(built.Timelines.ReadsOn);
        Assert.Equal("1", drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>Six posts, from 110 up to 660.</summary>
    private static Post[] Six() =>
        [.. new[] { "110", "220", "330", "440", "550", "660" }.Select(id => APost.With(id: id))];

    /// <summary>The shell drawn short over a feed of <paramref name="posts" />.</summary>
    private static Task<DrawnShell> Feed(Post[] posts) =>
        DrawnShell.Of(80, Short, Themes.Plain, new AShell { Timelines = FakeTimelineReader.Holding(posts) });

    /// <summary>How many rows the screen on top draws, at the width the content region draws them.</summary>
    private static int RowsOf(DrawnShell drawn) =>
        drawn.Shell.Screen.Lines(new Drawing(drawn.Content.Viewport.Width, drawn.Built.Clock.GetUtcNow())).Count;
}
