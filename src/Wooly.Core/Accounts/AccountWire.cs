using WireAccount = Mastonet.Entities.Account;

namespace Wooly.Core.Accounts;

/// <summary>
///     The one crossing between Mastodon's account and this project's <see cref="Account" />, alongside
///     <see cref="Posts.PostWire" /> and <see cref="Notifications.NotificationWire" /> and for the same reason: one
///     mapping, so an account looks the same however it arrived — found by a search, or read off a post.
/// </summary>
internal static class AccountWire
{
    /// <param name="instance">
    ///     The instance being read, needed because it names its own accounts by bare username and everyone else's in
    ///     full.
    /// </param>
    public static Account ToAccount(WireAccount account, string instance) => new()
    {
        Address = MastodonWire.Qualify(account, instance),
        Author = MastodonWire.DisplayName(account),
        Followers = account.FollowersCount,
        Following = account.FollowingCount,
        Posts = account.StatusesCount,
        Url = account.ProfileUrl,
    };
}
