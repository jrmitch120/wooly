namespace Wooly.Tui.Shell;

/// <summary>
///     Where the <see cref="ShellWidth" /> is remembered between launches: the <c>[preferences]</c> section of the
///     config file, one for every profile as the shell width is (#415).
/// </summary>
public interface IShellWidthStore
{
    /// <summary>Saves <paramref name="width" />, so that the next launch opens at it.</summary>
    void Save(ShellWidth width);
}
