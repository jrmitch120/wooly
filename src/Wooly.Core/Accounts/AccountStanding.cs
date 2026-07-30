namespace Wooly.Core.Accounts;

/// <summary>
///     Where the profile's own account stands with another one: whether it follows it, has asked to, is followed back,
///     and whether it has blocked or muted it. What ADR-0011 left off <see cref="Account" /> until there was a command
///     that acted on it.
/// </summary>
/// <remarks>
///     All five are asked and answered together, because Mastodon answers them together: one relationship per account,
///     read or changed in a single call. A caller holding one of these knows the instance was asked — an account whose
///     standing is unknown carries none at all rather than five falses.
/// </remarks>
public sealed record AccountStanding
{
    /// <summary>Whether the profile follows the account.</summary>
    public required bool Following { get; init; }

    /// <summary>
    ///     Whether a follow is waiting for the account to accept it, which is what following a locked account leaves
    ///     behind. Never true alongside <see cref="Following" />: a request that was accepted is a follow.
    /// </summary>
    public required bool FollowRequested { get; init; }

    /// <summary>Whether the account follows the profile, which is the other direction and not implied by either above.</summary>
    public required bool FollowedBy { get; init; }

    /// <summary>Whether the profile has blocked the account.</summary>
    public required bool Blocking { get; init; }

    /// <summary>Whether the profile has muted the account.</summary>
    public required bool Muting { get; init; }
}
