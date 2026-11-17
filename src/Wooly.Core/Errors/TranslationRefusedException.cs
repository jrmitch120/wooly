namespace Wooly.Core.Errors;

/// <summary>
///     An instance would not translate a post and said why, or said nothing readable: a post only followers or the people
///     it mentions can see, a pair of languages its translator does not take (a post already in the target among them),
///     or no translator at all (ADR-0037).
/// </summary>
/// <remarks>
///     A <see cref="WoolyException" /> for the reason <see cref="TrendsRefusedException" /> gives: it is what the TUI's
///     enquiry says on the status row, and what the CLI reports with ADR-0006's general exit code. A translator that
///     failed to answer is not this, but an <see cref="InstanceFailedException" />.
/// </remarks>
public sealed class TranslationRefusedException : WoolyException
{
    private TranslationRefusedException(string? reason)
        : base(reason is null
            ? "The instance would not translate the post."
            : $"The instance would not translate the post: {reason}") =>
        Reason = reason;

    /// <summary>What the instance said was wrong, in its own words, or nothing where it said nothing readable.</summary>
    public string? Reason { get; }

    /// <summary>A refusal, out of what the instance said was wrong.</summary>
    public static TranslationRefusedException For(string? reason) => new(reason);
}
