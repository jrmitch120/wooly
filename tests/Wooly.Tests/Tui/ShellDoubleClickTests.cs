using System.Drawing;
using Terminal.Gui.Input;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;
using static Wooly.Tests.Tui.ContentClicks;

namespace Wooly.Tests.Tui;

/// <summary>
///     A double click in the content, sent at a cell of a headless terminal the way Terminal.Gui reports one — the first
///     click on its own, then the pair: it is a click on the row and then <c>⏎</c>, meaning whatever <c>⏎</c> means
///     on the screen in front, including nothing (#291).
/// </summary>
public class ShellDoubleClickTests
{
    /// <summary>
    ///     A double click on the second thing of each screen where <c>⏎</c> opens something lands the shell exactly where
    ///     a click on it and then <c>⏎</c> does: a post, a search result, a conversation, the asker of a follow request,
    ///     a person in a follow list, and a profile switched to.
    /// </summary>
    [Theory]
    [InlineData("feed")]
    [InlineData("search")]
    [InlineData("messages")]
    [InlineData("requests")]
    [InlineData("follows")]
    [InlineData("profiles")]
    public async Task ADoubleClickIsAClickAndThenEnter(string screen)
    {
        using var keyed = await On(screen);
        using var doubled = await On(screen);

        var shown = doubled.Shell.Screen;

        keyed.Click(OverContent, ContentRowOf(keyed, 1));
        keyed.Press(Key.Enter);
        keyed.Settle();

        doubled.DoubleClick(OverContent, ContentRowOf(doubled, 1));
        doubled.Settle();

        Assert.NotSame(shown, doubled.Shell.Screen);
        Assert.Equal(keyed.Shell.Screen.GetType(), doubled.Shell.Screen.GetType());
        Assert.Equal(keyed.Rows(), doubled.Rows());
    }

    /// <summary>
    ///     Where <c>⏎</c> means nothing for what is picked, a double click only picks: the account screen's header block,
    ///     after which the post keys go quiet.
    /// </summary>
    [Fact]
    public async Task WhereEnterMeansNothingADoubleClickOnlyPicks()
    {
        using var drawn = await On("account");

        drawn.Press(Key.K);

        var account = drawn.Shell.Screen;

        drawn.DoubleClick(OverContent, ContentRowOf(drawn, 0));
        drawn.Settle();

        Assert.Same(account, drawn.Shell.Screen);
        Assert.Null(drawn.Shell.Screen.Picked);
    }

    /// <summary>
    ///     A double click on a post with a hashtag walked to inside it opens the post, since the click lets the
    ///     reference go before the <c>⏎</c>.
    /// </summary>
    [Fact]
    public async Task ADoubleClickOnAPostWithAWalkedReferenceOpensThePost()
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "110", content: "<p>Shearing day #sheep</p>"),
                APost.With(id: "220")),
        };

        using var drawn = await DrawnShell.Of(80, Tall, Themes.Plain, built);

        drawn.Press(Key.CursorRight);

        Assert.NotNull(drawn.Shell.Screen.Reference);

        drawn.DoubleClick(OverContent, RowOf(drawn, "Shearing day"));
        drawn.Settle();

        var post = Assert.IsType<PostScreen>(drawn.Shell.Screen);

        Assert.Equal("110", post.Picked?.Id);
    }

    /// <summary>
    ///     A double click on a row that is part of nothing does nothing — in particular it does not open the thing
    ///     already picked.
    /// </summary>
    [Fact]
    public async Task ADoubleClickOnARowOfNothingDoesNothing()
    {
        using var drawn = await On("feed");

        var feed = drawn.Shell.Screen;
        var rows = drawn.Rows();

        Assert.Null(drawn.Content.ItemAt(new Point(OverContent, Tall - 3)));

        drawn.DoubleClick(OverContent, Tall - 3);
        drawn.Settle();

        Assert.Same(feed, drawn.Shell.Screen);
        Assert.Equal(rows, drawn.Rows());
        Assert.Equal("110", drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>
    ///     An open confirmation wins: the double click declines it, as a click does, and neither picks nor opens — not
    ///     even with its first click having closed the question before the pair arrives.
    /// </summary>
    [Fact]
    public async Task ADoubleClickDeclinesAnOpenConfirmationAndOpensNothing()
    {
        var answers = Enumerable.Range(1, 3).Select(at => APost.AnAnswer($"Answer {at}", at)).ToList();

        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "110", poll: APost.APoll(options: answers)),
                APost.With(id: "220", content: "Two sheep")),
        };

        using var drawn = await DrawnShell.Of(80, Tall, Themes.Plain, built);

        var feed = drawn.Shell.Screen;

        drawn.Press(new Key('2'));
        drawn.Press(Key.V);

        Assert.NotNull(drawn.Shell.Asking);

        drawn.DoubleClick(OverContent, RowOf(drawn, "Two sheep"));
        drawn.Settle();

        Assert.Null(drawn.Shell.Asking);
        Assert.Same(feed, drawn.Shell.Screen);
        Assert.Equal("110", drawn.Shell.Screen.Picked?.Id);
        Assert.Empty(built.Engagement.Votes);
    }

    /// <summary>And an open filter prompt: the double click closes it and neither picks nor opens.</summary>
    [Fact]
    public async Task ADoubleClickClosesAnOpenFilterPromptAndOpensNothing()
    {
        using var drawn = await On("follows");

        drawn.Press(Key.F);

        var follows = Assert.IsType<FollowsScreen>(drawn.Shell.Screen);

        Assert.True(follows.IsTyping);

        drawn.DoubleClick(OverContent, RowOf(drawn, "bea@hachyderm.io"));
        drawn.Settle();

        Assert.Same(follows, drawn.Shell.Screen);
        Assert.False(follows.IsTyping);
        Assert.Equal("ann@hachyderm.io", follows.PickedPerson?.Address);
    }
}
