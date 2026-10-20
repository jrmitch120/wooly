using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace Wooly.Tui.Views;

/// <summary>
///     A row of the edit public profile form that takes no typing, laid over it to take the walk and the mouse: one of
///     the three toggles (#406), which a click flips, or the avatar's or the header's, which a click opens the file
///     browser for (#407). It draws nothing — the box, the picture and the words are the form's row, showing through.
/// </summary>
/// <remarks>
///     It leaves <c>space</c> and <c>⏎</c> to the form's view and so to the keymap, which says what either means on the
///     row, and the arrows, <c>tab</c>, <c>esc</c> and the <c>ctrl</c> and <c>alt</c> chords to whoever wants them. Any
///     other letter is nobody's here — read by the keymap it would be a key of the screen under the form.
/// </remarks>
/// <param name="pressed">A click on it: what the row does, and the walk on it.</param>
internal sealed class PublicProfileButton : View
{
    private readonly Action _pressed;

    public PublicProfileButton(Action pressed)
    {
        _pressed = pressed;

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
    ///     A click presses it. Everything else the pointer does here is nothing: a right click, which the form's view has
    ///     already taken for <c>esc</c>, and the wheel, which bubbling to the window would read as the arrows.
    /// </summary>
    protected override bool OnMouseEvent(Mouse mouse)
    {
        if (ShellKeys.Of(mouse) is null && mouse.Flags.HasFlag(MouseFlags.LeftButtonClicked))
        {
            _pressed();
        }

        return true;
    }
}
