namespace Wooly.Tui.Media;

/// <summary>
///     The pictures the TUI has, fetched once each and held for as long as they are worth holding. Asked on every draw
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
public sealed class Pictures(
    Func<string, CancellationToken, Task<byte[]?>> fetch,
    Func<CellSize?> cell,
    Action arrived) : IPictures, IDisposable
{
    /// <summary>
    ///     How many pictures are held at once. Only a handful can be on screen and only what is near the screen is ever
    ///     sent for, so this is a scroll or two of slack rather than a gallery — and a picture is megabytes once it is
    ///     pixels, so a client holding a morning's scrolling would be holding a morning's scrolling in memory.
    ///     <para>
    ///         Roomy enough for a screen's worth of posts to hold their attachments and their authors' avatars at
    ///         once: a cache too small for both would drop and re-fetch an avatar every frame, which is a fetch per
    ///         keypress — the very thing holding pictures at all exists to prevent.
    ///     </para>
    /// </summary>
    public const int MostHeld = 48;

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
    private readonly Queue<string> _order = new();
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
    public static Pictures Over(HttpClient http, Func<CellSize?> cell, Action arrived) => new(
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
        arrived);

    /// <inheritdoc />
    public Picture? Of(Drawn drawn)
    {
        lock (_gate)
        {
            return _held.GetValueOrDefault(drawn.Id);
        }
    }

    /// <inheritdoc />
    public void Want(Drawn drawn)
    {
        lock (_gate)
        {
            if (_held.ContainsKey(drawn.Id))
            {
                return;
            }

            // Written down before the fetch goes out, and holding null until it lands: that is what makes asking on
            // every frame cost one fetch rather than one a frame, and what stops a picture that cannot be had from
            // being asked for again for as long as it is remembered.
            Remember(drawn.Id, picture: null);
        }

        _ = Fetch(drawn);
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
            // Remembered afresh where it has since been dropped, so that what is held and the order it is dropped in
            // cannot come apart and leave the cache growing without a bound.
            if (_held.ContainsKey(drawn.Id))
            {
                _held[drawn.Id] = picture;
            }
            else
            {
                Remember(drawn.Id, picture);
            }
        }

        arrived();
    }

    /// <summary>Holds a picture, dropping the one held longest once there are more than there is room for.</summary>
    private void Remember(string id, Picture? picture)
    {
        _held[id] = picture;
        _order.Enqueue(id);

        while (_order.Count > MostHeld)
        {
            _held.Remove(_order.Dequeue());
        }
    }
}
