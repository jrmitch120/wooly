using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Wooly.Tui.Views;

/// <summary>
///     A one-line <see cref="TextField" /> laid over a compose header's value column — the warning's (#320), Lang's
///     (#340) — and what every such field shares: its colours, the hint over it while it is empty, and the keys compose
///     means the same thing by in every field.
/// </summary>
/// <remarks>
///     Its keys are its own before they bubble, which is how <c>?</c> and <c>c</c> are letters here without a rule of
///     the shell's: a focused view consumes what it handles, and the window only ever sees what was left — the arrows
///     and <c>tab</c> among them, which is how a header joins the walk between the fields with no key rule of its own
///     (#337).
/// </remarks>
/// <param name="theme">What every colour the field is asked for comes from, as for the editor (#316).</param>
/// <param name="text">The role what is typed is drawn in.</param>
/// <param name="hint">
///     What the field says, dimly, while it is empty — the screen's to say, since which hint it is depends on whether
///     the typing is here.
/// </param>
/// <param name="send"><c>ctrl-s</c>.</param>
/// <param name="cancel"><c>esc</c>: the draft thrown away, as everywhere in compose.</param>
/// <param name="warn"><c>ctrl-w</c>: the typing into the warning, or back out of it.</param>
internal abstract class ComposeHeaderField(
    ITheme theme,
    Role text,
    Func<string> hint,
    Action send,
    Action cancel,
    Action warn) : TextField
{
    /// <summary>
    ///     Every visual role Terminal.Gui asks for, answered from the theme: text in the field's own role, and a
    ///     selection — the <c>Active</c> role, as it is in the editor — in <see cref="Role.SelectedText" />, so every
    ///     field selects alike.
    /// </summary>
    protected override bool OnGettingAttributeForRole(in VisualRole role, ref Attribute currentAttribute)
    {
        currentAttribute = role == VisualRole.Active ? theme.For(Role.SelectedText) : theme.For(text);

        return true;
    }

    /// <summary>The hint over an empty field, in <see cref="Role.Muted" />, drawn only while there is nothing written.</summary>
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

    /// <summary>
    ///     First refusal on every key, ahead of everything else here: a question the shell has open on the status row,
    ///     which the key answers instead of doing anything in the field (#373). A key it takes the field never sees.
    /// </summary>
    public Func<Key, bool>? Answering { get; set; }

    /// <summary>
    ///     Asked of every key whether it is compose's paste from the clipboard — <c>ctrl-v</c>, or <c>alt-v</c> where
    ///     the terminal keeps <c>ctrl-v</c> — and if so, whether the clipboard held a picture or files it attached
    ///     (#380): <see langword="null" /> where the key is no paste, and otherwise the field's own paste follows
    ///     only where nothing was attached.
    /// </summary>
    public Func<Key, bool?>? FromTheClipboard { get; set; }

    /// <summary>
    ///     First refusal on every key, ahead of compose's own — nothing, unless a field has something hung under it whose
    ///     keys come first.
    /// </summary>
    protected virtual bool TakesFirst(Key key) => false;

    /// <summary><c>enter</c>: finishing a one-line field, which means something different in each.</summary>
    protected abstract void Entered();

    /// <summary><c>ctrl-w</c>'s meaning, for a field whose <c>enter</c> means it too.</summary>
    protected void Warn() => warn();

    protected override bool OnKeyDown(Key key)
    {
        if (Answering?.Invoke(key) == true || TakesFirst(key))
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
            Warn();

            return true;
        }

        // Pasted by hand rather than left to the key, since alt-v is no paste of TextField's own.
        if (FromTheClipboard?.Invoke(key) is { } attached)
        {
            if (!attached)
            {
                Paste();
            }

            return true;
        }

        if (key == Key.Enter)
        {
            Entered();

            return true;
        }

        return base.OnKeyDown(key);
    }

    /// <summary>
    ///     A right click is <c>esc</c>, which on a draft means nothing (#307), and opens no context menu either.
    /// </summary>
    protected override bool OnMouseEvent(Mouse mouse) => ShellKeys.Of(mouse) is not null || base.OnMouseEvent(mouse);
}
