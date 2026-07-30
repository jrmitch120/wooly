using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Wooly.Core;
using Wooly.Core.Notifications;
using Wooly.Core.Profiles;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Core;

/// <summary>
///     The inbox is the adapter behind <see cref="INotificationInbox" />, and ADR-0005 puts an adapter's tests at the
///     <see cref="HttpMessageHandler" /> seam: what it does is turn what Mastonet deserialized into notifications, so
///     there has to be a payload for the mapping to be observable at all. The same goes for what it asks of an instance —
///     which endpoint a dismissal reaches, and the <c>max_id</c> a second page carries — and for the rate limit it stops
///     on, which is raised below <c>IMastodonClient</c>. Commands above this fake <see cref="INotificationInbox" />.
/// </summary>
public class NotificationInboxTests
{
    private static readonly ActiveProfile Profile = new()
    {
        Name = "personal",
        Instance = "mastodon.social",
        Account = "jeff@mastodon.social",
        AccessToken = "token-personal",
    };

    [Fact]
    public async Task Read_ReportsTheNotificationsWaitingForTheAccount()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Json(Page(NotificationJson("34"))));

        var fetch = await NewInbox(network).Read(Profile, 20, TestContext.Current.CancellationToken);

        var notification = Assert.Single(fetch.Notifications);
        Assert.Equal("34", notification.Id);
        Assert.Equal("alice@hachyderm.io", notification.Account);
        Assert.Equal("Alice", notification.Author);
        Assert.Equal(new DateTimeOffset(2026, 7, 29, 12, 4, 0, TimeSpan.Zero), notification.ReceivedAt);
    }

    /// <summary>
    ///     CONTEXT.md's vocabulary is the point of this layer: what the wire calls a <c>reblog</c> and a
    ///     <c>favourite</c> reaches anything above here as a boost and a favorite.
    /// </summary>
    [Theory]
    [InlineData("mention", "mention")]
    [InlineData("follow", "follow")]
    [InlineData("reblog", "boost")]
    [InlineData("favourite", "favorite")]
    public async Task Read_NamesTheFourKindsItKnowsInThisProjectsVocabulary(string wire, string expected)
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Json(Page(NotificationJson("34", type: wire))));

        var fetch = await NewInbox(network).Read(Profile, 20, TestContext.Current.CancellationToken);

        Assert.Equal(expected, Assert.Single(fetch.Notifications).Kind.Name);
    }

    /// <summary>
    ///     Mastodon keeps adding kinds, and one this client has never heard of is still something the account was
    ///     notified about. Dropping it would leave a notification that cannot be read, and therefore cannot be
    ///     dismissed by id either — so it comes through under the instance's own word for it.
    /// </summary>
    [Theory]
    [InlineData("poll")]
    [InlineData("update")]
    [InlineData("admin.report")]
    public async Task Read_KeepsAKindItHasNoWordForUnderTheInstancesOwnWord(string type)
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Json(Page(NotificationJson("34", type: type))));

        var fetch = await NewInbox(network).Read(Profile, 20, TestContext.Current.CancellationToken);

        Assert.Equal(type, Assert.Single(fetch.Notifications).Kind.Name);
    }

    /// <summary>
    ///     A follow request is somebody asking to follow, which is not the same as following. Reported as a follow, it
    ///     would tell an account it has a follower it does not have.
    /// </summary>
    [Fact]
    public async Task Read_DoesNotReportARequestToFollowAsAFollow()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Json(Page(NotificationJson("34", type: "follow_request"))));

        var fetch = await NewInbox(network).Read(Profile, 20, TestContext.Current.CancellationToken);

        Assert.NotEqual(NotificationKind.Follow, Assert.Single(fetch.Notifications).Kind);
    }

    /// <summary>
    ///     A mention is only worth reading with the post in it, and it is the same post a timeline would show — mapped
    ///     once, so the two cannot describe it differently.
    /// </summary>
    [Fact]
    public async Task Read_CarriesThePostANotificationIsAbout()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Json(Page(NotificationJson("34", post: PostJson("110")))));

        var fetch = await NewInbox(network).Read(Profile, 20, TestContext.Current.CancellationToken);

        var post = Assert.Single(fetch.Notifications).Post;
        Assert.Equal("110", post?.Id);
        Assert.Equal("Hello world", post?.Content);
        Assert.Equal("jeff@mastodon.social", post?.Account);
    }

    /// <summary>A follow is somebody arriving rather than something they wrote, and has no post to carry.</summary>
    [Fact]
    public async Task Read_ReportsAFollowAsANotificationWithNoPostBehindIt()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Json(Page(NotificationJson("34", type: "follow"))));

        var fetch = await NewInbox(network).Read(Profile, 20, TestContext.Current.CancellationToken);

        Assert.Null(Assert.Single(fetch.Notifications).Post);
    }

    /// <summary>
    ///     An account on this instance is named by the wire by bare username and one anywhere else in full. A list
    ///     mixes them, and two lines side by side may not say who they are about in two different ways.
    /// </summary>
    [Fact]
    public async Task Read_NamesEveryAccountByTheInstanceItIsOn()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json(Page(
            NotificationJson("34"),
            NotificationJson("33", account: "bob"))));

        var fetch = await NewInbox(network).Read(Profile, 20, TestContext.Current.CancellationToken);

        Assert.Equal(
            ["alice@hachyderm.io", "bob@mastodon.social"],
            fetch.Notifications.Select(notification => notification.Account));
    }

    [Fact]
    public async Task Read_AsksTheInstanceForTheAccountsNotifications()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Json(Page(NotificationJson("34"))));

        await NewInbox(network).Read(Profile, 20, TestContext.Current.CancellationToken);

        var request = Assert.Single(network.Requests);
        Assert.Equal("https://mastodon.social/api/v1/notifications?limit=20", request.RequestUri?.ToString());
        Assert.Equal("Bearer token-personal", request.Headers.Authorization?.ToString());
    }

    /// <summary>
    ///     An instance serves at most a page at a time, so more notifications than that is more than one call — the same
    ///     paging a timeline gets, because it is the same loop (ADR-0007).
    /// </summary>
    [Fact]
    public async Task Read_PagesUntilItHasAsManyNotificationsAsWereAskedFor()
    {
        var network = new ScriptedHttpMessageHandler(
            PageResponse(PageOf(count: 40, firstId: 200), nextMaxId: "150"),
            PageResponse(PageOf(count: 5, firstId: 149)));

        var fetch = await NewInbox(network).Read(Profile, 45, TestContext.Current.CancellationToken);

        Assert.Equal(45, fetch.Notifications.Count);
        Assert.True(fetch.IsComplete);
        Assert.Equal(2, network.Requests.Count);
        Assert.Equal("https://mastodon.social/api/v1/notifications?limit=40", network.Requests[0].RequestUri?.ToString());
        Assert.Equal(
            "https://mastodon.social/api/v1/notifications?max_id=150&limit=5",
            network.Requests[1].RequestUri?.ToString());
    }

    [Fact]
    public async Task Read_StopsWhenTheNotificationsRunOutBeforeTheLimit()
    {
        var network = new ScriptedHttpMessageHandler(PageResponse(PageOf(count: 3, firstId: 200)));

        var fetch = await NewInbox(network).Read(Profile, 45, TestContext.Current.CancellationToken);

        Assert.Equal(3, fetch.Notifications.Count);
        Assert.True(fetch.IsComplete);
        Assert.Single(network.Requests);
    }

    /// <summary>
    ///     ADR-0007's second decision, inherited: a limit hit part way through loses none of what already arrived, and
    ///     the fetch says it was cut short so nobody reports an empty inbox the user does not have.
    /// </summary>
    [Fact]
    public async Task Read_StopsOnARateLimitAndKeepsTheNotificationsItAlreadyHad()
    {
        var network = new ScriptedHttpMessageHandler(
            PageResponse(PageOf(count: 40, firstId: 200), nextMaxId: "161"),
            ScriptedHttpMessageHandler.Status(HttpStatusCode.TooManyRequests));

        var fetch = await NewInbox(network).Read(Profile, 45, TestContext.Current.CancellationToken);

        Assert.Equal(40, fetch.Notifications.Count);
        Assert.False(fetch.IsComplete);
        Assert.Equal("mastodon.social", fetch.StoppedBy?.Instance);

        // Fail fast: the limit is not waited out and the page it refused is not asked for again.
        Assert.Equal(2, network.Requests.Count);
    }

    /// <summary>The same thing on the first page: nothing in hand, but emphatically not an empty inbox.</summary>
    [Fact]
    public async Task Read_ReportsARateLimitOnTheFirstPageAsAFetchThatNeverGotGoing()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Status(HttpStatusCode.TooManyRequests));

        var fetch = await NewInbox(network).Read(Profile, 20, TestContext.Current.CancellationToken);

        Assert.Empty(fetch.Notifications);
        Assert.False(fetch.IsComplete);
        Assert.NotNull(fetch.StoppedBy);
    }

    /// <summary>
    ///     Mastodon offers to leave kinds out of what it sends, and this asks for all of them: a kind left out is one
    ///     the account can neither read nor dismiss.
    /// </summary>
    [Fact]
    public async Task Read_AsksTheInstanceToLeaveNoKindOfNotificationOut()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Json(Page(NotificationJson("34"))));

        await NewInbox(network).Read(Profile, 20, TestContext.Current.CancellationToken);

        Assert.DoesNotContain("exclude", Assert.Single(network.Requests).RequestUri?.Query ?? string.Empty);
    }

    [Fact]
    public async Task Dismiss_ClearsTheOneNotificationItWasNamed()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json("{}"));

        await NewInbox(network).Dismiss(Profile, "34", TestContext.Current.CancellationToken);

        var request = Assert.Single(network.Requests);
        Assert.Equal("https://mastodon.social/api/v1/notifications/34/dismiss", request.RequestUri?.ToString());
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("Bearer token-personal", request.Headers.Authorization?.ToString());
    }

    /// <summary>
    ///     One call rather than a dismissal each: an account with a hundred notifications would otherwise spend a
    ///     hundred requests against the instance's rate limit to empty a list Mastodon empties in one.
    /// </summary>
    [Fact]
    public async Task Clear_EmptiesTheWholeInboxInOneCall()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json("{}"));

        await NewInbox(network).Clear(Profile, TestContext.Current.CancellationToken);

        var request = Assert.Single(network.Requests);
        Assert.Equal("https://mastodon.social/api/v1/notifications/clear", request.RequestUri?.ToString());
        Assert.Equal(HttpMethod.Post, request.Method);
    }

    /// <summary>Resolved from the container the app builds, so the wiring is under test alongside the behavior.</summary>
    private static INotificationInbox NewInbox(HttpMessageHandler network)
    {
        var services = new ServiceCollection();
        services.AddWoolyCore();
        services.AddHttpClient(WoolyClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => network);

        return services.BuildServiceProvider().GetRequiredService<INotificationInbox>();
    }

    private static string Page(params string[] notifications) => $"[{string.Join(",", notifications)}]";

    /// <summary>A page of <paramref name="count" /> notifications with descending ids, the way an inbox comes back.</summary>
    private static string PageOf(int count, int firstId) =>
        Page(Enumerable.Range(0, count).Select(offset => NotificationJson($"{firstId - offset}")).ToArray());

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
                    $"<https://mastodon.social/api/v1/notifications?max_id={nextMaxId}>; rel=\"next\"");
            }

            return response;
        };

    /// <param name="account">
    ///     The wire's <c>acct</c>: bare for an account on the instance being read, <c>username@instance</c> for one
    ///     anywhere else.
    /// </param>
    /// <param name="post">
    ///     The wire's <c>status</c>, absent by default — most notifications carry one, and the mapping of the one that
    ///     does not is what <see cref="Read_ReportsAFollowAsANotificationWithNoPostBehindIt" /> is about.
    /// </param>
    private static string NotificationJson(
        string id,
        string type = "mention",
        string account = "alice@hachyderm.io",
        string? post = null) =>
        $$"""
          {
            "id": "{{id}}",
            "type": "{{type}}",
            "created_at": "2026-07-29T12:04:00.000Z",
            "account": {
              "id": "2",
              "username": "{{account.Split('@')[0]}}",
              "acct": "{{account}}",
              "display_name": "{{char.ToUpperInvariant(account[0]) + account.Split('@')[0][1..]}}"
            },
            "status": {{post ?? (type == "follow" || type == "follow_request" ? "null" : PostJson("110"))}}
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
            "visibility": "public",
            "reblogs_count": 3,
            "favourites_count": 5,
            "replies_count": 1,
            "reblog": null
          }
          """;
}
