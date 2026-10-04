using Wooly.Core.Accounts;
using Wooly.Core.Errors;
using Wooly.Core.Relationships;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     The accounts the profile follows, as people a post can mention (#321): read once, in the background, the first
///     time an <c>@</c> is typed in a session — and never for a reader who types none.
/// </summary>
public class FollowsToMentionTests
{
    /// <summary>Composing without ever typing <c>@</c> asks nothing for mentions.</summary>
    [Fact]
    public async Task ComposingWithNoAtReadsNoFollows()
    {
        var shell = new AShell { Accounts = Following(Account("maria@b.social", "Maria")) };
        var opened = await shell.Opened();

        opened.Compose();
        shell.Host.Drain();

        Assert.Empty(FollowReads(shell));
    }

    /// <summary>
    ///     The first <c>@</c> reads the profile's own follows, nobody named, capped at five pages of 80 — and the
    ///     follows it finds are offered once it lands.
    /// </summary>
    [Fact]
    public async Task TheFirstAtReadsTheProfilesFollows()
    {
        var shell = new AShell { Accounts = Following(Account("maria@b.social", "Maria")) };
        var opened = await shell.Opened();

        opened.Compose();
        opened.PeopleMatching("mar");
        shell.Host.Drain();

        var listed = Assert.Single(FollowReads(shell));

        Assert.Equal(new FakeAccountRelationships.Listed("personal", FollowSide.Following, null, 400), listed);
        Assert.Equal(["maria@b.social"], Addresses(opened, "mar"));
    }

    /// <summary>Later <c>@</c>s, and later compose screens in the same session, ask nothing more.</summary>
    [Fact]
    public async Task TheFollowsAreReadOnceASession()
    {
        var shell = new AShell { Accounts = Following(Account("maria@b.social", "Maria")) };
        var opened = await shell.Opened();

        opened.Compose();
        opened.PeopleMatching(string.Empty);
        shell.Host.Drain();
        opened.PeopleMatching("m");
        opened.Back();
        opened.Compose();
        opened.PeopleMatching(string.Empty);
        shell.Host.Drain();

        Assert.Single(FollowReads(shell));
    }

    /// <summary>
    ///     Within a tier, people seen on screen still come first, and the follows after them in order of name — whatever
    ///     order the instance listed them in.
    /// </summary>
    [Fact]
    public async Task FollowsComeAfterPeopleOnScreen_InOrderOfName()
    {
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "1", account: "mark@a.social", author: "Mark")),
            Accounts = Following(
                Account("zed@d.social", "Maria Zed"),
                Account("maria@b.social", "Maria"),
                Account("mabel@c.social", "Mabel")),
        };

        var opened = await shell.Opened();

        opened.PeopleMatching("ma");
        shell.Host.Drain();

        Assert.Equal(["mark@a.social", "mabel@c.social", "maria@b.social", "zed@d.social"], Addresses(opened, "ma"));
    }

    /// <summary>Somebody both seen and followed is offered once, as somebody seen.</summary>
    [Fact]
    public async Task SomebodySeenAndFollowedIsOfferedOnce()
    {
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "1", account: "maria@b.social", author: "Maria"),
                APost.With(id: "2", account: "mark@a.social", author: "Mark")),
            Accounts = Following(Account("maria@b.social", "Maria"), Account("mabel@c.social", "Mabel")),
        };

        var opened = await shell.Opened();

        opened.PeopleMatching("ma");
        shell.Host.Drain();

        Assert.Equal(["maria@b.social", "mark@a.social", "mabel@c.social"], Addresses(opened, "ma"));
    }

    /// <summary>A whole address somebody followed answers to asks nothing more of the list.</summary>
    [Fact]
    public async Task ACompleteFollowedAddressMatchesNothing()
    {
        var shell = new AShell { Accounts = Following(Account("maria@b.social", "Maria")) };
        var opened = await shell.Opened();

        opened.PeopleMatching(string.Empty);
        shell.Host.Drain();

        Assert.Empty(opened.PeopleMatching("maria@b.social"));
    }

    /// <summary>A read the rate limit stopped keeps whatever it got, and says nothing.</summary>
    [Fact]
    public async Task ARateLimitedReadKeepsWhatItGot_Silently()
    {
        var shell = new AShell
        {
            Accounts = FakeAccountRelationships.RateLimitedAfter(Account("maria@b.social", "Maria")),
        };

        var opened = await shell.Opened();

        opened.Compose();
        opened.PeopleMatching("m");
        shell.Host.Drain();

        Assert.Equal(["maria@b.social"], Addresses(opened, "mar"));
        Assert.Null(opened.Notice);
    }

    /// <summary>
    ///     A refused read leaves the people on screen offered as they were, says nothing, and is not asked again in the
    ///     session.
    /// </summary>
    [Fact]
    public async Task ARefusedReadLeavesWhatWasKnown_Silently()
    {
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "1", account: "mark@a.social", author: "Mark")),
            Accounts = FakeAccountRelationships.Refusing(new AuthenticationException("No.")),
        };

        var opened = await shell.Opened();

        opened.Compose();
        opened.PeopleMatching("m");
        shell.Host.Drain();

        Assert.Equal(["mark@a.social"], Addresses(opened, "ma"));
        Assert.Null(opened.Notice);
        Assert.IsType<ComposeScreen>(opened.Screen);
        Assert.Single(FollowReads(shell));
    }

    /// <summary>A profile switch drops the follows, and the next <c>@</c> reads the new profile's.</summary>
    [Fact]
    public async Task ASwitchDropsTheFollows_AndTheNextAtReadsTheNewProfiles()
    {
        var shell = new AShell
        {
            Profiles = FakeProfileRegistry.Holding(
                "personal",
                FakeProfileRegistry.Profile("personal", "mastodon.social", "jeff@mastodon.social"),
                FakeProfileRegistry.Profile("work", "hachyderm.io", "jeff@hachyderm.io")),
            Accounts = Following(Account("maria@b.social", "Maria")),
        };

        var opened = await shell.Opened();

        opened.PeopleMatching(string.Empty);
        shell.Host.Drain();

        Assert.NotEmpty(opened.PeopleMatching("maria"));

        opened.Press(ShellKey.CtrlP);
        Assert.IsType<ProfilesScreen>(opened.Screen).Pick("work");
        opened.Press(ShellKey.Enter);
        shell.Host.Drain();

        Assert.Equal(["personal"], FollowReads(shell).Select(listed => listed.Profile));

        opened.PeopleMatching(string.Empty);
        shell.Host.Drain();

        Assert.Equal(["personal", "work"], FollowReads(shell).Select(listed => listed.Profile));
    }

    /// <summary>
    ///     Following somebody through the shell makes them somebody to mention at once, with no read; unfollowing drops
    ///     them from the follows offered.
    /// </summary>
    [Fact]
    public void AFollowAddsToTheFollows_AndAnUnfollowDropsThem()
    {
        var people = new PeopleToMention(new AShell().Profile);
        var zoe = Account("zoe@x.social", "Zoe");

        people.Tied(zoe with { Standing = AnAccount.Standing(following: true) });

        Assert.Equal(["zoe@x.social"], people.Matching("zo").Select(person => person.Address));

        people.Tied(zoe with { Standing = AnAccount.Standing(following: false) });

        Assert.Empty(people.Matching("zo"));
    }

    /// <summary>A follow that is only a request waiting on a locked account is not a follow yet.</summary>
    [Fact]
    public void AFollowRequestIsNotAFollow()
    {
        var people = new PeopleToMention(new AShell().Profile);

        people.Tied(Account("zoe@x.social", "Zoe") with { Standing = AnAccount.Standing(followRequested: true) });

        Assert.Empty(people.Matching("zo"));
    }

    /// <summary>
    ///     An unfollow made while the follow list was still being read is not undone by a page that listed them before
    ///     it.
    /// </summary>
    [Fact]
    public void AnUnfollowOutlastsAPageReadBeforeIt()
    {
        var people = new PeopleToMention(new AShell().Profile);
        var zoe = Account("zoe@x.social", "Zoe");

        people.Tied(zoe with { Standing = AnAccount.Standing(following: false) });
        people.Followed([zoe]);

        Assert.Empty(people.Matching("zo"));
    }

    /// <summary>A follow made through the shell reaches the store, asking nothing beyond the follow itself.</summary>
    [Fact]
    public async Task AFollowThroughTheShellAsksNoRead()
    {
        var ben = AnAccount.With(id: "42", address: "ben@hachyderm.io", author: "Ben");

        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "1", account: "ben@hachyderm.io", author: "Ben")),
            Accounts = FakeAccountRelationships.Holding(ben),
        };

        var opened = await shell.Opened();

        opened.PeopleMatching(string.Empty);
        shell.Host.Drain();

        await opened.OpenAuthor();
        shell.Host.Drain();

        shell.Accounts.Becoming = ben with { Standing = AnAccount.Standing(following: true) };
        opened.Press(Pressing.Tying(AccountTie.Follow));
        shell.Host.Drain();

        var asked = shell.Requests;

        Assert.Equal(["ben@hachyderm.io"], Addresses(opened, "be"));
        Assert.Equal(asked, shell.Requests);
        Assert.Single(FollowReads(shell));
    }

    /// <summary>Every read of the profile's own follows — the counts' read of follow requests is no part of this.</summary>
    private static IEnumerable<FakeAccountRelationships.Listed> FollowReads(AShell shell) =>
        shell.Accounts.Lists.Where(listed => listed.Side == FollowSide.Following && listed.Account is null);

    private static Account Account(string address, string author) =>
        AnAccount.With(id: address, address: address, author: author);

    private static FakeAccountRelationships Following(params Account[] follows) =>
        FakeAccountRelationships.Holding(null, follows);

    private static IReadOnlyList<string> Addresses(Shell shell, string query) =>
        [.. shell.PeopleMatching(query).Select(person => person.Address)];
}
