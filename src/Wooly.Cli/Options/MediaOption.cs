using Wooly.Core.Posts;

namespace Wooly.Cli.Options;

/// <summary>
///     How <c>--media</c> is spelled: a path, and after a colon the alt text describing what the file shows. One word
///     rather than two options, so that a post carrying three files says which description belongs to which — three
///     <c>--media</c> flags and three <c>--alt</c> flags would only be paired by counting.
///     <para>
///         Which colon separates is the whole of the rule. A Windows path opens with one (<c>C:\pics\cat.png</c>) that is
///         part of the path, so that one is stepped over and the next one separates. What this cannot express is a file
///         whose own name contains a colon — rare enough on Windows, where the character is not allowed in a name, and
///         answerable elsewhere by renaming the file or attaching it without alt text.
///     </para>
///     Lives in the CLI rather than the core layer because it is a command line's problem: a TUI composing a post has a
///     field for the path and a field for the description, and no need of a character between them.
/// </summary>
internal static class MediaOption
{
    /// <summary>The attachment <paramref name="value" /> describes.</summary>
    /// <remarks>Only meaningful for a value <see cref="IsWellFormed" /> accepts; a caller is expected to have asked.</remarks>
    public static MediaAttachment Parse(string value)
    {
        var trimmed = value.Trim();
        var separator = SeparatorIn(trimmed);

        if (separator < 0)
        {
            return new MediaAttachment { Path = trimmed };
        }

        var altText = trimmed[(separator + 1)..].Trim();

        return new MediaAttachment
        {
            Path = trimmed[..separator],

            // A colon with nothing after it is somebody who meant to write a description and did not. An empty one
            // would be worse than none: a reader relying on it would be told the picture shows nothing.
            AltText = altText.Length == 0 ? null : altText,
        };
    }

    /// <summary>Whether <paramref name="value" /> names a file at all.</summary>
    public static bool IsWellFormed(string? value) => value is not null && Parse(value).Path.Length > 0;

    /// <summary>
    ///     How a value that names no file is described. Says what the option looks like, because a user who wrote it
    ///     wrongly has nothing else to go on.
    /// </summary>
    public static string Rejection(string value) =>
        $"Give --media a file to attach, optionally followed by ':' and what it shows — "
        + $"cat.png or 'cat.png:a ginger cat', not '{value}'.";

    /// <summary>
    ///     Where the separating colon is, or <c>-1</c> if there is none. A drive letter's colon — one letter, a colon,
    ///     then a slash — is part of the path and is stepped over.
    /// </summary>
    private static int SeparatorIn(string value)
    {
        var from = LooksLikeADriveLetter(value) ? 2 : 0;
        var separator = value.IndexOf(':', from);

        return separator;
    }

    private static bool LooksLikeADriveLetter(string value) =>
        value.Length >= 3 && char.IsLetter(value[0]) && value[1] == ':' && value[2] is '\\' or '/';
}
