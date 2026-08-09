using Microsoft.Extensions.DependencyInjection;
using Wooly.Core.Posts;

namespace Wooly.Tests.Integration;

/// <summary>
///     Runs <see cref="PostAuthor" /> against the live instance: publishes a post and reads back exactly what
///     <see cref="IPostAuthor.Publish" /> promises — the post as the instance actually published it, not as this
///     client asked for it (#33).
/// </summary>
[Trait("Category", "Integration")]
[Collection(LiveInstanceCollection.Name)]
public class PostAuthorIntegrationTests
{
    [Fact(Skip = LiveInstance.SkipReason, SkipType = typeof(LiveInstance), SkipUnless = nameof(LiveInstance.Available))]
    public async Task Publish_ReturnsThePostAsTheInstancePublishedIt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var services = LiveInstance.NewServices();
        var profile = LiveInstance.Profile;
        var author = services.GetRequiredService<IPostAuthor>();

        var text = $"wooly integration test {Guid.NewGuid():N}";
        var draft = new PostDraft { Text = text, Visibility = PostVisibility.Unlisted, VisibilityChosen = true };

        var published = await author.Publish(profile, draft, cancellationToken);

        try
        {
            Assert.Equal(text, published.Content);
            Assert.Equal(PostVisibility.Unlisted, published.Visibility);
            Assert.Equal($"{LiveInstance.Username}@{profile.Instance}", published.Account);
            Assert.False(published.IsBoost);
        }
        finally
        {
            await author.Delete(profile, published.Id, cancellationToken);
        }
    }
}
