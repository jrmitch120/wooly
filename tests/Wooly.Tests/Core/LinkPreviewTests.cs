using Wooly.Core.Posts;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Core;

/// <summary>
///     What a link preview says, as against how either surface lays it out: the name it goes by, and the rows
///     underneath it. Asserted here rather than once per surface, for the reason <see cref="PostMediaTests" /> asserts
///     an attachment's <c>Shows</c> here — the point of the sentence living on the preview is that the CLI and the TUI
///     cannot come to say different things about the same page (#125).
/// </summary>
public class LinkPreviewTests
{
    /// <summary>What the page calls itself, where the instance made something of it.</summary>
    [Fact]
    public void Name_IsWhatThePageCallsItself() =>
        Assert.Equal("Sheep, at length", APost.ALinkPreview().Name);

    /// <summary>
    ///     The site's own name stands in for a title the instance made nothing of, which is a name for the page
    ///     rather than a host for a reader to read one out of.
    /// </summary>
    [Fact]
    public void Name_StandsTheSitesNameInForATitleTheInstanceMadeNothingOf() =>
        Assert.Equal("Example News", APost.ALinkPreview(title: null).Name);

    /// <summary>
    ///     And the address itself where it named neither: there is always something to say, because reaching the page
    ///     is what a link preview is for (ADR-0018).
    /// </summary>
    [Fact]
    public void Name_FallsBackToTheAddressWhereTheInstanceNamedNeither() =>
        Assert.Equal(
            "https://example.com/sheep",
            APost.ALinkPreview(title: null, providerName: null).Name);

    /// <summary>
    ///     The same fallback stopping one short, for the surface that has already written the address on the row and
    ///     would otherwise write it twice: what the instance called the page, and nothing where it called it nothing.
    /// </summary>
    [Theory]
    [InlineData("Sheep, at length", "Example News", "Sheep, at length")]
    [InlineData(null, "Example News", "Example News")]
    [InlineData(null, null, null)]
    public void Called_IsTheTitleThenTheSiteAndNothingWhereTheInstanceNamedNeither(
        string? title,
        string? providerName,
        string? expected) =>
        Assert.Equal(expected, APost.ALinkPreview(title: title, providerName: providerName).Called);

    /// <summary>
    ///     What is said under the name, in the order it is said: the site, what the page is about, and who it says
    ///     wrote it — the byline as <c>by {author}</c> and never an address of its own (ADR-0018).
    /// </summary>
    [Fact]
    public void Says_NamesTheSiteThenTheDescriptionThenTheByline() =>
        Assert.Equal(
            ["Example News", "What a flock does all winter", "by Maria Shepherd"],
            APost.ALinkPreview().Says);

    /// <summary>
    ///     Except where the site's name is already the page's <see cref="LinkPreview.Name" />, which is a link preview
    ///     the instance sent no title with: it has been said once and once is enough.
    /// </summary>
    [Fact]
    public void Says_LeavesTheSiteOutWhereItStoodInForTheTitle() =>
        Assert.Equal(
            ["What a flock does all winter", "by Maria Shepherd"],
            APost.ALinkPreview(title: null).Says);

    /// <summary>Nothing at all for whatever the instance did not say, rather than a row of empty space for it.</summary>
    [Fact]
    public void Says_LeavesOutWhateverTheInstanceDidNotSay() =>
        Assert.Equal(
            ["Example News"],
            APost.ALinkPreview(description: null, author: null).Says);

    /// <summary>
    ///     And nothing whatever for a preview that is an address and no more — which still has a
    ///     <see cref="LinkPreview.Name" />, because the address is the whole of what it is offering.
    /// </summary>
    [Fact]
    public void Says_SaysNothingForAPreviewCarryingNothingButAnAddress() =>
        Assert.Empty(APost.ALinkPreview(title: null, description: null, providerName: null, author: null).Says);
}
