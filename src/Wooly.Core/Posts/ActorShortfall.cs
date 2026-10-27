namespace Wooly.Core.Posts;

/// <summary>
///     Why one side of a post's <b>Actors</b>, read to its end, lists fewer people than the post counts (#491). An
///     instance lists only the favorites and boosts it knows of, and Mastodon delivers each to the post's home instance
///     alone — so a remote post seen from the reader's instance lists only the people on it, however many the count
///     says. Said once here, so that the screen and the command line give the same reason in the same words.
/// </summary>
/// <param name="Side">Which of the post's lists is short.</param>
/// <param name="Known">How many the reader's instance listed.</param>
/// <param name="Counted">How many the post counts on that side.</param>
/// <param name="Reader">The reader's instance, which is the one that listed them.</param>
/// <param name="Home">
///     The post's home instance, where the rest are — or <see langword="null" /> for a post on the reader's own instance,
///     whose list is short for its own reasons: accounts deleted or suspended since, or ones the reader blocks.
/// </param>
public sealed record ActorShortfall(ActorSide Side, int Known, long Counted, string Reader, string? Home)
{
    /// <summary>
    ///     How <paramref name="post" />'s <paramref name="side" /> falls short, or nothing where <paramref name="known" />
    ///     is as many as it counts — or more, a count being able to be stale.
    /// </summary>
    /// <param name="post">The post, never the boost carrying it, with the counts it carried when it was asked about.</param>
    /// <param name="side">Which of its lists.</param>
    /// <param name="known">How many the side was read to hold, to its end.</param>
    /// <param name="reader">The reader's instance.</param>
    public static ActorShortfall? Of(Post post, ActorSide side, int known, string reader)
    {
        var counted = side.Either(post.Favorites, post.Boosts);

        return known >= counted ? null : new ActorShortfall(side, known, counted, reader, HomeOf(post, reader));
    }

    /// <summary>
    ///     The instance <paramref name="post" /> lives on, where its favorites and boosts are delivered — or nothing where
    ///     that is <paramref name="reader" />'s own, matched however either was cased, as an address is.
    /// </summary>
    public static string? HomeOf(Post post, string reader)
    {
        // Post.Account is always username@instance, so what follows the last @ is where the post lives.
        var home = post.Account[(post.Account.LastIndexOf('@') + 1)..];

        return string.Equals(home, reader, StringComparison.OrdinalIgnoreCase) ? null : home;
    }

    /// <summary>
    ///     The reason, in a sentence: "mastodon.online knows of 1 of 140 favorites. The rest are on mastodon.social." for
    ///     a remote post, and "1 of 3 boosts can be listed." for a local one.
    /// </summary>
    public string Says
    {
        get
        {
            var of = $"{(Known == 0 ? "none" : Number.Of(Known))} of {Number.Of(Counted)} {What}";

            return Home is { } home
                ? $"{Reader} knows of {of}. The rest are on {home}."
                : $"{char.ToUpperInvariant(of[0])}{of[1..]} can be listed.";
        }
    }

    /// <summary>What was counted, by the count: "favorites", or "favorite" where it was one.</summary>
    private string What => Side.Either("favorite", "boost") + (Counted == 1 ? string.Empty : "s");
}
