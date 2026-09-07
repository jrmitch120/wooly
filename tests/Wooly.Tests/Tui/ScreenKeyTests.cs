using Wooly.Core.Posts;
using Wooly.Core.Search;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     The rule <see cref="PostKeys" /> states in both directions, asked of every screen in the shell rather than of
///     the ones somebody remembered (#193): a key that acts on the picked post is announced wherever one is picked,
///     and announced nowhere while none is.
/// </summary>
/// <remarks>
///     Walked over every <see cref="Screen" /> there is, and guarded by <see cref="EveryScreenIsAskedTheRule" /> so
///     that a screen added later is one that inherits the rule rather than one somebody has to remember — the
///     failure this is about is a row that lies, and there is nothing to catch that at compile time.
/// </remarks>
public class ScreenKeyTests
{
    /// <summary>The keys that act on the picked post, which is every one of them bar <c>c</c>.</summary>
    private static IReadOnlyList<KeyHint> ActingOnAPost =>
        [.. PostKeys.OnAPost.Where(key => key != PostKeys.Composing)];

    /// <summary>Every screen in a state where nothing is picked out for a post key to act on.</summary>
    private static readonly string[] NothingPicked =
    [
        "feed-empty",
        "inbox-empty",
        "notifications-follow",
        "account-header",
        "search-account",
        "search-hashtag",
        "search-typing",
        "messages",
        "requests",
        "compose",
        "notice",
        "help",
    ];

    /// <summary>Every screen that can have a post picked out, with one picked.</summary>
    private static readonly string[] APostPicked =
    [
        "feed",
        "post-reply",
        "account-post",
        "notifications-mention",
        "search-post",
        "conversation",
    ];

    /// <summary>Those states, as the theory reads them.</summary>
    public static TheoryData<string> WithNoPostPicked => new(NothingPicked);

    /// <summary>And every screen holding a post, with one of them picked out.</summary>
    public static TheoryData<string> WithAPostPicked => new(APostPicked);

    /// <summary>
    ///     A key with nothing to act on is announced nowhere: a row offering one is a row that reads as a shell
    ///     missing the press.
    /// </summary>
    [Theory]
    [MemberData(nameof(WithNoPostPicked))]
    public void Keys_LeaveOffEveryKeyThatActsOnAPostWhereNoneIsPicked(string state)
    {
        var screen = Of(state);

        Assert.Null(screen.Picked);

        foreach (var key in ActingOnAPost)
        {
            Assert.DoesNotContain(key, screen.Keys);
        }
    }

    /// <summary>
    ///     And <c>c</c> stays, on every row that ever carried it: a fresh post is written from anywhere, so it is the
    ///     one of these that acts on nothing picked out.
    /// </summary>
    [Theory]
    [InlineData("feed-empty")]
    [InlineData("inbox-empty")]
    [InlineData("notifications-follow")]
    [InlineData("account-header")]
    public void Keys_GoOnSayingCompose(string state) => Assert.Contains(PostKeys.Composing, Of(state).Keys);

    /// <summary>
    ///     The rule the other way, which is the half that already held: every key that acts on the picked post is
    ///     announced where one is picked — by letter, since a screen may say something of its own by that letter
    ///     (<c>d</c> dismisses a notification and deletes a post).
    /// </summary>
    [Theory]
    [MemberData(nameof(WithAPostPicked))]
    public void Keys_SayEveryKeyThatActsOnThePickedPost(string state)
    {
        var screen = Of(state);

        Assert.NotNull(screen.Picked);

        Assert.Subset(
            screen.Keys.Select(key => key.Key).ToHashSet(StringComparer.Ordinal),
            PostKeys.OnAPost.Select(key => key.Key).ToHashSet(StringComparer.Ordinal));
    }

    /// <summary>
    ///     And a screen's own key that shares a letter with one of those is untouched, which is the whole reason the
    ///     rule runs by hint: what a key does is what its screen says it does.
    /// </summary>
    [Theory]
    [InlineData("notifications-follow", "d:dismiss")]
    [InlineData("search-account", "⏎:open")]
    [InlineData("search-hashtag", "⏎:open")]
    [InlineData("messages", "⏎:open")]
    [InlineData("messages", "m:mark read")]
    [InlineData("requests", "⏎:read them")]
    [InlineData("requests", "a:accept")]
    [InlineData("requests", "x:reject")]
    public void Keys_LeaveAScreensOwnKeyAloneWhereItSharesALetter(string state, string hint) =>
        Assert.Contains(hint, Of(state).Keys.Select(key => key.ToString()));

    /// <summary>
    ///     And every screen there is was asked, so that a screen added later cannot quietly be the one screen the rule
    ///     was never put to — in whichever direction it can be put: a screen that says which post is picked out on it
    ///     is asked with one picked, and every screen at all is asked with none.
    /// </summary>
    /// <remarks>
    ///     Read off the screen rather than listed here, because a list is the thing that gets forgotten: a screen
    ///     overriding <see cref="Screen.Picked" /> is a screen that can hold a post, and that is the same fact the
    ///     rule turns on.
    /// </remarks>
    [Fact]
    public void EveryScreenIsAskedTheRule()
    {
        var withNothing = NothingPicked.Select(state => Of(state).GetType()).ToHashSet();
        var withAPost = APostPicked.Select(state => Of(state).GetType()).ToHashSet();

        var screens = typeof(Screen).Assembly.GetTypes()
            .Where(type => type.IsSubclassOf(typeof(Screen)) && !type.IsAbstract);

        foreach (var screen in screens)
        {
            Assert.Contains(screen, withNothing.Concat(withAPost));

            if (screen.GetProperty(nameof(Screen.Picked))?.DeclaringType == screen)
            {
                Assert.Contains(screen, withAPost);
            }
            else
            {
                Assert.Contains(screen, withNothing);
            }
        }
    }

    /// <summary>The screen the <paramref name="state" /> names, built with no terminal and no shell around it.</summary>
    private static Screen Of(string state)
    {
        var post = APost.With(id: "110");

        switch (state)
        {
            case "feed-empty":
                return Feed([], "Nothing here yet.");

            case "inbox-empty":
                return new NotificationsScreen([], "Nothing waiting.");

            case "notifications-follow":
                return new NotificationsScreen([ANotification.Follow()]);

            case "notifications-mention":
                return new NotificationsScreen([ANotification.With(post: post)]);

            case "account-header":
                return new AccountScreen(AnAccount.With(), [post]);

            case "account-post":
                var account = new AccountScreen(AnAccount.With(), [post]);

                // The header block is what an arrival picks out, so the first post of theirs is one walk down (#179).
                account.Move(1);

                return account;

            case "search-typing":
                return new SearchScreen();

            case "search-account":
                return Searched(new SearchResults { Accounts = [AnAccount.With()] });

            case "search-hashtag":
                return Searched(new SearchResults { Hashtags = [AHashtag.With()] });

            case "search-post":
                return Searched(new SearchResults { Posts = [post] });

            case "messages":
                return new DirectMessagesScreen([AConversation.With(latest: post)]);

            case "requests":
                return new FollowRequestsScreen([AnAccount.With()]);

            case "compose":
                return new ComposeScreen(ComposeFor.Post);

            case "notice":
                return new NoticeScreen("rate limit", "The instance asked for a moment.");

            case "help":
                return new HelpScreen(Feed([post]));

            case "conversation":
                return new ConversationScreen(
                    AConversation.Thread(AConversation.With(), post with { Visibility = PostVisibility.Direct }));

            case "post-reply":
                var thread = new PostScreen(post, new PostThread([], [APost.With(id: "220")]));

                // Onto the answer below it: ⏎ is the one key the post screen leaves off while the post it would open
                // is the one already on screen (#48), and that is its own rule rather than this one.
                thread.Move(1);

                return thread;

            case "feed":
                return Feed([post]);

            // No screen by that name rather than a feed by default: a state nobody built is a state nobody asserted,
            // and the whole of this file's worth is that every screen there is was asked.
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state, "No screen by that name.");
        }
    }

    /// <summary>A home timeline holding <paramref name="posts" />, which is what a rail destination opens onto.</summary>
    private static FeedScreen Feed(IReadOnlyList<Post> posts, string? notice = null) =>
        new(new Destination(DestinationKind.Home, "Home"), posts, notice);

    /// <summary>A search screen holding what an instance answered, which is what stops its prompt taking letters.</summary>
    private static SearchScreen Searched(SearchResults results)
    {
        var search = new SearchScreen();

        search.Found("maria", results);

        return search;
    }
}
