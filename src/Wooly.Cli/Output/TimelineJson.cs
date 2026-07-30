using System.Text.Json.Serialization;
using Spectre.Console;
using Wooly.Core.Timelines;

namespace Wooly.Cli.Output;

/// <summary>
///     Writes a timeline for another program to read. An object rather than a bare array of posts, per ADR-0007: a
///     timeline cut short by a rate limit and a timeline with nothing on it would otherwise both be <c>[]</c>, and under
///     a pipe the exit code is gone by the time the JSON is parsed. The posts themselves are
///     <see cref="PostDocument" />s, spelled the one way every command spells a post.
/// </summary>
internal static class TimelineJson
{
    public static void Write(IAnsiConsole console, Timeline timeline, TimelineFetch fetch)
    {
        var document = new TimelineDocument(
            NameOf(timeline.Scope),
            timeline.Hashtag,
            fetch.IsComplete,
            RateLimitDocument.Of(fetch.StoppedBy),
            fetch.Posts.Select(PostDocument.Of).ToList());

        JsonOutput.Write(console, document);
    }

    /// <summary>
    ///     What each timeline is called in the output. Spelled out for the same reason the field names are: derived from
    ///     the enum's own member names, renaming one would silently change a value somebody is matching on.
    /// </summary>
    private static string NameOf(TimelineScope scope) => scope switch
    {
        TimelineScope.Home => "home",
        TimelineScope.Local => "local",
        TimelineScope.Federated => "federated",
        TimelineScope.Tag => "tag",
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Not a timeline this client reads."),
    };

    /// <param name="Complete">
    ///     Whether every post asked for was read. False says the rest was cut short, which an empty <c>posts</c>
    ///     otherwise could not be told from a timeline with nothing on it.
    /// </param>
    private sealed record TimelineDocument(
        [property: JsonPropertyName("timeline")] string Timeline,
        [property: JsonPropertyName("hashtag")] string? Hashtag,
        [property: JsonPropertyName("complete")] bool Complete,
        [property: JsonPropertyName("rateLimit")] RateLimitDocument? RateLimit,
        [property: JsonPropertyName("posts")] IReadOnlyList<PostDocument> Posts);
}
