// PROTOTYPE (#374) — throwaway. The compose screen in three layouts, and the attachments screen layout C opens.

using System.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Prototype.Attachments;

internal static class Geometry
{
    public const int Pad = 2;
    public const int LabelWidth = 5; // wide enough for Media
    public const int ValueAt = Pad + LabelWidth + 2;

    public static void Label(Painted view, int y, string label, Role role) =>
        view.Put(Pad + LabelWidth - Glyphs.Columns(label), y, label, role);
}

internal sealed class ComposeScreen : Screen
{
    private const int Above = 5; // blank, From, To, Lang, Warn — then the attachments

    private readonly Field _warning;

    private readonly AttachArea _attach;

    private readonly Editor _editor;

    private readonly StripArea _strip;

    private View? _last;

    public ComposeScreen()
    {
        CanFocus = true;

        _warning = new Field
        {
            X = Geometry.ValueAt,
            Y = 4,
            Width = Dim.Fill(Geometry.Pad),
            Height = 1,
            Hint = "none · ctrl-w to add",
        };
        _attach = new AttachArea(AreaMode.Rows) { X = 0, Y = Above, Width = Dim.Fill() };
        _editor = new Editor { X = Geometry.Pad, Width = Dim.Fill(Geometry.Pad), Hint = "What's on your mind?" };
        _strip = new StripArea { X = 0, Width = Dim.Fill(), Height = StripArea.Rows };

        _attach.Height = Dim.Func(_ => _attach.Wanted);
        _editor.Y = Pos.Func(_ => Above + _attach.Wanted + 2);
        _editor.Height = Dim.Func(_ => Math.Max(3, Viewport.Height - (Above + _attach.Wanted + 2) - StripRoom - 2));
        _strip.Y = Pos.Func(_ => Viewport.Height - 2 - StripArea.Rows);

        _warning.TextChanged += (_, _) =>
        {
            Proto.Draft.Warning = _warning.Text;
            Proto.Draft.Touch();
        };
        _warning.Ahead = key =>
        {
            if (key == Key.CursorDown || key == Key.Enter || key == Key.Tab)
            {
                Enter(_attach, fromAbove: true);
                return true;
            }

            return Shared(key, text => _warning.InsertText(text));
        };

        _editor.Ahead = key =>
        {
            if (key == Key.CursorUp && _editor.CurrentRow == 0)
            {
                Enter(_attach, fromAbove: false);
                return true;
            }

            if (key == Key.CursorDown && _editor.CurrentRow >= _editor.Lines - 1 && _strip.Visible)
            {
                _strip.Cursor = 0;
                _strip.SetFocus();
                return true;
            }

            if (key == Key.W.WithCtrl)
            {
                _warning.SetFocus();
                return true;
            }

            if (key == Key.Esc)
            {
                Proto.Say("esc here would ask \"Discard this post?\" (#373) — ctrl-q quits the prototype");
                return true;
            }

            return Shared(key, _editor.Insert);
        };

        _editor.ContentsChanged += (_, _) => SetNeedsDraw();
        _attach.Up = () => _warning.SetFocus();
        _attach.Down = () => _editor.SetFocus();
        _attach.Warn = () => _warning.SetFocus();
        _strip.Up = () => _editor.SetFocus();

        Add(_warning, _attach, _editor, _strip);

        foreach (var view in new View[] { _warning, _attach, _editor, _strip })
        {
            view.HasFocusChanged += (_, args) =>
            {
                if (args.NewValue) _last = view;
                SetNeedsDraw();
            };
        }

        Relaid();
    }

    private int StripRoom => _strip.Visible ? StripArea.Rows + 1 : 0;

    public override string Hints => _attach.HasFocus ? _attach.Hints
        : _strip.HasFocus ? _strip.Hints
        : "ctrl-o attach · ctrl-v paste a picture or files · drop files on the terminal · ↑ to the headers · ctrl-w warning · ctrl-s send";

    public override void Shown() => (_last ?? _editor).SetFocus();

    public override void Relaid()
    {
        _attach.Mode = Proto.Layout switch
        {
            ComposeLayout.Rows => AreaMode.Rows,
            ComposeLayout.Strip => AreaMode.HeaderOnly,
            _ => AreaMode.Summary,
        };
        _strip.Visible = Proto.Layout == ComposeLayout.Strip && Proto.Draft.Items.Count > 0;

        if (_strip.HasFocus && !_strip.Visible) _editor.SetFocus();

        _attach.Cursor = Math.Min(_attach.Cursor, _attach.Last);
        SetNeedsLayout();
        SetNeedsDraw();
    }

    private void Enter(AttachArea area, bool fromAbove)
    {
        area.Cursor = fromAbove ? -1 : area.Last;
        area.SetFocus();
    }

    /// <summary>Keys the warning, the editor and the attachments all take.</summary>
    public static bool Shared(Key key, Action<string> text)
    {
        if (key == Key.O.WithCtrl)
        {
            Proto.OpenBrowser();
            return true;
        }

        if (key == Key.V.WithCtrl)
        {
            Proto.ClipboardPaste(text);
            return true;
        }

        if (key == Key.S.WithCtrl)
        {
            Proto.Send();
            return true;
        }

        return false;
    }

    protected override void Paint()
    {
        var width = Viewport.Width;
        var hairline = new string('─', Math.Max(0, width - Geometry.Pad * 2));

        Geometry.Label(this, 1, "From", Role.Muted);
        Spans(Geometry.ValueAt, 1, ("@jeff", Role.BylineHandle), (" · mastodon.social", Role.Muted));
        Geometry.Label(this, 2, "To", Role.Muted);
        Spans(Geometry.ValueAt, 2, ("public", Role.Body), ("  unlisted  followers  direct", Role.Muted));
        Geometry.Label(this, 3, "Lang", Role.Muted);
        Spans(Geometry.ValueAt, 3, ("en", Role.Body), (" · English", Role.Muted));
        Geometry.Label(this, 4, "Warn", _warning.Text.Length > 0 || _warning.HasFocus ? Role.ContentWarning : Role.Muted);

        var under = Above + _attach.Wanted;
        Put(Geometry.Pad, under, hairline, Role.PanelBorder);

        var foot = Viewport.Height - 2;
        Put(Geometry.Pad, foot, hairline, Role.PanelBorder);

        var count = $"{_editor.Text.Length} / 500";
        Put(width - Geometry.Pad - count.Length, foot + 1, count, Role.Muted);
    }
}

internal enum AreaMode { Rows, HeaderOnly, Summary, Manage }

/// <summary>The Attach header, and in layouts A and C's screen, a row per attachment.</summary>
internal sealed class AttachArea(AreaMode mode) : Painted
{
    private enum Hit { Attach, Sensitive, Row, Describe, Remove, Retry, Manage }

    private readonly List<(Rectangle Where, Hit Hit, int Index)> _hits = [];

    private int? _dragFrom;

    private bool _dragMoved;

    private int _dragOver;

    private Point _pressedAt;

    public AreaMode Mode { get; set; } = mode;

    /// <summary>-1 is the header itself; 0… the attachments' rows.</summary>
    public int Cursor { get; set; } = -1;

    public Action? Up { get; set; }

    public Action? Down { get; set; }

    public Action? Warn { get; set; }

    private static Draft Draft => Proto.Draft;

    private bool ListsRows => Mode is AreaMode.Rows or AreaMode.Manage;

    public int Last => ListsRows ? Draft.Items.Count - 1 : -1;

    private int ThumbRows => Mode == AreaMode.Manage ? 5 : Proto.TallRows ? 3 : 1;

    private int ThumbColumns => ThumbRows == 1 ? 3 : ThumbRows * 2 + 2;

    private int RowHeight => ThumbRows;

    public int Wanted => 1 + (ListsRows ? Draft.Items.Count * (RowHeight + (Mode == AreaMode.Manage ? 1 : 0)) : 0);

    private int RowTop(int index) => 1 + index * (RowHeight + (Mode == AreaMode.Manage ? 1 : 0));

    public string Hints => Cursor < 0
        ? Mode == AreaMode.Summary && Draft.Items.Count > 0
            ? "enter or click: the attachments screen · s sensitive · ↑ warning · ↓ editor"
            : "enter or click: attach… · s sensitive · ↑/↓ walk · ctrl-o attach"
        : "enter describe · del remove · ctrl-z undo remove · shift-↑/↓ reorder (or drag) · r retry · s sensitive · ↑/↓ walk";

    protected override void Paint()
    {
        _hits.Clear();

        var width = Viewport.Width;
        var focused = HasFocus;
        var items = Draft.Items;

        if (Mode != AreaMode.Manage)
        {
            Geometry.Label(this, 0, Proto.AttachLabel, Role.Muted);
        }
        else
        {
            Put(Geometry.Pad, 0, Proto.AttachLabel, Role.Media);
        }

        var x = Geometry.ValueAt;
        var onHeader = focused && Cursor < 0;

        var headerEnd = x;

        if (Mode == AreaMode.Summary && items.Count > 0)
        {
            var parts = new List<string> { $"{items.Count} attached" };

            if (Draft.Undescribed > 0) parts.Add($"{Draft.Undescribed} undescribed");
            if (Draft.Unfinished > 0) parts.Add($"{Draft.Unfinished} uploading");
            if (Draft.Refused > 0) parts.Add($"{Draft.Refused} refused");

            var summary = string.Join(" · ", parts) + "  ▸";
            Put(x, 0, summary, onHeader ? Role.SelectedText : Draft.Refused > 0 ? Role.Error : Role.Body);
            _hits.Add((new Rectangle(x, 0, Glyphs.Columns(summary), 1), Hit.Manage, -1));
            headerEnd = x + Glyphs.Columns(summary);
        }
        else if (items.Count < Instance.Most)
        {
            // The warning header's format: a muted hint of what the header holds and the key that adds to it.
            // The warning header's format: what the header holds, then the key that adds to it.
            var held = items.Count == 0 ? "none" : $"{items.Count} of {Instance.Most}";
            var end = Spans(x, 0, (held, onHeader ? Role.SelectedText : Role.Muted), (" · ", Role.Muted), ("ctrl-o to add", Role.Muted));
            _hits.Add((new Rectangle(x, 0, end - x, 1), Hit.Attach, -1));
            headerEnd = end;

            if (Mode == AreaMode.HeaderOnly && items.Count > 0)
            {
                headerEnd = Spans(end, 0, ($"  · {items.Count} below", Role.Muted));
            }
        }
        else
        {
            headerEnd = Spans(x, 0, ($"{items.Count} of {Instance.Most} attached", onHeader ? Role.SelectedText : Role.Muted));
        }

        if (items.Count > 0)
        {
            var (toggle, role) = Draft.Locked ? ("■ sensitive (warning)", Role.ContentWarning)
                : Draft.SensitiveChosen ? ("■ sensitive", Role.ContentWarning)
                : ("□ sensitive", Role.Muted);
            // Part of the header's own line, after its hint: it belongs to the media, and moves only when the
            // header's words do.
            var at = Spans(headerEnd, 0, (" · ", Role.Muted));
            Put(at, 0, toggle, role);
            _hits.Add((new Rectangle(at, 0, Glyphs.Columns(toggle), 1), Hit.Sensitive, -1));
        }

        if (!ListsRows) return;

        for (var index = 0; index < items.Count; index++)
        {
            PaintRow(index, items[index], focused && Cursor == index);
        }

    }

    /// <summary>The widest a compact row's description column gets, so × stays near what it removes.</summary>
    private const int DescriptionColumn = 40;

    private void PaintRow(int index, Attachment item, bool current)
    {
        var width = Viewport.Width;
        var top = RowTop(index);
        var thumb = new Rectangle(Geometry.ValueAt, top, ThumbColumns, ThumbRows);
        var dragged = _dragFrom == index;
        var items = Draft.Items;

        if (current) Put(Geometry.Pad, top, "▌", Role.Selection); // one space short of the grip

        Put(Geometry.Pad + 2, top, "⠿", dragged ? Role.Selection : Role.Muted);
        Pics.Paint(this, thumb, item.Path, Theme);

        _hits.Add((new Rectangle(0, top, width, RowHeight), Hit.Row, index));

        // Columns, as wide as the widest row needs, so every row lines up with the one above it.
        var (nameAt, nameWidth, kindAt, sizeAt, removeAt, describedAt) = Columns();
        var described = item.Description.Trim().Length > 0;

        var name = Glyphs.Columns(item.Name) > nameWidth ? Glyphs.Cut(item.Name, nameWidth - 1) + "…" : item.Name;
        Put(nameAt, top, name, current ? Role.SelectedText : Role.Body);
        Put(removeAt, top, "×", Role.Destructive);
        _hits.Insert(0, (new Rectangle(removeAt - 1, top, 3, 1), Hit.Remove, index));
        Put(kindAt, top, item.KindWord, Role.Muted);
        Put(sizeAt, top, item.Size.PadLeft(8), Role.Muted);
        // One column for whatever the row has to say: its progress, then its refusal, and once it is ready its
        // description. Always the same width, so nothing to its right moves as an upload goes along.
        var room = Math.Min(DescriptionColumn, width - Geometry.Pad - 3 - describedAt);
        var ready = item.State is State.Ready;

        if (!ready)
        {
            PaintState(describedAt, top, item, index);
        }

        if (RowHeight == 1)
        {
            if (ready && room > 4)
            {
                var said = Glyphs.Cut(described ? Quoted(item.Description, room) : Proto.Mark, room);
                Put(describedAt, top, said, described ? Role.Body : Role.Muted, room);

                // The words themselves open the description; the rest of the row only selects it.
                _hits.Insert(0, (new Rectangle(describedAt, top, Glyphs.Columns(said), 1), Hit.Describe, index));
            }

            return;
        }

        var textAt = nameAt;
        var right = describedAt;
        var lines = described ? TextWrap.Wrap(item.Description, Math.Max(1, right - textAt)) : [Proto.Mark];

        for (var line = 0; line < Math.Min(lines.Count, RowHeight - 1); line++)
        {
            var text = line == RowHeight - 2 && lines.Count > RowHeight - 1 ? Glyphs.Cut(lines[line], Math.Max(0, right - textAt - 1)) + "…" : lines[line];
            Put(textAt, top + 1 + line, text, described ? Role.Body : Role.Muted);
        }
    }

    /// <summary>
    ///     Where each column of a row starts, the same for every row so the rows line up: the name, its kind,
    ///     its size, ×, and then the one column for progress, a refusal or the description.
    /// </summary>
    private (int NameAt, int NameWidth, int KindAt, int SizeAt, int RemoveAt, int DescribedAt) Columns()
    {
        // Fixed, not the widest name's: attaching a long name moves nothing on the rows already there.
        const int nameWidth = 32;
        var nameAt = Geometry.ValueAt + ThumbColumns + 1;
        var kindAt = nameAt + nameWidth + 2;
        var sizeAt = kindAt + 9;
        var removeAt = sizeAt + 8 + 2;

        return (nameAt, nameWidth, kindAt, sizeAt, removeAt, removeAt + 3);
    }

    private static string Flat(string text) => text.ReplaceLineEndings(" ");

    /// <summary>The description in quotes, cut to <paramref name="room" /> with … inside the closing quote.</summary>
    private static string Quoted(string description, int room)
    {
        var flat = Flat(description).Trim();

        return Glyphs.Columns(flat) + 2 <= room ? $"“{flat}”" : $"“{Glyphs.Cut(flat, room - 3).TrimEnd()}…”";
    }

    private const string Retry = "retry (r)";

    private static List<(string Text, Role Role)> StateSpans(Attachment item)
    {
        switch (item.State)
        {
            case State.Uploading(var done):
                var filled = (int)Math.Round(done * 8);
                return [(new string('█', filled), Role.Gauge), (new string('░', 8 - filled), Role.GaugeEmpty), ($" {done,4:P0}", Role.Muted)];

            case State.Processing:
                var spin = "⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏"[(int)(DateTime.Now.TimeOfDay.TotalMilliseconds / 100) % 10];
                return [($"{spin} processing", Role.Loading)];

            case State.Refused(var why, _):
                return item.State is State.Refused { Retryable: false }
                    ? [($"✗ {why}", Role.Error)]
                    : [($"✗ {why}", Role.Error), ("  ", Role.Body), (Retry, Role.Key)];
        }

        return [];
    }

    private void PaintState(int x, int y, Attachment item, int index)
    {
        foreach (var (text, role) in StateSpans(item))
        {
            if (text == Retry) _hits.Insert(0, (new Rectangle(x, y, Retry.Length, 1), Hit.Retry, index));

            x = Spans(x, y, (text, role));
        }
    }

    protected override bool OnKeyDown(Key key)
    {
        if (Proto.Global(key)) return true;

        var items = Draft.Items;

        if (key == Key.CursorUp)
        {
            if (Cursor > -1) Cursor--;
            else Up?.Invoke();
        }
        else if (key == Key.CursorDown)
        {
            if (Cursor < Last) Cursor++;
            else Down?.Invoke();
        }
        else if ((key == Key.CursorUp.WithShift || key == Key.CursorDown.WithShift) && Cursor >= 0)
        {
            Cursor = Draft.Move(Cursor, key == Key.CursorUp.WithShift ? -1 : 1);
        }
        else if ((key == Key.Delete || key == Key.Backspace) && Cursor >= 0 && Cursor < items.Count)
        {
            var gone = items[Cursor];
            Draft.Remove(gone);
            Cursor = Math.Min(Cursor, Last);
            Proto.Say($"removed {gone.Name}");
        }
        else if (key == Key.Enter)
        {
            if (Cursor >= 0 && Cursor < items.Count) Proto.Describe(items[Cursor]);
            else if (Mode == AreaMode.Summary && items.Count > 0) Proto.Shell.Push(new ManageScreen());
            else Proto.OpenBrowser();
        }
        else if (key == Key.S)
        {
            if (items.Count > 0) Proto.ToggleSensitive();
        }
        else if (key == Key.Z.WithCtrl)
        {
            if (Draft.Restore() is { } back)
            {
                Cursor = back;
                Proto.Say($"brought back {items[back].Name}");
            }
        }
        else if (key == Key.R && Cursor >= 0 && Cursor < items.Count)
        {
            Draft.Retry(items[Cursor]);
        }
        else if (key == Key.W.WithCtrl && Warn is not null)
        {
            Warn();
        }
        else if (key == Key.Esc && Mode == AreaMode.Manage)
        {
            Proto.Shell.Pop();
        }
        else if (!ComposeScreen.Shared(key, _ => Proto.Say("text pastes into the editor or the warning")))
        {
            return base.OnKeyDown(key);
        }

        SetNeedsDraw();
        Proto.Shell.SetNeedsDraw();

        return true;
    }

    protected override bool OnMouseEvent(Mouse mouse)
    {
        if (mouse.Position is not { } at) return base.OnMouseEvent(mouse);

        var flags = mouse.Flags;

        if (flags.HasFlag(MouseFlags.LeftButtonPressed) && !flags.HasFlag(MouseFlags.PositionReport) && _dragFrom is null)
        {
            SetFocus();
            _pressedAt = at;

            if (Pointed(at) is { Hit: Hit.Row } row)
            {
                _dragFrom = row.Index;
                _dragOver = row.Index;
                Cursor = row.Index;
                App?.Mouse.GrabMouse(this);
            }

            SetNeedsDraw();
            return true;
        }

        if (flags.HasFlag(MouseFlags.PositionReport) && _dragFrom is not null)
        {
            // The row itself moves as the pointer does, the others making way, so where it is is where it lands.
            var over = Math.Clamp(Enumerable.Range(0, Draft.Items.Count).LastOrDefault(index => RowTop(index) <= at.Y), 0, Last);

            if (_dragFrom is { } now && over != now)
            {
                Draft.MoveTo(now, over);
                _dragFrom = over;
                Cursor = over;
                _dragMoved = true;
            }

            SetNeedsDraw();
            return true;
        }

        if (flags.HasFlag(MouseFlags.LeftButtonReleased))
        {
            if (_dragFrom is not null)
            {
                _dragFrom = null;
                App?.Mouse.UngrabMouse();

                if (_dragMoved)
                {
                    _dragMoved = false;
                    Proto.Say("moved by dragging");
                    SetNeedsDraw();
                    return true;
                }
            }

            if (Pointed(at) is { } hit && Pointed(_pressedAt) is { } pressed && pressed == hit)
            {
                Clicked(hit.Hit, hit.Index);
            }

            SetNeedsDraw();
            return true;
        }

        if (flags.HasFlag(MouseFlags.LeftButtonDoubleClicked))
        {
            // Anywhere on a row, as a mail client opens an attachment.
            if (Pointed(at) is { Hit: Hit.Row or Hit.Describe } row && row.Index < Draft.Items.Count)
            {
                Proto.Describe(Draft.Items[row.Index]);
            }

            return true;
        }

        if (flags.HasFlag(MouseFlags.LeftButtonClicked))
        {
            return true;
        }

        return base.OnMouseEvent(mouse);
    }

    private (Rectangle Where, Hit Hit, int Index)? Pointed(Point at) =>
        _hits.FirstOrDefault(hit => hit.Where.Contains(at)) is { Where.Width: > 0 } hit ? hit : null;

    private void Clicked(Hit hit, int index)
    {
        var items = Draft.Items;

        switch (hit)
        {
            case Hit.Attach:
                Proto.OpenBrowser();
                break;
            case Hit.Manage:
                Proto.Shell.Push(new ManageScreen());
                break;
            case Hit.Sensitive:
                Proto.ToggleSensitive();
                break;
            case Hit.Remove when index < items.Count:
                var gone = items[index];
                Draft.Remove(gone);
                Cursor = Math.Min(Cursor, Last);
                Proto.Say($"removed {gone.Name}");
                break;
            case Hit.Retry when index < items.Count:
                Draft.Retry(items[index]);
                break;
            case Hit.Row when index < items.Count:
                Cursor = index;
                break;
            case Hit.Describe when index < items.Count:
                Proto.Describe(items[index]);
                break;
        }
    }
}

/// <summary>Layout B: drawn thumbnails in a strip between the editor and the footer.</summary>
internal sealed class StripArea : Painted
{
    public const int Rows = 8;

    private const int TileWidth = 18;

    private int? _dragFrom;

    private int _dragOver;

    private Point _pressedAt;

    public int Cursor { get; set; }

    public Action? Up { get; set; }

    private static Draft Draft => Proto.Draft;

    private int Tiles => Draft.Items.Count + (Draft.Items.Count < Instance.Most ? 1 : 0);

    public string Hints => "←/→ walk · enter describe · del remove · ctrl-z undo remove · shift-←/→ reorder (or drag) · r retry · ↑ editor";

    private int TileAt(int x) => (x - Geometry.Pad) / TileWidth;

    protected override void Paint()
    {
        var items = Draft.Items;

        for (var index = 0; index < Tiles; index++)
        {
            var left = Geometry.Pad + index * TileWidth;

            if (left + TileWidth > Viewport.Width) break;

            var current = HasFocus && Cursor == index;
            var thumb = new Rectangle(left, 0, TileWidth - 2, 5);

            if (index == items.Count)
            {
                for (var row = 0; row < 5; row++) Put(left, row, new string(row is 0 or 4 ? '┄' : ' ', TileWidth - 2), Role.PanelBorder);
                Put(left + 3, 2, "＋ attach…", current ? Role.SelectedText : Role.Link);
                Put(left + 5, 5, "ctrl-o", Role.Muted);
            }
            else
            {
                var item = items[index];
                Pics.Paint(this, thumb, item.Path, Theme);
                Put(left, 5, item.Name, current ? Role.SelectedText : Role.Body, TileWidth - 4);
                Put(left + TileWidth - 4, 5, " ×", Role.Destructive);

                var (said, role) = item.State switch
                {
                    State.Uploading(var done) => ($"{new string('█', (int)(done * 8))}{new string('░', 8 - (int)(done * 8))} {done:P0}", Role.Gauge),
                    State.Processing => ("processing…", Role.Loading),
                    State.Refused(var why, _) => ($"✗ {why}", Role.Error),
                    _ => item.Description.Trim().Length > 0 ? ($"“{item.Description.ReplaceLineEndings(" ")}”", Role.Body) : (Proto.Mark, Role.Muted),
                };
                Put(left, 6, said, role, TileWidth - 2);
            }

            if (current) Put(left, 7, new string('▔', TileWidth - 2), Role.Selection);
        }

        if (_dragFrom is { } from && _dragOver != from)
        {
            var at = Geometry.Pad + (_dragOver > from ? _dragOver + 1 : _dragOver) * TileWidth - 2;
            for (var row = 0; row < 7; row++) Put(Math.Max(0, at), row, "┃", Role.Selection);
        }
    }

    protected override bool OnKeyDown(Key key)
    {
        if (Proto.Global(key)) return true;

        var items = Draft.Items;

        if (key == Key.CursorLeft) Cursor = Math.Max(0, Cursor - 1);
        else if (key == Key.CursorRight) Cursor = Math.Min(Tiles - 1, Cursor + 1);
        else if (key == Key.CursorUp) Up?.Invoke();
        else if ((key == Key.CursorLeft.WithShift || key == Key.CursorRight.WithShift) && Cursor < items.Count)
            Cursor = Draft.Move(Cursor, key == Key.CursorLeft.WithShift ? -1 : 1);
        else if ((key == Key.Delete || key == Key.Backspace) && Cursor < items.Count)
        {
            var gone = items[Cursor];
            Draft.Remove(gone);
            Proto.Say($"removed {gone.Name}");
            if (items.Count == 0) Up?.Invoke();
            Cursor = Math.Min(Cursor, Tiles - 1);
        }
        else if (key == Key.Enter)
        {
            if (Cursor < items.Count) Proto.Describe(items[Cursor]);
            else Proto.OpenBrowser();
        }
        else if (key == Key.Z.WithCtrl)
        {
            if (Draft.Restore() is { } back)
            {
                Cursor = back;
                Proto.Say($"brought back {items[back].Name}");
            }
        }
        else if (key == Key.R && Cursor < items.Count) Draft.Retry(items[Cursor]);
        else if (key == Key.S) Proto.ToggleSensitive();
        else if (!ComposeScreen.Shared(key, _ => Proto.Say("text pastes into the editor or the warning")))
            return base.OnKeyDown(key);

        SetNeedsDraw();
        Proto.Shell.SetNeedsDraw();

        return true;
    }

    protected override bool OnMouseEvent(Mouse mouse)
    {
        if (mouse.Position is not { } at) return base.OnMouseEvent(mouse);

        var flags = mouse.Flags;
        var items = Draft.Items;
        var tile = TileAt(at.X);

        if (flags.HasFlag(MouseFlags.LeftButtonPressed) && !flags.HasFlag(MouseFlags.PositionReport) && _dragFrom is null)
        {
            _pressedAt = at;

            if (tile >= 0 && tile < Tiles)
            {
                Cursor = tile;
                SetFocus();
            }

            if (tile >= 0 && tile < items.Count)
            {
                _dragFrom = tile;
                _dragOver = tile;
                App?.Mouse.GrabMouse(this);
            }

            SetNeedsDraw();
            return true;
        }

        if (flags.HasFlag(MouseFlags.PositionReport) && _dragFrom is not null)
        {
            _dragOver = Math.Clamp(tile, 0, items.Count - 1);
            SetNeedsDraw();
            return true;
        }

        if (flags.HasFlag(MouseFlags.LeftButtonReleased))
        {
            if (_dragFrom is { } from)
            {
                _dragFrom = null;
                App?.Mouse.UngrabMouse();

                if (_dragOver != from)
                {
                    Draft.MoveTo(from, _dragOver);
                    Cursor = _dragOver;
                    Proto.Say("moved by dragging");
                    return true;
                }
            }

            if (TileAt(_pressedAt.X) == tile && tile >= 0 && tile < Tiles)
            {
                var left = Geometry.Pad + tile * TileWidth;

                if (tile == items.Count) Proto.OpenBrowser();
                else if (at.Y == 5 && at.X >= left + TileWidth - 4) Draft.Remove(items[tile]);
                else if (at.Y == 6 && items[tile].State is State.Refused) Draft.Retry(items[tile]);
                else Proto.Describe(items[tile]);
            }

            SetNeedsDraw();
            return true;
        }

        if (flags.HasFlag(MouseFlags.LeftButtonClicked) || flags.HasFlag(MouseFlags.LeftButtonDoubleClicked)) return true;

        return base.OnMouseEvent(mouse);
    }
}

/// <summary>Layout C's attachments screen: the header, and a row per attachment with a bigger picture.</summary>
internal sealed class ManageScreen : Screen
{
    private readonly AttachArea _area = new(AreaMode.Manage) { X = 0, Y = 2, Width = Dim.Fill(), Height = Dim.Fill(1) };

    public ManageScreen()
    {
        _area.Cursor = Proto.Draft.Items.Count > 0 ? 0 : -1;
        Add(_area);
    }

    public override string Hints => "esc back · " + _area.Hints;

    public override void Shown() => _area.SetFocus();

    public override void Relaid()
    {
        _area.Cursor = Math.Min(_area.Cursor, _area.Last);
        SetNeedsDraw();
    }

    protected override void Paint()
    {
        Spans(Geometry.Pad, 0, ("Compose", Role.Muted), (" › ", Role.Muted), ("Attachments", Role.PanelTitle));
        Put(Geometry.Pad, 1, new string('─', Math.Max(0, Viewport.Width - Geometry.Pad * 2)), Role.PanelBorder);
    }
}
