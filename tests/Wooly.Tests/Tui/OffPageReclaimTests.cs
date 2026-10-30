using System.Drawing;
using Terminal.Gui.Input;
using Wooly.Core.Notifications;
using Wooly.Core.Paging;
using Wooly.Core.Posts;
using Wooly.Core.Timelines;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     What <c>j</c>, <c>k</c>, <c>[</c> and <c>]</c> pick once the pick has been scrolled off the page: the first thing
///     whose start is on it — its content's first row, or its first row where it has no content — falling back to the
///     topmost thing with any row showing where no thing begins there (#472, asked in review of #480).
///     Pressed and wheeled on the whole shell drawn headless, and read back off what is picked.
/// </summary>
public class OffPageReclaimTests
{
    /// <summary>Short enough that six posts run well past the foot of the page.</summary>
    private const int Short = 16;

    /// <summary>Whoever the account screen is about in these tests.</summary>
    private const string Whose = "ben@hachyderm.io";

    /// <summary>A column inside the content panel, clear of the pick's gutter.</summary>
    private const int OverContent = ContentClicks.OverContent;

    /// <summary>A row inside the content panel, below its top edge.</summary>
    private const int Inside = 3;

    /// <summary>
    ///     With the foot of a post filling the top of the page — its counts, its text scrolled off — both keys pick the
    ///     next post down, the first one the reader can see beginning, rather than the half-gone one above it.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task APostsFootAtTheTopOfThePageIsPassedOver(bool next)
    {
        var posts = Six();
        posts[1] = APost.With(id: "220", content: "Shorn this morning");

        using var drawn = await Feed(posts);

        LeaveThePickBelow(drawn);

        // The text, the blank under it, and the counts on the page's first row.
        TextOffBy(drawn, "Shorn this morning", 2);

        drawn.Press(next ? Key.K : Key.J);

        Assert.Equal("330", drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>
    ///     A post begins at its content, not its byline: with the byline and avatar scrolled off and its first row of
    ///     text on the page's first row, both keys pick it, and with that row gone too, the next post down (asked in
    ///     review of #480).
    /// </summary>
    [Theory]
    [InlineData(true, 0, "220")]
    [InlineData(false, 0, "220")]
    [InlineData(true, 1, "330")]
    [InlineData(false, 1, "330")]
    public async Task APostBeginsAtItsContent(bool next, int scrolledOff, string picked)
    {
        var posts = Six();
        posts[1] = APost.With(id: "220", content: "Shorn this morning");

        using var drawn = await Feed(posts);

        LeaveThePickBelow(drawn);
        TextOffBy(drawn, "Shorn this morning", scrolledOff);

        drawn.Press(next ? Key.K : Key.J);

        Assert.Equal(picked, drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>
    ///     A boosted post begins at its content too: with the boost line and the byline gone and its text on the page
    ///     the post counts as begun, and with the text gone as well it does not.
    /// </summary>
    [Theory]
    [InlineData(0, "220")]
    [InlineData(1, "330")]
    public async Task ABoostedPostBeginsAtItsContent(int scrolledOff, string picked)
    {
        var posts = Six();
        posts[1] = APost.With(id: "220", boosted: APost.With(id: "221", content: "Shorn this morning"));

        using var drawn = await Feed(posts);

        LeaveThePickBelow(drawn);
        TextOffBy(drawn, "Shorn this morning", scrolledOff);

        drawn.Press(Key.K);

        Assert.Equal(picked, drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>
    ///     A mention begins at the post it brings, not at the row saying who mentioned the reader or the post's byline:
    ///     the same way on its screen as a post does on a feed.
    /// </summary>
    [Theory]
    [InlineData(0, "220")]
    [InlineData(1, "330")]
    public async Task AMentionBeginsAtItsPostsContent(int scrolledOff, string picked)
    {
        using var drawn = await Notifications(
            [
                .. new[] { "110", "220", "330", "440", "550", "660" }.Select(
                    (id, at) => ANotification.With(
                    id: $"{at + 1}",
                    post: APost.With(id: id, content: $"Fleece {id}"))),
            ]);

        LeaveThePickBelow(drawn);
        TextOffBy(drawn, "Fleece 220", scrolledOff);

        drawn.Press(Key.K);

        Assert.Equal(picked, drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>
    ///     A favourite of the reader's own post begins at the muted reminder of that post under the faces, with the
    ///     names row and the avatar strip above it scrolled off.
    /// </summary>
    [Theory]
    [InlineData(0, "220")]
    [InlineData(1, "330")]
    public async Task AFavouriteOfYourPostBeginsAtItsPreview(int scrolledOff, string picked)
    {
        // Twelve, since each is two rows and the pick has to be left well below the page.
        using var drawn = await Notifications(
            [
                .. Enumerable.Range(1, 12).Select(
                    at => ANotification.With(
                        id: $"{at}",
                        kind: NotificationKind.Favorite,
                        post: APost.With(id: $"{at}{at}0", content: $"Fleece {at}{at}0", isMine: true))),
            ]);

        LeaveThePickBelow(drawn);
        TextOffBy(drawn, "Fleece 220", scrolledOff);

        drawn.Press(Key.K);

        Assert.Equal(picked, drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>
    ///     A follow has no post to begin at, so it begins at its first row: on the page it is picked, and with that row
    ///     gone the next notification is.
    /// </summary>
    [Theory]
    [InlineData(0, "2")]
    [InlineData(1, "3")]
    public async Task AFollowBeginsAtItsFirstRow(int scrolledOff, string picked)
    {
        using var drawn = await Notifications(
            [
                ANotification.With(id: "1", post: APost.With(id: "110")),
                ANotification.Follow(id: "2", actorId: "20"),
                ANotification.With(id: "3", post: APost.With(id: "330")),
                ANotification.With(id: "4", post: APost.With(id: "440")),
                ANotification.With(id: "5", post: APost.With(id: "550")),
                ANotification.With(id: "6", post: APost.With(id: "660")),
            ]);

        LeaveThePickBelow(drawn);
        TopOffBy(drawn, item: 1, rows: scrolledOff);

        drawn.Press(Key.K);

        Assert.Equal(picked, PickedNotification(drawn));
    }

    /// <summary>A post behind a content warning begins at the warning, the first of its content drawn.</summary>
    [Theory]
    [InlineData(0, "220")]
    [InlineData(1, "330")]
    public async Task APostBehindAWarningBeginsAtTheWarning(int scrolledOff, string picked)
    {
        var posts = Six();
        posts[1] = APost.With(id: "220", contentWarning: "Shearing day");

        using var drawn = await Feed(posts);

        LeaveThePickBelow(drawn);
        TextOffBy(drawn, "Shearing day", scrolledOff);

        drawn.Press(Key.K);

        Assert.Equal(picked, drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>A post with nothing but an attachment begins at the attachment.</summary>
    [Theory]
    [InlineData(0, "220")]
    [InlineData(1, "330")]
    public async Task APostWithOnlyAnAttachmentBeginsAtIt(int scrolledOff, string picked)
    {
        var posts = Six();
        posts[1] = APost.With(
            id: "220",
            content: string.Empty,
            media: [APost.Attached(MediaKind.Image, description: "Sheep, at length")]);

        using var drawn = await Feed(posts);

        LeaveThePickBelow(drawn);
        TextOffBy(drawn, "Sheep, at length", scrolledOff);

        drawn.Press(Key.K);

        Assert.Equal(picked, drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>A post with nothing but a link preview begins at the preview.</summary>
    [Theory]
    [InlineData(0, "220")]
    [InlineData(1, "330")]
    public async Task APostWithOnlyALinkPreviewBeginsAtIt(int scrolledOff, string picked)
    {
        var posts = Six();
        posts[1] = APost.With(
            id: "220",
            content: string.Empty,
            linkPreview: APost.ALinkPreview(title: "Fleece prices", image: null));

        using var drawn = await Feed(posts);

        LeaveThePickBelow(drawn);
        TextOffBy(drawn, "Fleece prices", scrolledOff);

        drawn.Press(Key.K);

        Assert.Equal(picked, drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>A post with nothing but a poll begins at the poll's first answer.</summary>
    [Theory]
    [InlineData(0, "220")]
    [InlineData(1, "330")]
    public async Task APostWithOnlyAPollBeginsAtIt(int scrolledOff, string picked)
    {
        var posts = Six();
        posts[1] = APost.With(
            id: "220",
            content: string.Empty,
            poll: APost.APoll([APost.AnAnswer("Merino", 4), APost.AnAnswer("Jacob", 6)]));

        using var drawn = await Feed(posts);

        LeaveThePickBelow(drawn);
        TextOffBy(drawn, "Merino", scrolledOff);

        drawn.Press(Key.K);

        Assert.Equal(picked, drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>
    ///     A search's account and hashtag have no content to begin at, so each begins at its first row: on the page it
    ///     is picked, and with that row gone the next thing found is.
    /// </summary>
    [Theory]
    [InlineData(1, 0, 1)]
    [InlineData(1, 1, 2)]
    [InlineData(3, 0, 3)]
    [InlineData(3, 1, 4)]
    public async Task ASearchsAccountAndHashtagBeginAtTheirFirstRow(int item, int scrolledOff, int picked)
    {
        using var drawn = await Searched();

        LeaveThePickBelow(drawn);
        TopOffBy(drawn, item, scrolledOff);

        drawn.Press(Key.K);

        Assert.Equal(picked, Assert.IsType<SearchScreen>(drawn.Shell.Screen).At);
    }

    /// <summary>
    ///     An account screen's header block is not a post and begins at its first row: scrolled back up to it, the
    ///     press picks it, and with that row gone, the first post under it.
    /// </summary>
    [Fact]
    public async Task AnAccountsHeaderBlockBeginsAtItsFirstRow()
    {
        using var drawn = await Account(pinned: [], posts: [.. Six().Select(post => post with { Account = Whose })]);

        LeaveThePickBelow(drawn);
        TopOffBy(drawn, item: 0, rows: 0);

        drawn.Press(Key.K);

        Assert.Null(drawn.Shell.Screen.Picked);

        LeaveThePickBelow(drawn);
        TopOffBy(drawn, item: 0, rows: 1);

        drawn.Press(Key.K);

        Assert.Equal("110", drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>
    ///     A post taller than the page filling it, its top scrolled off and no other thing beginning on the page, is
    ///     the post being read, and both keys pick it: <c>↓ ↓ ↓ j</c> on a long post keeps the reader where they are.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task APostFillingThePageIsPickedWhereNothingBeginsOnIt(bool next)
    {
        var tall = string.Concat(Enumerable.Range(1, 40).Select(line => $"<p>Line {line} of the fleece</p>"));

        using var drawn = await Feed([APost.With(id: "110"), APost.With(id: "220", content: tall), APost.With(id: "330")]);

        LeaveThePickBelow(drawn);
        TopOffBy(drawn, item: 1, rows: 5);

        drawn.Press(next ? Key.K : Key.J);

        Assert.Equal("220", drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>A page scrolled past the last thing, with the pick left at the top, takes that last thing.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PastTheLastThingThePressPicksIt(bool next)
    {
        using var drawn = await Feed(Six());

        // The pick is the first post, and the wheel runs the page as far down as it goes.
        for (var notch = 0; notch < 100; notch++)
        {
            drawn.Wheel(OverContent, Inside);
        }

        Assert.Null(drawn.Content.ItemAt(new Point(OverContent, Short - 2)));

        drawn.Press(next ? Key.K : Key.J);

        Assert.Equal("660", drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>With only the foot of the pick still on the page it is not off the page, and the keys move on from it.</summary>
    [Theory]
    [InlineData(true, "330")]
    [InlineData(false, "110")]
    public async Task APickWithItsFootShowingIsMovedFrom(bool next, string picked)
    {
        using var drawn = await Feed(Six());

        drawn.Press(Key.K);
        TopOffBy(drawn, item: 1, rows: 1);

        drawn.Press(next ? Key.K : Key.J);

        Assert.Equal(picked, drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>The reclaiming press does nothing else, and the one after it moves on from what it reclaimed.</summary>
    [Fact]
    public async Task ThePressAfterTheReclaimMovesOnFromIt()
    {
        var posts = Six();
        posts[1] = APost.With(id: "220", content: "Shorn this morning");

        using var drawn = await Feed(posts);

        LeaveThePickBelow(drawn);
        TextOffBy(drawn, "Shorn this morning", 1);

        drawn.Press(Key.J);
        drawn.Press(Key.K);

        Assert.Equal("440", drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>
    ///     <c>[</c> and <c>]</c> jump from the thing the same rule reclaims: on an account scrolled so that the foot of
    ///     the last pinned post, its text gone, fills the top of the page and their first post begins below it,
    ///     <c>[</c> goes back to the first pinned post, where from the half-gone pinned one it would have had nowhere
    ///     to go but the header.
    /// </summary>
    [Fact]
    public async Task TheBracketsJumpFromWhatIsReclaimed()
    {
        using var drawn = await Account(
            pinned:
            [
                APost.With(id: "110", account: Whose),
                APost.With(id: "220", account: Whose, content: "Shorn this morning"),
            ],
            posts: [APost.With(id: "330", account: Whose), APost.With(id: "440", account: Whose), APost.With(id: "550", account: Whose)]);

        LeaveThePickBelow(drawn);
        TextOffBy(drawn, "Shorn this morning", 1);

        drawn.Press(new Key('['));

        Assert.Equal("110", drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>Six posts, from 110 up to 660.</summary>
    private static Post[] Six() =>
        [.. new[] { "110", "220", "330", "440", "550", "660" }.Select(id => APost.With(id: id))];

    /// <summary>The shell drawn short on an account screen, with <paramref name="pinned" /> above <paramref name="posts" />.</summary>
    private static async Task<DrawnShell> Account(IReadOnlyList<Post> pinned, IReadOnlyList<Post> posts)
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Answering(timeline => timeline.Scope switch
            {
                TimelineScope.Pinned => Fetch<Post>.Complete(pinned),
                TimelineScope.Account => Fetch<Post>.Complete(posts),
                _ => Fetch<Post>.Complete([APost.With(id: "1", account: Whose)]),
            }),
            Accounts = FakeAccountRelationships.Holding(AnAccount.With(address: Whose)),
        };

        var drawn = await DrawnShell.Of(80, Short, Themes.Plain, built);

        await drawn.Shell.OpenAuthor();
        built.Host.Drain();
        drawn.Redraw();

        return drawn;
    }

    /// <summary>
    ///     The shell drawn short over what a search for sheep found: two accounts, then two hashtags, then six posts
    ///     — items 0 and 1, 2 and 3, and 4 on.
    /// </summary>
    private static async Task<DrawnShell> Searched()
    {
        var built = new AShell
        {
            Search = FakeInstanceSearch.Finding(
                accounts: [AnAccount.With(address: "ann@hachyderm.io"), AnAccount.With(address: "bea@hachyderm.io")],
                hashtags: [AHashtag.With(name: "sheep"), AHashtag.With(name: "wool")],
                posts: Six()),
        };

        var drawn = await DrawnShell.Of(80, Short, Themes.Plain, built);

        drawn.Shell.Search();
        built.Host.Drain();

        foreach (var letter in "sheep")
        {
            drawn.Shell.Type(letter);
        }

        drawn.Shell.Press(ShellKey.Enter);
        built.Host.Drain();
        drawn.Redraw();

        return drawn;
    }

    /// <summary>The shell drawn short on the notifications screen, holding <paramref name="notifications" />.</summary>
    private static async Task<DrawnShell> Notifications(Notification[] notifications)
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "1")),
            Notifications = FakeNotificationInbox.Holding(notifications),
        };

        var drawn = await DrawnShell.Of(80, Short, Themes.Plain, built);

        drawn.Shell.Rail.GoTo(drawn.Shell.Rail.Of(DestinationKind.Notifications));
        built.Host.Drain();
        drawn.Redraw();

        return drawn;
    }

    /// <summary>Which notification is picked, by its id: a follow has no post to say it by.</summary>
    private static string? PickedNotification(DrawnShell drawn) =>
        Assert.IsType<NotificationsScreen>(drawn.Shell.Screen).PickedGroup?.NewestId;

    /// <summary>The shell drawn short over a feed of <paramref name="posts" />.</summary>
    private static Task<DrawnShell> Feed(Post[] posts) =>
        DrawnShell.Of(80, Short, Themes.Plain, new AShell { Timelines = FakeTimelineReader.Holding(posts) });

    /// <summary>The last thing picked, which brings the page down to it, well below where these tests read.</summary>
    private static void LeaveThePickBelow(DrawnShell drawn) => drawn.WalkToTheLast();

    /// <summary>
    ///     The page wheeled to where the first row reading <paramref name="text" /> is <paramref name="rows" /> rows
    ///     above the page's first row: nought for that row on the page's first row.
    /// </summary>
    private static void TextOffBy(DrawnShell drawn, string text, int rows)
    {
        while (drawn.Content.Top > 0)
        {
            drawn.Wheel(OverContent, Inside, down: false);
        }

        var first = drawn.Content.ViewportToScreen(Point.Empty).Y;

        for (var notch = 0; !drawn.Rows()[first].Contains(text, StringComparison.Ordinal); notch++)
        {
            Assert.True(notch < 100, $"No page begins on \"{text}\":\n{string.Join('\n', drawn.Rows())}");

            drawn.Wheel(OverContent, Inside);
        }

        for (var notch = 0; notch < rows; notch++)
        {
            drawn.Wheel(OverContent, Inside);
        }
    }

    /// <summary>
    ///     The page wheeled to where the <paramref name="item" />th thing's first row is <paramref name="rows" /> rows
    ///     above the page's first row: nought for that row on the page's first row.
    /// </summary>
    private static void TopOffBy(DrawnShell drawn, int item, int rows)
    {
        while (drawn.Content.Top > 0)
        {
            drawn.Wheel(OverContent, Inside, down: false);
        }

        // A notch at a time, so the first page beginning on the thing is the one beginning on its top.
        var first = new Point(OverContent, drawn.Content.ViewportToScreen(Point.Empty).Y);

        for (var notch = 0; drawn.Content.ItemAt(first) != item; notch++)
        {
            Assert.True(notch < 100, $"No page begins on thing {item}:\n{string.Join('\n', drawn.Rows())}");

            drawn.Wheel(OverContent, Inside);
        }

        for (var notch = 0; notch < rows; notch++)
        {
            drawn.Wheel(OverContent, Inside);
        }
    }
}
