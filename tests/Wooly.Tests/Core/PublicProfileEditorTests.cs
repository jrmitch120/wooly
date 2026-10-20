using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
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
public class PublicProfileEditorTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

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

    public void Dispose() => _directory.Dispose();

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

    /// <summary>
    ///     A picture has no endpoint of its own to go up ahead of time, so it goes in the save itself (ADR-0028): one
    ///     multipart request carrying the file, as the file it is, beside whatever else changed.
    /// </summary>
    [Fact]
    public async Task Save_SendsAnAvatarInTheSameRequestAsEverythingElse()
    {
        var network = Answering(Credentials);

        await Editor(network).Save(
            Profile,
            new PublicProfileChange { DisplayName = "Jeff M", Avatar = _directory.WriteFile("me.png") },
            TestContext.Current.CancellationToken);

        var request = Assert.Single(network.Requests);
        Assert.Equal(HttpMethod.Patch, request.Method);
        Assert.Equal("https://mastodon.social/api/v1/accounts/update_credentials", request.RequestUri?.ToString());
        Assert.Equal("multipart/form-data", request.Content?.Headers.ContentType?.MediaType);
        Assert.Equal(
            [("display_name", null, "text/plain"), ("avatar", "me.png", "image/png")],
            Parts(network));
        Assert.Contains("pretend this is a picture", network.Bodies[0]);
    }

    [Fact]
    public async Task Save_SendsAHeaderAndAnAvatarTogether()
    {
        var network = Answering(Credentials);

        await Editor(network).Save(
            Profile,
            new PublicProfileChange
            {
                Avatar = _directory.WriteFile("me.jpg", "the avatar"),
                Header = _directory.WriteFile("hills.webp", "the header"),
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(
            [("avatar", "me.jpg", "image/jpeg"), ("header", "hills.webp", "image/webp")],
            Parts(network));
        Assert.Contains("the avatar", network.Bodies[0]);
        Assert.Contains("the header", network.Bodies[0]);
    }

    /// <summary>
    ///     No type is refused before sending (ADR-0028): a file of a type this client cannot name still goes, for the
    ///     instance to judge.
    /// </summary>
    [Fact]
    public async Task Save_SendsAPictureOfATypeItCannotNameForTheInstanceToJudge()
    {
        var network = Answering(Credentials);

        await Editor(network).Save(
            Profile,
            new PublicProfileChange { Header = _directory.WriteFile("hills.tiff") },
            TestContext.Current.CancellationToken);

        Assert.Equal([("header", "hills.tiff", null)], Parts(network));
    }

    /// <summary>A path with no file behind it is said before anything is sent, rather than failing part way.</summary>
    [Fact]
    public async Task Save_SendsNothingForAPictureThatIsNotThere()
    {
        var network = Answering(Credentials);
        var missing = System.IO.Path.Combine(_directory.Path, "gone.png");

        var refused = await Assert.ThrowsAsync<PictureNotFoundException>(() =>
            Editor(network).Save(
                Profile,
                new PublicProfileChange { DisplayName = "Jeff M", Avatar = missing },
                TestContext.Current.CancellationToken));

        Assert.Equal(missing, refused.Path);
        Assert.Empty(network.Requests);
    }

    /// <summary>A picture refused — too large, or not a picture at all — is the instance's to word, as any refusal is.</summary>
    [Fact]
    public async Task Save_SaysARefusedPictureInTheInstancesOwnWords()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Refusal(
                HttpStatusCode.UnprocessableEntity,
                "Validation failed: Avatar file size must be less than 2 MB"));

        var refused = await Assert.ThrowsAsync<PublicProfileRefusedException>(() =>
            Editor(network).Save(
                Profile,
                new PublicProfileChange { Avatar = _directory.WriteFile("huge.png") },
                TestContext.Current.CancellationToken));

        Assert.Equal("Validation failed: Avatar file size must be less than 2 MB", refused.Reason);
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

    /// <summary>
    ///     Each part the one multipart request carried, by its name, the file name it went as, and its type, in order.
    ///     Read off the body as sent, since the parts themselves are disposed with the request.
    /// </summary>
    private static List<(string? Name, string? FileName, string? Type)> Parts(ScriptedHttpMessageHandler network)
    {
        var boundary = Assert.Single(network.Requests).Content?.Headers.ContentType?.Parameters
                              .Single(parameter => parameter.Name == "boundary").Value?.Trim('"');

        return network.Bodies.Single()
                      .Split($"--{boundary}")
                      .Select(part => part.Split("\r\n\r\n")[0].Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
                      .Where(headers => headers.Any(header => header.StartsWith("Content-Disposition:")))
                      .Select(headers =>
                      {
                          var disposition = ContentDispositionHeaderValue.Parse(Header(headers, "Content-Disposition")!);

                          return (
                              disposition.Name?.Trim('"'),
                              disposition.FileName?.Trim('"'),
                              Header(headers, "Content-Type") is { } type ? MediaTypeHeaderValue.Parse(type).MediaType : null);
                      })
                      .ToList();

        static string? Header(string[] headers, string name) =>
            headers.FirstOrDefault(header => header.StartsWith($"{name}:"))?[(name.Length + 1)..].Trim();
    }

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
