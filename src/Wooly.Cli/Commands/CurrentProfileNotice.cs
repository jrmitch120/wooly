using Spectre.Console;
using Wooly.Core.Profiles;

namespace Wooly.Cli.Commands;

/// <summary>
///     What the user is told when the profile commands default to has just been set or changed. Written in one place
///     because several commands end this way — adding the first profile, switching to another — and the same state
///     described two ways would read as two different states.
/// </summary>
internal static class CurrentProfileNotice
{
    /// <summary>Says that <paramref name="name" /> is the profile commands act as when they are not told otherwise.</summary>
    public static void Write(IAnsiConsole console, string name) =>
        console.MarkupLine(ProfileWords.ActsAs($"[bold]{Markup.Escape(name)}[/]"));
}
