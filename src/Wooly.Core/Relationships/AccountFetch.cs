using Wooly.Core.Accounts;
using Wooly.Core.Errors;

namespace Wooly.Core.Relationships;

/// <summary>
///     What a read of a list of accounts came back with, and whether that is all of what was asked for. ADR-0007's
///     second decision, inherited a third time: a fetch a rate limit stopped part way through may hold nothing at all,
///     and a caller unable to tell that from an account with no followers would report "no followers" to somebody with
///     thousands.
/// </summary>
public sealed record AccountFetch
{
    /// <summary>The accounts that arrived, in the order the instance listed them.</summary>
    public required IReadOnlyList<Account> Accounts { get; init; }

    /// <summary>
    ///     The rate limit that cut the fetch short, or <see langword="null" /> if nothing did. Held as the exception
    ///     itself so a front end that treats this as a failure — the CLI does, per ADR-0006 — can throw the instance's
    ///     own answer rather than a second-hand copy of it.
    /// </summary>
    public required RateLimitedException? StoppedBy { get; init; }

    /// <summary>Whether this is everything the caller asked for, as far as the accounts go.</summary>
    public bool IsComplete => StoppedBy is null;

    /// <summary>A fetch that ran to the end of what was asked for.</summary>
    public static AccountFetch Complete(IReadOnlyList<Account> accounts) =>
        new() { Accounts = accounts, StoppedBy = null };

    /// <summary>A fetch the instance's rate limit stopped, holding whatever had already arrived.</summary>
    public static AccountFetch StoppedShort(IReadOnlyList<Account> accounts, RateLimitedException rateLimit) =>
        new() { Accounts = accounts, StoppedBy = rateLimit };
}
