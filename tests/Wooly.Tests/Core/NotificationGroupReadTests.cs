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
///     The grouped read behind <see cref="INotificationInbox.ReadGroups" />, at the <see cref="HttpMessageHandler" />
///     seam ADR-0005 puts an adapter's tests at: what Mastodon's <c>/api/v2/notifications</c> answers turned into
///     <b>Notification groups</b>, how it is paged, and the fall back to <c>/api/v1</c> on an instance too old to group
///     (#453, ADR-0032). The wire is the one #449 pinned against a real instance.
/// </summary>
public class NotificationGroupReadTests
{
    private static readonly ActiveProfile Profile = new()
    {
        Name = "personal",
        Instance = "mastodon.social",
        Account = "jeff@mastodon.social",
        AccessToken = "token-personal",
    };

    /// <summary>
    ///     A group is what the instance said of it: its key, its kind in this project's words, how many notifications
    ///     are in it (counted past the accounts sent with it), its newest notification — whose id arrives as a number,
    ///     unlike every other id — and its sample of actors in the order given, each an account with an id and a face.
    /// </summary>
    [Fact]
    public async Task ReadGroups_TurnsAGroupedAnswerIntoNotificationGroups()
    {
        var network = new ScriptedHttpMessageHandler(GroupedResponse(Grouped(
            [Group("favourite-110-493000", "favourite", count: 9, newest: 340, samples: ["2", "3"], status: "110")],
            accounts: [AccountJson("2", "alice@hachyderm.io"), AccountJson("3", "bob")],
            statuses: [PostJson("110")])));

        var fetch = await NewInbox(network).ReadGroups(Profile, 40, TestContext.Current.CancellationToken);

        var group = Assert.Single(fetch.Items);
        Assert.Equal("favourite-110-493000", group.Key);
        Assert.Equal(NotificationKind.Favorite, group.Kind);
        Assert.Equal(9, group.Count);
        Assert.Equal("340", group.NewestId);
        Assert.Equal(new DateTimeOffset(2026, 7, 29, 12, 4, 0, TimeSpan.Zero), group.NewestAt);
        Assert.Equal(["2", "3"], group.Actors.Select(actor => actor.Id));
        Assert.Equal(["alice@hachyderm.io", "bob@mastodon.social"], group.Actors.Select(actor => actor.Address));
        Assert.Equal(["Alice", "Bob"], group.Actors.Select(actor => actor.Author));
        Assert.Equal("https://files.example/2.png", group.Actors[0].AvatarUrl);
        Assert.Equal("110", group.Post?.Id);
        Assert.True(group.Post?.IsMine);
    }

    /// <summary>
    ///     A group's actors and the author of its post come with the custom emoji their names use, read off the raw
    ///     grouped answer through the same account and post mapping as everywhere else (ADR-0032, ADR-0035).
    /// </summary>
    [Fact]
    public async Task ReadGroups_ReadsTheCustomEmojiTheActorsAndThePostsAuthorList()
    {
        var network = new ScriptedHttpMessageHandler(GroupedResponse(Grouped(
            [Group("favourite-110-493000", "favourite", count: 1, newest: 340, samples: ["2"], status: "110")],
            accounts:
            [
                AccountJson(
                    "2",
                    "alice@hachyderm.io",
                    displayName: "Alice :verified:",
                    emojis: WireEmojis.Json(("verified", "https://files.hachyderm.io/emoji/verified.png"))),
            ],
            statuses:
            [
                PostJson(
                    "110",
                    author: "Jeff :US_AZ:",
                    emojis: WireEmojis.Json(("US_AZ", "https://files.mastodon.social/emoji/US_AZ.png"))),
            ])));

        var fetch = await NewInbox(network).ReadGroups(Profile, 40, TestContext.Current.CancellationToken);

        var group = Assert.Single(fetch.Items);
        var actor = Assert.Single(group.Actors);
        Assert.Equal("Alice :verified:", actor.Author);
        Assert.Equal(
            [new CustomEmoji("verified", "https://files.hachyderm.io/emoji/verified-static.png")],
            actor.CustomEmoji);
        Assert.Equal(
            [new CustomEmoji("US_AZ", "https://files.mastodon.social/emoji/US_AZ-static.png")],
            group.Post?.AuthorCustomEmoji);
    }

    /// <summary>
    ///     The grouped read is sent as the instance's own grouping, with no <c>grouped_types</c>: which kinds group is
    ///     the instance's call, never this client's (ADR-0032).
    /// </summary>
    [Fact]
    public async Task ReadGroups_AsksTheGroupedEndpointWithoutChoosingWhatGroups()
    {
        var network = new ScriptedHttpMessageHandler(GroupedResponse(Grouped([])));

        await NewInbox(network).ReadGroups(Profile, 40, TestContext.Current.CancellationToken);

        var asked = Assert.Single(network.Requests).RequestUri!;
        Assert.Equal("/api/v2/notifications", asked.AbsolutePath);
        Assert.DoesNotContain("grouped_types", asked.Query, StringComparison.Ordinal);
        Assert.Equal("Bearer", network.Requests[0].Headers.Authorization?.Scheme);
    }

    /// <summary>
    ///     The four kinds this project names arrive in its words, and anything else under the instance's word, as a
    ///     single notification's kind does (ADR-0010). A follow has no post.
    /// </summary>
    [Theory]
    [InlineData("mention", "mention")]
    [InlineData("follow", "follow")]
    [InlineData("reblog", "boost")]
    [InlineData("favourite", "favorite")]
    [InlineData("poll", "poll")]
    [InlineData("admin.sign_up", "admin.sign_up")]
    public async Task ReadGroups_NamesKindsAsASingleNotificationDoes(string wire, string expected)
    {
        var network = new ScriptedHttpMessageHandler(GroupedResponse(Grouped(
            [Group("k", wire, count: 1, newest: 34, samples: ["2"], status: null)],
            accounts: [AccountJson("2", "alice@hachyderm.io")])));

        var fetch = await NewInbox(network).ReadGroups(Profile, 40, TestContext.Current.CancellationToken);

        var group = Assert.Single(fetch.Items);
        Assert.Equal(expected, group.Kind.Name);
        Assert.Null(group.Post);
    }

    /// <summary>
    ///     Groups come back newest first, a page at a time, following the <c>next</c> link the instance sends — the page
    ///     size served is never trusted to say where the list ends (#449).
    /// </summary>
    [Fact]
    public async Task ReadGroups_FollowsTheNextLinkPastTheFirstPage()
    {
        var network = new ScriptedHttpMessageHandler(
            GroupedResponse(
                Grouped([Group("a", "mention", 1, 50, ["2"], null), Group("b", "mention", 1, 49, ["2"], null)],
                    accounts: [AccountJson("2", "alice@hachyderm.io")]),
                nextMaxId: "49"),
            GroupedResponse(
                Grouped([Group("c", "mention", 1, 48, ["2"], null)],
                    accounts: [AccountJson("2", "alice@hachyderm.io")])));

        var fetch = await NewInbox(network).ReadGroups(Profile, 40, TestContext.Current.CancellationToken);

        Assert.Equal(["a", "b", "c"], fetch.Items.Select(group => group.Key));
        Assert.Contains("max_id=49", network.Requests[1].RequestUri!.Query, StringComparison.Ordinal);
    }

    /// <summary>
    ///     An instance older than Mastodon 4.3 has no grouped read and answers 404. Its notifications are read as
    ///     they always were, through <c>/api/v1</c>, each a group of one — and that is learned once: the next read goes
    ///     straight to <c>v1</c> rather than asking again.
    /// </summary>
    [Fact]
    public async Task ReadGroups_FallsBackToOneGroupPerNotificationOnAnInstanceWithNoGroupedRead()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Status(HttpStatusCode.NotFound),
            ScriptedHttpMessageHandler.Json($"[{V1NotificationJson("34", "favourite")}]"),
            ScriptedHttpMessageHandler.Json($"[{V1NotificationJson("35", "follow")}]"));
        var inbox = NewInbox(network);

        var first = await inbox.ReadGroups(Profile, 40, TestContext.Current.CancellationToken);
        var second = await inbox.ReadGroups(Profile, 40, TestContext.Current.CancellationToken);

        var group = Assert.Single(first.Items);
        Assert.Equal(NotificationKind.Favorite, group.Kind);
        Assert.Equal(1, group.Count);
        Assert.Equal("34", group.NewestId);
        Assert.Equal("2", Assert.Single(group.Actors).Id);
        Assert.Equal("alice@hachyderm.io", group.Actors[0].Address);
        Assert.Equal("https://files.example/2.png", group.Actors[0].AvatarUrl);
        Assert.Equal("110", group.Post?.Id);
        Assert.Equal("35", Assert.Single(second.Items).NewestId);
        Assert.Equal(
            ["/api/v2/notifications", "/api/v1/notifications", "/api/v1/notifications"],
            network.Requests.Select(request => request.RequestUri!.AbsolutePath));
    }

    /// <summary>
    ///     Reading on (#465): a grouped read that stopped at what was asked for says where to read on from, and handing
    ///     that back asks <c>/api/v2</c> for the page the instance's link named next — its <c>max_id</c>, a notification
    ///     id, never anything this client worked out — rather than reading the groups again from the top.
    /// </summary>
    [Fact]
    public async Task ReadGroupsOn_AsksForThePageTheLinkNamedNext()
    {
        var network = new ScriptedHttpMessageHandler(
            GroupedResponse(
                Grouped([Group("a", "mention", 1, 50, ["2"], null), Group("b", "mention", 1, 47, ["2"], null)],
                    accounts: [AccountJson("2", "alice@hachyderm.io")]),
                nextMaxId: "45"),
            GroupedResponse(
                Grouped([Group("c", "mention", 1, 44, ["2"], null)],
                    accounts: [AccountJson("2", "alice@hachyderm.io")]),
                nextMaxId: "40"));
        var inbox = NewInbox(network);
        var token = TestContext.Current.CancellationToken;

        var first = await inbox.ReadGroups(Profile, 2, token);
        var onward = await inbox.ReadGroupsOn(Profile, first.Onward!, 1, token);

        Assert.False(first.IsWhole);
        Assert.Equal("/api/v2/notifications", network.Requests[1].RequestUri!.AbsolutePath);
        Assert.Contains("max_id=45", network.Requests[1].RequestUri!.Query, StringComparison.Ordinal);
        Assert.Equal(["c"], onward.Items.Select(group => group.Key));
        Assert.False(onward.IsWhole);
    }

    /// <summary>
    ///     The end is the instance's to say: a short page that still names a next page reads on, and only a page with
    ///     no <c>next</c> link makes the groups whole (#449).
    /// </summary>
    [Fact]
    public async Task ReadGroupsOn_IsWholeOnlyWhereTheInstanceNamesNoNextPage()
    {
        var network = new ScriptedHttpMessageHandler(
            GroupedResponse(
                Grouped([Group("a", "mention", 1, 50, ["2"], null)], accounts: [AccountJson("2", "alice@hachyderm.io")]),
                nextMaxId: "49"),
            GroupedResponse(
                Grouped([Group("b", "mention", 1, 48, ["2"], null)], accounts: [AccountJson("2", "alice@hachyderm.io")]),
                nextMaxId: "47"),
            GroupedResponse(
                Grouped([Group("c", "mention", 1, 46, ["2"], null)], accounts: [AccountJson("2", "alice@hachyderm.io")])));
        var inbox = NewInbox(network);
        var token = TestContext.Current.CancellationToken;

        var first = await inbox.ReadGroups(Profile, 1, token);
        var second = await inbox.ReadGroupsOn(Profile, first.Onward!, 1, token);
        var third = await inbox.ReadGroupsOn(Profile, second.Onward!, 40, token);

        Assert.False(second.IsWhole);
        Assert.Contains("max_id=47", network.Requests[2].RequestUri!.Query, StringComparison.Ordinal);
        Assert.Equal(["c"], third.Items.Select(group => group.Key));
        Assert.True(third.IsWhole);
    }

    /// <summary>
    ///     An instance too old to group reads on too, through <c>/api/v1</c> as it was first read, from the last
    ///     notification that read held — each older notification a group of one (ADR-0032).
    /// </summary>
    [Fact]
    public async Task ReadGroupsOn_ReadsOnThroughV1OnAnInstanceWithNoGroupedRead()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Status(HttpStatusCode.NotFound),
            ScriptedHttpMessageHandler.Json($"[{V1NotificationJson("34", "favourite")}]"),
            ScriptedHttpMessageHandler.Json($"[{V1NotificationJson("33", "follow")}]"));
        var inbox = NewInbox(network);
        var token = TestContext.Current.CancellationToken;

        var first = await inbox.ReadGroups(Profile, 1, token);
        var onward = await inbox.ReadGroupsOn(Profile, first.Onward!, 40, token);

        Assert.Equal("/api/v1/notifications", network.Requests[2].RequestUri!.AbsolutePath);
        Assert.Contains("max_id=34", network.Requests[2].RequestUri!.Query, StringComparison.Ordinal);
        var group = Assert.Single(onward.Items);
        Assert.Equal("33", group.NewestId);
        Assert.Equal(1, group.Count);
        Assert.True(onward.IsWhole);
    }

    /// <summary>
    ///     A group's actors are read from the group's own accounts endpoint, so the list is exactly the group the row
    ///     counted, every one an account with an id and a face, and it is read on past the first page by the
    ///     instance's <c>next</c> link (#455, #449).
    /// </summary>
    [Fact]
    public async Task ReadGroupActors_ReadsTheGroupsAccountsPageByPage()
    {
        var network = new ScriptedHttpMessageHandler(
            GroupedResponse(
                $"[{AccountJson("2", "alice@hachyderm.io")},{AccountJson("3", "bob")}]",
                nextMaxId: "338"),
            GroupedResponse($"[{AccountJson("4", "carol@carpdiem.online")}]"));

        var fetch = await NewInbox(network).ReadGroupActors(
            Profile,
            "favourite-110-493000",
            80,
            TestContext.Current.CancellationToken);

        Assert.Equal(["2", "3", "4"], fetch.Items.Select(actor => actor.Id));
        Assert.Equal(
            ["alice@hachyderm.io", "bob@mastodon.social", "carol@carpdiem.online"],
            fetch.Items.Select(actor => actor.Address));
        Assert.Equal("https://files.example/2.png", fetch.Items[0].AvatarUrl);
        Assert.All(
            network.Requests,
            request => Assert.Equal(
                "/api/v2/notifications/favourite-110-493000/accounts",
                request.RequestUri!.AbsolutePath));
        Assert.Equal(HttpMethod.Get, network.Requests[0].Method);
        Assert.DoesNotContain("max_id", network.Requests[0].RequestUri!.Query, StringComparison.Ordinal);
        Assert.Contains("max_id=338", network.Requests[1].RequestUri!.Query, StringComparison.Ordinal);
    }

    /// <summary>Asked for fewer than the group holds, the read stops there rather than walking on through the rest.</summary>
    [Fact]
    public async Task ReadGroupActors_StopsAtTheLimitAskedFor()
    {
        var network = new ScriptedHttpMessageHandler(
            GroupedResponse(
                $"[{AccountJson("2", "alice@hachyderm.io")},{AccountJson("3", "bob")}]",
                nextMaxId: "338"));

        var fetch = await NewInbox(network).ReadGroupActors(
            Profile,
            "favourite-110-493000",
            2,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, fetch.Items.Count);
        Assert.Contains("limit=2", Assert.Single(network.Requests).RequestUri!.Query, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Reading on (#467): a read of a group's actors that stopped at what was asked for says where to read on from,
    ///     and handing that back asks for the page the instance named next rather than reading the group from the top.
    /// </summary>
    [Fact]
    public async Task ReadGroupActorsOn_AsksForThePageTheLastReadNamedNext()
    {
        var network = new ScriptedHttpMessageHandler(
            GroupedResponse(
                $"[{AccountJson("2", "alice@hachyderm.io")},{AccountJson("3", "bob")}]",
                nextMaxId: "338"),
            GroupedResponse($"[{AccountJson("4", "carol@carpdiem.online")}]", nextMaxId: "301"));
        var inbox = NewInbox(network);
        var token = TestContext.Current.CancellationToken;

        var first = await inbox.ReadGroupActors(Profile, "favourite-110-493000", 2, token);
        var onward = await inbox.ReadGroupActorsOn(Profile, "favourite-110-493000", first.Onward!, 1, token);

        Assert.False(first.IsWhole);
        Assert.Equal(2, network.Requests.Count);
        Assert.Equal(
            "https://mastodon.social/api/v2/notifications/favourite-110-493000/accounts?limit=1&max_id=338",
            network.Requests[1].RequestUri?.ToString());
        Assert.Equal("4", Assert.Single(onward.Items).Id);
        Assert.False(onward.IsWhole);
    }

    /// <summary>
    ///     And where the instance names no page after the one read on, the group is whole: its accounts endpoint pages
    ///     by notification ids, so nothing read can stand in for a link the instance did not send.
    /// </summary>
    [Fact]
    public async Task ReadGroupActorsOn_IsWholeWhereTheInstanceNamesNoFurtherPage()
    {
        var network = new ScriptedHttpMessageHandler(
            GroupedResponse(
                $"[{AccountJson("2", "alice@hachyderm.io")},{AccountJson("3", "bob")}]",
                nextMaxId: "338"),
            GroupedResponse($"[{AccountJson("4", "carol@carpdiem.online")}]"));
        var inbox = NewInbox(network);
        var token = TestContext.Current.CancellationToken;

        var first = await inbox.ReadGroupActors(Profile, "favourite-110-493000", 2, token);
        var onward = await inbox.ReadGroupActorsOn(Profile, "favourite-110-493000", first.Onward!, 80, token);

        Assert.Equal("4", Assert.Single(onward.Items).Id);
        Assert.True(onward.IsWhole);
    }

    /// <summary>
    ///     <c>d</c> on a group clears every notification in it with one request, named by the group's key — never one
    ///     dismissal per notification (#457, #449).
    /// </summary>
    [Fact]
    public async Task DismissGroup_SendsOneDismissByTheGroupsKey()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json("{}"));

        await NewInbox(network).DismissGroup(
            Profile,
            ANotification.Group(key: "favourite-110-493000", count: 9, newestId: "340"),
            TestContext.Current.CancellationToken);

        var request = Assert.Single(network.Requests);
        Assert.Equal(
            "https://mastodon.social/api/v2/notifications/favourite-110-493000/dismiss",
            request.RequestUri?.ToString());
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("Bearer token-personal", request.Headers.Authorization?.ToString());
    }

    /// <summary>
    ///     An instance too old to group has no group to dismiss either. There every group is one notification, and it is
    ///     dismissed by its own id, as it always was (#457).
    /// </summary>
    [Fact]
    public async Task DismissGroup_DismissesTheOneNotificationByItsOwnIdOnAnInstanceWithNoGroupedRead()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Status(HttpStatusCode.NotFound),
            ScriptedHttpMessageHandler.Json($"[{V1NotificationJson("34", "favourite")}]"),
            ScriptedHttpMessageHandler.Json("{}"));
        var inbox = NewInbox(network);

        var read = await inbox.ReadGroups(Profile, 40, TestContext.Current.CancellationToken);
        await inbox.DismissGroup(Profile, Assert.Single(read.Items), TestContext.Current.CancellationToken);

        var request = network.Requests[^1];
        Assert.Equal("https://mastodon.social/api/v1/notifications/34/dismiss", request.RequestUri?.ToString());
        Assert.Equal(HttpMethod.Post, request.Method);
    }

    private static INotificationInbox NewInbox(HttpMessageHandler network)
    {
        var services = new ServiceCollection();
        services.AddWoolyCore();
        services.AddHttpClient(WoolyClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => network);

        return services.BuildServiceProvider().GetRequiredService<INotificationInbox>();
    }

    /// <param name="nextMaxId">Where the instance says the next page starts, in its link header; none where null.</param>
    private static Func<HttpRequestMessage, HttpResponseMessage> GroupedResponse(string json, string? nextMaxId = null) =>
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
                    $"<https://mastodon.social/api/v2/notifications?max_id={nextMaxId}>; rel=\"next\", "
                    + "<https://mastodon.social/api/v2/notifications?min_id=50>; rel=\"prev\"");
            }

            return response;
        };

    /// <summary>A grouped answer: the groups, and the accounts and posts they name by id.</summary>
    private static string Grouped(
        IReadOnlyList<string> groups,
        IReadOnlyList<string>? accounts = null,
        IReadOnlyList<string>? statuses = null) =>
        $$"""
          {
            "accounts": [{{string.Join(",", accounts ?? [])}}],
            "statuses": [{{string.Join(",", statuses ?? [])}}],
            "notification_groups": [{{string.Join(",", groups)}}]
          }
          """;

    /// <summary>One group as the instance sends it — <c>most_recent_notification_id</c> a JSON number (#449).</summary>
    private static string Group(
        string key,
        string type,
        int count,
        long newest,
        IReadOnlyList<string> samples,
        string? status) =>
        $$"""
          {
            "group_key": "{{key}}",
            "notifications_count": {{count}},
            "type": "{{type}}",
            "most_recent_notification_id": {{newest}},
            "page_min_id": "{{newest - count + 1}}",
            "page_max_id": "{{newest}}",
            "latest_page_notification_at": "2026-07-29T12:04:00.000Z",
            "sample_account_ids": [{{string.Join(",", samples.Select(id => $"\"{id}\""))}}],
            "status_id": {{(status is null ? "null" : $"\"{status}\"")}}
          }
          """;

    private static string AccountJson(string id, string acct, string? displayName = null, string emojis = "[]") =>
        $$"""
          {
            "id": "{{id}}",
            "username": "{{acct.Split('@')[0]}}",
            "acct": "{{acct}}",
            "display_name": "{{displayName ?? char.ToUpperInvariant(acct[0]) + acct.Split('@')[0][1..]}}",
            "emojis": {{emojis}},
            "avatar": "https://files.example/{{id}}.png",
            "avatar_static": "https://files.example/{{id}}.png",
            "created_at": "2024-01-01T00:00:00.000Z"
          }
          """;

    private static string V1NotificationJson(string id, string type) =>
        $$"""
          {
            "id": "{{id}}",
            "type": "{{type}}",
            "created_at": "2026-07-29T12:04:00.000Z",
            "group_key": "ungrouped-{{id}}",
            "account": {{AccountJson("2", "alice@hachyderm.io")}},
            "status": {{(type == "follow" ? "null" : PostJson("110"))}}
          }
          """;

    /// <param name="author">The name its author chose to be shown as.</param>
    /// <param name="emojis">The custom emoji the author's account lists.</param>
    private static string PostJson(string id, string author = "Jeff", string emojis = "[]") =>
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
              "display_name": "{{author}}",
              "emojis": {{emojis}}
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
