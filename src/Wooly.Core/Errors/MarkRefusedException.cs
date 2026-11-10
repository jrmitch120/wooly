namespace Wooly.Core.Errors;

/// <summary>
///     An instance turned a mark down and said why — a boost of a post only its author's followers may see, a pin of a
///     post that is somebody else's, a post it no longer has (#596).
/// </summary>
/// <remarks>
///     Named here rather than passed on as the API client's own exception, for the reason
///     <see cref="VoteRefusedException" /> is: a <see cref="WoolyException" /> is drawn as a notice over what the reader
///     was reading, and anything else reaching the TUI's enquiry is said by nobody, so a refused mark used to vanish.
///     The message is the instance's words and nothing else, which is also exactly what the CLI printed for the API
///     client's exception before this was named — the instance knows its rules, and no wording of this client's would
///     be clearer about which of them was broken.
/// </remarks>
public sealed class MarkRefusedException(Exception refusal) : WoolyException(refusal.Message, refusal);
