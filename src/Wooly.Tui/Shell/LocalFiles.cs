using Wooly.Tui.Screens;

namespace Wooly.Tui.Shell;

/// <summary>
///     What the shell reads off this machine's disk for attaching (#375, #376): a folder as the file browser lists it,
///     and how large a file being attached is. Here rather than on the screens, which hold what they are handed and
///     read nothing themselves (ADR-0015, review of #372).
/// </summary>
/// <remarks>
///     Not a port, for the reason the clipboard is not (ADR-0020): it is this machine, not an instance — and tests reach
///     it through real files in a temporary folder rather than a fake. Read on the drawing thread, as a folder of a few
///     hundred entries is read faster than a frame is drawn.
/// </remarks>
internal static class LocalFiles
{
    /// <summary>
    ///     <paramref name="folder" /> as the file browser lists it: the folder above, then its folders and files by name,
    ///     with nothing hidden. A folder that cannot be read lists only the way back up.
    /// </summary>
    public static FolderListing Listing(string folder)
    {
        var here = new DirectoryInfo(Path.GetFullPath(folder));
        var up = here.Parent is { } parent
            ? new FolderEntry(parent.FullName, "..", Folder: true, 0, parent.LastWriteTime)
            : null;
        var entries = new List<FolderEntry>();

        try
        {
            entries.AddRange(here.EnumerateDirectories()
                                 .Where(inside => !Hidden(inside))
                                 .OrderBy(inside => inside.Name, StringComparer.OrdinalIgnoreCase)
                                 .Select(inside => new FolderEntry(
                                     inside.FullName,
                                     inside.Name,
                                     Folder: true,
                                     0,
                                     inside.LastWriteTime)));

            entries.AddRange(here.EnumerateFiles()
                                 .Where(file => !Hidden(file))
                                 .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
                                 .Select(file => new FolderEntry(
                                     file.FullName,
                                     file.Name,
                                     Folder: false,
                                     file.Length,
                                     file.LastWriteTime)));
        }
        catch (Exception failed) when (failed is UnauthorizedAccessException or IOException)
        {
            // A folder that cannot be read lists only the way back up, and the browser says there is nothing here.
            entries.Clear();
        }

        return new FolderListing(here.FullName, Shown(here.FullName), up, entries);
    }

    /// <summary>
    ///     How large the file at <paramref name="path" /> is, as its row says — nothing, for one gone since it was found,
    ///     whose upload will say so.
    /// </summary>
    public static long Size(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch (Exception failed) when (failed is UnauthorizedAccessException or IOException)
        {
            return 0;
        }
    }

    /// <summary>A file or folder left out of every listing: one named with a leading dot, or marked hidden.</summary>
    private static bool Hidden(FileSystemInfo info) =>
        info.Name.StartsWith('.') || info.Attributes.HasFlag(FileAttributes.Hidden);

    /// <summary>A folder as the author would write it, under <c>~</c> where it is in their home.</summary>
    private static string Shown(string folder)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        return home.Length > 0 && folder.StartsWith(home, StringComparison.Ordinal)
            ? $"~{folder[home.Length..]}"
            : folder;
    }
}
