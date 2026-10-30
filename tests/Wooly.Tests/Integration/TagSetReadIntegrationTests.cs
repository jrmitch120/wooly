using Microsoft.Extensions.DependencyInjection;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;
using Wooly.Core.Timelines;

namespace Wooly.Tests.Integration;

/// <summary>
///     Reads a <b>Tag set</b> against the live instance (ADR-0033, #511): what a real Mastodon answers the raw tag
///     timeline with <c>any[]</c> is the union of the set's tags — all four of a full set's, in one ask — and not the
///     posts carrying only the path tag, nor any carrying a tag the set does not hold. The tags are this run's own, so
///     nothing else on the instance carries them.
/// </summary>
[Trait("Category", "Integration")]
[Collection(LiveInstanceCollection.Name)]
public class TagSetReadIntegrationTests
{
    [Fact(Skip = LiveInstance.SkipReason, SkipType = typeof(LiveInstance), SkipUnless = nameof(LiveInstance.Available))]
    public async Task Read_HoldsThePostsCarryingEachOfTheSetsFourTags_AndNoOthers()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var services = LiveInstance.NewServices();
        var profile = LiveInstance.Profile;
        var author = services.GetRequiredService<IPostAuthor>();
        var reader = services.GetRequiredService<ITimelineReader>();

        // Letters only after the run's mark, so that no tag is all digits, which Mastodon does not read as a tag.
        var run = $"ws{Guid.NewGuid():N}"[..14];
        string[] tags = [$"{run}a", $"{run}b", $"{run}c", $"{run}d"];
        var outside = $"{run}z";
        var set = TagSet.Of("integration", tags);

        var published = new List<Post>();

        try
        {
            foreach (var tag in tags)
            {
                published.Add(await Publish(author, profile, $"wooly tag set test #{tag}", cancellationToken));
            }

            // One post carrying two of the set's tags, which the union holds once.
            var both = await Publish(author, profile, $"wooly tag set test #{tags[1]} #{tags[3]}", cancellationToken);
            published.Add(both);

            // And one carrying a tag the set does not hold, which it never shows.
            var stranger = await Publish(author, profile, $"wooly tag set test #{outside}", cancellationToken);
            published.Add(stranger);

            var wanted = published.Where(post => post.Id != stranger.Id).Select(post => post.Id).ToHashSet();
            IReadOnlyList<Post> posts = [];

            // A tag is attached to a post as it is created, but the read is polled for the same reason the home
            // timeline's is: a live instance is allowed to be a moment behind what it has just been told.
            for (var attempt = 0; attempt < 10; attempt++)
            {
                posts = (await reader.Read(profile, Timeline.Of(set), limit: 40, cancellationToken)).Items;

                if (wanted.IsSubsetOf(posts.Select(post => post.Id)))
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }

            Assert.Equal(
                wanted.Order(StringComparer.Ordinal),
                posts.Select(post => post.Id).Order(StringComparer.Ordinal));
            Assert.Single(posts, post => post.Id == both.Id);
            Assert.Equal(posts.Select(post => post.Id), posts.Select(post => post.Id).OrderByDescending(Id));
        }
        finally
        {
            foreach (var post in published)
            {
                await author.Delete(profile, post.Id, cancellationToken);
            }
        }
    }

    /// <summary>A post id as a number, newest the largest, which is how a timeline orders them.</summary>
    private static ulong Id(string id) => ulong.Parse(id, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>A public post saying <paramref name="text" />, so that it stands on its tags' timelines.</summary>
    private static Task<Post> Publish(
        IPostAuthor author,
        ActiveProfile profile,
        string text,
        CancellationToken cancellationToken) =>
        author.Publish(
            profile,
            new PostDraft { Text = text, Visibility = PostVisibility.Public, VisibilityChosen = true },
            cancellationToken);
}
