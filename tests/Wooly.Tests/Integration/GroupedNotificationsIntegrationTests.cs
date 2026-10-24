using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Wooly.Core.Http;
using Wooly.Core.Notifications;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;

namespace Wooly.Tests.Integration;

/// <summary>
///     What the TUI's notification groups are going to stand on (#448), checked against the live instance before any of
///     it is built (#449): that the instance groups favorites of one post into one <b>Notification group</b> without
///     being asked to, never groups a mention, counts a group past the accounts it sends with it, lists exactly a
///     group's actors on request, and dismisses a group whole.
/// </summary>
/// <remarks>
///     Read raw rather than through <see cref="INotificationInbox" />, which has no grouped read yet: these pin the
///     wire the grouped read will be written against, field names and all, so that what is found here is what #453
///     maps. Mastonet 3.1.3 has no v2 notifications at all, which is why that read will be raw too.
/// </remarks>
[Trait("Category", "Integration")]
[Collection(LiveInstanceCollection.Name)]
public class GroupedNotificationsIntegrationTests
{
    /// <summary>The most of a group's accounts the instance sends with the group itself.</summary>
    private const int Sampled = 8;

    /// <summary>
    ///     Nine favorites of one post come back as one group of nine carrying eight of its accounts, and the group's
    ///     own accounts are those nine, newest first, paged by the instance's <c>next</c> link — and not the voter,
    ///     who favorited the post too but whose notification was dismissed before the nine arrived. That is what
    ///     tells a group's actors from everyone who ever favorited the post, which is what <c>favourited_by</c> lists.
    /// </summary>
    [Fact(Skip = LiveInstance.SkipReason, SkipType = typeof(LiveInstance), SkipUnless = nameof(LiveInstance.Available))]
    public async Task Favorites_OfOnePostAreOneGroupCountedPastTheAccountsItIsSentWith()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var services = LiveInstance.NewServices();
        var tester = LiveInstance.Profile;
        var fans = LiveInstance.Fans;
        var author = services.GetRequiredService<IPostAuthor>();
        var engagement = services.GetRequiredService<IPostEngagement>();
        var http = services.GetRequiredService<IHttpClientFactory>();

        await AwayFromTheHour(cancellationToken);

        var post = await author.Publish(tester, LiveInstance.ThrowawayDraft(), cancellationToken);

        try
        {
            await engagement.Mark(
                LiveInstance.VoterProfile,
                post.Id,
                PostMark.Favorite,
                wanted: true,
                cancellationToken);

            var before = await GroupAbout(http, tester, post.Id, "favourite", 1, cancellationToken);

            Assert.NotNull(before);
            await Dismiss(http, tester, GroupKey(before.Value), cancellationToken);

            foreach (var fan in fans)
            {
                await engagement.Mark(fan.Profile, post.Id, PostMark.Favorite, wanted: true, cancellationToken);
            }

            var group = await GroupAbout(http, tester, post.Id, "favourite", fans.Count, cancellationToken);

            Assert.NotNull(group);
            var found = group.Value;
            Assert.Equal(fans.Count, found.GetProperty("notifications_count").GetInt32());
            Assert.Equal(Sampled, found.GetProperty("sample_account_ids").GetArrayLength());
            Assert.StartsWith($"favourite-{post.Id}-", GroupKey(found));

            // Not the strings every other id on this endpoint is: the grouped read has to take this one as a number.
            Assert.Equal(JsonValueKind.Number, found.GetProperty("most_recent_notification_id").ValueKind);
            Assert.Equal(JsonValueKind.String, found.GetProperty("page_max_id").ValueKind);

            var pages = await Actors(http, tester, GroupKey(found), cancellationToken);

            Assert.Equal([4, 4, 1], pages.Select(page => page.Count));
            Assert.Equal(fans.Select(fan => fan.Username).Reverse(), pages.SelectMany(page => page));
        }
        finally
        {
            await author.Delete(tester, post.Id, cancellationToken);
        }
    }

    /// <summary>
    ///     A mention is a group of one under a key of its own, <c>ungrouped-</c> and the notification's id: the key is
    ///     the claim, since two mentions are two posts and would be two groups by post alone.
    /// </summary>
    [Fact(Skip = LiveInstance.SkipReason, SkipType = typeof(LiveInstance), SkipUnless = nameof(LiveInstance.Available))]
    public async Task Mentions_AreEachAGroupOfOne()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var services = LiveInstance.NewServices();
        var tester = LiveInstance.Profile;
        var author = services.GetRequiredService<IPostAuthor>();
        var http = services.GetRequiredService<IHttpClientFactory>();

        var mentions = new List<(ActiveProfile By, Post Post)>();

        try
        {
            foreach (var fan in LiveInstance.Fans.Take(2))
            {
                mentions.Add((fan.Profile,
                    await author.Publish(fan.Profile, LiveInstance.ThrowawayMention(), cancellationToken)));
            }

            var groups = new List<JsonElement>();

            foreach (var (_, mention) in mentions)
            {
                var group = await GroupAbout(http, tester, mention.Id, "mention", 1, cancellationToken);

                Assert.NotNull(group);
                groups.Add(group.Value);
            }

            Assert.All(groups, group => Assert.Equal(1, group.GetProperty("notifications_count").GetInt32()));
            Assert.All(groups, group => Assert.Equal(
                $"ungrouped-{group.GetProperty("most_recent_notification_id").GetInt64()}",
                GroupKey(group)));
        }
        finally
        {
            foreach (var (by, mention) in mentions)
            {
                await author.Delete(by, mention.Id, cancellationToken);
            }
        }
    }

    /// <summary>
    ///     Dismissing a group by its key takes every notification in it: the instance no longer knows the key, and the
    ///     one-by-one read has none of them either.
    /// </summary>
    [Fact(Skip = LiveInstance.SkipReason, SkipType = typeof(LiveInstance), SkipUnless = nameof(LiveInstance.Available))]
    public async Task Dismiss_TakesEveryNotificationInTheGroup()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var services = LiveInstance.NewServices();
        var tester = LiveInstance.Profile;
        var author = services.GetRequiredService<IPostAuthor>();
        var engagement = services.GetRequiredService<IPostEngagement>();
        var inbox = services.GetRequiredService<INotificationInbox>();
        var http = services.GetRequiredService<IHttpClientFactory>();

        await AwayFromTheHour(cancellationToken);

        var post = await author.Publish(tester, LiveInstance.ThrowawayDraft(), cancellationToken);

        try
        {
            foreach (var fan in LiveInstance.Fans.Take(2))
            {
                await engagement.Mark(fan.Profile, post.Id, PostMark.Favorite, wanted: true, cancellationToken);
            }

            var group = await GroupAbout(http, tester, post.Id, "favourite", 2, cancellationToken);

            Assert.NotNull(group);
            var key = GroupKey(group.Value);

            await Dismiss(http, tester, key, cancellationToken);

            using (var looked = await RawMastodonCall.Sent(
                       http,
                       tester,
                       HttpMethod.Get,
                       $"api/v2/notifications/{key}",
                       content: null,
                       cancellationToken))
            {
                Assert.Equal(HttpStatusCode.NotFound, looked.StatusCode);
            }

            // From just below the group's oldest notification, so the read covers the whole group however many have
            // arrived since, rather than only what the newest page of the inbox reaches.
            var oldest = group.Value.GetProperty("page_min_id").GetString()!;
            var since = long.Parse(oldest, CultureInfo.InvariantCulture) - 1;
            var listed = await inbox.ReadNewer(
                tester,
                since.ToString(CultureInfo.InvariantCulture),
                40,
                cancellationToken);

            Assert.DoesNotContain(listed.Items, notification => notification.Post?.Id == post.Id);
        }
        finally
        {
            await author.Delete(tester, post.Id, cancellationToken);
        }
    }

    /// <summary>
    ///     Waits out the last half-minute of the hour, if that is where the clock is. The instance keys a favorite's
    ///     group by the post and the hour it arrived in, so favorites either side of the hour are two groups, and a
    ///     test that wants one would fail for the time of day it ran at.
    /// </summary>
    private static async Task AwayFromTheHour(CancellationToken cancellationToken)
    {
        var intoTheHour = TimeSpan.FromTicks(DateTimeOffset.UtcNow.TimeOfDay.Ticks % TimeSpan.TicksPerHour);
        var left = TimeSpan.FromHours(1) - intoTheHour;

        if (left < TimeSpan.FromSeconds(30))
        {
            await Task.Delay(left + TimeSpan.FromSeconds(1), cancellationToken);
        }
    }

    /// <summary>The key the instance names <paramref name="group" /> by, which is what dismisses it.</summary>
    private static string GroupKey(JsonElement group) => group.GetProperty("group_key").GetString()!;

    /// <summary>Dismisses the group <paramref name="groupKey" /> names, failing unless the instance took it.</summary>
    private static async Task Dismiss(
        IHttpClientFactory http,
        ActiveProfile profile,
        string groupKey,
        CancellationToken cancellationToken)
    {
        using var dismissed = await RawMastodonCall.Sent(
            http,
            profile,
            HttpMethod.Post,
            $"api/v2/notifications/{groupKey}/dismiss",
            content: null,
            cancellationToken);

        Assert.True(dismissed.IsSuccessStatusCode);
    }

    /// <summary>
    ///     The group of <paramref name="type" /> about the post <paramref name="postId" />, once it has at least
    ///     <paramref name="count" /> in it, or <see langword="null" /> where it never does. The wait is the one
    ///     <see cref="NotificationInboxIntegrationTests" /> makes: Sidekiq delivers a notification a moment after the
    ///     act that caused it.
    /// </summary>
    private static async Task<JsonElement?> GroupAbout(
        IHttpClientFactory http,
        ActiveProfile profile,
        string postId,
        string type,
        int count,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var page = await RawMastodonCall.Get<JsonElement>(
                http,
                profile,
                "api/v2/notifications",
                [new KeyValuePair<string, string>("limit", "40")],
                cancellationToken);

            var group = page.GetProperty("notification_groups")
                            .EnumerateArray()
                            .Where(group => group.GetProperty("type").GetString() == type)
                            .Where(group => group.TryGetProperty("status_id", out var id) && id.GetString() == postId)
                            .Select(group => (JsonElement?)group)
                            .FirstOrDefault();

            if (group is { } found && found.GetProperty("notifications_count").GetInt32() >= count)
            {
                return found;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }

        return null;
    }

    /// <summary>
    ///     Every actor in the group <paramref name="groupKey" /> names, by username, page by page as the instance
    ///     served them when asked for four at a time and led on by its <c>next</c> link.
    /// </summary>
    private static async Task<IReadOnlyList<IReadOnlyList<string>>> Actors(
        IHttpClientFactory http,
        ActiveProfile profile,
        string groupKey,
        CancellationToken cancellationToken)
    {
        var pages = new List<IReadOnlyList<string>>();
        var path = $"api/v2/notifications/{groupKey}/accounts?limit=4";

        while (path is not null)
        {
            using var response = await RawMastodonCall.Sent(
                http,
                profile,
                HttpMethod.Get,
                path,
                content: null,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            using var page = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);

            if (page.RootElement.GetArrayLength() == 0)
            {
                break;
            }

            pages.Add([
                .. page.RootElement.EnumerateArray().Select(account => account.GetProperty("acct").GetString()!),
            ]);
            path = NextPagePath(response);
        }

        return pages;
    }

    /// <summary>
    ///     The path and query of the instance's <c>rel="next"</c> link, or <see langword="null" /> where it sent none.
    /// </summary>
    private static string? NextPagePath(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Link", out var links))
        {
            return null;
        }

        var next = string.Join(",", links)
                         .Split(',')
                         .Select(link => link.Trim())
                         .FirstOrDefault(link => link.EndsWith("rel=\"next\"", StringComparison.Ordinal));

        return next is null ? null : new Uri(next[1..next.IndexOf('>')]).PathAndQuery.TrimStart('/');
    }
}
