namespace Wooly.Cli;

/// <summary>
///     The exit codes this CLI is allowed to return. The set is deliberately small and fixed so a script can branch on
///     <em>why</em> a command failed, not just that it did — see ADR-0006. Numbers here are part of the CLI's public
///     contract: add to them, never renumber them.
/// </summary>
public enum ExitCode
{
    /// <summary>The command did what it was asked to.</summary>
    Success = 0,

    /// <summary>The command failed for a reason with no more specific code.</summary>
    Error = 1,

    /// <summary>The command line itself was wrong — unknown command, missing argument, bad flag value.</summary>
    UsageError = 2,

    /// <summary>No usable credentials for the profile: not logged in, or the access token was rejected.</summary>
    AuthenticationError = 3,

    /// <summary>The instance could not be reached, and the retries allowed for a transient fault were spent.</summary>
    NetworkError = 4,

    /// <summary>The instance rate-limited the request. The CLI reports this rather than waiting the limit out.</summary>
    RateLimited = 5,
}
