using Wooly.Core.Posts;
using Wooly.Core.Search;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The three screens that put rows of their own between the things they hold: search, whose three kinds each get a
///     heading, the post screen, which says how many replies follow the post itself, and the direct messages screen,
///     whose notice sits above the list.
/// </summary>
/// <remarks>
///     Stamping a row is <see cref="Picked{T}" />'s and asserted there (<see cref="PickedTests" />), so what is left
///     here is only the splicing — that a screen putting a heading or a notice among the rows has not thereby put a
///     row of its own where a thing should be, or numbered what follows it from somewhere other than zero.
///     <para>
///         Worth revisiting: if this never catches anything now that no screen numbers its own rows, it can go.
///     </para>
/// </remarks>
public class ScreenPickTests
{
    /// <summary>The three screens that splice, with how many things there are on each to pick.</summary>
    public static TheoryData<string, int> Screens => new()
    {
        { "post", 3 },
        { "search", 4 },
        { "messages", 3 },
    };

    /// <summary>
    ///     The rows and the pick agree across the spliced rows: after picking the <c>at</c>th thing, the rows carrying
    ///     the mark are exactly the rows that say they are part of it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Screens))]
    public void Pick_NumbersTheRowsTheSameWayItNumbersWhatItSelects(string kind, int count)
    {
        var screen = Of(kind);

        for (var at = 0; at < count; at++)
        {
            screen.Pick(at);

            var lines = screen.Lines(new Drawing(61, AShell.Now));

            var marked = lines.Where(line => line.Has(Role.Selection)).ToList();
            var named = lines.Where(line => line.Item == at).ToList();

            Assert.NotEmpty(named);
            Assert.Equal(named, marked);
        }
    }

    /// <summary>
    ///     Every thing on the screen is named and nothing else is, so a heading or a notice spliced among the rows is
    ///     part of none of them — a page that begins on one begins on a thing.
    /// </summary>
    [Theory]
    [MemberData(nameof(Screens))]
    public void Pick_NamesEveryItemOnTheScreenAndNothingSplicedBetweenThem(string kind, int count)
    {
        var items = Of(kind)
                    .Lines(new Drawing(61, AShell.Now))
                    .Select(line => line.Item)
                    .OfType<int>()
                    .Distinct()
                    .Order();

        Assert.Equal(Enumerable.Range(0, count), items);
    }

    /// <summary>One of each screen that splices, each with things on it to pick out.</summary>
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
            case "post":
                // Its own post is 0 and the replies follow it under a heading, so two replies make three to pick.
                return new PostScreen(posts[0], new PostThread([], [posts[1], posts[2]]));

            case "search":
                // One of each kind and then some, so that the three headed sections are numbered as one list.
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

            default:
                // A notice above the list, which is about the list rather than about anything on it.
                return new DirectMessagesScreen(
                    [
                        AConversation.With(id: "1"),
                        AConversation.Emptied(id: "2"),
                        AConversation.With(id: "3"),
                    ],
                    "A rate limit cut this short.");
        }
    }
}
