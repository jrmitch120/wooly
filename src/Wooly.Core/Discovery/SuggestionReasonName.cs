namespace Wooly.Core.Discovery;

/// <summary>
///     How a <see cref="SuggestionReason" /> is spelled at each of the two edges it is spelled at: the source string an
///     instance sends, and the heading a reader is shown. Alongside <see cref="Search.SearchKindName" /> and
///     <see cref="Posts.PostVisibilityName" />, and for the same reason — one place, so the two edges cannot come to
///     disagree about how many reasons there are.
///     <para>
///         This is the one place the five headings are written. The Discover screen reads them from here rather than
///         holding its own copy, because a heading is the whole of what makes a weak reason readable <i>as</i> weak,
///         and two spellings of "most followed here" is one of them going stale.
///     </para>
///     Both spellings are written out rather than derived from the member's name. The source could be snake-cased out
///     of it and the heading could not, and deriving one of the two would make a member renamed here quietly stop
///     matching what an instance sends.
/// </summary>
public static class SuggestionReasonName
{
    /// <summary>
    ///     The five, each with the word an instance sends it under and the heading a reader sees. In the enum's own
    ///     order, which is the rank a screen draws its sections in.
    /// </summary>
    /// <remarks>
    ///     The headings are phrases and not sentences, because a screen draws a count in front of one —
    ///     <c>11 followed by people you follow</c> — and a heading that read as a sentence would not take a number.
    /// </remarks>
    private static readonly (SuggestionReason Reason, string Source, string Heading)[] Named =
    [
        (SuggestionReason.FriendsOfFriends, "friends_of_friends", "followed by people you follow"),
        (SuggestionReason.SimilarToRecentlyFollowed, "similar_to_recently_followed", "like people you followed lately"),
        (SuggestionReason.Featured, "featured", "featured by this instance"),
        (SuggestionReason.MostInteractions, "most_interactions", "talked with most here"),
        (SuggestionReason.MostFollowed, "most_followed", "most followed here"),
    ];

    /// <summary>The heading a section of <paramref name="reason" /> is drawn under, without the count in front of it.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     <paramref name="reason" /> is not one of the five. Thrown rather than answered, because this end of the
    ///     mapping only ever takes a value this client made: an unnamed <i>source</i> is an instance being newer than
    ///     this client and is answered for by <see cref="Parse" />, but an unnamed <i>reason</i> is a member added to
    ///     the enum and never added to the table.
    /// </exception>
    public static string Of(SuggestionReason reason)
    {
        foreach (var named in Named)
        {
            if (named.Reason == reason)
            {
                return named.Heading;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(reason), reason, "Not a reason this client suggests anybody for.");
    }

    /// <summary>
    ///     The reason <paramref name="source" /> names, or <see langword="null" /> where it names none of them — which
    ///     is an instance offering a source newer than this client, and not an error. Mastodon keeps adding sources,
    ///     and a person offered under a word this client has no heading for is still a person worth showing.
    /// </summary>
    /// <remarks>
    ///     Matched case-insensitively against the table by hand rather than through
    ///     <see cref="Enum.TryParse{TEnum}(string, out TEnum)" />, which would read neither the wire's snake case nor
    ///     the underlying numbers correctly — and which would have to be told that a value it does not recognize is
    ///     ordinary here rather than exceptional.
    /// </remarks>
    public static SuggestionReason? Parse(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        var trimmed = source.Trim();

        foreach (var named in Named)
        {
            if (string.Equals(named.Source, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return named.Reason;
            }
        }

        return null;
    }
}
