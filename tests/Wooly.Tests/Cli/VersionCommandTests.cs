using Spectre.Console.Testing;
using Wooly.Cli;

namespace Wooly.Tests.Cli;

public class VersionCommandTests
{
    [Fact]
    public void Version_PrintsTheClientNameAndVersion()
    {
        var console = new TestConsole();
        var app = WoolyCommandApp.Create(console);

        var exitCode = app.Run(["version"], TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        // Matched rather than hard-coded: the version is stamped at build time, so pinning it here would turn every
        // release bump into a test failure.
        Assert.Matches(@"^mastodon-cli \d+\.\d+\.\d+", console.Output.Trim());
    }

    [Fact]
    public void UnknownCommand_FailsWithANonZeroExitCode()
    {
        var console = new TestConsole();
        var app = WoolyCommandApp.Create(console);

        var exitCode = app.Run(["definitely-not-a-command"], TestContext.Current.CancellationToken);

        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public void Help_IsWrittenUnderTheClientsInvocationName()
    {
        var console = new TestConsole();
        var app = WoolyCommandApp.Create(console);

        var exitCode = app.Run(["--help"], TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Contains("mastodon-cli", console.Output);
        Assert.Contains("version", console.Output);
    }
}
