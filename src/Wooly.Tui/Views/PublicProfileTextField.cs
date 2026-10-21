using System.Globalization;
using Terminal.Gui.Input;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Views;

/// <summary>
///     A one-line field on the edit public profile form (#406) — the display name, and each custom field's label and
///     value — laid over the row the form keeps for it, with the colours, the hint and the keys a compose header's
///     field has. <c>ctrl-w</c> means nothing here, there being no warning, and <c>⏎</c> finishes the field and walks
///     on to the next, as a form's does.
/// </summary>
/// <param name="theme">What every colour the field is asked for comes from.</param>
/// <param name="hint">What the field says, dimly, while it is empty.</param>
/// <param name="send"><c>ctrl-s</c>: the form saved.</param>
/// <param name="cancel"><c>esc</c>: the form left, asking first where it is touched.</param>
/// <param name="entered"><c>⏎</c>: the typing on to the next field.</param>
/// <param name="left">
///     <c>←</c> with the caret at the start: the typing across to the field beside this one on the left — a custom
///     field's value to its label — or nothing, where there is none and the key goes on to the field as ever.
/// </param>
/// <param name="right"><c>→</c> with the caret at the end: across to the right, a label to its value.</param>
internal sealed class PublicProfileTextField(
    ITheme theme,
    string hint,
    Action send,
    Action cancel,
    Action entered,
    Action? left = null,
    Action? right = null) : ComposeHeaderField(theme, Role.Body, () => hint, send, cancel, () => { })
{
    protected override void Entered() => entered();

    protected override bool TakesFirst(Key key)
    {
        var across = SelectedLength > 0 ? null
            : key == Key.CursorLeft && InsertionPoint == 0 ? left
            : key == Key.CursorRight && InsertionPoint >= new StringInfo(Text).LengthInTextElements ? right
            : null;

        across?.Invoke();

        return across is not null;
    }
}
