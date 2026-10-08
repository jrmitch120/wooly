namespace Wooly.Tui.Screens;

/// <summary>
///     A folder on this machine as the file browser lists it (#376): read off the disk by the shell and handed to the
///     screen, which reads nothing itself (ADR-0015, review of #372).
/// </summary>
/// <param name="Path">The folder's whole path.</param>
/// <param name="Shown">The folder as the author would write it — under <c>~</c> where it is in their home.</param>
/// <param name="Up">The folder above it, which <c>..</c> stands for, or none at the very root of the disk.</param>
/// <param name="Entries">
///     Its folders and then its files, each by name, and none of them hidden: a file the instance does not accept is
///     here too, for the browser to leave out until every file is asked for.
/// </param>
public sealed record FolderListing(string Path, string Shown, FolderEntry? Up, IReadOnlyList<FolderEntry> Entries);

/// <summary>One thing in a folder the file browser lists (#376): a folder in it, or a file.</summary>
/// <param name="Path">Its whole path.</param>
/// <param name="Name">Its own name.</param>
/// <param name="Folder">Whether it is a folder.</param>
/// <param name="Bytes">How large it is, for a file.</param>
/// <param name="Changed">When it was last written.</param>
public sealed record FolderEntry(string Path, string Name, bool Folder, long Bytes, DateTime Changed);
