using Wooly.Core.Errors;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     Compose starts on the account's own defaults (ADR-0024, #339): where the config sets no
///     <c>default_visibility</c>, To starts on what the account's settings on its instance say — read once per profile
///     in a session, and held, failure included.
/// </summary>
public class ComposeAccountDefaultsTests
{
    /// <summary>With no preference, a fresh post starts on the account's default and sends it, unchosen.</summary>
    [Fact]
    public async Task AFreshPostStartsOnTheAccountsDefaultAndSendsIt()
    {
        var built = Over(FakeAccountDefaults.Setting(new PostDefaults(PostVisibility.Followers, "fr")));
        var compose = ComposeRows.Open(await built.Opened(), ComposeFor.Post);

        compose.Text = "hello";

        Assert.Equal("     To  ○ public  ○ unlisted  ● followers  ○ direct", Texts(compose)[2]);

        var draft = Publishing(compose);

        Assert.Equal(PostVisibility.Followers, draft.Visibility);
        Assert.False(draft.VisibilityChosen);
    }

    /// <summary>The config's <c>default_visibility</c> wins over the account's own.</summary>
    [Fact]
    public async Task TheConfigsPreferenceWinsOverTheAccountsDefault()
    {
        var built = Over(FakeAccountDefaults.Setting(new PostDefaults(PostVisibility.Followers, null)));
        built.DefaultVisibility = PostVisibility.Unlisted;

        var compose = ComposeRows.Open(await built.Opened(), ComposeFor.Post);

        Assert.Equal(PostVisibility.Unlisted, compose.Visibility);
    }

    /// <summary>A reply still narrows the account's default to the post it answers.</summary>
    [Fact]
    public async Task AReplyNarrowsTheAccountsDefaultToThePostItAnswers()
    {
        var built = Over(FakeAccountDefaults.Setting(new PostDefaults(PostVisibility.Public, null)));
        built.Timelines = FakeTimelineReader.Holding(
            APost.With(id: "220", account: "ben@hachyderm.io", visibility: PostVisibility.Followers));

        var compose = ComposeRows.Open(await built.Opened(), ComposeFor.Reply);

        compose.Text += "hello";

        Assert.Equal(PostVisibility.Followers, Publishing(compose).Visibility);
        Assert.False(Publishing(compose).VisibilityChosen);
    }

    /// <summary>A reply narrower than the post it answers stays as narrow as the account's default.</summary>
    [Fact]
    public async Task AReplyKeepsAnAccountDefaultNarrowerThanThePost()
    {
        var built = Over(FakeAccountDefaults.Setting(new PostDefaults(PostVisibility.Unlisted, null)));

        var compose = ComposeRows.Open(await built.Opened(), ComposeFor.Reply);

        Assert.Equal(PostVisibility.Unlisted, compose.Visibility);
    }

    /// <summary>Compose opened twice asks the account's defaults once, and never as compose opens.</summary>
    [Fact]
    public async Task ComposeOpenedTwiceReadsTheDefaultsOnce()
    {
        var built = Over(FakeAccountDefaults.Setting(new PostDefaults(PostVisibility.Followers, null)));
        var shell = await built.Opened();

        ComposeRows.Open(shell, ComposeFor.Post);
        shell.Press(ShellKey.Escape);
        built.Host.Drain();
        var again = ComposeRows.Open(shell, ComposeFor.Post);

        Assert.Equal(PostVisibility.Followers, again.Visibility);
        Assert.Equal(["personal"], built.Defaults.Reads);
    }

    /// <summary>After a switch of profile, compose starts on the other account's defaults.</summary>
    [Fact]
    public async Task AfterASwitchComposeStartsOnTheOtherAccountsDefaults()
    {
        var built = Over(FakeAccountDefaults.Setting(new Dictionary<string, PostDefaults>
        {
            ["personal"] = new(PostVisibility.Public, "en"),
            ["work"] = new(PostVisibility.Unlisted, "de"),
        }));
        built.Profiles = FakeProfileRegistry.Holding(
            "personal",
            FakeProfileRegistry.Profile("personal", "mastodon.social", "jeff@mastodon.social"),
            FakeProfileRegistry.Profile("work", "hachyderm.io", "jeff@hachyderm.io"));
        var shell = await built.Opened();

        shell.Press(ShellKey.CtrlP);
        Assert.IsType<ProfilesScreen>(shell.Screen).Pick("work");
        Assert.True(shell.Press(ShellKey.Enter));
        built.Host.Drain();

        var compose = ComposeRows.Open(shell, ComposeFor.Post);

        Assert.Equal(PostVisibility.Unlisted, compose.Visibility);
        Assert.Equal("de", compose.Lang.Language?.Code);
        Assert.Equal(["token-personal", "token-work"], built.Defaults.Tokens);
    }

    /// <summary>
    ///     A read that failed leaves To on "account default", sending nothing, and compose still opens — and it is held
    ///     as unknown rather than asked again at every compose.
    /// </summary>
    [Fact]
    public async Task AFailedReadLeavesAccountDefaultAndIsNotAskedAgain()
    {
        var built = Over(FakeAccountDefaults.Refusing(new AuthenticationException("No.")));
        var shell = await built.Opened();

        var compose = ComposeRows.Open(shell, ComposeFor.Post);
        compose.Text = "hello";

        Assert.Equal(ComposeRows.ToAccountDefault, Texts(compose)[2]);
        Assert.Null(Publishing(compose).Visibility);

        compose.Text = string.Empty;
        shell.Press(ShellKey.Escape);
        built.Host.Drain();
        ComposeRows.Open(shell, ComposeFor.Post);

        Assert.Single(built.Defaults.Reads);
    }

    /// <summary>An account whose defaults are unknown leaves To where it always was.</summary>
    [Fact]
    public async Task AnUnknownDefaultLeavesAccountDefault()
    {
        var compose = ComposeRows.Open(await Over(FakeAccountDefaults.Setting()).Opened(), ComposeFor.Post);

        Assert.Null(compose.Visibility);
    }

    private static AShell Over(FakeAccountDefaults defaults) => new()
    {
        Timelines = FakeTimelineReader.Holding(APost.With(id: "220", account: "ben@hachyderm.io")),
        Defaults = defaults,
    };

    private static PostDraft Publishing(ComposeScreen compose) =>
        Assert.IsType<Outgoing.Publishing>(compose.Outgoing).Draft;

    private static IReadOnlyList<string> Texts(ComposeScreen compose) =>
        [.. compose.Lines(new Drawing(60, AShell.Now, Height: 17)).Select(line => line.Text)];
}
