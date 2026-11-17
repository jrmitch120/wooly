namespace Wooly.Core.Errors;

/// <summary>
///     An instance would not translate a post and said why, or said nothing readable: a post only followers or the people
///     it mentions can see, a pair of languages its translator does not take (a post already in the target among them),
///     or no translator at all (ADR-0037) — or would not say which languages it translates (#616).
/// </summary>
/// <remarks>
///     A <see cref="WoolyException" /> for the reason <see cref="TrendsRefusedException" /> gives: it is what the TUI's
///     enquiry says on the status row, and what the CLI reports with ADR-0006's general exit code. A translator that
///     failed to answer is not this, but an <see cref="InstanceFailedException" />.
/// </remarks>
public sealed class TranslationRefusedException : WoolyException
{
    private TranslationRefusedException(string refused, string? reason)
        : base(reason is null ? $"{refused}." : $"{refused}: {reason}") =>
        Reason = reason;

    /// <summary>What the instance said was wrong, in its own words, or nothing where it said nothing readable.</summary>
    public string? Reason { get; }

    /// <summary>A refusal to translate a post, out of what the instance said was wrong.</summary>
    public static TranslationRefusedException For(string? reason) =>
        new("The instance would not translate the post", reason);

    /// <summary>
    ///     A refusal to say which languages the instance can translate (#616), out of what it said was wrong — no post
    ///     having been asked about.
    /// </summary>
    public static TranslationRefusedException ForLanguages(string? reason) =>
        new("The instance would not say which languages it can translate", reason);
}
