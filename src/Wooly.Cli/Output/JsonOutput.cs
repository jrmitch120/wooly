using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spectre.Console;

namespace Wooly.Cli.Output;

/// <summary>
///     How this client writes anything meant for another program to read. One place, so that a timeline and a post just
///     published are not indented differently or escaped differently by whichever command happened to write them.
/// </summary>
internal static class JsonOutput
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,

        // A null field is a field that does not apply — no content warning, no boost, no rate limit — and leaving it
        // out says that more plainly than a null does.
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

        // This output goes to a terminal or a pipe, never into HTML, so a post written in Japanese should read as
        // Japanese rather than as a run of \u escapes.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Writes <paramref name="document" /> to stdout, past the console's renderer.</summary>
    /// <remarks>
    ///     Unwrapped for the reason <see cref="ConsoleOutput.WriteUnwrapped" /> gives: a pipe is the whole point of this
    ///     output, and folding a long line at whatever width a console defaults to breaks it for whatever is parsing it.
    /// </remarks>
    public static void Write<TDocument>(IAnsiConsole console, TDocument document) =>
        console.WriteUnwrapped(JsonSerializer.Serialize(document, Options));
}
