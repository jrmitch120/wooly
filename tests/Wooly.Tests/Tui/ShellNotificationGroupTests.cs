using Wooly.Core.Notifications;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     Notifications drawn as <b>Notification groups</b>, as the instance groups them (#453, ADR-0032): one row for
///     eight favorites of one post, naming two actors and counting the rest, with the post under it once. Layout A7b of
///     #450 — the names, then the kind's icon and sentence in the kind's colour, and above a post drawn whole by its
///     actor, the kind alone.
/// </summary>
public class ShellNotificationGroupTests
{
    /// <summary>Where the rail's notifications destination is, counting from Home.</summary>
    private const int ToNotifications = 6;

    /// <summary>
    ///     A group names two actors and counts everyone else in it by the instance's count, not by how many actors came
    ///     with it: eight favorites sent with two actors is "and 6 others".
    /// </summary>
    [Fact]
    public async Task Arriving_DrawsAGroupAsOneRowNamingTwoActorsAndCountingTheRest()
    {
        var (shell, opened) = await Arrived(
            ANotification.Group(count: 8, actors: ["Alice", "Bob"]));

        var screen = Assert.IsType<NotificationsScreen>(opened.Screen);
        var lines = Drawn(screen);

        Assert.Single(screen.Groups);
        Assert.Contains(lines, line => line.StartsWith(
            "Alice, Bob and 6 others ★ favorited your post",
            StringComparison.Ordinal));
        Assert.Single(shell.Notifications.GroupReads);
    }

    /// <summary>
    ///     Two, three and one read as English: "and 1 other" is singular, two actors are joined with "and", and a group
    ///     of one is the same names line with no count at all.
    /// </summary>
    [Theory]
    [InlineData(1, new[] { "Alice" }, "Alice + followed you")]
    [InlineData(2, new[] { "Alice", "Bob" }, "Alice and Bob + followed you")]
    [InlineData(3, new[] { "Alice", "Bob", "Carol" }, "Alice, Bob and 1 other + followed you")]
    public async Task Arriving_NamesAsManyActorsAsTheGroupHas(int count, string[] actors, string expected)
    {
        var (_, opened) = await Arrived(
            ANotification.Group(kind: NotificationKind.Follow, count: count, actors: actors, noPost: true));

        Assert.Contains(Drawn(opened.Screen), line => line.StartsWith(expected, StringComparison.Ordinal));
    }

    /// <summary>
    ///     The icon and sentence go in the kind's colour, the same roles a post's marks use — so the icon still says the
    ///     kind where there is no colour — and the names in the byline's.
    /// </summary>
    [Fact]
    public async Task Arriving_DrawsTheKindInItsOwnColour()
    {
        var (_, opened) = await Arrived(
            ANotification.Group(key: "f", kind: NotificationKind.Favorite, newestId: "36", actors: ["Alice"]),
            ANotification.Group(key: "b", kind: NotificationKind.Boost, newestId: "35", actors: ["Bob"]));

        var lines = opened.Screen.Lines(new Drawing(61, AShell.Now));
        var spans = lines.SelectMany(line => line.Spans).ToList();

        Assert.Contains(spans, span => span is { Text: " ★ favorited your post", Role: Role.Favorite });
        Assert.Contains(spans, span => span is { Text: " ↺ boosted your post", Role: Role.Boost });
        Assert.Contains(spans, span => span is { Text: "Alice", Role: Role.BylineName });
    }

    /// <summary>A group's age is its newest notification's, so the row says when it last happened.</summary>
    [Fact]
    public async Task Arriving_AgesAGroupByItsNewestNotification()
    {
        var group = ANotification.Group(actors: ["Alice"]) with { NewestAt = AShell.Now.AddHours(-2) };

        var (_, opened) = await Arrived(group);

        Assert.Contains(Drawn(opened.Screen), line => line.Contains("favorited your post", StringComparison.Ordinal)
                                                      && line.TrimEnd().EndsWith("2h", StringComparison.Ordinal));
    }

    /// <summary>
    ///     A group is unread when anything in it is newer than the read marker — which its newest notification then is.
    ///     The marker moves to the newest group's newest notification, as it moved to the newest notification before.
    /// </summary>
    [Fact]
    public async Task Arriving_DrawsAGroupUnreadWhereItsNewestIsPastTheMarker()
    {
        var (shell, opened) = await Arrived(
            marker: "40",
            ANotification.Group(key: "new", newestId: "45", actors: ["Alice"]),
            ANotification.Group(key: "old", newestId: "39", actors: ["Bob"], kind: NotificationKind.Boost));

        var screen = Assert.IsType<NotificationsScreen>(opened.Screen);

        Assert.True(screen.IsUnread(screen.Groups[0]));
        Assert.False(screen.IsUnread(screen.Groups[1]));
        Assert.Contains(Drawn(screen), line => line.Contains("Alice", StringComparison.Ordinal)
                                               && line.Contains(ConversationLines.UnreadMark, StringComparison.Ordinal));
        Assert.DoesNotContain(Drawn(screen), line => line.Contains("Bob", StringComparison.Ordinal)
                                                     && line.Contains(ConversationLines.UnreadMark, StringComparison.Ordinal));
        Assert.Equal("45", Assert.Single(shell.Notifications.MarkerMoves).NotificationId);
    }

    /// <summary>
    ///     A post drawn whole by its actor — a mention — is headed by the kind alone: the byline under it already says
    ///     who and when, and is left exactly as on every feed (#450, ADR-0032).
    /// </summary>
    [Fact]
    public async Task Arriving_HeadsAMentionWithItsKindAloneAndLeavesItsBylineAsOnAFeed()
    {
        var mention = APost.With(id: "120", account: "robin@carpdiem.online", author: "Robin Ward");
        var group = ANotification.Group(
            key: "ungrouped-34",
            kind: NotificationKind.Mention,
            actors: ["Robin Ward"],
            post: mention) with
        {
            Actors = [AnAccount.With(address: "robin@carpdiem.online", author: "Robin Ward", id: "7")],
        };

        var (_, opened) = await Arrived(group);
        var lines = Drawn(opened.Screen);

        Assert.Equal("@ mentioned you", lines[0].TrimEnd().Replace(ConversationLines.UnreadMark, "").TrimEnd());
        Assert.Contains(lines.Skip(1), line => line.Contains("Robin Ward", StringComparison.Ordinal));
        Assert.Single(lines, line => line.Contains("Robin Ward", StringComparison.Ordinal));
    }

    /// <summary>A kind this client has no word for still appears, under the instance's own word (ADR-0010).</summary>
    [Fact]
    public async Task Arriving_DrawsAKindItHasNoWordForUnderTheInstancesWord()
    {
        var (_, opened) = await Arrived(
            ANotification.Group(kind: NotificationKind.Reported("poll"), actors: ["Alice"]));

        Assert.Contains(Drawn(opened.Screen), line => line.Contains("notified you (poll)", StringComparison.Ordinal));
    }

    /// <summary>
    ///     The post under a group is drawn the way a feed draws it, and is what the keys that act on a post act on — so
    ///     <c>⏎</c> opens it. A follow has none.
    /// </summary>
    [Fact]
    public async Task Enter_OpensThePostUnderTheGroup()
    {
        var (shell, opened) = await Arrived(
            ANotification.Group(post: APost.With(id: "110", content: "Spent the weekend grouping", isMine: true)));

        Assert.Contains(Drawn(opened.Screen), line => line.Contains("Spent the weekend grouping", StringComparison.Ordinal));
        Assert.Equal("110", opened.Screen.Picked?.Id);

        opened.Press(ShellKey.Enter);
        shell.Host.Drain();

        Assert.IsType<PostScreen>(opened.Screen);
    }

    /// <summary>A group about a post that is no longer there is a row about nothing, so it leaves with the post.</summary>
    [Fact]
    public async Task Heard_TakesOffAGroupWhosePostIsDeleted()
    {
        var (_, opened) = await Arrived(
            ANotification.Group(key: "a", newestId: "36", post: APost.With(id: "110", isMine: true)),
            ANotification.Group(key: "b", newestId: "35", kind: NotificationKind.Follow, noPost: true));

        var screen = Assert.IsType<NotificationsScreen>(opened.Screen);

        screen.Heard(new Change.PostGone("110"));

        Assert.Equal(["b"], screen.Groups.Select(group => group.Key));
    }

    /// <summary>
    ///     Favorites of one post in two different hours are two groups to the instance, and two rows here: grouping is
    ///     the instance's call, so rows are never merged by post (#448).
    /// </summary>
    [Fact]
    public async Task Arriving_DrawsTwoGroupsAboutOnePostAsTwoRows()
    {
        var (_, opened) = await Arrived(
            ANotification.Group(key: "favourite-110-493001", newestId: "36", actors: ["Alice"]),
            ANotification.Group(key: "favourite-110-493000", newestId: "35", actors: ["Bob"]));

        Assert.Equal(2, Assert.IsType<NotificationsScreen>(opened.Screen).Groups.Count);
    }

    private static Task<(AShell Shell, Shell Opened)> Arrived(params NotificationGroup[] groups) =>
        Arrived("0", groups);

    private static async Task<(AShell Shell, Shell Opened)> Arrived(
        string marker,
        params NotificationGroup[] groups)
    {
        var shell = new AShell { Notifications = FakeNotificationInbox.Holding() };
        shell.Notifications.Groups = groups;
        shell.Notifications.Marker = marker;

        var opened = await shell.Opened();

        opened.Step(ToNotifications);
        shell.Host.Settle();

        return (shell, opened);
    }

    /// <summary>The screen's rows as text, each without the gutter column that marks the row picked out.</summary>
    private static List<string> Drawn(Screen screen) =>
        [.. screen.Lines(new Drawing(61, AShell.Now)).Select(line => line.Text.Length > 0 ? line.Text[1..] : "")];
}
