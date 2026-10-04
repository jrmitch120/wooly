using System.Text.RegularExpressions;
using Wooly.Core.Accounts;
using Wooly.Core.Notifications;
using Wooly.Core.Posts;

namespace Wooly.Tui.Shell;

/// <summary>
///     Somebody a post could mention (#318): where they are addressed, and the name they chose to be shown as — empty
///     where all a screen knew of them was an address, as it is for the accounts a post mentions.
/// </summary>
/// <param name="Address">Where they are addressed, as <c>username@instance</c>.</param>
/// <param name="Name">Their display name, with any custom-emoji shortcodes taken out.</param>
public sealed partial record Mentionable(string Address, string Name)
{
    /// <summary>Somebody named <paramref name="name" />, the name's shortcodes taken out on the way in.</summary>
    public static Mentionable Of(string address, string name) => new(address, Plain(name));

    /// <summary>Who an account is.</summary>
    public static Mentionable Of(Account account) => Of(account.Address, account.Author);

    /// <summary>
    ///     Everybody <paramref name="post" /> names: who wrote it, then whatever it boosted, then everyone it mentions —
    ///     the order a reader meets them in.
    /// </summary>
    public static IEnumerable<Mentionable> In(Post post)
    {
        yield return Of(post.Account, post.Author);

        if (post.Boosted is { } boosted)
        {
            foreach (var named in In(boosted))
            {
                yield return named;
            }
        }

        foreach (var mentioned in post.Mentions)
        {
            yield return new Mentionable(mentioned, string.Empty);
        }
    }

    /// <summary>Who a notification came from, then everybody its post names.</summary>
    public static IEnumerable<Mentionable> In(Notification notification) =>
        [Of(notification.Account, notification.Author), .. notification.Post is { } post ? In(post) : []];

    /// <summary>Everybody <paramref name="posts" /> name, top to bottom.</summary>
    public static IEnumerable<Mentionable> In(IEnumerable<Post> posts) => posts.SelectMany(In);

    /// <summary>
    ///     <paramref name="name" /> with its custom-emoji shortcodes taken out, so that <c>:blobcat:</c> neither matches
    ///     a query nor crowds the name in the list — and the spaces either side of one closed up.
    /// </summary>
    private static string Plain(string name) =>
        Spaces().Replace(Shortcode().Replace(name, " "), " ").Trim();

    [GeneratedRegex(@":[A-Za-z0-9_]+:")]
    private static partial Regex Shortcode();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
