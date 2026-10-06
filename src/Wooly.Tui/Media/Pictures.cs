using Size = SixLabors.ImageSharp.Size;

namespace Wooly.Tui.Media;

/// <summary>
///     The pictures the TUI has, fetched once each and held for as long as a frame has wanted them recently enough.
///     Asked on every draw and answering at once, because a screen is redrawn on every keypress and a fetch per frame
///     would be a fetch per keypress (ADR-0016).
/// </summary>
/// <remarks>
///     Held in two tiers, each budgeted in bytes and each letting go of the picture a frame wanted longest ago
///     (ADR-0025): the decoded pixels, which are what <see cref="Of" /> answers from, and the downloaded files they were
///     decoded from. Pixels cost ten to fifty times what their file does, so letting go of the pixels while keeping the
///     file makes a picture scrolled back to cost a decode rather than a fetch — and makes a window grown wider cost a
///     decode too, rather than a picture drawn blurred or fetched again.
/// </remarks>
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
///     What to do when a picture's pixels are let go of to make room, given its <see cref="Drawn.Id" />: tell a Kitty
///     terminal holding a copy to let go of it too (ADR-0022). Only for the decoded tier: a picture whose file alone is
///     let go of has nothing on the terminal that this client is not still drawing. Called on whichever thread made the
///     room — the one saying what a frame wants, or the one a picture landed on.
/// </param>
/// <param name="columns">
///     How many columns wide the window is now, or <see langword="null" /> where nothing says. With the cell, that is
///     the largest box a picture could be drawn in here, which is the size it is decoded to (ADR-0025). Asked afresh
///     each frame, on the thread saying what the frame wants, because the window can be made wider.
/// </param>
public sealed class Pictures(
    Func<string, CancellationToken, Task<byte[]?>> fetch,
    Func<CellSize?> cell,
    Action arrived,
    Action<string>? dropped = null,
    Func<int>? columns = null) : IPictures, IDisposable
{
    /// <summary>
    ///     How many bytes of decoded pixels are held, at four bytes a pixel — about two dozen photographs at the size a
    ///     wide window draws them, and every avatar on several screens besides (ADR-0025). Past this only for what is on
    ///     screen, which is never let go of however much that is; the screen is small, so how far past is bounded.
    /// </summary>
    /// <remarks>
    ///     A starting point rather than a measurement. A window large enough to raise the size each picture is decoded
    ///     to raises what each costs, and this is the first number to revisit if that becomes common.
    /// </remarks>
    public const long DecodedBudget = 64L * 1024 * 1024;

    /// <summary>
    ///     How many bytes of downloaded files are held, so that a picture whose pixels were let go of is decoded again
    ///     rather than fetched again. A preview is tens of kilobytes, so this is several hundred of them: a morning's
    ///     scroll back, or two destinations' worth (ADR-0025). Also a starting point.
    /// </summary>
    public const long EncodedBudget = 32L * 1024 * 1024;

    /// <summary>
    ///     What remembering a picture costs the encoded tier besides its file: its name, its address, its size and its
    ///     place in the order. Counted so that a picture whose file is gone, or which could not be had at all, is held
    ///     to the budget as well — remembered, so that it is not asked for again on every frame, but not for ever.
    /// </summary>
    public const int Remembering = 1024;

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
    private readonly Dictionary<string, Held> _held = [];

    /// <summary>What is remembered, wanted longest ago first — the order both tiers let go of it in.</summary>
    private readonly LinkedList<Held> _wanted = new();

    /// <summary>What the latest frame had on screen, which is never let go of from the decoded tier.</summary>
    private HashSet<string> _onScreen = [];

    private readonly CancellationTokenSource _abandoned = new();

    /// <summary>How many frames have said what they want, which is what names the latest of them.</summary>
    private long _frame;

    /// <summary>How many bytes of pixels are held: what <see cref="DecodedBudget" /> bounds.</summary>
    private long _decoded;

    /// <summary>How many bytes of files, and of remembering, are held: what <see cref="EncodedBudget" /> bounds.</summary>
    private long _encoded;

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
    /// <param name="columns">How wide the window is — see the constructor.</param>
    public static Pictures Over(
        HttpClient http,
        Func<CellSize?> cell,
        Action arrived,
        Action<string>? dropped = null,
        Func<int>? columns = null) => new(
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
        dropped,
        columns);

    /// <inheritdoc />
    public Picture? Of(Drawn drawn)
    {
        lock (_gate)
        {
            return _held.GetValueOrDefault(drawn.Id)?.Picture;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    ///     Only as much of <paramref name="frame" /> is taken into each tier as there is room for, nearest first, and
    ///     everything on screen whatever room that leaves. Taking the rest would fetch or decode pictures only to let go
    ///     of them again at once, and fetch or decode them again on the next frame, which is a fetch or a decode per
    ///     keypress. Past the decoded tier's room a picture is still fetched while the encoded tier has room for its
    ///     file, so that by the time it is scrolled to it is a decode away rather than a fetch.
    /// </remarks>
    public void Want(IReadOnlyList<WantedPicture> frame)
    {
        var sending = new List<Drawn>();
        List<string> letGo;

        lock (_gate)
        {
            var taking = Distinct(frame);

            _frame++;

            _onScreen = [.. taking.Where(wanted => wanted.OnScreen).Select(wanted => wanted.Drawn.Id)];

            // Renewed farthest first, so that the nearest ends up the most recently wanted and is the last of this
            // frame's to be let go of.
            for (var at = taking.Count - 1; at >= 0; at--)
            {
                var held = Renewed(taking[at].Drawn);

                // Worked out here, on the thread that lays out rows, because that is the only thread the window's size
                // may be asked from; whatever decodes the picture later reads it from here.
                held.Room = Room(held.Drawn);
            }

            var pixelsLeft = DecodedBudget;
            var bytesLeft = EncodedBudget;

            foreach (var wanted in taking)
            {
                var held = _held[wanted.Drawn.Id];

                // A size not known yet is a picture never fetched, taken while there is any room at all: what it costs
                // is only learnt by fetching it, and once it is known the next frame goes by it. Going by a guess
                // instead would either fetch nothing near a screen of photographs or everything near one of avatars.
                var keepsFile = wanted.OnScreen || (held.Length is { } length ? length <= bytesLeft : bytesLeft > 0);
                var decodes = wanted.OnScreen || (held.Pixels is { } pixels ? pixels <= pixelsLeft : pixelsLeft > 0);

                bytesLeft -= keepsFile ? held.Length ?? 0 : 0;
                pixelsLeft -= decodes ? held.Pixels ?? 0 : 0;
                held.DecodesIn = decodes ? _frame : held.DecodesIn;

                if (held.Coming || held.Failed)
                {
                    continue;
                }

                var fetches = held.Bytes is null && held.Picture is null && (keepsFile || decodes);
                var decodesAgain = decodes && held.Bytes is not null && (held.Picture is null || held.Grown);

                if (fetches || decodesAgain)
                {
                    held.Coming = true;
                    sending.Add(held.Drawn);
                }
            }

            letGo = LetGo();
        }

        Announce(letGo);

        foreach (var drawn in sending)
        {
            _ = Fetch(drawn);
        }
    }

    /// <summary>
    ///     Each picture in <paramref name="frame" /> once, nearest first, where a frame wants the same one twice — the
    ///     same author's avatar on two posts — and on screen if either was.
    /// </summary>
    private static List<WantedPicture> Distinct(IReadOnlyList<WantedPicture> frame)
    {
        var taking = new List<WantedPicture>();
        var where = new Dictionary<string, int>();

        foreach (var wanted in frame)
        {
            if (where.TryGetValue(wanted.Drawn.Id, out var at))
            {
                taking[at] = taking[at] with { OnScreen = taking[at].OnScreen || wanted.OnScreen };
            }
            else
            {
                where[wanted.Drawn.Id] = taking.Count;
                taking.Add(wanted);
            }
        }

        return taking;
    }

    /// <summary>
    ///     What is remembered of <paramref name="drawn" />, made the most recently wanted — or a new memory of it, where
    ///     there was none. Under the lock.
    /// </summary>
    private Held Renewed(Drawn drawn)
    {
        if (_held.TryGetValue(drawn.Id, out var held))
        {
            _wanted.Remove(held.Place);
            _wanted.AddLast(held.Place);

            return held;
        }

        // Written down before anything is sent for: that is what makes asking on every frame cost one fetch rather
        // than one a frame.
        return Remembered(drawn, _wanted.AddLast);
    }

    /// <summary>
    ///     A new memory of <paramref name="drawn" />, put in the order by <paramref name="place" /> and costing its
    ///     remembering from now on. Under the lock.
    /// </summary>
    private Held Remembered(Drawn drawn, Func<Held, LinkedListNode<Held>> place)
    {
        var held = new Held(drawn);

        held.Place = place(held);
        _held[drawn.Id] = held;
        _encoded += Remembering;

        return held;
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

    /// <summary>
    ///     Gets <paramref name="drawn" />'s pixels: from its file where that is already held, from the network where
    ///     not, and decoded only where the latest frame had room for them. A file fetched for a picture the decoded tier
    ///     had no room for is held as a file, and decoded when the picture is scrolled to.
    /// </summary>
    private async Task Fetch(Drawn drawn)
    {
        byte[]? bytes = null;
        Size? stored = null;
        Picture? picture = null;

        try
        {
            // A few at a time. Decoding holds the whole of a picture in memory before it is scaled down, so a screenful
            // arriving at once is a screenful of originals held at once — which is how this ran a machine out of memory
            // rather than merely making it wait (ADR-0016).
            await _atATime.WaitAsync(_abandoned.Token);

            try
            {
                lock (_gate)
                {
                    bytes = _held.GetValueOrDefault(drawn.Id)?.Bytes;
                }

                bytes ??= await fetch(drawn.Address, _abandoned.Token);

                if (bytes is not null)
                {
                    // Off the thread that asked, which is the UI thread itself where the file was already held: a
                    // decode is the slow part, and a frame waiting on one would be a frame that stuttered.
                    var file = bytes;

                    (stored, picture) = await Task.Run(() => Decoded(drawn, file), _abandoned.Token);
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

        // A shell that has been closed is not told about a picture: there is nothing left to draw it on.
        if (_abandoned.IsCancellationRequested)
        {
            return;
        }

        List<string> letGo;
        bool landed;

        lock (_gate)
        {
            landed = Land(drawn, stored is null ? null : bytes, stored, picture);
            letGo = LetGo();
        }

        Announce(letGo);

        if (landed)
        {
            arrived();
        }
    }

    /// <summary>
    ///     How big the picture in <paramref name="bytes" /> is stored, and its pixels at the size the latest frame said
    ///     to decode it to where that frame had room for them — or nothing at all where the bytes are not a picture.
    ///     Outside the lock, because this is the slow part.
    /// </summary>
    private (Size? Stored, Picture? Picture) Decoded(Drawn drawn, byte[] bytes)
    {
        if (PictureDecoder.Measured(bytes) is not { } stored)
        {
            return (null, null);
        }

        Size room;

        lock (_gate)
        {
            if (_held.GetValueOrDefault(drawn.Id) is not { } held || held.DecodesIn != _frame)
            {
                return (stored, null);
            }

            room = held.Room;
        }

        return (stored, PictureDecoder.From(bytes, room));
    }

    /// <summary>
    ///     Writes down what came of sending for <paramref name="drawn" />, and says whether there are new pixels to
    ///     draw. Under the lock.
    /// </summary>
    private bool Land(Drawn drawn, byte[]? bytes, Size? stored, Picture? picture)
    {
        // Never forgotten while it was on its way (LetGo), so there is somewhere for it to land. Where the page moved on
        // while it was coming, it lands as a file alone, because Decoded asked the latest frame whether to decode it:
        // the file of a picture scrolled past, kept so that scrolling back to it is a decode rather than a fetch
        // (ADR-0025). Only a shell closing forgets it, and that is not told about anything.
        if (!_held.TryGetValue(drawn.Id, out var held))
        {
            return false;
        }

        held.Coming = false;

        if (bytes is null || stored is null)
        {
            // Remembered as nothing, so that it is not asked for again for as long as it is remembered.
            held.Failed = true;

            return false;
        }

        held.Stored = stored;
        Keep(held, bytes);

        if (picture is null)
        {
            return false;
        }

        Show(held, picture);

        return true;
    }

    /// <summary>
    ///     Lets go of what was wanted longest ago until each tier is back within its budget, and says whose pixels were
    ///     let go of — to be told outside the lock, since whoever is told may take one of its own. Pixels on screen in
    ///     the latest frame are passed over however far over budget that leaves the decoded tier. A picture with
    ///     neither pixels nor file left is forgotten where the encoded tier still needs the room remembering it takes,
    ///     and is sent for again if it is ever wanted again.
    /// </summary>
    private List<string> LetGo()
    {
        var letGo = new List<string>();

        for (var place = _wanted.First; place is not null && _decoded > DecodedBudget; place = place.Next)
        {
            if (place.Value.Picture is not null && !_onScreen.Contains(place.Value.Drawn.Id))
            {
                Show(place.Value, null);
                letGo.Add(place.Value.Drawn.Id);
            }
        }

        for (var place = _wanted.First; place is not null && _encoded > EncodedBudget;)
        {
            var held = place.Value;

            place = place.Next;
            Keep(held, null);

            if (_encoded > EncodedBudget && held.Picture is null && !held.Coming)
            {
                _wanted.Remove(held.Place);
                _held.Remove(held.Drawn.Id);
                _encoded -= Remembering;
            }
        }

        return letGo;
    }

    /// <summary>Holds <paramref name="bytes" /> as <paramref name="held" />'s file, or none, keeping the tally.</summary>
    private void Keep(Held held, byte[]? bytes)
    {
        _encoded += (bytes?.LongLength ?? 0) - (held.Bytes?.LongLength ?? 0);
        held.Bytes = bytes;

        if (bytes is not null)
        {
            held.Length = bytes.Length;
        }
    }

    /// <summary>Holds <paramref name="picture" /> as <paramref name="held" />'s pixels, or none, keeping the tally.</summary>
    private void Show(Held held, Picture? picture)
    {
        _decoded += Cost(picture) - Cost(held.Picture);
        held.Picture = picture;
    }

    /// <summary>What <paramref name="picture" />'s pixels cost: four bytes each.</summary>
    private static long Cost(Picture? picture) => picture is null ? 0 : 4L * picture.Width * picture.Height;

    /// <summary>
    ///     The largest box <paramref name="drawn" /> could be drawn in on this window, in pixels, which is as large as it
    ///     is ever worth decoding: the box it is given where it has one of its own (<see cref="Drawn.Largest" />), and
    ///     otherwise the window's full width by <see cref="Rendering.Inset.WholeRows" /> rows. Any larger and the pixels
    ///     are held only to be thrown away by the scale down to the box, at four bytes each.
    /// </summary>
    private Size Room(Drawn drawn)
    {
        if (cell() is not { Width: > 0, Height: > 0 } size)
        {
            return PictureDecoder.SomeRoom;
        }

        if (drawn.Largest is var (most, tall))
        {
            return new Size(Math.Max(1, most) * size.Width, Math.Max(1, tall) * size.Height);
        }

        var across = columns?.Invoke() is > 0 and var wide ? wide * size.Width : PictureDecoder.LongestSide;

        return new Size(across, Rendering.Inset.WholeRows * size.Height);
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

    /// <summary>
    ///     Everything remembered of one picture: its file and its pixels where either is held, what is known of its
    ///     size, and whether it is on its way. Read and written under the lock only.
    /// </summary>
    private sealed class Held(Drawn drawn)
    {
        public Drawn Drawn { get; } = drawn;

        /// <summary>Where this is in the order pictures were wanted in.</summary>
        public LinkedListNode<Held> Place { get; set; } = null!;

        /// <summary>The downloaded file, while the encoded tier holds it.</summary>
        public byte[]? Bytes { get; set; }

        /// <summary>The decoded pixels, while the decoded tier holds them: what <see cref="Of" /> answers.</summary>
        public Picture? Picture { get; set; }

        /// <summary>How long the file is, kept after the file is let go of, so that it is not fetched to find out.</summary>
        public int? Length { get; set; }

        /// <summary>How big the picture is stored, from its file's header.</summary>
        public Size? Stored { get; set; }

        /// <summary>The largest box the latest frame could draw it in, in pixels: what it is decoded to.</summary>
        public Size Room { get; set; } = PictureDecoder.SomeRoom;

        /// <summary>
        ///     The latest frame that had room in the decoded tier for this picture's pixels. Kept as a frame rather than
        ///     a yes or no, so that a frame not wanting it at all says no without every picture remembered being
        ///     visited to say so.
        /// </summary>
        public long DecodesIn { get; set; } = -1;

        /// <summary>Whether it is being fetched or decoded now, so that a frame does not send for it twice.</summary>
        public bool Coming { get; set; }

        /// <summary>Whether it could not be had, so is not asked for again for as long as it is remembered.</summary>
        public bool Failed { get; set; }

        /// <summary>
        ///     What its pixels cost decoded for <see cref="Room" />, or <see langword="null" /> while its size is not
        ///     known — or what the pixels held cost, where that is more: a window made smaller does not decode a picture
        ///     again, so the larger pixels are what stay held.
        /// </summary>
        public long? Pixels =>
            Stored is { } stored
                ? Math.Max(Cost(Picture), 4L * PictureDecoder.Fitted(stored, Room).Width * PictureDecoder.Fitted(stored, Room).Height)
                : null;

        /// <summary>
        ///     Whether the window has grown past what the pixels held were decoded for, so that decoding the file again
        ///     would draw the picture sharper. Never where the window has shrunk: the pixels held are drawn scaled down.
        /// </summary>
        public bool Grown =>
            Picture is { } picture && Stored is { } stored
            && PictureDecoder.Fitted(stored, Room) is var fitted
            && (fitted.Width > picture.Width || fitted.Height > picture.Height);
    }
}
