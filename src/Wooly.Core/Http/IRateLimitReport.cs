namespace Wooly.Core.Http;

/// <summary>
///     What the instance last said about the profile's remaining API budget. Read by a front end that shows it — the
///     TUI's rail does, because the rail is the one thing that can spend the budget by accident (ADR-0014) — and
///     written by nothing above the HTTP layer.
///     <para>
///         A report of the last call rather than a count kept here: the instance is the authority on what is left, it
///         says so on every response, and a client keeping its own tally would drift from it the moment anything else
///         signed in as the same account.
///     </para>
/// </summary>
public interface IRateLimitReport
{
    /// <summary>
    ///     What the last call to an instance reported, or <see langword="null" /> before anything has been called or
    ///     where the instance sends no budget headers at all — which is a thing to draw nothing for rather than to
    ///     guess at.
    /// </summary>
    RateLimitQuota? Latest { get; }
}
