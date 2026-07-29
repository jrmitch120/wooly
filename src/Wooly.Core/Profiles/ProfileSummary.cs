namespace Wooly.Core.Profiles;

/// <summary>
///     One profile as it appears in a list of them: what it is called, where it points, and whether it is the one
///     commands currently default to. Carries no access token — listing profiles must never open the keyring.
/// </summary>
public sealed record ProfileSummary
{
    /// <summary>The local name the user gave this profile, e.g. <c>work</c>.</summary>
    public required string Name { get; init; }

    /// <summary>The instance's domain, e.g. <c>mastodon.social</c>.</summary>
    public required string Instance { get; init; }

    /// <summary>The account this profile signs in as, or <see langword="null" /> if it was never established.</summary>
    public required string? Account { get; init; }

    /// <summary>Whether commands with no <c>--profile</c> of their own act as this one.</summary>
    public required bool IsCurrent { get; init; }
}
