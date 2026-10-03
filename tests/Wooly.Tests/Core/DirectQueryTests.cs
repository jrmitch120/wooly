using Wooly.Core.Search;

namespace Wooly.Tests.Core;

/// <summary>
///     What is typed into search when it names one thing rather than asks after something. Only hashtags so far: a tag needs no
///     asking, since any well-formed tag has a timeline.
/// </summary>
public class DirectQueryTests
{
    [Theory]
    [InlineData("#cats", "cats")]
    [InlineData("  #cats  ", "cats")]
    [InlineData("#caturday_2026", "caturday_2026")]

    // A tag in another script is as well-formed a tag as one in English, so it is as direct.
    [InlineData("#日本語", "日本語")]
    [InlineData("#γάτες", "γάτες")]
    public void Tag_NamesTheTagAHashtagIsDirectFor(string typed, string tag) =>
        Assert.Equal(tag, DirectQuery.Tag(typed));

    [Theory]
    [InlineData("")]

    // A word with no sign on it is how a reader asks for everything that matches it.
    [InlineData("cats")]

    // A phrase is asked after, not named.
    [InlineData("#cats dogs")]

    // What is left of starting a tag and not finishing it.
    [InlineData("#")]

    // Two hashes is not how a tag is written, though the timeline's rule would strip both.
    [InlineData("##cats")]

    // Not one word, so not a tag the timeline would take.
    [InlineData("#cats/../../instance")]

    // A handle is direct too, but it names an account rather than a tag.
    [InlineData("@alice")]
    public void Tag_IsNothingForWhatIsSearchedFor(string typed) => Assert.Null(DirectQuery.Tag(typed));
}
