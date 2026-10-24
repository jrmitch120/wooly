namespace Wooly.Tui.Shell;

/// <summary>
///     Where the shell's own preferences are remembered between launches — the <see cref="ShellWidth" /> (#415) and
///     whether the rail is hidden (#442, #445): the <c>[preferences]</c> section of the config file, one for every
///     profile, as both of them are.
/// </summary>
/// <remarks>
///     One port for both rather than one each, since they are saved the same way, to the same place, for the same
///     reason: each is how the reader likes the screen, changed by a key and saved the moment it is.
/// </remarks>
public interface IShellPreferenceStore
{
    /// <summary>Saves <paramref name="width" />, so that the next launch opens at it.</summary>
    void SaveWidth(ShellWidth width);

    /// <summary>
    ///     Saves whether the rail is the hidden rail or the shown one, <paramref name="rail" />, so that the next launch
    ///     opens on it.
    /// </summary>
    void SaveRail(RailState rail);
}
