namespace Wooly.Tests.Fakes;

/// <summary>A scratch directory that stands in for a user's config folder, and cleans itself up after the test.</summary>
internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory() => Directory.CreateDirectory(Path);

    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"wooly-tests-{Guid.NewGuid():N}");

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
