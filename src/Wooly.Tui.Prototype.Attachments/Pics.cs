// PROTOTYPE (#374) — throwaway. Pictures drawn for real: Kitty placeholders where the terminal is known to draw them
// (herdr in Ghostty, kitty), otherwise Terminal.Gui's image view (Kitty or sixel), otherwise a stand-in.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;
using Attribute = Terminal.Gui.Drawing.Attribute;
using Glyphs = Wooly.Tui.Rendering.Glyphs;
using Color = Terminal.Gui.Drawing.Color;
using Rectangle = System.Drawing.Rectangle;

namespace Wooly.Tui.Prototype.Attachments;

internal static class Pics
{
    public static IApplication App { get; set; } = null!;

    private static readonly bool ByName = KnownTerminal.DrawsPlaceholders(Environment.GetEnvironmentVariable);

    private static readonly ConcurrentDictionary<string, Task<Image<Rgba32>?>> Loads = new();

    private static readonly Dictionary<string, int> Sent = [];

    private static readonly Dictionary<string, Color[,]> Scaled = [];

    private static int _nextId = Random.Shared.Next(0x10000, 0xF00000);

    private static KittyImages? _kitty;

    public static long BytesSent { get; private set; }

    public static Raster Raster => Raster.Of(App.Driver, ByName, WindowSize.Measure);

    public static CellSize Cell => Raster.Cell ?? new CellSize(10, 20);

    public static string Way => Raster.Way.ToString();

    public static void ForgetAll()
    {
        if (ByName) App.Driver?.GetOutput().Write("\u001b_Ga=d,d=A,q=2\u001b\\");
    }

    /// <summary>The picture, decoded off the UI thread; null until it is here (a redraw follows when it lands).</summary>
    public static Image<Rgba32>? Loaded(string path)
    {
        var task = Loads.GetOrAdd(path, p =>
        {
            var load = Task.Run(() => Decode(p));
            load.ContinueWith(_ => App.Invoke(() => App.LayoutAndDraw(true)));

            return load;
        });

        return task.IsCompletedSuccessfully ? task.Result : null;
    }

    public static bool Failed(string path) => Loads.TryGetValue(path, out var task) && task.IsCompleted && task.Result is null;

    private static Image<Rgba32>? Decode(string path)
    {
        try
        {
            var kind = Instance.Of(path);
            var source = path;
            var extension = Path.GetExtension(path).ToLowerInvariant();

            if (kind is Kind.Video)
            {
                source = Frame(path);
            }
            else if (kind is Kind.Sound)
            {
                return null;
            }
            else if (extension is ".heic" or ".heif" or ".avif" && OperatingSystem.IsMacOS())
            {
                source = Converted(path);
            }

            if (source is null) return null;

            var image = Image.Load<Rgba32>(source);
            image.Mutate(context => context.AutoOrient());

            if (Math.Max(image.Width, image.Height) > 1400)
            {
                image.Mutate(context => context.Resize(new ResizeOptions { Size = new SixLabors.ImageSharp.Size(1400, 1400), Mode = ResizeMode.Max }));
            }

            return image;
        }
        catch
        {
            return null;
        }
    }

    private static string? Converted(string path)
    {
        var target = Path.Combine(Path.GetTempPath(), $"wooly-374-{Math.Abs(path.GetHashCode())}.jpg");

        if (!File.Exists(target))
        {
            Run("sips", ["-s", "format", "jpeg", "-Z", "1400", path, "--out", target]);
        }

        return File.Exists(target) ? target : null;
    }

    /// <summary>A frame a second in, where ffmpeg is about.</summary>
    private static string? Frame(string path)
    {
        var target = Path.Combine(Path.GetTempPath(), $"wooly-374-{Math.Abs(path.GetHashCode())}-frame.png");

        if (!File.Exists(target))
        {
            var ffmpeg = new[] { "/opt/homebrew/bin/ffmpeg", "/usr/local/bin/ffmpeg", "/usr/bin/ffmpeg" }.FirstOrDefault(File.Exists);

            if (ffmpeg is null) return null;

            Run(ffmpeg, ["-y", "-loglevel", "quiet", "-ss", "1", "-i", path, "-frames:v", "1", target]);
        }

        return File.Exists(target) ? target : null;
    }

    private static void Run(string file, string[] arguments)
    {
        try
        {
            var start = new ProcessStartInfo(file, arguments) { RedirectStandardOutput = true, RedirectStandardError = true };
            Process.Start(start)?.WaitForExit(10_000);
        }
        catch
        {
            // No tool, no picture.
        }
    }

    /// <summary>The largest box of <paramref name="image" />'s shape inside <paramref name="room" />, centred.</summary>
    public static Rectangle Fit(Image<Rgba32> image, Rectangle room)
    {
        var cell = Cell;
        var columns = room.Width;
        var rows = (int)Math.Round((double)columns * cell.Width * image.Height / image.Width / cell.Height);

        if (rows > room.Height)
        {
            rows = room.Height;
            columns = (int)Math.Round((double)rows * cell.Height * image.Width / image.Height / cell.Width);
        }

        columns = Math.Clamp(columns, 1, Math.Min(room.Width, KittyPlaceholder.MostCells));
        rows = Math.Clamp(rows, 1, Math.Min(room.Height, KittyPlaceholder.MostCells));

        return new Rectangle(room.X + (room.Width - columns) / 2, room.Y + (room.Height - rows) / 2, columns, rows);
    }

    /// <summary>
    ///     Draws the picture at <paramref name="path" /> into <paramref name="room" /> of <paramref name="view" />, or a
    ///     stand-in saying what it is. Returns where it drew.
    /// </summary>
    public static Rectangle Paint(Painted view, Rectangle room, string path, ITheme theme)
    {
        if (room.Width <= 0 || room.Height <= 0) return Rectangle.Empty;

        var kind = Instance.Of(path);
        var image = Loaded(path);

        if (image is null)
        {
            var word = kind is Kind.Sound ? "♪ sound"
                : kind is Kind.Video && Failed(path) ? "▶ video"
                : Failed(path) ? "no preview"
                : "…";

            StandIn(view, room, word, theme);

            return room;
        }

        var fit = Fit(image, room);

        switch (Raster.Way)
        {
            case PictureWay.Placeholders:
                var id = Transmitted(path, image, fit.Width, fit.Height);
                view.SetAttribute(KittyPlaceholder.Painted(id, theme.For(Role.Body)));

                for (var row = 0; row < fit.Height; row++)
                {
                    view.AddStr(fit.X, fit.Y + row, KittyPlaceholder.Row(row, fit.Width));
                }

                break;

            case PictureWay.Kitty or PictureWay.Sixel:
                var key = $"{path}@{fit.Width}x{fit.Height}";
                var pixels = Pixels(path, image, fit);

                if (Raster.Way == PictureWay.Sixel) view.Slot(fit, key, Encoded(key, pixels));
                else view.Slot(fit, key, new Picture(pixels));
                break;

            default:
                StandIn(view, room, kind is Kind.Video ? "▶ video" : "▣ no graphics", theme);
                return room;
        }

        if (kind is Kind.Video && fit.Width >= 3)
        {
            view.Put(fit.X + fit.Width / 2 - 1, fit.Y + fit.Height / 2, " ▶ ", Role.SelectedText);
        }

        return fit;
    }

    private static void StandIn(Painted view, Rectangle room, string word, ITheme theme)
    {
        for (var row = 0; row < room.Height; row++)
        {
            view.Put(room.X, room.Y + row, new string('░', room.Width), Role.StandIn);
        }

        if (Glyphs.Columns(word) <= room.Width)
        {
            view.Put(room.X + (room.Width - Glyphs.Columns(word)) / 2, room.Y + room.Height / 2, word, Role.Muted);
        }
    }

    private static int Transmitted(string path, Image<Rgba32> image, int columns, int rows)
    {
        var cell = Cell;
        var key = $"{path}@{columns}x{rows}@{cell.Width}x{cell.Height}";

        if (Sent.TryGetValue(key, out var id)) return id;

        id = _nextId++;
        Sent[key] = id;

        using var shrunk = image.Clone(context => context.Resize(columns * cell.Width, rows * cell.Height));
        using var png = new MemoryStream();
        shrunk.SaveAsPng(png, new SixLabors.ImageSharp.Formats.Png.PngEncoder { CompressionLevel = SixLabors.ImageSharp.Formats.Png.PngCompressionLevel.BestSpeed });

        _kitty ??= new KittyImages(sequence => App.Driver?.GetOutput().Write(sequence));
        _kitty.Transmit(id, png.ToArray(), columns, rows);
        BytesSent += png.Length;

        return id;
    }

    private static readonly Dictionary<string, Sixel> Sixels = [];

    /// <summary>
    ///     Sixel the way the product encodes it: quantized to its own palette first (SixelPalette), since Terminal.Gui's
    ///     encoder left to choose its own palette tinted a photograph red.
    /// </summary>
    private static Sixel Encoded(string key, Color[,] pixels)
    {
        if (Sixels.TryGetValue(key, out var sixel)) return sixel;

        var colours = Math.Max(16, Raster.SixelColours);
        var (quantized, palette) = SixelPalette.Quantized(pixels, colours);
        var encoder = new SixelEncoder();
        encoder.Quantizer.MaxColors = colours;
        encoder.Quantizer.PaletteBuildingAlgorithm = palette;

        return Sixels[key] = new Sixel(pixels, encoder.EncodeSixel(quantized));
    }

    private static Color[,] Pixels(string path, Image<Rgba32> image, Rectangle fit)
    {
        var cell = Cell;
        var key = $"{path}@{fit.Width}x{fit.Height}@{cell.Width}x{cell.Height}";

        if (Scaled.TryGetValue(key, out var pixels)) return pixels;

        using var shrunk = image.Clone(context => context.Resize(fit.Width * cell.Width, fit.Height * cell.Height));
        pixels = new Color[shrunk.Width, shrunk.Height];

        for (var y = 0; y < shrunk.Height; y++)
        for (var x = 0; x < shrunk.Width; x++)
        {
            var pixel = shrunk[x, y];
            pixels[x, y] = new Color(pixel.R, pixel.G, pixel.B);
        }

        Scaled[key] = pixels;

        return pixels;
    }
}

/// <summary>A view that paints its own cells in the theme's roles, and draws pictures through slots where it must.</summary>
internal abstract class Painted : View
{
    protected static ITheme Theme => Proto.Theme;

    private readonly List<PictureView> _slots = [];

    private int _used;

    protected Painted()
    {
        CanFocus = true;
    }

    public void Put(int x, int y, string text, Role role, int? room = null)
    {
        if (y < 0 || y >= Viewport.Height || x >= Viewport.Width) return;

        var fits = Glyphs.Cut(text, Math.Min(room ?? int.MaxValue, Viewport.Width - x));

        SetAttribute(Theme.For(role));
        AddStr(x, y, fits);
    }

    /// <summary>Paints a run of spans; returns the column after the last.</summary>
    public int Spans(int x, int y, params (string Text, Role Role)[] spans)
    {
        foreach (var (text, role) in spans)
        {
            Put(x, y, text, role);
            x += Glyphs.Columns(text);
        }

        return x;
    }

    public void Blank(int y, Role role = Role.Body)
    {
        if (y < 0 || y >= Viewport.Height) return;

        SetAttribute(Theme.For(role));
        AddStr(0, y, new string(' ', Viewport.Width));
    }

    /// <summary>
    ///     The fallback for terminals not known to draw placeholders: the product's own picture view positioned over the
    ///     cells — Kitty through a box, or sixel already encoded.
    /// </summary>
    public void Slot(Rectangle cells, string id, object shown)
    {
        if (_used == _slots.Count)
        {
            var slot = new PictureView();
            _slots.Add(slot);
            Add(slot);
        }

        var view = _slots[_used++];

        // Never straight from one picture to another (PictureView's own rule).
        if (view.PictureId is { } held && held != id) view.Release();

        if (view.Frame != cells) view.Frame = cells;
        if (shown is Sixel sixel) view.Show(id, sixel);
        else view.Show(id, (Picture)shown);
        view.Visible = true;
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        _used = 0;

        for (var row = 0; row < Viewport.Height; row++)
        {
            Blank(row);
        }

        Paint();

        foreach (var slot in _slots.Skip(_used))
        {
            slot.Release();
        }

        return true;
    }

    protected abstract void Paint();
}
