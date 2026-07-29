namespace Wooly.Core.Configuration;

/// <summary>
///     One named local profile: which instance it talks to, and which account there it is. The access token belongs to
///     the credential store, never here — this file is meant to be readable and shareable without leaking a secret.
/// </summary>
public sealed record ProfileConfig
{
    /// <summary>The instance's domain, e.g. <c>mastodon.social</c>. A profile without one is meaningless.</summary>
    public required string Instance { get; init; }

    /// <summary>
    ///     The Mastodon account this profile signs in as, as <c>username@instance</c>, or <see langword="null" /> until
    ///     an authentication flow has established it.
    /// </summary>
    public string? Account { get; init; }
}
