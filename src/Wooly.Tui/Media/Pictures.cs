using Wooly.Core.Posts;

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
    ///     How many pictures are held at once. A feed shows a handful of posts and a reader scrolls through a few
    ///     screens of them; past that, the oldest is the one least likely to be looked at again, and a client holding
    ///     every picture of a morning's scrolling would be holding a morning's scrolling in memory.
    /// </summary>
    public const int MostHeld = 32;

    /// <summary>
    ///     How many bytes of a download are worth reading. A preview is tens of kilobytes; anything of this size is
    ///     either not a preview or not worth the memory of finding out which.
    /// </summary>
    public const int MostBytes = 8 * 1024 * 1024;

    /// <summary>
    ///     How long a preview is waited for. Short, because nothing is waiting on it: the post is already on screen
    ///     saying what is attached to it, and a picture that has not arrived by now is one the reader has read past.
    /// </summary>
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private readonly Lock _gate = new();
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
    public Picture? Of(PostMedia media)
    {
        lock (_gate)
        {
            if (_held.TryGetValue(media.Id, out var held))
            {
                return held;
            }

            // Written down before the fetch goes out, and holding null until it lands: that is what makes asking on
            // every draw cost one fetch rather than one a frame, and what stops a picture that cannot be had from
            // being asked for again for as long as it is remembered.
            Remember(media.Id, picture: null);
        }

        _ = Fetch(media);

        return null;
    }

    /// <summary>
    ///     The smaller copy where the instance offered one, and the file itself where it did not. A terminal draws a
    ///     few hundred pixels across at most, and fetching a photograph at full size to throw nine tenths of it away is
    ///     somebody's data allowance.
    /// </summary>
    private static string AddressOf(PostMedia media) => media.Preview ?? media.Url;

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

    private async Task Fetch(PostMedia media)
    {
        Picture? picture = null;

        try
        {
            if (await fetch(AddressOf(media), _abandoned.Token) is { } bytes)
            {
                picture = PictureDecoder.From(bytes);
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
            if (_held.ContainsKey(media.Id))
            {
                _held[media.Id] = picture;
            }
            else
            {
                Remember(media.Id, picture);
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
