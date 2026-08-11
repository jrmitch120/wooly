using Wooly.Core.Posts;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Core;

/// <summary>
///     Who a handle written in a post's text is, as far as the post itself says (#85) — the lookup that lets a reader
///     walk to a mention and open the account without the shell asking an instance anything.
/// </summary>
public class PostMentionsTests
{
    private static readonly string[] Named = ["maria@fosstodon.org", "ben@hachyderm.io"];

    /// <summary>A handle written in full is the account it spells, whichever way it was capitalised.</summary>
    [Theory]
    [InlineData("@maria@fosstodon.org")]
    [InlineData("maria@fosstodon.org")]
    [InlineData("@Maria@FossTodon.org")]
    public void Named_ReadsAHandleWrittenInFull(string written) =>
        Assert.Equal("maria@fosstodon.org", PostMentions.Named(APost.With(mentions: Named), written));

    /// <summary>
    ///     And a handle written bare is whoever the post names by that username — which is the case that matters,
    ///     since a mention flattened out of an instance's HTML is usually just <c>@maria</c>.
    /// </summary>
    [Fact]
    public void Named_ReadsABareUsernameAsWhoeverThePostNamesByIt() =>
        Assert.Equal("maria@fosstodon.org", PostMentions.Named(APost.With(mentions: Named), "@maria"));

    /// <summary>
    ///     A handle the post does not name is nobody. Guessing an instance for it would open somebody else's account
    ///     under somebody's name.
    /// </summary>
    [Theory]
    [InlineData("@nobody")]
    [InlineData("@maria@mastodon.social")]
    [InlineData("@")]
    [InlineData("")]
    public void Named_ReadsAHandleThePostDoesNotNameAsNobody(string written) =>
        Assert.Null(PostMentions.Named(APost.With(mentions: Named), written));

    /// <summary>A post that names nobody resolves nothing, which is most posts.</summary>
    [Fact]
    public void Named_ReadsNothingOutOfAPostThatNamesNobody() =>
        Assert.Null(PostMentions.Named(APost.With(), "@maria"));

    /// <summary>
    ///     A boost carries no text of its own, so the handles walked are the boosted post's — and so are the accounts
    ///     they name.
    /// </summary>
    [Fact]
    public void Named_ReadsABoostsHandlesOffThePostItBoosts()
    {
        var boost = APost.With(id: "220", boosted: APost.With(id: "110", mentions: Named));

        Assert.Equal("ben@hachyderm.io", PostMentions.Named(boost, "@ben"));
    }
}
