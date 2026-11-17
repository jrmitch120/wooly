namespace Wooly.Core.Errors;

/// <summary>
///     An instance turned a read of its trends down and said why, or said nothing readable. An instance that keeps no
///     trends is not this: it answers with nothing, which is nothing trending.
/// </summary>
/// <remarks>
///     A <see cref="WoolyException" /> rather than the HTTP stack's own exception for the reason
///     <see cref="MarkRefusedException" /> gives: the TUI's enquiry says a <see cref="WoolyException" /> on the status
///     row, and anything else reaching it is said by nobody — so a refused read of the trends under the search prompt
///     would leave the reader looking at an empty Search with no word of why (#605).
/// </remarks>
public sealed class TrendsRefusedException : WoolyException
{
    private TrendsRefusedException(string? reason)
        : base(reason is null
            ? "The instance would not show its trends."
            : $"The instance would not show its trends: {reason}") =>
        Reason = reason;

    /// <summary>What the instance said was wrong, in its own words, or nothing where it said nothing readable.</summary>
    public string? Reason { get; }

    /// <summary>A refusal, out of what the instance said was wrong.</summary>
    public static TrendsRefusedException For(string? reason) => new(reason);
}
