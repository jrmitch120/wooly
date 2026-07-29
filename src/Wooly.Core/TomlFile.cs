using System.Text;
using Tomlyn;
using Tomlyn.Model;
using Wooly.Core.Errors;

namespace Wooly.Core;

/// <summary>
///     Reading and writing one of this client's own TOML files. Both stores keep their own file's shape and meaning;
///     what they share — a file that may not exist yet, a parse failure the user has to be told about, and a directory
///     that has to be there before a write — lives here once.
/// </summary>
internal static class TomlFile
{
    /// <summary>TOML is written without a byte order mark, which the format does not want and hand-editors do not expect.</summary>
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    ///     Reads <paramref name="path" />, or an empty table if it does not exist yet — the normal state of a machine
    ///     that has never run this client, not a failure.
    /// </summary>
    /// <exception cref="ConfigurationException">The file exists but is not valid TOML.</exception>
    public static TomlTable Read(string path)
    {
        if (!File.Exists(path))
        {
            return new TomlTable();
        }

        try
        {
            // An empty file deserializes to nothing at all, which means the same as a file full of defaults.
            return TomlSerializer.Deserialize<TomlTable>(File.ReadAllText(path)) ?? new TomlTable();
        }
        catch (TomlException exception)
        {
            // Tomlyn's message already carries the line and column, which is the part worth repeating.
            throw new ConfigurationException(path, exception.Message);
        }
    }

    /// <summary>Writes <paramref name="text" /> to <paramref name="path" />, creating its directory if need be.</summary>
    /// <param name="unixMode">
    ///     The mode to create the file with, where the OS has such a thing. Passed for a file holding secrets, so that
    ///     it is never briefly world-readable between being created and being locked down.
    /// </param>
    public static void Write(string path, string text, UnixFileMode? unixMode = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        if (unixMode is not { } mode || OperatingSystem.IsWindows())
        {
            File.WriteAllText(path, text, Utf8);

            return;
        }

        using (var writer = new StreamWriter(
                   path,
                   Utf8,
                   new FileStreamOptions
                   {
                       Mode = FileMode.Create,
                       Access = FileAccess.Write,
                       UnixCreateMode = mode,
                   }))
        {
            writer.Write(text);
        }

        // UnixCreateMode applies only to a file being created, so an existing, laxer file needs saying again.
        File.SetUnixFileMode(path, mode);
    }
}
