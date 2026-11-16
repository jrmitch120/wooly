using Wooly.Core.Trends;

namespace Wooly.Tests.Fakes;

/// <summary>A trending link with everything an instance usually says about one filled in.</summary>
internal static class ATrendingLink
{
    public static TrendingLink With(
        string url = "https://news.example/cats-learn-to-type",
        string title = "Cats learn to type",
        string? description = "A study of keyboards and the cats who sit on them.",
        string site = "Example News",
        long peopleSharing = 65) => new()
    {
        Url = new Uri(url),
        Title = title,
        Description = description,
        Site = site,
        PeopleSharing = peopleSharing,
    };
}
