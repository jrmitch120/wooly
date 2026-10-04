using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     The people a post can mention, from what is already on screen (#318): every account the shell has read as screens
///     arrive, offered with no request of its own, best match first.
/// </summary>
public class PeopleToMentionTests
{
    /// <summary>
    ///     Replying to somebody and typing the start of their handle offers them at once, and asking costs nothing but
    ///     the one read of the profile's follows the first ask starts (#321): the people came with the screens that
    ///     were already read.
    /// </summary>
    [Fact]
    public async Task AReplyOffersWhoIsBeingAnswered_AtOnce()
    {
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "220", account: "ben@hachyderm.io", author: "Ben Adams")),
            Accounts = FakeAccountRelationships.HoldingNobody(),
        };

        var opened = await shell.Opened();

        opened.Reply();

        var before = shell.Requests;

        Assert.Equal(["ben@hachyderm.io"], Addresses(opened, "be"));
        Assert.Equal(["ben@hachyderm.io"], Addresses(opened, "ad"));
        Assert.Equal(before + 1, shell.Requests);
    }

    /// <summary>
    ///     Three tiers, first tier first however recently each was seen: a handle starting with what was typed, then a
    ///     word of the name starting with it, then a handle merely containing it.
    /// </summary>
    [Fact]
    public async Task MatchesComeInThreeTiers()
    {
        var opened = await Showing(
            APost.With(id: "1", account: "emma@c.social", author: "Emma"),
            APost.With(id: "2", account: "zed@b.social", author: "Maria Zed"),
            APost.With(id: "3", account: "mark@a.social", author: "Mark"),
            APost.With(id: "4", account: "nobody@d.social", author: "Nobody"));

        Assert.Equal(["mark@a.social", "zed@b.social", "emma@c.social"], Addresses(opened, "MA"));
    }

    /// <summary>Within a tier, whoever was seen most recently comes first — on one screen, the one nearer the top.</summary>
    [Fact]
    public async Task WithinATier_TheMostRecentlySeenComesFirst()
    {
        var opened = await Showing(
            APost.With(id: "1", account: "mark@a.social", author: "Mark"),
            APost.With(id: "2", account: "mary@b.social", author: "Mary"));

        Assert.Equal(["mark@a.social", "mary@b.social"], Addresses(opened, "ma"));

        opened.Walk(1, reclaiming: null);
        opened.Walk(1, reclaiming: null);
        opened.Reply();

        Assert.Equal("mary@b.social", Assert.IsType<ComposeScreen>(opened.Screen).About?.Account);

        Assert.Equal(["mary@b.social", "mark@a.social"], Addresses(opened, "ma"));
    }

    /// <summary>Five at most, and nobody twice however many posts they wrote.</summary>
    [Fact]
    public async Task NoMoreThanFive_AndNobodyTwice()
    {
        var opened = await Showing(
            APost.With(id: "1", account: "a1@x.social"),
            APost.With(id: "2", account: "a1@x.social"),
            APost.With(id: "3", account: "a2@x.social"),
            APost.With(id: "4", account: "a3@x.social"),
            APost.With(id: "5", account: "a4@x.social"),
            APost.With(id: "6", account: "a5@x.social"),
            APost.With(id: "7", account: "a6@x.social"));

        Assert.Equal(
            ["a1@x.social", "a2@x.social", "a3@x.social", "a4@x.social", "a5@x.social"],
            Addresses(opened, "a"));
    }

    /// <summary>A bare <c>@</c> offers the most recently seen, before a letter narrows them.</summary>
    [Fact]
    public async Task NothingTypedOffersTheMostRecentlySeen()
    {
        var opened = await Showing(
            APost.With(id: "1", account: "ben@hachyderm.io"),
            APost.With(id: "2", account: "maria@fosstodon.org"));

        Assert.Equal(["ben@hachyderm.io", "maria@fosstodon.org"], Addresses(opened, string.Empty));
    }

    /// <summary>Custom-emoji shortcodes come out of a name, so <c>:blobcat:</c> neither matches nor crowds it.</summary>
    [Fact]
    public async Task ShortcodesAreTakenOutOfNames()
    {
        var opened = await Showing(APost.With(id: "1", account: "mg@x.social", author: "Maria :blobcat: Gonzalez :verified:"));

        var maria = Assert.Single(opened.PeopleMatching("go"));

        Assert.Equal("Maria Gonzalez", maria.Name);
        Assert.Empty(opened.PeopleMatching("blob"));
    }

    /// <summary>
    ///     Everybody a post names is somebody who could be mentioned: who boosted it, who wrote what they boosted, and
    ///     everyone it mentions.
    /// </summary>
    [Fact]
    public async Task BoostersAuthorsAndMentionsAreAllOffered()
    {
        var boosted = APost.With(id: "1", account: "writer@x.social", author: "Writer", mentions: ["named@y.social"]);

        var opened = await Showing(APost.With(id: "2", account: "booster@z.social", author: "Booster", boosted: boosted));

        Assert.Equal(["booster@z.social"], Addresses(opened, "boo"));
        Assert.Equal(["writer@x.social"], Addresses(opened, "wri"));
        Assert.Equal(["named@y.social"], Addresses(opened, "nam"));
    }

    /// <summary>The profile's own account is nobody to mention.</summary>
    [Fact]
    public async Task TheProfileItselfIsNotOffered()
    {
        var opened = await Showing(APost.With(id: "1", account: "jeff@mastodon.social", author: "Jeff"));

        Assert.Empty(opened.PeopleMatching("je"));
    }

    /// <summary>A word that is already a whole, known address asks nothing more of the list.</summary>
    [Fact]
    public async Task ACompleteKnownAddressMatchesNothing()
    {
        var opened = await Showing(APost.With(id: "1", account: "mark@a.social"));

        Assert.Equal(["mark@a.social"], Addresses(opened, "mark@a.soc"));
        Assert.Empty(opened.PeopleMatching("mark@a.social"));
        Assert.Empty(opened.PeopleMatching("Mark@A.Social"));
    }

    /// <summary>
    ///     What a mention writes: the short <c>@user</c> for somebody on the profile's own instance, the full address
    ///     for anybody else.
    /// </summary>
    [Fact]
    public async Task AMentionIsShortOnTheProfilesOwnInstance()
    {
        var opened = await Showing(
            APost.With(id: "1", account: "maria@mastodon.social"),
            APost.With(id: "2", account: "ben@hachyderm.io"));

        Assert.Equal("@maria", opened.MentionOf(Assert.Single(opened.PeopleMatching("mar"))));
        Assert.Equal("@ben@hachyderm.io", opened.MentionOf(Assert.Single(opened.PeopleMatching("ben"))));
    }

    /// <summary>Notifications offer whoever they came from, as well as everyone their posts name.</summary>
    [Fact]
    public void ANotificationsScreenOffersWhoTheyCameFrom()
    {
        var screen = new NotificationsScreen([ANotification.Follow(account: "bob@mastodon.social")]);

        Assert.Contains(screen.Seen, person => person.Address == "bob@mastodon.social");
    }

    /// <summary>A switch of profile starts the people again: one account's are never offered to another.</summary>
    [Fact]
    public async Task SwitchingProfileEmptiesTheStore()
    {
        var shell = new AShell
        {
            Profiles = FakeProfileRegistry.Holding(
                "personal",
                FakeProfileRegistry.Profile("personal", "mastodon.social", "jeff@mastodon.social"),
                FakeProfileRegistry.Profile("work", "hachyderm.io", "jeff@hachyderm.io")),
            Timelines = FakeTimelineReader.Holding(APost.With(id: "1", account: "mark@a.social")),
        };

        var opened = await shell.Opened();

        Assert.NotEmpty(opened.PeopleMatching("mark"));

        shell.Timelines.NowHolding(APost.With(id: "2", account: "sam@b.social"));
        opened.Press(ShellKey.CtrlP);
        Assert.IsType<ProfilesScreen>(opened.Screen).Pick("work");
        opened.Press(ShellKey.Enter);
        shell.Host.Drain();

        Assert.Empty(opened.PeopleMatching("mark"));
        Assert.NotEmpty(opened.PeopleMatching("sam"));
    }

    private static async Task<Shell> Showing(params Post[] posts) =>
        await new AShell { Timelines = FakeTimelineReader.Holding(posts) }.Opened();

    private static IReadOnlyList<string> Addresses(Shell shell, string query) =>
        [.. shell.PeopleMatching(query).Select(person => person.Address)];
}
