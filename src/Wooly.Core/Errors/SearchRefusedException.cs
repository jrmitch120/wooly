namespace Wooly.Core.Errors;

/// <summary>
///     An instance turned a search down, and said why where it said anything (#599).
/// </summary>
/// <remarks>
///     Needed once search left Mastonet for a raw call: Mastonet turned a refusal into an exception carrying the
///     instance's own words, which the CLI printed as they were, and a bare failed status from the raw call would be
///     reported as a defect instead. Named for the reason <see cref="PostRefusedException" /> is — a refusal is
///     something the searcher has to be told, and a <see cref="WoolyException" /> is what every front end says.
/// </remarks>
/// <param name="reason">What the instance said was wrong, or <see langword="null" /> where it said nothing readable.</param>
public sealed class SearchRefusedException(string? reason)
    : WoolyException(
        reason is null ? "The instance would not search for that." : $"The instance would not search for that: {reason}");
