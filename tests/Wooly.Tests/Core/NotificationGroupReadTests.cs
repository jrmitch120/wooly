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

    private static string AccountJson(string id, string acct) =>
        $$"""
          {
            "id": "{{id}}",
            "username": "{{acct.Split('@')[0]}}",
            "acct": "{{acct}}",
            "display_name": "{{char.ToUpperInvariant(acct[0]) + acct.Split('@')[0][1..]}}",
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
