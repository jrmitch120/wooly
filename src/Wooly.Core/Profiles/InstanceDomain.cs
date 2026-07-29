namespace Wooly.Core.Profiles;

/// <summary>
///     The rule that an instance is named by a bare domain, in the one place every entry point can reach it. Mastonet
///     builds its own <c>https://</c> addresses from this value, so a scheme or a path here does not fail where it was
///     typed — it fails much later, as a puzzling network error rather than the typo it is. Living here rather than in
///     one command's argument validation is what stops the next way of making a profile (ADR-0004's OAuth flow, or the
///     TUI) from having to remember the rule, or quietly not having it.
/// </summary>
public static class InstanceDomain
{
    /// <summary>Whether <paramref name="instance" /> is a bare domain this client can build addresses from.</summary>
    /// <remarks>
    ///     A port is deliberately allowed: a dockerized instance on <c>localhost:3000</c> is a real address this client
    ///     has to reach, and ADR-0005's integration suite runs against exactly that.
    /// </remarks>
    public static bool IsWellFormed(string? instance) =>
        !string.IsNullOrWhiteSpace(instance) && !instance.Contains('/');

    /// <summary>
    ///     How a value that is not a bare domain is described, shared so that the command rejecting one before it is
    ///     stored and the store rejecting one that got past it cannot say different things about the same value.
    /// </summary>
    public static string Rejection(string instance) =>
        $"Give the instance as a domain — mastodon.social, not '{instance}'.";
}
