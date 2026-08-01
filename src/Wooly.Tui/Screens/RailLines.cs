using System.Globalization;
using Wooly.Core.Http;
using Wooly.Tui.Rendering;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     The rail as rows: the nine destinations with their unread counts, and the rate-limit quota at its foot. Eighteen
///     columns, full height less the status row (<c>docs/tui-shell.md</c>).
/// </summary>
public static class RailLines
{
    /// <summary>How wide the rail is, which is what leaves the content 61 columns at an 80-column terminal.</summary>
    public const int Width = 18;

    /// <summary>Where the cursor is — where the tabbing has got to.</summary>
    private const string CursorMark = "▶";

    /// <summary>What is selected — what is actually on screen.</summary>
    private const string SelectedMark = "▸";

    /// <summary>Below this share of the budget, the quota is drawn as nearly spent.</summary>
    private const double NearlySpent = 0.1;

    /// <summary>The rail, drawn to <paramref name="height" /> rows with the quota held at the bottom.</summary>
    /// <param name="rail">The destinations, the cursor, and the selection.</param>
    /// <param name="quota">What the instance last said is left, or <see langword="null" /> before anything asked.</param>
    /// <param name="height">How many rows there are, which the destinations and the quota share.</param>
    public static IReadOnlyList<Line> Of(Rail rail, RateLimitQuota? quota, int height)
    {
        var lines = new List<Line>();

        for (var at = 0; at < rail.Destinations.Count; at++)
        {
            lines.Add(Entry(rail, at));

            // The four timelines are one group and the four waiting-for-you destinations another; the profile's own
            // account is neither, so it sits below a rule of its own.
            if (at is 3 or 7)
            {
                lines.Add(Rule());
            }
        }

        // The quota is held at the foot however tall the terminal is, because that is where a reader learns to look
        // for it — and it is load-bearing here rather than decorative: the rail is the one thing that can spend the
        // budget by accident (ADR-0014).
        var foot = Foot(quota);

        // Filled with rail-width blanks rather than empty rows, so the rail is a column of one width all the way down
        // rather than a ragged edge wherever a destination happens to be short.
        while (lines.Count < height - foot.Count)
        {
            lines.Add(Line.Of(new string(' ', Width), Role.Rail));
        }

        lines.AddRange(foot);

        return lines;
    }

    /// <summary>The budget as the rail says it: what is left of what is allowed.</summary>
    public static string Spent(RateLimitQuota quota) =>
        $"{quota.Remaining.ToString("N0", CultureInfo.CurrentCulture)}/{quota.Limit.ToString("N0", CultureInfo.CurrentCulture)} left";

    private static Line Entry(Rail rail, int at)
    {
        var destination = rail.Destinations[at];
        var role = at == rail.Current ? Role.RailCurrent : Role.Rail;

        var marks = $"{(at == rail.Cursor ? CursorMark : " ")}{(at == rail.Current ? SelectedMark : " ")}";
        var unread = destination.Unread > 0 ? destination.Unread.ToString(CultureInfo.CurrentCulture) : string.Empty;
        var room = Width - marks.Length - 1 - unread.Length;
        var label = TextWrap.Clip(destination.Label, room).PadRight(room);

        return Line.Of([
            new Span($"{marks} ", role),
            new Span(label, role),
            new Span(unread, unread.Length > 0 ? Role.RailUnread : role),
        ]);
    }

    private static Line Rule() => Line.Of(new string('─', Width), Role.Chrome);

    private static IReadOnlyList<Line> Foot(RateLimitQuota? quota) =>
    [
        Rule(),
        quota is null
            ? Line.Of(new string(' ', Width), Role.Quota)
            : Line.Of(
                TextWrap.Clip($" {Spent(quota)}", Width).PadRight(Width),
                quota.Fraction <= NearlySpent ? Role.QuotaLow : Role.Quota),
    ];
}
