namespace Wooly.Core.Errors;

/// <summary>
///     An instance turned a vote down and said why — most often because this account has already voted, which
///     Mastodon refuses outright rather than replacing, and which is the whole reason the TUI asks before casting one.
/// </summary>
/// <remarks>
///     Named here rather than passed on as the API client's own exception, because a refusal is something the reader
///     has to be told rather than a defect: a <see cref="WoolyException" /> is drawn as a notice over what they were
///     reading, and anything else reaching the shell is a crash. The instance's own words are kept — it knows its
///     rules, and no wording of this client's would be clearer about which of them was broken.
/// </remarks>
public sealed class VoteRefusedException(Exception refusal)
    : WoolyException($"The instance would not take that vote. {refusal.Message}", refusal);
