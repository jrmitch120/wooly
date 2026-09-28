using Wooly.Core.Errors;
using Wooly.Core.Paging;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     <c>R</c> on the profiles screen: the picked profile signed in again, its instance and name fixed and its token
///     replaced without asking — and a token refused mid-session, said on the status row with the key that fixes it
///     (ADR-0020, #248).
/// </summary>
public class ShellReauthorizeTests
{
    /// <summary>What the status row says wherever an instance refuses the token a question was put with.</summary>
    private const string TokenRefused = "This profile's token was refused — ctrl-p to sign in again.";

    /// <summary>Acting as personal, which is the default, with work set up beside it on another instance.</summary>
    private static AShell PersonalAndWork(string workAccount = "jeff@hachyderm.io") => new()
    {
        Profiles = FakeProfileRegistry.Holding(
            "personal",
            FakeProfileRegistry.Profile("personal", "mastodon.social", "jeff@mastodon.social"),
            FakeProfileRegistry.Profile("work", "hachyderm.io", workAccount)),
    };

    /// <summary>The profiles screen, with <paramref name="name" /> picked.</summary>
    private static void OnProfiles(Shell shell, string name)
    {
        shell.Press(ShellKey.CtrlP);

        Assert.IsType<ProfilesScreen>(shell.Screen).Pick(name);
    }

    /// <summary>Offered on every row, the one acted as and the default included: every token can be replaced.</summary>
    [Theory]
    [InlineData("personal")]
    [InlineData("work")]
    public async Task R_IsOfferedOnEveryRow(string name)
    {
        var opened = await PersonalAndWork().Opened();

        OnProfiles(opened, name);

        Assert.Contains(opened.Keys, key => key is { Key: "R", Does: "sign in again" });
    }

    /// <summary>The add screen, with the picked profile's instance and name fixed rather than typed.</summary>
    [Fact]
    public async Task R_OpensSigningInAgain_WithTheInstanceAndNameFixed()
    {
        var shell = PersonalAndWork();
        var opened = await shell.Opened();

        OnProfiles(opened, "work");

        Assert.True(opened.Press(ShellKey.CapitalR));

        var adding = Assert.IsType<AddProfileScreen>(opened.Screen);
        Assert.True(adding.SignsInAgain);
        Assert.False(adding.Alone);
        Assert.Equal("hachyderm.io", adding.Instance);
        Assert.Equal("work", adding.Name);
        Assert.False(adding.IsTyping);
        Assert.Equal("Home › Profiles › Sign in again", opened.Breadcrumb);
        Assert.Empty(shell.Authorizer.Instances);
    }

    /// <summary>
    ///     Signed in, the token is replaced with nothing asked — and the profile's name, instance, account and whether
    ///     it is the default are as they were. The reader is back on the list with the row still picked.
    /// </summary>
    [Fact]
    public async Task R_ReplacesTheToken_AndLeavesEverythingElseAlone()
    {
        var shell = PersonalAndWork();
        var opened = await shell.Opened();

        OnProfiles(opened, "work");
        opened.Press(ShellKey.CapitalR);
        opened.Press(ShellKey.Enter);
        shell.Host.Drain();

        Assert.Null(opened.Asking);

        var (name, profile, token) = Assert.Single(shell.Profiles.Added);
        Assert.Equal("work", name);
        Assert.Equal("hachyderm.io", profile.Instance);
        Assert.Equal("jeff@hachyderm.io", profile.Account);
        Assert.Equal("token-from-browser", token);

        var list = shell.Profiles.List();
        Assert.False(list.Single(held => held.Name == "work").IsCurrent);
        Assert.True(list.Single(held => held.Name == "personal").IsCurrent);

        var profiles = Assert.IsType<ProfilesScreen>(opened.Screen);
        Assert.Equal("work", profiles.PickedProfile?.Name);
        Assert.Equal(2, opened.Depth);
        Assert.Equal("Replaced profile work.", opened.Notice);
    }

    /// <summary>A working token for somebody else is refused on the screen, and nothing is written.</summary>
    [Fact]
    public async Task R_WithATokenForSomebodyElse_IsRefused_AndNothingIsWritten()
    {
        var shell = PersonalAndWork(workAccount: "somebody@hachyderm.io");
        var opened = await shell.Opened();

        OnProfiles(opened, "work");
        opened.Press(ShellKey.CapitalR);
        opened.Press(ShellKey.Enter);
        shell.Host.Drain();

        Assert.Empty(shell.Profiles.Added);
        Assert.IsType<AddProfileScreen>(opened.Screen);

        var said = string.Join(" ", opened.Screen.Lines(new Drawing(61, AShell.Now)).Select(line => line.Text));
        Assert.Contains("@jeff@hachyderm.io, not @somebody@hachyderm.io", said);
    }

    /// <summary>
    ///     Signing in again as the profile acted as is still the same person, so the session carries on: the stack is
    ///     as it was, nothing is read again for it, and the next question goes out with the new token.
    /// </summary>
    [Fact]
    public async Task R_OnTheProfileActedAs_KeepsTheStack_AndTheNextRequestUsesTheNewToken()
    {
        var shell = PersonalAndWork();
        shell.Engagement = FakePostEngagement.Answered(APost.With(id: "110"));
        var opened = await shell.Opened();

        await opened.Enter();
        shell.Host.Drain();

        var thread = opened.Screen;
        var requests = shell.Requests;

        OnProfiles(opened, "personal");
        opened.Press(ShellKey.CapitalR);
        opened.Press(ShellKey.Enter);
        shell.Host.Drain();

        Assert.Equal("token-from-browser", Assert.Single(shell.Profiles.Added).AccessToken);
        Assert.Equal(requests, shell.Requests);

        opened.Press(ShellKey.Escape);

        Assert.Same(thread, opened.Screen);
        Assert.Equal(2, opened.Depth);

        shell.Engagement.Tokens.Clear();
        await opened.Mark(PostMark.Favorite);
        shell.Host.Drain();

        Assert.Equal("token-from-browser", Assert.Single(shell.Engagement.Tokens));
    }

    /// <summary>
    ///     A token refused once something has been read is said on the status row, naming the key that fixes it — and
    ///     nothing opens by itself, since whether to sign in again now is the reader's.
    /// </summary>
    [Fact]
    public async Task ATokenRefusedMidSession_IsSaid_NamingCtrlP_AndNothingOpens()
    {
        var reads = 0;
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Awaiting(_ => reads++ == 0
                ? Task.FromResult(Fetch<Post>.Complete([APost.With(id: "110")]))
                : Task.FromException<Fetch<Post>>(new AuthenticationException("mastodon.social refused the token."))),
        };
        var opened = await shell.Opened();

        await opened.Refresh();
        shell.Host.Drain();

        Assert.IsType<FeedScreen>(opened.Screen);
        Assert.Equal(1, opened.Depth);
        Assert.Equal(TokenRefused, opened.Notice);
        Assert.True(opened.NoticeIsError);
    }

    /// <summary>The same from a verb as from a read: any question refused for its token says so the same way.</summary>
    [Fact]
    public async Task ATokenRefusedOnAMark_IsSaidTheSameWay()
    {
        var shell = new AShell
        {
            Engagement = FakePostEngagement.Refusing(new AuthenticationException("mastodon.social refused the token.")),
        };
        var opened = await shell.Opened();

        await opened.Mark(PostMark.Favorite);
        shell.Host.Drain();

        Assert.IsType<FeedScreen>(opened.Screen);
        Assert.Equal(TokenRefused, opened.Notice);
    }
}
