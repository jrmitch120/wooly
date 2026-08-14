using Microsoft.Extensions.DependencyInjection;
using Wooly.Core.Posts;

namespace Wooly.Tests.Integration;

/// <summary>
///     Runs <see cref="PostEngagement" /> against the live instance: boosts and favorites a post of the seeded
///     account's own, then takes each mark back off, and votes in a poll of its own — checking the instance's own
///     answer at every step rather than assuming a write that did not throw took (#33).
/// </summary>
[Trait("Category", "Integration")]
[Collection(LiveInstanceCollection.Name)]
public class PostEngagementIntegrationTests
{
    [Fact(Skip = LiveInstance.SkipReason, SkipType = typeof(LiveInstance), SkipUnless = nameof(LiveInstance.Available))]
    public async Task Mark_BoostsAndUnboostsAPost()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var services = LiveInstance.NewServices();
        var profile = LiveInstance.Profile;
        var author = services.GetRequiredService<IPostAuthor>();
        var engagement = services.GetRequiredService<IPostEngagement>();

        var published = await author.Publish(profile, LiveInstance.ThrowawayDraft(), cancellationToken);

        try
        {
            var boosted = await engagement.Mark(profile, published.Id, PostMark.Boost, wanted: true, cancellationToken);
            Assert.Equal(published.Id, boosted.Id);
            Assert.True(boosted.Marks.Boosted);

            var unboosted = await engagement.Mark(profile, published.Id, PostMark.Boost, wanted: false, cancellationToken);
            Assert.False(unboosted.Marks.Boosted);
        }
        finally
        {
            await author.Delete(profile, published.Id, cancellationToken);
        }
    }

    [Fact(Skip = LiveInstance.SkipReason, SkipType = typeof(LiveInstance), SkipUnless = nameof(LiveInstance.Available))]
    public async Task Mark_FavoritesAndUnfavoritesAPost()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var services = LiveInstance.NewServices();
        var profile = LiveInstance.Profile;
        var author = services.GetRequiredService<IPostAuthor>();
        var engagement = services.GetRequiredService<IPostEngagement>();

        var published = await author.Publish(profile, LiveInstance.ThrowawayDraft(), cancellationToken);

        try
        {
            var favorited = await engagement.Mark(profile, published.Id, PostMark.Favorite, wanted: true, cancellationToken);
            Assert.True(favorited.Marks.Favorited);

            var unfavorited = await engagement.Mark(profile, published.Id, PostMark.Favorite, wanted: false, cancellationToken);
            Assert.False(unfavorited.Marks.Favorited);
        }
        finally
        {
            await author.Delete(profile, published.Id, cancellationToken);
        }
    }

    /// <summary>
    ///     The one thing about a vote that can only be seen against a real instance: that it is the poll's own id the
    ///     endpoint takes, and that what comes back is the whole poll rather than the post around it (#87). Cast in a
    ///     poll of the seeded account's own, which Mastodon allows, and taken down with the post afterwards.
    /// </summary>
    [Fact(Skip = LiveInstance.SkipReason, SkipType = typeof(LiveInstance), SkipUnless = nameof(LiveInstance.Available))]
    public async Task Vote_CastsAVoteInThePollOnAPost()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var services = LiveInstance.NewServices();
        var profile = LiveInstance.Profile;
        var author = services.GetRequiredService<IPostAuthor>();
        var engagement = services.GetRequiredService<IPostEngagement>();

        var draft = LiveInstance.ThrowawayDraft() with
        {
            Poll = PollDraft.Of(["Cats", "Dogs"], TimeSpan.FromMinutes(30)),
        };

        var published = await author.Publish(profile, draft, cancellationToken);

        try
        {
            var voted = await engagement.Vote(profile, published, [1], cancellationToken);

            Assert.Equal(published.Id, voted.Id);
            Assert.True(voted.Poll?.Voted);
            Assert.Equal([false, true], voted.Poll?.Options.Select(option => option.Picked));
            Assert.Equal(1, voted.Poll?.Votes);
        }
        finally
        {
            await author.Delete(profile, published.Id, cancellationToken);
        }
    }
}
