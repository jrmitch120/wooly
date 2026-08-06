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

    private static FeedScreen Feed(params Wooly.Core.Posts.Post[] posts) =>
        new(new Destination(DestinationKind.Home, "Home"), posts);
}
