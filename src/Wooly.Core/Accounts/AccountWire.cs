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

        // The same flattener a post's content goes through, because a bio is the same HTML subset an instance serves
        // that as. Empty in means empty out: an account with no bio has one, and it is nothing.
        Bio = InstanceHtml.ToPlainText(account.Note),

        // Mastonet leaves this null rather than empty where an account set no rows, the way it does a post's media.
        Fields = account.Fields?.Select(ToField).ToList() ?? [],

        // Read as the day it was, not the moment: Mastodon stamps this in UTC like everything else, so the day is
        // taken there rather than wherever this machine happens to be.
        Joined = DateOnly.FromDateTime(MastodonWire.AsUtc(account.CreatedAt).UtcDateTime),
        IsLocked = account.Locked,

        // Nullable on the way in because that is how the client library types the field; an instance that said
        // nothing has an account nobody marked.
        IsBot = account.Bot ?? false,
        Url = account.ProfileUrl,

        // The wire says "no avatar" with an empty string, the same way it says "no bio" with one.
        AvatarUrl = MastodonWire.SaidOrNothing(account.AvatarUrl),
        Standing = standing is null ? null : ToStanding(standing),
    };

    /// <summary>One row under an account's bio, with what it says flattened the way the bio above it is.</summary>
    private static AccountField ToField(Field field) => new()
    {
        Label = field.Name,
        Said = InstanceHtml.ToPlainText(field.Value),
        Verified = field.VerifiedAt is { } verified ? MastodonWire.AsUtc(verified) : null,
    };

    /// <summary>
    ///     The five ties this client acts on and the note it reads, out of the thirteen facts a relationship carries.
    ///     The rest — endorsements, domain blocks, whether boosts are shown — belong to commands this client does not
    ///     have, and a record holding them would promise answers no command here can give.
    /// </summary>
    private static AccountStanding ToStanding(Relationship relationship) => new()
    {
        Following = relationship.Following,
        FollowRequested = relationship.Requested,
        FollowedBy = relationship.FollowedBy,
        Blocking = relationship.Blocking,
        Muting = relationship.Muting,

        // The profile's own private note about the account, which Mastonet's doc comment misnames "this user's
        // profile bio" — the bio is a different field, read off the account above. Empty is a note nobody wrote.
        Note = MastodonWire.SaidOrNothing(relationship.Note),
    };
}
