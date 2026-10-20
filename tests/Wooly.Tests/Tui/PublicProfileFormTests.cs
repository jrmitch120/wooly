using Wooly.Core.Accounts;
using Wooly.Core.Errors;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     The "edit public profile" form (#406, ADR-0028): opened from <b>You</b> with <c>E</c>, read as written under an
///     enquiry, and saved as changed. Through the shell over a fake public profile, so what is asserted is what was
///     drawn and what the port was asked to send.
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

    /// <summary><c>E</c> on <b>You</b> reads the public profile as written, then puts the form up over it.</summary>
    [Fact]
    public async Task CapitalE_OnYou_ReadsThePublicProfileAndOpensTheForm()
    {
        var built = Built();

        var (shell, _) = await Editing(built);

        Assert.Equal(["personal"], built.PublicProfile.Reads);
        Assert.Equal("Edit public profile", shell.Screen.Crumb);
        Assert.Equal(2, shell.Depth);
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

    /// <summary>A read that fails opens no form, and the status row says why in the instance's words.</summary>
    [Fact]
    public async Task AFailedRead_OpensNothingAndSaysWhy()
    {
        var built = Built();
        built.PublicProfile = FakePublicProfileEditor.Unreadable(PublicProfileRefusedException.Reading("Not today"));
        var shell = await OnYou(built);

        shell.Press(ShellKey.CapitalE);
        built.Host.Drain();

        Assert.IsType<AccountScreen>(shell.Screen);
        Assert.Equal("The instance would not show your public profile: Not today", shell.Notice);
        Assert.True(shell.NoticeIsError);
    }
}
