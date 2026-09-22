using Wooly.Core.Posts;
using Wooly.Core.Relationships;
using Wooly.Core.Search;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     How a crumb is spelled, asked of every screen in the shell rather than of the ones somebody remembered: every
///     crumb is sentence case, except one that opens with a handle or a hashtag, which is spelled the way that thing
///     is spelled (#216).
/// </summary>
/// <remarks>
///     The complaint that started #160 was that the sidebar said <c>Home</c> and the breadcrumb said <c>home</c>, so
///     the rail's own labels are what the rule is matched to — they are already sentence case, and a rail-label crumb
///     matches the rail by construction. Guarded by <see cref="EveryScreenIsAskedHowItsCrumbIsSpelled" /> so that a
///     screen added later inherits the rule rather than being one somebody has to remember.
/// </remarks>
public class CrumbTests
{
    /// <summary>Every screen there is, in the state it opens in, and the crumb it says it is.</summary>
    private static readonly (string State, string Crumb)[] Spelled =
    [
        ("feed", "Home"),
        ("notifications", "Notifications"),
        ("messages", "Direct messages"),
        ("requests", "Follow requests"),
        ("search", "Search"),
        ("search-cats", "Search cats"),
        ("discover", "Discover"),
        ("help", "Keys"),
        ("notice", "Hashtag"),
        ("post", "Post by @ben@hachyderm.io"),
        ("conversation", "With @alice@hachyderm.io"),
        ("compose", "Compose"),
        ("reply", "Reply to @ben@hachyderm.io"),
        ("edit", "Edit"),
        ("account", "@maria@fosstodon.org"),
        ("following", "@maria@fosstodon.org following"),
        ("followers", "@maria@fosstodon.org followers"),
    ];

    /// <summary>Those screens, as the theory reads them.</summary>
    public static TheoryData<string, string> HowEachIsSpelled
    {
        get
        {
            var screens = new TheoryData<string, string>();

            foreach (var (state, crumb) in Spelled)
            {
                screens.Add(state, crumb);
            }

            return screens;
        }
    }

    /// <summary>What each screen says it is, word for word — the rail's own spelling where it has one.</summary>
    [Theory]
    [MemberData(nameof(HowEachIsSpelled))]
    public void Crumb_IsSpelledTheWayTheContractSpellsIt(string state, string crumb) =>
        Assert.Equal(crumb, Of(state).Crumb);

    /// <summary>
    ///     And the rule underneath those, said once so that the eighteenth screen inherits it: a crumb starts with a
    ///     capital, unless it opens with a handle or a hashtag — nobody's username is capitalised.
    /// </summary>
    [Theory]
    [MemberData(nameof(HowEachIsSpelled))]
    public void Crumb_IsSentenceCaseUnlessItOpensWithAHandleOrAHashtag(string state, string _)
    {
        var crumb = Of(state).Crumb;

        Assert.NotEqual(string.Empty, crumb);

        if (crumb[0] is '@' or '#')
        {
            Assert.Equal(crumb.ToLowerInvariant(), crumb);

            return;
        }

        Assert.Equal(char.ToUpperInvariant(crumb[0]), crumb[0]);

        // Sentence case rather than title case: what follows the first word is spelled as prose, bar the handle a
        // crumb may go on to name.
        Assert.Equal(crumb[1..].Split(' ')[0].ToLowerInvariant(), crumb[1..].Split(' ')[0]);
    }

    /// <summary>
    ///     And every screen there is was asked, so that a screen added later cannot quietly be the one whose crumb
    ///     nobody spelled — read off the assembly rather than listed here, because a list is the thing that gets
    ///     forgotten (the guard <see cref="ScreenKeyTests.EveryScreenIsAskedTheRule" /> keeps for #193).
    /// </summary>
    [Fact]
    public void EveryScreenIsAskedHowItsCrumbIsSpelled()
    {
        var asked = Spelled.Select(screen => Of(screen.State).GetType()).ToHashSet();

        var screens = typeof(Screen).Assembly.GetTypes()
            .Where(type => type.IsSubclassOf(typeof(Screen)) && !type.IsAbstract);

        foreach (var screen in screens)
        {
            Assert.Contains(screen, asked);
        }
    }

    /// <summary>The screen the <paramref name="state" /> names, built with no terminal and no shell around it.</summary>
    private static Screen Of(string state)
    {
        var post = APost.With(id: "110", account: "ben@hachyderm.io");
        var maria = AnAccount.With(address: "maria@fosstodon.org");

        switch (state)
        {
            case "feed":
                return new FeedScreen(new Destination(DestinationKind.Home, "Home"), [post]);

            case "notifications":
                return new NotificationsScreen([ANotification.Follow()]);

            case "messages":
                return new DirectMessagesScreen([AConversation.With(latest: post)]);

            case "requests":
                return new FollowRequestsScreen([AnAccount.With()]);

            case "search":
                return new SearchScreen();

            case "search-cats":
                var search = new SearchScreen();

                search.Found("cats", new SearchResults { Hashtags = [AHashtag.With()] });

                return search;

            case "discover":
                return new DiscoverScreen([ASuggestion.With()]);

            case "help":
                return new HelpScreen(new FeedScreen(new Destination(DestinationKind.Home, "Home"), [post]));

            // The one screen whose crumb comes from its call site rather than from itself, so the spelling asserted
            // here is the one the shell hands it — the rail's own label for the destination that opens it.
            case "notice":
                return new NoticeScreen("Hashtag", "No hashtag is set for the rail.");

            case "post":
                return new PostScreen(post, new PostThread([], []));

            case "conversation":
                return new ConversationScreen(AConversation.Thread(
                    AConversation.With(),
                    post with { Visibility = PostVisibility.Direct }));

            case "compose":
                return new ComposeScreen(ComposeFor.Post);

            case "reply":
                return new ComposeScreen(ComposeFor.Reply, post);

            case "edit":
                return new ComposeScreen(ComposeFor.Edit, post);

            case "account":
                return new AccountScreen(maria, [post], pinned: []);

            case "following":
                return new FollowsScreen(maria, FollowSide.Following, mine: false);

            case "followers":
                return new FollowsScreen(maria, FollowSide.Followers, mine: false);

            // No screen by that name rather than a feed by default: a state nobody built is a state nobody asserted.
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state, "No screen by that name.");
        }
    }
}
