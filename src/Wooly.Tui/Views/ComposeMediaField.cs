using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace Wooly.Tui.Views;

/// <summary>
///     The Media header's value (#379), laid over it the way To's row of buttons is laid over To's: there to take the
///     typing as the walk passes through, and the mouse. It draws nothing of its own — the header is the screen's row,
///     painted behind, lit where the typing is on it — and what a key or a click on it means is the screen's, asked
///     through the shell.
/// </summary>
/// <remarks>
///     It leaves to the window every key it does not mean to swallow, and so to <c>Keymap</c>: the arrows that walk the
///     fields, <c>tab</c>, <c>esc</c>, the <c>ctrl</c> chords, and <c>s</c>, the sensitive toggle. Any other letter is
///     nobody's here — read by the keymap it would be a boost or a compose behind a draft.
/// </remarks>
internal sealed class ComposeMediaField : View
{
    private readonly Action<int> _clicked;

    /// <param name="clicked">A click this many columns into the header's value.</param>
    public ComposeMediaField(Action<int> clicked)
    {
        _clicked = clicked;

        CanFocus = true;
        ViewportSettings |= ViewportSettingsFlags.Transparent;
    }

    /// <summary>
    ///     First refusal on every key, ahead of everything else here: a question the shell has open on the status row,
    ///     which the key answers instead (#373).
    /// </summary>
    public Func<Key, bool>? Answering { get; set; }

    protected override bool OnKeyDown(Key key) =>
        Answering?.Invoke(key) == true
        || !(key == Key.CursorLeft || key == Key.CursorRight || key == Key.CursorUp || key == Key.CursorDown
          || key == Key.Tab || key == Key.Tab.WithShift || key == Key.Esc || key == Key.S || key.IsCtrl
          || key.IsAlt);

    /// <summary>
    ///     A click on the header is the screen's to answer, as a click on To is. Everything else the pointer does here is
    ///     nothing, as on the other fields: a right click (#307), and the wheel, which bubbling to the window would read
    ///     as the arrows that walk the fields.
    /// </summary>
    protected override bool OnMouseEvent(Mouse mouse)
    {
        if (ShellKeys.Of(mouse) is null && mouse.Flags.HasFlag(MouseFlags.LeftButtonClicked))
        {
            if (!HasFocus)
            {
                SetFocus();
            }

            _clicked(mouse.ScreenPosition.X - FrameToScreen().X);
        }

        return true;
    }
}
