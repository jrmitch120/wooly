using System.Diagnostics;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     What a wheel notch costs, headless — over sixel photographs, over the same through Kitty in a box, and over text
///     alone: the time a notch takes to draw and how much is sent to the terminal for it. A measurement rather than a
///     test — the numbers are what #292 asks to be compared before and after — so it is skipped unless asked for.
/// </summary>
/// <remarks>
///     Run with <c>WOOLY_MEASURE=1 dotnet test -c Release --filter ScrollMeasure --logger "console;verbosity=detailed"</c>.
///     The terminal is 120×50 and the photographs are busy ones at the size <c>PictureDecoder</c> holds them, as #287's
///     were measured.
/// </remarks>
public class ScrollMeasure(ITestOutputHelper output)
{
    public static bool Enabled => Environment.GetEnvironmentVariable("WOOLY_MEASURE") == "1";

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
        int count = Count)
    {
        var times = new List<double>();
        var bytes = new List<long>();

        for (var notch = 0; notch < count; notch++)
        {
            var clock = Stopwatch.StartNew();

            drawn.Wheel(RailLines.Width + 4, 3, down);

            times.Add(clock.Elapsed.TotalMilliseconds);
            bytes.Add(drawn.Application.Driver!.GetOutput().GetLastOutput().Length);

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
