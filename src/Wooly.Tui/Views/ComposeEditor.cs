using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Wooly.Tui.Views;

// Terminal.Gui 2.4 marks TextView superseded by a separate Editor package. That package is a new NuGet dependency, and
// this project takes none without an ADR saying why (#28's conventions) — so the shipped, still-working editor is used
// and the warning is turned off here, at the one place that touches it, rather than for the assembly.
#pragma warning disable CS0618

/// <summary>
///     The editor a post is written in. A <see cref="TextView" /> with three keys taken off it: the three that are the
///     shell's rather than the editor's.
/// </summary>
/// <remarks>
///     Taken off by overriding the hook that runs before the editor sees a key, because a focused view consumes what
///     it handles and an ancestor's handler only ever sees what was left. That is why <c>esc</c> reaches the shell
///     from inside an editor at all.
/// </remarks>
/// <param name="theme">
///     What every colour the editor is asked for comes from (#316), so that composing is drawn in the theme's roles
///     rather than Terminal.Gui's own saturated default.
/// </param>
/// <param name="placeholder">What an empty editor says, dimly, where the first letter will go.</param>
/// <param name="warn">
///     <c>ctrl-w</c>: what moves the typing to the content warning over this post (#123). Taken off here for the same
///     reason the other two are — a <see cref="TextView" /> has its own uses for a control key, and the field above is
///     not one of them.
/// </param>
/// <param name="up">
///     <c>↑</c> on the post's first line: what moves the typing up into the headers (ADR-0024, #337). Taken off here
///     because the editor would otherwise spend the press putting the caret at the start of the line.
/// </param>
/// <param name="down">
///     <c>↓</c> on the last line, where there is a field below to move the typing down into — the edit public profile
///     form's bio (#406) — and nothing where there is none, as on compose, whose post is the bottom of its walk.
/// </param>
internal sealed class ComposeEditor(
    ITheme theme,
    string placeholder,
    Action send,
    Action cancel,
    Action warn,
    Action up,
    Action? down = null) : TextView
{
    /// <summary>
    ///     First refusal on every key, ahead of the editor's own — the list of people to mention, while it is open
    ///     (#318). A key it takes the editor never sees.
    /// </summary>
    public Func<Key, bool>? Ahead { get; set; }

    /// <summary>
    ///     First refusal on every key, ahead of everything else here: a question the shell has open on the status row,
    ///     which the key answers instead of doing anything in the field (#373). A key it takes the field never sees.
    /// </summary>
    public Func<Key, bool>? Answering { get; set; }

    /// <summary>
    ///     Asked of every key whether it is compose's paste from the clipboard — <c>ctrl-v</c>, or <c>alt-v</c> where
    ///     the terminal keeps <c>ctrl-v</c> — and if so, whether the clipboard held a picture or files it attached
    ///     (#380): <see langword="null" /> where the key is no paste, and otherwise the editor's own paste follows
    ///     only where nothing was attached.
    /// </summary>
    public Func<Key, bool?>? FromTheClipboard { get; set; }

    /// <summary>
    ///     Whether the hashtags, mentions and addresses written here are painted in their roles as they are typed
    ///     (#571) — by the rule a post's text is read by, <see cref="BodyText.References" />, so what is written looks
    ///     the way it will read once sent. Colour only: no character is drawn that was not typed, and nothing here is
    ///     walked, picked or opened, so caret, selection, wrap and undo are what they were. On for what is read as a
    ///     post's text once sent — compose's body — and off, as it is unless set, for what is never walked, such as an
    ///     attachment's description.
    /// </summary>
    public bool PaintsReferences { get; init; }

    /// <summary>
    ///     The role of each letter drawn, a row of the editor at a time and a letter of the row at a time, where it is
    ///     in a reference — <see langword="null" /> where it is not — as found when the editor last drew.
    /// </summary>
    private Role?[][] _painted = [];

    /// <summary>
    ///     Every visual role Terminal.Gui asks for, answered from the theme: text in <see cref="Role.Body" /> on the
    ///     page, and a selection — which <see cref="TextView" /> draws in its <c>Active</c> role, as the compose
    ///     prototype found (#313) — in <see cref="Role.SelectedText" />. Nothing is left to Terminal.Gui's own scheme,
    ///     so no cell of the editor is a colour the theme cannot change.
    /// </summary>
    /// <remarks>
    ///     <c>Active</c> alone. <c>Highlight</c> is the pointer hovering, which over the page is no selection at all,
    ///     and the editor has no hot keys to draw.
    /// </remarks>
    protected override bool OnGettingAttributeForRole(in VisualRole role, ref Attribute currentAttribute)
    {
        currentAttribute = role == VisualRole.Active
            ? theme.For(Role.SelectedText)
            : theme.For(Role.Body);

        return true;
    }

    /// <summary>
    ///     The placeholder over an empty editor, in <see cref="Role.Muted" /> — drawn only while there is nothing
    ///     written, so it goes with the first letter and comes back when the post is cleared, and never reads as part
    ///     of the post.
    /// </summary>
    protected override bool OnDrawingContent(DrawContext? context)
    {
        _painted = PaintsReferences ? Painted(Text, GetAllLines()) : [];

        var drawn = base.OnDrawingContent(context);

        if (Text.Length == 0 && Viewport.Width > 0)
        {
            SetAttribute(theme.For(Role.Muted));
            AddStr(0, 0, TextWrap.Clip(placeholder, Viewport.Width));
        }

        return drawn;
    }

    /// <summary>
    ///     A letter drawn in the normal role takes the role of the reference it is written in, where it is in one. A
    ///     selected letter is drawn by another hook, so a selection stays <see cref="Role.SelectedText" /> over a
    ///     reference.
    /// </summary>
    /// <remarks>
    ///     <paramref name="idxRow" /> and <paramref name="idxCol" /> are where the letter is drawn, on the rows the
    ///     text wrapped to. The references were found on the whole text, as a post's are (#83), so the letter is looked
    ///     up where it is written in that — which is what keeps an address wrapped across rows one address.
    /// </remarks>
    protected override void OnDrawNormalColor(List<Cell> line, int idxCol, int idxRow)
    {
        if (idxRow >= 0 && idxRow < _painted.Length && idxCol >= 0 && idxCol < _painted[idxRow].Length
            && _painted[idxRow][idxCol] is { } role)
        {
            SetAttribute(theme.For(role));

            return;
        }

        base.OnDrawNormalColor(line, idxCol, idxRow);
    }

    /// <summary>
    ///     The role of each letter on each of <paramref name="rows" /> — the editor's rows, wrapped where it wraps —
    ///     from the references found in the whole of <paramref name="text" />.
    /// </summary>
    /// <remarks>
    ///     A row's letters are found in the text in turn, stepping over whatever whitespace the wrap took up between
    ///     them, since the rows are all the editor says about where it broke the text. Whitespace is in no reference,
    ///     so a space the wrap kept or dropped can never put a letter of one out of place.
    /// </remarks>
    private static Role?[][] Painted(string text, List<List<Cell>> rows)
    {
        var roles = new Role?[text.Length];

        foreach (var reference in BodyText.References(text))
        {
            Array.Fill(roles, reference.Role, reference.At, reference.Text.Length);
        }

        var painted = new Role?[rows.Count][];
        var at = 0;

        for (var row = 0; row < rows.Count; row++)
        {
            painted[row] = new Role?[rows[row].Count];

            for (var column = 0; column < rows[row].Count; column++)
            {
                var letter = rows[row][column].Grapheme;

                while (at < text.Length && !Written(text, at, letter) && char.IsWhiteSpace(text[at]))
                {
                    at++;
                }

                if (letter.Length > 0 && Written(text, at, letter))
                {
                    painted[row][column] = roles[at];
                    at += letter.Length;
                }
            }
        }

        return painted;
    }

    private static bool Written(string text, int at, string letter) =>
        at + letter.Length <= text.Length && string.CompareOrdinal(text, at, letter, 0, letter.Length) == 0;

    /// <summary>
    ///     Where the caret is, in the coordinates the editor's own frame is laid in — on the row the text wrapped it
    ///     to, which only the editor knows.
    /// </summary>
    public System.Drawing.Point Caret =>
        new(Frame.X + CurrentColumn - Viewport.X, Frame.Y + CurrentRow - Viewport.Y);

    /// <summary>
    ///     Replaces the <paramref name="length" /> characters before the caret with <paramref name="text" />, as one
    ///     edit — selected and put in the way a paste goes in, rather than as the letter-by-letter typing
    ///     <see cref="TextView.InsertText(string)" /> stands in for — so that one undo puts back what was there.
    /// </summary>
    public void ReplaceBeforeCaret(int length, string text)
    {
        SelectionStartRow = CurrentRow;
        SelectionStartColumn = CurrentColumn - length;
        IsSelecting = true;
        OnPaste(text);
    }

    protected override bool OnKeyDown(Key key)
    {
        if (Answering?.Invoke(key) == true || Ahead?.Invoke(key) == true)
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

        // Pasted by hand rather than left to the key, since alt-v is no paste of TextView's own.
        if (FromTheClipboard?.Invoke(key) is { } attached)
        {
            if (!attached)
            {
                Paste();
            }

            return true;
        }

        if (key == Key.CursorUp && CurrentRow == 0)
        {
            up();

            return true;
        }

        if (down is not null && key == Key.CursorDown && CurrentRow >= Lines - 1)
        {
            down();

            return true;
        }

        return base.OnKeyDown(key);
    }

    /// <summary>
    ///     A right click is the shell's <c>esc</c>, which the view the editor is laid in has already taken ahead of the
    ///     views under the pointer (<see cref="DraftFields" />, #373) — so any that reaches here is taken before the
    ///     editor opens its own context menu on it, and does nothing (#307). A sideways notch is taken and does nothing
    ///     too: the text wraps to the editor's width, so there is nothing off either side, yet <see cref="TextView" />
    ///     scrolled a column a notch regardless and a trackpad drifting sideways carried the text off the field.
    /// </summary>
    protected override bool OnMouseEvent(Mouse mouse) =>
        ShellKeys.Of(mouse) is not null
        || mouse.Flags.HasFlag(MouseFlags.WheeledLeft)
        || mouse.Flags.HasFlag(MouseFlags.WheeledRight)
        || base.OnMouseEvent(mouse);
}
