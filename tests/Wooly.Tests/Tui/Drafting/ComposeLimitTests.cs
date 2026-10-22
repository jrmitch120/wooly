using Wooly.Core.Errors;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The count measured against the instance's own limit rather than Mastodon's default (#319): read once per
///     instance a session, the first time a post is written there, and never said anything about when it cannot be.
///     Asked of drafting, which opens every compose and hears every limit (ADR-0029).
/// </summary>
public class ComposeLimitTests
{
    private static readonly PostLimits Roomy = new(1000, 30);

    /// <summary>The count says the instance's limit once the instance has said it.</summary>
    [Fact]
    public void TheCountIsOutOfTheInstancesOwnLimit()
    {
        var drafting = Over(FakeInstanceLimits.Setting(Roomy));

        drafting.Press(ShellKey.C);
        var compose = drafting.Composing;

        Assert.Equal("0 / 500", Counted(compose));

        drafting.Fakes.Host.Drain();

        Assert.Equal("0 / 1000", Counted(compose));
        Assert.Equal(1, drafting.Changes);
    }

    /// <summary>An address counts what the instance says one counts.</summary>
    [Fact]
    public void AnAddressCountsWhatTheInstanceSays()
    {
        var drafting = Over(FakeInstanceLimits.Setting(Roomy));

        drafting.Press(ShellKey.C);
        drafting.Fakes.Host.Drain();
        drafting.Composing.Rewrite("https://example.com");

        Assert.Equal("30 / 1000", Counted(drafting.Composing));
    }

    /// <summary>The colours move with the limit: the last tenth of it, and past it.</summary>
    [Theory]
    [InlineData(900, Role.Muted)]
    [InlineData(901, Role.QuotaLow)]
    [InlineData(1000, Role.QuotaLow)]
    [InlineData(1001, Role.Error)]
    public void TheColoursMoveWithTheLimit(int length, Role role)
    {
        var drafting = Over(FakeInstanceLimits.Setting(Roomy));

        drafting.Press(ShellKey.C);
        drafting.Fakes.Host.Drain();
        drafting.Composing.Rewrite(new string('a', length));

        Assert.Equal(role, Assert.Single(Count(drafting.Composing).Spans, span => span.Text.Trim().Length > 0).Role);
    }

    /// <summary>
    ///     Limits that arrive while the compose is covered by the file browser reach it all the same, so that coming
    ///     back finds it measured against them (#424).
    /// </summary>
    [Fact]
    public void LimitsArrivingUnderTheFileBrowserReachTheCompose()
    {
        var drafting = Over(FakeInstanceLimits.Setting(Roomy));

        drafting.Press(ShellKey.C);
        var compose = drafting.Composing;
        var browser = new FileBrowserScreen(
            new FolderListing(Path.GetTempPath(), "/tmp", Up: null, []),
            compose.Limits,
            compose.Media.Room);

        drafting.Stack.Push(browser);
        drafting.Fakes.Host.Drain();
        drafting.Stack.TakeOff(browser);

        Assert.Same(compose, drafting.Composing);
        Assert.Equal("0 / 1000", Counted(compose));
    }

    /// <summary>A reader who never writes a post costs no request for a limit.</summary>
    [Fact]
    public void NothingIsAskedUntilAPostIsWritten()
    {
        var drafting = Over(FakeInstanceLimits.Setting());

        Assert.Empty(drafting.Fakes.Limits.Reads);
    }

    /// <summary>Asked once a session, however many posts are written; every one after is measured at once.</summary>
    [Fact]
    public void TheLimitIsAskedOnceASession()
    {
        var drafting = Over(FakeInstanceLimits.Setting(Roomy));

        drafting.Press(ShellKey.C);
        drafting.Fakes.Host.Drain();
        drafting.Stack.TakeOff(drafting.Composing);
        drafting.Press(ShellKey.R);

        Assert.Equal("1000", Counted(drafting.Composing).Split(" / ")[1]);
        Assert.Equal(["mastodon.social"], drafting.Fakes.Limits.Reads);
    }

    /// <summary>
    ///     An instance that does not answer leaves Mastodon's default standing, says nothing about it rather than
    ///     counting a wait down over the post, and is asked again the next time one is written.
    /// </summary>
    [Fact]
    public void ALimitThatCannotBeReadIsSilentAndAskedAgain() =>
        AssertSilentAndAskedAgain(Over(FakeInstanceLimits.Unanswering()));

    /// <summary>And the same where the token is refused, which the reads that fill the screen already say.</summary>
    [Fact]
    public void ARefusedTokenIsSilentHereToo() =>
        AssertSilentAndAskedAgain(Over(FakeInstanceLimits.Refusing(new AuthenticationException("No."))));

    private static void AssertSilentAndAskedAgain(ADrafting drafting)
    {
        drafting.Press(ShellKey.C);
        drafting.Fakes.Host.Drain();

        Assert.Equal("0 / 500", Counted(drafting.Composing));
        Assert.Null(drafting.Notice);

        drafting.Stack.TakeOff(drafting.Composing);
        drafting.Press(ShellKey.C);
        drafting.Fakes.Host.Drain();

        Assert.Equal(2, drafting.Fakes.Limits.Reads.Count);
    }

    private static ADrafting Over(FakeInstanceLimits limits) => new ADrafting { Fakes = { Limits = limits } }.Build();

    private static Line Count(ComposeScreen compose) =>
        compose.Lines(new Drawing(60, AShell.Now, Height: 17))[^1];

    private static string Counted(ComposeScreen compose) => Count(compose).Text.Trim();
}
