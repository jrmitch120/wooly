using System.Net;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Wooly.Core;
using Wooly.Core.Http;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Core;

/// <summary>
///     An instance's post limits are the adapter behind <see cref="IInstanceLimits" />, and ADR-0005 puts an adapter's
///     tests at the <see cref="HttpMessageHandler" /> seam: which endpoint is read, and which field of which answer the
///     limit comes from, are only observable here (#319). The compose screen fakes <see cref="IInstanceLimits" />.
/// </summary>
public class InstanceLimitsTests
{
    private static readonly ActiveProfile Profile = new()
    {
        Name = "personal",
        Instance = "mastodon.social",
        Account = "jeff@mastodon.social",
        AccessToken = "token-personal",
    };

    /// <summary>Mastodon 4 and later say both limits under <c>/api/v2/instance</c>'s configuration.</summary>
    [Fact]
    public async Task Read_TakesBothLimitsFromTheV2Instance()
    {
        var network = Answering(
            """{"domain":"mastodon.social","configuration":{"statuses":{"max_characters":1000,"characters_reserved_per_url":30}}}""");

        var limits = await Limits(network).Read(Profile, TestContext.Current.CancellationToken);

        Assert.Equal(new PostLimits(1000, 30), limits);
        Assert.Equal("https://mastodon.social/api/v2/instance", network.Requests.Single().RequestUri?.ToString());
    }

    /// <summary>An instance from before <c>v2</c> existed is asked <c>v1</c>, which carried the same configuration.</summary>
    [Fact]
    public async Task Read_AsksV1WhereThereIsNoV2()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Status(HttpStatusCode.NotFound),
            ScriptedHttpMessageHandler.Json(
                """{"uri":"old.example","configuration":{"statuses":{"max_characters":5000}}}"""));

        var limits = await Limits(network).Read(Profile, TestContext.Current.CancellationToken);

        Assert.Equal(new PostLimits(5000, 23), limits);
        Assert.Equal("https://mastodon.social/api/v1/instance", network.Requests[1].RequestUri?.ToString());
    }

    /// <summary>Pleroma and Akkoma answer <c>v1</c> with a limit of their own name.</summary>
    [Fact]
    public async Task Read_TakesPleromasOwnNameForTheLimit()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Status(HttpStatusCode.NotFound),
            ScriptedHttpMessageHandler.Json("""{"uri":"pleroma.example","max_toot_chars":5000}"""));

        var limits = await Limits(network).Read(Profile, TestContext.Current.CancellationToken);

        Assert.Equal(new PostLimits(5000, 23), limits);
    }

    /// <summary>An instance that says nothing about its limits is taken to have Mastodon's.</summary>
    [Fact]
    public async Task Read_TakesMastodonsDefaultsWhereTheInstanceSaysNothing()
    {
        var limits = await Limits(Answering("""{"domain":"quiet.example"}"""))
            .Read(Profile, TestContext.Current.CancellationToken);

        Assert.Equal(PostLimits.Default, limits);
    }

    /// <summary>
    ///     A rate limit is not a limit of 500: the instance did not answer, which is said as nothing at all, so the
    ///     caller can tell it from an answer and ask again.
    /// </summary>
    [Fact]
    public async Task Read_AnswersNothingWhereARateLimitStoppedTheCall()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Status(HttpStatusCode.TooManyRequests));

        Assert.Null(await Limits(network).Read(Profile, TestContext.Current.CancellationToken));
    }

    /// <summary>Nor is an instance that could not be reached.</summary>
    [Fact]
    public async Task Read_AnswersNothingWhereTheInstanceCouldNotBeReached() =>
        Assert.Null(
            await Limits(ScriptedHttpMessageHandler.AlwaysUnreachable()).Read(Profile, TestContext.Current.CancellationToken));

    /// <summary>Nor one that took too long, which the HTTP stack says by cancelling a call nobody asked to cancel.</summary>
    [Fact]
    public async Task Read_AnswersNothingWhereTheInstanceTookTooLong()
    {
        var network = new ScriptedHttpMessageHandler(_ => throw new TaskCanceledException("The request timed out."));

        Assert.Null(await Limits(network).Read(Profile, TestContext.Current.CancellationToken));
    }

    /// <summary>Nor one that refused, or answered with something that is not Mastodon's.</summary>
    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "{}")]
    [InlineData(HttpStatusCode.OK, "not json")]
    public async Task Read_AnswersNothingWhereTheInstanceRefusedOrAnsweredNonsense(HttpStatusCode status, string body)
    {
        var network = new ScriptedHttpMessageHandler(
            _ => new HttpResponseMessage(status) { Content = new StringContent(body) });

        Assert.Null(await Limits(network).Read(Profile, TestContext.Current.CancellationToken));
    }

    private static IInstanceLimits Limits(HttpMessageHandler network)
    {
        var services = new ServiceCollection();
        services.AddWoolyCore();

        // A test that fakes an unreachable instance would otherwise spend the retry policy's backoff waiting it out.
        services.AddSingleton<IRetryDelay>(new RecordingRetryDelay());
        services.AddHttpClient(WoolyClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => network);

        return services.BuildServiceProvider().GetRequiredService<IInstanceLimits>();
    }

    private static ScriptedHttpMessageHandler Answering(params string[] payloads) =>
        new(payloads.Select(ScriptedHttpMessageHandler.Json).ToArray());
}
