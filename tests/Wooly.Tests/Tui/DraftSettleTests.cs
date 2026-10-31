using Wooly.Core.Accounts;
using Wooly.Core.Timelines;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     Opening a draft while the rail's settle is pending calls the settle off (#429, ADR-0014): the cursor goes back to
///     the destination selected, nothing is fetched for the one tabbed to, and the draft stays in front. A settle that
///     has already landed is nothing to call off, and tabbing from a draft or after one settles as ever.
/// </summary>
public class DraftSettleTests : IDisposable
{
    /// <summary>The account the profile every test acts as signs in as, which is who <b>You</b> reads.</summary>
    private static readonly Account Me = AnAccount.With(address: "jeff@mastodon.social", author: "Jeff", id: "7");

    private readonly TemporaryDirectory _files = new();

    public void Dispose() => _files.Dispose();

    /// <summary>
    ///     <c>tab</c> on a feed, <c>c</c> before the settle lands and a file attached: once the settle's time has passed
    ///     the compose is still in front with its attachment, the selection is where it was, the cursor back on it, and
    ///     the timeline tabbed to was never read.
    /// </summary>
    [Fact]
    public async Task ComposingBeforeTheSettleLands_CallsTheSettleOff()
    {
        var built = new AShell { LaunchedFrom = _files.Path };
        var shell = await built.Opened();

        shell.Press(ShellKey.Tab);
        shell.Press(ShellKey.C);

        var compose = Assert.IsType<ComposeScreen>(shell.Screen);

        Assert.True(shell.Paste(_files.WriteFile("one.png")));
        built.Host.Settle();

        Assert.Same(compose, shell.Screen);
        Assert.True(compose.Touched);
        Assert.Equal(DestinationKind.Home, shell.Rail.Showing.Kind);
        Assert.Equal(shell.Rail.Current, shell.Rail.Cursor);
        Assert.DoesNotContain(built.Timelines.Reads, read => read.Timeline == Timeline.Local);
    }

    /// <summary>The same for the public profile form, opened with <c>E</c> on <b>You</b> before the settle lands.</summary>
    [Fact]
    public async Task EditingThePublicProfileBeforeTheSettleLands_CallsTheSettleOff()
    {
        var built = new AShell
        {
            Accounts = FakeAccountRelationships.Holding(Me),
            PublicProfile = FakePublicProfileEditor.Holding(APublicProfile.With(account: Me)),
        };
        var shell = await built.Opened();

        shell.Rail.GoTo(shell.Rail.Of(DestinationKind.Profile));
        built.Host.Settle();

        shell.Press(ShellKey.Tab);
        shell.Press(ShellKey.Tab);
        shell.Press(ShellKey.CapitalE);
        built.Host.Drain();

        var form = Assert.IsType<PublicProfileScreen>(shell.Screen);

        Assert.Equal(shell.Rail.Current, shell.Rail.Cursor);

        built.Host.Settle();

        Assert.Same(form, shell.Screen);
        Assert.Equal(DestinationKind.Profile, shell.Rail.Showing.Kind);
        Assert.Equal(shell.Rail.Current, shell.Rail.Cursor);
        Assert.DoesNotContain(built.Timelines.Reads, read => read.Timeline == Timeline.Local);
    }

    /// <summary>
    ///     Tabbing from a draft opened mid-settle steps from the destination selected — the one place the cursor was put
    ///     back to — and arrives there at once, as tabbing off any draft does (#373).
    /// </summary>
    [Fact]
    public async Task TabbingFromADraftOpenedMidSettle_StepsFromTheSelection()
    {
        var built = new AShell();
        var shell = await built.Opened();

        shell.Press(ShellKey.Tab);
        shell.Press(ShellKey.C);
        shell.Press(ShellKey.Tab);

        Assert.IsNotType<ComposeScreen>(shell.Screen);
        Assert.Equal(DestinationKind.Local, shell.Rail.Showing.Kind);
        Assert.Equal(shell.Rail.Current, shell.Rail.Cursor);
    }

    /// <summary>A settle already landed is nothing to call off: the compose opens over the destination it landed on.</summary>
    [Fact]
    public async Task ComposingAfterTheSettleLanded_LeavesTheRailWhereItLanded()
    {
        var built = new AShell();
        var shell = await built.Opened();

        shell.Press(ShellKey.Tab);
        built.Host.Settle();
        shell.Press(ShellKey.C);

        Assert.IsType<ComposeScreen>(shell.Screen);
        Assert.Equal(DestinationKind.Local, shell.Rail.Showing.Kind);
        Assert.Equal(shell.Rail.Current, shell.Rail.Cursor);
    }
}
