using Wooly.Core.Posts;

namespace Wooly.Tests.Core;

/// <summary>
///     The languages a post can say it is in, tested at the one place the <c>--language</c> flag, the config file's
///     <c>default_language</c> key and the TUI's list all ask — so a word one of them accepts is never one another
///     turns down.
/// </summary>
public class PostLanguageNameTests
{
    [Theory]
    [InlineData("fr", "fr")]
    [InlineData("de", "de")]

    // An ISO 639-3 code, which Mastodon takes for languages ISO 639-1 has no two-letter code for.
    [InlineData("tok", "tok")]

    // By its name in English, and by its own name — whichever the author thinks of it as.
    [InlineData("French", "fr")]
    [InlineData("Français", "fr")]
    [InlineData("Deutsch", "de")]

    // A user typing at a shell prompt, or by hand in a file, does not think about case.
    [InlineData("FR", "fr")]
    [InlineData("french", "fr")]
    [InlineData("FRANÇAIS", "fr")]
    [InlineData("  fr  ", "fr")]
    public void Parse_ReadsALanguageByItsCodeOrEitherOfItsNames(string name, string expected) =>
        Assert.Equal(expected, PostLanguageName.Parse(name)?.Code);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("xx")]
    [InlineData("Klingon")]

    // Only a whole code or a whole name is a language. A part of one is something the TUI's list offers to finish,
    // not a thing to publish.
    [InlineData("fre")]
    [InlineData("Fren")]
    public void Parse_RefusesWhatIsNotALanguage(string? name) => Assert.Null(PostLanguageName.Parse(name));

    [Fact]
    public void Of_NamesTheLanguageACodeSpellsInEnglishAndInItsOwnWords()
    {
        var french = PostLanguageName.Of("fr");

        Assert.NotNull(french);
        Assert.Equal("French", french.Name);
        Assert.Equal("Français", french.OwnName);
    }

    /// <summary>
    ///     A post read off an instance may carry a code this client does not list, and that is not an error — the instance
    ///     knew it — just nothing this client can name.
    /// </summary>
    [Fact]
    public void Of_KnowsNothingOfACodeItDoesNotList() => Assert.Null(PostLanguageName.Of("xx"));

    [Fact]
    public void All_ListsEveryLanguageOnceByCode() =>
        Assert.Equal(
            PostLanguageName.All.Count,
            PostLanguageName.All.Select(language => language.Code).Distinct().Count());

    [Fact]
    public void All_ReadsBackEveryLanguageByItsOwnCode() =>
        Assert.All(PostLanguageName.All, language =>
            Assert.Equal(language.Code, PostLanguageName.Parse(language.Code)?.Code));

    [Theory]
    [InlineData("en")]
    [InlineData("ja")]
    [InlineData("zh")]
    [InlineData("ckb")]
    [InlineData("kab")]
    public void All_HoldsTheLanguagesMastodonTakes(string code) =>
        Assert.Contains(PostLanguageName.All, language => language.Code == code);

    /// <summary>The TUI's list narrowing as an author types a code into it.</summary>
    [Fact]
    public void Matching_FindsTheLanguagesWhoseCodeBeginsWithWhatWasTyped()
    {
        var codes = PostLanguageName.Matching("f").Select(language => language.Code).ToList();

        Assert.Contains("fr", codes);
        Assert.Contains("fi", codes);
        Assert.DoesNotContain("de", codes);
    }

    /// <summary>And as one types a name, in English or in the language's own words.</summary>
    [Theory]
    [InlineData("fren", "fr")]
    [InlineData("FRAN", "fr")]
    [InlineData("deut", "de")]
    [InlineData("german", "de")]
    public void Matching_FindsTheLanguagesWhoseNameHoldsWhatWasTyped(string typed, string expected) =>
        Assert.Contains(PostLanguageName.Matching(typed), language => language.Code == expected);

    /// <summary>
    ///     A code typed in full is the language the author meant, so it leads the list rather than sitting wherever
    ///     its name happens to fall.
    /// </summary>
    [Fact]
    public void Matching_PutsTheLanguageWhoseCodeWasTypedInFullFirst() =>
        Assert.Equal("de", PostLanguageName.Matching("de").First().Code);

    [Fact]
    public void Matching_OffersEveryLanguageWhenNothingHasBeenTyped() =>
        Assert.Equal(PostLanguageName.All.Count, PostLanguageName.Matching(string.Empty).Count);

    [Fact]
    public void Matching_OffersNothingWhenNothingMatches() =>
        Assert.Empty(PostLanguageName.Matching("zzzz"));

    /// <summary>Too many languages to list, so the message says how one is spelled instead.</summary>
    [Fact]
    public void Rejection_SaysWhatWasTypedAndHowALanguageIsSpelled()
    {
        var rejection = PostLanguageName.Rejection("Klingon");

        Assert.Contains("'Klingon'", rejection);
        Assert.Contains("fr", rejection);
        Assert.Contains("French", rejection);
    }
}
