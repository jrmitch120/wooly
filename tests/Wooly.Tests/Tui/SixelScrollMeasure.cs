using System.Diagnostics;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     What a wheel notch costs over sixel photographs, headless: the time a notch takes to draw and how much is sent
///     to the terminal for it. A measurement rather than a test — the numbers are what #292 asks to be compared before
///     and after — so it is skipped unless asked for.
/// </summary>
/// <remarks>
///     Run with <c>WOOLY_MEASURE=1 dotnet test -c Release --filter SixelScrollMeasure --logger "console;verbosity=detailed"</c>.
///     The terminal is 120×50 and the photographs are busy ones at the size <c>PictureDecoder</c> holds them, as #287's
///     were measured.
/// </remarks>
public class SixelScrollMeasure(ITestOutputHelper output)
{
    public static bool Enabled => Environment.GetEnvironmentVariable("WOOLY_MEASURE") == "1";

    [Fact(Skip = "A measurement, not a test. Set WOOLY_MEASURE=1 to run it.", SkipUnless = nameof(Enabled))]
    public async Task AWheelNotchOverSixelPhotographs()
    {
        var pictures = FakePictures.With();
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
        Report("down, new rows, a notch every 16 ms", await Notches(drawn, down: true, apart: TimeSpan.FromMilliseconds(16)));
        Report("down, new rows, back to back", await Notches(drawn, down: true, apart: TimeSpan.Zero));
        Report("up, rows already drawn, back to back", await Notches(drawn, down: false, apart: TimeSpan.Zero));
    }

    private const int Count = 30;

    private static async Task<(List<double> Times, List<long> Bytes)> Notches(DrawnShell drawn, bool down, TimeSpan apart)
    {
        var times = new List<double>();
        var bytes = new List<long>();

        for (var notch = 0; notch < Count; notch++)
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

    private void Report(string what, (List<double> Times, List<long> Bytes) measured) =>
        output.WriteLine(
            $"{Count} notches over sixel photographs at 120×50, {what}: "
            + $"median {measured.Times[Count / 2]:F1} ms, worst {measured.Times[^1]:F1} ms a notch; "
            + $"{measured.Bytes.Average() / 1024:F0} KB sent a notch on average, {measured.Bytes.Max() / 1024:F0} KB at most.");
}
