using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Wooly.Core;
using Wooly.Core.Errors;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Core;

/// <summary>
///     The adapter behind <see cref="IPostEngagement" />, tested where ADR-0005 puts an adapter: at the
///     <see cref="HttpMessageHandler" /> seam. Which of Mastodon's six endpoints a mark turns into is only observable
///     on the wire, and so is the one piece of reading this does — a boost comes back wrapped in a post of its own, and
///     unwrapping it is what makes <c>post boost 110</c> answer about post 110. Commands above this fake the port.
/// </summary>
public class PostEngagementTests
{
    private static readonly ActiveProfile Profile = new()
    {
        Name = "personal",
        Instance = "mastodon.social",
        Account = "jeff@mastodon.social",
        AccessToken = "token-personal",
    };

    [Theory]
    [InlineData(PostMark.Boost, true, "reblog")]
    [InlineData(PostMark.Boost, false, "unreblog")]
    [InlineData(PostMark.Favorite, true, "favourite")]
    [InlineData(PostMark.Favorite, false, "unfavourite")]
    [InlineData(PostMark.Pin, true, "pin")]
    [InlineData(PostMark.Pin, false, "unpin")]
    public async Task Mark_AsksTheInstanceForTheMarkWanted(PostMark mark, bool wanted, string endpoint)
    {
        var network = Answering(StatusJson("110"));

        await NewEngagement(network).Mark(Profile, "110", mark, wanted, TestContext.Current.CancellationToken);

        var request = Assert.Single(network.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal($"https://mastodon.social/api/v1/statuses/110/{endpoint}", request.RequestUri?.ToString());
        Assert.Equal("Bearer token-personal", request.Headers.Authorization?.ToString());
    }

    /// <summary>
    ///     Boosting answers with the boost — a post of its own, with its own id, carrying the post that was boosted. A
    ///     caller asked about post 110 and has to be answered about post 110, or <c>post boost 110 --json</c> would
    ///     report an id that is not the one anything else names that post by.
    /// </summary>
    [Fact]
    public async Task Mark_ReportsThePostThatWasBoostedRatherThanTheBoostCarryingIt()
    {
        var network = Answering(BoostJson("555", StatusJson("110", boosts: 4)));

        var post = await NewEngagement(network).Mark(
            Profile,
            "110",
            PostMark.Boost,
            wanted: true,
            TestContext.Current.CancellationToken);

        Assert.Equal("110", post.Id);
        Assert.Equal(4, post.Boosts);
        Assert.False(post.IsBoost);
    }

    /// <summary>Taking a boost back answers with the post itself, which is already the post the caller asked about.</summary>
    [Fact]
    public async Task Mark_ReportsThePostItselfWhereNothingWrapsIt()
    {
        var network = Answering(StatusJson("110", boosts: 3));

        var post = await NewEngagement(network).Mark(
            Profile,
            "110",
            PostMark.Boost,
            wanted: false,
            TestContext.Current.CancellationToken);

        Assert.Equal("110", post.Id);
        Assert.Equal(3, post.Boosts);
    }

    [Fact]
    public async Task Show_ReadsThePostTheIdNames()
    {
        var network = Answering(StatusJson("110"));

        var post = await NewEngagement(network).Show(Profile, "110", TestContext.Current.CancellationToken);

        var request = Assert.Single(network.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://mastodon.social/api/v1/statuses/110", request.RequestUri?.ToString());

        Assert.Equal("110", post.Id);
        Assert.Equal("jeff@mastodon.social", post.Account);
        Assert.Equal("Hello world", post.Content);
        Assert.Equal(PostVisibility.Public, post.Visibility);
        Assert.Equal("https://mastodon.social/@jeff/110", post.Url);
    }

    /// <summary>
    ///     A post asked for by id carries what a post on a timeline does, the link preview included — one crossing off
    ///     the wire is what keeps a field from being filled in on one screen and empty on the next (ADR-0018).
    /// </summary>
    [Fact]
    public async Task Show_ReportsTheLinkPreviewOnThePostTheIdNames()
    {
        var network = Answering(StatusJson("110", card: CardJson()));

        var post = await NewEngagement(network).Show(Profile, "110", TestContext.Current.CancellationToken);

        Assert.Equal("https://example.com/sheep", post.LinkPreview?.Url);
        Assert.Equal("The sheep of the world", post.LinkPreview?.Title);
        Assert.Equal("Example", post.LinkPreview?.ProviderName);
    }

    /// <summary>
    ///     A boost asked for by its own id stays a boost, unlike the one <see cref="IPostEngagement.Mark" /> unwraps: a
    ///     reader who named the boost is shown the boost, the same as a timeline shows it.
    /// </summary>
    [Fact]
    public async Task Show_ShowsABoostAsABoostOfThePostItCarries()
    {
        var network = Answering(BoostJson("555", StatusJson("110")));

        var post = await NewEngagement(network).Show(Profile, "555", TestContext.Current.CancellationToken);

        Assert.Equal("555", post.Id);
        Assert.True(post.IsBoost);
        Assert.Equal("110", post.Boosted?.Id);
    }

    /// <summary>
    ///     Mastodon serves what came before a post as well as what came after it, on one endpoint, and both halves are
    ///     drawn on the post screen — the ancestors above it and the answers below (#86).
    /// </summary>
    [Fact]
    public async Task Thread_ReadsWhatThePostAnswersAsWellAsWhatAnsweredIt()
    {
        var network = Answering($$"""
                                 {
                                   "ancestors": [{{StatusJson("100")}}, {{StatusJson("105")}}],
                                   "descendants": [{{StatusJson("111")}}, {{StatusJson("112")}}]
                                 }
                                 """);

        var thread = await NewEngagement(network).Thread(Profile, "110", TestContext.Current.CancellationToken);

        var request = Assert.Single(network.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://mastodon.social/api/v1/statuses/110/context", request.RequestUri?.ToString());

        Assert.Equal(["100", "105"], thread.Ancestors.Select(ancestor => ancestor.Id));
        Assert.Equal(["111", "112"], thread.Replies.Select(reply => reply.Id));
    }

    /// <summary>
    ///     The whole chain back to the root, uncapped: a reader who opened the fifth post in a thread is shown the four
    ///     above it, not just the one it answers (#86).
    /// </summary>
    [Fact]
    public async Task Thread_ReadsTheWholeAncestorChainRatherThanTheNearestOne()
    {
        var ancestors = string.Join(", ", new[] { "100", "101", "102", "103" }.Select(id => StatusJson(id)));
        var network = Answering($$"""{"ancestors": [{{ancestors}}], "descendants": []}""");

        var thread = await NewEngagement(network).Thread(Profile, "110", TestContext.Current.CancellationToken);

        Assert.Equal(["100", "101", "102", "103"], thread.Ancestors.Select(ancestor => ancestor.Id));
    }

    /// <summary>
    ///     A post standing on its own has empty lists at both ends, which are not holes for a caller to check for.
    /// </summary>
    [Fact]
    public async Task Thread_ReportsNothingWhereNobodyHasAnsweredThePostAndItAnswersNothing()
    {
        var network = Answering("""{"ancestors": [], "descendants": []}""");

        var thread = await NewEngagement(network).Thread(Profile, "110", TestContext.Current.CancellationToken);

        Assert.Empty(thread.Ancestors);
        Assert.Empty(thread.Replies);
    }

    /// <summary>
    ///     Mastodon votes on the poll rather than on the post carrying it, so what goes on the wire is the poll's own
    ///     id and the indices of the options chosen — neither of which is observable anywhere but here.
    /// </summary>
    [Fact]
    public async Task Vote_CastsTheChosenOptionsOnThePollThePostCarries()
    {
        var network = Answering(PollJson("7", [4, 7], ownVotes: [1]));
        var post = APost.With(poll: APost.APoll(id: "7"));

        await NewEngagement(network).Vote(Profile, post, [1], TestContext.Current.CancellationToken);

        var request = Assert.Single(network.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://mastodon.social/api/v1/polls/7/votes", request.RequestUri?.ToString());
        Assert.Equal("Bearer token-personal", request.Headers.Authorization?.ToString());

        Assert.Contains("1", Choices(network));
    }

    /// <summary>A poll that lets a voter choose several sends all of them on the one call.</summary>
    [Fact]
    public async Task Vote_CastsEveryChosenOptionOnAMultipleChoicePoll()
    {
        var network = Answering(PollJson("7", [4, 7, 2], ownVotes: [0, 2]));
        var post = APost.With(poll: APost.APoll(id: "7", multipleChoice: true));

        await NewEngagement(network).Vote(Profile, post, [0, 2], TestContext.Current.CancellationToken);

        Assert.Equal(["0", "2"], Choices(network));
    }

    /// <summary>
    ///     The vote endpoint answers with the whole poll as it now stands, so the post the caller was already holding
    ///     is brought up to date from that answer — one call, and no second read of the post around it.
    /// </summary>
    [Fact]
    public async Task Vote_AnswersWithTheCallersOwnPostCarryingThePollAsItNowStands()
    {
        var network = Answering(PollJson("7", [4, 7], ownVotes: [1]));
        var post = APost.With(id: "110", poll: APost.APoll(id: "7", voted: false));

        var voted = await NewEngagement(network).Vote(Profile, post, [1], TestContext.Current.CancellationToken);

        Assert.Single(network.Requests);

        Assert.Equal("110", voted.Id);
        Assert.Equal("Hello world", voted.Content);

        Assert.Equal(11, voted.Poll?.Votes);
        Assert.Equal([4, 7], voted.Poll?.Options.Select(option => option.Votes));
        Assert.Equal([false, true], voted.Poll?.Options.Select(option => option.Picked));
        Assert.True(voted.Poll?.Voted);
    }

    /// <summary>
    ///     A vote's answer is the poll alone, its custom emoji list with it, and the options grafted onto the post keep
    ///     the emoji they are written with (ADR-0035).
    /// </summary>
    [Fact]
    public async Task Vote_KeepsTheCustomEmojiThePollListsOnItsOptions()
    {
        var network = Answering(PollJson(
            "7",
            [4, 7],
            ownVotes: [1],
            emojis: WireEmojis.Json(("cat", "https://files.example/emoji/cat.gif"))));
        var post = APost.With(id: "110", poll: APost.APoll(id: "7", voted: false));

        var voted = await NewEngagement(network).Vote(Profile, post, [1], TestContext.Current.CancellationToken);

        Assert.All(
            voted.Poll!.Options,
            option => Assert.Equal(
                [new CustomEmoji("cat", "https://files.example/emoji/cat-static.png")],
                option.CustomEmoji));
    }

    /// <summary>
    ///     An instance refuses a second vote outright rather than replacing the first, and that refusal is something
    ///     the reader is told rather than something this client falls over on.
    /// </summary>
    [Fact]
    public async Task Vote_ReportsTheInstancesRefusalInItsOwnWords()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Refusal(HttpStatusCode.UnprocessableEntity, "You have already voted on this poll"));

        var post = APost.With(poll: APost.APoll(id: "7"));

        var refusal = await Assert.ThrowsAsync<VoteRefusedException>(
            () => NewEngagement(network).Vote(Profile, post, [1], TestContext.Current.CancellationToken));

        Assert.Contains("already voted", refusal.Message);
    }

    /// <summary>
    ///     A post's <b>Actors</b> are two lists the instance keeps apart, and each side is read from its own endpoint,
    ///     a page of the most accounts Mastodon serves at a time (#487).
    /// </summary>
    [Theory]
    [InlineData(ActorSide.Favorited, "favourited_by")]
    [InlineData(ActorSide.Boosted, "reblogged_by")]
    public async Task ReadActors_AsksTheEndpointForTheSide(ActorSide side, string endpoint)
    {
        var network = new ScriptedHttpMessageHandler(Page(Accounts(AccountJson("alice", "7")), nextPageAt: null));

        var fetch = await NewEngagement(network).ReadActors(
            Profile,
            "110",
            side,
            80,
            TestContext.Current.CancellationToken);

        var request = Assert.Single(network.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal($"https://mastodon.social/api/v1/statuses/110/{endpoint}?limit=80", request.RequestUri?.ToString());
        var alice = Assert.Single(fetch.Items);
        Assert.Equal("7", alice.Id);
        Assert.Equal("alice@mastodon.social", alice.Address);
        Assert.True(fetch.IsWhole);
    }

    /// <summary>
    ///     Paged by the instance's link, which names the next page in the id space of the favorite or boost rather than
    ///     of the account — so more than a page is read by following it, and nothing is guessed from an account's id.
    /// </summary>
    [Fact]
    public async Task ReadActors_FollowsTheInstancesLinkToTheNextPage()
    {
        var network = new ScriptedHttpMessageHandler(
            Page(FullPage(), nextPageAt: "4801"),
            Page(Accounts(AccountJson("last", "999")), nextPageAt: null));

        var fetch = await NewEngagement(network).ReadActors(
            Profile,
            "110",
            ActorSide.Favorited,
            100,
            TestContext.Current.CancellationToken);

        Assert.Equal(81, fetch.Items.Count);
        Assert.Contains("max_id=4801", network.Requests[1].RequestUri?.Query);
        Assert.True(fetch.IsWhole);
    }

    /// <summary>Where the instance names no next page the list is whole, however full the page it sent.</summary>
    [Fact]
    public async Task ReadActors_EndsWhereTheInstanceNamesNoNextPage()
    {
        var network = new ScriptedHttpMessageHandler(
            Page(FullPage(), nextPageAt: null),
            Page(Accounts(AccountJson("never-read", "999")), nextPageAt: null));

        var fetch = await NewEngagement(network).ReadActors(
            Profile,
            "110",
            ActorSide.Boosted,
            100,
            TestContext.Current.CancellationToken);

        Assert.Equal(80, fetch.Items.Count);
        Assert.True(fetch.IsWhole);
        Assert.Single(network.Requests);
    }

    /// <summary>
    ///     Reading on (#467): a read that stopped at what was asked for says where to read on from, and handing that back
    ///     asks the same side of the same post for the page the instance named next.
    /// </summary>
    [Fact]
    public async Task ReadActorsOn_AsksForThePageTheLastReadNamedNext()
    {
        var network = new ScriptedHttpMessageHandler(
            Page(FullPage(), nextPageAt: "4801"),
            Page(Accounts(AccountJson("last", "999")), nextPageAt: null));
        var engagement = NewEngagement(network);
        var token = TestContext.Current.CancellationToken;

        var first = await engagement.ReadActors(Profile, "110", ActorSide.Boosted, 80, token);
        var onward = await engagement.ReadActorsOn(Profile, "110", ActorSide.Boosted, first.Onward!, 80, token);

        Assert.False(first.IsWhole);
        Assert.Equal(
            "https://mastodon.social/api/v1/statuses/110/reblogged_by?max_id=4801&limit=80",
            network.Requests[1].RequestUri?.ToString());
        Assert.Equal("999", Assert.Single(onward.Items).Id);
        Assert.True(onward.IsWhole);
    }

    private static string Accounts(params string[] accounts) => $"[{string.Join(",", accounts)}]";

    /// <summary>A page as full as the endpoint serves.</summary>
    private static string FullPage() =>
        Accounts([.. Enumerable.Range(1, 80).Select(id => AccountJson($"a{id}", $"{id}"))]);

    /// <summary>One page of a post's actors, with the link header an instance names the next page in.</summary>
    /// <param name="nextPageAt">
    ///     Where the next page starts, in the id space the endpoint pages by — a favorite's or a boost's, nothing like
    ///     any account id on the page — or <see langword="null" /> for the last page.
    /// </param>
    private static Func<HttpRequestMessage, HttpResponseMessage> Page(string json, string? nextPageAt) =>
        _ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };

            if (nextPageAt is not null)
            {
                response.Headers.TryAddWithoutValidation(
                    "Link",
                    $"""<https://mastodon.social/api/v1/statuses/110/favourited_by?max_id={nextPageAt}>; rel="next" """);
            }

            return response;
        };

    private static string AccountJson(string account, string id) =>
        $$"""
          {
            "id": "{{id}}",
            "username": "{{account}}",
            "acct": "{{account}}",
            "display_name": "Alice",
            "url": "https://mastodon.social/@{{account}}",
            "note": "",
            "avatar": "https://mastodon.social/avatars/default.png",
            "locked": false,
            "bot": false,
            "fields": [],
            "created_at": "2020-01-01T00:00:00.000Z",
            "followers_count": 1,
            "following_count": 1,
            "statuses_count": 1
          }
          """;

    /// <summary>What each request asked to be voted for, in the order the choices were given.</summary>
    private static IReadOnlyList<string> Choices(ScriptedHttpMessageHandler network) =>
    [
        .. network.Bodies
                  .SelectMany(body => body.Split('&'))
                  .Select(field => field.Split('='))
                  .Where(field => field[0].StartsWith("choices", StringComparison.Ordinal))
                  .Select(field => field[1]),
    ];

    private static ScriptedHttpMessageHandler Answering(string json) =>
        new(ScriptedHttpMessageHandler.Json(json));

    /// <summary>Resolved from the container the app builds, so the wiring is under test alongside the behavior.</summary>
    private static IPostEngagement NewEngagement(HttpMessageHandler network)
    {
        var services = new ServiceCollection();
        services.AddWoolyCore();
        services.AddHttpClient(WoolyClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => network);

        return services.BuildServiceProvider().GetRequiredService<IPostEngagement>();
    }

    /// <summary>What an instance answers a boost with: a post of the booster's own, carrying the post boosted.</summary>
    private static string BoostJson(string id, string boosted) => StatusJson(id, boosted: boosted);

    /// <summary>
    ///     What the instance made of a link in a post's text, as far as this file needs it: that a post asked for by id
    ///     carries one at all. What is kept and what is dropped is settled where the mapping is covered in full, in
    ///     <see cref="TimelineReaderTests" />.
    /// </summary>
    private static string CardJson() =>
        """
        {
          "url": "https://example.com/sheep",
          "title": "The sheep of the world",
          "description": "A field guide to every breed.",
          "type": "link",
          "author_name": "Maria",
          "provider_name": "Example",
          "html": "",
          "image": "https://example.com/sheep.png",
          "embed_url": ""
        }
        """;

    /// <summary>What an instance answers a vote with: the whole poll as it now stands, and nothing of the post on it.</summary>
    /// <param name="emojis">The custom emoji the poll lists, as <see cref="WireEmojis.Json" /> writes them.</param>
    private static string PollJson(
        string id,
        IReadOnlyList<long> votes,
        IReadOnlyList<int> ownVotes,
        string emojis = "[]")
    {
        var options = votes.Select((count, at) => $$"""{"title": "Option {{at}}", "votes_count": {{count}}}""");

        return $$"""
                 {
                   "id": "{{id}}",
                   "expires_at": null,
                   "expired": false,
                   "multiple": false,
                   "votes_count": {{votes.Sum()}},
                   "voters_count": null,
                   "voted": true,
                   "own_votes": [{{string.Join(", ", ownVotes)}}],
                   "options": [{{string.Join(", ", options)}}],
                   "emojis": {{emojis}}
                 }
                 """;
    }

    /// <param name="card">The wire's <c>card</c>, or <see langword="null" /> for a post the instance previewed no link on.</param>
    private static string StatusJson(string id, long boosts = 0, string? boosted = null, string? card = null) =>
        $$"""
          {
            "id": "{{id}}",
            "card": {{card ?? "null"}},
            "uri": "https://mastodon.social/users/jeff/statuses/{{id}}",
            "url": "https://mastodon.social/@jeff/{{id}}",
            "created_at": "2026-07-29T12:00:00.000Z",
            "account": { "id": "1", "username": "jeff", "acct": "jeff", "display_name": "Jeff" },
            "content": "<p>Hello world</p>",
            "spoiler_text": "",
            "sensitive": false,
            "visibility": "public",
            "reblogs_count": {{boosts}},
            "favourites_count": 0,
            "replies_count": 0,
            "media_attachments": [],
            "reblog": {{boosted ?? "null"}},
            "poll": null
          }
          """;
}
