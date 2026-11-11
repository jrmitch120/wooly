using System.Net.Http.Json;
using Wooly.Core.Errors;
using Wooly.Core.Http;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;
using Wooly.Core.Search;
using WireStatus = Mastonet.Entities.Status;
using WireTag = Mastonet.Entities.Tag;

namespace Wooly.Core.Trends;

/// <summary>
///     Reads the three trends endpoints raw, through <see cref="RawMastodonCall" /> over the named
///     <see cref="HttpClient" /> every Mastonet call runs through, so a trends read is retried and rate-limit-checked
///     like any other. Raw for all three rather than for links alone, because Mastonet 3.1.3 has no trending links,
///     keeps its trending posts off its interface, and cannot read offset paging — and one adapter asking the same
///     way of all three is one way a page is asked for (ADR-0036).
///     <para>
///         A refusal is raised as a <see cref="TrendsRefusedException" /> rather than passed on as the HTTP stack's
///         own exception: a <see cref="WoolyException" /> is what the TUI's enquiry says on the status row, and
///         anything else reaching it is said by nobody.
///     </para>
/// </summary>
public sealed class InstanceTrends(IHttpClientFactory httpClientFactory) : ITrends
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<Hashtag>> Hashtags(
        ActiveProfile profile,
        TrendPage page,
        CancellationToken cancellationToken)
    {
        var answered = await Read<WireTag>(profile, "api/v1/trends/tags", page, cancellationToken);

        return answered.Select(HashtagWire.ToHashtag).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Post>> Posts(
        ActiveProfile profile,
        TrendPage page,
        CancellationToken cancellationToken)
    {
        var answered = await Read<WireStatus>(profile, "api/v1/trends/statuses", page, cancellationToken);

        return answered.Select(status => PostWire.ToPost(status, profile)).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TrendingLink>> Links(
        ActiveProfile profile,
        TrendPage page,
        CancellationToken cancellationToken)
    {
        var answered = await Read<TrendingLinkWire>(profile, "api/v1/trends/links", page, cancellationToken);

        // A card with no address is not a link anybody can open or read the posts sharing, so it is dropped rather
        // than drawn as a row that goes nowhere.
        return answered.Where(link => !string.IsNullOrWhiteSpace(link.Url))
                       .Select(link => link.ToTrendingLink())
                       .ToList();
    }

    /// <summary>
    ///     One page of <paramref name="path" />, or nothing where the instance answered with nothing — which is how an
    ///     instance with trends turned off answers.
    /// </summary>
    private async Task<List<TWire>> Read<TWire>(
        ActiveProfile profile,
        string path,
        TrendPage page,
        CancellationToken cancellationToken)
    {
        using var response = await RawMastodonCall.Sent(
            httpClientFactory,
            profile,
            HttpMethod.Get,
            $"{path}{QueryString(page)}",
            content: null,
            cancellationToken);

        await InstanceError.EnsureAnswered(profile, response, TrendsRefusedException.For, cancellationToken);

        return await response.Content.ReadFromJsonAsync<List<TWire>>(cancellationToken) ?? [];
    }

    /// <summary>
    ///     The page as the instance takes it, saying only what was asked: no limit leaves the instance its own default,
    ///     and an offset of none is the top of the ranking whether or not it is sent.
    /// </summary>
    private static string QueryString(TrendPage page)
    {
        List<string> parameters = [];

        if (page.Limit is { } limit)
        {
            parameters.Add($"limit={limit}");
        }

        if (page.Offset > 0)
        {
            parameters.Add($"offset={page.Offset}");
        }

        return parameters.Count == 0 ? string.Empty : $"?{string.Join("&", parameters)}";
    }
}
