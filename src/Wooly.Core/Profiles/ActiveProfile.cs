namespace Wooly.Core.Profiles;

/// <summary>
///     The profile an invocation is acting as, with both halves of it in hand: where it points, and the token to call
///     with. Everything a command needs to reach an instance as somebody, and nothing it does not.
/// </summary>
public sealed record ActiveProfile
{
    /// <summary>The local name the user gave this profile, e.g. <c>work</c>.</summary>
    public required string Name { get; init; }

    /// <summary>The instance's domain, e.g. <c>mastodon.social</c>.</summary>
    public required string Instance { get; init; }

    /// <summary>The Mastodon account this profile signs in as, as <c>username@instance</c>.</summary>
    public required string? Account { get; init; }

    /// <summary>The access token to authenticate calls with. Never rendered, logged, or written to the config file.</summary>
    public required string AccessToken { get; init; }
}
