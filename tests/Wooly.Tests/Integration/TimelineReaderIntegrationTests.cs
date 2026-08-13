using Microsoft.Extensions.DependencyInjection;
using Wooly.Core.Posts;
using Wooly.Core.Timelines;

namespace Wooly.Tests.Integration;

/// <summary>
///     Runs <see cref="TimelineReader" /> against the live instance, publishing a post through
///     <see cref="IPostAuthor" /> first so there is something of this run's own to find — a home timeline the seeded
///     account has followed nobody into is otherwise as likely to be empty as to hold drift worth catching (#33).
/// </summary>
[Trait("Category", "Integration")]
[Collection(LiveInstanceCollection.Name)]
public class TimelineReaderIntegrationTests
{
    [Fact(Skip = LiveInstance.SkipReason, SkipType = typeof(LiveInstance), SkipUnless = nameof(LiveInstance.Available))]
    public async Task Read_FindsAPostJustPublishedOnTheHomeTimeline()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var services = LiveInstance.NewServices();
        var profile = LiveInstance.Profile;
        var author = services.GetRequiredService<IPostAuthor>();
        var reader = services.GetRequiredService<ITimelineReader>();

        var draft = LiveInstance.ThrowawayDraft();
        var published = await author.Publish(profile, draft, cancellationToken);

        try
        {
            // Mastodon fans a new post out to its author's own home feed through a Sidekiq job rather than inline
            // with publishing it, so a read straight after Publish returns can beat that job there. Polling a few
            // times is the test waiting out that one instance-specific race, not something ITimelineReader itself
            // needs to account for — a caller elsewhere in this client always reads a feed that already exists.
            IReadOnlyList<Post> posts = [];

            for (var attempt = 0; attempt < 10; attempt++)
            {
                var fetch = await reader.Read(profile, Timeline.Home, limit: 20, cancellationToken);
                posts = fetch.Items;

                if (posts.Any(post => post.Id == published.Id))
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }

            Assert.Contains(posts, post => post.Id == published.Id && post.Content == draft.Text);
        }
        finally
        {
            await author.Delete(profile, published.Id, cancellationToken);
        }
    }
}
