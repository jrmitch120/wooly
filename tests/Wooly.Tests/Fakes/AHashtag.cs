using Wooly.Core.Search;

namespace Wooly.Tests.Fakes;

/// <summary>A hashtag a search turned up, with its recent usage filled in.</summary>
internal static class AHashtag
{
    public static Hashtag With(string name = "cats", long recentPosts = 42, long recentAccounts = 30) => new()
    {
        Name = name,
        RecentPosts = recentPosts,
        RecentAccounts = recentAccounts,
        Url = $"https://mastodon.social/tags/{name}",
    };
}
