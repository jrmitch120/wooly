using System.Globalization;

namespace Wooly.Tui.Rendering;

/// <summary>
///     How long ago something was, in the two or three characters a feed has room for at the end of a byline. A
///     timeline is read by scanning it, and "12m" answers the only question being asked of a timestamp there; the
///     moment itself is on the post screen, where there is room for it.
/// </summary>
public static class Elapsed
{
    /// <summary>How long before <paramref name="now" /> the moment <paramref name="then" /> was.</summary>
    /// <remarks>
    ///     Rounded down at every step, because "1h" for something 119 minutes old is the reading a person expects from
    ///     a clock. A moment in the future — an instance whose clock is ahead of this machine's — reads as "now"
    ///     rather than as a negative age.
    /// </remarks>
    public static string Since(DateTimeOffset then, DateTimeOffset now)
    {
        var since = now - then;

        return since switch
        {
            { TotalMinutes: < 1 } => "now",
            { TotalHours: < 1 } => Count(since.TotalMinutes, "m"),
            { TotalDays: < 1 } => Count(since.TotalHours, "h"),
            { TotalDays: < 365 } => Count(since.TotalDays, "d"),
            _ => Count(since.TotalDays / 365, "y"),
        };
    }

    /// <summary>The moment itself, for a screen with room to say it exactly.</summary>
    public static string Moment(DateTimeOffset when) =>
        when.ToLocalTime().ToString("d MMM yyyy, HH:mm", CultureInfo.CurrentCulture);

    private static string Count(double howMany, string unit) =>
        ((int)Math.Floor(howMany)).ToString(CultureInfo.InvariantCulture) + unit;
}
