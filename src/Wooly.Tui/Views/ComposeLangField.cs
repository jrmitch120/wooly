using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Wooly.Tui.Views;

/// <summary>
///     What language a post being written is in (ADR-0024, #340): a one-line <see cref="TextField" /> laid over Lang's
///     value column, as the warning field is laid over the warning's, with the list of languages hung under it
///     (<see cref="LanguageList" />). Typing a code or a name opens the list; so does a click, and <c>⏎</c>.
/// </summary>
/// <remarks>
///     While the list is open its keys come first (<see cref="Ahead" />). Otherwise <c>esc</c>, <c>ctrl-s</c> and
///     <c>ctrl-w</c> are what they are in the other fields, and the arrows and <c>tab</c> bubble to the window — which is
///     how Lang joins the walk between the fields with no key rule of its own (#337).
/// </remarks>
/// <param name="theme">What every colour the field is asked for comes from.</param>
/// <param name="hint">What the field says, dimly, while it is empty — the screen's to say.</param>
/// <param name="send"><c>ctrl-s</c>.</param>
/// <param name="cancel"><c>esc</c> with the list closed: the draft thrown away, as everywhere in compose.</param>
/// <param name="warn"><c>ctrl-w</c>: the typing into the warning.</param>
internal sealed class ComposeLangField(
    ITheme theme,
    Func<string> hint,
    Action send,
    Action cancel,
    Action warn) : TextField
{
    /// <summary>First refusal on every key the field is sent: the list's, while it is open.</summary>
    public Func<Key, bool>? Ahead { get; set; }

    /// <summary>What a click on the field or <c>⏎</c> in it does: opens the list.</summary>
    public Action? Asked { get; set; }

    /// <summary>Text in the body's role and a selection in <see cref="Role.SelectedText" />, as in the other fields.</summary>
    protected override bool OnGettingAttributeForRole(in VisualRole role, ref Attribute currentAttribute)
    {
        currentAttribute = role == VisualRole.Active ? theme.For(Role.SelectedText) : theme.For(Role.Body);

        return true;
    }

    /// <summary>The hint over an empty field, in <see cref="Role.Muted" />.</summary>
    protected override bool OnDrawingContent(DrawContext? context)
    {
        var drawn = base.OnDrawingContent(context);

        if (Text.Length == 0 && Viewport.Width > 0)
        {
            SetAttribute(theme.For(Role.Muted));
            AddStr(0, 0, TextWrap.Clip(hint(), Viewport.Width));
        }

        return drawn;
    }

    protected override bool OnKeyDown(Key key)
    {
        if (Ahead?.Invoke(key) == true)
        {
            return true;
        }

        if (key == Key.Esc)
        {
            cancel();

            return true;
        }

        if (key == Key.S.WithCtrl)
        {
            send();

            return true;
        }

        if (key == Key.W.WithCtrl)
        {
            warn();

            return true;
        }

        if (key == Key.Enter)
        {
            Asked?.Invoke();

            return true;
        }

        return base.OnKeyDown(key);
    }

    /// <summary>
    ///     A click places the caret, as in any field, and opens the list. A right click is <c>esc</c>, which on a draft
    ///     means nothing (#307).
    /// </summary>
    protected override bool OnMouseEvent(Mouse mouse)
    {
        if (ShellKeys.Of(mouse) is not null)
        {
            return true;
        }

        var handled = base.OnMouseEvent(mouse);

        if (mouse.Flags.HasFlag(MouseFlags.LeftButtonClicked))
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
