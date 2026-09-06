using Wooly.Core.Accounts;

namespace Wooly.Core.Discovery;

/// <summary>
///     Somebody an instance offers this profile to follow, and why it is offering them (CONTEXT.md). The reason is what
///     makes this a record at all rather than a bare <see cref="Accounts.Account" />: a list of strangers is a list of
///     strangers, and it is the reason that earns a stranger a row.
///     <para>
///         There is no <see cref="Accounts.Account.Standing" /> on the account here and no call spent finding one. An
///         instance never suggests somebody already followed, dismissed or blocked, so the tie is known by
///         construction — the row says who somebody is and the heading over it says why.
///     </para>
/// </summary>
public sealed record Suggestion
{
    /// <summary>Who is being suggested, mapped the same way every other account in this client is.</summary>
    public required Account Account { get; init; }

    /// <summary>
    ///     Why they are being suggested — the wire's <c>sources</c>, in the order the instance sent them, and empty
    ///     where it named no reason this client has a heading for.
    ///     <para>
    ///         A list rather than one reason, because <c>sources</c> is an array and a person can be offered under
    ///         several at once. Which one they are drawn under, and what to do about the same person arriving twice, is
    ///         the screen's ranking to do: a port that kept only the first would have made that choice on its behalf.
    ///     </para>
    ///     Empty is not an error and not a person to drop. A source this client cannot name is dropped from this list
    ///     and the account is kept, because an instance that has learned a sixth reason has still named somebody worth
    ///     showing — what a row with no heading over it does is the screen's decision.
    /// </summary>
    public required IReadOnlyList<SuggestionReason> Reasons { get; init; }
}
