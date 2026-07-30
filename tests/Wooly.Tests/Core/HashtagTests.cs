using Wooly.Core.Timelines;

namespace Wooly.Tests.Core;

/// <summary>
///     A hashtag goes into a request path rather than a query value, so what counts as one is a rule about safety as
///     much as about typos. Tested as the pure predicate it is, at the one place both the argument parser and the
///     domain ask it.
/// </summary>
public class HashtagTests
{
    [Theory]
    [InlineData("cats")]
    [InlineData("#cats")]
    [InlineData("  #cats  ")]
    [InlineData("caturday_2026")]

    // Letters are letters wherever they are from — a tag in Japanese is as much a tag as one in English.
    [InlineData("日本語")]
    public void IsWellFormed_AcceptsATagHoweverItWasSpelled(string hashtag) =>
        Assert.True(Hashtag.IsWellFormed(hashtag));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]

    // What is left of quoting the hash and then forgetting the tag.
    [InlineData("#")]

    // A phrase where a word belongs. Mastodon has no such tag, and the space would go into the path raw.
    [InlineData("two words")]

    // The reason this rule is not only about typos: slashes in a tag walk to a different endpoint, whose answer would
    // then be rendered as posts.
    [InlineData("../../v1/accounts")]
    [InlineData("cats/../../instance")]
    [InlineData("cats?limit=1")]
    public void IsWellFormed_RefusesWhatIsNotOneWord(string? hashtag) =>
        Assert.False(Hashtag.IsWellFormed(hashtag));

    [Theory]
    [InlineData("cats", "cats")]
    [InlineData("#cats", "cats")]
    [InlineData("  #cats  ", "cats")]
    public void Bare_StripsWhatAnInstanceDoesNotWant(string hashtag, string expected) =>
        Assert.Equal(expected, Hashtag.Bare(hashtag));

    /// <summary>
    ///     The domain's own guard. A caller is expected to have rejected the value already, so reaching here with one is
    ///     a defect rather than something to report to a user — but it is emphatically not something to pass on to an
    ///     instance.
    /// </summary>
    [Fact]
    public void Tag_RefusesAHashtagThatWouldWalkOutOfTheTagEndpoint()
    {
        var exception = Assert.Throws<ArgumentException>(() => Timeline.Tag("../../v1/accounts"));

        Assert.Contains("../../v1/accounts", exception.Message);
    }
}
