using System.Diagnostics;
using System.Text;
using Terminal.Gui.Drivers;
using Wooly.Core;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;

namespace Wooly.Tests.Tui;

/// <summary>
///     What a wheel notch costs, headless — over sixel photographs, over the same through Kitty in a box, and over text
///     alone: the time a notch takes to draw and how much is sent to the terminal for it. A measurement rather than a
///     test — the numbers are what #292 asks to be compared before and after — so it is skipped unless asked for.
/// </summary>
/// <remarks>
///     Run from <c>tests/Wooly.Tests/bin/Release/net10.0</c>, after a Release build, with
///     <c>WOOLY_MEASURE=1 ./Wooly.Tests -class Wooly.Tests.Tui.ScrollMeasure -showLiveOutput</c> — or <c>-method</c> and
///     one measurement's full name for that one alone. <c>dotnet test</c> prints nothing a passing test writes.
///     The terminal is 120×50 and the photographs are busy ones at the size <c>PictureDecoder</c> holds them, as #287's
///     were measured — but for <see cref="AScrollAsPicturesLand" />, at 140×50 with its pictures fetched as it scrolls.
/// </remarks>
public class ScrollMeasure(ITestOutputHelper output)
{
    public static bool Enabled => Environment.GetEnvironmentVariable("WOOLY_MEASURE") == "1";

    /// <summary>
    ///     What the client's own output costs a frame with every cell waiting to be written, against Terminal.Gui's:
    ///     the rest of this class is headless and never reaches it (ADR-0031).
    /// </summary>
    [Fact(Skip = "A measurement, not a test. Set WOOLY_MEASURE=1 to run it.", SkipUnless = nameof(Enabled))]
    public void AFrameThroughTheOutput()
    {
        const string latin = "the quick brown fox jumps over the lazy dog and keeps on running through the field";
        const string cyrillic = "быстрая коричневая лиса прыгает через ленивую собаку и бежит дальше по полю";
        const string han = "敏捷的棕色狐狸跳过懒狗然后继续穿过田野一直跑到很远的地方";

        (string Name, Func<int, string> Row)[] pages =
        [
            ("Latin", _ => latin),
            ("Cyrillic", _ => cyrillic),
            ("Han", _ => han),
            ("a rail and a rule", _ => "│ Home     │" + new string('─', 100)),
            ("a feed", row => row % 5 == 0 ? latin + "\U0001F633" : row % 5 == 1 ? "↺0  ☆0  ↩ 0    ○ 2h" : latin),
            ("20 emoji on a row", _ => string.Concat(Enumerable.Repeat("\U0001F633 ", 20))),
            ("40 emoji side by side", _ => string.Concat(Enumerable.Repeat("\U0001F633", 40))),
            ("40 symbols side by side", _ => new string('♛', 40)),
            ("Braille, the whole row", _ => new string('⠿', 119)),
        ];

        foreach (var (name, row) in pages)
        {
            var theirs = new CountingAnsi();
            var ours = new CountingHeld();

            var (theirTime, theirBytes) = Frames(theirs.Write, () => theirs.Written, row);
            var (ourTime, ourBytes) = Frames(ours.Write, () => ours.Written, row);

            output.WriteLine(
                $"{name}: Terminal.Gui {theirTime:F0} µs, {theirBytes / 1024.0:F1} KB; "
                + $"ours {ourTime:F0} µs, {ourBytes / 1024.0:F1} KB");
        }
    }

    /// <summary>
    ///     The median time and the bytes of writing a page 120×50 with every cell dirty, after warming up.
    /// </summary>
    private static (double Microseconds, long Bytes) Frames(
        Action<IOutputBuffer> write,
        Func<long> written,
        Func<int, string> row)
    {
        var buffer = new OutputBufferImpl();
        buffer.SetSize(120, 50);

        var times = new List<double>();
        long bytes = 0;

        for (var frame = 0; frame < 800; frame++)
        {
            for (var at = 0; at < 50; at++)
            {
                buffer.Move(0, at);
                buffer.AddStr(row(at));
                buffer.Move(119, at);
                buffer.AddStr("│");
            }

            var before = written();
            var start = Stopwatch.GetTimestamp();

            write(buffer);

            if (frame >= 200)
            {
                times.Add(Stopwatch.GetElapsedTime(start).TotalMicroseconds);
            }

            bytes = written() - before;
        }

        times.Sort();

        return (times[times.Count / 2], bytes);
    }

    /// <summary>A cursor move's bytes, in the common case of a two-digit row and a two-digit column.</summary>
    private const int ACursorMove = 8;

    private sealed class CountingAnsi : AnsiOutput
    {
        public long Written { get; private set; }

        protected override void Write(StringBuilder output) => Written += Encoding.UTF8.GetByteCount(output.ToString());

        protected override bool SetCursorPositionImpl(int col, int row)
        {
            Written += ACursorMove;

            return true;
        }
    }

    private sealed class CountingHeld : HeldOutput
    {
        public long Written { get; private set; }

        protected override void Write(StringBuilder output) => Written += Encoding.UTF8.GetByteCount(output.ToString());

        protected override bool SetCursorPositionImpl(int col, int row)
        {
            Written += ACursorMove;

            return true;
        }
    }

    [Fact(Skip = "A measurement, not a test. Set WOOLY_MEASURE=1 to run it.", SkipUnless = nameof(Enabled))]
    public async Task AWheelNotchOverSixelPhotographs()
    {
        var pictures = new FakePictures();
        var posts = Enumerable.Range(1, 12)
            .Select(at =>
            {
                pictures.HoldingPhotograph($"m{at}", 853, 640);

                return APost.With(id: $"{at}0", media: [APost.APicture($"m{at}")]);
            })
            .ToArray();

        using var drawn = await DrawnShell.Of(
            120,
            50,
            Themes.Plain,
            new AShell { Timelines = FakeTimelineReader.Holding(posts) },
            pictures: pictures,
            drawsPictures: true);

        // Warmed up, so that the first encode of each photograph is not what is measured.
        for (var notch = 0; notch < 5; notch++)
        {
            drawn.Wheel(RailLines.Width + 4, 3);
        }

        // Down over rows the page has not been cut at yet, first paced as a trackpad sends them and then as fast as
        // they can be drawn; then back up over rows already drawn.
        Report("sixel, down, new rows, a notch every 16 ms", await Notches(drawn, down: true, apart: TimeSpan.FromMilliseconds(16)));
        Report("sixel, down, new rows, back to back", await Notches(drawn, down: true, apart: TimeSpan.Zero));
        Report("sixel, up, rows already drawn, back to back", await Notches(drawn, down: false, apart: TimeSpan.Zero));
        await Pages(drawn);
    }

    /// <summary>
    ///     The same, through Kitty in a box — Terminal.Gui's image view, which is what WezTerm would get were it drawn
    ///     through Kitty rather than sixel.
    /// </summary>
    [Fact(Skip = "A measurement, not a test. Set WOOLY_MEASURE=1 to run it.", SkipUnless = nameof(Enabled))]
    public async Task AWheelNotchOverKittyPhotographsInABox()
    {
        var pictures = new FakePictures();
        var posts = Enumerable.Range(1, 12)
            .Select(at =>
            {
                pictures.HoldingPhotograph($"m{at}", 853, 640);

                return APost.With(id: $"{at}0", media: [APost.APicture($"m{at}")]);
            })
            .ToArray();

        using var drawn = await DrawnShell.Of(
            120,
            50,
            Themes.Plain,
            new AShell { Timelines = FakeTimelineReader.Holding(posts) },
            pictures: pictures,
            answersKitty: true);

        for (var notch = 0; notch < 5; notch++)
        {
            drawn.Wheel(RailLines.Width + 4, 3);
        }

        Report("Kitty in a box, down, a notch every 16 ms", await Notches(drawn, down: true, apart: TimeSpan.FromMilliseconds(16)));
        Report("Kitty in a box, up, back to back", await Notches(drawn, down: false, apart: TimeSpan.Zero));
    }

    /// <summary>
    ///     A timeline full of custom emoji on sixel (#527, ADR-0035): forty posts each writing sixteen of forty emoji
    ///     between its words, busy 36-pixel pictures as an instance serves them, and every fourth post a photograph —
    ///     with how many boxes each frame draws, emoji and photographs apart, and how many emoji are left their shade.
    /// </summary>
    [Fact(Skip = "A measurement, not a test. Set WOOLY_MEASURE=1 to run it.", SkipUnless = nameof(Enabled))]
    public async Task AWheelNotchOverSixelCustomEmoji()
    {
        var pictures = new FakePictures();
        var customEmoji = Enumerable.Range(0, 40)
            .Select(at => new Wooly.Core.CustomEmoji($"e{at}", $"https://files.example/emoji/e{at}.png"))
            .ToArray();

        foreach (var each in customEmoji)
        {
            pictures.HoldingPhotograph(Wooly.Tui.Media.Drawn.CustomEmoji(each).Id, 36, 36);
        }

        var posts = Enumerable.Range(1, 40)
            .Select(at =>
            {
                var used = Enumerable.Range(0, 16).Select(nth => customEmoji[((at * 7) + nth) % customEmoji.Length]).ToArray();
                var content = $"Post {at}: " + string.Join(' ', used.Select(each => $"so {each.Written} it goes"));

                if (at % 4 != 0)
                {
                    return APost.With(id: $"{at}0", content: content, customEmoji: used);
                }

                pictures.HoldingPhotograph($"m{at}", 853, 640);

                return APost.With(id: $"{at}0", content: content, media: [APost.APicture($"m{at}")], customEmoji: used);
            })
            .ToArray();

        using var drawn = await DrawnShell.Of(
            120,
            50,
            Themes.Plain,
            new AShell { Timelines = FakeTimelineReader.Holding(posts) },
            pictures: pictures,
            drawsPictures: true);

        for (var notch = 0; notch < 5; notch++)
        {
            drawn.Wheel(RailLines.Width + 4, 3);
        }

        var boxes = new List<(int CustomEmoji, int Others, int Shaded)>();

        var paced = TimeSpan.FromMilliseconds(16);

        Report("sixel emoji, down, a notch every 16 ms", await Notches(drawn, true, paced, boxes: boxes));
        Report("sixel emoji, down, back to back", await Notches(drawn, true, TimeSpan.Zero, boxes: boxes));
        Report("sixel emoji, up, back to back", await Notches(drawn, false, TimeSpan.Zero, boxes: boxes));

        output.WriteLine(
            $"Boxes a frame: emoji {Spread(boxes.Select(each => each.CustomEmoji))}; "
            + $"photographs {Spread(boxes.Select(each => each.Others))}; "
            + $"emoji left shaded {Spread(boxes.Select(each => each.Shaded))}.");

        await Pages(drawn);
    }

    /// <summary>
    ///     A scroll into posts whose pictures land as it goes, as the client scrolls: fetched off the UI thread through
    ///     the real cache, landing in bursts through <see cref="PicturesLanding" />, and the loop run a pass at a time,
    ///     a notch a frame in flicks of twelve with a pause between, paced at 16 ms. Every post has an avatar and four of
    ///     forty custom emoji in its words and name, and every third a photograph. Each way of drawing at each length of
    ///     feed: a frame's time, what it allocates, and what is written to the terminal — on the frames a picture landed
    ///     in and over the whole scroll (#584, and the hitches over custom emoji before it).
    /// </summary>
    [Theory(Skip = "A measurement, not a test. Set WOOLY_MEASURE=1 to run it.", SkipUnless = nameof(Enabled))]
    [InlineData(PictureWay.Sixel, 40)]
    [InlineData(PictureWay.Sixel, 200)]
    [InlineData(PictureWay.Kitty, 40)]
    [InlineData(PictureWay.Kitty, 200)]
    [InlineData(PictureWay.Kitty, 800)]
    [InlineData(PictureWay.Placeholders, 200)]
    public async Task AScrollAsPicturesLand(PictureWay way, int posts)
    {
        var landed = 0;
        PicturesLanding? landing = null;

        using var pictures = new Pictures(
            async (address, _, cancellation) =>
            {
                // As a file server answers: somewhere between 40 and 160 ms, the same each time for an address.
                await Task.Delay(40 + Math.Abs(address.GetHashCode() % 120), cancellation);

                return Fetched.Of(
                    address.Contains("/emoji/", StringComparison.Ordinal) ? Busy(128, 128)
                    : address.Contains("/avatars/", StringComparison.Ordinal) ? Busy(400, 400)
                    : Busy(800, 600));
            },
            () =>
            {
                Interlocked.Exchange(ref landed, 1);
                landing?.Landed();
            });

        var customEmoji = Enumerable.Range(0, 40)
            .Select(at => new CustomEmoji($"e{at}", $"https://files.example/emoji/e{at}.png"))
            .ToArray();

        var feed = Enumerable.Range(1, posts)
            .Select(at =>
            {
                var used = Enumerable.Range(0, 4).Select(nth => customEmoji[((at * 3) + nth) % customEmoji.Length]).ToArray();

                return APost.With(
                    id: $"{at}0",
                    account: $"user{at}@mastodon.social",
                    author: $"User {used[3].Written}",
                    content: $"Post {at} {used[0].Written} with {used[1].Written} some {used[2].Written} words to wrap",
                    avatarUrl: $"https://files.example/avatars/{at}.png",
                    media: at % 3 == 0 ? [APost.APicture($"m{at}", shape: new PictureShape(800, 600))] : null,
                    customEmoji: used,
                    authorCustomEmoji: used);
            })
            .ToArray();

        using var drawn = await DrawnShell.Of(
            140,
            50,
            Themes.Dark,
            new AShell { Timelines = FakeTimelineReader.Holding(feed) },
            pictures: pictures,
            drawsPictures: way == PictureWay.Sixel,
            answersKitty: way == PictureWay.Kitty,
            drawsPlaceholders: way == PictureWay.Placeholders);

        landing = new PicturesLanding(drawn.Application);

        long written = 0;
        drawn.Application.LayoutAndDrawComplete += (_, _) =>
            written += drawn.Application.Driver!.GetOutput().GetLastOutput().Length;

        var frames = new List<(double Milliseconds, long Allocated, long Written, bool Landed)>();

        for (var frame = 0; frame < 400; frame++)
        {
            if (frame % 24 < 12)
            {
                drawn.WheelUndrawn(RailLines.Width + 4, 3);
            }

            var before = written;
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            var clock = Stopwatch.StartNew();

            drawn.Iterate();

            clock.Stop();
            frames.Add((
                clock.Elapsed.TotalMilliseconds,
                GC.GetAllocatedBytesForCurrentThread() - allocated,
                written - before,
                Interlocked.Exchange(ref landed, 0) == 1));

            // Slept rather than awaited, so that every pass runs on the thread the application was begun on.
            if (TimeSpan.FromMilliseconds(16) - clock.Elapsed is { TotalMilliseconds: > 0 } rest)
            {
                Thread.Sleep(rest);
            }
        }

        var times = frames.Select(each => each.Milliseconds).Order().ToList();
        var landings = frames.Where(each => each.Landed).ToList();

        output.WriteLine(
            $"{way}, {posts} posts: a frame median {times[times.Count / 2]:F1} ms, p90 {times[times.Count * 9 / 10]:F1}, "
            + $"worst {times[^1]:F1}; {frames.Count(each => each.Milliseconds > 50)} of {frames.Count} over 50 ms; "
            + $"{frames.Select(each => each.Allocated).Order().ElementAt(frames.Count / 2) / (1024 * 1024)} MB allocated a frame; "
            + $"{landings.Count} frames a picture landed in wrote {landings.Sum(each => each.Written) / 1024} KB, "
            + $"the whole scroll {frames.Sum(each => each.Written) / 1024} KB.");
    }

    /// <summary>A busy PNG, every pixel different from its neighbours, at the size given — one per size, made once.</summary>
    private static byte[] Busy(int width, int height) =>
        BusyPngs.GetOrAdd((width, height), size =>
        {
            using var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(size.Width, size.Height);

            for (var x = 0; x < size.Width; x++)
            {
                for (var y = 0; y < size.Height; y++)
                {
                    image[x, y] = new SixLabors.ImageSharp.PixelFormats.Rgba32(
                        (byte)((x * 7) + (y * 3)),
                        (byte)(x * y),
                        (byte)((x * 13) + (y * 11)),
                        255);
                }
            }

            using var bytes = new MemoryStream();

            SixLabors.ImageSharp.ImageExtensions.SaveAsPng(image, bytes);

            return bytes.ToArray();
        });

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(int Width, int Height), byte[]> BusyPngs = new();

    /// <summary>The median of <paramref name="counts" /> and the most of them, said.</summary>
    private static string Spread(IEnumerable<int> counts)
    {
        var sorted = counts.Order().ToList();

        return $"median {sorted[sorted.Count / 2]}, most {sorted[^1]}";
    }

    /// <summary>
    ///     How many boxes the last frame drew — the 2×1 boxes of custom emoji and the rest apart — and how many emoji
    ///     on the page were drawn as their shade, two shaded cells to each.
    /// </summary>
    private static (int CustomEmoji, int Others, int Shaded) Boxed(DrawnShell drawn)
    {
        var images = drawn.Application.Driver!.GetOutputBuffer().GetRasterImages();
        var customEmoji = images.Count(image => image.DestinationCells is { Width: 2, Height: 1 });
        var shaded = drawn.Rows().Sum(row => row.Count(cell => cell == '░')) / 2;

        return (customEmoji, images.Count - customEmoji, shaded);
    }

    /// <summary>
    ///     The floor under all of it: a notch over posts with no pictures at all, which is what a frame costs before a
    ///     single picture is drawn (#292's fifth slice).
    /// </summary>
    [Fact(Skip = "A measurement, not a test. Set WOOLY_MEASURE=1 to run it.", SkipUnless = nameof(Enabled))]
    public async Task AWheelNotchOverText()
    {
        var posts = Enumerable.Range(1, 40)
            .Select(at => APost.With(id: $"{at}0", content: $"Post {at}, long enough to wrap: {string.Concat(Enumerable.Repeat("words and more words ", 12))}"))
            .ToArray();

        using var drawn = await DrawnShell.Of(120, 50, Themes.Dark, new AShell { Timelines = FakeTimelineReader.Holding(posts) });

        for (var notch = 0; notch < 5; notch++)
        {
            drawn.Wheel(RailLines.Width + 4, 3);
        }

        Report("text only, back to back", await Notches(drawn, down: true, apart: TimeSpan.Zero, count: 150));
    }

    private const int Count = 30;

    private static async Task<(List<double> Times, List<long> Bytes)> Notches(
        DrawnShell drawn,
        bool down,
        TimeSpan apart,
        int count = Count,
        List<(int CustomEmoji, int Others, int Shaded)>? boxes = null)
    {
        var times = new List<double>();
        var bytes = new List<long>();

        for (var notch = 0; notch < count; notch++)
        {
            var clock = Stopwatch.StartNew();

            drawn.Wheel(RailLines.Width + 4, 3, down);

            times.Add(clock.Elapsed.TotalMilliseconds);
            bytes.Add(drawn.Application.Driver!.GetOutput().GetLastOutput().Length);
            boxes?.Add(Boxed(drawn));

            if (apart > TimeSpan.Zero)
            {
                await Task.Delay(apart, TestContext.Current.CancellationToken);
            }
        }

        times.Sort();

        return (times, bytes);
    }

    /// <summary>A page at a time down rows not drawn yet, as quickly as a reader taps <c>PgDn</c>.</summary>
    private async Task Pages(DrawnShell drawn)
    {
        // Back to the top, then past everything drawn so far, so every page is new.
        drawn.Press(Terminal.Gui.Input.Key.Home);
        drawn.Press(Terminal.Gui.Input.Key.PageDown);
        drawn.Press(Terminal.Gui.Input.Key.PageDown);

        var times = new List<double>();

        for (var page = 0; page < 8; page++)
        {
            await Task.Delay(150, TestContext.Current.CancellationToken);

            var clock = Stopwatch.StartNew();

            drawn.Press(Terminal.Gui.Input.Key.PageDown);

            times.Add(clock.Elapsed.TotalMilliseconds);
        }

        times.Sort();

        output.WriteLine($"8 pages down, 150 ms apart: median {times[4]:F1} ms, worst {times[^1]:F1} ms a page.");
    }

    private void Report(string what, (List<double> Times, List<long> Bytes) measured) =>
        output.WriteLine(
            $"{measured.Times.Count} notches at 120×50, {what}: "
            + $"median {measured.Times[measured.Times.Count / 2]:F1} ms, worst {measured.Times[^1]:F1} ms a notch; "
            + $"{measured.Bytes.Average() / 1024:F0} KB sent a notch on average, {measured.Bytes.Max() / 1024:F0} KB at most.");
}
