using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Wooly.Core;
using Wooly.Core.Accounts;
using Wooly.Core.Conversations;
using Wooly.Core.Errors;
using Wooly.Core.Profiles;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Core;

/// <summary>
///     <see cref="DirectMessages" /> is the adapter behind <see cref="IDirectMessages" />, and ADR-0005 puts an
///     adapter's tests at the <see cref="HttpMessageHandler" /> seam: what it does is turn what Mastonet deserialized
///     into conversations, which is not observable without a payload. Two things are only visible here — that showing
///     one conversation means walking the list until its id turns up, because Mastodon serves no single conversation by
///     id, and that the thread itself comes from the context of the last post in it. Commands above this fake
///     <see cref="IDirectMessages" />.
/// </summary>
public class DirectMessagesTests
{
    private static readonly ActiveProfile Profile = new()
    {
        Name = "personal",
        Instance = "mastodon.social",
        Account = "jeff@mastodon.social",
        AccessToken = "token-personal",
    };

    [Fact]
    public async Task List_ReportsTheConversationsTheProfileIsIn()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Json(Page(ConversationJson("7"))));

        var fetch = await NewMessages(network).List(Profile, 20, TestContext.Current.CancellationToken);

        var conversation = Assert.Single(fetch.Conversations);
        Assert.Equal("7", conversation.Id);
        Assert.Equal(["alice@hachyderm.io"], conversation.With);
        Assert.True(conversation.Unread);
        Assert.Equal("110", conversation.Latest?.Id);
    }

    [Fact]
    public async Task List_AsksTheInstanceForTheProfilesConversations()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Json(Page(ConversationJson("7"))));

        await NewMessages(network).List(Profile, 20, TestContext.Current.CancellationToken);

        var request = Assert.Single(network.Requests);
        Assert.Equal("https://mastodon.social/api/v1/conversations?limit=20", request.RequestUri?.ToString());
        Assert.Equal("Bearer token-personal", request.Headers.Authorization?.ToString());
    }

    /// <summary>
    ///     An account on this instance is named by the wire by bare username and one anywhere else in full. A listing
    ///     mixes them, and two lines side by side may not say who they are with in two different ways.
    /// </summary>
    [Fact]
    public async Task List_NamesEveryAccountByTheInstanceItIsOn()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Json(Page(ConversationJson("7", accounts: ["alice@hachyderm.io", "bob"]))));

        var fetch = await NewMessages(network).List(Profile, 20, TestContext.Current.CancellationToken);

        Assert.Equal(["alice@hachyderm.io", "bob@mastodon.social"], Assert.Single(fetch.Conversations).With);
    }

    /// <summary>
    ///     A conversation whose posts have all been taken down is still one the account is in, and still one that can be
    ///     marked read. Dropped from the listing, it would be a thread nothing could name.
    /// </summary>
    [Fact]
    public async Task List_KeepsAConversationWithNothingLeftInIt()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Json(Page(ConversationJson("7", latest: "null"))));

        var fetch = await NewMessages(network).List(Profile, 20, TestContext.Current.CancellationToken);

        var conversation = Assert.Single(fetch.Conversations);
        Assert.Equal("7", conversation.Id);
        Assert.Null(conversation.Latest);
    }

    /// <summary>
    ///     The same paging a timeline and an inbox get, because it is the same loop (ADR-0007) — including asking for no
    ///     more in one call than the endpoint serves.
    /// </summary>
    [Fact]
    public async Task List_PagesUntilItHasAsManyConversationsAsWereAskedFor()
    {
        var network = new ScriptedHttpMessageHandler(
            PageResponse(PageOf(count: 40, firstId: 200), nextMaxId: "150"),
            PageResponse(PageOf(count: 5, firstId: 149)));

        var fetch = await NewMessages(network).List(Profile, 45, TestContext.Current.CancellationToken);

        Assert.Equal(45, fetch.Conversations.Count);
        Assert.True(fetch.IsComplete);
        Assert.Equal("https://mastodon.social/api/v1/conversations?limit=40", network.Requests[0].RequestUri?.ToString());
        Assert.Equal(
            "https://mastodon.social/api/v1/conversations?max_id=150&limit=5",
            network.Requests[1].RequestUri?.ToString());
    }

    /// <summary>
    ///     ADR-0007's second decision, inherited: a limit hit part way through loses none of what already arrived, and
    ///     the fetch says it was cut short so nobody reports an empty inbox the user does not have.
    /// </summary>
    [Fact]
    public async Task List_StopsOnARateLimitAndKeepsTheConversationsItAlreadyHad()
    {
        var network = new ScriptedHttpMessageHandler(
            PageResponse(PageOf(count: 40, firstId: 200), nextMaxId: "160"),
            ScriptedHttpMessageHandler.Status(HttpStatusCode.TooManyRequests));

        var fetch = await NewMessages(network).List(Profile, 45, TestContext.Current.CancellationToken);

        Assert.Equal(40, fetch.Conversations.Count);
        Assert.False(fetch.IsComplete);
        Assert.Equal("mastodon.social", fetch.StoppedBy?.Instance);
        Assert.Equal(2, network.Requests.Count);
    }

    /// <summary>
    ///     Mastodon serves no single conversation by id, so the only way to one is down the list — and the thread in it
    ///     comes from the context of its last post, which is the only post a listing carries.
    /// </summary>
    [Fact]
    public async Task Show_ReadsTheConversationTheIdNamesAndEverythingSaidInIt()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Json(Page(ConversationJson("7", latest: PostJson("110")))),
            ScriptedHttpMessageHandler.Json(ContextJson(ancestors: [PostJson("108")], descendants: [PostJson("112")])));

        var thread = await NewMessages(network).Show(Profile, "7", TestContext.Current.CancellationToken);

        Assert.Equal("7", thread.Conversation.Id);
        Assert.Equal(["alice@hachyderm.io"], thread.Conversation.With);

        // Oldest first: a reply printed above the thing it answers is the one order a conversation cannot be read in.
        Assert.Equal(["108", "110", "112"], thread.Posts.Select(post => post.Id));

        Assert.Equal(
            "https://mastodon.social/api/v1/statuses/110/context",
            network.Requests[1].RequestUri?.ToString());
    }

    /// <summary>
    ///     The walk stops at the page the wanted conversation is on. Asking for the caller's whole search ceiling and
    ///     looking through it afterwards would spend five calls to find something on the first page.
    /// </summary>
    [Fact]
    public async Task Show_AsksForNoPagesBeyondTheOneTheConversationIsOn()
    {
        var network = new ScriptedHttpMessageHandler(
            PageResponse(PageOf(count: 40, firstId: 200), nextMaxId: "160"),
            ScriptedHttpMessageHandler.Json(ContextJson()));

        await NewMessages(network).Show(Profile, "180", TestContext.Current.CancellationToken);

        Assert.Equal(2, network.Requests.Count);
        Assert.Equal("https://mastodon.social/api/v1/conversations?limit=40", network.Requests[0].RequestUri?.ToString());
        Assert.EndsWith("/context", network.Requests[1].RequestUri?.ToString());
    }

    /// <summary>A conversation further down than the first page is still reachable, at the cost of the pages between.</summary>
    [Fact]
    public async Task Show_KeepsWalkingTheListUntilTheIdTurnsUp()
    {
        var network = new ScriptedHttpMessageHandler(
            PageResponse(PageOf(count: 40, firstId: 200), nextMaxId: "160"),
            PageResponse(PageOf(count: 40, firstId: 159), nextMaxId: "119"),
            ScriptedHttpMessageHandler.Json(ContextJson()));

        var thread = await NewMessages(network).Show(Profile, "140", TestContext.Current.CancellationToken);

        Assert.Equal("140", thread.Conversation.Id);
        Assert.Equal(3, network.Requests.Count);
    }

    [Fact]
    public async Task Show_RefusesAnIdNoConversationInTheListCarries()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json(Page(ConversationJson("7"))));

        var refusal = await Assert.ThrowsAsync<UnknownConversationException>(
            () => NewMessages(network).Show(Profile, "9", TestContext.Current.CancellationToken));

        Assert.Equal("9", refusal.ConversationId);
        Assert.Contains("9", refusal.Message);
    }

    /// <summary>
    ///     A rate limit part way down the list is not "no such conversation": telling a user their id is wrong when what
    ///     happened is that the looking stopped would send them checking a value that was right all along.
    /// </summary>
    [Fact]
    public async Task Show_ReportsARateLimitThatStoppedTheSearchAsItself()
    {
        var network = new ScriptedHttpMessageHandler(
            PageResponse(PageOf(count: 40, firstId: 200), nextMaxId: "160"),
            ScriptedHttpMessageHandler.Status(HttpStatusCode.TooManyRequests));

        await Assert.ThrowsAsync<RateLimitedException>(
            () => NewMessages(network).Show(Profile, "9", TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     A conversation can hold more than one thread — an instance groups it by who is in it, not by what answers
    ///     what — and only the one the last post is in comes back. Pinned rather than left to chance: what is shown is
    ///     that post and its context, so a lone root shows as one post rather than as everything ever said to the
    ///     account (ADR-0013).
    /// </summary>
    [Fact]
    public async Task Show_ReadsOnlyTheThreadTheLastPostIsIn()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Json(Page(ConversationJson("7", latest: PostJson("110")))),
            ScriptedHttpMessageHandler.Json(ContextJson()));

        var thread = await NewMessages(network).Show(Profile, "7", TestContext.Current.CancellationToken);

        Assert.Equal(["110"], thread.Posts.Select(post => post.Id));
    }

    /// <summary>There is no post to ask the context of, so no call is made and the thread is empty.</summary>
    [Fact]
    public async Task Show_ReadsAConversationWithNothingLeftInItWithoutAskingForAThread()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Json(Page(ConversationJson("7", latest: "null"))));

        var thread = await NewMessages(network).Show(Profile, "7", TestContext.Current.CancellationToken);

        Assert.Empty(thread.Posts);
        Assert.Single(network.Requests);
    }

    /// <summary>
    ///     Reading a conversation leaves its unread mark exactly as it found it. A client that cleared it on the way
    ///     past would make "what have I not read" unanswerable for anything that looked afterwards.
    /// </summary>
    [Fact]
    public async Task Show_LeavesTheConversationUnread()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Json(Page(ConversationJson("7", unread: true))),
            ScriptedHttpMessageHandler.Json(ContextJson()));

        var thread = await NewMessages(network).Show(Profile, "7", TestContext.Current.CancellationToken);

        Assert.True(thread.Conversation.Unread);
        Assert.DoesNotContain(network.Requests, request => request.RequestUri!.ToString().EndsWith("/read"));
    }

    /// <summary>
    ///     Named by the conversation's own id and reached without walking the list first: marking one read needs
    ///     nothing about it except that the instance knows the id.
    /// </summary>
    [Fact]
    public async Task MarkRead_ClearsTheUnreadMarkOnTheConversationItWasNamed()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Json(ConversationJson("7", unread: false)));

        var conversation = await NewMessages(network).MarkRead(Profile, "7", TestContext.Current.CancellationToken);

        var request = Assert.Single(network.Requests);
        Assert.Equal("https://mastodon.social/api/v1/conversations/7/read", request.RequestUri?.ToString());
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("Bearer token-personal", request.Headers.Authorization?.ToString());

        Assert.Equal("7", conversation.Id);
        Assert.False(conversation.Unread);
    }

    /// <summary>
    ///     A message to a conversation with several accounts in it names every one of them, because Mastodon delivers
    ///     a direct post to the accounts its text mentions and one that named only the last speaker would drop the
    ///     rest of the conversation out of it.
    /// </summary>
    [Fact]
    public void To_NamesEveryAccountAMessageIsFor()
    {
        AccountAddress[] both =
        [
            AccountAddress.Parse("alice@hachyderm.io"),
            AccountAddress.Parse("ben@fosstodon.org"),
        ];

        Assert.Equal("@alice@hachyderm.io @ben@fosstodon.org Both of you then", DirectMessage.To(both, "Both of you then"));

        // The mentions alone, for a message carrying files and no words — a mention with a space after it is not what
        // gets sent.
        Assert.Equal("@alice@hachyderm.io @ben@fosstodon.org", DirectMessage.To(both, "  "));
    }

    /// <summary>Resolved from the container the app builds, so the wiring is under test alongside the behavior.</summary>
    private static IDirectMessages NewMessages(HttpMessageHandler network)
    {
        var services = new ServiceCollection();
        services.AddWoolyCore();
        services.AddHttpClient(WoolyClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => network);

        return services.BuildServiceProvider().GetRequiredService<IDirectMessages>();
    }

    private static string Page(params string[] conversations) => $"[{string.Join(",", conversations)}]";

    /// <summary>A page of <paramref name="count" /> conversations with descending ids, the way a listing comes back.</summary>
    private static string PageOf(int count, int firstId) =>
        Page(Enumerable.Range(0, count).Select(offset => ConversationJson($"{firstId - offset}")).ToArray());

    /// <param name="nextMaxId">
    ///     What the instance names as the start of the next page, in the link header it sends alongside a list — or
    ///     <see langword="null" /> for an instance that sends none.
    /// </param>
    private static Func<HttpRequestMessage, HttpResponseMessage> PageResponse(string json, string? nextMaxId = null) =>
        _ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };

            if (nextMaxId is not null)
            {
                response.Headers.Add(
                    "Link",
                    $"<https://mastodon.social/api/v1/conversations?max_id={nextMaxId}>; rel=\"next\"");
            }

            return response;
        };

    /// <param name="accounts">
    ///     The wire's <c>acct</c> for each account in it: bare for one on the instance being read,
    ///     <c>username@instance</c> for one anywhere else.
    /// </param>
    /// <param name="latest">
    ///     The wire's <c>last_status</c>, which is <c>null</c> for a conversation whose posts have all been taken down.
    /// </param>
    private static string ConversationJson(
        string id,
        string[]? accounts = null,
        bool unread = true,
        string? latest = null) =>
        $$"""
          {
            "id": "{{id}}",
            "unread": {{(unread ? "true" : "false")}},
            "accounts": [{{string.Join(",", (accounts ?? ["alice@hachyderm.io"]).Select(AccountJson))}}],
            "last_status": {{latest ?? PostJson("110")}}
          }
          """;

    private static string ContextJson(string[]? ancestors = null, string[]? descendants = null) =>
        $$"""
          {
            "ancestors": [{{string.Join(",", ancestors ?? [])}}],
            "descendants": [{{string.Join(",", descendants ?? [])}}]
          }
          """;

    private static string AccountJson(string account) =>
        $$"""
          {
            "id": "2",
            "username": "{{account.Split('@')[0]}}",
            "acct": "{{account}}",
            "display_name": "{{char.ToUpperInvariant(account[0]) + account.Split('@')[0][1..]}}"
          }
          """;

    private static string PostJson(string id) =>
        $$"""
          {
            "id": "{{id}}",
            "uri": "https://mastodon.social/users/jeff/statuses/{{id}}",
            "url": "https://mastodon.social/@jeff/{{id}}",
            "created_at": "2026-07-29T12:00:00.000Z",
            "account": {
              "id": "1",
              "username": "jeff",
              "acct": "jeff",
              "display_name": "Jeff"
            },
            "content": "<p>Hello world</p>",
            "spoiler_text": "",
            "visibility": "direct",
            "reblogs_count": 0,
            "favourites_count": 0,
            "replies_count": 1,
            "reblog": null
          }
          """;
}
