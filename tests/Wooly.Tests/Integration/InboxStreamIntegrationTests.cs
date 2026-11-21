using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Wooly.Core.Conversations;
using Wooly.Core.Http;
using Wooly.Core.Inbox;
using Wooly.Core.Notifications;
using Wooly.Core.Posts;

namespace Wooly.Tests.Integration;

/// <summary>
///     Runs <see cref="InboxStream" /> against the live instance's streaming service (#401): <c>woolyvoter</c> writing to
///     <c>woolytester</c> reaches the tester's stream as the port's events — a mention as a notification, a direct
///     message as a conversation. The direct message is also where removing a conversation (#620) is seen against an
///     instance: off the list, and back on it with the next message in the thread.
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

    /// <summary>
    ///     The direct message, and then the conversation it made: removed, it leaves the tester's list, and the voter's
    ///     next message in the thread brings it back, under a new id (#620) — what GLOSSARY.md's <b>Removing a
    ///     conversation</b> claims, seen here rather than taken on trust. Done inside this test, on the message it already
    ///     sends, because the suite runs close to Mastodon's limit on deleting posts and a test of its own would be
    ///     another pair. The answer that brings it back is the voter's, whose deletes are this test's alone.
    /// </summary>
    [Fact(Skip = LiveInstance.SkipReason, SkipType = typeof(LiveInstance), SkipUnless = nameof(LiveInstance.Available))]
    public async Task ADirectMessage_ReachesTheStreamAsAConversationThatRemovingTakesOffTheListUntilTheNextMessage()
    {
        // Mastodon 4.3 on files a private mention from someone the account does not follow under its notification
        // requests, by default — and a mention filtered so never reaches the conversation, or the stream. The tester
        // does not follow the voter, so it takes private mentions from anyone for the length of this test.
        await PrivateMentions("accept");

        try
        {
            var heard = await Heard(
                new PostDraft
                {
                    Text = $"@{LiveInstance.Username} wooly integration test {Guid.NewGuid():N}",
                    Visibility = PostVisibility.Direct,
                    VisibilityChosen = true,
                },
                said => said is InboxEvent.ConversationChanged,
                whileItStands: RemovedUntilTheNextMessage);

            Assert.Equal(new InboxEvent.ConversationChanged(), heard);
        }
        finally
        {
            await PrivateMentions("filter");
        }
    }

    /// <summary>
    ///     Removes the conversation <paramref name="sent" /> made on the tester's list, sees it gone, and has the voter
    ///     answer in the thread — which brings it back.
    /// </summary>
    private static async Task RemovedUntilTheNextMessage(Post sent)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var services = LiveInstance.NewServices();
        var messages = services.GetRequiredService<IDirectMessages>();
        var author = services.GetRequiredService<IPostAuthor>();

        var conversation = await Holding(messages, sent.Id, cancellationToken)
                           ?? throw new InvalidOperationException("The direct message made no conversation on the list.");

        await messages.Remove(LiveInstance.Profile, conversation.Id, cancellationToken);

        Assert.Null(await Holding(messages, sent.Id, cancellationToken));

        var answer = await author.Publish(
            LiveInstance.VoterProfile,
            new PostDraft
            {
                Text = $"@{LiveInstance.Username} wooly integration test {Guid.NewGuid():N}",
                Visibility = PostVisibility.Direct,
                VisibilityChosen = true,
                InReplyTo = sent.Id,
            },
            cancellationToken);

        try
        {
            // Delivered by a Sidekiq job rather than inline with publishing, as the message before it was.
            var back = await Eventually(() => Holding(messages, answer.Id, cancellationToken), cancellationToken);

            Assert.NotNull(back);

            // Back as a new entry on the list rather than the old one restored: Mastodon destroyed the reader's copy,
            // and the next message makes another, under an id of its own.
            Assert.NotEqual(conversation.Id, back.Id);
        }
        finally
        {
            await author.Delete(LiveInstance.VoterProfile, answer.Id, cancellationToken);
        }
    }

    /// <summary>The conversation on the tester's list whose last post is <paramref name="postId" />, if there is one.</summary>
    private static async Task<Conversation?> Holding(
        IDirectMessages messages,
        string postId,
        CancellationToken cancellationToken)
    {
        var listing = await messages.List(LiveInstance.Profile, 40, cancellationToken);

        return listing.Items.FirstOrDefault(conversation => conversation.Latest?.Id == postId);
    }

    /// <summary>What <paramref name="look" /> finds, looked for again until it finds something or patience runs out.</summary>
    private static async Task<T?> Eventually<T>(Func<Task<T?>> look, CancellationToken cancellationToken)
        where T : class
    {
        var giveUp = DateTimeOffset.UtcNow + Patience;

        while (true)
        {
            var found = await look();

            if (found is not null || DateTimeOffset.UtcNow > giveUp)
            {
                return found;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        }
    }

    /// <summary>Sets what the tester's notification policy does with a private mention from someone it does not follow.</summary>
    private static async Task PrivateMentions(string policy)
    {
        var services = LiveInstance.NewServices();

        using var answer = await RawMastodonCall.Sent(
            services.GetRequiredService<IHttpClientFactory>(),
            LiveInstance.Profile,
            HttpMethod.Patch,
            "api/v2/notifications/policy",
            new FormUrlEncodedContent([new KeyValuePair<string, string>("for_private_mentions", policy)]),
            TestContext.Current.CancellationToken);

        answer.EnsureSuccessStatusCode();
    }

    /// <summary>
    ///     Listens to the tester's stream, has the voter publish <paramref name="draft" /> once it is up, and answers
    ///     the first thing heard that is <paramref name="wanted" /> — or skips, where the stream never came up.
    /// </summary>
    /// <param name="whileItStands">
    ///     What else to do with the published post once it has been heard, before it is deleted — or nothing.
    /// </param>
    private static async Task<InboxEvent?> Heard(
        PostDraft draft,
        Func<InboxEvent, bool> wanted,
        Func<Post, Task>? whileItStands = null)
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
                var said = await Next(heard.Reader, wanted, cancellationToken);

                if (said is not null && whileItStands is not null)
                {
                    await whileItStands(post);
                }

                return said;
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
