using System.Globalization;

namespace Wooly.Core;

/// <summary>
///     A count as a reader reads it — grouped the way their own machine groups numbers, so <c>4210</c> is
///     <c>4,210</c> here and <c>4.210</c> to somebody whose machine says so.
/// </summary>
/// <remarks>
///     Said in one place because a boost count, a follower count and a hashtag's recent posts are the same kind of
///     thing, and three screens each formatting one is three chances for a number to be grouped one way beside a
///     number grouped another. Here rather than among the TUI's rendering because a poll option's vote count is
///     written by both surfaces from <see cref="Posts.PollBar" />, and reading a thousand is no more a screen's
///     business than a pipe's (#150).
/// </remarks>
public static class Number
{
    /// <summary>How many, written out.</summary>
    public static string Of(long count) => count.ToString("N0", CultureInfo.CurrentCulture);
}
