using Wooly.Core.Errors;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Fakes;

/// <summary>
///     Where the shell's preferences are saved, without a config file: it only remembers each one it was asked to save
///     (#415, #442).
/// </summary>
internal sealed class FakeShellPreferenceStore : IShellPreferenceStore
{
    /// <summary>Every width saved, as its percentage, in order — where a test proves what was saved and how often.</summary>
    public List<int> Widths { get; } = [];

    /// <summary>Every rail saved, <see langword="true" /> where it was the collapsed rail, in order.</summary>
    public List<bool> Rails { get; } = [];

    /// <summary>
    ///     Where the config file has been edited by hand into something that no longer reads, what saving says — or
    ///     <see langword="null" /> where it saves.
    /// </summary>
    public string? Unreadable { get; set; }

    /// <inheritdoc />
    public void SaveWidth(ShellWidth width)
    {
        Refuse();

        Widths.Add(width.Percent);
    }

    /// <inheritdoc />
    public void SaveRail(bool collapsed)
    {
        Refuse();

        Rails.Add(collapsed);
    }

    /// <summary>Throws what saving says where the file no longer reads, as the real store would on loading it.</summary>
    private void Refuse()
    {
        if (Unreadable is { } problem)
        {
            throw new ConfigurationException("/home/jeff/.config/wooly/config.toml", problem);
        }
    }
}
