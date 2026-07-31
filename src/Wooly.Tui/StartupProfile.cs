namespace Wooly.Tui;

/// <summary>
///     The one thing the TUI takes on its command line: which profile to act as for this run (story 9). The same
///     <c>--profile</c> the CLI's commands take, meaning the same thing — act as that profile, without changing which
///     one is current.
/// </summary>
/// <remarks>
///     Read by hand rather than through Spectre.Console.Cli, which is the CLI's parser and would bring a whole command
///     surface with it for one option. There is nothing else to parse here: the TUI is one screen, not a verb.
/// </remarks>
public static class StartupProfile
{
    private const string Flag = "--profile";

    /// <summary>
    ///     The profile named in <paramref name="args" />, or <see langword="null" /> where none was — which means the
    ///     current one.
    /// </summary>
    /// <remarks>Both spellings, because a user who types one and is told to type the other has found a papercut.</remarks>
    public static string? NamedIn(IReadOnlyList<string> args)
    {
        for (var at = 0; at < args.Count; at++)
        {
            if (args[at].StartsWith($"{Flag}=", StringComparison.Ordinal))
            {
                return Named(args[at][(Flag.Length + 1)..]);
            }

            if (args[at] == Flag && at + 1 < args.Count)
            {
                return Named(args[at + 1]);
            }
        }

        return null;
    }

    /// <summary>
    ///     A name with nothing in it is nobody, and is answered the way naming nobody is: with the current profile.
    ///     The alternative is looking up a profile called the empty string, which fails with a message about a profile
    ///     that was never asked for.
    /// </summary>
    private static string? Named(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
