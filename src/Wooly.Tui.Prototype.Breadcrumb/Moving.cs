using System.Diagnostics;

namespace Wooly.Tui.Prototype.Breadcrumb;

/// <summary>
///     The scenes that have to move to be judged (#213). A printed row cannot answer a question about a rate, so
///     these redraw in place until a key is pressed: five rates at once, the jiggle padding is there to stop, the two
///     cycles, and what a real fetch's life looks like at the durations fetches actually take.
/// </summary>
public static class Moving
{
    /// <summary>How often a frame is drawn. Fast enough that no beat below is measured by this instead of by itself.</summary>
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(25);

    /// <summary>The 61 columns of content an 80-column terminal leaves, which every ticket on this map owes.</summary>
    private const int Width = 61;

    /// <summary>What #168 settled: the current crumb in a foreground of its own, a long trail elided from the left.</summary>
    private static readonly Style Settled = Trail.Styles[1];

    private static readonly string[] Two = ["Home", "Post by @maria"];

    private static readonly string[] Five =
    [
        "Home", "Post by @maria@mastodon.social", "@maria@mastodon.social", "@maria@mastodon.social following",
        "Keys",
    ];

    public static void Play()
    {
        Rates();
        Jiggle();
        Cycles();
        Lives();
        Terminals();

        Console.WriteLine();
        Console.WriteLine("That is all of them.");
        Console.WriteLine();
    }

    /// <summary>The five rates side by side, which is the only way to tell a stall from a machine in trouble.</summary>
    private static void Rates()
    {
        Scene(
            "THE RATE — the same mark at five steps, all running at once",
            "A dot a second reads as a stall; four a second reads as trouble. Pick one by watching them together.",
            [
                .. Mark.Beats.Select(beat => (Func<TimeSpan, string>)(since => Labelled(
                    $"{beat.Name,-8}",
                    Draw(Two, Mark.Spelled(since, beat.Step, Mark.Arriving, padded: true, delayed: false), Ink.Dark),
                    beat.Why))),
            ]);
    }

    /// <summary>What the width question is actually about, drawn: a trail that re-elides on every tick.</summary>
    private static void Jiggle()
    {
        var beat = Mark.Beats[2].Step;

        Scene(
            "THE WIDTH — a five-deep trail at 61 columns, padded and not",
            "Unpadded, the mark is 9 columns on one tick and 11 on the next, so the elided trail under it moves too.",
            [
                since => Labelled(
                    "padded ",
                    Draw(Five, Mark.Spelled(since, beat, Mark.Arriving, padded: true, delayed: false), Ink.Dark),
                    "laid out at 11 — the trail holds still"),
                since => Labelled(
                    "not    ",
                    Draw(Five, Mark.Spelled(since, beat, Mark.Arriving, padded: false, delayed: false), Ink.Dark),
                    "the row jiggles at both ends"),
                since => Labelled(
                    "today  ",
                    Draw(Five, Mark.Still, Ink.Dark),
                    "9 columns, and it does not move at all"),
            ]);
    }

    /// <summary>Whether starting over goes back to one dot or to none.</summary>
    private static void Cycles()
    {
        var beat = Mark.Beats[2].Step;

        Scene(
            "THE CYCLE — three spellings or four",
            "Both start over; the question is whether the mark is ever the bare word.",
            [
                since => Labelled(
                    ". .. ...",
                    Draw(Two, Mark.Spelled(since, beat, Mark.Arriving, padded: true, delayed: false), Ink.Dark),
                    "never bare; the reset is a jump of two dots"),
                since => Labelled(
                    "  . .. ...",
                    Draw(Two, Mark.Spelled(since, beat, Mark.Breathing, padded: true, delayed: false), Ink.Dark),
                    "a beat of rest, and a word that reads as finished for it"),
            ]);
    }

    /// <summary>
    ///     The thing no still drawing shows: a mark that appears for one frame on a fetch nobody waited for. The
    ///     durations are what fetches actually take — a cached destination, a quick timeline, a slow one, an instance
    ///     having a bad day.
    /// </summary>
    private static void Lives()
    {
        var beat = Mark.Beats[2].Step;
        var gap = TimeSpan.FromMilliseconds(900);

        (string Name, TimeSpan Long)[] fetches =
        [
            ("80ms  ", TimeSpan.FromMilliseconds(80)),
            ("300ms ", TimeSpan.FromMilliseconds(300)),
            ("1.2s  ", TimeSpan.FromMilliseconds(1200)),
            ("4s    ", TimeSpan.FromSeconds(4)),
        ];

        foreach (var delayed in new[] { false, true })
        {
            Scene(
                delayed
                    ? "A FETCH'S LIFE — the mark waits for the first tick"
                    : "A FETCH'S LIFE — the mark appears the moment the fetch starts",
                "Four fetches of different lengths, each on a loop with a rest between. `‹in flight›` is the truth; "
                + "the row is what the reader sees.",
                [
                    .. fetches.Select(fetch => (Func<TimeSpan, string>)(since =>
                    {
                        var round = fetch.Long + gap;
                        var at = TimeSpan.FromMilliseconds(since.TotalMilliseconds % round.TotalMilliseconds);
                        var flying = at < fetch.Long;

                        return Labelled(
                            fetch.Name,
                            Draw(
                                Two,
                                flying ? Mark.Spelled(at, beat, Mark.Arriving, padded: true, delayed) : string.Empty,
                                Ink.Dark),
                            flying ? "‹in flight›" : string.Empty);
                    })),
                ]);
        }
    }

    /// <summary>The three terminals, since a mark that only reads in dark is not a mark.</summary>
    private static void Terminals()
    {
        var beat = Mark.Beats[2].Step;

        Scene(
            "THE THREE TERMINALS — and the still mark a plain one might keep",
            "Motion is not colour, so `NO_COLOR` does not settle this by itself.",
            [
                since => Labelled(
                    "dark  ",
                    Draw(Two, Mark.Spelled(since, beat, Mark.Arriving, padded: true, delayed: false), Ink.Dark),
                    string.Empty),
                since => Labelled(
                    "light ",
                    Draw(Two, Mark.Spelled(since, beat, Mark.Arriving, padded: true, delayed: false), Ink.Light),
                    string.Empty),
                since => Labelled(
                    "mono  ",
                    Draw(Two, Mark.Spelled(since, beat, Mark.Arriving, padded: true, delayed: false), Ink.Mono),
                    "the dots are all it has"),
                _ => Labelled("mono, still", Draw(Two, Mark.Still, Ink.Mono), "today's spelling, holding still"),
            ]);
    }

    private static string Draw(IReadOnlyList<string> crumbs, string mark, Ink ink) =>
        ink.Print(Trail.Row(crumbs, Settled, Elide.FromLeft, mark, Width), Settled.Banded);

    private static string Labelled(string label, string row, string note) =>
        $"  {label,-11} │{row}│ {note}";

    /// <summary>
    ///     Draws <paramref name="rows" /> over and over in place until a key is pressed, so that the thing being
    ///     judged is motion rather than a description of it.
    /// </summary>
    private static void Scene(string title, string about, IReadOnlyList<Func<TimeSpan, string>> rows)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 78));
        Console.WriteLine(title);
        Console.WriteLine(new string('=', 78));
        Console.WriteLine(about);
        Console.WriteLine();
        Console.WriteLine($"  {new string(' ', 11)} │{new string('─', Width)}│   (any key for the next)");

        var clock = Stopwatch.StartNew();
        var drawn = false;

        // A run whose output is being piped has nobody to press anything, so each scene gets a few seconds and moves
        // on rather than hanging — the frames go past either way.
        var watching = !Console.IsInputRedirected;
        var until = TimeSpan.FromSeconds(4);

        while (watching ? !Pressed() : clock.Elapsed < until)
        {
            if (drawn)
            {
                Console.Write($"[{rows.Count}A");
            }

            foreach (var row in rows)
            {
                Console.WriteLine("[2K" + row(clock.Elapsed));
            }

            drawn = true;
            Thread.Sleep(Frame);
        }

        if (watching)
        {
            Console.ReadKey(intercept: true);
        }

        Console.WriteLine();
    }

    /// <summary>Whether a key is waiting.</summary>
    private static bool Pressed() => Console.KeyAvailable;
}
