using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spectre.Console;
using Wooly.Core.Timelines;

namespace Wooly.Cli.Output;

/// <summary>
///     Writes a timeline for another program to read. The field names below are a contract with whatever is parsing
///     them, which is why they are spelled out here rather than derived from the domain records — a rename in the
///     domain must not silently rename somebody's <c>jq</c> filter. They are this project's vocabulary, not the API's.
/// </summary>
internal static class TimelineJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,

        // A null field is a field that does not apply — no content warning, no boost, no rate limit — and leaving it
        // out says that more plainly than a null does.
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

        // This output goes to a terminal or a pipe, never into HTML, so a post written in Japanese should read as
        // Japanese rather than as a run of \u escapes.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static void Write(IAnsiConsole console, Timeline timeline, TimelineFetch fetch)
    {
        var document = new TimelineDocument(
            NameOf(timeline.Scope),
            timeline.Hashtag,
            fetch.IsComplete,
            fetch.StoppedBy is null ? null : new RateLimitDocument(fetch.StoppedBy.Instance, fetch.StoppedBy.ResetsAt),
            fetch.Posts.Select(ToDocument).ToList());

        console.WriteUnwrapped(JsonSerializer.Serialize(document, Options));
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

    private static PostDocument ToDocument(Post post) => new(
        post.Id,
        post.Account,
        post.Author,
        post.PostedAt,
        post.ContentWarning,
        post.Content,
        post.Boosts,
        post.Favorites,
        post.Replies,
        post.Url,
        post.Boosted is null ? null : ToDocument(post.Boosted));

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

    private sealed record RateLimitDocument(
        [property: JsonPropertyName("instance")] string Instance,
        [property: JsonPropertyName("resetsAt")] DateTimeOffset? ResetsAt);

    private sealed record PostDocument(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("account")] string Account,
        [property: JsonPropertyName("author")] string Author,
        [property: JsonPropertyName("postedAt")] DateTimeOffset PostedAt,
        [property: JsonPropertyName("contentWarning")] string? ContentWarning,
        [property: JsonPropertyName("content")] string Content,
        [property: JsonPropertyName("boosts")] long Boosts,
        [property: JsonPropertyName("favorites")] long Favorites,
        [property: JsonPropertyName("replies")] long Replies,
        [property: JsonPropertyName("url")] string? Url,
        [property: JsonPropertyName("boosted")] PostDocument? Boosted);
}
