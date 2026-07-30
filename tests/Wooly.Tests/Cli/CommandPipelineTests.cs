using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Testing;
using Wooly.Cli;
using Wooly.Core;
using Wooly.Core.Http;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Cli;

/// <summary>
///     The cross-cutting behavior every command inherits, exercised end to end through the real command app with only
///     the network faked: errors on stderr, reserved exit codes, bounded retries, and fail-fast rate limiting.
///     <para>
///         ADR-0005 makes <c>IMastodonClient</c> the seam for command logic and keeps
///         <see cref="HttpMessageHandler" /> fakes narrow. These tests use the HTTP seam deliberately, because what
///         they cover — retry, rate-limit translation, and the exit code each produces — only exists below
///         <c>IMastodonClient</c>; faking that interface would skip the very code under test (ADR-0006).
///     </para>
/// </summary>
public class CommandPipelineTests
{
    private const string InstanceJson = """{"domain":"mastodon.social","title":"Mastodon","version":"4.3.1"}""";

    [Fact]
    public void Run_WritesOnlyToStdoutAndExitsZeroWhenACommandSucceeds()
    {
        var run = Run(["version", "--instance", "mastodon.social"], ScriptedHttpMessageHandler.Json(InstanceJson));

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Contains("mastodon.social (Mastodon 4.3.1)", run.Output);
        Assert.Empty(run.ErrorOutput.Trim());
    }

    [Fact]
    public void Run_FailsFastWithTheRateLimitedExitCodeWhenTheInstanceRateLimits()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Status(HttpStatusCode.TooManyRequests));

        var run = Run(["version", "--instance", "mastodon.social"], network);

        Assert.Equal((int)ExitCode.RateLimited, run.ExitCode);
        Assert.Contains("Rate limited by mastodon.social", run.ErrorOutput);

        // Fail fast: the limit is neither waited out nor retried behind the user's back.
        Assert.Single(network.Requests);
        Assert.Empty(run.Delay.Waits);
    }

    [Fact]
    public void Run_RetriesAnUnreachableInstanceThenReportsTheNetworkExitCode()
    {
        var network = ScriptedHttpMessageHandler.AlwaysUnreachable();

        var run = Run(["version", "--instance", "mastodon.social"], network);

        Assert.Equal((int)ExitCode.NetworkError, run.ExitCode);
        Assert.Contains("Could not reach mastodon.social", run.ErrorOutput);
        Assert.Equal(3, network.Requests.Count);
        Assert.Equal([TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(750)], run.Delay.Waits);
    }

    [Fact]
    public void Run_SucceedsWhenAFlakyConnectionRecoversOnARetry()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Unreachable(),
            ScriptedHttpMessageHandler.Json(InstanceJson));

        var run = Run(["version", "--instance", "mastodon.social"], network);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Contains("mastodon.social (Mastodon 4.3.1)", run.Output);
        Assert.Empty(run.ErrorOutput.Trim());
    }

    [Fact]
    public void Run_ReportsAMistypedCommandAsAUsageErrorOnStderr()
    {
        // A typo of a command this client has, rather than a name it might one day grow into — this test outlived
        // the first such stand-in, which was "timeline home" before there was one.
        var run = Run(["tiimeline", "home"], ScriptedHttpMessageHandler.Json(InstanceJson));

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.NotEmpty(run.ErrorOutput.Trim());
        Assert.Empty(run.Output.Trim());
    }

    /// <summary>
    ///     An instance turning a request down is an expected outcome, not a defect in this client, so it gets a plain
    ///     line and the general error code rather than a stack trace.
    /// </summary>
    [Fact]
    public void Run_ReportsAnInstanceErrorAsAPlainMessageWithTheGeneralExitCode()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Refusal(HttpStatusCode.NotFound, "Record not found"));

        var run = Run(["version", "--instance", "mastodon.social"], network);

        Assert.Equal((int)ExitCode.Error, run.ExitCode);
        Assert.StartsWith("error:", run.ErrorOutput.Trim());
        Assert.Contains("Record not found", run.ErrorOutput);
    }

    /// <summary>Pointing the CLI at a domain that isn't an instance is a typo, not something to hand back a parser
    ///     stack trace for.</summary>
    [Fact]
    public void Run_ReportsADomainThatIsNotAnInstanceAsAPlainMessage()
    {
        var network = new ScriptedHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<!doctype html><html></html>", Encoding.UTF8, "text/html"),
        });

        var run = Run(["version", "--instance", "example.com"], network);

        Assert.Equal((int)ExitCode.Error, run.ExitCode);
        Assert.Contains("did not answer with Mastodon API data", run.ErrorOutput);
        Assert.DoesNotContain("JsonException", run.ErrorOutput);
    }

    /// <summary>Stdout has to stay pipeable, so no part of a failure may leak into it.</summary>
    [Fact]
    public void Run_KeepsAFailuresErrorTextOutOfStdout()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Status(HttpStatusCode.TooManyRequests));

        var run = Run(["version", "--instance", "mastodon.social"], network);

        Assert.DoesNotContain("Rate limited", run.Output);
        Assert.DoesNotContain("error:", run.Output);
    }

    private static CommandRun Run(string[] args, params Func<HttpRequestMessage, HttpResponseMessage>[] steps) =>
        Run(args, new ScriptedHttpMessageHandler(steps));

    private static CommandRun Run(string[] args, ScriptedHttpMessageHandler network)
    {
        // Wide enough that no assertion is defeated by a wrapped line.
        var console = new TestConsole().Width(200);
        var errorConsole = new TestConsole().Width(200);
        var delay = new RecordingRetryDelay();

        var app = WoolyCommandApp.Create(console, errorConsole, services =>
        {
            services.AddSingleton<IRetryDelay>(delay);
            services.AddHttpClient(WoolyClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => network);
        });

        var exitCode = app.Run(args);

        return new CommandRun(exitCode, console.Output, errorConsole.Output, delay);
    }

    private sealed record CommandRun(int ExitCode, string Output, string ErrorOutput, RecordingRetryDelay Delay);
}
