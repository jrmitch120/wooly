namespace Wooly.Core.Errors;

/// <summary>
///     A link's sharing posts were asked for, and the instance has none to give because the link is not — or is no
///     longer — one of its <b>Trending link</b>s (#603). An instance keeps the posts sharing a link only while the link
///     trends, and answers any other with a 404; that is a not-found answer, told apart from a trending link that
///     nobody has posted about yet, which is a timeline with nothing on it.
///     <para>
///         Reported like an unknown account: a value on the command line that is wrong, rather than a client that could
///         not do its job (ADR-0006). Only the first page of a read from the top raises it; a link that stops trending
///         while it is being read on simply ends the timeline, keeping what was already read
///         (<see cref="Timelines.TimelineReader" />).
///     </para>
/// </summary>
public sealed class LinkNotTrendingException(Uri link, string instance)
    : WoolyException(
        $"{link.OriginalString} is not trending on {instance}, so it has no posts sharing it to read. "
        + "Only a link the instance ranks as trending has them.")
{
    /// <summary>The address that was asked after.</summary>
    public Uri Link { get; } = link;
}
