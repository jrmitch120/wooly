using System.Text.RegularExpressions;

namespace Wooly.Core.Http;

/// <summary>
///     Where an instance says the next page of a list starts, off the <c>Link</c> header it sends with a page — what
///     Mastonet reads for a call made through it, read here for one made around it (<see cref="RawMastodonCall" />).
/// </summary>
internal static partial class LinkHeader
{
    /// <summary>
    ///     The <c>max_id</c> of the link marked <c>rel="next"</c>, or <see langword="null" /> where the answer names no
    ///     next page.
    /// </summary>
    public static string? NextMaxId(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Link", out var values))
        {
            return null;
        }

        foreach (var link in values.SelectMany(value => value.Split(',')))
        {
            if (Next().Match(link) is { Success: true } next)
            {
                return Uri.UnescapeDataString(next.Groups["id"].Value);
            }
        }

        return null;
    }

    [GeneratedRegex("""<[^>]*[?&]max_id=(?<id>[^&>]+)[^>]*>\s*;\s*rel="?next"?""")]
    private static partial Regex Next();
}
