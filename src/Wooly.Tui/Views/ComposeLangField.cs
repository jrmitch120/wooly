using Terminal.Gui.Input;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Views;

/// <summary>
///     What language a post being written is in (ADR-0024, #340): a one-line field laid over Lang's value column, as the
///     warning field is laid over the warning's, with the list of languages hung under it (<see cref="LanguageList" />).
///     Typing a code or a name opens the list; so does a click, and <c>⏎</c>.
/// </summary>
/// <remarks>
///     While the list is open its keys come first (<see cref="Ahead" />). Otherwise the field's keys are every compose
///     header's (<see cref="ComposeHeaderField" />).
/// </remarks>
internal sealed class ComposeLangField(
    ITheme theme,
    Func<string> hint,
    Action send,
    Action cancel,
    Action warn) : ComposeHeaderField(theme, Role.Body, hint, send, cancel, warn)
{
    /// <summary>First refusal on every key the field is sent: the list's, while it is open.</summary>
    public Func<Key, bool>? Ahead { get; set; }

    /// <summary>What a click on the field or <c>⏎</c> in it does: opens the list.</summary>
    public Action? Asked { get; set; }

    protected override bool TakesFirst(Key key) => Ahead?.Invoke(key) == true;

    protected override void Entered() => Asked?.Invoke();

    /// <summary>
    ///     A click places the caret, as in any field, and opens the list. A right click is <c>esc</c>, which on a draft
    ///     means nothing (#307).
    /// </summary>
    protected override bool OnMouseEvent(Mouse mouse)
    {
        var handled = base.OnMouseEvent(mouse);

        if (ShellKeys.Of(mouse) is null && mouse.Flags.HasFlag(MouseFlags.LeftButtonClicked))
        {
            if (!HasFocus)
            {
                SetFocus();
            }

            Asked?.Invoke();

            return true;
        }

        return handled;
    }
}
