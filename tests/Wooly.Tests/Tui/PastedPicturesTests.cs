using Wooly.Tests.Fakes;
using Wooly.Tui.Clipboard;

namespace Wooly.Tests.Tui;

/// <summary>
///     Where a picture pasted from the clipboard is kept while it goes up (#380): a temporary folder of the session's,
///     made at the first paste and taken away again as the session ends, so that nothing is left behind on the disk
///     (review of #372).
/// </summary>
public class PastedPicturesTests : IDisposable
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47];

    private readonly TemporaryDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    /// <summary>Each picture is kept as <c>pasted-N.png</c>, counted over the session, holding what was pasted.</summary>
    [Fact]
    public void EachPictureIsKeptUnderItsTurn()
    {
        using var pasted = new PastedPictures(_temp.Path);

        var first = pasted.Keep(Png);
        var second = pasted.Keep(Png);

        Assert.Equal("pasted-1.png", Path.GetFileName(first));
        Assert.Equal("pasted-2.png", Path.GetFileName(second));
        Assert.Equal(Png, File.ReadAllBytes(second));
    }

    /// <summary>Let go, it takes its folder away with every picture in it.</summary>
    [Fact]
    public void LettingGoTakesTheFolderAway()
    {
        var pasted = new PastedPictures(_temp.Path);
        var kept = pasted.Keep(Png);

        pasted.Dispose();

        Assert.False(File.Exists(kept));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_temp.Path));
    }

    /// <summary>A session that pasted nothing makes no folder at all.</summary>
    [Fact]
    public void NothingPastedMakesNothing()
    {
        new PastedPictures(_temp.Path).Dispose();

        Assert.Empty(Directory.EnumerateFileSystemEntries(_temp.Path));
    }
}
