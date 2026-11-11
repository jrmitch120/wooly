using System.Drawing;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Wooly.Tui.Views;

/// <summary>
///     A one-line <see cref="TextField" /> laid over a screen's row (ADR-0030) — a compose header's (#320), a field of the
///     public profile form's (#406), the search field (#608) — and what every one of them looks like and answers to
///     alike: its colours, the hint over it while it is empty, and an open question's first refusal on every key.
///     Selecting, moving by word, copying, cutting and pasting are the <see cref="TextField" />'s own.
/// </summary>
/// <param name="theme">What every colour the field is asked for comes from, as for the editor (#316).</param>
/// <param name="text">The role what is typed is drawn in.</param>
/// <param name="hint">
///     What the field says, dimly, while it is empty — the screen's to say, since which hint it is can depend on whether
///     the typing is here.
/// </param>
internal abstract class OneLineField(ITheme theme, Role text, Func<string> hint) : TextField
{
    /// <summary>
    ///     First refusal on every key, ahead of everything else here: a question the shell has open on the status row,
    ///     which the key answers instead of doing anything in the field (#373). A key it takes the field never sees.
    /// </summary>
    public Func<Key, bool>? Answering { get; set; }

    /// <summary>
    ///     The role what is typed is drawn in: the field's own, unless a kind of field draws it otherwise while it is not
    ///     taking letters — the search field, dimmed while a trend under it is picked (#605).
    /// </summary>
    protected virtual Role TextRole => text;

    /// <summary>
    ///     Every visual role Terminal.Gui asks for, answered from the theme: text in the field's own role, and a
    ///     selection — the <c>Active</c> role, as it is in the editor — in <see cref="Role.SelectedText" />, so every
    ///     field selects alike.
    /// </summary>
    protected override bool OnGettingAttributeForRole(in VisualRole role, ref Attribute currentAttribute)
    {
        currentAttribute = role == VisualRole.Active ? theme.For(Role.SelectedText) : theme.For(TextRole);

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
    ///     An open question's first refusal on <paramref name="key" />, and then the field's own — what each kind of field
    ///     takes ahead of the <see cref="TextField" />, and <c>ctrl-u</c>, which every one of them takes alike.
    /// </summary>
    protected override bool OnKeyDown(Key key) =>
        Answering?.Invoke(key) == true || Takes(key) || Emptied(key) || base.OnKeyDown(key);

    /// <summary>
    ///     <c>ctrl-u</c>: the field emptied, wherever the caret was, and left taking letters — the one key that empties
    ///     every one-line prompt in the shell, the ones the shell types into itself as well as these (#310).
    /// </summary>
    /// <remarks>
    ///     The field's own rather than a binding of <see cref="TextField" />'s, which has none for it: Terminal.Gui
    ///     empties a field with <c>ctrl-shift-delete</c>, a key few terminals can send. What it does is
    ///     <see cref="TextField.DeleteAll" />, the field's own emptying, so <c>ctrl-z</c> brings the text back.
    /// </remarks>
    private bool Emptied(Key key)
    {
        if (key != Key.U.WithCtrl)
        {
            return false;
        }

        if (Text.Length > 0)
        {
            DeleteAll();
        }

        return true;
    }

    /// <summary>The keys this kind of field takes ahead of the <see cref="TextField" />, once no question has.</summary>
    protected abstract bool Takes(Key key);

    /// <summary>
    ///     Where the left button went down in the field, until the pointer first moves with it held: where a drag's
    ///     selection begins.
    /// </summary>
    private Point? _pressedAt;

    /// <summary>
    ///     The mouse as <see cref="TextField" /> takes it, but for where a drag's selection begins: at the cell the button
    ///     went down on (#608).
    /// </summary>
    /// <remarks>
    ///     <see cref="TextField" /> anchors a selection at the first report of the pointer moving with the button held,
    ///     and a terminal reports that only once the pointer has reached the next cell — so a drag from the start of a
    ///     word selected all of it but its first letter. The press is put to it once more as that first move, so the
    ///     anchor lands where the press did.
    /// </remarks>
    protected override bool OnMouseEvent(Mouse mouse)
    {
        if (mouse.Flags == MouseFlags.LeftButtonPressed)
        {
            _pressedAt = mouse.Position;
        }
        else if (mouse.Flags == (MouseFlags.LeftButtonPressed | MouseFlags.PositionReport) && _pressedAt is { } at)
        {
            _pressedAt = null;
            base.OnMouseEvent(new Mouse { Flags = mouse.Flags, Position = at, ScreenPosition = mouse.ScreenPosition });
        }
        else
        {
            _pressedAt = null;
        }

        return base.OnMouseEvent(mouse);
    }
}
