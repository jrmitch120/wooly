namespace Wooly.Core.Errors;

/// <summary>
///     An instance turned the removal of a conversation down and said why, or said nothing readable (#620). An id it has
///     no conversation for is not this: that is a not-found of its own (<see cref="UnknownConversationException" />).
/// </summary>
/// <remarks>
///     A <see cref="WoolyException" /> rather than the HTTP stack's own exception, for the reason
///     <see cref="MarkRefusedException" /> gives: the TUI's enquiry says a <see cref="WoolyException" /> on the status
///     row and anything else reaching it is said by nobody, so a refused removal would leave the row standing with no
///     word of why. The CLI reports it with ADR-0006's general exit code.
/// </remarks>
public sealed class ConversationRefusedException : WoolyException
{
    private ConversationRefusedException(string? reason)
        : base(reason is null
            ? "The instance would not remove the conversation."
            : $"The instance would not remove the conversation: {reason}") =>
        Reason = reason;

    /// <summary>What the instance said was wrong, in its own words, or nothing where it said nothing readable.</summary>
    public string? Reason { get; }

    /// <summary>A refusal to remove a conversation, out of what the instance said was wrong.</summary>
    public static ConversationRefusedException ForRemoval(string? reason) => new(reason);
}
