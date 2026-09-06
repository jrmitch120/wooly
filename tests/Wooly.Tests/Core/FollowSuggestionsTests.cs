using System.Net;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Wooly.Core;
using Wooly.Core.Discovery;
using Wooly.Core.Errors;
using Wooly.Core.Http;
using Wooly.Core.Profiles;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Core;

/// <summary>
///     Follow suggestions are the adapter behind <see cref="IFollowSuggestions" />, and ADR-0005 puts an adapter's
///     tests at the <see cref="HttpMessageHandler" /> seam. Two things are only observable here: that the read is the
///     <c>v2</c> endpoint rather than the <c>v1</c> one Mastonet has — the whole reason this port exists — and that the
///     reasons come across intact. The Discover screen fakes <see cref="IFollowSuggestions" /> instead.
/// </summary>
public class FollowSuggestionsTests
{
    private static readonly ActiveProfile Profile = new()
    {
        Name = "personal",
        Instance = "mastodon.social",
        Account = "jeff@mastodon.social",
        AccessToken = "token-personal",
    };

    /// <summary>
    ///     The call this port was built for: <c>v2</c>, over the same named client and carrying the same token as every
    ///     Mastonet call, because <c>v1</c> answers with the accounts and throws the reasons away.
    /// </summary>
    [Fact]
    public async Task Read_AsksTheV2EndpointForSuggestionsCarryingTheirSources()
    {
        var network = Answering(
            Offered(
                SuggestionJson(AccountJson("jon@hachyderm.io", id: "7"), "friends_of_friends"),
                SuggestionJson(AccountJson("sam", id: "8"), "most_followed")));

        var suggestions = await Suggestions(network).Read(Profile, limit: 40, TestContext.Current.CancellationToken);

        Assert.Equal(
            "https://mastodon.social/api/v2/suggestions?limit=40",
            network.Requests[0].RequestUri?.ToString());

        Assert.Equal(HttpMethod.Get, network.Requests[0].Method);
        Assert.Equal("Bearer token-personal", network.Requests[0].Headers.Authorization?.ToString());

        Assert.Collection(
            suggestions,
            jon =>
            {
                Assert.Equal("jon@hachyderm.io", jon.Account.Address);
                Assert.Equal([SuggestionReason.FriendsOfFriends], jon.Reasons);
            },
            sam =>
            {
                Assert.Equal("sam@mastodon.social", sam.Account.Address);
                Assert.Equal([SuggestionReason.MostFollowed], sam.Reasons);
            });
    }

    /// <summary>
    ///     <c>sources</c> is an array, and a suggestion can arrive under more than one reason. All of them come across
    ///     in the order the instance sent: which one a person is drawn under is the screen's ranking to do, and a port
    ///     that kept only the first would have made that choice for it.
    /// </summary>
    [Fact]
    public async Task Read_KeepsEverySourceASuggestionArrivedUnder()
    {
        var network = Answering(
            Offered(
                SuggestionJson(AccountJson("jon@hachyderm.io", id: "7"), "most_followed", "friends_of_friends")));

        var suggestions = await Suggestions(network).Read(Profile, limit: 40, TestContext.Current.CancellationToken);

        Assert.Equal(
            [SuggestionReason.MostFollowed, SuggestionReason.FriendsOfFriends],
            suggestions.Single().Reasons);
    }

    /// <summary>
    ///     Mastodon adds sources. A word this client has no heading for is dropped from the reasons and the person is
    ///     kept, because a suggestion with no reason this client names is still somebody worth showing — what to do
    ///     with a row that has no heading is the screen's decision, not this port's.
    /// </summary>
    [Fact]
    public async Task Read_KeepsASuggestionWhoseSourceThisClientDoesNotName()
    {
        var network = Answering(
            Offered(SuggestionJson(AccountJson("jon@hachyderm.io", id: "7"), "past_interactions", "featured")));

        var suggestions = await Suggestions(network).Read(Profile, limit: 40, TestContext.Current.CancellationToken);

        var jon = Assert.Single(suggestions);
        Assert.Equal("jon@hachyderm.io", jon.Account.Address);
        Assert.Equal([SuggestionReason.Featured], jon.Reasons);
    }

    /// <summary>A suggestion whose every source is a word this client has never seen still has a person on it.</summary>
    [Fact]
    public async Task Read_KeepsASuggestionThatArrivedUnderNoReasonThisClientNames()
    {
        var network = Answering(Offered(SuggestionJson(AccountJson("jon@hachyderm.io", id: "7"), "past_interactions")));

        var suggestions = await Suggestions(network).Read(Profile, limit: 40, TestContext.Current.CancellationToken);

        Assert.Equal("jon@hachyderm.io", suggestions.Single().Account.Address);
        Assert.Empty(suggestions.Single().Reasons);
    }

    /// <summary>
    ///     An instance is entitled to send a suggestion with no <c>sources</c> at all, and Mastonet's own entities leave
    ///     an absent array null rather than empty. Reading one as nobody's reason rather than as a missing property is
    ///     what keeps a null out of a screen that is about to walk the list.
    /// </summary>
    [Fact]
    public async Task Read_ReadsASuggestionThatNamedNoSourcesAsCarryingNoReason()
    {
        var network = Answering($$"""[{"account":{{AccountJson("jon@hachyderm.io", id: "7")}}}]""");

        var suggestions = await Suggestions(network).Read(Profile, limit: 40, TestContext.Current.CancellationToken);

        Assert.Empty(suggestions.Single().Reasons);
    }

    /// <summary>
    ///     Nobody suggested is a real answer, and the only one the Discover screen has to tell apart from a failure. It
    ///     is the empty list rather than nothing, because the screen always asks on arrival — there is no not-asked
    ///     here for a null to stand for.
    /// </summary>
    [Fact]
    public async Task Read_AnswersNobodySuggestedAsAnEmptyListRatherThanNothing()
    {
        var suggestions = await Suggestions(Answering("[]")).Read(Profile, limit: 40, TestContext.Current.CancellationToken);

        Assert.Empty(suggestions);
    }

    /// <summary>A body that is nothing at all is not documented, and the nearest true thing to say about it is nobody.</summary>
    [Fact]
    public async Task Read_ReadsAnInstanceThatSentNoBodyAsNobodySuggested()
    {
        var suggestions = await Suggestions(Answering("null")).Read(Profile, limit: 40, TestContext.Current.CancellationToken);

        Assert.Empty(suggestions);
    }

    /// <summary>
    ///     The opposite of familiar followers, and deliberately: this is the Discover screen's only call, so a failure
    ///     that answered with an empty list would draw <c>Nobody suggested.</c> over a rate limit. It throws, and the
    ///     screen's enquiry turns that into the shell's notice.
    /// </summary>
    [Fact]
    public async Task Read_ThrowsWhereARateLimitStoppedTheCall()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Status(HttpStatusCode.TooManyRequests));

        await Assert.ThrowsAsync<RateLimitedException>(() =>
            Suggestions(network).Read(Profile, limit: 40, TestContext.Current.CancellationToken));
    }

    /// <summary>An instance that turned the call down has not said nobody is suggested; it has said nothing at all.</summary>
    [Fact]
    public async Task Read_ThrowsWhereTheInstanceRefusedTheCall()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Refusal(HttpStatusCode.Forbidden, "This action is not allowed"));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            Suggestions(network).Read(Profile, limit: 40, TestContext.Current.CancellationToken));
    }

    /// <summary>A network the retries could not ride out is the shell's notice too, and not an empty screen.</summary>
    [Fact]
    public async Task Read_ThrowsWhereTheInstanceCouldNotBeReached() =>
        await Assert.ThrowsAsync<TransientNetworkException>(() =>
            Suggestions(ScriptedHttpMessageHandler.AlwaysUnreachable())
                .Read(Profile, limit: 40, TestContext.Current.CancellationToken));

    /// <summary>A body that is not what the endpoint documents is a failure like any other, and not nobody suggested.</summary>
    [Fact]
    public async Task Read_ThrowsWhereTheInstanceAnsweredWithSomethingUnreadable() =>
        await Assert.ThrowsAsync<JsonException>(() =>
            Suggestions(Answering("{\"error\":\"nope\"}"))
                .Read(Profile, limit: 40, TestContext.Current.CancellationToken));

    /// <summary>
    ///     The half of this port Mastonet does have. It is <c>v1</c> — there is no <c>v2</c> dismissal — and it names
    ///     the account rather than the suggestion, there being no id for a suggestion at all.
    /// </summary>
    [Fact]
    public async Task Dismiss_TellsTheInstanceToStopSuggestingTheAccount()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json("{}"));

        await Suggestions(network).Dismiss(Profile, "42", TestContext.Current.CancellationToken);

        Assert.Equal("https://mastodon.social/api/v1/suggestions/42", network.Requests[0].RequestUri?.ToString());
        Assert.Equal(HttpMethod.Delete, network.Requests[0].Method);
    }

    /// <summary>
    ///     What Mastonet 3.1.3 actually does with a refused dismissal, pinned here because it is not what the rest of
    ///     this client does with one and a screen is about to be built on it. <c>DeleteFollowSuggestion</c> answers
    ///     <see cref="Task" /> rather than an entity, so it never deserializes a body and never reaches the check that
    ///     turns Mastodon's error JSON into a <c>ServerErrorException</c> — a refusal comes back as a return.
    /// </summary>
    /// <remarks>
    ///     Recorded rather than worked around, and it costs less than it looks like it does: checked against a live
    ///     instance, this endpoint answers <c>200</c> to any account id at all, so the refusals swallowed here are ones
    ///     Mastodon does not actually make. The statuses below are what a differently-configured instance might send,
    ///     pinned so that a later reader knows this silence was measured rather than assumed.
    /// </remarks>
    [Theory]
    [InlineData(HttpStatusCode.NotFound, "Record not found")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "Validation failed")]
    public async Task Dismiss_ComesBackWithoutComplaintWhereTheInstanceRefusedTheCall(
        HttpStatusCode statusCode,
        string error)
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Refusal(statusCode, error));

        await Suggestions(network).Dismiss(Profile, "42", TestContext.Current.CancellationToken);

        Assert.Single(network.Requests);
    }

    /// <summary>
    ///     The one failure a dismissal does report, and it reports it because it never got as far as the library: an
    ///     instance the retries could not reach is the handler's business rather than Mastonet's.
    /// </summary>
    [Fact]
    public async Task Dismiss_ThrowsWhereTheInstanceCouldNotBeReached() =>
        await Assert.ThrowsAsync<TransientNetworkException>(() =>
            Suggestions(ScriptedHttpMessageHandler.AlwaysUnreachable())
                .Dismiss(Profile, "42", TestContext.Current.CancellationToken));

    /// <summary>A dismissal is a call like any other, and a rate limit stops it the way it stops the read.</summary>
    [Fact]
    public async Task Dismiss_ThrowsWhereARateLimitStoppedTheCall()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Status(HttpStatusCode.TooManyRequests));

        await Assert.ThrowsAsync<RateLimitedException>(() =>
            Suggestions(network).Dismiss(Profile, "42", TestContext.Current.CancellationToken));
    }

    /// <summary>Resolved from the container the app builds, so the wiring is under test alongside the behavior.</summary>
    private static IFollowSuggestions Suggestions(HttpMessageHandler network)
    {
        var services = new ServiceCollection();
        services.AddWoolyCore();

        // The one thing swapped out of the real container: a test that fakes an unreachable instance would otherwise
        // spend the retry policy's backoff waiting it out for real.
        services.AddSingleton<IRetryDelay>(new RecordingRetryDelay());
        services.AddHttpClient(WoolyClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => network);

        return services.BuildServiceProvider().GetRequiredService<IFollowSuggestions>();
    }

    /// <summary>A network answering each call in turn with the JSON given, repeating the last once it runs out.</summary>
    private static ScriptedHttpMessageHandler Answering(params string[] payloads) =>
        new(payloads.Select(ScriptedHttpMessageHandler.Json).ToArray());

    /// <summary>The array an instance wraps its suggestions in, named apart from the port so the two do not read alike.</summary>
    private static string Offered(params string[] suggestions) => $"[{string.Join(",", suggestions)}]";

    /// <summary>
    ///     What <c>/v2/suggestions</c> answers with: an account, and the sources it was suggested under. Written out
    ///     here because Mastonet 3.1.3 has no entity for an endpoint it does not call — its <c>v1</c> one has a single
    ///     <c>source</c> string and no array at all, which is the whole reason this port reads <c>v2</c>.
    /// </summary>
    private static string SuggestionJson(string account, params string[] sources) =>
        $$"""{"sources":[{{string.Join(",", sources.Select(source => $"\"{source}\""))}}],"account":{{account}}}""";

    /// <param name="account">
    ///     The wire's <c>acct</c>: bare for an account on the instance being read, <c>username@instance</c> for one
    ///     anywhere else.
    /// </param>
    private static string AccountJson(string account, string id) =>
        $$"""
          {
            "id": "{{id}}",
            "username": "{{account.Split('@')[0]}}",
            "acct": "{{account}}",
            "display_name": "Alice",
            "url": "https://{{(account.Contains('@') ? account.Split('@')[1] : "mastodon.social")}}/@{{account.Split('@')[0]}}",
            "note": "<p>Hello</p>",
            "avatar": "https://hachyderm.io/avatars/default.png",
            "locked": false,
            "bot": false,
            "fields": [],
            "created_at": "2020-01-01T00:00:00.000Z",
            "followers_count": 1203,
            "following_count": 187,
            "statuses_count": 4210
          }
          """;
}
