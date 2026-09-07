using Wooly.Core.Accounts;

namespace Wooly.Tests.Fakes;

/// <summary>
///     An account with everything filled in, so a test only says the part it is about — <see cref="APost" /> for the
///     accounts a search turns up and the accounts a relationship command lists, and for the same reason.
/// </summary>
internal static class AnAccount
{
    public static Account With(
        string address = "alice@hachyderm.io",
        string author = "Alice",
        long followers = 1203,
        long following = 187,
        long posts = 4210,
        string id = "42",
        AccountStanding? standing = null,
        string bio = "Cat photographer.",
        IReadOnlyList<AccountField>? fields = null,
        DateOnly? joined = null,
        bool isLocked = false,
        bool isBot = false,
        string? avatarUrl = null) => new()
    {
        Id = id,
        Address = address,
        Author = author,
        Followers = followers,
        Following = following,
        Posts = posts,
        Bio = bio,
        Fields = fields ?? [],
        Joined = joined ?? new DateOnly(2020, 1, 1),
        IsLocked = isLocked,
        IsBot = isBot,
        Url = $"https://hachyderm.io/@{address.Split('@')[0]}",
        AvatarUrl = avatarUrl ?? $"https://hachyderm.io/avatars/{address.Split('@')[0]}.png",
        Standing = standing,
    };

    /// <summary>One custom field, verified or not, for the rows an account sets under its bio.</summary>
    public static AccountField Field(string label, string said, DateTimeOffset? verified = null) => new()
    {
        Label = label,
        Said = said,
        Verified = verified,
    };

    /// <summary>Where the profile stands with an account, with nothing in place but what a test says is.</summary>
    public static AccountStanding Standing(
        bool following = false,
        bool followRequested = false,
        bool followedBy = false,
        bool blocking = false,
        bool muting = false,
        string? note = null) => new()
    {
        Following = following,
        FollowRequested = followRequested,
        FollowedBy = followedBy,
        Blocking = blocking,
        Muting = muting,
        Note = note,
    };
}
