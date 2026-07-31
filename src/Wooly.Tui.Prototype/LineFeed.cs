using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace Wooly.Tui.Prototype;

/// <summary>One drawn row: what it says, how it is coloured, and which post it belongs to.</summary>
internal readonly record struct FeedLine(string Text, Attribute Attribute, int Item);

/// <summary>
///     Scrolling and selection over a list of posts that each take however many rows they take. Shared because it is
///     plumbing, not design — every variant that shows a feed still composes its own rows.
/// </summary>
internal abstract class LineFeed : View
{
    private IReadOnlyList<FeedLine> _lines = [];
    private int _scroll;
    private int _width = -1;

    protected LineFeed(IReadOnlyList<FeedItem> items)
    {
        Items = items;
        CanFocus = true;
    }

    public IReadOnlyList<FeedItem> Items { get; }

    public int Selected { get; private set; }

    public FeedItem Current => Items[Selected];

    public event EventHandler<int>? SelectionChanged;

    /// <summary>
    ///     A shell-level keymap, consulted before this view's own keys. Needed because a <c>KeyDown</c> handler on an
    ///     ancestor only ever sees what the focused view did not consume — j and k would never reach it.
    /// </summary>
    public Func<Key, bool>? Intercept { get; set; }

    /// <summary>Whether the selected post's rows are marked. Off where something else already shows the selection.</summary>
    protected virtual bool MarkSelection => true;

    /// <summary>Turns the posts into rows, at the width the view actually has.</summary>
    protected abstract IReadOnlyList<FeedLine> Compose(int width);

    public void Invalidate()
    {
        _width = -1;
        SetNeedsDraw();
    }

    protected override bool OnKeyDown(Key key)
    {
        if (Intercept?.Invoke(key) == true)
        {
            return true;
        }

        if (key == Key.CursorDown || key == Key.J)
        {
            Move(1);

            return true;
        }

        if (key == Key.CursorUp || key == Key.K)
        {
            Move(-1);

            return true;
        }

        if (key == Key.PageDown)
        {
            Move(4);

            return true;
        }

        if (key == Key.PageUp)
        {
            Move(-4);

            return true;
        }

        if (key == Key.Home)
        {
            Move(-Items.Count);

            return true;
        }

        if (key == Key.End)
        {
            Move(Items.Count);

            return true;
        }

        return false;
    }

    public void Select(int index)
    {
        var wanted = Math.Clamp(index, 0, Items.Count - 1);

        if (wanted == Selected)
        {
            return;
        }

        Selected = wanted;
        SelectionChanged?.Invoke(this, Selected);

        // Cheap, and it lets a variant draw the selection into the rows themselves rather than over them.
        _width = -1;
        ScrollToSelection();
        SetNeedsDraw();
    }

    private void Move(int by) => Select(Selected + by);

    private void ScrollToSelection()
    {
        var first = -1;
        var last = -1;

        for (var index = 0; index < _lines.Count; index++)
        {
            if (_lines[index].Item != Selected)
            {
                continue;
            }

            first = first < 0 ? index : first;
            last = index;
        }

        if (first < 0)
        {
            return;
        }

        var height = Viewport.Height;

        if (first < _scroll)
        {
            _scroll = first;
        }
        else if (last >= _scroll + height)
        {
            _scroll = Math.Max(0, last - height + 1);
        }
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        var width = Viewport.Width;
        var height = Viewport.Height;

        if (width <= 0 || height <= 0)
        {
            return true;
        }

        if (width != _width)
        {
            _width = width;
            _lines = Compose(width);
            ScrollToSelection();
        }

        SetAttribute(Ink.Body);

        for (var row = 0; row < height; row++)
        {
            var index = _scroll + row;

            if (index >= _lines.Count)
            {
                SetAttribute(Ink.Body);
                AddStr(0, row, new string(' ', width));

                continue;
            }

            var line = _lines[index];
            var marked = MarkSelection && line.Item == Selected;

            SetAttribute(marked ? Ink.SelectedDim : Ink.Body);
            AddStr(0, row, marked ? "▌" : " ");

            SetAttribute(line.Attribute);
            AddStr(1, row, Ink.Clip(line.Text, width - 1).PadRight(width - 1));
        }

        return true;
    }
}
