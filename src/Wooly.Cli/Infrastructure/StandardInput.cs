namespace Wooly.Cli.Infrastructure;

/// <summary>
///     What was piped into the command, for an option that takes <c>-</c> to mean "read it from there" — the one place
///     a command reads stdin, so that tests can hand it text rather than the terminal.
/// </summary>
internal sealed class StandardInput(TextReader reader)
{
    /// <summary>
    ///     Everything piped in, without the line ending it finishes on: a pipe ends on one whether or not what was meant
    ///     did (<c>echo</c> adds one), and a trailing newline is never part of what anybody meant to say.
    /// </summary>
    public string ReadAll() => reader.ReadToEnd().TrimEnd('\r', '\n');
}
