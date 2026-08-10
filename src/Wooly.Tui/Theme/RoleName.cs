namespace Wooly.Tui.Theme;

/// <summary>
///     What each <see cref="Role" /> is called outside the code: in <c>docs/tui-shell.md</c>'s table, and as the key a
///     user writes in a <c>[themes.*]</c> table. Written out rather than derived from the enum member's name, so that
///     renaming a member in C# cannot silently rename the key somebody has in their config file.
/// </summary>
public static class RoleName
{
    private static readonly Dictionary<Role, string> Names = new()
    {
        [Role.Body] = "body",
        [Role.Hashtag] = "hashtag",
        [Role.Mention] = "mention",
        [Role.Link] = "link",
        [Role.Muted] = "muted",
        [Role.BylineName] = "byline-name",
        [Role.BylineHandle] = "byline-handle",
        [Role.Audience] = "audience",
        [Role.ContentWarning] = "content-warning",
        [Role.Media] = "media",
        [Role.Poll] = "poll",
        [Role.ReferencePicked] = "reference-picked",
        [Role.Boost] = "boost",
        [Role.BoostMine] = "boost-mine",
        [Role.Favorite] = "favorite",
        [Role.FavoriteMine] = "favorite-mine",
        [Role.Selection] = "selection",
        [Role.Rail] = "rail",
        [Role.RailCurrent] = "rail-current",
        [Role.RailUnread] = "rail-unread",
        [Role.Quota] = "quota",
        [Role.QuotaLow] = "quota-low",
        [Role.Chrome] = "chrome",
        [Role.Loading] = "loading",
        [Role.Destructive] = "destructive",
        [Role.Error] = "error",
    };

    /// <summary>What <paramref name="role" /> is called in the contract and in a theme.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     A role nothing has a name for, which is a member added to the enum without coming here — a defect to read
    ///     about rather than a key to invent.
    /// </exception>
    public static string Of(Role role) => Names.TryGetValue(role, out var name)
        ? name
        : throw new ArgumentOutOfRangeException(nameof(role), role, "Not a role this client has a name for.");

    /// <summary>The role <paramref name="name" /> names, or <see langword="null" /> where nothing does.</summary>
    /// <remarks>
    ///     What turns a key in somebody's <c>[themes.midnight]</c> table into a role — and what lets a key that is not
    ///     one be named in the config error rather than quietly ignored.
    /// </remarks>
    public static Role? For(string name) =>
        Names.Where(entry => entry.Value == name).Select(entry => (Role?)entry.Key).FirstOrDefault();
}
