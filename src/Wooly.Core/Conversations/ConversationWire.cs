using Wooly.Core.Posts;
using Wooly.Core.Profiles;
using WireConversation = Mastonet.Entities.Conversation;

namespace Wooly.Core.Conversations;

/// <summary>
///     The one crossing between Mastodon's conversation and this project's <see cref="Conversation" />, alongside
///     <see cref="PostWire" /> and <see cref="Notifications.NotificationWire" /> and for the same reason: one mapping,
///     so a conversation looks the same whether it was listed, shown or just marked read.
/// </summary>
internal static class ConversationWire
{
    /// <param name="reader">
    ///     The profile reading it: which instance is being read, needed because it names its own accounts by bare
    ///     username and everyone else's in full, and who is reading, which the post it carries is asked about.
    /// </param>
    public static Conversation ToConversation(WireConversation conversation, ActiveProfile reader) => new()
    {
        Id = conversation.Id,
        With = conversation.Accounts.Select(account => MastodonWire.Qualify(account, reader.Instance)).ToList(),
        Unread = conversation.Unread,

        // Absent on a conversation whose posts have all been taken down, which is still a conversation the account is
        // in — so it is carried with nothing to show rather than dropped from the list.
        Latest = conversation.LastStatus is null ? null : PostWire.ToPost(conversation.LastStatus, reader),
    };
}
