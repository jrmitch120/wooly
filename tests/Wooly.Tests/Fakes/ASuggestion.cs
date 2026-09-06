using Wooly.Core.Accounts;
using Wooly.Core.Discovery;

namespace Wooly.Tests.Fakes;

/// <summary>
///     A suggestion with both halves filled in, so a test only says the part it is about — <see cref="AnAccount" /> for
///     the person on it, and for the same reason.
/// </summary>
internal static class ASuggestion
{
    /// <param name="reasons">
    ///     Why the instance is offering them, or <see langword="null" /> for the one reason that is really an argument
    ///     — a test that does not say is a test about something other than the heading. An <i>empty</i> list is a
    ///     different thing again and is passed through as one: somebody offered under a source this client has no name
    ///     for, which is still somebody to show.
    /// </param>
    public static Suggestion With(Account? account = null, IReadOnlyList<SuggestionReason>? reasons = null) => new()
    {
        Account = account ?? AnAccount.With(),
        Reasons = reasons ?? [SuggestionReason.FriendsOfFriends],
    };
}
