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
///     decode too, rather than a picture drawn soft or fetched again.
/// </remarks>
/// <param name="fetch">
///     How the bytes at an address are got, reading no more than the count of them it is handed — or
///     <see cref="Fetched.None" /> where they cannot be had, and <see cref="Fetched.TooLarge" /> where the file is past
///     that count, which <see cref="WhyNot" /> tells apart. A delegate rather than an <see cref="HttpClient" /> so
///     that a test can answer without a socket, and so the one thing this class is about — asked once, held, and
///     announced when it lands — is testable on its own. The count is the limit this class keeps for the picture sent
///     for (<see cref="MostBytes" />, <see cref="MostBytesHeldApart" />).
/// </param>
/// <param name="arrived">
///     What to do when a picture lands: redraw, so the rows that have been waiting for it fill in. Called off the
///     thread the fetch finished on, so whatever is passed here is what has to get back to the UI thread.
/// </param>
public sealed class Pictures(
    Func<string, int, CancellationToken, Task<Fetched>> fetch,
    Action arrived) : IPictures, IDisposable
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
    public const int RememberingCost = 1024;

    /// <summary>
    ///     How many bytes of a download are worth reading for a picture drawn inline. A preview is tens of kilobytes;
    ///     anything of this size is either not a preview or not worth the memory of finding out which.
    /// </summary>
    public const int MostBytes = 8 * 1024 * 1024;

    /// <summary>
    ///     How many bytes of a download are worth reading for a picture held apart (<see cref="Drawn.HeldApart" />):
    ///     an attachment's original, which a photograph off a phone or a screenshot of a page can be several megabytes
    ///     of, and which is held only while the Picture screen shows it (ADR-0034).
    /// </summary>
    public const int MostBytesHeldApart = 32 * 1024 * 1024;

    /// <summary>
    ///     How many pictures are fetched and decoded at once. Small on purpose: see <see cref="Fetch" />. The rest wait
    ///     their turn nearest first (<see cref="Pump" />).
    /// </summary>
    public const int AtATime = 4;

    /// <summary>
    ///     How long a preview is waited for. Short, because nothing is waiting on it: the post is already on screen
    ///     saying what is attached to it, and a picture that has not arrived by now is one the reader has read past.
    /// </summary>
    /// <remarks>
    ///     The file server's client's own timeout, which bounds the wait for it to answer and not the read of what it
    ///     sends: <see cref="Over" /> asks for the headers alone, and <see cref="HttpClient.Timeout" /> is over once
    ///     they are here. So an original of tens of megabytes on a slow line is read for as long as it takes, while a
    ///     server that never answers is given up on as soon as a preview's would be (ADR-0034).
    /// </remarks>
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private readonly Lock _gate = new();
    private readonly Dictionary<string, Held> _held = [];

    /// <summary>What is remembered, wanted longest ago first — the order both tiers let go of it in.</summary>
    private readonly LinkedList<Held> _wanted = new();

    /// <summary>What the latest frame had on screen, which is never let go of from the decoded tier.</summary>
    private HashSet<string> _onScreen = [];

    /// <summary>
    ///     What has been sent for and not yet started, nearest the screen first as of the latest frame. Put back in
    ///     order on every frame, because the order pictures were first wanted in says nothing about which the page is
    ///     near now (#352).
    /// </summary>
    private List<Drawn> _queued = [];

    /// <summary>
    ///     Whose pixels have been let go of since the last <see cref="Drain" />, by <see cref="Drawn.Id" />, each once,
    ///     in the order they went — to make room, or replaced by pixels decoded again for a wider window. Only the
    ///     decoded tier's: a picture whose file alone is let go of has nothing on a terminal that this client is not
    ///     still drawing.
    /// </summary>
    private readonly List<string> _letGo = [];

    /// <summary>How many fetches and decodes are under way, never more than <see cref="AtATime" />.</summary>
    private int _fetching;

    private readonly CancellationTokenSource _abandoned = new();

    /// <summary>How many frames have said what they want, which is what names the latest of them.</summary>
    private long _frame;

    /// <summary>How many bytes of pixels are held: what <see cref="DecodedBudget" /> bounds.</summary>
    private long _decoded;

    /// <summary>
    ///     How many bytes of files, and of remembering, are held: what <see cref="EncodedBudget" /> bounds.
    /// </summary>
    private long _encoded;

    /// <summary>Stops anything still being fetched, for a shell that is closing.</summary>
    public void Dispose()
    {
        _abandoned.Cancel();
        _abandoned.Dispose();
    }

    /// <summary>
    ///     Everything the shell needs to fetch and hold pictures, wired to <paramref name="http" /> — and to the disk,
    ///     for a <c>file:</c> address: a file being attached, which is on no instance yet (#382).
    /// </summary>
    /// <param name="arrived">What to do when one lands — see the constructor.</param>
    public static Pictures Over(HttpClient http, Action arrived) => new(
        async (address, most, cancellation) =>
        {
            if (Uri.TryCreate(address, UriKind.Absolute, out var uri) && uri.IsFile)
            {
                return await ReadFile(uri.LocalPath, most, cancellation).ConfigureAwait(false);
            }

            // Headers first, so that a length worth refusing is refused before the body is read rather than after it
            // has already been held in memory.
            using var response = await http
                .GetAsync(address, HttpCompletionOption.ResponseHeadersRead, cancellation)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return Fetched.None;
            }

            if (response.Content.Headers.ContentLength > most)
            {
                return Fetched.TooLarge;
            }

            await using var body = await response.Content.ReadAsStreamAsync(cancellation).ConfigureAwait(false);

            return await Read(body, most, cancellation).ConfigureAwait(false);
        },
        arrived);

    /// <inheritdoc />
    public Picture? Of(Drawn drawn)
    {
        lock (_gate)
        {
            return _held.GetValueOrDefault(drawn.Id)?.Picture;
        }
    }

    /// <inheritdoc />
    public Unshowable? WhyNot(Drawn drawn)
    {
        lock (_gate)
        {
            return _held.GetValueOrDefault(drawn.Id)?.WhyNot;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Drain()
    {
        lock (_gate)
        {
            string[] letGo = [.. _letGo];

            _letGo.Clear();

            return letGo;
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
    public void Want(IReadOnlyList<WantedPicture> frame, Raster raster, int columns)
    {
        lock (_gate)
        {
            // What is already waiting its turn, and what this frame sends for that was not: the two that make up the
            // queue once this frame has put it in order.
            var sending = _queued.Select(drawn => drawn.Id).ToHashSet();
            var taking = Distinct(frame);

            _frame++;

            _onScreen = [.. taking.Where(wanted => wanted.OnScreen).Select(wanted => wanted.Drawn.Id)];

            // Renewed farthest first, so that the nearest ends up the most recently wanted and is the last of this
            // frame's to be let go of.
            for (var at = taking.Count - 1; at >= 0; at--)
            {
                var held = Renewed(taking[at].Drawn);

                held.Room = Room(held.Drawn, raster.Cell, columns);
            }

            var pixelsLeft = DecodedBudget;
            var bytesLeft = EncodedBudget;

            foreach (var wanted in taking)
            {
                var held = _held[wanted.Drawn.Id];
                var (keepsFile, decodes) = Takes(wanted, held, ref bytesLeft, ref pixelsLeft);

                held.DecodesIn = decodes ? _frame : held.DecodesIn;

                if (held.Coming || held.WhyNot is not null)
                {
                    continue;
                }

                var fetches = held.Bytes is null && held.Picture is null && (keepsFile || decodes);
                var decodesAgain = decodes && held.Bytes is not null && (held.Picture is null || held.Grown);

                if (fetches || decodesAgain)
                {
                    held.Coming = true;
                    sending.Add(held.Drawn.Id);
                }
            }

            LetGoOfWhatIsHeldApart(taking);

            Abandon(taking);

            LetGo();

            // Nearest first as this frame has it, and none that this very frame abandoned or let go of.
            _queued =
            [
                .. taking.Select(wanted => wanted.Drawn)
                         .Where(drawn => sending.Contains(drawn.Id)
                                         && _held.GetValueOrDefault(drawn.Id) is { Coming: true }),
            ];
        }

        Pump();
    }

    /// <summary>
    ///     Whether this frame has room to keep <paramref name="held" />'s file and to decode its pixels, taking what
    ///     they cost out of what room is left. Under the lock.
    /// </summary>
    /// <remarks>
    ///     A size not known yet is a picture never fetched, taken while there is any room at all: what it costs is only
    ///     learnt by fetching it, and once it is known the next frame goes by it. Going by a guess instead would either
    ///     fetch nothing near a screen of photographs or everything near one of avatars. One on screen is taken
    ///     whatever room is left, and one held apart whatever room there is, taking none (<see cref="Counts" />).
    /// </remarks>
    private static (bool KeepsFile, bool Decodes) Takes(
        WantedPicture wanted,
        Held held,
        ref long bytesLeft,
        ref long pixelsLeft)
    {
        if (!Counts(held))
        {
            return (true, true);
        }

        var keepsFile = wanted.OnScreen || (held.Length is { } length ? length <= bytesLeft : bytesLeft > 0);
        var decodes = wanted.OnScreen || (held.Pixels is { } pixels ? pixels <= pixelsLeft : pixelsLeft > 0);

        bytesLeft -= keepsFile ? held.Length ?? 0 : 0;
        pixelsLeft -= decodes ? held.Pixels ?? 0 : 0;

        return (keepsFile, decodes);
    }

    /// <summary>
    ///     Forgets every picture held apart that <paramref name="taking" /> does not want — pixels, file and all, and
    ///     whether or not it is on its way — writing down whose pixels went for the next <see cref="Drain" />: the
    ///     Picture screen has gone, or moved on from it, and it is held only while it is looked at (ADR-0034). One on
    ///     its way lands nowhere, and is sent for again if it is wanted again. Under the lock.
    /// </summary>
    private void LetGoOfWhatIsHeldApart(List<WantedPicture> taking)
    {
        var wanting = taking.Select(wanted => wanted.Drawn.Id).ToHashSet();

        foreach (var held in _held.Values.Where(held => !Counts(held) && !wanting.Contains(held.Drawn.Id)).ToList())
        {
            if (held.Picture is not null)
            {
                RecordLetGo(held.Drawn.Id);
            }

            _wanted.Remove(held.Place);
            _held.Remove(held.Drawn.Id);
        }
    }

    /// <summary>
    ///     Takes back every picture still waiting its turn that <paramref name="taking" /> no longer wants: the page has
    ///     left it behind, and fetching it would hold up what the page is near now. One with nothing held yet is
    ///     forgotten as though it had never been asked for, rather than remembered as nothing, so that a frame that
    ///     wants it again sends for it again; one waiting only to be decoded from its file keeps the file. One already
    ///     under way is not touched — its bytes are on their way, and it lands as a file (ADR-0025).
    /// </summary>
    private void Abandon(List<WantedPicture> taking)
    {
        var wanting = taking.Select(wanted => wanted.Drawn.Id).ToHashSet();

        foreach (var drawn in _queued.Where(drawn => !wanting.Contains(drawn.Id)))
        {
            if (_held.GetValueOrDefault(drawn.Id) is not { } held)
            {
                continue;
            }

            held.Coming = false;

            if (held.Bytes is null && held.Picture is null)
            {
                _wanted.Remove(held.Place);
                _held.Remove(drawn.Id);
                Tally(held, bytes: -RememberingCost);
            }
        }
    }

    /// <summary>
    ///     Starts the nearest of what is waiting, for as many as there is room for under <see cref="AtATime" />. Called
    ///     when a frame has said what it wants and whenever a fetch finishes, which are the only two things that change
    ///     what can start.
    /// </summary>
    private void Pump()
    {
        while (true)
        {
            Drawn next;

            lock (_gate)
            {
                if (_fetching >= AtATime || _queued.Count == 0 || _abandoned.IsCancellationRequested)
                {
                    return;
                }

                next = _queued[0];
                _queued.RemoveAt(0);
                _fetching++;
            }

            _ = Fetch(next);
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
    /// <remarks>
    ///     Remembered as this frame says it rather than as it was first said, so that a box that grows with the window —
    ///     the Picture screen's original, whose <see cref="Drawn.Largest" /> is the window — is decoded to the window
    ///     there is now.
    /// </remarks>
    private Held Renewed(Drawn drawn)
    {
        if (_held.TryGetValue(drawn.Id, out var held))
        {
            _wanted.Remove(held.Place);
            _wanted.AddLast(held.Place);
            held.Drawn = drawn;

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
        Tally(held, bytes: RememberingCost);

        return held;
    }

    /// <summary>
    ///     Whether <paramref name="held" /> counts against the two budgets, which is every picture but one held apart
    ///     (<see cref="Drawn.HeldApart" />): that one is taken whatever room there is (<see cref="Takes" />), costs
    ///     neither tally anything (<see cref="Tally" />), is let go of by no frame wanting it
    ///     (<see cref="LetGoOfWhatIsHeldApart" />) and never to make room (<see cref="LetGo" />), and so never makes
    ///     the tiers let go of anything else either (ADR-0034). The one question all four ask.
    /// </summary>
    private static bool Counts(Held held) => !held.Drawn.HeldApart;

    /// <summary>
    ///     Moves the two tallies by what <paramref name="held" /> has just come to cost or stopped costing: the one
    ///     place either moves, and so the one place a picture held apart is kept out of them (<see cref="Counts" />).
    ///     Under the lock.
    /// </summary>
    /// <param name="held">The picture whose cost changed.</param>
    /// <param name="bytes">How many more bytes of file and remembering it costs, or fewer where negative.</param>
    /// <param name="pixels">How many more bytes of pixels it costs, or fewer where negative.</param>
    private void Tally(Held held, long bytes = 0, long pixels = 0)
    {
        if (!Counts(held))
        {
            return;
        }

        _encoded += bytes;
        _decoded += pixels;
    }

    /// <summary>
    ///     What the file at <paramref name="path" /> holds, or <see langword="null" /> where it is gone or cannot be
    ///     read — refused as too large where it holds more than <paramref name="most" />, the cap a file server is held
    ///     to, since a photograph straight off a camera decodes to as much memory wherever it came from.
    /// </summary>
    private static async Task<Fetched> ReadFile(string path, int most, CancellationToken cancellation)
    {
        try
        {
            await using var file = File.OpenRead(path);

            return file.Length > most ? Fetched.TooLarge : await Read(file, most, cancellation).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return Fetched.None;
        }
        catch (UnauthorizedAccessException)
        {
            return Fetched.None;
        }
    }

    /// <summary>
    ///     What <paramref name="body" /> holds, refused as too large where it holds more than <paramref name="most" />.
    ///     Read in pieces and counted as it goes rather than taken whole, because a server that declares no length —
    ///     or declares one and sends another — would otherwise decide how much of this client's memory to use.
    /// </summary>
    private static async Task<Fetched> Read(Stream body, int most, CancellationToken cancellation)
    {
        using var bytes = new MemoryStream();
        var piece = new byte[64 * 1024];

        while (bytes.Length <= most)
        {
            // Carried on wherever the piece was read rather than back on the thread that asked, which is the UI thread:
            // it gets to a waiting piece only between frames, so a 4 MB photograph read in 64 KB pieces took two to
            // three seconds to preview (#382).
            var read = await body.ReadAsync(piece, cancellation).ConfigureAwait(false);

            if (read == 0)
            {
                return Fetched.Of(bytes.ToArray());
            }

            bytes.Write(piece, 0, read);
        }

        return Fetched.TooLarge;
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
        var tooLarge = false;

        try
        {
            // A few at a time, which Pump has already seen to before starting this one. Decoding holds the whole of a
            // picture in memory before it is scaled down, so a screenful arriving at once is a screenful of originals
            // held at once — which is how this ran a machine out of memory rather than merely making it wait
            // (ADR-0016).
            try
            {
                lock (_gate)
                {
                    bytes = _held.GetValueOrDefault(drawn.Id)?.Bytes;
                }

                if (bytes is null)
                {
                    var fetched = await fetch(
                        drawn.Address,
                        drawn.HeldApart ? MostBytesHeldApart : MostBytes,
                        _abandoned.Token);

                    bytes = fetched.Bytes;
                    tooLarge = fetched.IsTooLarge;
                }

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
                lock (_gate)
                {
                    _fetching--;
                }

                Pump();
            }
        }
        catch (Exception failure) when (failure is not OutOfMemoryException)
        {
            // A picture nobody can fetch is not something to tell a reader about in a feed: the post already says what
            // is attached to it, and an error row where a photograph was meant to be would be worse than the
            // description. Only the Picture screen says so, of its original (WhyNot).
        }

        // A shell that has been closed is not told about a picture: there is nothing left to draw it on.
        if (_abandoned.IsCancellationRequested)
        {
            return;
        }

        bool landed;
        bool refused;

        lock (_gate)
        {
            landed = Land(drawn, stored is null ? null : bytes, stored, picture, tooLarge);
            refused = drawn.HeldApart && _held.GetValueOrDefault(drawn.Id)?.WhyNot is not null;
            LetGo();
        }

        // A picture held apart that will not come is announced as one landing is: the Picture screen waiting on it
        // says why on the status row, and only a frame drawn again asks it.
        if (landed || refused)
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
    ///     draw — letting go of the pixels it already held where those are replaced. Under the lock.
    /// </summary>
    /// <param name="drawn">The picture sent for.</param>
    /// <param name="bytes">Its file, or <see langword="null" /> where none came that was a picture.</param>
    /// <param name="stored">How big it is stored, where it was a picture.</param>
    /// <param name="picture">Its pixels, where the latest frame had room for them.</param>
    /// <param name="tooLarge">Whether no file came because the one there was refused as too large.</param>
    private bool Land(Drawn drawn, byte[]? bytes, Size? stored, Picture? picture, bool tooLarge)
    {
        // Never forgotten while it was on its way (LetGo), so there is somewhere for it to land. Where the page moved on
        // while it was coming, it lands as a file alone, because Decoded asked the latest frame whether to decode it:
        // the file of a picture scrolled past, kept so that scrolling back to it is a decode rather than a fetch
        // (ADR-0025). Only a shell closing forgets it, and that is not told about anything — and a frame that no longer
        // wants a picture held apart, which is let go whatever it is doing (LetGoOfWhatIsHeldApart).
        if (!_held.TryGetValue(drawn.Id, out var held))
        {
            return false;
        }

        held.Coming = false;

        if (bytes is null || stored is null)
        {
            // Remembered as nothing, so that it is not asked for again for as long as it is remembered.
            held.WhyNot = tooLarge ? Unshowable.TooLarge : Unshowable.Failed;

            return false;
        }

        held.Stored = stored;
        HoldFile(held, bytes);

        if (picture is null)
        {
            return false;
        }

        // Decoded again for a wider window: the pixels it replaces are let go of as surely as any made room for, and a
        // Kitty terminal holding a copy sent at the old size is told so once this is drained (ADR-0022) — that copy's
        // id is keyed on the size it was decoded at, so nothing would ever ask for it, or delete it, again.
        if (held.Picture is not null)
        {
            RecordLetGo(drawn.Id);
        }

        HoldPixels(held, picture);

        return true;
    }

    /// <summary>
    ///     Lets go of what was wanted longest ago until each tier is back within its budget, writing down whose pixels
    ///     were let go of for the next <see cref="Drain" />. Under the lock. Pixels on screen in the latest frame are
    ///     passed over however far over budget that leaves the decoded tier. A picture with neither pixels nor file left
    ///     is forgotten where the encoded tier still needs the room remembering it takes, and is sent for again if it is
    ///     ever wanted again.
    /// </summary>
    private void LetGo()
    {
        for (var place = _wanted.First; place is not null && _decoded > DecodedBudget; place = place.Next)
        {
            if (place.Value.Picture is not null && !_onScreen.Contains(place.Value.Drawn.Id) && Counts(place.Value))
            {
                LetGoOfPixels(place.Value);
                RecordLetGo(place.Value.Drawn.Id);
            }
        }

        for (var place = _wanted.First; place is not null && _encoded > EncodedBudget;)
        {
            var held = place.Value;

            place = place.Next;

            // Passed over while it is on its way: one waiting to be decoded again reads its file when its turn comes,
            // and letting go of the file before then turns that decode into a second fetch. One held apart is no part
            // of the tally, so letting go of it brings the tier no nearer its budget.
            if (held.Coming || !Counts(held))
            {
                continue;
            }

            LetGoOfFile(held);

            if (_encoded > EncodedBudget && held.Picture is null)
            {
                _wanted.Remove(held.Place);
                _held.Remove(held.Drawn.Id);
                Tally(held, bytes: -RememberingCost);
            }
        }
    }

    /// <summary>Holds <paramref name="bytes" /> as <paramref name="held" />'s file, keeping the tally.</summary>
    private void HoldFile(Held held, byte[] bytes)
    {
        Tally(held, bytes: bytes.LongLength - (held.Bytes?.LongLength ?? 0));
        held.Bytes = bytes;
        held.Length = bytes.Length;
    }

    /// <summary>
    ///     Lets go of <paramref name="held" />'s file, keeping the tally — and keeping how long it was, so that what
    ///     it costs is still known without fetching it again.
    /// </summary>
    private void LetGoOfFile(Held held)
    {
        Tally(held, bytes: -(held.Bytes?.LongLength ?? 0));
        held.Bytes = null;
    }

    /// <summary>Holds <paramref name="picture" /> as <paramref name="held" />'s pixels, keeping the tally.</summary>
    private void HoldPixels(Held held, Picture picture)
    {
        Tally(held, pixels: Cost(picture) - Cost(held.Picture));
        held.Picture = picture;
    }

    /// <summary>Lets go of <paramref name="held" />'s pixels, keeping the tally.</summary>
    private void LetGoOfPixels(Held held)
    {
        Tally(held, pixels: -Cost(held.Picture));
        held.Picture = null;
    }

    /// <summary>What <paramref name="picture" />'s pixels cost: four bytes each.</summary>
    private static long Cost(Picture? picture) => picture is null ? 0 : Cost(new Size(picture.Width, picture.Height));

    /// <summary>What pixels <paramref name="size" /> cost: four bytes each.</summary>
    private static long Cost(Size size) => 4L * size.Width * size.Height;

    /// <summary>
    ///     The largest box <paramref name="drawn" /> could be drawn in on this window, in pixels, which is as large as
    ///     it is ever worth decoding: the box it is given where it has one of its own (<see cref="Drawn.Largest" />),
    ///     and otherwise the content region's full width by <see cref="Rendering.Inset.WholeRows" /> rows. Any larger
    ///     and the pixels are held only to be thrown away by the scale down to the box, at four bytes each.
    /// </summary>
    /// <param name="drawn">The picture.</param>
    /// <param name="cell">How big a cell is in the latest frame's <see cref="Raster" />.</param>
    /// <param name="columns">How wide the content region is in the latest frame, or nought before it has drawn.</param>
    private static Size Room(Drawn drawn, CellSize? cell, int columns)
    {
        if (cell is not { Width: > 0, Height: > 0 } size)
        {
            return PictureDecoder.SomeRoom;
        }

        if (drawn.Largest is var (most, tall))
        {
            return new Size(Math.Max(1, most) * size.Width, Math.Max(1, tall) * size.Height);
        }

        var across = columns is > 0 and var wide ? wide * size.Width : PictureDecoder.LongestSide;

        return new Size(across, Rendering.Inset.WholeRows * size.Height);
    }

    /// <summary>
    ///     Writes down that <paramref name="drawnId" />'s pixels were let go of, for the next <see cref="Drain" /> —
    ///     once, however often that happens before it. Under the lock.
    /// </summary>
    private void RecordLetGo(string drawnId)
    {
        if (!_letGo.Contains(drawnId))
        {
            _letGo.Add(drawnId);
        }
    }

    /// <summary>
    ///     Everything remembered of one picture: its file and its pixels where either is held, what is known of its
    ///     size, and whether it is on its way. Read and written under the lock only.
    /// </summary>
    private sealed class Held(Drawn drawn)
    {
        /// <summary>The picture, as the latest frame to want it said it.</summary>
        public Drawn Drawn { get; set; } = drawn;

        /// <summary>Where this is in the order pictures were wanted in.</summary>
        public LinkedListNode<Held> Place { get; set; } = null!;

        /// <summary>The downloaded file, while the encoded tier holds it.</summary>
        public byte[]? Bytes { get; set; }

        /// <summary>The decoded pixels, while the decoded tier holds them: what <see cref="Of" /> answers.</summary>
        public Picture? Picture { get; set; }

        /// <summary>
        ///     How long the file is, kept after the file is let go of, so that it is not fetched to find out.
        /// </summary>
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

        /// <summary>
        ///     Why it could not be had, where it could not, so is not asked for again for as long as it is remembered.
        /// </summary>
        public Unshowable? WhyNot { get; set; }

        /// <summary>
        ///     What its pixels cost decoded for <see cref="Room" />, or <see langword="null" /> while its size is not
        ///     known — or what the pixels held cost, where that is more: a window made smaller does not decode a picture
        ///     again, so the larger pixels are what stay held.
        /// </summary>
        public long? Pixels =>
            Stored is { } stored
                ? Math.Max(Cost(Picture), Cost(PictureDecoder.Fitted(stored, Room)))
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
