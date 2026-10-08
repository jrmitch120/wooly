namespace Wooly.Core.Errors;

/// <summary>
///     An instance would not take a file sent up as a pending attachment, and said why — too large, most often, or of a
///     type it does not accept (#375) — or would not take a description for one, longer than it allows (#377). Trying
///     again cannot help, unlike a dropped connection
///     (<see cref="TransientNetworkException" />); the author has to change what is attached.
/// </summary>
/// <param name="reason">What the instance said was wrong, or <see langword="null" /> where it said nothing readable.</param>
public sealed class AttachmentRefusedException(string? reason)
    : WoolyException(reason ?? "The instance would not take that file.")
{
    /// <summary>What the instance said was wrong, in its own words, or nothing where it said nothing readable.</summary>
    public string? Reason { get; } = reason;
}
