using System.Text.Json.Serialization;
using Wooly.Core.Errors;

namespace Wooly.Cli.Output;

/// <summary>
///     How a rate limit that cut a fetch short is spelled for another program to read. Shared by every list command
///     that can be stopped by one, for the reason <see cref="PostDocument" /> gives: a second spelling is how a timeline
///     and an inbox would come to describe the same refusal differently to the same <c>jq</c> filter.
/// </summary>
internal sealed record RateLimitDocument(
    [property: JsonPropertyName("instance")] string Instance,
    [property: JsonPropertyName("resetsAt")] DateTimeOffset? ResetsAt)
{
    /// <summary>How <paramref name="rateLimit" /> is written down, or nothing at all where no limit stopped anything.</summary>
    public static RateLimitDocument? Of(RateLimitedException? rateLimit) =>
        rateLimit is null ? null : new RateLimitDocument(rateLimit.Instance, rateLimit.ResetsAt);
}
