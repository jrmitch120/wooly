using Wooly.Core.Accounts;
using Wooly.Core.Relationships;
using Wooly.Tests.Fakes;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     What an arrival at an account costs, which is one resolving account search and not three (#184, ADR-0012's
///     second amendment). The resolution the first call produced travels to the reads that follow it, and never to a
///     write.
/// </summary>
/// <remarks>
///     Asked at the port fakes rather than at HTTP, because what is being decided here is what the shell hands each
///     port — whether a read is given a resolution somebody has already paid for. What an adapter then does with one
///     is <c>TimelineReaderTests</c>'s and <c>AccountRelationshipsTests</c>'s, at the seam ADR-0005 puts them at.
/// </remarks>
public class AccountResolutionTests
{
    /// <summary>Whoever the account screen is about in these tests.</summary>
    private const string Whose = "ben@hachyderm.io";

    /// <summary>
    ///     One account read for the whole arrival: the timeline and the pinned run are asked for by naming the account
    ///     that read answered with, id and all, rather than by handing back the address and paying twice more.
    /// </summary>
    [Fact]
    public async Task OpeningAnAccountResolvesTheAddressOnceAndPassesTheResolutionOn()
    {
        var (fakes, _) = await OnTheAccountScreen();

        var read = Assert.Single(fakes.Accounts.Reads);
        Assert.Equal(Whose, read.Account.Text);

        var named = NamedIn(fakes, from: 0);

        Assert.Equal(2, named.Count);
        Assert.All(named, whose => Assert.Equal("7", whose.Id));
        Assert.All(named, whose => Assert.Equal(Whose, whose.Address.Text));
    }

    /// <summary>
    ///     And the browser opened off that screen is holding the same resolution, so listing a side of their follows
    ///     costs the list and nothing else.
    /// </summary>
    [Fact]
    public async Task OpeningTheFollowsBrowserNamesAnAccountItAlreadyHasTheIdFor()
    {
        var (fakes, opened) = await OnTheAccountScreen();

        var reads = fakes.Accounts.Reads.Count;

        await opened.OpenFollows();
        fakes.Host.Drain();

        var listed = Assert.Single(fakes.Accounts.Lists, list => list.Side is not null);

        Assert.Equal("7", listed.Account?.Id);
        Assert.Equal(Whose, listed.Account?.Address.Text);

        // Nothing was read again to arrive at an id the screen was already holding.
        Assert.Equal(reads, fakes.Accounts.Reads.Count);
    }

    /// <summary>
    ///     A refresh resolves again, deliberately: it is the one command meaning "check this is still true", so it
    ///     must be the one command that can correct an id the reader is holding for somebody who has since moved
    ///     instances. The resolution it passes on is the one it just made.
    /// </summary>
    [Fact]
    public async Task RefreshingAnAccountResolvesTheAddressAgainRatherThanReusingWhatIsHeld()
    {
        var (fakes, opened) = await OnTheAccountScreen();

        var before = fakes.Timelines.Reads.Count;

        // The account moved instances while the screen was up, so the id the reader is holding now names nobody.
        fakes.Accounts.NowShowing(AnAccount.With(address: Whose, id: "9"));

        await opened.Refresh();
        fakes.Host.Drain();

        Assert.Equal(2, fakes.Accounts.Reads.Count);
        Assert.Equal(Whose, fakes.Accounts.Reads[^1].Account.Text);

        // And the reads that follow travel on what that resolution answered, which is the whole of what a refresh is
        // for: the one command meaning "check this is still true" is the one that can correct a wrong id.
        Assert.All(
            NamedIn(fakes, from: before),
            whose => Assert.Equal("9", whose.Id));
    }

    /// <summary>
    ///     No write ever takes a resolution, however well the caller knows the answer: a tie is put on the address,
    ///     and the port looks it up itself.
    /// </summary>
    [Theory]
    [InlineData(AccountTie.Follow)]
    [InlineData(AccountTie.Block)]
    [InlineData(AccountTie.Mute)]
    public async Task PuttingATieOnTheAccountShowingStillNamesTheAddress(AccountTie tie)
    {
        var (fakes, opened) = await OnTheAccountScreen();

        await opened.Tie(tie);
        fakes.Host.Drain();

        var tied = Assert.Single(fakes.Accounts.Ties);

        Assert.Equal(tie, tied.Tie);
        Assert.Equal(Whose, tied.Account.Text);
    }

    /// <summary>
    ///     Whoever the timeline reads made since <paramref name="from" /> named — the account timeline and the pinned
    ///     run, and never the four timelines that belong to nobody.
    /// </summary>
    private static IReadOnlyList<NamedAccount> NamedIn(AShell fakes, int from) =>
    [
        .. fakes.Timelines.Reads
            .Skip(from)
            .Select(read => read.Timeline.Account)
            .OfType<NamedAccount>(),
    ];

    /// <summary>A shell drilled into <see cref="Whose" />'s account screen, whose id the instance answers with.</summary>
    private static async Task<(AShell Fakes, Shell Opened)> OnTheAccountScreen()
    {
        var fakes = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "110", account: Whose)),
            Accounts = FakeAccountRelationships.Holding(AnAccount.With(address: Whose, id: "7")),
        };

        var opened = await fakes.Opened();

        await opened.OpenAuthor();
        fakes.Host.Drain();

        return (fakes, opened);
    }
}
