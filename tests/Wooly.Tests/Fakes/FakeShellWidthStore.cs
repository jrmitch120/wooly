using Wooly.Tui.Shell;

namespace Wooly.Tests.Fakes;

/// <summary>
///     Where the shell width is saved, without a config file: it only remembers each width it was asked to save (#415).
/// </summary>
internal sealed class FakeShellWidthStore : IShellWidthStore
{
    /// <summary>Every width saved, as its percentage, in order — where a test proves what was saved and how often.</summary>
    public List<int> Saved { get; } = [];

    /// <inheritdoc />
    public void Save(ShellWidth width) => Saved.Add(width.Percent);
}
