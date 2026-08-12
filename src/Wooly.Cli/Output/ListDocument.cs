using Spectre.Console;
using Wooly.Core.Paging;

namespace Wooly.Cli.Output;

/// <summary>
///     The envelope ADR-0007 puts round every paged list written for another program: whether the list is all of what
///     was asked for, the rate limit that stopped the rest if one did, and then the things themselves. An object rather
///     than a bare array, because a list cut short by a rate limit and a list with nothing on it would otherwise both
///     be <c>[]</c>, and under a pipe the exit code is gone by the time the JSON is parsed.
///     <para>
///         One spelling of it, so that a script reading a timeline the way it reads an inbox is reading the same fields
///         in the same order. It was four private records saying the same three things, which is three chances for one
///         list to start answering a rate limit unlike the others (#101).
///     </para>
/// </summary>
internal static class ListDocument
{
    /// <summary>Writes <paramref name="fetch" /> in the envelope, under the name its contents go by on the wire.</summary>
    /// <param name="asDocument">
    ///     How one of them is spelled — <see cref="PostDocument.Of" /> and the like, so that a post read on a timeline
    ///     and the same post read in a notification cannot come to look like two different posts.
    /// </param>
    /// <param name="plural">
    ///     What the list is called in the output: <c>posts</c>, <c>accounts</c>, <c>notifications</c>,
    ///     <c>conversations</c>. Passed in rather than taken from what the list holds, because ADR-0007 settles the
    ///     wire names here at the serialization seam — a domain property renamed is not a reason for a
    ///     <c>jq</c> filter somebody wrote to stop matching.
    /// </param>
    /// <param name="about">
    ///     What this list is, ahead of the envelope's own three fields, for the lists that say so — which timeline and
    ///     which hashtag, which side of a follow and whose. A field whose value is <see langword="null" /> is one that
    ///     does not apply here, and is left out.
    /// </param>
    public static void Write<TItem, TDocument>(
        IAnsiConsole console,
        Fetch<TItem> fetch,
        Func<TItem, TDocument> asDocument,
        string plural,
        params (string Field, string? Value)[] about)
    {
        // Written field by field rather than as a record because the plural is only known at the call site, and a
        // record's field names are attributes fixed at compile time. Ordered, because the order these come out in is
        // as much a part of the output as the names are.
        var document = new OrderedDictionary<string, object?>();

        foreach (var (field, value) in about)
        {
            // Left out by hand: JsonOutput's rule about not writing nulls is a rule about a record's properties, and
            // reaches nothing put in here.
            if (value is not null)
            {
                document[field] = value;
            }
        }

        document["complete"] = fetch.IsComplete;

        if (RateLimitDocument.Of(fetch.StoppedBy) is { } rateLimit)
        {
            document["rateLimit"] = rateLimit;
        }

        document[plural] = fetch.Items.Select(asDocument).ToList();

        JsonOutput.Write(console, document);
    }
}
