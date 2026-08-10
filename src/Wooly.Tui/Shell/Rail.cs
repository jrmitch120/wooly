namespace Wooly.Tui.Shell;

/// <summary>
///     The rail, and the cursor and selection it tracks separately because they answer to different things
///     (ADR-0014). <c>Cursor</c> is where the tabbing has got to, drawn <c>▶</c>; <c>Current</c> is what is actually
///     being shown, drawn <c>▷</c> only while it differs from the cursor — the two coincide at rest, so a row shows
///     at most one mark (#78).
///     <para>
///         The cursor moves on every press, immediately, because a key that draws nothing for a quarter of a second
///         reads as lag however much work it is saving. The selection follows it only when the pressing stops for
///         <see cref="Settle" />, and moving the selection is what asks the instance for anything. Six tabs are six
///         cursor moves, one selection and one fetch.
///     </para>
///     <para>
///         There is no third mark. A destination chosen but not loaded does not exist here, and a fetch in flight is
///         said once on the breadcrumb — a rail somebody is reading should hold still.
///     </para>
/// </summary>
public sealed class Rail
{
    private readonly List<Destination> _destinations;
    private readonly IShellHost _host;

    private IDisposable? _settling;

    /// <param name="destinations">The nine, in the order they are drawn.</param>
    /// <param name="host">What the wait is scheduled through.</param>
    /// <param name="settle">
    ///     How long the tabbing has to stop for before the selection follows the cursor. Long enough that a deliberate
    ///     double-tap lands as one move; short enough that a single tab does not read as lag.
    /// </param>
    public Rail(IReadOnlyList<Destination> destinations, IShellHost host, TimeSpan settle)
    {
        if (destinations.Count == 0)
        {
            throw new ArgumentException("A rail with nowhere to go is not a rail.", nameof(destinations));
        }

        _destinations = [.. destinations];
        _host = host;
        Settle = settle;
    }

    /// <summary>Raised when the selection has followed the cursor, which is the moment a destination is asked for.</summary>
    public event Action<Destination>? Selected;

    /// <summary>Raised whenever anything the rail draws has changed, including the cursor moving on its own.</summary>
    public event Action? Changed;

    /// <summary>How long the tabbing has to stop for before the selection follows the cursor.</summary>
    public TimeSpan Settle { get; }

    /// <summary>The nine, in the order they are drawn.</summary>
    public IReadOnlyList<Destination> Destinations => _destinations;

    /// <summary>Where the tabbing has got to, drawn as <c>▶</c>.</summary>
    public int Cursor { get; private set; }

    /// <summary>What is being shown, drawn as <c>▷</c> only while it differs from <see cref="Cursor" />.</summary>
    public int Current { get; private set; }

    /// <summary>The destination being shown.</summary>
    public Destination Showing => _destinations[Current];

    /// <summary>
    ///     Moves the cursor by <paramref name="by" /> places, wrapping at either end, and restarts the settle window —
    ///     abandoning whatever the press before it left waiting.
    /// </summary>
    public void Step(int by)
    {
        Cursor = Wrapped(Cursor + by);

        // Every press abandons the schedule the one before it left. This is the whole of the mechanism: without it a
        // walk from Home to Follow requests was six fetches with five thrown away.
        _settling?.Dispose();
        _settling = _host.After(Settle, Land);

        Changed?.Invoke();
    }

    /// <summary>
    ///     Puts the cursor and the selection on <paramref name="kind" /> at once, with no settle and no wait, for the
    ///     places a destination is arrived at other than by tabbing to it — opening the shell, and walking back out of
    ///     a drill that started somewhere else.
    /// </summary>
    public void GoTo(DestinationKind kind)
    {
        var index = IndexOf(kind);

        _settling?.Dispose();
        _settling = null;

        Cursor = index;

        if (Current != index)
        {
            Current = index;
            Selected?.Invoke(_destinations[index]);
        }

        Changed?.Invoke();
    }

    /// <summary>
    ///     Replaces a destination with itself carrying a different unread count, or a different tag. The rail is drawn
    ///     from these, so this is how a count that arrived after the shell opened reaches the screen.
    /// </summary>
    public void Update(Destination destination)
    {
        _destinations[IndexOf(destination.Kind)] = destination;

        Changed?.Invoke();
    }

    private int IndexOf(DestinationKind kind)
    {
        var index = _destinations.FindIndex(destination => destination.Kind == kind);

        return index >= 0
            ? index
            : throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a destination on this rail.");
    }

    /// <summary>
    ///     The settle window closing: the selection catches up with the cursor, and that destination — only that one —
    ///     is asked for. A walk that ended where it started asks for nothing, because nothing moved.
    /// </summary>
    private void Land()
    {
        _settling = null;

        if (Current == Cursor)
        {
            return;
        }

        Current = Cursor;

        Changed?.Invoke();
        Selected?.Invoke(Showing);
    }

    private int Wrapped(int index) => ((index % _destinations.Count) + _destinations.Count) % _destinations.Count;
}
