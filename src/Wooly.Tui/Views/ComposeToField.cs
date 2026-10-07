using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Views;

/// <summary>
///     Who a post being written goes to (ADR-0024, #338): a row of radio buttons laid over To's value column, the way
///     the warning field is laid over the warning's. It is there to take the typing and the mouse — what it shows and
///     what a key or a click on it means are the compose screen's, asked through the shell.
/// </summary>
/// <remarks>
///     It leaves every key it does not mean to swallow to the window and so to <c>Keymap</c>: the arrows, which walk the
///     fields and choose along the row, <c>tab</c>, <c>esc</c> and the <c>ctrl</c> chords. A letter or a digit is
///     nobody's here — read by the keymap it would be a boost or a compose behind a draft.
/// </remarks>
/// <param name="theme">What every colour on the row comes from.</param>
/// <param name="spans">The row as the screen draws it, this many columns wide.</param>
/// <param name="clicked">A click this many columns into a row this wide.</param>
internal sealed class ComposeToField(
    ITheme theme,
    Func<int, IReadOnlyList<Span>> spans,
    Action<int, int> clicked) : View
{
    /// <summary>The row, span by span, each in its own role.</summary>
    protected override bool OnDrawingContent(DrawContext? context)
    {
        var column = 0;

        SetAttribute(theme.For(Role.Body));
        AddStr(0, 0, new string(' ', Math.Max(0, Viewport.Width)));

        foreach (var span in spans(Viewport.Width))
        {
            SetAttribute(theme.For(span.Role));
            AddStr(column, 0, span.Text);
            column += span.Width;
        }

        return true;
    }

    /// <summary>
    ///     First refusal on every key, ahead of everything else here: a question the shell has open on the status row,
    ///     which the key answers instead of doing anything in the field (#373). A key it takes the field never sees.
    /// </summary>
    public Func<Key, bool>? Answering { get; set; }

    protected override bool OnKeyDown(Key key) =>
        Answering?.Invoke(key) == true
        || !(key == Key.CursorLeft || key == Key.CursorRight || key == Key.CursorUp || key == Key.CursorDown
          || key == Key.Tab || key == Key.Tab.WithShift || key == Key.Esc || key.IsCtrl || key.IsAlt);

    /// <summary>
    ///     A click on the row is the screen's to answer. Everything else the pointer does here is nothing: a right click,
    ///     as on the other fields (#307), and the wheel, which bubbling to the window would read as the arrows that walk
    ///     the fields.
    /// </summary>
    protected override bool OnMouseEvent(Mouse mouse)
    {
        if (ShellKeys.Of(mouse) is null && mouse.Flags.HasFlag(MouseFlags.LeftButtonClicked))
        {
            clicked(mouse.ScreenPosition.X - FrameToScreen().X, Viewport.Width);
        }

        return true;
    }
}
