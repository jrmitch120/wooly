using System.Text;
using Wooly.Core;
using Wooly.Core.Accounts;
using Wooly.Core.Posts;

namespace Wooly.Tui.Rendering;

/// <summary>
///     A display name where only one line of plain text can hold it — the crumb, the follows filter and the mention
///     list — which is the name with the shortcodes its account lists taken out, since none of the three can draw a
///     <b>custom emoji</b> and <c>:verified:</c> spelled out is noise in a title and a false match in a search (#525).
/// </summary>
/// <remarks>
///     One rule for all three, read the way the drawn name reads its custom emoji (<see cref="Shortcode.In" />): a
///     <c>:word:</c> the account does not list is the words they chose to be called and stays. The spaces a shortcode
///     leaves either side of it are closed up, and a name that is nothing but shortcodes is nothing at all, which
///     <see cref="Of(string, IReadOnlyList{CustomEmoji}, string)" /> answers with the handle.
/// </remarks>
public static class PlainName
{
    /// <summary>
    ///     <paramref name="name" /> with the shortcodes of <paramref name="listed" /> taken out and the spaces left
    ///     behind closed up — empty where nothing else was written, for a caller that says the handle beside it anyway.
    /// </summary>
    public static string Without(string name, IReadOnlyList<CustomEmoji> listed)
    {
        var shortcodes = Shortcode.In(name, listed);
        var left = new StringBuilder(name.Length);
        var from = 0;

        foreach (var shortcode in shortcodes)
        {
            left.Append(name, from, shortcode.At - from).Append(' ');
            from = shortcode.End;
        }

        left.Append(name, from, name.Length - from);

        return string.Join(' ', left.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    ///     <paramref name="name" /> as <see cref="Without" /> leaves it, or the handle of <paramref name="address" />
    ///     where that leaves nothing — so that a line naming somebody always names them.
    /// </summary>
    public static string Of(string name, IReadOnlyList<CustomEmoji> listed, string address) =>
        Without(name, listed) is { Length: > 0 } plain ? plain : $"@{address}";

    /// <summary>What <paramref name="account" /> is called on a line of plain text.</summary>
    public static string Of(Account account) => Of(account.Author, account.CustomEmoji, account.Address);

    /// <summary>What the author of <paramref name="post" /> is called on a line of plain text.</summary>
    public static string OfAuthor(Post post) => Of(post.Author, post.AuthorCustomEmoji, post.Account);
}
