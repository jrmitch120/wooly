namespace Wooly.Tui.Shell;

/// <summary>
///     Which destinations have their own list read in flight — an arrival at one, or a refresh of it — and which
///     re-reads of an Inbox count were held back from the screen for one (story 11 of #395).
/// </summary>
/// <remarks>
///     A re-read is held back only by its own destination's list read: arriving at a destination draws a screen with
///     nothing on it until the list lands, and that read — with its own count — is about to fill the screen itself. A
///     re-read let in first would fill a screen about to be replaced. Anything else in flight — a dismiss, a request
///     answered, a page, a favourite — leaves the screen as it is, and holds nothing back (stories 20–23).
///     <para>
///         What a held-back re-read found may not be on the list that lands — the list may have been read before it
///         arrived — so it is not dropped: where the last list read of the destination is over, the count is read
///         again, and what is new goes on top then. Only ever touched on the drawing thread.
///     </para>
/// </remarks>
internal sealed class ListReads
{
    private readonly Dictionary<DestinationKind, int> _inFlight = [];

    private readonly HashSet<DestinationKind> _heldBack = [];

    /// <summary>A list read of <paramref name="kind" /> has been put.</summary>
    public void Began(DestinationKind kind) => _inFlight[kind] = _inFlight.GetValueOrDefault(kind) + 1;

    /// <summary>
    ///     A list read of <paramref name="kind" /> is over, landed or not.
    /// </summary>
    /// <returns>Whether a re-read was held back for it, and is now to be made again.</returns>
    public bool Ended(DestinationKind kind)
    {
        var left = _inFlight.GetValueOrDefault(kind) - 1;

        if (left > 0)
        {
            _inFlight[kind] = left;

            return false;
        }

        _inFlight.Remove(kind);

        return _heldBack.Remove(kind);
    }

    /// <summary>
    ///     Whether what a re-read of <paramref name="kind" /> found is to be kept off the screen, because its own list
    ///     read is in flight — noted, where it is, so that the count is read again once that read is over.
    /// </summary>
    public bool HoldsBack(DestinationKind kind)
    {
        if (!_inFlight.ContainsKey(kind))
        {
            return false;
        }

        _heldBack.Add(kind);

        return true;
    }
}
