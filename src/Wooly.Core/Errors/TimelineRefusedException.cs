namespace Wooly.Core.Errors;

/// <summary>
///     An instance turned a read of a timeline it is asked for raw down and said why, or said nothing readable — the
///     posts sharing a <b>Trending link</b> (#603) for one. A link that is not trending is not this: that is a not-found
///     answer of its own (<see cref="LinkNotTrendingException" />).
/// </summary>
/// <remarks>
///     A <see cref="WoolyException" /> rather than the HTTP stack's own exception, for the reason
///     <see cref="TrendsRefusedException" /> and <see cref="SearchRefusedException" /> give: the TUI's enquiry says a
///     <see cref="WoolyException" /> on the status row and anything else reaching it is said by nobody — so the three
///     raw adapters #598 added say a refusal one way (#598's review).
/// </remarks>
public sealed class TimelineRefusedException : WoolyException
{
    private TimelineRefusedException(string? reason)
        : base(reason is null
            ? "The instance would not show that timeline."
            : $"The instance would not show that timeline: {reason}") =>
        Reason = reason;

    /// <summary>What the instance said was wrong, in its own words, or nothing where it said nothing readable.</summary>
    public string? Reason { get; }

    /// <summary>A refusal, out of what the instance said was wrong.</summary>
    public static TimelineRefusedException For(string? reason) => new(reason);
}
