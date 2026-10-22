using Wooly.Tui.Drafting;
using Wooly.Tui.Screens;

namespace Wooly.Tests.Tui;

/// <summary>
///     A stack of screens that is a list and nothing more, for drafting to be tested without a shell (ADR-0029) — held to
///     the same facts as the shell's own stack by <see cref="StackContract" />.
/// </summary>
/// <param name="bottom">The screen at the bottom, which never leaves.</param>
/// <param name="clearNotice">Lets go of whatever the status row was saying.</param>
internal sealed class ListStack(Screen bottom, Action clearNotice) : IStack
{
    private readonly List<Screen> _screens = [bottom];

    /// <summary>The drafting told as screens leave and are uncovered, once it is built over this.</summary>
    public Wooly.Tui.Drafting.Drafting? Hooked { get; set; }

    /// <inheritdoc />
    public IReadOnlyList<Screen> Screens => _screens;

    /// <inheritdoc />
    public Screen Top => _screens[^1];

    /// <inheritdoc />
    public void Push(Screen screen)
    {
        _screens.Add(screen);
        clearNotice();
    }

    /// <inheritdoc />
    public void TakeOff(Screen screen)
    {
        var at = _screens.IndexOf(screen);

        if (at < 1)
        {
            return;
        }

        while (_screens.Count > at)
        {
            Hooked?.Left(_screens[^1]);
            _screens[^1].Left();
            _screens.RemoveAt(_screens.Count - 1);
        }

        clearNotice();
        Hooked?.Uncovered();
    }
}
