using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace Wooly.Tui.Views;

/// <summary>
///     One of the edit public profile form's three toggles (#406), laid over its row to take the walk and the mouse. It
///     draws nothing — the box and the words are the form's row, showing through — and a click on it flips it.
/// </summary>
/// <remarks>
///     It leaves <c>space</c> and <c>⏎</c> to the form's view and so to the keymap, which flips the toggle for either,
///     and the arrows, <c>tab</c>, <c>esc</c> and the <c>ctrl</c> chords to whoever wants them. Any other letter is
///     nobody's here — read by the keymap it would be a key of the screen under the form.
/// </remarks>
/// <param name="flip">A click on it: it the other way, and the walk on it.</param>
internal sealed class PublicProfileToggle : View
{
    private readonly Action _flip;

    public PublicProfileToggle(Action flip)
    {
        _flip = flip;

        CanFocus = true;

        // Nothing of its own to draw: the form's row shows through.
        ViewportSettings |= ViewportSettingsFlags.Transparent;
    }

    /// <summary>
    ///     First refusal on every key, ahead of everything else here: a question the shell has open on the status row,
    ///     which the key answers instead (#373).
    /// </summary>
    public Func<Key, bool>? Answering { get; set; }

    protected override bool OnKeyDown(Key key) =>
        Answering?.Invoke(key) == true
        || !(key == Key.Space || key == Key.Enter
          || key == Key.CursorUp || key == Key.CursorDown || key == Key.CursorLeft || key == Key.CursorRight
          || key == Key.Tab || key == Key.Tab.WithShift || key == Key.Esc || key.IsCtrl || key.IsAlt);

    /// <summary>
    ///     A click flips it. Everything else the pointer does here is nothing: a right click, which the form's view has
    ///     already taken for <c>esc</c>, and the wheel, which bubbling to the window would read as the arrows.
    /// </summary>
    protected override bool OnMouseEvent(Mouse mouse)
    {
        if (ShellKeys.Of(mouse) is null && mouse.Flags.HasFlag(MouseFlags.LeftButtonClicked))
        {
            _flip();
        }

        return true;
    }
}
