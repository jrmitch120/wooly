using Wooly.Core.Posts;

namespace Wooly.Tests.Core;

/// <summary>
///     The spellings of a visibility, tested at the one place both the <c>--visibility</c> flag and the config file's
///     <c>default_visibility</c> key ask for them — so a word this client accepts on the command line can never be one
///     it turns down in the file.
/// </summary>
public class PostVisibilityNameTests
{
    [Theory]
    [InlineData("public", PostVisibility.Public)]
    [InlineData("unlisted", PostVisibility.Unlisted)]
    [InlineData("followers", PostVisibility.Followers)]
    [InlineData("direct", PostVisibility.Direct)]

    // Mastodon's own word, which this client spelled unchanged before ADR-0024. Config files and scripts written then
    // say it, and keep working.
    [InlineData("private", PostVisibility.Followers)]
    [InlineData("PRIVATE", PostVisibility.Followers)]

    // A user typing a word at a shell prompt does not think about its case, and neither does one hand-editing a file.
    [InlineData("Followers", PostVisibility.Followers)]
    [InlineData("DIRECT", PostVisibility.Direct)]
    [InlineData("  followers  ", PostVisibility.Followers)]
    public void Parse_ReadsAVisibilityHoweverItWasSpelled(string name, PostVisibility expected) =>
        Assert.Equal(expected, PostVisibilityName.Parse(name));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("friends")]
    [InlineData("secret")]

    // What Enum.TryParse would have accepted and no user could have meant: the number behind the name, and a
    // comma-separated combination of names.
    [InlineData("2")]
    [InlineData("public,direct")]
    public void Parse_RefusesWhatIsNotAVisibility(string? name) => Assert.Null(PostVisibilityName.Parse(name));

    [Theory]
    [InlineData(PostVisibility.Public, "public")]
    [InlineData(PostVisibility.Unlisted, "unlisted")]
    [InlineData(PostVisibility.Followers, "followers")]
    [InlineData(PostVisibility.Direct, "direct")]
    public void Of_SpellsEachVisibilityInThisProjectsWords(PostVisibility visibility, string expected) =>
        Assert.Equal(expected, PostVisibilityName.Of(visibility));

    [Fact]
    public void Of_SpellsEveryVisibilityTheWayBothTheFlagAndTheFileTakeIt() =>
        Assert.All(Enum.GetValues<PostVisibility>(), visibility =>
            Assert.Equal(visibility, PostVisibilityName.Parse(PostVisibilityName.Of(visibility))));

    /// <summary>
    ///     With four to choose from, listing them is usually the whole answer. The alias is left out: offering both words
    ///     would invite the one this project avoids.
    /// </summary>
    [Fact]
    public void Rejection_ListsTheWordsThatWouldHaveWorked() =>
        Assert.Equal(
            "'friends' is not a post visibility. Use one of: public, unlisted, followers, direct.",
            PostVisibilityName.Rejection("friends"));
}
