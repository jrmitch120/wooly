using Wooly.Core.Search;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     Picking an item by its number rather than by a step from wherever the selection was — which is what <c>j</c>
///     does once the arrows have scrolled the selected post off the page (#51).
/// </summary>
/// <remarks>
///     The property that matters is that a screen's rows and its <see cref="Screen.Pick" /> count the same things in
///     the same order. Four screens number something other than a plain list of posts — the post screen, where 0 is
///     the post itself; search, with its three kinds; notifications; direct messages — and a row naming a different
///     ordinal from the one <c>Pick</c> takes would select a post the reader was not looking at.
/// </remarks>
public class ScreenPickTests
{
    /// <summary>Every screen that holds a selection, with how many things there are on it to pick.</summary>
    public static TheoryData<string, int> Screens => new()
    {
        { "feed", 3 },
        { "post", 3 },
        { "account", 3 },
        { "conversation", 3 },
        { "search", 4 },
        { "notifications", 3 },
        { "messages", 3 },
        { "requests", 3 },
    };

    /// <summary>
    ///     The rows and the pick agree: after picking the <c>at</c>th thing, the rows carrying the selection are
    ///     exactly the rows that say they are part of it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Screens))]
    public void Pick_NumbersTheRowsTheSameWayItNumbersWhatItSelects(string kind, int count)
    {
        var screen = Of(kind);

        for (var at = 0; at < count; at++)
        {
            screen.Pick(at);

            var lines = screen.Lines(61, AShell.Now);

            var selected = lines.Where(line => line.Has(Role.Selection)).ToList();
            var named = lines.Where(line => line.Item == at).ToList();

            Assert.NotEmpty(named);
            Assert.Equal(named, selected);
        }
    }

    /// <summary>Every row that is part of something says which, so that no row on a page is unaccounted for.</summary>
    [Theory]
    [MemberData(nameof(Screens))]
    public void Pick_NamesEveryItemOnTheScreen(string kind, int count)
    {
        var items = Of(kind).Lines(61, AShell.Now).Select(line => line.Item).OfType<int>().Distinct().Order();

        Assert.Equal(Enumerable.Range(0, count), items);
    }

    /// <summary>A number off either end of the list is clamped, the same way stepping off the end of one is.</summary>
    [Theory]
    [MemberData(nameof(Screens))]
    public void Pick_StopsAtEitherEnd(string kind, int count)
    {
        var screen = Of(kind);

        screen.Pick(int.MaxValue);

        Assert.Contains(screen.Lines(61, AShell.Now), line => line.Item == count - 1 && line.Has(Role.Selection));

        screen.Pick(int.MinValue);

        Assert.Contains(screen.Lines(61, AShell.Now), line => line.Item == 0 && line.Has(Role.Selection));
    }

    /// <summary>A screen with nothing on it has nothing to pick, and picking anyway is not a crash.</summary>
    [Theory]
    [InlineData("feed")]
    [InlineData("notifications")]
    [InlineData("messages")]
    [InlineData("requests")]
    public void Pick_DoesNothingOnAScreenWithNothingOnIt(string kind)
    {
        var screen = kind switch
        {
            "feed" => new FeedScreen(new Destination(DestinationKind.Home, "Home"), []),
            "notifications" => new NotificationsScreen([]),
            "messages" => new DirectMessagesScreen([]),
            _ => (Screen)new FollowRequestsScreen([]),
        };

        screen.Pick(2);

        Assert.DoesNotContain(screen.Lines(61, AShell.Now), line => line.Item is not null);
    }

    /// <summary>One of each screen that holds a selection, each with three things on it to pick out.</summary>
    private static Screen Of(string kind)
    {
        var posts = new[]
        {
            APost.With(id: "1"),
            APost.With(id: "2"),
            APost.With(id: "3"),
        };

        switch (kind)
        {
            case "feed":
                return new FeedScreen(new Destination(DestinationKind.Home, "Home"), posts);

            case "post":
                // Its own post is 0 and the replies follow it, so two replies make three things to pick.
                return new PostScreen(posts[0], [posts[1], posts[2]]);

            case "account":
                return new AccountScreen(AnAccount.With(), posts);

            case "conversation":
                return new ConversationScreen(AConversation.Thread(AConversation.With(), posts));

            case "search":
                // One of each kind and then some, so that the three sections are numbered as one list.
                var search = new SearchScreen();

                search.Found(
                    "sheep",
                    new SearchResults
                    {
                        Accounts = [AnAccount.With()],
                        Hashtags = [AHashtag.With()],
                        Posts = [posts[0], posts[1]],
                    });

                return search;

            case "notifications":
                return new NotificationsScreen(
                [
                    ANotification.With(id: "1"),
                    ANotification.Follow(id: "2"),
                    ANotification.With(id: "3"),
                ]);

            case "messages":
                return new DirectMessagesScreen(
                [
                    AConversation.With(id: "1"),
                    AConversation.Emptied(id: "2"),
                    AConversation.With(id: "3"),
                ]);

            default:
                return new FollowRequestsScreen(
                [
                    AnAccount.With(id: "1", address: "alice@hachyderm.io"),
                    AnAccount.With(id: "2", address: "ben@hachyderm.io"),
                    AnAccount.With(id: "3", address: "cass@hachyderm.io"),
                ]);
        }
    }
}
