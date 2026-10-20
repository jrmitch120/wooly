using System.Net;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Wooly.Core;
using Wooly.Core.Accounts;
using Wooly.Core.Errors;
using Wooly.Core.Http;
using Wooly.Core.Profiles;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Core;

/// <summary>
///     The own account's public profile is the adapter behind <see cref="IPublicProfileEditor" />, and ADR-0005 puts an
///     adapter's tests at the <see cref="HttpMessageHandler" /> seam: that the read takes <c>source</c> rather than the
///     HTML, and that a save sends only what changed, are only observable here (#404, ADR-0028).
/// </summary>
public class PublicProfileEditorTests
{
    private static readonly ActiveProfile Profile = new()
    {
        Name = "personal",
        Instance = "mastodon.social",
        Account = "jeff@mastodon.social",
        AccessToken = "token-personal",
    };

    /// <summary>
    ///     What <c>verify_credentials</c> answers for an account whose bio and one field carry links, which the HTML
    ///     flattens and <c>source</c> keeps as written.
    /// </summary>
    private const string Credentials = """
        {
          "id": "1",
          "username": "jeff",
          "acct": "jeff",
          "display_name": "Jeff",
          "note": "<p>Writes <a href=\"https://wooly.example\">Wooly</a></p>",
          "avatar": "https://files.example/avatar.png",
          "header": "https://files.example/header.png",
          "locked": true,
          "bot": false,
          "discoverable": false,
          "created_at": "2022-11-05T00:00:00.000Z",
          "followers_count": 3,
          "following_count": 4,
          "statuses_count": 5,
          "fields": [{ "name": "Site", "value": "<a href=\"https://wooly.example\">wooly.example</a>", "verified_at": null }],
          "source": {
            "note": "Writes https://wooly.example",
            "fields": [{ "name": "Site", "value": "https://wooly.example", "verified_at": null }],
            "privacy": "public",
            "discoverable": true
          }
        }
        """;

    /// <summary>The bio and fields as written, never as flattened, and the rest off the account itself.</summary>
    [Fact]
    public async Task Read_TakesWhatTheAccountWroteFromSourceRatherThanTheHtml()
    {
        var network = Answering(Credentials);

        var read = await Editor(network).Read(Profile, TestContext.Current.CancellationToken);

        Assert.Equal("Jeff", read.DisplayName);
        Assert.Equal("Writes https://wooly.example", read.Bio);
        Assert.Equal([new PublicProfileField("Site", "https://wooly.example")], read.Fields);
        Assert.Equal("https://files.example/avatar.png", read.AvatarUrl);
        Assert.Equal("https://files.example/header.png", read.HeaderUrl);
        Assert.True(read.IsLocked);
        Assert.False(read.IsBot);
        Assert.True(read.IsDiscoverable);
        Assert.Equal("jeff@mastodon.social", read.Account.Address);

        var request = Assert.Single(network.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://mastodon.social/api/v1/accounts/verify_credentials", request.RequestUri?.ToString());
        Assert.Equal("token-personal", request.Headers.Authorization?.Parameter);
    }

    /// <summary>An account that wrote nothing has an empty bio and no fields, rather than nothing to read.</summary>
    [Fact]
    public async Task Read_TakesNothingWrittenAsEmpty()
    {
        var read = await Editor(Answering("""
                {"id":"1","username":"jeff","acct":"jeff","display_name":"","note":"","created_at":"2022-11-05T00:00:00.000Z",
                 "locked":false,"source":{"note":"","fields":[]}}
                """))
            .Read(Profile, TestContext.Current.CancellationToken);

        Assert.Equal("", read.DisplayName);
        Assert.Equal("", read.Bio);
        Assert.Empty(read.Fields);
        Assert.False(read.IsBot);
        Assert.False(read.IsDiscoverable);
    }

    [Fact]
    public async Task Read_SaysARefusalInTheInstancesOwnWords()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Refusal(HttpStatusCode.Forbidden, "This action is outside the authorized scopes"));

        var refused = await Assert.ThrowsAsync<PublicProfileRefusedException>(() =>
            Editor(network).Read(Profile, TestContext.Current.CancellationToken));

        Assert.Equal("This action is outside the authorized scopes", refused.Reason);
    }

    /// <summary>Only what the change names goes, so nothing left alone can overwrite a change made on the web.</summary>
    [Fact]
    public async Task Save_SendsOnlyWhatTheChangeNames()
    {
        var network = Answering(Credentials);

        await Editor(network).Save(
            Profile,
            new PublicProfileChange { DisplayName = "Jeff M", IsLocked = false },
            TestContext.Current.CancellationToken);

        var request = Assert.Single(network.Requests);
        Assert.Equal(HttpMethod.Patch, request.Method);
        Assert.Equal("https://mastodon.social/api/v1/accounts/update_credentials", request.RequestUri?.ToString());
        Assert.Equal("token-personal", request.Headers.Authorization?.Parameter);
        Assert.Equal([("display_name", "Jeff M"), ("locked", "false")], Sent(network));
    }

    [Fact]
    public async Task Save_SendsTheBioAsWritten()
    {
        var network = Answering(Credentials);

        await Editor(network).Save(
            Profile,
            new PublicProfileChange { Bio = "Line one\nhttps://wooly.example & more" },
            TestContext.Current.CancellationToken);

        Assert.Equal([("note", "Line one\nhttps://wooly.example & more")], Sent(network));
    }

    /// <summary>Mastonet does not carry <c>bot</c>, so it is sent by hand like everything else here.</summary>
    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public async Task Save_SendsBot(bool bot, string sent)
    {
        var network = Answering(Credentials);

        await Editor(network).Save(
            Profile,
            new PublicProfileChange { IsBot = bot, IsDiscoverable = !bot },
            TestContext.Current.CancellationToken);

        Assert.Equal([("bot", sent), ("discoverable", (!bot).ToString().ToLowerInvariant())], Sent(network));
    }

    /// <summary>
    ///     Mastodon replaces the fields as a set, so all four go every time — the rows not wanted as blanks, which is
    ///     how an instance is told to drop them.
    /// </summary>
    [Fact]
    public async Task Save_SendsAllFourFieldsTogether()
    {
        var network = Answering(Credentials);

        await Editor(network).Save(
            Profile,
            new PublicProfileChange { Fields = [new("Site", "https://wooly.example"), new("Pronouns", "they/them")] },
            TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                ("fields_attributes[0][name]", "Site"), ("fields_attributes[0][value]", "https://wooly.example"),
                ("fields_attributes[1][name]", "Pronouns"), ("fields_attributes[1][value]", "they/them"),
                ("fields_attributes[2][name]", ""), ("fields_attributes[2][value]", ""),
                ("fields_attributes[3][name]", ""), ("fields_attributes[3][value]", ""),
            ],
            Sent(network));
    }

    [Fact]
    public async Task Save_SendsNoFieldsAsFourBlanks()
    {
        var network = Answering(Credentials);

        await Editor(network).Save(Profile, new PublicProfileChange { Fields = [] }, TestContext.Current.CancellationToken);

        Assert.Equal(8, Sent(network).Count);
        Assert.All(Sent(network), sent => Assert.Equal("", sent.Value));
    }

    /// <summary>A fork that keeps more than four rows has them all sent, rather than the fifth cut off.</summary>
    [Fact]
    public async Task Save_SendsEveryFieldWhereThereAreMoreThanFour()
    {
        var network = Answering(Credentials);

        await Editor(network).Save(
            Profile,
            new PublicProfileChange { Fields = [.. Enumerable.Range(1, 5).Select(n => new PublicProfileField($"L{n}", $"V{n}"))] },
            TestContext.Current.CancellationToken);

        Assert.Equal(("fields_attributes[4][name]", "L5"), Sent(network)[8]);
        Assert.Equal(10, Sent(network).Count);
    }

    [Fact]
    public async Task Save_AnswersTheAccountAsTheInstanceNowHasIt()
    {
        var account = await Editor(Answering(Credentials)).Save(
            Profile,
            new PublicProfileChange { DisplayName = "Jeff" },
            TestContext.Current.CancellationToken);

        Assert.Equal("jeff@mastodon.social", account.Address);
        Assert.Equal("Jeff", account.Author);
        Assert.True(account.IsLocked);
    }

    /// <summary>A 422 is the instance judging what was sent, and says why in its own words.</summary>
    [Fact]
    public async Task Save_SaysARefusalInTheInstancesOwnWords()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Refusal(
                HttpStatusCode.UnprocessableEntity,
                "Validation failed: Display name is too long (maximum is 30 characters)"));

        var refused = await Assert.ThrowsAsync<PublicProfileRefusedException>(() =>
            Editor(network).Save(
                Profile,
                new PublicProfileChange { DisplayName = new string('x', 31) },
                TestContext.Current.CancellationToken));

        Assert.Equal("Validation failed: Display name is too long (maximum is 30 characters)", refused.Reason);
        Assert.Contains("Display name is too long", refused.Message);
    }

    [Fact]
    public async Task Save_SaysAnInstanceThatFailedToAnswerAsThat()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Status(HttpStatusCode.BadGateway));

        var failed = await Assert.ThrowsAsync<InstanceFailedException>(() =>
            Editor(network).Save(Profile, new PublicProfileChange { Bio = "" }, TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadGateway, failed.Status);
    }

    /// <summary>What the request carried, as the pairs a form sends, in order.</summary>
    private static List<(string Name, string Value)> Sent(ScriptedHttpMessageHandler network) =>
        network.Bodies.Single()
               .Split('&', StringSplitOptions.RemoveEmptyEntries)
               .Select(pair => pair.Split('='))
               .Select(pair => (Decoded(pair[0]), Decoded(pair[1])))
               .ToList();

    private static string Decoded(string encoded) => Uri.UnescapeDataString(encoded.Replace('+', ' '));

    private static IPublicProfileEditor Editor(HttpMessageHandler network)
    {
        var services = new ServiceCollection();
        services.AddWoolyCore();
        services.AddSingleton<IRetryDelay>(new RecordingRetryDelay());
        services.AddHttpClient(WoolyClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => network);

        return services.BuildServiceProvider().GetRequiredService<IPublicProfileEditor>();
    }

    private static ScriptedHttpMessageHandler Answering(string payload) =>
        new(ScriptedHttpMessageHandler.Json(payload));
}
