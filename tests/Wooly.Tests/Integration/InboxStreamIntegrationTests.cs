using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Wooly.Core.Inbox;
using Wooly.Core.Notifications;
using Wooly.Core.Posts;

namespace Wooly.Tests.Integration;

/// <summary>
///     Runs <see cref="InboxStream" /> against the live instance's streaming service (#401): <c>woolyvoter</c> writing to
///     <c>woolytester</c> reaches the tester's stream as the port's events — a mention as a notification, a direct
///     message as a conversation.
/// </summary>
/// <remarks>
///     What this leaves to the fake seams: follow requests over the stream (it would mean locking the tester account,
///     changing what every other test here sees), and dropping and reconnecting, which is timing against a live server.
///     Skipped where the instance will not stream, which is not what these are about.
/// </remarks>
[Trait("Category", "Integration")]
[Collection(LiveInstanceCollection.Name)]
public class InboxStreamIntegrationTests
{
    /// <summary>How long anything here is waited for: a connection coming up, and then what was written arriving.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    [Fact(Skip = LiveInstance.SkipReason, SkipType = typeof(LiveInstance), SkipUnless = nameof(LiveInstance.Available))]
    public async Task AMention_ReachesTheStreamAsANotification()
    {
        var heard = await Heard(
            new PostDraft { Text = $"@{LiveInstance.Username} wooly integration test {Guid.NewGuid():N}" },
            said => said is InboxEvent.Notified);

        Assert.Equal(new InboxEvent.Notified(NotificationKind.Mention), heard);
    }

    [Fact(Skip = LiveInstance.SkipReason, SkipType = typeof(LiveInstance), SkipUnless = nameof(LiveInstance.Available))]
    public async Task ADirectMessage_ReachesTheStreamAsAConversation()
    {
        var heard = await Heard(
            new PostDraft
            {
                Text = $"@{LiveInstance.Username} wooly integration test {Guid.NewGuid():N}",
                Visibility = PostVisibility.Direct,
                VisibilityChosen = true,
            },
            said => said is InboxEvent.ConversationChanged);

        Assert.Equal(new InboxEvent.ConversationChanged(), heard);
    }

    /// <summary>
    ///     Listens to the tester's stream, has the voter publish <paramref name="draft" /> once it is up, and answers
    ///     the first thing heard that is <paramref name="wanted" /> — or skips, where the stream never came up.
    /// </summary>
    private static async Task<InboxEvent?> Heard(PostDraft draft, Func<InboxEvent, bool> wanted)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var services = LiveInstance.NewServices();
        var stream = services.GetRequiredService<IInboxStream>();
        var author = services.GetRequiredService<IPostAuthor>();
        var heard = Channel.CreateUnbounded<InboxEvent>();

        using var closing = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var listening = stream.Listen(LiveInstance.Profile, said => heard.Writer.TryWrite(said), closing.Token);

        try
        {
            if (await Next(heard.Reader, said => said is InboxEvent.Connected, cancellationToken) is null)
            {
                Assert.Skip("The live instance's stream did not come up.");
            }

            var post = await author.Publish(LiveInstance.VoterProfile, draft, cancellationToken);

            try
            {
                // The one wait: Mastodon delivers to the account mentioned through a Sidekiq job rather than inline
                // with publishing, so what the stream says can be a moment behind the post.
                return await Next(heard.Reader, wanted, cancellationToken);
            }
            finally
            {
                await author.Delete(LiveInstance.VoterProfile, post.Id, cancellationToken);
            }
        }
        finally
        {
            await closing.CancelAsync();

            try
            {
                await listening;
            }
            catch (Exception)
            {
                // However the connection ended once it was closed, it is no part of what was being tested.
            }
        }
    }

    /// <summary>The first thing heard that is <paramref name="wanted" />, or nothing within <see cref="Patience" />.</summary>
    private static async Task<InboxEvent?> Next(
        ChannelReader<InboxEvent> heard,
        Func<InboxEvent, bool> wanted,
        CancellationToken cancellationToken)
    {
        using var patience = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        patience.CancelAfter(Patience);

        try
        {
            await foreach (var said in heard.ReadAllAsync(patience.Token))
            {
                if (wanted(said))
                {
                    return said;
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Out of patience.
        }

        return null;
    }
}
