namespace Wooly.Core.Configuration;

/// <summary>
///     Everything this client remembers between runs that is not a secret: the profiles a user has set up, which one
///     commands default to, and their preferences.
/// </summary>
public sealed record WoolyConfig
{
    /// <summary>What a machine that has never run this client has: no profiles, and nothing chosen.</summary>
    public static WoolyConfig Empty { get; } = new();

    /// <summary>
    ///     The profile commands use when no <c>--profile</c> is given, or <see langword="null" /> when none has been
    ///     chosen. Not guaranteed to name an entry in <see cref="Profiles" /> — a hand-edited file can say otherwise,
    ///     and reporting that is the caller's job, not this record's.
    /// </summary>
    public string? CurrentProfile { get; init; }

    /// <summary>Every profile that has been set up, keyed by the name the user gave it.</summary>
    public IReadOnlyDictionary<string, ProfileConfig> Profiles { get; init; } =
        new Dictionary<string, ProfileConfig>(StringComparer.Ordinal);

    /// <summary>Settings that are not tied to any one profile.</summary>
    public Preferences Preferences { get; init; } = new();

    /// <summary>
    ///     The theme the TUI draws in, by name, or <see langword="null" /> where none has been chosen. A built-in name
    ///     or one of <see cref="Themes" />; which, and whether it names one that exists at all, is the TUI's to say.
    /// </summary>
    public string? Theme { get; init; }

    /// <summary>Every theme written in the config file, keyed by the name its table was given.</summary>
    public IReadOnlyDictionary<string, ThemeConfig> Themes { get; init; } =
        new Dictionary<string, ThemeConfig>(StringComparer.Ordinal);
}
