namespace Wooly.Cli.Commands;

/// <summary>
///     What every timeline command takes, whichever timeline it reads: which profile to read as, how many posts to
///     ask for, and whether the answer is for a person or for another program — the last two shared with every other
///     paged list this client reads.
/// </summary>
internal class TimelineSettings : PagedListSettings
{
    /// <inheritdoc />
    protected override string Counted => "post";
}
