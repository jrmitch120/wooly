using Mastonet.Entities;
using WireAccount = Mastonet.Entities.Account;

namespace Wooly.Core.Accounts;

/// <summary>
///     The one crossing between Mastodon's account and this project's <see cref="Account" />, alongside
///     <see cref="Posts.PostWire" /> and <see cref="Notifications.NotificationWire" /> and for the same reason: one
///     mapping, so an account looks the same however it arrived — found by a search, read off a post, or listed as a
///     follower.
/// </summary>
internal static class AccountWire
{
    /// <param name="instance">
    ///     The instance being read, needed because it names its own accounts by bare username and everyone else's in
    ///     full.
    /// </param>
    /// <param name="standing">
    ///     What the instance said about where the profile stands with this account, or <see langword="null" /> where it
    ///     was not asked — which is everywhere but the relationship endpoints.
    /// </param>
    public static Account ToAccount(WireAccount account, string instance, Relationship? standing = null) => new()
    {
        Id = account.Id,
        Address = MastodonWire.Qualify(account, instance),
        Author = MastodonWire.DisplayName(account),
        Followers = account.FollowersCount,
        Following = account.FollowingCount,
        Posts = account.StatusesCount,
        Url = account.ProfileUrl,
        Standing = standing is null ? null : ToStanding(standing),
    };

    /// <summary>
    ///     The five facts this client acts on, out of the thirteen a relationship carries. The rest — endorsements,
    ///     domain blocks, whether boosts are shown — belong to commands this client does not have, and a record holding
    ///     them would promise answers no command here can give.
    /// </summary>
    private static AccountStanding ToStanding(Relationship relationship) => new()
    {
        Following = relationship.Following,
        FollowRequested = relationship.Requested,
        FollowedBy = relationship.FollowedBy,
        Blocking = relationship.Blocking,
        Muting = relationship.Muting,
    };
}
