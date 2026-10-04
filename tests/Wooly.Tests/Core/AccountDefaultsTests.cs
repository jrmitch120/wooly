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
///     An account's own defaults are the adapter behind <see cref="IAccountDefaults" />, and ADR-0005 puts an adapter's
///     tests at the <see cref="HttpMessageHandler" /> seam: which endpoint is read, and which fields of its answer the
///     defaults come from, are only observable here (#339). The compose screen fakes <see cref="IAccountDefaults" />.
/// </summary>
public class AccountDefaultsTests
{
    private static readonly ActiveProfile Profile = new()
    {
        Name = "personal",
        Instance = "mastodon.social",
        Account = "jeff@mastodon.social",
        AccessToken = "token-personal",
    };

    /// <summary>Both come off <c>verify_credentials</c>'s <c>source</c>, which only the account itself is shown.</summary>
    [Fact]
    public async Task Read_TakesBothDefaultsFromTheAccountsOwnSource()
    {
        var network = Answering("""{"id":"1","username":"jeff","source":{"privacy":"unlisted","language":"fr"}}""");

        var defaults = await Defaults(network).Read(Profile, TestContext.Current.CancellationToken);

        Assert.Equal(new PostDefaults(PostVisibility.Unlisted, "fr"), defaults);
        Assert.Equal(
            "https://mastodon.social/api/v1/accounts/verify_credentials",
            network.Requests.Single().RequestUri?.ToString());
    }

    /// <summary>Mastodon's <c>private</c> is this client's followers (ADR-0024).</summary>
    [Fact]
    public async Task Read_TakesPrivateAsFollowers()
    {
        var defaults = await Defaults(Answering("""{"id":"1","source":{"privacy":"private","language":null}}"""))
            .Read(Profile, TestContext.Current.CancellationToken);

        Assert.Equal(new PostDefaults(PostVisibility.Followers, null), defaults);
    }

    /// <summary>An answer with no <c>source</c> says nothing about either, which is unknown rather than a failure.</summary>
    [Fact]
    public async Task Read_TakesNoSourceAsUnknown()
    {
        var defaults = await Defaults(Answering("""{"id":"1","username":"jeff"}"""))
            .Read(Profile, TestContext.Current.CancellationToken);

        Assert.Equal(PostDefaults.Unknown, defaults);
    }

    /// <summary>
    ///     A regional posting language Mastodon offers (Chinese as written in Taiwan) is a language this client knows,
    ///     not an unknown one.
    /// </summary>
    [Fact]
    public async Task Read_TakesARegionalPostingLanguage()
    {
        var defaults = await Defaults(Answering("""{"id":"1","source":{"privacy":"public","language":"zh-TW"}}"""))
            .Read(Profile, TestContext.Current.CancellationToken);

        Assert.Equal(new PostDefaults(PostVisibility.Public, "zh-TW"), defaults);
    }

    /// <summary>
    ///     A visibility or a language this client does not know is not one to start a post on: it is unknown, and the
    ///     other is still read.
    /// </summary>
    [Fact]
    public async Task Read_TakesWhatItDoesNotKnowAsUnknown()
    {
        var defaults = await Defaults(Answering("""{"id":"1","source":{"privacy":"local","language":"xx-nonsense"}}"""))
            .Read(Profile, TestContext.Current.CancellationToken);

        Assert.Equal(PostDefaults.Unknown, defaults);
    }

    /// <summary>A rate limit, an unreachable instance and a refusal are all unknown, never thrown.</summary>
    [Fact]
    public async Task Read_AnswersUnknownWhereARateLimitStoppedTheCall()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Status(HttpStatusCode.TooManyRequests));

        Assert.Equal(PostDefaults.Unknown, await Defaults(network).Read(Profile, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Read_AnswersUnknownWhereTheInstanceCouldNotBeReached() =>
        Assert.Equal(
            PostDefaults.Unknown,
            await Defaults(ScriptedHttpMessageHandler.AlwaysUnreachable())
                .Read(Profile, TestContext.Current.CancellationToken));

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "{}")]
    [InlineData(HttpStatusCode.OK, "not json")]
    public async Task Read_AnswersUnknownWhereTheInstanceRefusedOrAnsweredNonsense(HttpStatusCode status, string body)
    {
        var network = new ScriptedHttpMessageHandler(
            _ => new HttpResponseMessage(status) { Content = new StringContent(body) });

        Assert.Equal(PostDefaults.Unknown, await Defaults(network).Read(Profile, TestContext.Current.CancellationToken));
    }

    private static IAccountDefaults Defaults(HttpMessageHandler network)
    {
        var services = new ServiceCollection();
        services.AddWoolyCore();

        // A test that fakes an unreachable instance would otherwise spend the retry policy's backoff waiting it out.
        services.AddSingleton<IRetryDelay>(new RecordingRetryDelay());
        services.AddHttpClient(WoolyClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => network);

        return services.BuildServiceProvider().GetRequiredService<IAccountDefaults>();
    }

    private static ScriptedHttpMessageHandler Answering(params string[] payloads) =>
        new(payloads.Select(ScriptedHttpMessageHandler.Json).ToArray());
}
