using System.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Views;

/// <summary>
///     A list a compose field offers things from, hung wherever the field says (#318, #335): which thing is picked, how
///     far it has scrolled, and the keys and the pointer it takes while it is open. The field decides what is offered,
///     what a row says, where the list sits and what picking one does; everything about being a list is here, once, so
///     the people to mention and the languages behave alike.
/// </summary>
/// <remarks>
///     The keys are <c>↑</c>/<c>↓</c> to move the pick, round at either end; <c>tab</c>/<c>⏎</c> to pick; <c>esc</c>
///     to close. A click on a thing picks it, a notch of the wheel over the list moves the pick a thing at a time,
///     scrolling the rows to keep it shown, and a right click on it is nothing (#307). A click anywhere else closes the
///     list and is spent on that, so nothing under it is touched (<see cref="ClosedBy" />).
/// </remarks>
internal sealed class PickList<T>
{
    private readonly Func<T, int> _columns;
    private readonly Action<T> _picked;
    private readonly Action _closed;

    /// <summary>What is on offer, in order; nothing while the list is closed.</summary>
    private IReadOnlyList<T> _things = [];

    /// <summary>What the list stands open saying in place of things to pick (<see cref="Note" />); nothing otherwise.</summary>
    private Line? _note;

    /// <summary>Which of <see cref="_things" /> <c>tab</c> or <c>⏎</c> would pick.</summary>
    private int _at;

    /// <summary>The first of <see cref="_things" /> drawn, where there are more than the list has rows for.</summary>
    private int _top;

    /// <summary>Whether a press outside the list closed it, so the rest of that click is spent with it.</summary>
    private bool _spent;

    /// <param name="theme">What answers the roles.</param>
    /// <param name="id">The list's <c>Id</c> among the window's views.</param>
    /// <param name="row">
    ///     What a thing says on its row, given whether it is the picked one, any picture on it set at the column it
    ///     stands at in what it says.
    /// </param>
    /// <param name="columns">How many columns what a thing says takes, which is what the list is sized to.</param>
    /// <param name="picked">What picking a thing does — by key or by click.</param>
    /// <param name="closed">What closing the list without picking does — by <c>esc</c> or by a click elsewhere.</param>
    /// <param name="picks">What <c>tab</c> is said to do along the list's bottom edge.</param>
    /// <param name="overlaid">
    ///     What the list draws the pictures on its rows from, laid over the content panel — a custom emoji's (#580) — or
    ///     <see langword="null" /> for a list of rows of text alone.
    /// </param>
    public PickList(
        ITheme theme,
        string id,
        Func<T, bool, Line> row,
        Func<T, int> columns,
        Action<T> picked,
        Action closed,
        string picks = "insert",
        PanelPictures? overlaid = null)
    {
        _columns = columns;
        _picked = picked;
        _closed = closed;

        View = new PaintedView(
            theme,
            (width, height) => _note is { } note
                ? PickLines.Rows([note], picked: -1, top: 0, height, width, (line, _) => line, picks)
                : PickLines.Rows(_things, _at, Scrolled(height), height, width, row, picks),
            over: overlaid)
        {
            Id = id,
            CanFocus = false,
            Visible = false,
        };

        View.MouseEvent += (_, mouse) => mouse.Handled = Pointed(mouse);
    }

    /// <summary>The list as drawn, which the window lays over the field.</summary>
    public PaintedView View { get; }

    /// <summary>Whether the list is open, which is whether it has anything on offer or stands open on a note.</summary>
    public bool Open => _things.Count > 0 || Noting;

    /// <summary>Whether the list stands open on a <see cref="Note" />, with nothing to pick.</summary>
    public bool Noting => _note is not null;

    /// <summary>
    ///     Puts <paramref name="things" /> on offer, opening the list on them or, where there are none, closing it.
    ///     <paramref name="keepingThePick" /> keeps the pick where it was, as when more of the same arrive; otherwise
    ///     it goes back to <paramref name="picking" />, the first unless said otherwise — as a list opened on what its
    ///     field already holds opens picked on it.
    /// </summary>
    public void Offer(IReadOnlyList<T> things, bool keepingThePick, int picking = 0)
    {
        // A note has no pick to keep: the things that follow it open picked as a list opening does.
        var noted = Noting;

        _things = things;
        _note = null;

        if (!keepingThePick || things.Count == 0 || noted)
        {
            _at = picking;
            _top = 0;
        }

        _at = Math.Clamp(_at, 0, Math.Max(0, things.Count - 1));

        Shown();
    }

    /// <summary>
    ///     Opens the list on <paramref name="note" /> alone, said rather than offered: drawn where a thing's row is,
    ///     unmarked, with the list's usual foot, and sized to it as to a thing. Nothing picks it — <c>tab</c>,
    ///     <c>⏎</c>, a click and the wheel on it are spent on nothing — and <c>↑</c>/<c>↓</c>, with nothing to move,
    ///     go on to the field; <c>esc</c> and a click elsewhere close the list as ever.
    /// </summary>
    public void Note(Line note)
    {
        _things = [];
        _note = note;
        _at = 0;
        _top = 0;

        Shown();
    }

    /// <summary>
    ///     How big the list is drawn, given at most <paramref name="width" /> columns and <paramref name="height" />
    ///     rows: wide enough for its widest thing, and tall enough for every thing where there is room, scrolling where
    ///     there is not.
    /// </summary>
    public Size Within(int width, int height)
    {
        var widest = _note is { } note ? note.Width : _things.Select(_columns).DefaultIfEmpty().Max();
        var rows = Noting ? 1 : _things.Count;

        return new Size(
            Math.Min(PickLines.Width(widest), width),
            Math.Min(rows, Math.Max(1, height - PickLines.Edges)) + PickLines.Edges);
    }

    /// <summary>
    ///     First refusal on a key the field was sent: <c>↑</c>/<c>↓</c> move the pick, <c>tab</c>/<c>⏎</c> pick,
    ///     <c>esc</c> closes the list — while it is open. Nothing at all while it is closed.
    /// </summary>
    /// <returns>Whether the list took the key, which the field then does not see.</returns>
    public bool Took(Key key)
    {
        if (!Open)
        {
            return false;
        }

        if (key == Key.CursorDown || key == Key.CursorUp)
        {
            if (Noting)
            {
                return false;
            }

            Move((_at + (key == Key.CursorDown ? 1 : -1) + _things.Count) % _things.Count);

            return true;
        }

        if (key == Key.Esc)
        {
            _closed();

            return true;
        }

        if (key != Key.Tab && key != Key.Enter)
        {
            return false;
        }

        if (!Noting)
        {
            _picked(_things[_at]);
        }

        return true;
    }

    /// <summary>
    ///     First refusal on every mouse event on the terminal, wherever it lands: a click outside the open list closes
    ///     it, and is spent on that from the press to the click, so whatever is under it — the editor's caret, a
    ///     destination on the rail — is not touched.
    /// </summary>
    /// <returns>Whether the event was spent on the list, which nothing else then sees.</returns>
    public bool ClosedBy(Mouse mouse)
    {
        var left = mouse.Flags.HasFlag(MouseFlags.LeftButtonPressed)
                   || mouse.Flags.HasFlag(MouseFlags.LeftButtonReleased)
                   || mouse.Flags.HasFlag(MouseFlags.LeftButtonClicked)
                   || mouse.Flags.HasFlag(MouseFlags.LeftButtonDoubleClicked)
                   || mouse.Flags.HasFlag(MouseFlags.LeftButtonTripleClicked);

        if (!left)
        {
            return false;
        }

        if (_spent)
        {
            _spent = !mouse.Flags.HasFlag(MouseFlags.LeftButtonClicked);

            return true;
        }

        if (!Open || View.FrameToScreen().Contains(mouse.ScreenPosition))
        {
            return false;
        }

        _spent = !mouse.Flags.HasFlag(MouseFlags.LeftButtonClicked);
        _closed();

        return true;
    }

    /// <summary>A mouse event on the list itself: a click on a thing picks it, and a notch moves the pick.</summary>
    private bool Pointed(Mouse mouse)
    {
        // A note is only said: every press on it is the list's, and picks nothing.
        if (!Open || Noting)
        {
            return Open;
        }

        if (mouse.Flags.HasFlag(MouseFlags.LeftButtonClicked))
        {
            if (View.ItemAt(mouse.ScreenPosition) is { } at)
            {
                _picked(_things[at]);
            }

            return true;
        }

        if (mouse.Flags.HasFlag(MouseFlags.WheeledLeft) || mouse.Flags.HasFlag(MouseFlags.WheeledRight))
        {
            return true;
        }

        if (mouse.Flags.HasFlag(MouseFlags.WheeledDown) || mouse.Flags.HasFlag(MouseFlags.WheeledUp))
        {
            Move(Math.Clamp(_at + (mouse.Flags.HasFlag(MouseFlags.WheeledDown) ? 1 : -1), 0, _things.Count - 1));

            return true;
        }

        // Every other press on the list is the list's — a right click above all, which is nothing here (#307) rather
        // than the esc it is elsewhere.
        return true;
    }

    private void Shown()
    {
        View.Visible = Open;
        View.SetNeedsLayout();
        View.SetNeedsDraw();
        View.SuperView?.SetNeedsDraw();
    }

    private void Move(int to)
    {
        _at = to;
        View.SetNeedsDraw();
    }

    /// <summary>
    ///     The first thing drawn in <paramref name="height" /> rows: where it was, moved only as far as keeps the pick
    ///     on them.
    /// </summary>
    private int Scrolled(int height)
    {
        var shown = Math.Max(1, height - PickLines.Edges);

        _top = Math.Clamp(_top, Math.Max(0, _at - shown + 1), _at);
        _top = Math.Clamp(_top, 0, Math.Max(0, _things.Count - shown));

        return _top;
    }
}
