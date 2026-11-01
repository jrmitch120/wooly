using Wooly.Core;
using Wooly.Core.Accounts;
using Wooly.Core.Notifications;
using Wooly.Core.Posts;
using Wooly.Tui.Rendering;

namespace Wooly.Tui.Shell;

/// <summary>
///     Somebody a post could mention (#318): where they are addressed, and the name they chose to be shown as — empty
///     where all a screen knew of them was an address, as it is for the accounts a post mentions.
/// </summary>
/// <param name="Address">Where they are addressed, as <c>username@instance</c>.</param>
/// <param name="Name">
///     Their display name as one line of plain text holds it (<see cref="PlainName.Without" />), the shortcodes their
///     account lists taken out — and empty where that leaves nothing, the row then saying the handle alone.
/// </param>
public sealed record Mentionable(string Address, string Name)
{
    /// <summary>
    ///     Somebody named <paramref name="name" />, the shortcodes of <paramref name="listed" /> taken out on the way
    ///     in, so that <c>:blobcat:</c> neither matches a query nor crowds the name in the list (#525).
    /// </summary>
    public static Mentionable Of(string address, string name, IReadOnlyList<CustomEmoji> listed) =>
        new(address, PlainName.Without(name, listed));

    /// <summary>Somebody known only by <paramref name="address" />, as the accounts a post mentions are.</summary>
    public static Mentionable Addressed(string address) => new(address, string.Empty);

    /// <summary>Who an account is.</summary>
    public static Mentionable Of(Account account) => Of(account.Address, account.Author, account.CustomEmoji);

    /// <summary>
    ///     Everybody <paramref name="post" /> names: who wrote it, then whatever it boosted, then everyone it mentions —
    ///     the order a reader meets them in.
    /// </summary>
    public static IEnumerable<Mentionable> In(Post post)
    {
        yield return Of(post.Account, post.Author, post.AuthorCustomEmoji);

        if (post.Boosted is { } boosted)
        {
            foreach (var named in In(boosted))
            {
                yield return named;
            }
        }

        foreach (var mentioned in post.Mentions)
        {
            yield return Addressed(mentioned);
        }
    }

    /// <summary>Who a notification came from, then everybody its post names.</summary>
    public static IEnumerable<Mentionable> In(Notification notification) =>
        [Of(notification.Actor), .. notification.Post is { } post ? In(post) : []];

    /// <summary>Who a notification group came from, in the order the instance gave them, then everybody its post names.</summary>
    public static IEnumerable<Mentionable> In(NotificationGroup group) =>
        [.. group.Actors.Select(Of), .. group.Post is { } post ? In(post) : []];

    /// <summary>Everybody <paramref name="posts" /> name, top to bottom.</summary>
    public static IEnumerable<Mentionable> In(IEnumerable<Post> posts) => posts.SelectMany(In);
}
