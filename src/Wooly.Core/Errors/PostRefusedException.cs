namespace Wooly.Core.Errors;

/// <summary>
///     An instance turned a post, or a change to one, down and said why — most often because it is longer than the
///     instance allows.
/// </summary>
/// <remarks>
///     Named here rather than passed on as the API client's own exception, for the reason
///     <see cref="VoteRefusedException" /> is: a refusal is something the writer has to be told rather than a defect,
///     and anything that is not a <see cref="WoolyException" /> reaching the shell is said by nobody. The instance's
///     own words are kept — it knows its rules, and no wording of this client's would be clearer about which of them
///     was broken.
/// </remarks>
public sealed class PostRefusedException(Exception refusal)
    : WoolyException($"The instance would not take that post. {refusal.Message}", refusal);
