namespace Wooly.Cli.Options;

/// <summary>
///     How a length of time is written on this client's command line: a whole number and a unit, as <c>30m</c>,
///     <c>6h</c> or <c>7d</c>. A bare number is deliberately refused — <c>6</c> could be minutes, hours or days, and a
///     client that guessed would close a poll a week early or a week late without ever saying which it had assumed.
/// </summary>
internal static class DurationOption
{
    private static readonly Dictionary<char, TimeSpan> Units = new()
    {
        ['m'] = TimeSpan.FromMinutes(1),
        ['h'] = TimeSpan.FromHours(1),
        ['d'] = TimeSpan.FromDays(1),
    };

    /// <summary>
    ///     The length of time <paramref name="value" /> spells, or <see langword="null" /> if it spells none. Says
    ///     nothing about how long is <em>too</em> long: an instance has its own limits, and this client refusing first
    ///     would turn down what the instance would have taken.
    /// </summary>
    public static TimeSpan? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        if (!Units.TryGetValue(char.ToLowerInvariant(trimmed[^1]), out var unit))
        {
            return null;
        }

        // Parsed as a whole number: a fractional length of time is not something this client's units are for, and
        // "1.5h" reads differently in a locale that spells decimals with a comma.
        return int.TryParse(trimmed[..^1], out var count) && count > 0 ? count * unit : null;
    }

    /// <summary>
    ///     How a value that is not a length of time is described. Names the units, because a user who wrote one this does
    ///     not take has no other way to learn them.
    /// </summary>
    public static string Rejection(string value) =>
        $"Give a length of time as a number and a unit — 30m, 6h or 7d, not '{value}'.";
}
