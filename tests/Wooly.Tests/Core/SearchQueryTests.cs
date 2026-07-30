using Wooly.Core.Search;

namespace Wooly.Tests.Core;

/// <summary>
///     The rule that a search has to be for something, tested where every entry point asks it — the command turns a
///     blank query down against what the user typed, and the query itself cannot be built from one either.
/// </summary>
public class SearchQueryTests
{
    [Fact]
    public void For_KeepsWhatIsBeingLookedForAndTheKindWanted()
    {
        var query = SearchQuery.For("cats", SearchKind.Hashtags);

        Assert.Equal("cats", query.Text);
        Assert.Equal(SearchKind.Hashtags, query.Kind);
    }

    /// <summary>A search that named no kind wants all three, which is the point of there being one command.</summary>
    [Fact]
    public void For_LooksForEverythingWhenNoKindWasNamed() =>
        Assert.Equal(SearchKind.Everything, SearchQuery.For("cats").Kind);

    /// <summary>A shell leaves whitespace around a quoted argument, and an instance would search for it.</summary>
    [Fact]
    public void For_TrimsWhatAShellLeftAroundTheQuery() => Assert.Equal("cats", SearchQuery.For("  cats  ").Text);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void For_RefusesAQueryWithNothingToLookFor(string text) =>
        Assert.Throws<ArgumentException>(() => SearchQuery.For(text));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\t ")]
    public void IsWellFormed_SaysNoToAQueryWithNothingToLookFor(string? text) =>
        Assert.False(SearchQuery.IsWellFormed(text));

    [Theory]
    [InlineData("cats")]
    [InlineData("#cats")]
    [InlineData("@alice@hachyderm.io")]
    [InlineData("https://mastodon.social/@jeff/110")]
    public void IsWellFormed_SaysYesToAnythingAnInstanceCanBeAskedToLookFor(string text) =>
        Assert.True(SearchQuery.IsWellFormed(text));
}
