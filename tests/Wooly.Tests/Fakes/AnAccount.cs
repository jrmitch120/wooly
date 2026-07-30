using Wooly.Core.Accounts;

namespace Wooly.Tests.Fakes;

/// <summary>
///     An account with everything filled in, so a test only says the part it is about — <see cref="APost" /> for the
///     accounts a search turns up, and for the same reason.
/// </summary>
internal static class AnAccount
{
    public static Account With(
        string address = "alice@hachyderm.io",
        string author = "Alice",
        long followers = 1203,
        long following = 187,
        long posts = 4210) => new()
    {
        Address = address,
        Author = author,
        Followers = followers,
        Following = following,
        Posts = posts,
        Url = $"https://hachyderm.io/@{address.Split('@')[0]}",
    };
}
