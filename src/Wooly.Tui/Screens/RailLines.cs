using System.Globalization;
using Wooly.Core.Http;
using Wooly.Tui.Rendering;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     The rail as rows: the ten destinations with their unread counts, and the rate-limit quota at its foot — with the
///     instance above it where there are profiles to tell apart. Eighteen columns, full height less the status row
///     (<c>docs/tui-shell.md</c>).
/// </summary>
public static class RailLines
{
    /// <summary>How wide the rail is, which is what leaves the content 61 columns at an 80-column terminal.</summary>
    public const int Width = 18;

    /// <summary>Where the cursor is — where the tabbing has got to. Shown on the cursor's row however it stands.</summary>
    private const string CursorMark = "▶";

    /// <summary>
    ///     Where the selection has settled, shown only while it differs from the cursor — the ~250ms of a settle
    ///     window, and never at rest, since the two coincide there and the filled mark already covers the row (#78).
    /// </summary>
    private const string SettledMark = "▷";

    /// <summary>Below this share of the budget, the quota is drawn as nearly spent.</summary>
    private const double NearlySpent = 0.1;

    /// <summary>The rail, drawn to <paramref name="height" /> rows with the quota held at the bottom.</summary>
    /// <param name="rail">The destinations, the cursor, and the selection.</param>
    /// <param name="quota">What the instance last said is left, or <see langword="null" /> before anything asked.</param>
    /// <param name="height">How many rows there are, which the destinations and the quota share.</param>
    /// <param name="instance">
    ///     The instance the session is acting as, drawn above the quota, or <see langword="null" /> where there is only
    ///     the one profile and nothing to tell apart (ADR-0020).
    /// </param>
    public static IReadOnlyList<Line> Of(Rail rail, RateLimitQuota? quota, int height, string? instance = null)
    {
        var lines = new List<Line>();

        for (var at = 0; at < rail.Destinations.Count; at++)
        {
            lines.Add(Entry(rail, at));

            // The four timelines are one group and the five you-go-to-them destinations another; the profile's own
            // account is neither, so it sits below a rule of its own. The second rule moved from 7 to 8 when Discover
            // joined that group after Search — the one entry the rail has ever grown by (ADR-0019, #181).
            if (at is 3 or 8)
            {
                lines.Add(Rule());
            }
        }

        // The quota is held at the foot however tall the terminal is, because that is where a reader learns to look
        // for it — and it is load-bearing here rather than decorative: the rail is the one thing that can spend the
        // budget by accident (ADR-0014).
        var foot = Foot(quota, instance);

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

        var mark = at == rail.Cursor ? CursorMark : at == rail.Current ? SettledMark : " ";
        var unread = destination.Unread > 0 ? destination.Unread.ToString(CultureInfo.CurrentCulture) : string.Empty;
        var room = Width - Glyphs.Columns(mark) - 1 - Glyphs.Columns(unread);
        var label = Glyphs.Padded(TextWrap.Clip(destination.Label, room), room);

        return Line.Of([
            new Span($"{mark} ", role),
            new Span(label, role),
            new Span(unread, unread.Length > 0 ? Role.RailUnread : role),
        ]);
    }

    private static Line Rule() => Line.Of(new string('─', Width), Role.Chrome);

    private static IReadOnlyList<Line> Foot(RateLimitQuota? quota, string? instance)
    {
        List<Line> foot = [Rule()];

        // The instance and not the profile's name, which is the reader's own label and says nothing about who they
        // are. Read with the @handle on the Profile row it makes the full address; the handle is never moved down here,
        // so the clip only ever takes the instance's end and never the part telling two accounts on it apart (#241).
        if (instance is not null)
        {
            foot.Add(Fact(instance, Role.Quota));
        }

        foot.Add(quota is null
            ? Line.Of(new string(' ', Width), Role.Quota)
            : Fact(Spent(quota), quota.Fraction <= NearlySpent ? Role.QuotaLow : Role.Quota));

        return foot;
    }

    /// <summary>A row of the foot: one space in, clipped at its end to the rail, and padded out to it.</summary>
    private static Line Fact(string text, Role role) =>
        Line.Of(Glyphs.Padded(TextWrap.Clip($" {text}", Width), Width), role);
}
