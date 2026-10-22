using Wooly.Core.Errors;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Fakes;

/// <summary>
///     Where the shell width is saved, without a config file: it only remembers each width it was asked to save (#415).
/// </summary>
internal sealed class FakeShellWidthStore : IShellWidthStore
{
    /// <summary>Every width saved, as its percentage, in order — where a test proves what was saved and how often.</summary>
    public List<int> Saved { get; } = [];

    /// <summary>
    ///     Where the config file has been edited by hand into something that no longer reads, what saving says — or
    ///     <see langword="null" /> where it saves.
    /// </summary>
    public string? Unreadable { get; set; }

    /// <inheritdoc />
    public void Save(ShellWidth width)
    {
        if (Unreadable is { } problem)
        {
            throw new ConfigurationException("/home/jeff/.config/wooly/config.toml", problem);
        }

        Saved.Add(width.Percent);
    }
}
