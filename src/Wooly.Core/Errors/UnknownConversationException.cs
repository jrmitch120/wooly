namespace Wooly.Core.Errors;

/// <summary>
///     A conversation was named that could not be found. Mastodon offers no way to ask for one conversation by id — the
///     only way to it is down the list of them — so "not found" here means "not among the recent ones looked through",
///     and the message says so rather than claiming the conversation does not exist.
/// </summary>
/// <remarks>
///     Removing one is the exception to that (#620): the removal names the conversation to the instance directly, and
///     the instance's own not-found does say there is no such conversation on the profile's list — so that is what its
///     message says instead.
/// </remarks>
public sealed class UnknownConversationException : WoolyException
{
    /// <summary>An id not among the <paramref name="searched" /> most recent conversations looked through.</summary>
    public UnknownConversationException(string conversationId, int searched)
        : base($"No conversation with id {conversationId} is among the {searched} most recent this profile is in. "
               + "Check the id against a listing of them.") =>
        ConversationId = conversationId;

    private UnknownConversationException(string conversationId, string message)
        : base(message) =>
        ConversationId = conversationId;

    /// <summary>The id that was named.</summary>
    public string ConversationId { get; }

    /// <summary>An id the instance said names no conversation on the profile's list, asked about directly.</summary>
    public static UnknownConversationException NotOnTheList(string conversationId) =>
        new(
            conversationId,
            $"No conversation with id {conversationId} is on this profile's list. Check the id against a listing of them.");
}
