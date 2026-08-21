using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     Asking past a content warning, which is <see cref="Screen.Picked" /> and one question — the same question on
///     every screen, which is why no screen answers it for itself.
/// </summary>
/// <remarks>
///     What settles whether the key was used is whether there was anything to reveal, so the interesting answers are
///     the two <see langword="false" /> ones: a screen with nothing picked out, and a warning already asked past.
/// </remarks>
public class ScreenRevealTests
{
    /// <summary>The screens that hold no posts at all, which used to say so by overriding the question away.</summary>
    public static TheoryData<string> Postless => ["messages", "requests", "compose", "notice", "help"];

    /// <summary>A warning asked past is shown, and the key that asked counts as used.</summary>
    [Fact]
    public void Reveal_ShowsWhatThePickedPostsWarningWasHiding()
    {
        var screen = Feed(APost.With(id: "1", contentWarning: "spoilers"));

        Assert.True(screen.Reveal());
    }

    /// <summary>
    ///     Asked twice, the second press was not used — there was nothing left hidden for it to do, and a shell that
    ///     reported it as used would be claiming to have acted.
    /// </summary>
    [Fact]
    public void Reveal_IsNotUsedTwiceOnTheSameWarning()
    {
        var screen = Feed(APost.With(id: "1", contentWarning: "spoilers"));

        Assert.True(screen.Reveal());
        Assert.False(screen.Reveal());
    }

    /// <summary>A post with nothing hidden has nothing to reveal, so the key falls through to mean nothing.</summary>
    [Fact]
    public void Reveal_IsNotUsedOnAPostCarryingNoWarning()
    {
        Assert.False(Feed(APost.With(id: "1")).Reveal());
    }

    /// <summary>
    ///     A post the instance marked sensitive is asked past by the same key, warning text or no: what <c>x</c> shows
    ///     is what the post is hiding, and on the commonest sensitive post — a photograph with nothing written over it
    ///     — the attachments are the whole of that (#113).
    /// </summary>
    [Fact]
    public void Reveal_ShowsWhatASensitivePostIsHidingWithNoWarningWrittenOverIt()
    {
        var screen = Feed(APost.With(id: "1", sensitive: true, media: [APost.APicture()]));

        Assert.True(screen.Reveal());
        Assert.False(screen.Reveal());
    }

    /// <summary>
    ///     A screen with no posts on it picks none, so it reveals nothing — which is what lets the question be asked
    ///     once on <see cref="Screen" /> rather than answered away on each of them.
    /// </summary>
    [Theory]
    [MemberData(nameof(Postless))]
    public void Reveal_IsNotUsedOnAScreenWithNoPostsOnIt(string kind)
    {
        Screen screen = kind switch
        {
            "messages" => new DirectMessagesScreen([AConversation.With(id: "1")]),
            "requests" => new FollowRequestsScreen([AnAccount.With(id: "1")]),
            "compose" => new ComposeScreen(ComposeFor.Post),
            "notice" => new NoticeScreen("nowhere", "Nothing to read here."),
            _ => new HelpScreen(new NoticeScreen("nowhere", "Nothing to read here.")),
        };

        Assert.False(screen.Reveal());
    }

    /// <summary>An empty timeline picks nothing either, which is a fact about the list rather than a place in it.</summary>
    [Fact]
    public void Reveal_IsNotUsedOnAnEmptyList()
    {
        Assert.False(new FeedScreen(new Destination(DestinationKind.Home, "Home"), []).Reveal());
    }

    /// <summary>
    ///     A reveal belongs to the screen it was made on, so drilling into a post asked past in the feed asks again:
    ///     the post screen is a new screen, and a warning is a request to be asked before being shown (#121).
    /// </summary>
    /// <remarks>
    ///     The key being used a second time is the whole of the assertion — <see cref="Screen.Reveal" /> answers
    ///     <see langword="false" /> on a warning already asked past, so a <see langword="true" /> down here is the post
    ///     screen finding it still hidden.
    /// </remarks>
    [Fact]
    public async Task Reveal_AsksAgainOnAScreenDrilledIntoFromTheOneItWasMadeOn()
    {
        var built = Warned();
        var shell = await built.Opened();

        Assert.True(shell.Screen.Reveal());

        await shell.Enter();
        built.Host.Drain();

        var post = Assert.IsType<PostScreen>(shell.Screen);

        Assert.Equal("110", post.Post.Id);
        Assert.True(post.Reveal());
    }

    /// <summary>
    ///     And walking back out finds it still asked past: a pop hands back the very screen the reveal was made on,
    ///     the same law the page a screen is on follows (#133, #121).
    /// </summary>
    [Fact]
    public async Task Reveal_StandsOnAScreenWalkedBackOutTo()
    {
        var built = Warned();
        var shell = await built.Opened();

        var feed = shell.Screen;

        Assert.True(feed.Reveal());

        await shell.Enter();
        built.Host.Drain();

        shell.Back();

        Assert.Same(feed, shell.Screen);
        Assert.False(shell.Screen.Reveal());
    }

    /// <summary>
    ///     And a screen popped is a reveal gone with it, which is the half of the rule the two above do not reach: the
    ///     reader asks past the warning down on the post screen, walks out, and drills in again to a screen that has
    ///     been asked nothing (#121).
    /// </summary>
    /// <remarks>
    ///     The one assertion here that a <c>Revealed</c> shared by the whole stack would fail, and so the one that
    ///     pins which of the two designs this is: under a shared one the second drill-in would find the post already
    ///     asked past, and <c>x</c> would go unused.
    /// </remarks>
    [Fact]
    public async Task Reveal_LapsesWithTheScreenItWasMadeOnWhenThatScreenIsPopped()
    {
        var built = Warned();
        var shell = await built.Opened();

        await shell.Enter();
        built.Host.Drain();

        var post = shell.Screen;

        Assert.True(post.Reveal());

        shell.Back();

        await shell.Enter();
        built.Host.Drain();

        Assert.NotSame(post, shell.Screen);
        Assert.True(shell.Screen.Reveal());
    }

    /// <summary>
    ///     A refresh lapses it, for the reason it opens at the top again: <c>g</c> builds a screen rather than changing
    ///     the one in hand, and a screen nobody has read yet has been asked nothing (#121).
    /// </summary>
    /// <remarks>
    ///     Both of the ways a refresh replaces a screen, since they are different code: a destination's is the arrival
    ///     it already arrives by, and the post screen's is <c>Freshened</c> putting a new screen in place of the top of
    ///     the stack (#84).
    /// </remarks>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reveal_LapsesWhenTheScreenIsRefreshed(bool insideThePost)
    {
        var built = Warned();
        var shell = await built.Opened();

        if (insideThePost)
        {
            await shell.Enter();
            built.Host.Drain();

            Assert.IsType<PostScreen>(shell.Screen);
        }

        var before = shell.Screen;

        Assert.True(before.Reveal());

        await shell.Refresh();
        built.Host.Drain();

        Assert.NotSame(before, shell.Screen);
        Assert.True(shell.Screen.Reveal());
    }

    /// <summary>
    ///     A mark does not lapse it, which is the other half of "the screen being replaced is what does": favoriting
    ///     puts a fresh copy of the post on the same screen, and the set is keyed by post id rather than by the copy in
    ///     hand — so a star lighting up does not put the warning back (#121).
    /// </summary>
    [Fact]
    public async Task Reveal_StandsWhenAMarkReplacesThePostInPlace()
    {
        var built = Warned();

        built.Engagement = FakePostEngagement.Answering(Spoilered() with { Marks = APost.Marked(favorited: true) });

        var shell = await built.Opened();

        Assert.True(shell.Screen.Reveal());

        await shell.Mark(PostMark.Favorite);
        built.Host.Drain();

        Assert.True(shell.Screen.Picked?.Marks.Favorited);
        Assert.False(shell.Screen.Reveal());
    }

    /// <summary>A shell over one warned post, which is the whole feed and the post every drill-in opens.</summary>
    private static AShell Warned() => new() { Timelines = FakeTimelineReader.Holding(Spoilered()) };

    /// <summary>That post: the one thing on the feed, and the only thing any of these presses can act on.</summary>
    private static Post Spoilered() => APost.With(id: "110", contentWarning: "spoilers");

    private static FeedScreen Feed(params Post[] posts) =>
        new(new Destination(DestinationKind.Home, "Home"), posts);
}
