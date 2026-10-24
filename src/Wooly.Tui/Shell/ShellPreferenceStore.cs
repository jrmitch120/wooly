using Wooly.Core.Configuration;

namespace Wooly.Tui.Shell;

/// <summary>
///     The shell's preferences saved in the config file's <c>[preferences]</c> — <c>shell_width</c> (#415) and
///     <c>collapsed_rail</c> (#442): the file read, the one value set, and the file written back through the same store
///     everything else in it is written through.
/// </summary>
/// <remarks>
///     Written whole, as making a profile the default writes it, so comments written by hand in the file do not
///     survive a save — a separate concern (#413, out of scope).
/// </remarks>
public sealed class ShellPreferenceStore(IConfigStore config) : IShellPreferenceStore
{
    /// <inheritdoc />
    public void SaveWidth(ShellWidth width) => Saved(preferences => preferences with { ShellWidth = width.Percent });

    /// <inheritdoc />
    public void SaveRail(bool collapsed) => Saved(preferences => preferences with { CollapsedRail = collapsed });

    private void Saved(Func<Preferences, Preferences> set)
    {
        var stored = config.Load();

        config.Save(stored with { Preferences = set(stored.Preferences) });
    }
}
