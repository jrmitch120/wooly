namespace Wooly.Core.Discovery;

/// <summary>
///     Why an instance is offering somebody to follow — the whole of what makes a <see cref="Suggestion" /> worth
///     having, since a bare list of strangers is a list of strangers and "followed by people you follow" is an
///     argument (CONTEXT.md).
///     <para>
///         Five members, being the five sources Mastodon's <c>/v2/suggestions</c> documents. Unlike
///         <see cref="Notifications.NotificationKind" />, a source this client has never heard of is not kept under the
///         instance's own word: a reason is only ever shown as one of the headings
///         <see cref="SuggestionReasonName" /> spells, and an unknown source has no heading to be shown under. It is
///         dropped and the person is kept — see <see cref="Suggestion.Reasons" />.
///     </para>
///     Declared in the order a screen ranks them, best reason first, so that the fixed rank the Discover screen draws
///     its sections in is the order the enum already reads in rather than a second list to keep in step with this one.
/// </summary>
public enum SuggestionReason
{
    /// <summary>Followed by accounts this profile follows — the one reason that is really an argument.</summary>
    FriendsOfFriends,

    /// <summary>Like the accounts this profile has followed lately.</summary>
    SimilarToRecentlyFollowed,

    /// <summary>Put forward by the instance's own staff. Not the <b>Pinned</b> sense of the word (CONTEXT.md).</summary>
    Featured,

    /// <summary>Among the accounts talked with most on this instance.</summary>
    MostInteractions,

    /// <summary>Among the accounts most followed on this instance — the weakest of the five, and still worth a heading.</summary>
    MostFollowed,
}
