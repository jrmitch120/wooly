using System.Globalization;

namespace Wooly.Cli.Output;

/// <summary>
///     When something happened, for a person to read. One place, so a post on a timeline and the notification about it
///     cannot come to be stamped in two different formats on the same screen.
/// </summary>
internal static class LocalMoment
{
    /// <summary>
    ///     Written in this machine's own time zone: a person reading is placing what they read against their own day.
    ///     Output meant to be read back somewhere else is <c>--json</c>'s, and that stays UTC.
    /// </summary>
    public static string Of(DateTimeOffset moment) =>
        moment.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
}
