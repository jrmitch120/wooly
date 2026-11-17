using System.Net;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Wooly.Core;
using Wooly.Core.Errors;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Core;

/// <summary>
///     The translation port's adapter, at the <see cref="HttpMessageHandler" /> seam ADR-0005 gives an adapter: what it
///     does is ask the instance's translate endpoint raw (Mastonet has none, ADR-0037) and turn what it answers with
///     into a <see cref="Translation" />, so the request sent and the payload read are what make it observable.
///     Commands above this fake <see cref="IPostTranslation" /> instead.
/// </summary>
public class PostTranslationTests
{
    private static readonly ActiveProfile Profile = new()
    {
        Name = "personal",
        Instance = "mastodon.social",
        Account = "jeff@mastodon.social",
        AccessToken = "token-personal",
    };

    /// <summary>
    ///     With no target known, nothing is said about one: the instance falls back to the account's own web-interface
    ///     language, which no API exposes and so nothing here could send in its place.
    /// </summary>
    [Fact]
    public async Task Translate_PostsToTheTranslateEndpointSayingNothingOfATargetWhereNoneIsKnown()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json(TranslationJson()));

        await Resolved(network).Translate(Profile, "110", target: null, TestContext.Current.CancellationToken);

        var request = Assert.Single(network.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://mastodon.social/api/v1/statuses/110/translate", request.RequestUri?.ToString());
        Assert.Equal("Bearer token-personal", request.Headers.Authorization?.ToString());
        Assert.DoesNotContain("lang", Assert.Single(network.Bodies));
    }

    /// <summary>A target known — from <c>--to</c> or <c>translate_to</c> — is sent as the instance's <c>lang</c>.</summary>
    [Fact]
    public async Task Translate_SendsTheTargetAsTheInstancesLangWhereOneIsKnown()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json(TranslationJson()));

        await Resolved(network).Translate(Profile, "110", "pt-BR", TestContext.Current.CancellationToken);

        Assert.Equal("lang=pt-BR", Assert.Single(network.Bodies));
        Assert.Equal(
            "application/x-www-form-urlencoded",
            Assert.Single(network.Requests).Content?.Headers.ContentType?.MediaType);
    }

    /// <summary>
    ///     A whole 4.2+ answer read into one value: the text flattened as a post's is, the warning as the plain text
    ///     the instance sends it as, the poll's options matched to the poll by its id and each description to its
    ///     attachment by the attachment's, and the two languages and the translator named.
    /// </summary>
    [Fact]
    public async Task Translate_ReadsEveryPartOfThePostTheInstanceTranslated()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json(TranslationJson()));

        var translation = await Resolved(network).Translate(
            Profile,
            "110",
            "en",
            TestContext.Current.CancellationToken);

        Assert.Equal("I love my cat.\n\nShe sleeps #cats all day.", translation.Content);
        Assert.Equal("Cats & dogs", translation.ContentWarning);
        Assert.Equal("34858", translation.PollId);
        Assert.Equal(["Yes", "No"], translation.PollOptions);
        Assert.Equal("A cat asleep on a keyboard", translation.Descriptions["22345792"]);
        Assert.Equal("de", translation.From);
        Assert.Equal("en", translation.To);
        Assert.Equal("DeepL.com", translation.Provider);
    }

    /// <summary>
    ///     A post with no warning, poll or attachments is answered with an empty warning, a null poll and no
    ///     attachments, and a translator that did not say what it detected with a null — each read as nothing rather
    ///     than as an empty thing to show.
    /// </summary>
    [Fact]
    public async Task Translate_ReadsWhatThePostDidNotCarryAsNothing()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json(
            """
            {
              "detected_source_language": null,
              "language": "en",
              "provider": "LibreTranslate",
              "spoiler_text": "",
              "content": "<p>Hello</p>",
              "poll": null,
              "media_attachments": []
            }
            """));

        var translation = await Resolved(network).Translate(
            Profile,
            "110",
            target: null,
            TestContext.Current.CancellationToken);

        Assert.Equal("Hello", translation.Content);
        Assert.Null(translation.ContentWarning);
        Assert.Null(translation.PollId);
        Assert.Empty(translation.PollOptions);
        Assert.Empty(translation.Descriptions);
        Assert.Null(translation.From);
        Assert.Equal("LibreTranslate", translation.Provider);
    }

    /// <summary>
    ///     A refusal — 403 for a post only followers or the people it mentions can see, or a pair the translator does not
    ///     take; 404 for an instance with no translator — is raised in the instance's own words, as something a front
    ///     end says.
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "This action is not allowed")]
    [InlineData(HttpStatusCode.NotFound, "Record not found")]
    public async Task Translate_RaisesARefusalInTheInstancesOwnWords(HttpStatusCode status, string error)
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Refusal(status, error));

        var refusal = await Assert.ThrowsAsync<TranslationRefusedException>(
            () => Resolved(network).Translate(Profile, "110", "en", TestContext.Current.CancellationToken));

        Assert.Equal(error, refusal.Reason);
        Assert.Contains(error, refusal.Message);
    }

    /// <summary>
    ///     A translator rate-limited, out of quota or failing is a 503, raised as the instance failing to answer rather
    ///     than refusing — and with what it said, which is the only place the reader learns which of those it was.
    /// </summary>
    [Fact]
    public async Task Translate_RaisesATranslatorThatFailedAsTheInstanceFailingWithItsWords()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Refusal(
            HttpStatusCode.ServiceUnavailable,
            "The server-wide usage quota for the translation service has been exceeded."));

        var failure = await Assert.ThrowsAsync<InstanceFailedException>(
            () => Resolved(network).Translate(Profile, "110", "en", TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, failure.Status);
        Assert.Contains("The server-wide usage quota for the translation service has been exceeded.", failure.Message);
    }

    /// <summary>One call, so a rate limit is raised for ADR-0006's handler — or the TUI's countdown — to say.</summary>
    [Fact]
    public async Task Translate_RaisesTheRateLimitTheInstanceAnsweredWith()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Status(HttpStatusCode.TooManyRequests));

        var limited = await Assert.ThrowsAsync<RateLimitedException>(
            () => Resolved(network).Translate(Profile, "110", "en", TestContext.Current.CancellationToken));

        Assert.Equal("mastodon.social", limited.Instance);
    }

    /// <summary>
    ///     The languages map is read raw off the instance's own endpoint (#616): each language a post can be in, under
    ///     the code the instance gave it — <c>und</c> for a post that says none — with the languages it can be taken to,
    ///     region codes and all, as the instance wrote them.
    /// </summary>
    [Fact]
    public async Task Languages_ReadsTheMapWithUndAndRegionCodesAsTheInstanceWroteThem()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json(
            """
            {
              "und": ["de", "en-GB", "pt-BR"],
              "de": ["en-GB", "pt-BR"],
              "pt-BR": ["de"]
            }
            """));

        var languages = await Resolved(network).Languages(Profile, TestContext.Current.CancellationToken);

        var request = Assert.Single(network.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(
            "https://mastodon.social/api/v1/instance/translation_languages",
            request.RequestUri?.ToString());
        Assert.Equal("Bearer token-personal", request.Headers.Authorization?.ToString());
        Assert.Equal(["und", "de", "pt-BR"], languages.Targets.Keys);
        Assert.Equal(["de", "en-GB", "pt-BR"], languages.Targets["und"]);
        Assert.Equal(["en-GB", "pt-BR"], languages.Targets["de"]);
        Assert.Equal(["de"], languages.Targets["pt-BR"]);
    }

    /// <summary>
    ///     An instance with no translator answers an empty map, which takes nothing anywhere — not even with no target
    ///     known, where the language check is otherwise dropped.
    /// </summary>
    [Fact]
    public async Task Languages_ReadsAnEmptyMapAsTakingNothing()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json("{}"));

        var languages = await Resolved(network).Languages(Profile, TestContext.Current.CancellationToken);

        Assert.Empty(languages.Targets);
        Assert.False(languages.Takes("de", "en"));
        Assert.False(languages.Takes(from: null, to: null));
    }

    /// <summary>
    ///     An instance older than 4.2 has no such endpoint and answers 404, which is read as the empty map it amounts to
    ///     rather than as a failure: an instance that cannot translate, offering nothing.
    /// </summary>
    [Fact]
    public async Task Languages_ReadsAnInstanceWithoutTheEndpointAsAnEmptyMap()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Refusal(HttpStatusCode.NotFound, "Record not found"));

        var languages = await Resolved(network).Languages(Profile, TestContext.Current.CancellationToken);

        Assert.Empty(languages.Targets);
        Assert.False(languages.Takes(from: null, to: null));
    }

    private static IPostTranslation Resolved(HttpMessageHandler network)
    {
        var services = new ServiceCollection();
        services.AddWoolyCore();
        services.AddHttpClient(WoolyClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => network);

        return services.BuildServiceProvider().GetRequiredService<IPostTranslation>();
    }

    /// <summary>A whole answer, as Mastodon 4.2 and later give one: every part of the post translated together.</summary>
    private static string TranslationJson() =>
        """
        {
          "detected_source_language": "de",
          "language": "en",
          "provider": "DeepL.com",
          "spoiler_text": "Cats & dogs",
          "content": "<p>I love my cat.</p><p>She sleeps <a href=\"https://mastodon.social/tags/cats\">#cats</a> all day.</p>",
          "poll": {
            "id": "34858",
            "options": [{ "title": "Yes" }, { "title": "No" }]
          },
          "media_attachments": [
            { "id": "22345792", "description": "A cat asleep on a keyboard" }
          ]
        }
        """;
}
