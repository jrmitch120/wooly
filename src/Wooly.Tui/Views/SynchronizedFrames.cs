using Terminal.Gui.App;

namespace Wooly.Tui.Views;

/// <summary>
///     Each frame the shell draws, wrapped in synchronized output — DEC mode 2026, <c>CSI ?2026h</c> before it and
///     <c>CSI ?2026l</c> after — so that a terminal honouring it shows the frame whole, rather than whatever part of it
///     has arrived when it next paints (#342, ADR-0023).
/// </summary>
/// <remarks>
///     On a sixel terminal a frame is the page's text and a sixel for every picture on it, a few hundred kilobytes, and
///     a terminal painting part way through it showed an avatar a moment before the rule above it. A terminal that does
///     not know the mode ignores both sequences.
///     <para>
///         Terminal.Gui writes a frame itself and says nothing either side of it, so the block is opened and closed
///         around it from what is certain. It is opened as a region starts drawing: a frame cannot reach the terminal
///         before it has been drawn. It is closed when the application says its layout and draw are complete, which
///         is after the frame is written; were that ever to change, the frame would go out unsynchronized, as it did
///         before, and nothing worse. Failing that it is closed when the next pass of the main loop begins, which is
///         documented to come before anything else that pass does, so a block is never left open.
///     </para>
/// </remarks>
/// <param name="write">Where the two sequences are written: the terminal, as the frame is.</param>
internal sealed class SynchronizedFrames(Action<string> write)
{
    public const string Begin = "\u001b[?2026h";
    public const string End = "\u001b[?2026l";

    private bool _open;

    /// <summary>Closes each frame once <paramref name="application" /> has written it.</summary>
    public void Over(IApplication application)
    {
        application.LayoutAndDrawComplete += (_, _) => Close();
        application.Iteration += (_, _) => Close();
    }

    /// <summary>Opens a block for the frame being drawn, if one is not open already.</summary>
    public void Open()
    {
        if (_open)
        {
            return;
        }

        _open = true;
        write(Begin);
    }

    /// <summary>Closes the block the frame was drawn in, if one is open.</summary>
    public void Close()
    {
        if (!_open)
        {
            return;
        }

        _open = false;
        write(End);
    }
}
