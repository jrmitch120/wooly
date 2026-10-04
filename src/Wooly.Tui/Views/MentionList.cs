using System.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Views;

/// <summary>
///     The list of people to mention, hung under the @-word being typed in the compose editor (#318): what it matches,
///     which one is picked, where it sits, and the keys it takes while it is open. Closed, it takes nothing and the
///     editor's keys are what they always were.
/// </summary>
/// <remarks>
///     The word is read from the text as written rather than as wrapped, off the editor's unwrapped caret; where the
///     list sits is read from where the editor put its caret, since only the editor knows how the text wrapped.
/// </remarks>
internal sealed class MentionList
{
    /// <summary>The list's <c>Id</c> among the window's views.</summary>
    internal const string Id = "mentions";

    private readonly Shell.Shell _shell;
    private readonly ComposeEditor _editor;

    /// <summary>Who matches the word being typed, best first; nobody while the list is closed.</summary>
    private IReadOnlyList<Mentionable> _people = [];

    /// <summary>The word the list is open on, and the line it is on in the text as written.</summary>
    private (int Line, MentionWord Word)? _open;

    /// <summary>Which of <see cref="_people" /> <c>tab</c> or <c>enter</c> would insert.</summary>
    private int _picked;

    /// <summary>
    ///     The caret in the text as written, as the editor last said it was — the one place that knows, since its own
    ///     row and column are in the text as wrapped.
    /// </summary>
    private Point _unwrapped;

    /// <summary>The word <c>esc</c> closed the list on, by line and start, which keeps it closed until the caret leaves it.</summary>
    private (int Line, int Start)? _dismissed;

    public MentionList(ITheme theme, Shell.Shell shell, ComposeEditor editor)
    {
        _shell = shell;
        _editor = editor;

        View = new PaintedView(
            theme,
            (width, _) => MentionLines.Rows(_people, _picked, _open?.Word.Query ?? string.Empty, width))
        {
            Id = Id,
            X = Pos.Func(_ => At().X, editor),
            Y = Pos.Func(_ => At().Y, editor),
            Width = Dim.Func(_ => Size().Width, editor),
            Height = Dim.Func(_ => Size().Height, editor),
            CanFocus = false,
            Visible = false,
        };

        editor.ContentsChanged += (_, _) => Follow();
        editor.UnwrappedCursorPositionChanged += (_, moved) =>
        {
            _unwrapped = moved;
            Follow();
        };
    }

    /// <summary>The list as drawn, which the window lays over the editor.</summary>
    public PaintedView View { get; }

    /// <summary>
    ///     Reads the word the caret is at the end of again, and opens, narrows or closes the list to match — on every
    ///     edit and caret move, and whenever the shell has changed under it.
    /// </summary>
    public void Follow()
    {
        var query = _open?.Word.Query;

        _open = null;
        _people = [];

        if (_editor.Visible && _shell.Screen is ComposeScreen { WritingTheWarning: false } && Caret() is var (line, column, text))
        {
            if (MentionWord.At(text, column) is not { } word)
            {
                _dismissed = null;
            }
            else if (_dismissed != (line, word.Start))
            {
                _dismissed = null;
                _open = (line, word);
                _people = _shell.PeopleMatching(word.Query);
            }
        }

        if (_open?.Word.Query != query || _people.Count == 0)
        {
            _picked = 0;
        }

        View.Visible = _people.Count > 0;
        View.SetNeedsLayout();
        View.SetNeedsDraw();
        View.SuperView?.SetNeedsDraw();
    }

    /// <summary>
    ///     First refusal on a key the editor was sent: <c>↑</c>/<c>↓</c> move the pick, <c>tab</c>/<c>enter</c> insert
    ///     it, <c>esc</c> closes the list — while it is open. Nothing at all while it is closed.
    /// </summary>
    /// <returns>Whether the list took the key, which the editor then does not see.</returns>
    public bool Took(Key key)
    {
        if (!View.Visible || _open is not var (line, word))
        {
            return false;
        }

        if (key == Key.CursorDown || key == Key.CursorUp)
        {
            _picked = (_picked + (key == Key.CursorDown ? 1 : -1) + _people.Count) % _people.Count;
            View.SetNeedsDraw();

            return true;
        }

        if (key == Key.Esc)
        {
            _dismissed = (line, word.Start);
            Follow();

            return true;
        }

        if (key != Key.Tab && key != Key.Enter)
        {
            return false;
        }

        Insert(_people[_picked], word);

        return true;
    }

    /// <summary>
    ///     Replaces the word with <paramref name="person" />'s mention and a space, through the editor's own editing —
    ///     so the caret lands after it and one undo puts the word back.
    /// </summary>
    private void Insert(Mentionable person, MentionWord word)
    {
        var mention = $"{_shell.MentionOf(person)} ";

        View.Visible = false;

        _editor.ReplaceBeforeCaret(word.Query.Length + 1, mention);
        Follow();
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

    /// <summary>How big the list is drawn: wide enough for its widest row, never wider than the editor.</summary>
    private Size Size() =>
        new(Math.Min(MentionLines.Width(_people), _editor.Frame.Width), _people.Count + 2);

    /// <summary>
    ///     Where the list sits: on the row under the caret, left-aligned on the word's <c>@</c> — or over the line
    ///     where there is no room under it before the editor's foot. Read from where the editor put its caret, in the
    ///     wrapped text, so on a line that wrapped it hangs under the row actually being typed on.
    /// </summary>
    private Point At()
    {
        var size = Size();
        var caret = _editor.Caret;

        var typed = (_open?.Word.Query.Length ?? 0) + 1;
        var x = Math.Clamp(caret.X - typed, _editor.Frame.X, Math.Max(_editor.Frame.X, _editor.Frame.Right - size.Width));
        var y = caret.Y + 1 + size.Height <= _editor.Frame.Bottom ? caret.Y + 1 : Math.Max(0, caret.Y - size.Height);

        return new Point(x, y);
    }
}
