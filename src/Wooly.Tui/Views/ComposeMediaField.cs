using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace Wooly.Tui.Views;

/// <summary>
///     The Media header and the pending attachments' rows under it (#377), as something to take the walk and the mouse:
///     laid over the block the compose screen paints, drawing nothing of its own — the header and the rows, the
///     selection bar among them, are the screen's rows showing through. What a key or a click here means is the
///     screen's, asked through the shell.
/// </summary>
/// <remarks>
///     Nothing is typed here, so it leaves the keys that mean something on the block to the window and so to
///     <c>Keymap</c> — the arrows, <c>enter</c>, <c>tab</c>, <c>esc</c> and the <c>ctrl</c> chords — and swallows the
///     rest, as To's row does: a letter read by the keymap would be a boost or a compose behind a draft.
/// </remarks>
/// <param name="clicked">
///     A click this many rows into the block and this many columns across one this wide — twice, for a double click.
/// </param>
internal sealed class ComposeMediaField(Action<int, int, int, bool> clicked) : View
{
    /// <summary>
    ///     First refusal on every key, ahead of everything else here: a question the shell has open on the status row,
    ///     which the key answers instead (#373). A key it takes the field never sees.
    /// </summary>
    public Func<Key, bool>? Answering { get; set; }

    protected override bool OnKeyDown(Key key) =>
        Answering?.Invoke(key) == true
        || !(key == Key.CursorLeft || key == Key.CursorRight || key == Key.CursorUp || key == Key.CursorDown
          || key == Key.Enter || key == Key.Tab || key == Key.Tab.WithShift || key == Key.Esc || key.IsCtrl
          || key.IsAlt);

    /// <summary>
    ///     A click or a double click on the block is the screen's to answer. Everything else the pointer does here is
    ///     nothing, as on To's row: the wheel bubbling to the window would read as the arrows that walk the fields.
    /// </summary>
    protected override bool OnMouseEvent(Mouse mouse)
    {
        var twice = mouse.Flags.HasFlag(MouseFlags.LeftButtonDoubleClicked);

        if (twice || mouse.Flags.HasFlag(MouseFlags.LeftButtonClicked))
        {
            var at = FrameToScreen();

            clicked(mouse.ScreenPosition.Y - at.Y, mouse.ScreenPosition.X - at.X, Viewport.Width, twice);
        }

        return true;
    }
}
