using System.Globalization;

namespace Wooly.Core.Errors;

/// <summary>
///     An instance refused a call because the profile has spent its API quota. Carries <see cref="ResetsAt" /> so the
///     TUI can wait the limit out with a countdown; the CLI deliberately does not wait (ADR-0006) and reports this as a
///     failure so scripts never hang.
/// </summary>
public sealed class RateLimitedException(string instance, DateTimeOffset? resetsAt)
    : WoolyException(BuildMessage(instance, resetsAt))
{
    /// <summary>The instance that rate-limited the call, e.g. <c>mastodon.social</c>.</summary>
    public string Instance { get; } = instance;

    /// <summary>When the quota is restored, or <see langword="null" /> if the instance did not say.</summary>
    public DateTimeOffset? ResetsAt { get; } = resetsAt;

    private static string BuildMessage(string instance, DateTimeOffset? resetsAt)
    {
        if (resetsAt is null)
        {
            return $"Rate limited by {instance}. Wait a while before retrying.";
        }

        // Rendered as UTC so the line means the same thing wherever a script's output is read back.
        var reset = resetsAt.Value.UtcDateTime.ToString("u", CultureInfo.InvariantCulture);

        return $"Rate limited by {instance}. The limit resets at {reset}.";
    }
}
