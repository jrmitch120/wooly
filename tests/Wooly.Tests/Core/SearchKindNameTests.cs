using Wooly.Core.Search;

namespace Wooly.Tests.Core;

/// <summary>
///     The spellings of a kind of result, tested at the one place every entry point asks for them — so a word
///     <c>--type</c> accepts can never be one the TUI's search prompt turns down.
/// </summary>
public class SearchKindNameTests
{
    [Theory]
    [InlineData("accounts", SearchKind.Accounts)]
    [InlineData("hashtags", SearchKind.Hashtags)]
    [InlineData("posts", SearchKind.Posts)]

    // A user typing a word at a shell prompt does not think about its case.
    [InlineData("Accounts", SearchKind.Accounts)]
    [InlineData("POSTS", SearchKind.Posts)]
    [InlineData("  hashtags  ", SearchKind.Hashtags)]
    public void Parse_ReadsAKindHoweverItWasSpelled(string name, SearchKind expected) =>
        Assert.Equal(expected, SearchKindName.Parse(name));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("people")]
    [InlineData("account")]

    // What Enum.TryParse would have accepted and no user could have meant: the number behind the name, and a
    // comma-separated combination of names.
    [InlineData("1")]
    [InlineData("accounts,posts")]

    // Asking for everything is what a search with no --type already does, so there is no word for it to be written a
    // second way by.
    [InlineData("everything")]
    public void Parse_RefusesWhatIsNotAKindAUserCanAskFor(string? name) => Assert.Null(SearchKindName.Parse(name));

    [Theory]
    [InlineData(SearchKind.Accounts)]
    [InlineData(SearchKind.Hashtags)]
    [InlineData(SearchKind.Posts)]
    public void Of_SpellsEveryKindTheWayTheOptionTakesIt(SearchKind kind) =>
        Assert.Equal(kind, SearchKindName.Parse(SearchKindName.Of(kind)));

    /// <summary>With three to choose from, listing them is the whole answer.</summary>
    [Fact]
    public void Rejection_ListsTheWordsThatWouldHaveWorked()
    {
        var rejection = SearchKindName.Rejection("people");

        Assert.Contains("people", rejection);
        Assert.Contains("accounts, hashtags, posts", rejection);
        Assert.DoesNotContain("everything", rejection);
    }
}
