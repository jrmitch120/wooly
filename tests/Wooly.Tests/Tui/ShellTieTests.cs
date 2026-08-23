using Wooly.Core.Accounts;
using Wooly.Core.Relationships;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     The three tie actions on the account screen. A tie is on or off rather than an act of its own (ADR-0012), so the
///     decision these test is the one the shell makes before the call goes out: which way round the key means, read off
///     the standing the instance answered with.
/// </summary>
public class ShellTieTests
{
    [Theory]
    [InlineData(AccountTie.Follow)]
    [InlineData(AccountTie.Block)]
    [InlineData(AccountTie.Mute)]
    public async Task Tie_PutsATieOnAnAccountThatDoesNotHaveIt(AccountTie tie)
    {
        var (fakes, opened) = await OnTheAccountScreen(AnAccount.Standing());

        await opened.Tie(tie);
        fakes.Host.Drain();

        var tied = Assert.Single(fakes.Accounts.Ties);
        Assert.Equal("ben@hachyderm.io", tied.Account.Text);
        Assert.Equal(tie, tied.Tie);
        Assert.True(tied.Wanted);
    }

    [Theory]
    [InlineData(AccountTie.Follow)]
    [InlineData(AccountTie.Block)]
    [InlineData(AccountTie.Mute)]
    public async Task Tie_TakesATieOffAnAccountThatAlreadyHasIt(AccountTie tie)
    {
        var standing = AnAccount.Standing(following: true, blocking: true, muting: true);
        var (fakes, opened) = await OnTheAccountScreen(standing);

        await opened.Tie(tie);
        fakes.Host.Drain();

        Assert.False(Assert.Single(fakes.Accounts.Ties).Wanted);
    }

    /// <summary>
    ///     A follow this account has not answered yet is a follow to take back, not one to ask for twice — what
    ///     <c>F</c> undoes on a locked account is the request.
    /// </summary>
    [Fact]
    public async Task Tie_TakesBackAFollowThatIsStillWaitingToBeAccepted()
    {
        var (fakes, opened) = await OnTheAccountScreen(AnAccount.Standing(followRequested: true));

        await opened.Tie(AccountTie.Follow);
        fakes.Host.Drain();

        Assert.False(Assert.Single(fakes.Accounts.Ties).Wanted);
    }

    /// <summary>
    ///     A standing that only changed once the screen was opened again would make the key feel broken, so the account
    ///     the instance answered with replaces the copy the screen is holding.
    /// </summary>
    [Fact]
    public async Task Tie_DrawsTheStandingAsTheInstanceNowHasIt()
    {
        var followed = AnAccount.With(address: "ben@hachyderm.io", standing: AnAccount.Standing(following: true));
        var (fakes, opened) = await OnTheAccountScreen(AnAccount.Standing(), becoming: followed);

        await opened.Tie(AccountTie.Follow);
        fakes.Host.Drain();

        var account = Assert.IsType<AccountScreen>(opened.Screen);
        Assert.True(account.Account.Standing?.Following);
        Assert.True(account.Has(AccountTie.Follow));

        var drawn = account.Lines(new Drawing(61, AShell.Now)).Select(line => line.Text);
        Assert.Contains(drawn, line => line.Contains("you follow them", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Following a locked account leaves a request behind rather than a follow, and the instance's own answer is
    ///     the only thing that says which happened — so "now following" is not said about one (CONTEXT.md).
    /// </summary>
    [Fact]
    public async Task Tie_SaysAFollowOfALockedAccountWasAskedForRatherThanDone()
    {
        var waiting = AnAccount.With(
            address: "ben@hachyderm.io",
            standing: AnAccount.Standing(followRequested: true));

        var (fakes, opened) = await OnTheAccountScreen(AnAccount.Standing(), becoming: waiting);

        await opened.Tie(AccountTie.Follow);
        fakes.Host.Drain();

        Assert.Equal("Asked to follow @ben@hachyderm.io.", opened.Notice);
        Assert.False(opened.NoticeIsError);
    }

    [Fact]
    public async Task Tie_SaysWhatWentOnOrCameOff()
    {
        var (fakes, opened) = await OnTheAccountScreen(AnAccount.Standing(muting: true));

        await opened.Tie(AccountTie.Mute);
        fakes.Host.Drain();

        Assert.Equal("Unmuted @ben@hachyderm.io.", opened.Notice);
    }

    /// <summary>The tie keys belong to the account screen, and only it says so on its status row.</summary>
    [Fact]
    public async Task Tie_DoesNothingOffTheAccountScreen()
    {
        var shell = new AShell();
        var opened = await shell.Opened();

        await opened.Tie(AccountTie.Block);

        shell.Host.Drain();

        Assert.Empty(shell.Accounts.Ties);
    }

    /// <summary>
    ///     What the key offers depends on what is already in place, because that is what pressing it will do —
    ///     a row that said "follow" over an account you follow would be offering the wrong thing.
    /// </summary>
    [Fact]
    public async Task Keys_SayWhichWayRoundEachTieKeyMeans()
    {
        var (fakes, opened) = await OnTheAccountScreen(AnAccount.Standing(following: true, muting: true));

        Assert.Contains(opened.Keys, key => key is { Key: "F", Does: "unfollow" });
        Assert.Contains(opened.Keys, key => key is { Key: "M", Does: "unmute" });
        Assert.Contains(opened.Keys, key => key is { Key: "B", Does: "block" });
    }

    /// <summary>
    ///     A shell drilled into the account of whoever wrote the post on the timeline, standing where
    ///     <paramref name="standing" /> says — and answering a tie with <paramref name="becoming" /> where a test is
    ///     about what the instance said had changed.
    /// </summary>
    private static async Task<(AShell Fakes, Shell Opened)> OnTheAccountScreen(
        AccountStanding standing,
        Account? becoming = null)
    {
        var accounts = FakeAccountRelationships.Holding(
            AnAccount.With(address: "ben@hachyderm.io", standing: standing));

        accounts.Becoming = becoming;

        var fakes = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "110", account: "ben@hachyderm.io")),
            Accounts = accounts,
        };

        var opened = await fakes.Opened();

        await opened.OpenAuthor();

        fakes.Host.Drain();

        return (fakes, opened);
    }
}
