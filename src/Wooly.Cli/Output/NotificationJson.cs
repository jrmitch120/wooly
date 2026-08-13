using System.Text.Json.Serialization;
using Spectre.Console;
using Wooly.Core.Notifications;
using Wooly.Core.Paging;

namespace Wooly.Cli.Output;

/// <summary>
///     Writes notifications for another program to read: <see cref="ListDocument" />'s envelope, with nothing ahead of
///     it — there is only ever the one inbox. The post inside each one is a <see cref="PostDocument" />, spelled the
///     one way every command spells a post.
/// </summary>
internal static class NotificationJson
{
    public static void Write(IAnsiConsole console, Fetch<Notification> fetch) =>
        ListDocument.Write(console, fetch, Of, "notifications");

    private static NotificationDocument Of(Notification notification) => new(
        notification.Id,

        // The instance's word where this client has none of its own, which is what makes every notification say
        // something to a script rather than leaving a hole where the unfamiliar ones were.
        notification.Kind.Name,
        notification.ReceivedAt,
        notification.Account,
        notification.Author,
        notification.Post is null ? null : PostDocument.Of(notification.Post));

    /// <param name="Kind">
    ///     What happened: <c>mention</c>, <c>follow</c>, <c>boost</c> or <c>favorite</c> — this project's vocabulary,
    ///     not the API's — or, for a kind this client does not name, whatever the instance called it.
    /// </param>
    /// <param name="Post">
    ///     The post it is about, absent on a follow. The same document a timeline writes, so one <c>jq</c> filter reads
    ///     a post wherever it turns up.
    /// </param>
    private sealed record NotificationDocument(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("kind")] string Kind,
        [property: JsonPropertyName("receivedAt")] DateTimeOffset ReceivedAt,
        [property: JsonPropertyName("account")] string Account,
        [property: JsonPropertyName("author")] string Author,
        [property: JsonPropertyName("post")] PostDocument? Post);
}
