namespace Wooly.Tests.Fakes;

/// <summary>
///     A scratch directory on the real file system — a user's config folder, or somewhere their files to attach to a
///     post happen to be — which cleans itself up after the test.
/// </summary>
internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory() => Directory.CreateDirectory(Path);

    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"wooly-tests-{Guid.NewGuid():N}");

    /// <summary>Writes <paramref name="contents" /> to <paramref name="name" /> here, and says where it landed.</summary>
    public string WriteFile(string name, string contents = "pretend this is a picture")
    {
        var path = System.IO.Path.Combine(Path, name);

        File.WriteAllText(path, contents);

        return path;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
            // The test never wrote anything. Nothing to clean up.
        }
    }
}
