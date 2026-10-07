using Wooly.Core.Errors;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The count measured against the instance's own limit rather than Mastodon's default (#319): read once per
///     instance a session, the first time a post is written there, and never said anything about when it cannot be.
/// </summary>
public class ComposeLimitTests
{
    private static readonly PostLimits Roomy = new(1000, 30);

    /// <summary>The count says the instance's limit once the instance has said it.</summary>
    [Fact]
    public async Task TheCountIsOutOfTheInstancesOwnLimit()
    {
        var built = new AShell { Limits = FakeInstanceLimits.Setting(Roomy) };
        var shell = await built.Opened();

        var compose = ComposeRows.Open(shell, ComposeFor.Post);

        Assert.Equal("0 / 500", Counted(compose));

        built.Host.Drain();

        Assert.Equal("0 / 1000", Counted(compose));
    }

    /// <summary>An address counts what the instance says one counts.</summary>
    [Fact]
    public async Task AnAddressCountsWhatTheInstanceSays()
    {
        var built = new AShell { Limits = FakeInstanceLimits.Setting(Roomy) };
        var shell = await built.Opened();

        var compose = ComposeRows.Open(shell, ComposeFor.Post);

        built.Host.Drain();
        shell.EditCompose(compose => compose.Rewrite("https://example.com"));

        Assert.Equal("30 / 1000", Counted(compose));
    }

    /// <summary>The colours move with the limit: the last tenth of it, and past it.</summary>
    [Theory]
    [InlineData(900, Role.Muted)]
    [InlineData(901, Role.QuotaLow)]
    [InlineData(1000, Role.QuotaLow)]
    [InlineData(1001, Role.Error)]
    public async Task TheColoursMoveWithTheLimit(int length, Role role)
    {
        var built = new AShell { Limits = FakeInstanceLimits.Setting(Roomy) };
        var shell = await built.Opened();

        var compose = ComposeRows.Open(shell, ComposeFor.Post);

        built.Host.Drain();
        shell.EditCompose(compose => compose.Rewrite(new string('a', length)));

        Assert.Equal(role, Assert.Single(Count(compose).Spans, span => span.Text.Trim().Length > 0).Role);
    }

    /// <summary>A reader who never writes a post costs no request for a limit.</summary>
    [Fact]
    public async Task NothingIsAskedUntilAPostIsWritten()
    {
        var built = new AShell();

        await built.Opened();

        Assert.Empty(built.Limits.Reads);
    }

    /// <summary>Asked once a session, however many posts are written; every one after is measured at once.</summary>
    [Fact]
    public async Task TheLimitIsAskedOnceASession()
    {
        var built = new AShell { Limits = FakeInstanceLimits.Setting(Roomy) };
        var shell = await built.Opened();

        ComposeRows.Open(shell, ComposeFor.Post);
        built.Host.Drain();
        shell.Back();
        built.Host.Drain();

        var again = ComposeRows.Open(shell, ComposeFor.Reply);

        Assert.Equal("1000", Counted(again).Split(" / ")[1]);
        Assert.Equal(["mastodon.social"], built.Limits.Reads);
    }

    /// <summary>
    ///     An instance that does not answer leaves Mastodon's default standing, says nothing about it rather than
    ///     counting a wait down over the post, and is asked again the next time one is written.
    /// </summary>
    [Fact]
    public async Task ALimitThatCannotBeReadIsSilentAndAskedAgain()
    {
        var built = new AShell { Limits = FakeInstanceLimits.Unanswering() };

        await AssertSilentAndAskedAgain(built);
    }

    /// <summary>And the same where the token is refused, which the reads that fill the screen already say.</summary>
    [Fact]
    public async Task ARefusedTokenIsSilentHereToo()
    {
        var built = new AShell { Limits = FakeInstanceLimits.Refusing(new AuthenticationException("No.")) };

        await AssertSilentAndAskedAgain(built);
    }

    private static async Task AssertSilentAndAskedAgain(AShell built)
    {
        var shell = await built.Opened();

        var compose = ComposeRows.Open(shell, ComposeFor.Post);

        built.Host.Drain();

        Assert.Equal("0 / 500", Counted(compose));
        Assert.Null(shell.Notice);

        shell.Back();
        built.Host.Drain();
        ComposeRows.Open(shell, ComposeFor.Post);
        built.Host.Drain();

        Assert.Equal(2, built.Limits.Reads.Count);
    }

    private static Line Count(ComposeScreen compose) =>
        compose.Lines(new Drawing(60, AShell.Now, Height: 17))[^1];

    private static string Counted(ComposeScreen compose) => Count(compose).Text.Trim();
}
