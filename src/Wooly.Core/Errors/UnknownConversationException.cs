namespace Wooly.Core.Errors;

/// <summary>
///     A conversation was named that could not be found. Mastodon offers no way to ask for one conversation by id — the
///     only way to it is down the list of them — so "not found" here means "not among the recent ones looked through",
///     and the message says so rather than claiming the conversation does not exist.
/// </summary>
public sealed class UnknownConversationException(string conversationId, int searched)
    : WoolyException(
        $"No conversation with id {conversationId} is among the {searched} most recent this profile is in. "
        + "Check the id against a listing of them.")
{
    /// <summary>The id that was named.</summary>
    public string ConversationId { get; } = conversationId;
}
