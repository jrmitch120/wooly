using Wooly.Core.Configuration;

namespace Wooly.Tui.Shell;

/// <summary>
///     The shell width saved as <c>shell_width</c> in the config file's <c>[preferences]</c> (#415): the file read, the
///     one value set, and the file written back through the same store everything else in it is written through.
/// </summary>
/// <remarks>
///     Written whole, as making a profile the default writes it, so comments written by hand in the file do not
///     survive a save — a separate concern (#413, out of scope).
/// </remarks>
public sealed class ShellWidthStore(IConfigStore config) : IShellWidthStore
{
    /// <inheritdoc />
    public void Save(ShellWidth width)
    {
        var stored = config.Load();

        config.Save(stored with { Preferences = stored.Preferences with { ShellWidth = width.Percent } });
    }
}
