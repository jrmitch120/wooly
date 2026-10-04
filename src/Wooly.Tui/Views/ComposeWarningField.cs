using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Wooly.Tui.Views;

/// <summary>
///     The content warning over a post being written (#320): a one-line <see cref="TextField" /> laid over the warning
///     header's value column, the way <see cref="ComposeEditor" /> is laid over the body — so the warning selects, moves
///     by word, takes a paste and takes the mouse exactly as the post does. The same three keys are taken off it that
///     are taken off the editor, and <c>enter</c> besides.
/// </summary>
/// <remarks>
///     Its keys are its own before they bubble, which is how <c>?</c> and <c>c</c> are letters here without a rule of
///     the shell's: a focused view consumes what it handles, and the window only ever sees what was left.
/// </remarks>
/// <param name="theme">What every colour the field is asked for comes from, as for the editor (#316).</param>
/// <param name="hint">
///     What the field says, dimly, while it is empty — the screen's to say, since which hint it is depends on whether
///     the typing is here.
/// </param>
/// <param name="warn">
///     <c>ctrl-w</c> and <c>enter</c>: what hands the typing back to the post. <c>enter</c> because a warning is one
///     line, and finishing a one-line field is finishing it.
/// </param>
internal sealed class ComposeWarningField(
    ITheme theme,
    Func<string> hint,
    Action send,
    Action cancel,
    Action warn) : TextField
{
    /// <summary>
    ///     Every visual role Terminal.Gui asks for, answered from the theme: text in <see cref="Role.ContentWarning" />,
    ///     the role a warned post's own warning is drawn in, so a warning being written looks like the warning it will
    ///     become — and a selection, which is the <c>Active</c> role as it is in the editor, in
    ///     <see cref="Role.SelectedText" />, so both fields select alike.
    /// </summary>
    protected override bool OnGettingAttributeForRole(in VisualRole role, ref Attribute currentAttribute)
    {
        currentAttribute = role == VisualRole.Active
            ? theme.For(Role.SelectedText)
            : theme.For(Role.ContentWarning);

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

    protected override bool OnKeyDown(Key key)
    {
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

        if (key == Key.W.WithCtrl || key == Key.Enter)
        {
            warn();

            return true;
        }

        return base.OnKeyDown(key);
    }

    /// <summary>A right click is <c>esc</c>, which on a draft means nothing (#307), and opens no context menu either.</summary>
    protected override bool OnMouseEvent(Mouse mouse) => ShellKeys.Of(mouse) is not null || base.OnMouseEvent(mouse);
}
