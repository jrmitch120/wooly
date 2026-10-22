using Wooly.Core.Accounts;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     The "edit public profile" form (#406, ADR-0028) where it meets the shell: <b>You</b> offering <c>E</c>, the status
///     row's keys over the form, what a save makes stale and reads again, and the question asked before a touched form
///     is thrown away. Everything the form does itself is drafting's, and tested there
///     (<see cref="DraftingPublicProfileFormTests" />, #422).
/// </summary>
public class PublicProfileFormTests
{
    /// <summary>The account the profile every test acts as signs in as, which is who <b>You</b> reads.</summary>
    private static readonly Account Me = AnAccount.With(address: "jeff@mastodon.social", author: "Jeff", id: "7");

    /// <summary>A shell whose instance answers about the profile's own account, standing on <b>You</b>.</summary>
    private static async Task<Shell> OnYou(AShell built)
    {
        var shell = await built.Opened();

        shell.Rail.GoTo(DestinationKind.Profile);
        built.Host.Settle();

        return shell;
    }

    private static AShell Built(PublicProfile? read = null) => new()
    {
        Accounts = FakeAccountRelationships.Holding(Me),
        PublicProfile = FakePublicProfileEditor.Holding(read ?? APublicProfile.With(account: Me)),
    };

    /// <summary>The form, opened with <c>E</c> on <b>You</b> and read.</summary>
    private static async Task<(Shell Shell, PublicProfileScreen Form)> Editing(AShell built)
    {
        var shell = await OnYou(built);

        shell.Press(ShellKey.CapitalE);
        built.Host.Drain();

        return (shell, Assert.IsType<PublicProfileScreen>(shell.Screen));
    }

    /// <summary>Walks the form down to <paramref name="part" />, as the arrows do.</summary>
    private static void WalkTo(Shell shell, PublicProfileScreen form, PublicProfilePart part)
    {
        while (form.Typing.Part != part)
        {
            Assert.True(shell.Press(ShellKey.Down));
        }
    }

    /// <summary>
    ///     The status row offers the walk only the ways it goes, as compose's does: <c>↓</c> at the top, <c>↑</c> at
    ///     the bottom, both between.
    /// </summary>
    [Fact]
    public async Task TheStatusRow_OffersTheWalkOnlyTheWaysItGoes()
    {
        var (shell, form) = await Editing(Built());

        Assert.Contains(shell.Keys, key => key is { Key: "↓", Does: "field" });

        shell.Press(ShellKey.Down);

        Assert.Contains(shell.Keys, key => key is { Key: "↑↓", Does: "field" });

        while (form.Typing.Part != PublicProfilePart.Bio)
        {
            shell.Press(ShellKey.Down);
        }

        Assert.Contains(shell.Keys, key => key is { Key: "↑", Does: "field" });
    }

    /// <summary>
    ///     A save takes the form off and lands back on <b>You</b>, read again so that it shows the public profile as the
    ///     instance now has it with no refresh asked for, and the status row says it was saved.
    /// </summary>
    [Fact]
    public async Task ASave_LandsBackOnYouReadAgain()
    {
        var renamed = Me with { Author = "Jeff M" };
        var built = Built();
        built.PublicProfile = FakePublicProfileEditor.Holding(APublicProfile.With(account: Me), saved: renamed);
        var (shell, _) = await Editing(built);
        var readsBefore = built.Accounts.Reads.Count;

        shell.Drafting.Edit<PublicProfileScreen>(editing => editing.RewriteDisplayName("Jeff M"));
        built.Accounts.NowShowing(renamed);

        shell.Press(ShellKey.CtrlS);
        built.Host.Settle();

        var you = Assert.IsType<AccountScreen>(shell.Screen);

        Assert.Equal(1, shell.Depth);
        Assert.Equal(readsBefore + 1, built.Accounts.Reads.Count);
        Assert.Equal("Jeff M", you.Account.Author);
        Assert.Contains(AShell.Drawn(you), row => row.Contains("Jeff M", StringComparison.Ordinal));
        Assert.Equal("Public profile saved.", shell.Notice);
    }

    /// <summary>
    ///     A follow list held a moment ago with the own account on it is read again after a save rather than handed back
    ///     showing the public profile as it was — the own account reached from it, as from anywhere, offering <c>E</c>.
    /// </summary>
    [Fact]
    public async Task ASave_ForgetsACachedFollowListTheOwnAccountIsOn()
    {
        var ben = AnAccount.With(id: "42", address: "ben@hachyderm.io");
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "220", account: "ben@hachyderm.io")),
            Accounts = FakeAccountRelationships.Holding(ben, Me),
            PublicProfile = FakePublicProfileEditor.Holding(APublicProfile.With(account: Me)),
        };
        var shell = await built.Opened();

        await shell.OpenAuthor();
        built.Host.Drain();
        shell.Press(ShellKey.W);
        built.Host.Drain();

        built.Accounts.NowShowing(Me);
        shell.Press(ShellKey.Enter);
        built.Host.Drain();

        shell.Press(ShellKey.CapitalE);
        built.Host.Drain();
        shell.Drafting.Edit<PublicProfileScreen>(editing => editing.RewriteDisplayName("Jeff M"));
        shell.Press(ShellKey.CtrlS);
        built.Host.Drain();

        shell.Back();
        shell.Back();
        built.Accounts.NowShowing(ben);

        var asked = built.Accounts.Lists.Count(list => list.Side is not null);

        shell.Press(ShellKey.W);
        built.Host.Drain();

        Assert.Equal(asked + 1, built.Accounts.Lists.Count(list => list.Side is not null));
    }

    /// <summary>
    ///     A timeline held a moment ago with one of the own account's posts on it is read again after a save, rather
    ///     than handed back with the post's byline saying the public profile as it was (review of #403).
    /// </summary>
    [Fact]
    public async Task ASave_ForgetsACachedTimelineCarryingTheOwnAccountsPost()
    {
        var built = Built();
        built.Timelines = FakeTimelineReader.Holding(APost.With(id: "220", account: Me.Address, author: "Jeff"));
        var (shell, _) = await Editing(built);

        shell.Drafting.Edit<PublicProfileScreen>(editing => editing.RewriteDisplayName("Jeff M"));
        shell.Press(ShellKey.CtrlS);
        built.Host.Drain();

        var asked = built.Timelines.Reads.Count(read => read.Timeline == Wooly.Core.Timelines.Timeline.Home);

        shell.Rail.GoTo(DestinationKind.Home);
        built.Host.Settle();

        Assert.Equal(asked + 1, built.Timelines.Reads.Count(read => read.Timeline == Wooly.Core.Timelines.Timeline.Home));
    }

    /// <summary>
    ///     The own account somewhere further down the stack — <b>You</b>, under a follow list it was reached again from
    ///     — shows the public profile as saved when it is come back to, whichever copy of it the form was opened from
    ///     (review of #403).
    /// </summary>
    [Fact]
    public async Task ASave_ReachesTheOwnAccountFurtherDownTheStack()
    {
        var renamed = Me with { Author = "Jeff M" };
        var built = new AShell
        {
            Accounts = FakeAccountRelationships.Holding(Me, Me),
            PublicProfile = FakePublicProfileEditor.Holding(APublicProfile.With(account: Me), saved: renamed),
        };
        var shell = await OnYou(built);

        shell.Press(ShellKey.W);
        built.Host.Drain();
        shell.Press(ShellKey.Enter);
        built.Host.Drain();
        Assert.Equal(3, shell.Depth);

        shell.Press(ShellKey.CapitalE);
        built.Host.Drain();
        shell.Drafting.Edit<PublicProfileScreen>(editing => editing.RewriteDisplayName("Jeff M"));
        built.Accounts.NowShowing(renamed);
        shell.Press(ShellKey.CtrlS);
        built.Host.Settle();

        shell.Back();
        shell.Back();

        var you = Assert.IsType<AccountScreen>(shell.Screen);

        Assert.Equal(1, shell.Depth);
        Assert.Equal("Jeff M", you.Account.Author);
    }

    /// <summary>An untouched form leaves on <c>esc</c> without asking — a toggle flipped and flipped back included.</summary>
    [Fact]
    public async Task Esc_OnAnUntouchedForm_LeavesWithoutAsking()
    {
        var (shell, form) = await Editing(Built());

        WalkTo(shell, form, PublicProfilePart.Bot);
        shell.Press(ShellKey.Space);
        shell.Press(ShellKey.Space);

        shell.Press(ShellKey.Escape);

        Assert.Null(shell.Asking);
        Assert.IsType<AccountScreen>(shell.Screen);
    }

    /// <summary>
    ///     A touched form asks <c>Discard these changes? y / n</c> before any way out — <c>esc</c>, or <c>tab</c> along
    ///     the rail — and declining keeps the form with every edit, where <c>y</c> goes.
    /// </summary>
    [Theory]
    [InlineData(ShellKey.Escape)]
    [InlineData(ShellKey.Tab)]
    [InlineData(ShellKey.CtrlP)]
    public async Task ATouchedForm_AsksBeforeEveryWayOut(ShellKey way)
    {
        var built = Built();
        var (shell, form) = await Editing(built);

        shell.Drafting.Edit<PublicProfileScreen>(editing => editing.RewriteDisplayName("Jeff M"));

        shell.Press(way);

        Assert.Equal("Discard these changes?", shell.Asking?.Ask);
        Assert.True(shell.Asking?.YesOrNo);

        // n is no key of the shell's, and is a no as anything but y is.
        await shell.Answer(pressed: null);

        Assert.Same(form, shell.Screen);
        Assert.Equal("Jeff M", form.DisplayName);

        shell.Press(way);
        await shell.Answer(ShellKey.Y);
        built.Host.Settle();

        Assert.DoesNotContain(form.Crumb, shell.Crumbs);
    }

    /// <summary>
    ///     <b>You</b> has no ties to offer: an account cannot follow, mute or block itself, so <c>F</c>, <c>M</c> and
    ///     <c>B</c> are neither on the status row nor anybody's there, and the header block says nothing of where it
    ///     stands with itself.
    /// </summary>
    [Fact]
    public async Task You_OffersNoTiesAndSaysNoStanding()
    {
        var built = Built();
        built.Accounts.InCommon = [AnAccount.With(address: "ben@hachyderm.io")];
        var shell = await OnYou(built);

        Assert.DoesNotContain(shell.Keys, key => key.Key is "F" or "M" or "B");
        Assert.DoesNotContain(AShell.Drawn(shell.Screen), row => row.Contains("No ties", StringComparison.Ordinal)
            || row.Contains("Standing not asked", StringComparison.Ordinal)
            || row.Contains("Followed by", StringComparison.Ordinal));

        shell.Press(ShellKey.CapitalF);
        shell.Press(ShellKey.CapitalM);
        shell.Press(ShellKey.CapitalB);
        built.Host.Drain();

        Assert.Empty(built.Accounts.Ties);
    }

    /// <summary>
    ///     <b>You</b> draws no row for editing the public profile: <c>E</c> on the status row is the way to the form, and
    ///     the walk goes from the header block straight to the first post, as it does on anybody's account.
    /// </summary>
    [Fact]
    public async Task You_OffersEOnTheStatusRowAndNoRowForIt()
    {
        var built = Built();
        var shell = await OnYou(built);
        var you = Assert.IsType<AccountScreen>(shell.Screen);

        Assert.DoesNotContain(AShell.Drawn(you), row => row.Contains("Edit public profile", StringComparison.Ordinal));
        Assert.Contains(shell.Keys, key => key is { Key: "E", Does: "edit public profile" });

        shell.Move(1);

        Assert.NotNull(you.Picked);
    }

    /// <summary><b>You</b> offers <c>E</c> on its status row, worded as ADR-0028 words the form.</summary>
    [Fact]
    public async Task You_OffersCapitalE()
    {
        var shell = await OnYou(Built());

        Assert.Contains(shell.Keys, key => key is { Key: "E", Does: "edit public profile" });
    }

    /// <summary>
    ///     Anybody else's account neither offers <c>E</c> nor answers it, as <c>p</c>, <c>e</c> and <c>d</c> are off
    ///     somebody else's post: their public profile cannot be changed from here.
    /// </summary>
    [Fact]
    public async Task SomebodyElsesAccount_NeitherOffersNorAnswersCapitalE()
    {
        var built = new AShell { Accounts = FakeAccountRelationships.Holding(AnAccount.With(address: "ben@hachyderm.io")) };
        var shell = await OnYou(built);

        Assert.DoesNotContain(shell.Keys, key => key.Key == "E");

        shell.Press(ShellKey.CapitalE);
        built.Host.Drain();

        Assert.Empty(built.PublicProfile.Reads);
        Assert.IsType<AccountScreen>(shell.Screen);
    }
}
