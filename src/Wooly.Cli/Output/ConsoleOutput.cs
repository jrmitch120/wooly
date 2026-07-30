using Spectre.Console;

namespace Wooly.Cli.Output;

/// <summary>Ways of writing to the console that the rendering one would spoil.</summary>
internal static class ConsoleOutput
{
    /// <summary>
    ///     Writes <paramref name="text" /> exactly as given, past the console's renderer. Everything a person reads
    ///     goes through the renderer and should — it wraps to the terminal, and it colours. Output meant for another
    ///     program is the exception: wrapping a line of JSON at some width nobody chose breaks it for whatever is
    ///     parsing it, and under a pipe that width is a default rather than a terminal.
    /// </summary>
    public static void WriteUnwrapped(this IAnsiConsole console, string text) =>
        console.Profile.Out.Writer.WriteLine(text);
}
