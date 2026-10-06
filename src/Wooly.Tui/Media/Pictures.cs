namespace Wooly.Tui.Media;

/// <summary>
///     The pictures the TUI has, fetched once each and held for as long as a frame has wanted them recently enough. Asked on every draw
///     and answering at once, because a screen is redrawn on every keypress and a fetch per frame would be a fetch per
///     keypress (ADR-0016).
/// </summary>
/// <param name="fetch">
///     How the bytes at an address are got. A delegate rather than an <see cref="HttpClient" /> so that a test can
///     answer without a socket, and so the one thing this class is about — asked once, held, and announced when it
///     lands — is testable on its own.
/// </param>
/// <param name="cell">
///     How big a cell is on this terminal, or <see langword="null" /> where it draws no pictures at all. Asked afresh
///     each time rather than settled once, because the terminal answers the questions behind it some frames after the
///     shell is already on screen (<see cref="RasterProtocol" />).
/// </param>
/// <param name="arrived">
///     What to do when a picture lands: redraw, so the rows that have been waiting for it fill in. Called off the
///     thread the fetch finished on, so whatever is passed here is what has to get back to the UI thread.
/// </param>
/// <param name="dropped">
///     What to do when a picture is let go of to make room, given its <see cref="Drawn.Id" />: tell a Kitty terminal
///     holding a copy to let go of it too (ADR-0022). Called on the thread that said what a frame wants, since that is
///     the only thing that makes room.
/// </param>
public sealed class Pictures(
    Func<string, CancellationToken, Task<byte[]?>> fetch,
    Func<CellSize?> cell,
    Action arrived,
    Action<string>? dropped = null) : IPictures, IDisposable
{
    /// <summary>
    ///     How many pictures are held at once. Only a handful can be on screen and only what is near the screen is ever
    ///     sent for, so this is a scroll or two of slack rather than a gallery — and a picture is megabytes once it is
    ///     pixels, so a client holding a morning's scrolling would be holding a morning's scrolling in memory.
    ///     <para>
    ///         Raised from sixteen when a byline gained an avatar (#77), which about doubled how many pictures a
    ///         screenful wants: the three screens either side of the viewport that <c>Want</c> reaches over are some
    ///         ten posts, and ten posts can want ten avatars and their attachments besides. A cache too small for both
    ///         would drop and re-fetch an avatar every frame, which is a fetch per keypress — the very thing holding
    ///         pictures at all exists to prevent. An avatar is a thumbnail rather than a photograph, so the memory
    ///         this admits is nothing like twice what sixteen did.
    ///     </para>
    ///     <para>
    ///         Past this only for what is on screen, which is never let go of however many pictures that is; the
    ///         screen is small, so how far past is bounded (ADR-0025).
    ///     </para>
    /// </summary>
    public const int MostHeld = 32;

    /// <summary>
    ///     How many bytes of a download are worth reading. A preview is tens of kilobytes; anything of this size is
    ///     either not a preview or not worth the memory of finding out which.
    /// </summary>
    public const int MostBytes = 8 * 1024 * 1024;

    /// <summary>
    ///     How many pictures are fetched and decoded at once. Small on purpose: see <see cref="Fetch" />.
    /// </summary>
    public const int AtATime = 4;

    /// <summary>
    ///     How long a preview is waited for. Short, because nothing is waiting on it: the post is already on screen
    ///     saying what is attached to it, and a picture that has not arrived by now is one the reader has read past.
    /// </summary>
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private readonly Lock _gate = new();
    private readonly SemaphoreSlim _atATime = new(AtATime, AtATime);
    private readonly Dictionary<string, Picture?> _held = [];

    /// <summary>What is held, wanted longest ago first — the order it is let go of in.</summary>
    private readonly LinkedList<string> _wanted = new();

    /// <summary>Where each held picture is in <see cref="_wanted" />, so that wanting it again is not a search.</summary>
    private readonly Dictionary<string, LinkedListNode<string>> _places = [];

    /// <summary>What the latest frame had on screen, which is never let go of.</summary>
    private HashSet<string> _onScreen = [];
    private readonly CancellationTokenSource _abandoned = new();

    /// <inheritdoc />
    public CellSize? Cell => cell();

    /// <summary>Stops anything still being fetched, for a shell that is closing.</summary>
    public void Dispose()
    {
        _abandoned.Cancel();
        _abandoned.Dispose();
        _atATime.Dispose();
    }

    /// <summary>
    ///     Everything the shell needs to fetch and hold pictures, wired to <paramref name="http" />.
    /// </summary>
    /// <param name="cell">How big a cell is — see the constructor.</param>
    /// <param name="arrived">What to do when one lands — see the constructor.</param>
    /// <param name="dropped">What to do when one is let go of — see the constructor.</param>
    public static Pictures Over(
        HttpClient http,
        Func<CellSize?> cell,
        Action arrived,
        Action<string>? dropped = null) => new(
        async (address, cancellation) =>
        {
            // Headers first, so that a length worth refusing is refused before the body is read rather than after it
            // has already been held in memory.
            using var response = await http.GetAsync(address, HttpCompletionOption.ResponseHeadersRead, cancellation);

            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > MostBytes)
            {
                return null;
            }

            await using var body = await response.Content.ReadAsStreamAsync(cancellation);

            return await Read(body, cancellation);
        },
        cell,
        arrived,
        dropped);

    /// <inheritdoc />
    public Picture? Of(Drawn drawn)
    {
        lock (_gate)
        {
            return _held.GetValueOrDefault(drawn.Id);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    ///     Only as much of <paramref name="frame" /> is taken as there is room for, nearest first, and everything on
    ///     screen whatever room that leaves. Taking the rest would send for pictures only to let go of them again at
    ///     once, and send for them again on the next frame, which is a fetch per keypress.
    /// </remarks>
    public void Want(IReadOnlyList<WantedPicture> frame)
    {
        var sending = new List<Drawn>();
        List<string> letGo;

        lock (_gate)
        {
            var taking = Taken(frame);

            _onScreen = [.. taking.Where(wanted => wanted.OnScreen).Select(wanted => wanted.Drawn.Id)];

            // Renewed farthest first, so that the nearest ends up the most recently wanted and is the last of this
            // frame's to be let go of.
            for (var at = taking.Count - 1; at >= 0; at--)
            {
                var drawn = taking[at].Drawn;

                if (_places.Remove(drawn.Id, out var place))
                {
                    _wanted.Remove(place);
                }
                else
                {
                    // Written down before the fetch goes out, and holding null until it lands: that is what makes
                    // asking on every frame cost one fetch rather than one a frame, and what stops a picture that
                    // cannot be had from being asked for again for as long as it is remembered.
                    _held[drawn.Id] = null;
                    sending.Add(drawn);
                }

                _places[drawn.Id] = _wanted.AddLast(drawn.Id);
            }

            letGo = LetGo();

            // Nearest first, and none that this very frame has already let go of.
            sending.Reverse();
            sending.RemoveAll(drawn => !_held.ContainsKey(drawn.Id));
        }

        Announce(letGo);

        foreach (var drawn in sending)
        {
            _ = Fetch(drawn);
        }
    }

    /// <summary>
    ///     The part of <paramref name="frame" /> there is room for: its nearest <see cref="MostHeld" /> pictures, and
    ///     every one on screen past them. Each picture once, where a frame wants the same one twice — the same author's
    ///     avatar on two posts — and on screen if either was.
    /// </summary>
    private static List<WantedPicture> Taken(IReadOnlyList<WantedPicture> frame)
    {
        var taking = new List<WantedPicture>();
        var where = new Dictionary<string, int>();

        foreach (var wanted in frame)
        {
            if (where.TryGetValue(wanted.Drawn.Id, out var at))
            {
                taking[at] = taking[at] with { OnScreen = taking[at].OnScreen || wanted.OnScreen };
            }
            else if (taking.Count < MostHeld || wanted.OnScreen)
            {
                where[wanted.Drawn.Id] = taking.Count;
                taking.Add(wanted);
            }
        }

        return taking;
    }

    /// <summary>
    ///     What <paramref name="body" /> holds, or <see langword="null" /> where it holds more than
    ///     <see cref="MostBytes" />. Read in pieces and counted as it goes rather than taken whole, because a server
    ///     that declares no length — or declares one and sends another — would otherwise decide how much of this
    ///     client's memory to use.
    /// </summary>
    private static async Task<byte[]?> Read(Stream body, CancellationToken cancellation)
    {
        using var bytes = new MemoryStream();
        var piece = new byte[64 * 1024];

        while (bytes.Length <= MostBytes)
        {
            var read = await body.ReadAsync(piece, cancellation);

            if (read == 0)
            {
                return bytes.ToArray();
            }

            bytes.Write(piece, 0, read);
        }

        return null;
    }

    private async Task Fetch(Drawn drawn)
    {
        Picture? picture = null;

        try
        {
            // A few at a time. Decoding holds the whole of a picture in memory before it is scaled down, so a screenful
            // arriving at once is a screenful of originals held at once — which is how this ran a machine out of memory
            // rather than merely making it wait (ADR-0016).
            await _atATime.WaitAsync(_abandoned.Token);

            try
            {
                if (await fetch(drawn.Address, _abandoned.Token) is { } bytes)
                {
                    picture = PictureDecoder.From(bytes);
                }
            }
            finally
            {
                _atATime.Release();
            }
        }
        catch (Exception failure) when (failure is not OutOfMemoryException)
        {
            // A picture nobody can fetch is not something to tell a reader about: the post already says what is
            // attached to it, and an error row where a photograph was meant to be would be worse than the description.
        }

        // Already remembered as nothing by Of, and left that way so it is not asked for again. A shell that has been
        // closed is not told about a picture either: there is nothing left to draw it on.
        if (picture is null || _abandoned.IsCancellationRequested)
        {
            return;
        }

        lock (_gate)
        {
            // Let go of while it was on its way, so wanted by no frame since: not taken back, because taking it back
            // as the most recently wanted would let go of something a frame did want to make room for it.
            if (!_held.ContainsKey(drawn.Id))
            {
                return;
            }

            _held[drawn.Id] = picture;
        }

        arrived();
    }

    /// <summary>
    ///     Lets go of the pictures wanted longest ago until there are no more than there is room for, passing over any
    ///     on screen in the latest frame however far over that leaves it, and says which were let go of — to be told
    ///     outside the lock, since whoever is told may take one of its own.
    /// </summary>
    private List<string> LetGo()
    {
        var letGo = new List<string>();
        var place = _wanted.First;

        while (_held.Count > MostHeld && place is not null)
        {
            var next = place.Next;

            if (!_onScreen.Contains(place.Value))
            {
                _wanted.Remove(place);
                _places.Remove(place.Value);
                _held.Remove(place.Value);
                letGo.Add(place.Value);
            }

            place = next;
        }

        return letGo;
    }

    /// <summary>Says which pictures were let go of, outside the lock <see cref="LetGo" /> was called under.</summary>
    private void Announce(List<string> letGo)
    {
        if (dropped is null)
        {
            return;
        }

        letGo.ForEach(dropped);
    }
}
