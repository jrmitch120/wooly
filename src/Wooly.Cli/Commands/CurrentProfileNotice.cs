using Spectre.Console;

namespace Wooly.Cli.Commands;

/// <summary>
///     What the user is told when the profile commands default to has just been set or changed. Written in one place
///     because two commands end this way — adding the first profile, and switching to another — and the same state
///     described two ways would read as two different states.
/// </summary>
internal static class CurrentProfileNotice
{
    /// <summary>Says that <paramref name="name" /> is the profile commands act as when they are not told otherwise.</summary>
    public static void Write(IAnsiConsole console, string name) =>
        console.MarkupLineInterpolated($"Commands act as [bold]{name}[/] unless told otherwise.");
}
