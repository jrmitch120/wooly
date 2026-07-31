using Wooly.Core.Conversations;
using Wooly.Core.Posts;

namespace Wooly.Tests.Fakes;

/// <summary>
///     A conversation with everything filled in, so a test only says the part it is about — <see cref="APost" /> for the
///     direct conversations a profile is in, and for the same reason.
/// </summary>
internal static class AConversation
{
    public static Conversation With(
        string id = "7",
        string[]? with = null,
        bool unread = true,
        Post? latest = null) => new()
    {
        Id = id,
        With = with ?? ["alice@hachyderm.io"],
        Unread = unread,
        Latest = latest ?? DirectPost(),
    };

    /// <summary>A conversation whose posts have all been taken down, which is the one carrying no last post.</summary>
    public static Conversation Emptied(string id = "7") => new()
    {
        Id = id,
        With = ["alice@hachyderm.io"],
        Unread = false,
        Latest = null,
    };

    /// <summary>One conversation and everything said in it, oldest first.</summary>
    public static ConversationThread Thread(Conversation? conversation = null, params Post[] posts) => new()
    {
        Conversation = conversation ?? With(),
        Posts = posts.Length > 0 ? posts : [DirectPost()],
    };

    /// <summary>A post that went out direct, which is what everything in a conversation is.</summary>
    public static Post DirectPost(string id = "110", string content = "Hello world") => APost.With(
        id: id,
        account: "alice@hachyderm.io",
        author: "Alice",
        content: content,
        visibility: PostVisibility.Direct);
}
