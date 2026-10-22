using Wooly.Core.Errors;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     Compose starts on the account's own defaults (ADR-0024, #339): where the config sets no
///     <c>default_visibility</c>, To starts on what the account's settings on its instance say — read once per profile
///     in a session, and held, failure included. Asked of drafting, which opens every compose (ADR-0029).
/// </summary>
public class ComposeAccountDefaultsTests
{
    /// <summary>With no preference, a fresh post starts on the account's default and sends it, unchosen.</summary>
    [Fact]
    public void AFreshPostStartsOnTheAccountsDefaultAndSendsIt()
    {
        var drafting = Over(FakeAccountDefaults.Setting(new PostDefaults(PostVisibility.Followers, "fr"))).Build();

        drafting.Press(ShellKey.C);
        var compose = drafting.Composing;
        compose.Text = "hello";

        Assert.Equal("     To  ○ public  ○ unlisted  ● followers  ○ direct", Texts(compose)[2]);

        var draft = Publishing(compose);

        Assert.Equal(PostVisibility.Followers, draft.Visibility);
        Assert.False(draft.VisibilityChosen);
    }

    /// <summary>The config's <c>default_visibility</c> wins over the account's own.</summary>
    [Fact]
    public void TheConfigsPreferenceWinsOverTheAccountsDefault()
    {
        var built = Over(FakeAccountDefaults.Setting(new PostDefaults(PostVisibility.Followers, null)));
        built.Fakes.DefaultVisibility = PostVisibility.Unlisted;
        var drafting = built.Build();

        drafting.Press(ShellKey.C);

        Assert.Equal(PostVisibility.Unlisted, drafting.Composing.Visibility);
    }

    /// <summary>A reply still narrows the account's default to the post it answers.</summary>
    [Fact]
    public void AReplyNarrowsTheAccountsDefaultToThePostItAnswers()
    {
        var built = Over(FakeAccountDefaults.Setting(new PostDefaults(PostVisibility.Public, null)));
        built.Bottom = ADrafting.Home(
            APost.With(id: "220", account: "ben@hachyderm.io", visibility: PostVisibility.Followers));
        var drafting = built.Build();

        drafting.Press(ShellKey.R);
        var compose = drafting.Composing;
        compose.Text += "hello";

        Assert.Equal(PostVisibility.Followers, Publishing(compose).Visibility);
        Assert.False(Publishing(compose).VisibilityChosen);
    }

    /// <summary>A reply narrower than the post it answers stays as narrow as the account's default.</summary>
    [Fact]
    public void AReplyKeepsAnAccountDefaultNarrowerThanThePost()
    {
        var drafting = Over(FakeAccountDefaults.Setting(new PostDefaults(PostVisibility.Unlisted, null))).Build();

        drafting.Press(ShellKey.R);

        Assert.Equal(PostVisibility.Unlisted, drafting.Composing.Visibility);
    }

    /// <summary>Compose opened twice asks the account's defaults once, and never as compose opens.</summary>
    [Fact]
    public void ComposeOpenedTwiceReadsTheDefaultsOnce()
    {
        var drafting = Over(FakeAccountDefaults.Setting(new PostDefaults(PostVisibility.Followers, null))).Build();

        drafting.Press(ShellKey.C);
        drafting.Stack.TakeOff(drafting.Composing);
        drafting.Fakes.Host.Drain();
        drafting.Press(ShellKey.C);

        Assert.Equal(PostVisibility.Followers, drafting.Composing.Visibility);
        Assert.Equal(["personal"], drafting.Fakes.Defaults.Reads);
    }

    /// <summary>After a switch of profile, compose starts on the other account's defaults.</summary>
    [Fact]
    public void AfterASwitchComposeStartsOnTheOtherAccountsDefaults()
    {
        var drafting = Over(FakeAccountDefaults.Setting(new Dictionary<string, PostDefaults>
        {
            ["personal"] = new(PostVisibility.Public, "en"),
            ["work"] = new(PostVisibility.Unlisted, "de"),
        })).Build();

        drafting.ActAs(new ActiveProfile
        {
            Name = "work",
            Instance = "hachyderm.io",
            Account = "jeff@hachyderm.io",
            AccessToken = "token-work",
        });
        drafting.Fakes.Host.Drain();

        drafting.Press(ShellKey.C);

        Assert.Equal(PostVisibility.Unlisted, drafting.Composing.Visibility);
        Assert.Equal("de", drafting.Composing.Lang.Language?.Code);
        Assert.Equal(["token-personal", "token-work"], drafting.Fakes.Defaults.Tokens);
    }

    /// <summary>
    ///     A read that failed leaves To on "account default", sending nothing, and compose still opens — and it is held
    ///     as unknown rather than asked again at every compose.
    /// </summary>
    [Fact]
    public void AFailedReadLeavesAccountDefaultAndIsNotAskedAgain()
    {
        var drafting = Over(FakeAccountDefaults.Refusing(new AuthenticationException("No."))).Build();

        drafting.Press(ShellKey.C);
        var compose = drafting.Composing;
        compose.Text = "hello";

        Assert.Equal(ComposeRows.ToAccountDefault, Texts(compose)[2]);
        Assert.Null(Publishing(compose).Visibility);

        drafting.Stack.TakeOff(compose);
        drafting.Fakes.Host.Drain();
        drafting.Press(ShellKey.C);

        Assert.Single(drafting.Fakes.Defaults.Reads);
    }

    /// <summary>An account whose defaults are unknown leaves To where it always was.</summary>
    [Fact]
    public void AnUnknownDefaultLeavesAccountDefault()
    {
        var drafting = Over(FakeAccountDefaults.Setting()).Build();

        drafting.Press(ShellKey.C);

        Assert.Null(drafting.Composing.Visibility);
    }

    private static ADrafting Over(FakeAccountDefaults defaults) => new()
    {
        Fakes = { Defaults = defaults },
        Bottom = ADrafting.Home(APost.With(id: "220", account: "ben@hachyderm.io")),
    };

    private static PostDraft Publishing(ComposeScreen compose) =>
        Assert.IsType<Outgoing.Publishing>(compose.Outgoing).Draft;

    private static IReadOnlyList<string> Texts(ComposeScreen compose) =>
        [.. compose.Lines(new Drawing(60, AShell.Now, Height: 17)).Select(line => line.Text)];
}
