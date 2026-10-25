using Wooly.Core.Notifications;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     Reading on, on Notifications (#465): as the page nears the end of the groups held, the shell asks the instance
///     for the older <b>Notification groups</b> behind them and adds them below — while a group arriving live still
///     goes on top. Older groups are behind the <b>Read marker</b>, so reading on leaves the badge and the marker alone
///     (ADR-0027). Where the page is comes from the window (<see cref="Shell.Viewed" />), as on Home (#469).
/// </summary>
public class ShellNotificationsReadingOnTests
{
    /// <summary>Where the rail's notifications destination is, counting from Home.</summary>
    private const int ToNotifications = 6;

    /// <summary>The height of the content region these tests say the page is, in rows.</summary>
    private const int Screenful = 20;

    /// <summary>
    ///     The page within a screenful of the end of the groups held: the groups behind the last one are asked for and
    ///     put under it, in the instance's order, with the pick left on the group it was on.
    /// </summary>
    [Fact]
    public async Task Viewed_WithinAScreenfulOfTheEnd_PutsTheOlderGroupsUnderThoseHeld()
    {
        var (built, opened) = await OnNotifications(groups: 100);

        opened.Walk(3, reclaiming: null);
        opened.Viewed(under: Screenful, screenful: Screenful);
        built.Host.Drain();

        var screen = Assert.IsType<NotificationsScreen>(opened.Screen);
        Assert.Single(built.Notifications.GroupReadsOn);
        Assert.Equal(Keys(1, 80), screen.Groups.Select(group => group.Key));
        Assert.Equal("g4", screen.PickedGroup?.Key);
    }

    /// <summary>
    ///     The older groups are behind the read marker, so reading on is not reading what is new: the marker is not
    ///     moved, nothing unread is asked about, the badge says what it said, and the older groups are drawn read.
    /// </summary>
    [Fact]
    public async Task ReadingOn_LeavesTheBadgeAndTheReadMarkerAlone()
    {
        var (built, opened) = await OnNotifications(groups: 100);
        var marker = built.Notifications.Marker;
        var moves = built.Notifications.MarkerMoves.Count;
        var unreadReads = built.Notifications.UnreadReads.Count;
        var badge = Badge(opened);

        opened.Viewed(under: 0, screenful: Screenful);
        built.Host.Drain();

        var screen = Assert.IsType<NotificationsScreen>(opened.Screen);
        Assert.Equal(80, screen.Groups.Count);
        Assert.Equal(marker, built.Notifications.Marker);
        Assert.Equal(moves, built.Notifications.MarkerMoves.Count);
        Assert.Equal(unreadReads, built.Notifications.UnreadReads.Count);
        Assert.Equal((badge.Unread, badge.Full), (Badge(opened).Unread, Badge(opened).Full));
        Assert.All(screen.Groups.Skip(40), group => Assert.False(screen.IsUnread(group)));
    }

    /// <summary>
    ///     A group arriving live while a read on is out still goes on top, unread, and the older groups still land at
    ///     the bottom when it answers: neither waits for the other, and neither moves what the other put, the pick
    ///     staying on the group it was on.
    /// </summary>
    [Fact]
    public async Task ALiveGroupArrivingWhileAReadOnIsOut_GoesOnTopAndTheOlderGroupsStillLandAtTheBottom()
    {
        var (built, opened) = await OnNotifications(groups: 100);

        opened.Walk(3, reclaiming: null);
        built.Notifications.HoldReadsOn();
        opened.Viewed(under: 0, screenful: Screenful);
        built.Host.Drain();

        Arrives(built, [Group(0), .. built.Notifications.Groups!]);

        var screen = Assert.IsType<NotificationsScreen>(opened.Screen);
        Assert.Equal(Keys(0, 40), screen.Groups.Select(group => group.Key));

        built.Notifications.LetReadsOnGo();
        built.Host.Drain();

        Assert.Same(screen, opened.Screen);
        Assert.Equal(Keys(0, 80), screen.Groups.Select(group => group.Key));
        Assert.True(screen.IsUnread(screen.Groups[0]));
        Assert.Equal("g4", screen.PickedGroup?.Key);
    }

    /// <summary>
    ///     And the other way about: a group arriving live after the older groups landed goes on top of everything,
    ///     the older groups left where they are at the bottom — and the next read on goes on from the last of them.
    /// </summary>
    [Fact]
    public async Task ALiveGroupArrivingAfterAReadOn_GoesOnTopAndTheNextReadOnGoesOnFromTheBottom()
    {
        var (built, opened) = await OnNotifications(groups: 100);

        opened.Viewed(under: 0, screenful: Screenful);
        built.Host.Drain();
        Arrives(built, [Group(0), .. built.Notifications.Groups!]);
        opened.Viewed(under: 0, screenful: Screenful);
        built.Host.Drain();

        var screen = Assert.IsType<NotificationsScreen>(opened.Screen);
        Assert.Equal(Keys(0, 100), screen.Groups.Select(group => group.Key));
        Assert.Equal(Group(80).NewestId, built.Notifications.GroupReadsOn[^1].From.MaxId);
    }

    /// <summary>
    ///     The instance groups a page at a time, so a group can come back on an older page with older notifications
    ///     of its own. It is already on screen, under its newest, and stays there drawn once: a key held twice is a
    ///     row that <c>d</c> and <c>w</c> could not tell from the other.
    /// </summary>
    [Fact]
    public async Task ReadOn_BringingBackAGroupAlreadyHeld_LeavesItWhereItIsAndDrawsItOnce()
    {
        var (built, opened) = await OnNotifications(groups: 100);
        var groups = built.Notifications.Groups!.ToList();
        groups.Insert(41, Group(40) with { NewestId = "1" });
        built.Notifications.Groups = groups;

        opened.Viewed(under: 0, screenful: Screenful);
        built.Host.Drain();

        var screen = Assert.IsType<NotificationsScreen>(opened.Screen);
        Assert.Single(screen.Groups, group => group.Key == "g40");
        Assert.Equal(39, screen.Groups.ToList().FindIndex(group => group.Key == "g40"));
        Assert.Equal(Keys(1, 79), screen.Groups.Select(group => group.Key));
    }

    /// <summary>
    ///     An instance too old to group reads on too: every older notification a group of one, put under those held,
    ///     as the screen first read them (ADR-0032).
    /// </summary>
    [Fact]
    public async Task Viewed_OnAnInstanceWithNoGroupedRead_PutsTheOlderNotificationsUnderThoseHeldEachAGroupOfOne()
    {
        var built = new AShell
        {
            Notifications = FakeNotificationInbox.Holding(
                [.. Enumerable.Range(1, 100).Select(at => ANotification.With(id: $"{10_000 - at}"))]),
        };
        built.Notifications.Marker = $"{10_000 - 1}";
        var opened = await built.Opened();

        opened.Step(ToNotifications);
        built.Host.Settle();
        opened.Viewed(under: 0, screenful: Screenful);
        built.Host.Drain();

        var screen = Assert.IsType<NotificationsScreen>(opened.Screen);
        Assert.Equal(80, screen.Groups.Count);
        Assert.Equal($"{10_000 - 80}", screen.Groups[^1].NewestId);
        Assert.All(screen.Groups, group => Assert.Equal(1, group.Count));
    }

    /// <summary>
    ///     The foot of the list says a read is in flight under the last group held, and once the instance has nothing
    ///     behind them, that the inbox is whole — after which nothing more is asked.
    /// </summary>
    [Fact]
    public async Task Foot_SaysReadingOnWhileAReadIsOut_AndThatsEverythingOnceTheInboxIsWhole()
    {
        var (built, opened) = await OnNotifications(groups: 50);

        built.Notifications.HoldReadsOn();
        opened.Viewed(under: 0, screenful: Screenful);
        built.Host.Drain();
        var inFlight = Foot(opened.Screen);

        built.Notifications.LetReadsOnGo();
        built.Host.Drain();
        opened.Viewed(under: 0, screenful: Screenful);
        built.Host.Drain();

        Assert.Equal("reading on…", inFlight);
        Assert.Equal("that's everything", Foot(opened.Screen));
        Assert.Single(built.Notifications.GroupReadsOn);
    }

    /// <summary>The last row <paramref name="screen" /> draws, which is the foot of its list where it has one.</summary>
    private static string Foot(Screen screen) =>
        screen.Lines(new Drawing(61, AShell.Now))[^1].Text;

    /// <summary>
    ///     Notifications open on <paramref name="groups" /> groups, newest first, keyed <c>g1</c> on, every one read:
    ///     the read marker is at the newest.
    /// </summary>
    private static async Task<(AShell Built, Shell Opened)> OnNotifications(int groups)
    {
        var held = Enumerable.Range(1, groups).Select(Group).ToList();
        var built = new AShell { Notifications = FakeNotificationInbox.Holding([.. held.Select(Newest)]) };
        built.Notifications.Groups = held;
        built.Notifications.Marker = held[0].NewestId;
        var opened = await built.Opened();

        opened.Step(ToNotifications);
        built.Host.Settle();

        return (built, opened);
    }

    /// <summary>
    ///     The instance now groups what it holds as <paramref name="groups" />, and a poll a minute on re-reads it — as a
    ///     stream event would.
    /// </summary>
    private static void Arrives(AShell built, IReadOnlyList<NotificationGroup> groups)
    {
        built.Notifications.NowHolding([.. groups.Select(Newest)]);
        built.Notifications.Groups = groups;
        built.Clock.Advance(TimeSpan.FromSeconds(60));
        built.Host.Drain();
    }

    /// <summary>The <paramref name="at" />th group from the top, its newest notification older the further down.</summary>
    private static NotificationGroup Group(int at) =>
        ANotification.Group(key: $"g{at}", newestId: $"{10_000 - at}", actors: ["Alice"]);

    /// <summary>The newest notification in <paramref name="group" />, as the unread count reads it.</summary>
    private static Notification Newest(NotificationGroup group) => ANotification.With(id: group.NewestId);

    /// <summary>The rail's Notifications destination, which carries the badge.</summary>
    private static Destination Badge(Shell shell) =>
        shell.Rail.Destinations.Single(destination => destination.Kind == DestinationKind.Notifications);

    /// <summary>The keys of the groups from <paramref name="first" /> to <paramref name="last" />, in order.</summary>
    private static IEnumerable<string> Keys(int first, int last) =>
        Enumerable.Range(first, last - first + 1).Select(at => $"g{at}");
}
