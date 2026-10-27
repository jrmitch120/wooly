using Wooly.Core.Posts;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Core;

/// <summary>
///     Why a post's actors list fewer people than the post counts (#491): an instance lists only the favorites and boosts
///     it knows of, and a remote post's are delivered to its home instance — so the list says where the rest are.
/// </summary>
public class ActorShortfallTests
{
    [Fact]
    public void Of_ARemotePostListedShortSaysWhoKnowsOfHowManyAndWhereTheRestAre()
    {
        var post = APost.With(account: "miguel@mastodon.social") with { Favorites = 140 };

        var shortfall = ActorShortfall.Of(post, ActorSide.Favorited, known: 1, reader: "mastodon.online");

        Assert.Equal(
            "mastodon.online knows of 1 of 140 favorites. The rest are on mastodon.social.",
            shortfall?.Says);
        Assert.Equal("mastodon.social", shortfall?.Home);
    }

    [Fact]
    public void Of_ALocalPostListedShortSaysHowManyCanBeListed()
    {
        var post = APost.With(account: "jeff@mastodon.social") with { Boosts = 3 };

        var shortfall = ActorShortfall.Of(post, ActorSide.Boosted, known: 1, reader: "mastodon.social");

        Assert.Equal("1 of 3 boosts can be listed.", shortfall?.Says);
        Assert.Null(shortfall?.Home);
    }

    /// <summary>Nobody known of is "none", and a count of one is a favorite rather than favorites.</summary>
    [Theory]
    [InlineData("ben@hachyderm.io", 1, "mastodon.social knows of none of 1 favorite. The rest are on hachyderm.io.")]
    [InlineData("jeff@mastodon.social", 1_234, "None of 1,234 favorites can be listed.")]
    public void Of_NobodyKnownOfSaysNone(string author, long favorites, string says)
    {
        var post = APost.With(account: author) with { Favorites = favorites };

        Assert.Equal(says, ActorShortfall.Of(post, ActorSide.Favorited, known: 0, reader: "mastodon.social")?.Says);
    }

    /// <summary>Nothing is short where everybody counted is listed, or more than were counted — a count can be stale.</summary>
    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    public void Of_AListAsLongAsTheCountIsNotShort(int known)
    {
        var post = APost.With(account: "ben@hachyderm.io") with { Favorites = 5 };

        Assert.Null(ActorShortfall.Of(post, ActorSide.Favorited, known, reader: "mastodon.social"));
    }

    /// <summary>The instance is matched however it was cased, as an address is.</summary>
    [Fact]
    public void Of_APostOnTheReadersOwnInstanceIsLocalHoweverItIsCased()
    {
        var post = APost.With(account: "jeff@Mastodon.Social") with { Favorites = 2 };

        Assert.Null(ActorShortfall.Of(post, ActorSide.Favorited, known: 1, reader: "mastodon.social")?.Home);
    }
}
