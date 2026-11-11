using Microsoft.Extensions.DependencyInjection;
using Wooly.Core.Search;

namespace Wooly.Tests.Integration;

/// <summary>
///     Pages a narrowed search against the live instance (#599): what a real Mastodon makes of the <c>type</c>,
///     <c>limit</c> and <c>offset</c> the raw search sends, which Mastonet never sent. Accounts rather than posts,
///     because the suite's instance runs no Elasticsearch and so finds no post by its words; the seeded fans are the
///     accounts, nine of them sharing a name to page through. Nothing is published or deleted, so the suite's delete
///     throttle is untouched.
/// </summary>
[Trait("Category", "Integration")]
[Collection(LiveInstanceCollection.Name)]
public class InstanceSearchIntegrationTests
{
    private const int PerPage = 4;

    /// <summary>
    ///     Each page holds the fans past its offset, up to the limit — four, four, then the last one — which a search
    ///     whose offset was ignored would not: it would answer every page with the first four again. Counted rather
    ///     than compared by name, because an instance ranks equally good matches in no promised order.
    /// </summary>
    [Fact(Skip = LiveInstance.SkipReason, SkipType = typeof(LiveInstance), SkipUnless = nameof(LiveInstance.Available))]
    public async Task Find_PagesANarrowedSearchByItsOffset()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var search = LiveInstance.NewServices().GetRequiredService<IInstanceSearch>();
        var fans = LiveInstance.Fans.Count;

        for (var offset = 0; offset <= fans; offset += PerPage)
        {
            var page = await search.Find(
                LiveInstance.Profile,
                SearchQuery.For("woolyfan", SearchKind.Accounts, SearchPage.Of(PerPage, offset)),
                cancellationToken);

            Assert.Null(page.Hashtags);
            Assert.Null(page.Posts);
            Assert.Equal(Math.Min(PerPage, fans - offset), page.Accounts!.Count);
            Assert.All(page.Accounts, account => Assert.StartsWith("woolyfan", account.Address, StringComparison.Ordinal));
        }
    }
}
