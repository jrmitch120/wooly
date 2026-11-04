using Microsoft.Extensions.DependencyInjection;
using Wooly.Core.Posts;
using Wooly.Core.Timelines;

namespace Wooly.Tests.Integration;

/// <summary>
///     Runs <see cref="PostEngagement" /> against the live instance: boosts, favorites and bookmarks a post of the
///     seeded account's own, then takes each mark back off, and votes in a poll of its own — checking the instance's own
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

    /// <summary>
    ///     Read back from the <b>Favorites</b> run between the two (#562), newest favorite first, which is where drift
    ///     between Mastonet's <c>GetFavourites</c> and the live endpoint would show. In this test rather than one of its
    ///     own, because every post a test publishes is one more delete against the instance's throttle on deletes.
    /// </summary>
    [Fact(Skip = LiveInstance.SkipReason, SkipType = typeof(LiveInstance), SkipUnless = nameof(LiveInstance.Available))]
    public async Task Mark_FavoritesAndUnfavoritesAPost()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var services = LiveInstance.NewServices();
        var profile = LiveInstance.Profile;
        var author = services.GetRequiredService<IPostAuthor>();
        var engagement = services.GetRequiredService<IPostEngagement>();
        var timelines = services.GetRequiredService<ITimelineReader>();

        var published = await author.Publish(profile, LiveInstance.ThrowawayDraft(), cancellationToken);

        try
        {
            var favorited = await engagement.Mark(profile, published.Id, PostMark.Favorite, wanted: true, cancellationToken);
            Assert.True(favorited.Marks.Favorited);

            var favorites = await timelines.Read(profile, Timeline.Favorites, limit: 20, cancellationToken);
            Assert.Equal(published.Id, favorites.Items.FirstOrDefault()?.Id);

            var unfavorited = await engagement.Mark(profile, published.Id, PostMark.Favorite, wanted: false, cancellationToken);
            Assert.False(unfavorited.Marks.Favorited);
        }
        finally
        {
            await author.Delete(profile, published.Id, cancellationToken);
        }
    }

    /// <summary>
    ///     That the bookmark endpoints are where Mastonet says they are, and that the instance's <c>bookmarked</c> field
    ///     comes back on the post it answers with in both directions (#560); and that the <b>Bookmarks</b> run holds the
    ///     post, newest bookmark first, until the bookmark is taken off (#562).
    /// </summary>
    [Fact(Skip = LiveInstance.SkipReason, SkipType = typeof(LiveInstance), SkipUnless = nameof(LiveInstance.Available))]
    public async Task Mark_BookmarksAndUnbookmarksAPost()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var services = LiveInstance.NewServices();
        var profile = LiveInstance.Profile;
        var author = services.GetRequiredService<IPostAuthor>();
        var engagement = services.GetRequiredService<IPostEngagement>();
        var timelines = services.GetRequiredService<ITimelineReader>();

        var published = await author.Publish(profile, LiveInstance.ThrowawayDraft(), cancellationToken);

        try
        {
            var bookmarked = await engagement.Mark(profile, published.Id, PostMark.Bookmark, wanted: true, cancellationToken);
            Assert.Equal(published.Id, bookmarked.Id);
            Assert.True(bookmarked.Marks.Bookmarked);

            var shown = await engagement.Show(profile, published.Id, cancellationToken);
            Assert.True(shown.Marks.Bookmarked);

            var kept = await timelines.Read(profile, Timeline.Bookmarks, limit: 20, cancellationToken);
            Assert.Equal(published.Id, kept.Items.FirstOrDefault()?.Id);
            Assert.True(kept.Items[0].Marks.Bookmarked);

            var unbookmarked = await engagement.Mark(
                profile,
                published.Id,
                PostMark.Bookmark,
                wanted: false,
                cancellationToken);
            Assert.False(unbookmarked.Marks.Bookmarked);

            var after = await timelines.Read(profile, Timeline.Bookmarks, limit: 20, cancellationToken);
            Assert.DoesNotContain(after.Items, post => post.Id == published.Id);
        }
        finally
        {
            await author.Delete(profile, published.Id, cancellationToken);
        }
    }

    /// <summary>
    ///     The one thing about a vote that can only be seen against a real instance: that it is the poll's own id the
    ///     endpoint takes, and that what comes back is the whole poll rather than the post around it (#87). Mastodon
    ///     refuses a vote cast in the author's own name ("You cannot vote in your own polls"), so the poll is
    ///     published as <see cref="LiveInstance.Profile" /> and voted in as <see cref="LiveInstance.VoterProfile" />,
    ///     then taken down by the account that published it.
    /// </summary>
    [Fact(Skip = LiveInstance.SkipReason, SkipType = typeof(LiveInstance), SkipUnless = nameof(LiveInstance.Available))]
    public async Task Vote_CastsAVoteInThePollOnAPost()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var services = LiveInstance.NewServices();
        var profile = LiveInstance.Profile;
        var voter = LiveInstance.VoterProfile;
        var author = services.GetRequiredService<IPostAuthor>();
        var engagement = services.GetRequiredService<IPostEngagement>();

        var draft = LiveInstance.ThrowawayDraft() with
        {
            Poll = PollDraft.Of(["Cats", "Dogs"], TimeSpan.FromMinutes(30)),
        };

        var published = await author.Publish(profile, draft, cancellationToken);

        try
        {
            var voted = await engagement.Vote(voter, published, [1], cancellationToken);

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

    /// <summary>
    ///     A post's two lists of actors, against a real instance (#488): favorited and boosted by a second account, the
    ///     post lists that account on each side — read from the endpoint each side names, through the account mapping.
    /// </summary>
    [Fact(Skip = LiveInstance.SkipReason, SkipType = typeof(LiveInstance), SkipUnless = nameof(LiveInstance.Available))]
    public async Task ReadActors_ListsWhoFavoritedAndWhoBoostedAPost()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var services = LiveInstance.NewServices();
        var profile = LiveInstance.Profile;
        var other = LiveInstance.VoterProfile;
        var author = services.GetRequiredService<IPostAuthor>();
        var engagement = services.GetRequiredService<IPostEngagement>();

        var published = await author.Publish(profile, LiveInstance.ThrowawayDraft(), cancellationToken);

        try
        {
            await engagement.Mark(other, published.Id, PostMark.Favorite, wanted: true, cancellationToken);
            await engagement.Mark(other, published.Id, PostMark.Boost, wanted: true, cancellationToken);

            var favorited = await engagement.ReadActors(profile, published.Id, ActorSide.Favorited, 80, cancellationToken);
            var boosted = await engagement.ReadActors(profile, published.Id, ActorSide.Boosted, 80, cancellationToken);

            Assert.StartsWith($"{LiveInstance.VoterUsername}@", Assert.Single(favorited.Items).Address);
            Assert.StartsWith($"{LiveInstance.VoterUsername}@", Assert.Single(boosted.Items).Address);
            Assert.True(favorited.IsWhole);
        }
        finally
        {
            await author.Delete(profile, published.Id, cancellationToken);
        }
    }
}
