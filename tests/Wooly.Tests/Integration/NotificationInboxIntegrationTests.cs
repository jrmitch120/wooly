using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Wooly.Core.Notifications;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;

namespace Wooly.Tests.Integration;

/// <summary>
///     Runs <see cref="NotificationInbox" />'s read of what is unread against the live instance: what is newer than the
///     account's <b>Read marker</b> is unread, and moving the marker past a notification leaves it listed but no longer
///     unread (ADR-0027, #396) — moved by <see cref="NotificationInbox.MoveMarker" /> itself, so the move is under
///     test against the instance too (#397).
/// </summary>
/// <remarks>
///     What this cannot cover is an account with no marker at all: Mastodon cannot delete one, so a test account that
///     has ever had one cannot get back to having none. That rule is held at the fake seams.
/// </remarks>
[Trait("Category", "Integration")]
[Collection(LiveInstanceCollection.Name)]
public class NotificationInboxIntegrationTests
{
    [Fact(Skip = LiveInstance.SkipReason, SkipType = typeof(LiveInstance), SkipUnless = nameof(LiveInstance.Available))]
    public async Task ReadUnread_CountsAMentionUntilTheMarkerIsMovedPastIt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var services = LiveInstance.NewServices();
        var tester = LiveInstance.Profile;
        var voter = LiveInstance.VoterProfile;
        var author = services.GetRequiredService<IPostAuthor>();
        var inbox = services.GetRequiredService<INotificationInbox>();

        var mention = await author.Publish(
            voter,
            LiveInstance.ThrowawayMention(),
            cancellationToken);

        try
        {
            // The one wait: Mastodon delivers a mention to the account mentioned through a Sidekiq job rather than
            // inline with publishing it, so the notification can be a moment behind the post.
            var notification = await Notified(inbox, tester, mention.Id, cancellationToken);

            Assert.NotNull(notification);

            // Just below the mention, so that it — and only what came after it — is unread, whatever this account's
            // marker said before.
            Assert.True(await inbox.MoveMarker(tester, Below(notification.Id), cancellationToken));

            var unread = await inbox.ReadUnread(tester, 40, cancellationToken);

            Assert.True(unread.HasMarker);
            Assert.Contains(unread.Since.Items, waiting => waiting.Id == notification.Id);

            Assert.True(await inbox.MoveMarker(tester, notification.Id, cancellationToken));

            var read = await inbox.ReadUnread(tester, 40, cancellationToken);

            Assert.Empty(read.Since.Items);

            // Read is not dismissed: the notification is still there to be listed.
            var listed = await inbox.Read(tester, 40, cancellationToken);

            Assert.Contains(listed.Items, still => still.Id == notification.Id);
        }
        finally
        {
            await author.Delete(voter, mention.Id, cancellationToken);
        }
    }

    /// <summary>The notification <paramref name="profile" /> was sent about the post <paramref name="postId" />.</summary>
    private static async Task<Notification?> Notified(
        INotificationInbox inbox,
        ActiveProfile profile,
        string postId,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var fetch = await inbox.Read(profile, 40, cancellationToken);

            if (fetch.Items.FirstOrDefault(notification => notification.Post?.Id == postId) is { } found)
            {
                return found;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }

        return null;
    }

    /// <summary>The id just below <paramref name="id" />, which Mastodon's ids, being numbers, always have.</summary>
    private static string Below(string id) =>
        (long.Parse(id, CultureInfo.InvariantCulture) - 1).ToString(CultureInfo.InvariantCulture);
}
