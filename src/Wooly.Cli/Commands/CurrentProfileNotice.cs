using Spectre.Console;

namespace Wooly.Cli.Commands;

/// <summary>
///     What the user is told when the profile commands default to has just been set or changed. Written in one place
///     because several commands end this way — adding the first profile, switching to another, removing the current
///     one — and the same state described two ways would read as two different states.
/// </summary>
internal static class CurrentProfileNotice
{
    /// <summary>Says that <paramref name="name" /> is the profile commands act as when they are not told otherwise.</summary>
    public static void Write(IAnsiConsole console, string name) =>
        console.MarkupLineInterpolated($"Commands act as [bold]{name}[/] unless told otherwise.");

    /// <summary>
    ///     Says that no profile is current any more, and how to choose one. Worded after the failure the next command
    ///     with no <c>--profile</c> would meet, so the two read as the one state they are.
    /// </summary>
    public static void WriteNone(IAnsiConsole console) =>
        console.WriteLine("No profile is current. Switch to one with profile switch <NAME>, or name one with --profile.");
}
