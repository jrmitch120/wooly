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
///     The custom emoji an instance offers its people to write are the adapter behind <see cref="IInstanceEmoji" />, and
///     ADR-0005 puts an adapter's tests at the <see cref="HttpMessageHandler" /> seam: which endpoint is read, and which
///     of what it lists are kept, are only observable here (#578). The `:` list fakes <see cref="IInstanceEmoji" />.
/// </summary>
public class InstanceEmojiTests
{
    private static readonly ActiveProfile Profile = new()
    {
        Name = "personal",
        Instance = "mastodon.social",
        Account = "jeff@mastodon.social",
        AccessToken = "token-personal",
    };

    /// <summary>Each emoji is read off the instance's list as its shortcode and its still picture.</summary>
    [Fact]
    public async Task Read_TakesEachEmojiAtItsStillFromTheInstancesList()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json(
            """
            [
              { "shortcode": "blobcat", "url": "https://files.example/blobcat.gif",
                "static_url": "https://files.example/blobcat.png", "visible_in_picker": true, "category": "Blobs" },
              { "shortcode": "US_AZ", "url": "https://files.example/US_AZ.png",
                "static_url": "https://files.example/US_AZ-static.png", "visible_in_picker": true }
            ]
            """));

        var emoji = await Emoji(network).Read(Profile, TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                new CustomEmoji("blobcat", "https://files.example/blobcat.png"),
                new CustomEmoji("US_AZ", "https://files.example/US_AZ-static.png"),
            ],
            emoji);
        Assert.Equal("https://mastodon.social/api/v1/custom_emojis", network.Requests.Single().RequestUri?.ToString());
    }

    /// <summary>
    ///     An emoji the instance hides from its picker is not one it offers its people to write, so it is not read; it
    ///     still draws wherever a post or an account lists it, which is another read altogether.
    /// </summary>
    [Fact]
    public async Task Read_LeavesOutWhatTheInstanceHidesFromItsPicker()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json(
            """
            [
              { "shortcode": "hidden", "url": "https://files.example/hidden.png",
                "static_url": "https://files.example/hidden.png", "visible_in_picker": false },
              { "shortcode": "shown", "url": "https://files.example/shown.png",
                "static_url": "https://files.example/shown.png", "visible_in_picker": true }
            ]
            """));

        var emoji = await Emoji(network).Read(Profile, TestContext.Current.CancellationToken);

        Assert.Equal([new CustomEmoji("shown", "https://files.example/shown.png")], emoji);
    }

    /// <summary>An emoji with no shortcode or no still picture is nothing that could be written or drawn.</summary>
    [Theory]
    [InlineData("""{ "shortcode": "", "static_url": "https://files.example/a.png", "visible_in_picker": true }""")]
    [InlineData("""{ "shortcode": " ", "static_url": "https://files.example/a.png", "visible_in_picker": true }""")]
    [InlineData("""{ "shortcode": "php", "static_url": "", "visible_in_picker": true }""")]
    [InlineData("""{ "shortcode": "php", "visible_in_picker": true }""")]
    public async Task Read_LeavesOutAnEmojiWithNoShortcodeOrNoPicture(string blank)
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json(
            $$"""
              [
                {{blank}},
                { "shortcode": "kept", "static_url": "https://files.example/kept.png", "visible_in_picker": true }
              ]
              """));

        var emoji = await Emoji(network).Read(Profile, TestContext.Current.CancellationToken);

        Assert.Equal([new CustomEmoji("kept", "https://files.example/kept.png")], emoji);
    }

    /// <summary>
    ///     A rate limit, an unreachable instance, one that took too long, one that refused, and one answering with
    ///     something that is not Mastodon's are each the instance not answering, which is said as nothing at all rather
    ///     than as an instance with no emoji, so the caller can tell the two apart and ask again.
    /// </summary>
    [Theory]
    [InlineData("rate limit")]
    [InlineData("unreachable")]
    [InlineData("timeout")]
    [InlineData("refusal")]
    [InlineData("nonsense")]
    public async Task Read_AnswersNothingWhereTheInstanceDidNotAnswer(string how)
    {
        var network = how switch
        {
            "rate limit" => new ScriptedHttpMessageHandler(
                ScriptedHttpMessageHandler.Status(HttpStatusCode.TooManyRequests)),
            "unreachable" => ScriptedHttpMessageHandler.AlwaysUnreachable(),
            "timeout" => new ScriptedHttpMessageHandler(_ => throw new TaskCanceledException("The request timed out.")),
            "refusal" => new ScriptedHttpMessageHandler(
                ScriptedHttpMessageHandler.Refusal(HttpStatusCode.Forbidden, "This action is not allowed")),
            _ => new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json("not json")),
        };

        Assert.Null(await Emoji(network).Read(Profile, TestContext.Current.CancellationToken));
    }

    private static IInstanceEmoji Emoji(HttpMessageHandler network)
    {
        var services = new ServiceCollection();
        services.AddWoolyCore();

        // A test that fakes an unreachable instance would otherwise spend the retry policy's backoff waiting it out.
        services.AddSingleton<IRetryDelay>(new RecordingRetryDelay());
        services.AddHttpClient(WoolyClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => network);

        return services.BuildServiceProvider().GetRequiredService<IInstanceEmoji>();
    }
}
