using System.Text;
using Wooly.Core.Posts;

namespace Wooly.Tui.Shell;

/// <summary>
///     A paste read as files dropped onto the terminal (#375). A terminal has no drop of its own to tell a program
///     about: dropping files onto one pastes their paths, quoted or escaped the way its shell would want them. So a paste
///     made entirely of existing files the instance accepts is taken as a drop, and anything else — a sentence with a
///     path in it included — is text, as it always was.
/// </summary>
internal static class Dropped
{
    /// <summary>
    ///     The files <paramref name="pasted" /> names, in order, where every word of it is a whole path to an existing
    ///     file of a type <paramref name="limits" /> accepts — and <see langword="null" /> otherwise, which leaves it to
    ///     be pasted as text.
    /// </summary>
    /// <remarks>
    ///     A whole path, from the root: a terminal pastes a dropped file as one, and a bare name that happened to match a
    ///     file in whatever folder Wooly was launched from is somebody typing it.
    /// </remarks>
    public static IReadOnlyList<string>? Paths(string pasted, PostLimits limits)
    {
        var paths = Words(pasted).Select(Unwrapped).ToList();

        return paths.Count > 0 && paths.All(path => Path.IsPathFullyQualified(path)
                                                    && File.Exists(path)
                                                    && limits.Accepts(path))
            ? paths
            : null;
    }

    /// <summary>
    ///     A word as the file it stands for: a <c>file://</c> address as its path, as some terminals paste one, and a
    ///     path under <c>~</c> under the home folder, as a shell would expand it.
    /// </summary>
    private static string Unwrapped(string word)
    {
        if (word.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
            && Uri.TryCreate(word, UriKind.Absolute, out var address))
        {
            return address.LocalPath;
        }

        return word.StartsWith("~/", StringComparison.Ordinal)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), word[2..])
            : word;
    }

    /// <summary>
    ///     <paramref name="text" /> split into words the way a terminal quotes a drop: whitespace between them, a space
    ///     inside one escaped with a backslash or the whole word in quotes. Not on Windows, whose paths are made of
    ///     backslashes and whose terminals quote a dropped path rather than escape it.
    /// </summary>
    private static List<string> Words(string text)
    {
        var words = new List<string>();
        var word = new StringBuilder();
        char? quote = null;

        for (var at = 0; at < text.Length; at++)
        {
            var letter = text[at];

            if (quote is { } open)
            {
                if (letter == open)
                {
                    quote = null;
                }
                else
                {
                    word.Append(letter);
                }
            }
            else if (letter is '\'' or '"')
            {
                quote = letter;
            }
            else if (letter == '\\' && at + 1 < text.Length && !OperatingSystem.IsWindows())
            {
                word.Append(text[++at]);
            }
            else if (char.IsWhiteSpace(letter))
            {
                Said();
            }
            else
            {
                word.Append(letter);
            }
        }

        Said();

        return words;

        void Said()
        {
            if (word.Length > 0)
            {
                words.Add(word.ToString());
                word.Clear();
            }
        }
    }
}
