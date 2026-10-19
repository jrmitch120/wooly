namespace Wooly.Core.Errors;

/// <summary>
///     An instance would not show the own account's public profile as written, or would not take a change to it, and
///     said why — a display name or a bio longer than it allows, most often. Nothing is refused before it is sent,
///     because the limits are the instance's and an instance does not report them (ADR-0028); its own words are kept
///     for the reason <see cref="PostRefusedException" /> keeps them.
/// </summary>
public sealed class PublicProfileRefusedException : WoolyException
{
    private PublicProfileRefusedException(string refused, string? reason)
        : base(reason is null ? $"{refused}." : $"{refused}: {reason}") =>
        Reason = reason;

    /// <summary>What the instance said was wrong, in its own words, or nothing where it said nothing readable.</summary>
    public string? Reason { get; }

    /// <summary>The instance would not show the public profile as written.</summary>
    public static PublicProfileRefusedException Reading(string? reason) =>
        new("The instance would not show your public profile", reason);

    /// <summary>The instance would not take a change to the public profile.</summary>
    public static PublicProfileRefusedException Saving(string? reason) =>
        new("The instance would not take that change to your public profile", reason);
}
