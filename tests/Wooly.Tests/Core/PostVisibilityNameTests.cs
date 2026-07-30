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
    [InlineData("private", PostVisibility.Private)]
    [InlineData("direct", PostVisibility.Direct)]

    // A user typing a word at a shell prompt does not think about its case, and neither does one hand-editing a file.
    [InlineData("Private", PostVisibility.Private)]
    [InlineData("DIRECT", PostVisibility.Direct)]
    [InlineData("  private  ", PostVisibility.Private)]
    public void Parse_ReadsAVisibilityHoweverItWasSpelled(string name, PostVisibility expected) =>
        Assert.Equal(expected, PostVisibilityName.Parse(name));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("followers")]
    [InlineData("secret")]

    // What Enum.TryParse would have accepted and no user could have meant: the number behind the name, and a
    // comma-separated combination of names.
    [InlineData("2")]
    [InlineData("public,direct")]
    public void Parse_RefusesWhatIsNotAVisibility(string? name) => Assert.Null(PostVisibilityName.Parse(name));

    [Fact]
    public void Of_SpellsEveryVisibilityTheWayBothTheFlagAndTheFileTakeIt() =>
        Assert.All(Enum.GetValues<PostVisibility>(), visibility =>
            Assert.Equal(visibility, PostVisibilityName.Parse(PostVisibilityName.Of(visibility))));

    /// <summary>With four to choose from, listing them is usually the whole answer.</summary>
    [Fact]
    public void Rejection_ListsTheWordsThatWouldHaveWorked()
    {
        var rejection = PostVisibilityName.Rejection("followers");

        Assert.Contains("followers", rejection);
        Assert.Contains("public", rejection);
        Assert.Contains("direct", rejection);
    }
}
