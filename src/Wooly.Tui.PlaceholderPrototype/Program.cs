// PROTOTYPE (#292) — throwaway, not production. Answers one question: do Kitty graphics Unicode placeholders, drawn
// through Terminal.Gui, make pictures scroll smoothly *with* the text in Ghostty?
//
// Each photo is sent to the terminal ONCE (a=t, PNG, shrunk to its box), given a virtual placement (U=1), and then
// drawn as ordinary text: every cell of its box is U+10EEEE plus a row and a column diacritic, coloured with the image
// id. Scrolling sends no image data at all — the picture is part of the rows.
//
// Run:  dotnet run --project src/Wooly.Tui.PlaceholderPrototype [photo.jpg ...]
// Keys: wheel / trackpad, ↓ ↑ (one row), j k (three rows), PgDn PgUp, s = wheel step 1 ↔ 3, q = quit.

using System.Diagnostics;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;
using Color = Terminal.Gui.Drawing.Color;
using Size = System.Drawing.Size;

if (args.Contains("--winsize"))
{
    Console.WriteLine(Winsize.Raw());

    return 0;
}

var selfTest = args.Contains("--selftest");
args = [.. args.Where(arg => arg != "--selftest")];

var photos = args.Length > 0 ? args.ToList() : MacPhotos();

if (photos.Count == 0)
{
    Console.Error.WriteLine("No photos. Pass some: dotnet run --project src/Wooly.Tui.PlaceholderPrototype a.jpg b.jpg");
    return 1;
}

using var application = Application.Create();

if (selfTest)
{
    // Headless: a 120×40 terminal that says it speaks Kitty graphics, drawn, scrolled, and read back.
    application.Init("ansi");
    var w = int.Parse(Environment.GetEnvironmentVariable("SELFTEST_W") ?? "120");
    var h = int.Parse(Environment.GetEnvironmentVariable("SELFTEST_H") ?? "40");
    application.Driver!.SetScreenSize(w, h);
    application.Driver.SetKittyGraphicsSupport(new KittyGraphicsSupportResult { IsSupported = true, Resolution = new System.Drawing.Size(16, 34) });

    var probe = new Feed(photos) { Width = Dim.Fill(), Height = Dim.Fill(1), CanFocus = true };
    var said = "";
    probe.Status = text => said = text;
    var top = new Window { BorderStyle = LineStyle.None };
    top.Add(probe);
    application.Begin(top);
    probe.SetFocus();

    if (Environment.GetEnvironmentVariable("SELFTEST_SEQ") is { } sequence)
    {
        // d = a notch down, u = a notch up, all close together; prints where the page is after each.
        application.LayoutAndDraw(true);
        probe.NewKeyDownEvent(Key.F);
        for (var go = 0; go < 20; go++) probe.NewMouseEvent(new Mouse { Flags = MouseFlags.WheeledDown });
        var line = new StringBuilder("filter 2 in a row, from row 20: ");
        foreach (var notch in sequence)
        {
            probe.NewMouseEvent(new Mouse { Flags = notch == 'd' ? MouseFlags.WheeledDown : MouseFlags.WheeledUp });
            application.LayoutAndDraw(true);
            line.Append($"{notch}→{System.Text.RegularExpressions.Regex.Match(said, @"row (\d+)").Groups[1].Value} ");
        }
        Console.WriteLine(line);
        return 0;
    }

    if (Environment.GetEnvironmentVariable("SELFTEST_DUMP") is { } dump)
    {
        application.LayoutAndDraw(true);
        probe.NewKeyDownEvent(Key.J); // nothing; just make sure it is laid
        for (var go = 0; go < int.Parse(dump); go++) probe.NewMouseEvent(new Mouse { Flags = MouseFlags.WheeledDown });
        application.LayoutAndDraw(true);
        var text = application.Driver.ToAnsi();
        var lines = text.Split('\n');
        foreach (var (line, index) in lines.Select((line, index) => (line, index)).Where(pair => pair.line.Contains(char.ConvertFromUtf32(0x10EEEE))).Take(2))
        {
            var shown = string.Concat(line.EnumerateRunes().Select(rune => rune.Value == 0x10EEEE ? "▣" : rune.Value is >= 0x300 and < 0x10000 && System.Globalization.CharUnicodeInfo.GetUnicodeCategory(rune.Value) == System.Globalization.UnicodeCategory.NonSpacingMark ? $"[{Array.IndexOf(Diacritics.All, rune.ToString())}]" : rune.Value == 0x1b ? "␛" : rune.ToString()));
            Console.WriteLine($"row {index}: {shown}");
        }
        Console.WriteLine(said);
        return 0;
    }

    for (var step = 0; step < 6; step++)
    {
        application.LayoutAndDraw(true);
        var ansi = application.Driver.ToAnsi();
        var placeholders = ansi.EnumerateRunes().Count(rune => rune.Value == 0x10EEEE);
        Console.WriteLine($"step {step}: placeholder cells on screen {placeholders} |{said}");
        probe.NewMouseEvent(new Mouse { Flags = MouseFlags.WheeledDown, Position = new System.Drawing.Point(5, 5) });
        top.NewKeyDownEvent(Key.J);
    }

    top.Dispose();

    return 0;
}

application.Init();

var feed = new Feed(photos) { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(1), CanFocus = true };
var status = new Label { X = 0, Y = Pos.AnchorEnd(1), Width = Dim.Fill(), Text = "starting…" };
var window = new Window { BorderStyle = LineStyle.None };

feed.Status = text => status.Text = text;
window.Add(feed, status);
window.Initialized += (_, _) => feed.SetFocus();
window.KeyDown += (_, key) =>
{
    if (key == Key.Q || key == Key.Q.WithCtrl)
    {
        application.RequestStop();
        key.Handled = true;
    }
};

// Nothing left over from an earlier run, on the way in or on the way out.
const string ForgetEveryImage = "\u001b_Ga=d,d=A,q=2\u001b\\";
window.Initialized += (_, _) => application.Driver!.GetOutput().Write(ForgetEveryImage);
window.IsRunningChanged += (_, _) => { if (!window.IsRunning) application.Driver?.GetOutput().Write(ForgetEveryImage); };

application.Run(window);
window.Dispose();

return 0;

// Real photographs in the shapes a feed carries — landscape, wide and portrait — from picsum.photos (free stock
// photos, fetched once into a temp folder), falling back to the one full-size photographic wallpaper macOS ships.
static List<string> MacPhotos()
{
    var folder = Path.Combine(Path.GetTempPath(), "wooly-292-prototype-photos");
    Directory.CreateDirectory(folder);

    (string Name, string Spec)[] wanted =
    [
        ("Lakeside 3x2", "10/1600/1067"),
        ("River 16x9", "1015/1600/900"),
        ("Dog portrait 3x4", "1025/1200/1600"),
        ("Forest 3x2", "1018/1600/1067"),
        ("Mountains 2x1", "1036/1600/800"),
    ];

    var found = new List<string>();
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };

    foreach (var (name, spec) in wanted)
    {
        var target = Path.Combine(folder, name + ".jpg");

        if (!File.Exists(target))
        {
            try
            {
                File.WriteAllBytes(target, http.GetByteArrayAsync($"https://picsum.photos/id/{spec}").GetAwaiter().GetResult());
            }
            catch
            {
                continue;
            }
        }

        found.Add(target);
    }

    if (found.Count == 0 && File.Exists("/System/Library/Desktop Pictures/Sonoma.heic"))
    {
        var sonoma = Path.Combine(folder, "Sonoma.jpg");

        if (!File.Exists(sonoma))
        {
            Process.Start(new ProcessStartInfo("sips", ["-s", "format", "jpeg", "-Z", "1600", "/System/Library/Desktop Pictures/Sonoma.heic", "--out", sonoma])
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            })!.WaitForExit();
        }

        found.Add(sonoma);
    }

    return found;
}

/// <summary>A feed of fake posts, each a few lines of text and a photo, scrolled a row at a time.</summary>
internal sealed class Feed(List<string> photos) : View
{

    private static readonly string Cell = char.ConvertFromUtf32(0x10EEEE);

    private readonly Dictionary<string, Image<Rgba32>> _decoded = [];

    /// <summary>Which image ids the terminal holds, by "path@columns×rows".</summary>
    private readonly Dictionary<string, int> _sent = [];

    private List<Row> _rows = [];
    private int _laidWidth = -1;
    private Size _laidCell;
    private int _top;
    private int _wheelStep = 1;
    // Ids unique to this run, high enough not to collide with anything an earlier run left in the terminal: a
    // placeholder cell names no placement, so the terminal draws it with the *first* placement it holds for the id —
    // and a stale one from a previous run, at another size, is a picture fitted into the wrong box.
    private int _nextId = Random.Shared.Next(0x10000, 0xF00000);
    private long _imageBytesSent;
    private long _imageBytesThisScroll;
    private double _lastDrawMs;
    private int _wheelEvents;

    public Action<string>? Status { get; set; }

    /// <summary>
    ///     The cell's size in pixels: the window's pixel size from the kernel (TIOCGWINSZ, which Ghostty fills in)
    ///     divided by its cells, else whatever Terminal.Gui's detection reported, else 10×20.
    /// </summary>
    private Size CellPixels => Winsize.Cell() ?? (App?.Driver?.KittyGraphicsSupport is { IsSupported: true, Resolution: { Width: > 0, Height: > 0 } r } ? r : new Size(10, 20));

    private string CellSource => Winsize.Cell() is not null ? "ioctl" : App?.Driver?.KittyGraphicsSupport?.IsSupported == true ? "detected" : "guessed";

    /// <summary>
    ///     Kitty graphics, known at once from the terminal's own environment where it is one that has them, rather than
    ///     waiting several seconds on Terminal.Gui's query for the answer.
    /// </summary>
    private bool Kitty => KnownKitty || App?.Driver?.KittyGraphicsSupport?.IsSupported == true;

    private static readonly bool KnownKitty =
        Environment.GetEnvironmentVariable("TERM_PROGRAM") is "ghostty" or "WezTerm"
        || Environment.GetEnvironmentVariable("TERM") is "xterm-ghostty" or "xterm-kitty"
        || Environment.GetEnvironmentVariable("KITTY_WINDOW_ID") is not null
        || Environment.GetEnvironmentVariable("GHOSTTY_RESOURCES_DIR") is not null;

    // The wheel's events as they arrived, to tell a trackpad sending a stray opposite event apart from us mishandling
    // one. 'l' writes the log to a file; 'f' toggles the filter that holds back a stray reversal.
    private readonly Stopwatch _since = Stopwatch.StartNew();
    private readonly List<string> _log = [];
    private double _lastEventAt = -1;
    private int _lastDirection;
    private double _lastAppliedAt = -1;
    private int _lastAppliedDirection;
    private int _reversals;
    private int _dropped;
    /// <summary>0 = off; otherwise how many events in a row must agree before the direction changes.</summary>
    private int _filter;

    private int _moving;
    private int _held;

    /// <summary>A gap this long between events is a new gesture, and its first event goes whichever way it says.</summary>
    private const double PauseMs = 250;

    /// <summary>How soon after a step one way a step the other way counts as a stray rather than a change of mind.</summary>
    private const double StrayWithinMs = 150;

    protected override bool OnMouseEvent(Mouse mouse)
    {
        var direction = mouse.Flags.HasFlag(MouseFlags.WheeledDown) ? 1 : mouse.Flags.HasFlag(MouseFlags.WheeledUp) ? -1 : 0;

        if (direction == 0)
        {
            return false;
        }

        var now = _since.Elapsed.TotalMilliseconds;
        var gap = _lastEventAt < 0 ? -1 : now - _lastEventAt;
        var reversal = _lastDirection != 0 && direction != _lastDirection && gap >= 0 && gap < StrayWithinMs;

        if (reversal)
        {
            _reversals++;
        }

        // Held back: the other way from the direction the page is moving in, until enough events in a row agree that
        // the reader has changed their mind. No clock — a page still moving (the trackpad's inertia included) would
        // otherwise hold a real reversal back for as long as it kept moving. After a pause it goes through at once.
        var paused = gap < 0 || gap > PauseMs;
        var stray = false;

        if (_filter > 0 && _moving != 0 && direction != _moving && !paused)
        {
            _held++;

            if (_held < _filter)
            {
                stray = true;
            }
        }
        else
        {
            _held = 0;
        }

        if (stray)
        {
            _dropped++;
        }
        else
        {
            // A confirmed reversal takes the steps it held back too, so a change of mind loses nothing.
            var steps = _held >= _filter && _held > 0 ? _held : 1;

            _held = 0;
            _moving = direction;
            _lastAppliedAt = now;
            _lastAppliedDirection = direction;
            _wheelEvents++;
            ScrollBy(direction * _wheelStep * steps);
        }

        _log.Add($"{now,9:F1}ms  +{(gap < 0 ? 0 : gap),7:F1}ms  {(direction > 0 ? "down" : "UP  ")}  flags={mouse.Flags}{(reversal ? "  <-- REVERSAL" : "")}{(stray ? "  (dropped)" : "")}");

        if (_log.Count > 400)
        {
            _log.RemoveAt(0);
        }

        _lastEventAt = now;
        _lastDirection = direction;

        if (stray)
        {
            SetNeedsDraw();
        }

        return true;
    }

    protected override bool OnKeyDown(Key key)
    {
        if (key == Key.CursorDown) { ScrollBy(1); return true; }
        if (key == Key.CursorUp) { ScrollBy(-1); return true; }
        if (key == Key.J) { ScrollBy(3); return true; }
        if (key == Key.K) { ScrollBy(-3); return true; }
        if (key == Key.PageDown) { ScrollBy(Viewport.Height); return true; }
        if (key == Key.PageUp) { ScrollBy(-Viewport.Height); return true; }
        if (key == Key.S) { _wheelStep = _wheelStep == 1 ? 3 : 1; SetNeedsDraw(); return true; }
        if (key == Key.F) { _filter = _filter switch { 0 => 2, 2 => 3, _ => 0 }; _held = 0; SetNeedsDraw(); return true; }
        if (key == Key.L)
        {
            var file = Path.Combine(Path.GetTempPath(), "wooly-292-wheel.log");
            File.WriteAllLines(file, [$"filter {(_filter == 0 ? "off" : $"{_filter} in a row")}, reversals {_reversals}, dropped {_dropped}", .. _log]);
            Status?.Invoke($" wheel log written to {file}");
            return true;
        }

        return false;
    }

    private void ScrollBy(int rows)
    {
        _top = Math.Clamp(_top + rows, 0, Math.Max(0, _rows.Count - Viewport.Height));
        SetNeedsDraw();
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        var clock = Stopwatch.StartNew();
        var width = Viewport.Width;
        var height = Viewport.Height;

        _imageBytesThisScroll = 0;

        if (width != _laidWidth || CellPixels != _laidCell)
        {
            Lay(width);
        }

        for (var row = 0; row < height; row++)
        {
            var at = _top + row;

            SetAttribute(new Attribute(Color.White, Color.Black));
            AddStr(0, row, new string(' ', width));

            if (at >= _rows.Count)
            {
                continue;
            }

            switch (_rows[at])
            {
                case TextRow text:
                    SetAttribute(new Attribute(text.Muted ? Color.Gray : Color.White, Color.Black));
                    AddStr(1, row, text.Text.Length > width - 2 ? text.Text[..(width - 2)] : text.Text);
                    break;

                case PictureRow picture when Kitty:
                    var id = Sent(picture);

                    // The image id, carried in the foreground colour (24-bit), is how the terminal knows whose tile
                    // this cell is. Row and column ride on each cell as diacritics, so a box half scrolled off the
                    // top draws only its lower rows and the terminal crops for free.
                    SetAttribute(new Attribute(new Color((id >> 16) & 0xFF, (id >> 8) & 0xFF, id & 0xFF), Color.Black));

                    var cells = new StringBuilder();

                    for (var column = 0; column < picture.Columns; column++)
                    {
                        cells.Append(Cell).Append(Diacritics.All[picture.Row]).Append(Diacritics.All[column]);
                    }

                    AddStr(1, row, cells.ToString());
                    break;

                case PictureRow:
                    SetAttribute(new Attribute(Color.Gray, Color.Black));
                    AddStr(1, row, "▒ (this terminal did not report Kitty graphics)");
                    break;
            }
        }

        _lastDrawMs = clock.Elapsed.TotalMilliseconds;

        Status?.Invoke(
            $" {(Kitty ? KnownKitty ? "kitty ✓ (env)" : "kitty ✓ (detected)" : "kitty ✗")}  cell {CellPixels.Width}×{CellPixels.Height}px ({CellSource})  row {_top}/{Math.Max(0, _rows.Count - height)}"
            + $"  wheel step {_wheelStep} (s)  events {_wheelEvents}  reversals {_reversals}  filter {(_filter == 0 ? "off" : $"{_filter} in a row, dropped {_dropped}")} (f)  log (l)  draw {_lastDrawMs:F1}ms"
            + $"  image bytes: this frame {_imageBytesThisScroll / 1024}KB, total {_imageBytesSent / 1024}KB  q quits");

        return true;
    }

    /// <summary>Lays the feed out at this width: per photo, a post's text, then a box sized to the photo's shape.</summary>
    private void Lay(int width)
    {
        _laidWidth = width;
        _laidCell = CellPixels;

        var rows = new List<Row>();
        var columns = Math.Max(1, Math.Min(width - 2, Diacritics.All.Length));
        var post = 0;

        for (var round = 0; round < 3; round++)
        {
            foreach (var path in photos)
            {
                post++;
                var image = Decoded(path);

                rows.Add(new TextRow($"@ewe@hachyderm.io · post {post}", false));
                rows.Add(new TextRow($"{Path.GetFileNameWithoutExtension(path)} — a photograph, sent to the terminal once and scrolled as text.", false));
                rows.Add(new TextRow("Scroll with the trackpad: the picture should move in the same frame as these words.", true));
                rows.Add(new TextRow("", false));

                // Full width where the photo's shape allows it; a photo that would then be taller than most of the page
                // is narrowed instead — fitted, never squashed. Rows and columns both from the photo's shape and the
                // cell's, since a cell is taller than it is wide.
                var most = Math.Max(6, Math.Min(Diacritics.All.Length, Viewport.Height * 3 / 4));
                var boxColumns = columns;
                var boxRows = (int)Math.Round((double)boxColumns * _laidCell.Width * image.Height / image.Width / _laidCell.Height);

                if (boxRows > most)
                {
                    boxRows = most;
                    boxColumns = (int)Math.Round((double)boxRows * _laidCell.Height * image.Width / image.Height / _laidCell.Width);
                }

                boxRows = Math.Max(1, boxRows);

                for (var r = 0; r < boxRows; r++)
                {
                    rows.Add(new PictureRow(path, r, boxColumns, boxRows));
                }

                rows.Add(new TextRow("", false));
                rows.Add(new TextRow("↩ 2  ⟳ 5  ★ 11", true));
                rows.Add(new TextRow(new string('─', Math.Max(0, width - 2)), true));
            }
        }

        _rows = rows;
        _top = Math.Min(_top, Math.Max(0, _rows.Count - Viewport.Height));

        // Every photo sent up front, at layout, so that no scroll ever waits on an encode. The real thing would do
        // this off the UI thread as pictures arrive; here it just costs a moment at startup and on a resize.
        if (Kitty)
        {
            foreach (var picture in rows.OfType<PictureRow>().Where(row => row.Row == 0))
            {
                Sent(picture);
            }
        }
    }

    private Image<Rgba32> Decoded(string path)
    {
        if (!_decoded.TryGetValue(path, out var image))
        {
            _decoded[path] = image = Image.Load<Rgba32>(path);
        }

        return image;
    }

    /// <summary>
    ///     The terminal's id for this photo at this box size, sending it first if it has never been sent: a PNG shrunk
    ///     to the box's pixels, then a virtual placement of it over columns × rows cells. Once per photo per size.
    /// </summary>
    private int Sent(PictureRow picture)
    {
        var key = $"{picture.Path}@{picture.Columns}x{picture.Rows}";

        if (_sent.TryGetValue(key, out var id))
        {
            return id;
        }

        id = _nextId++;
        _sent[key] = id;

        using var shrunk = Decoded(picture.Path).Clone(context => context.Resize(
            picture.Columns * _laidCell.Width,
            picture.Rows * _laidCell.Height));

        using var png = new MemoryStream();
        shrunk.SaveAsPng(png, new SixLabors.ImageSharp.Formats.Png.PngEncoder { CompressionLevel = SixLabors.ImageSharp.Formats.Png.PngCompressionLevel.BestSpeed });

        var payload = Convert.ToBase64String(png.ToArray());
        var out_ = new StringBuilder();

        for (var at = 0; at < payload.Length; at += 4096)
        {
            var chunk = payload.Substring(at, Math.Min(4096, payload.Length - at));
            var more = at + 4096 < payload.Length ? 1 : 0;

            out_.Append(at == 0 ? $"\u001b_Ga=t,f=100,t=d,i={id},q=2,m={more};" : $"\u001b_Gm={more};");
            out_.Append(chunk).Append("\u001b\\");
        }

        // The virtual placement: where U=1 says "draw me wherever my placeholder cells are".
        out_.Append($"\u001b_Ga=p,U=1,i={id},c={picture.Columns},r={picture.Rows},q=2\u001b\\");

        App!.Driver!.GetOutput().Write(out_.ToString());

        _imageBytesSent += out_.Length;
        _imageBytesThisScroll += out_.Length;

        return id;
    }

    private abstract record Row;

    private sealed record TextRow(string Text, bool Muted) : Row;

    private sealed record PictureRow(string Path, int Row, int Columns, int Rows) : Row;
}

/// <summary>The terminal window's size from the kernel, which a terminal that knows its pixels fills in.</summary>
internal static class Winsize
{
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct Size4
    {
        public ushort Rows;
        public ushort Columns;
        public ushort XPixels;
        public ushort YPixels;
    }

    // ioctl is variadic. On Apple Silicon a variadic argument goes on the stack, not in a register, so declaring it
    // as ioctl(int, ulong, ref Size4) hands the kernel a garbage address (an access violation, seen in Ghostty). The
    // six dummies fill the rest of x0–x7, which pushes the pointer onto the stack where a variadic callee reads it.
    [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
    private static extern int IoctlArm64(int fd, ulong request, long x2, long x3, long x4, long x5, long x6, long x7, ref Size4 size);

    // Everywhere else (x64, and arm64 Linux) a variadic argument is passed like any other.
    [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
    private static extern int Ioctl(int fd, ulong request, ref Size4 size);

    private static int Ask(int fd, ulong request, ref Size4 size) =>
        OperatingSystem.IsMacOS() && System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.Arm64
            ? IoctlArm64(fd, request, 0, 0, 0, 0, 0, 0, ref size)
            : Ioctl(fd, request, ref size);

    /// <summary>What the kernel says, raw, for the --winsize check.</summary>
    public static string Raw()
    {
        var size = new Size4();
        var result = Ask(1, OperatingSystem.IsMacOS() ? 0x40087468UL : 0x5413UL, ref size);

        return $"ioctl={result} rows={size.Rows} cols={size.Columns} xpixel={size.XPixels} ypixel={size.YPixels}";
    }


    public static Size? Cell()
    {
        if (OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            var size = new Size4();
            var request = OperatingSystem.IsMacOS() ? 0x40087468UL : 0x5413UL;

            if (Ask(1, request, ref size) != 0 || size.Columns == 0 || size.Rows == 0 || size.XPixels == 0 || size.YPixels == 0)
            {
                return null;
            }

            return new Size(size.XPixels / size.Columns, size.YPixels / size.Rows);
        }
        catch
        {
            return null;
        }
    }
}
