using Wooly.Core.Configuration;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     What the <b>Scrollbar</b> tests draw it on (#500–#503): a home timeline of numbered posts, as many as it takes to
///     overflow the page or not, and how many rows the screen on top draws — which is what decides whether there is a
///     bar at all.
/// </summary>
internal static class ScrollbarFeeds
{
    /// <summary>Posts from 1 up to <paramref name="count" />.</summary>
    public static Post[] Posts(int count) =>
        [.. Enumerable.Range(1, count).Select(at => APost.With(id: $"{at}"))];

    /// <summary>
    ///     The shell drawn <paramref name="height" /> rows tall at 80 columns over a timeline holding
    ///     <paramref name="posts" />, its scrollbar in <paramref name="style" /> (#505) — the default where unsaid.
    /// </summary>
    public static Task<DrawnShell> Feed(
        Post[] posts,
        int height = 24,
        ITheme? theme = null,
        ScrollbarStyle style = ScrollbarStyle.Block) =>
        DrawnShell.Of(
            80,
            height,
            theme ?? Themes.Plain,
            new AShell { Timelines = FakeTimelineReader.Holding(posts), Scrollbar = style });

    /// <summary>How many rows the screen on top draws, at the width the content region draws them.</summary>
    public static int RowsOf(DrawnShell drawn) =>
        drawn.Shell.Screen.Lines(new Drawing(drawn.Content.Viewport.Width - 1, drawn.Built.Clock.GetUtcNow())).Count;
}
