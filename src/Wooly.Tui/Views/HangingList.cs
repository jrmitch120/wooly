using System.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Views;

/// <summary>
///     The list hung under the word being typed in an editor (#318, #576), offering whatever that kind of word calls
///     for: which one is picked, where it sits, and the keys it takes while it is open. Closed, it takes nothing and the
///     editor's keys are what they always were. Compose's post has one, and so does the edit public profile form's bio
///     (#408): each view plugs its own into its editor's <see cref="ComposeEditor.Ahead" />.
/// </summary>
/// <remarks>
///     <para>
///         Being the list is here, once, whatever it offers: the keys and the pointer (<see cref="PickList{T}" />'s),
///         the memory of a word <c>esc</c> closed, where it hangs, and replacing the word with the pick as one undoable
///         edit. What a word is, what matches it, what a row says, what a pick writes and what the list says while what
///         would match is on its way are each <see cref="IHangingOffering" />'s — people for an <c>@</c>-word
///         (<see cref="MentionOffering" />), the instance's custom emoji for a <c>:</c>-word (<see cref="EmojiOffering" />).
///     </para>
///     <para>
///         The word is read from the text as written rather than as wrapped, off the editor's unwrapped caret; where
///         the list sits is read from where the editor put its caret, since only the editor knows how the text wrapped.
///     </para>
/// </remarks>
internal sealed class HangingList
{
    /// <summary>The list's <c>Id</c> among the views of whichever view lays it beside its editor.</summary>
    internal const string Id = "hanging";

    private readonly ComposeEditor _editor;

    /// <summary>Whether the screen in front offers anything in the editor at all, as it stands.</summary>
    private readonly Func<bool> _offered;

    /// <summary>The kinds of word the list opens on, and what each offers; the caret is at the end of one at most.</summary>
    private readonly IReadOnlyList<IHangingOffering> _offerings;

    /// <summary>What matches the word being typed, best first, as a list to pick from; nothing while it is closed.</summary>
    private readonly PickList<HangingOffer> _list;

    /// <summary>The word the list is open on, the line it is on in the text as written, and what offers for it.</summary>
    private (int Line, HangingWord Word, IHangingOffering By)? _open;

    /// <summary>
    ///     The caret in the text as written, as the editor last said it was — the one place that knows, since its own
    ///     row and column are in the text as wrapped.
    /// </summary>
    private Point _unwrapped;

    /// <summary>
    ///     The text <see cref="_unwrapped" /> was said in. An edit changes the text before the editor says where it put
    ///     the caret, and whatever the edit sets off meanwhile (the shell changing, which follows the list again) would
    ///     read the word off the caret from before the edit; until the editor says, the list is left as it was.
    /// </summary>
    private string? _caretIn;

    /// <summary>
    ///     The word the caret is in, by line and its sign's start, and its kind: at its end, inside it, or in one too short
    ///     yet to open on. The caret leaving it is what makes the next one a new word.
    /// </summary>
    private (int Line, int Start, IHangingOffering By)? _in;

    /// <summary>Whether what offers for the word the caret is in has been told the caret came into it.</summary>
    private bool _toldIn;

    /// <summary>Whether <c>esc</c> closed the list on the word the caret is in, which keeps it closed until the caret leaves.</summary>
    private bool _dismissed;

    /// <summary>
    ///     Whether the list stands open on the word the caret is at the end of, saying what it offers is on its way
    ///     (<see cref="IHangingOffering.Awaiting" />) — the one time it takes up what came in without a keystroke.
    /// </summary>
    private bool _awaiting;

    /// <param name="editor">The editor this list is laid beside in the same view and hangs under.</param>
    /// <param name="offered">
    ///     Whether the screen in front offers anything in <paramref name="editor" />, as it stands: compose while the
    ///     typing is in the post rather than its warning, or the edit public profile form.
    /// </param>
    /// <param name="pictures">What the pictures on the list's rows are drawn from, the list being laid over the content panel.</param>
    /// <param name="offerings">The kinds of word the list opens on, each with what it offers.</param>
    public HangingList(
        ITheme theme,
        ComposeEditor editor,
        Func<bool> offered,
        PanelPictures pictures,
        params IReadOnlyList<IHangingOffering> offerings)
    {
        _editor = editor;
        _offered = offered;
        _offerings = offerings;

        _list = new PickList<HangingOffer>(
            theme,
            Id,
            (offer, picked) => offer.Row(picked),
            offer => offer.Columns,
            Insert,
            Dismiss,
            overlaid: pictures);

        // Laid beside the editor in the same view, so where the editor puts its caret is where in that view it goes.
        View.X = Pos.Func(_ => At().X);
        View.Y = Pos.Func(_ => At().Y);
        View.Width = Dim.Func(_ => Size().Width);
        View.Height = Dim.Func(_ => Size().Height);

        // Every edit ends with the editor saying where its caret went, even where it stayed put, so that is when an edit
        // is followed: not on the edit itself, whose caret is still the one before it.
        editor.UnwrappedCursorPositionChanged += (_, moved) =>
        {
            _unwrapped = moved;
            _caretIn = editor.Text;
            Follow(typed: true);
        };
    }

    /// <summary>The list as drawn, which the view holding the editor lays over it.</summary>
    public PaintedView View => _list.View;

    /// <summary>
    ///     The list a draft's editor hangs — compose's post and the edit public profile form's bio alike: the people to
    ///     mention on an <c>@</c>-word, and the instance's custom emoji on a <c>:</c>-word (#528).
    /// </summary>
    /// <param name="offered">Whether the screen in front offers anything in <paramref name="editor" />, as it stands.</param>
    public static HangingList OfADraft(
        ITheme theme,
        ComposeEditor editor,
        Func<bool> offered,
        Shell.Shell shell,
        Drafting.Drafting drafting,
        PanelPictures pictures) =>
        new(theme, editor, offered, pictures, new MentionOffering(shell), new EmojiOffering(drafting, pictures));

    /// <summary>
    ///     Reads the word the caret is at the end of again, and opens, narrows or closes the list to match, whenever the
    ///     shell has changed under it.
    /// </summary>
    public void Follow() => Follow(typed: false);

    /// <summary>
    ///     Reads the word the caret is at the end of again, and opens, narrows or closes the list to match — on every
    ///     edit and caret move, <paramref name="typed" />, and whenever the shell has changed under it. The pick stays
    ///     where it was only while the same kind of word asks the same.
    /// </summary>
    private void Follow(bool typed)
    {
        if (_editor.Text != _caretIn)
        {
            return;
        }

        var was = _open;
        var wasIn = _in;
        var wasAwaiting = _awaiting;
        IReadOnlyList<HangingOffer> offers = [];

        _open = null;
        _in = null;
        _awaiting = false;

        var caret = _editor.Visible && _offered() ? Caret() : null;

        if (caret is var (line, column, text))
        {
            _in = In(line, text, column);
        }

        // The caret leaving the word ends esc's memory of it, and whatever word it comes to next is a new one.
        if (_in != wasIn)
        {
            _dismissed = false;
            _toldIn = false;
        }

        // Told as the caret comes into the word, before it has the letters to open on, so whatever it offers is on its
        // way by the time it has.
        if (_in is var (_, _, into) && !_toldIn)
        {
            _toldIn = true;
            into.Entering();
        }

        if (caret is var (atLine, at, on)
            && _in is var (_, _, by)
            && !_dismissed
            && by.At(on, at) is { } word)
        {
            // A keystroke takes up what came in since the last, and so does a redraw while the list stands waiting on
            // this same word for it; no other redraw does, so the list never jumps open by itself.
            if (typed || (wasAwaiting && _in == wasIn))
            {
                by.TakeUp();
            }

            _open = (atLine, word, by);
            offers = by.Matching(word);

            // Nothing matching yet, since what would is on its way: the list stands open saying so.
            if (offers.Count == 0 && by.Awaiting() is { } awaiting)
            {
                _awaiting = true;
                _list.Note(new HangingOffer(_ => awaiting, awaiting.Width, () => string.Empty));

                return;
            }
        }

        _list.Offer(offers, keepingThePick: _open?.By == was?.By && _open?.Word.Query == was?.Word.Query);
    }

    /// <summary>
    ///     First refusal on a key the editor was sent: the list's own keys while it is open (<see cref="PickList{T}.Took" />),
    ///     nothing at all while it is closed.
    /// </summary>
    /// <returns>Whether the list took the key, which the editor then does not see.</returns>
    public bool Took(Key key) => _open is not null && _list.Took(key);

    /// <summary>First refusal on a mouse event anywhere: a click outside the open list closes it, as <c>esc</c> does.</summary>
    /// <returns>Whether the event was spent on closing the list.</returns>
    public bool ClosedBy(Mouse mouse) => _list.ClosedBy(mouse);

    /// <summary>
    ///     The word of whichever kind the caret at <paramref name="column" /> on <paramref name="text" />, line
    ///     <paramref name="line" />, is in, by where its sign starts, and what offers for it.
    /// </summary>
    private (int Line, int Start, IHangingOffering By)? In(int line, string text, int column)
    {
        foreach (var offering in _offerings)
        {
            if (offering.In(text, column) is { } start)
            {
                return (line, start, offering);
            }
        }

        return null;
    }

    /// <summary>Closes the list on the word it is open on, which keeps it closed until the caret leaves the word.</summary>
    private void Dismiss()
    {
        _dismissed |= _open is not null;

        Follow(typed: true);
    }

    /// <summary>
    ///     Replaces the word with what <paramref name="offer" /> writes, through the editor's own editing — so the caret
    ///     lands after it and one undo puts the word back.
    /// </summary>
    private void Insert(HangingOffer offer)
    {
        if (_open is not var (_, word, _))
        {
            return;
        }

        var writes = offer.Writes();

        View.Visible = false;

        _editor.ReplaceBeforeCaret(word.Length, writes);
        Follow(typed: true);
    }

    /// <summary>The caret in the text as written: its line, its column on it, and that line.</summary>
    private (int Line, int Column, string Text)? Caret()
    {
        var lines = _editor.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var (row, column) = (_unwrapped.Y, _unwrapped.X);

        if (row < 0 || row >= lines.Length)
        {
            return null;
        }

        return (row, Math.Clamp(column, 0, lines[row].Length), lines[row]);
    }

    /// <summary>
    ///     How big the list is drawn: wide enough for its widest row, never wider than the editor, and no taller than
    ///     the more room there is under the caret's row before the editor's foot or over it.
    /// </summary>
    private Size Size()
    {
        var caret = _editor.Caret.Y;

        return _list.Within(_editor.Frame.Width, Math.Max(_editor.Frame.Bottom - caret - 1, caret));
    }

    /// <summary>
    ///     Where the list sits: on the row under the caret, left-aligned on the word's sign — or over the line where
    ///     there is no room under it before the editor's foot. Read from where the editor put its caret, in the wrapped
    ///     text, so on a line that wrapped it hangs under the row actually being typed on.
    /// </summary>
    private Point At()
    {
        var size = Size();
        var caret = _editor.Caret;

        var typed = _open?.Word.Length ?? 1;
        var x = Math.Clamp(caret.X - typed, _editor.Frame.X, Math.Max(_editor.Frame.X, _editor.Frame.Right - size.Width));
        var y = caret.Y + 1 + size.Height <= _editor.Frame.Bottom ? caret.Y + 1 : Math.Max(0, caret.Y - size.Height);

        return new Point(x, y);
    }
}
