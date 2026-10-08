namespace Wooly.Tui.Clipboard;

/// <summary>
///     Where a picture pasted from the clipboard is kept while it goes up to the instance (#380): a file of its own, as
///     anything attached is, named <c>pasted-N.png</c> — counted over the session, so that each pasted picture's row says
///     something of its own — in a temporary folder of the session's.
/// </summary>
/// <remarks>
///     The folder is made at the first paste, so a session that pastes nothing makes nothing, and taken away with every
///     picture in it when this is let go, as the session ends: nothing a session pasted is left on the disk after it
///     (review of #372). Not before, since a picture is read off the disk for as long as its row is drawn and until its
///     upload is through. A folder the system will not let go of — a file still held open on Windows — is left for the
///     system's own clearing of its temporary files, rather than failing the way out.
/// </remarks>
/// <param name="under">The folder to make the session's folder in: the system's temporary one, where nobody says.</param>
public sealed class PastedPictures(string? under = null) : IDisposable
{
    /// <summary>The session's folder, once a picture has been pasted.</summary>
    private string? _folder;

    /// <summary>How many pictures have been pasted, which names the next.</summary>
    private int _pasted;

    /// <summary>Writes <paramref name="png" /> to a file of its own, and says where.</summary>
    public string Keep(byte[] png)
    {
        _folder ??= Directory.CreateDirectory(
                Path.Combine(under ?? Path.GetTempPath(), $"wooly-pasted-{Guid.NewGuid():N}"))
            .FullName;

        var path = Path.Combine(_folder, $"pasted-{++_pasted}.png");

        File.WriteAllBytes(path, png);

        return path;
    }

    /// <summary>Takes the session's folder away, with every picture pasted into it.</summary>
    public void Dispose()
    {
        if (_folder is null)
        {
            return;
        }

        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (Exception held) when (held is IOException or UnauthorizedAccessException)
        {
            // Left for the system's own clearing of its temporary files.
        }

        _folder = null;
    }
}
