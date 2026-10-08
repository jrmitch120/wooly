// PROTOTYPE (#374) — throwaway. The file browser: a screen on the stack, over the real file system.

using System.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Prototype.Attachments;

internal sealed class BrowserScreen : Screen
{
    private const int ListTop = 4;

    private readonly HashSet<string> _chosen = [];

    private string _folder = Directory.Exists(Proto.Folder) ? Proto.Folder : Environment.CurrentDirectory;

    private string _filter = "";

    private bool _everyFile;

    private List<Entry> _entries = [];

    private int _cursor;

    private int _top;

    private sealed record Entry(string Path, string Name, bool Folder, bool Up, long Bytes, DateTime Changed)
    {
        public bool Takeable => !Folder && (Instance.Of(Path) is not null);
    }

    public BrowserScreen() => Read();

    private static int Room => Instance.Most - Proto.Draft.Items.Count;

    public override string Hints =>
        $"type to filter · space choose · enter {(_chosen.Count > 0 ? $"attach {_chosen.Count}" : "open / attach")} · ← up a folder · ctrl-a {(_everyFile ? "accepted only" : "every file")} · esc back";

    private int ListHeight => Math.Max(1, Viewport.Height - ListTop - 1);

    private int ListWidth => Viewport.Width >= 90 ? Viewport.Width * 11 / 20 : Viewport.Width;

    private void Read()
    {
        var entries = new List<Entry>();

        try
        {
            var here = new DirectoryInfo(_folder);

            if (here.Parent is { } parent)
            {
                entries.Add(new Entry(parent.FullName, "..", true, true, 0, parent.LastWriteTime));
            }

            entries.AddRange(here.EnumerateDirectories()
                .Where(folder => !Hidden(folder))
                .OrderBy(folder => folder.Name, StringComparer.OrdinalIgnoreCase)
                .Select(folder => new Entry(folder.FullName, folder.Name, true, false, 0, folder.LastWriteTime)));

            entries.AddRange(here.EnumerateFiles()
                .Where(file => !Hidden(file) && (_everyFile || Instance.Of(file.FullName) is not null))
                .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
                .Select(file => new Entry(file.FullName, file.Name, false, false, file.Length, file.LastWriteTime)));
        }
        catch (Exception failure) when (failure is UnauthorizedAccessException or IOException)
        {
            Proto.Say($"can't read {_folder}: {failure.Message}");
        }

        _entries = entries;

        if (_cursor == 0 && entries.Count > 1 && entries[0].Up) _cursor = 1;
        Filtered();
    }

    private static bool Hidden(FileSystemInfo info) =>
        info.Name.StartsWith('.') || info.Attributes.HasFlag(FileAttributes.Hidden);

    private List<Entry> _shown = [];

    private void Filtered()
    {
        // Fuzzy, the way fzf matches: the letters typed, in order, anywhere in the name — so "ss0229" finds
        // "Screenshot 2024-02-29 at 9.31.07 AM.png" and space never has to be typed, leaving it to choose. Closest
        // matches first, folders still ahead of files.
        _shown = _filter.Length == 0
            ? _entries
            :
            [
                .. _entries.Where(entry => entry.Up),
                .. _entries.Where(entry => !entry.Up)
                           .Select(entry => (Entry: entry, Score: Fuzzy(entry.Name, _filter)))
                           .Where(match => match.Score is not null)
                           .OrderBy(match => match.Entry.Folder ? 0 : 1)
                           .ThenByDescending(match => match.Score)
                           .ThenBy(match => match.Entry.Name, StringComparer.OrdinalIgnoreCase)
                           .Select(match => match.Entry),
            ];

        // On the closest match whenever the filter changes, so enter takes it.
        _cursor = _filter.Length > 0
            ? _shown.FindIndex(entry => !entry.Up) is >= 0 and var first ? first : 0
            : Math.Clamp(_cursor, 0, Math.Max(0, _shown.Count - 1));

        Scrolled();
        SetNeedsDraw();
    }

    /// <summary>
    ///     How well <paramref name="filter" /> matches <paramref name="name" />, higher being closer, or null where
    ///     its letters are not all there in order. Scored the way fzf and VS Code score: each letter found earns a
    ///     little, a letter right after the one before earns more, a letter that starts a word (or a run of digits)
    ///     earns more again, and every letter skipped between two matches costs a little. The best placing of the
    ///     letters is found, not the first.
    /// </summary>
    private static int? Fuzzy(string name, string filter)
    {
        const int Found = 16, Adjacent = 24, WordStart = 12, Gap = 1;
        const int Never = int.MinValue / 2;

        var n = name.Length;
        var m = filter.Length;

        if (m == 0) return 0;
        if (m > n) return null;

        // best[j]: the best score with the letters so far matched and the last of them at name[j].
        var best = new int[n];
        var next = new int[n];

        for (var i = 0; i < m; i++)
        {
            var wanted = char.ToLowerInvariant(filter[i]);
            var carried = Never; // the best of best[k] - Gap·(j - k - 1) over k < j - 1, kept as j moves right

            for (var j = 0; j < n; j++)
            {
                next[j] = Never;

                if (i > 0 && j > 1) carried = Math.Max(carried - Gap, best[j - 2]);

                if (char.ToLowerInvariant(name[j]) != wanted) continue;

                var bonus = Found + (StartsWord(name, j) ? WordStart : 0);

                if (i == 0)
                {
                    next[j] = bonus;
                }
                else
                {
                    var after = j > 0 && best[j - 1] > Never ? best[j - 1] + Adjacent : Never;
                    var skipped = carried > Never ? carried - Gap : Never;
                    var before = Math.Max(after, skipped);

                    if (before > Never) next[j] = before + bonus;
                }
            }

            (best, next) = (next, best);
        }

        var top = best.Max();

        return top > Never ? top : null;
    }

    private static bool StartsWord(string name, int at) =>
        at == 0
        || !char.IsLetterOrDigit(name[at - 1])
        || (char.IsDigit(name[at]) && !char.IsDigit(name[at - 1]))
        || (char.IsUpper(name[at]) && char.IsLower(name[at - 1]));

    private void Scrolled()
    {
        // Not before the screen has a height: a list one row tall scrolls ".." out of sight.
        if (Viewport.Height <= ListTop) return;

        if (_cursor < _top) _top = _cursor;
        if (_cursor >= _top + ListHeight) _top = _cursor - ListHeight + 1;
        _top = Math.Max(0, _top);
    }

    private void Open(string folder)
    {
        var came = _folder;
        _folder = folder;
        _filter = "";
        _cursor = 0;
        _top = 0;
        Read();

        // Going up lands on the folder just left.
        if (_entries.FindIndex(entry => !entry.Up && entry.Path == came) is >= 0 and var at) _cursor = at;

        Scrolled();
    }

    private void Choose(Entry entry)
    {
        if (entry.Folder) return;

        if (_chosen.Remove(entry.Path)) return;

        if (_chosen.Count >= Room)
        {
            Proto.Say($"{Instance.Most} is the most a post can carry — {Room} more fit");
            return;
        }

        _chosen.Add(entry.Path);
    }

    private void Take(Entry? entry)
    {
        if (entry is { Folder: true })
        {
            Open(entry.Path);
            return;
        }

        var paths = _chosen.Count > 0 ? _chosen.ToList() : entry is not null ? [entry.Path] : [];

        if (paths.Count == 0) return;

        Proto.Folder = _folder;
        Proto.Shell.Pop();
        Proto.Attach(paths, paths.Count == 1 ? $"{Path.GetFileName(paths[0])}" : "files");
    }

    private Rectangle _showingAt;

    private void EveryFile()
    {
        _everyFile = !_everyFile;
        Read();
        Proto.Say(_everyFile ? "showing every file — the instance refuses types it doesn't accept" : "showing the types this instance accepts");
    }

    private Entry? Current => _cursor < _shown.Count ? _shown[_cursor] : null;

    protected override bool OnKeyDown(Key key)
    {
        if (Proto.Global(key)) return true;

        if (key == Key.Esc)
        {
            if (_filter.Length > 0)
            {
                _filter = "";
                Filtered();
            }
            else
            {
                Proto.Shell.Pop();
            }
        }
        else if (key == Key.CursorDown) Move(1);
        else if (key == Key.CursorUp) Move(-1);
        else if (key == Key.PageDown) Move(ListHeight);
        else if (key == Key.PageUp) Move(-ListHeight);
        else if (key == Key.Home) Move(-_shown.Count);
        else if (key == Key.End) Move(_shown.Count);
        else if (key == Key.Enter) Take(Current);
        else if (key == Key.CursorRight && Current is { Folder: true } folder) Open(folder.Path);
        else if (key == Key.CursorLeft)
        {
            if (Directory.GetParent(_folder) is { } parent) Open(parent.FullName);
        }
        else if (key == Key.Backspace || key == Key.Delete)
        {
            // Only ever the filter: held down to clear it, it stops at empty rather than walking up the folders.
            if (_filter.Length > 0)
            {
                _filter = _filter[..^1];
                Filtered();
            }
        }
        else if (key == Key.Space)
        {
            if (Current is { } entry)
            {
                Choose(entry);
                Move(1);
            }
        }
        else if (key == Key.A.WithCtrl)
        {
            EveryFile();
        }
        else if (key == Key.V.WithCtrl)
        {
            Proto.Say("ctrl-v attaches on the compose screen; here it would paste into the filter");
        }
        else if (key.AsRune.Value is var rune and > 31 && !key.IsCtrl && !key.IsAlt && rune != 127)
        {
            _filter += char.ConvertFromUtf32(rune);
            Filtered();
        }
        else
        {
            return base.OnKeyDown(key);
        }

        SetNeedsDraw();
        Proto.Shell.SetNeedsDraw();

        return true;
    }

    private void Move(int by)
    {
        _cursor = Math.Clamp(_cursor + by, 0, Math.Max(0, _shown.Count - 1));
        Scrolled();
        SetNeedsDraw();
    }

    protected override bool OnMouseEvent(Mouse mouse)
    {
        if (mouse.Position is not { } at) return base.OnMouseEvent(mouse);

        var flags = mouse.Flags;

        if (flags.HasFlag(MouseFlags.WheeledDown) || flags.HasFlag(MouseFlags.WheeledUp))
        {
            if (flags.HasFlag(MouseFlags.WheeledLeft) || flags.HasFlag(MouseFlags.WheeledRight)) return true;

            var by = flags.HasFlag(MouseFlags.WheeledDown) ? 3 : -3;
            _top = Math.Clamp(_top + by, 0, Math.Max(0, _shown.Count - ListHeight));
            _cursor = Math.Clamp(_cursor, _top, Math.Max(_top, Math.Min(_shown.Count - 1, _top + ListHeight - 1)));
            SetNeedsDraw();
            return true;
        }

        var row = at.Y - ListTop + _top;
        var onList = at.Y >= ListTop && at.X < ListWidth && row >= 0 && row < _shown.Count;

        if (flags.HasFlag(MouseFlags.LeftButtonDoubleClicked) && onList)
        {
            _cursor = row;
            var entry = _shown[row];

            if (entry.Folder) Open(entry.Path);
            else
            {
                _chosen.Remove(entry.Path);
                Take(entry);
            }

            return true;
        }

        if (flags.HasFlag(MouseFlags.LeftButtonClicked))
        {
            SetFocus();

            if (_showingAt.Contains(at))
            {
                EveryFile();
            }
            else if (onList)
            {
                _cursor = row;

                // The box, or a click with ctrl or shift: choose. A plain click on a name only moves.
                if (at.X <= Geometry.Pad + 3 || flags.HasFlag(MouseFlags.Ctrl) || flags.HasFlag(MouseFlags.Shift)) Choose(_shown[row]);
            }
            else if (at.Y == Viewport.Height - 1 && _chosen.Count > 0)
            {
                Take(null);
            }

            SetNeedsDraw();
            return true;
        }

        return flags.HasFlag(MouseFlags.LeftButtonPressed) || flags.HasFlag(MouseFlags.LeftButtonReleased) || base.OnMouseEvent(mouse);
    }

    protected override void Paint()
    {
        Scrolled();

        var width = Viewport.Width;
        var listWidth = ListWidth;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var shownFolder = _folder.StartsWith(home) ? "~" + _folder[home.Length..] : _folder;

        Spans(Geometry.Pad, 0, ("Compose", Role.Muted), (" › ", Role.Muted), ("Attach", Role.PanelTitle), ("  " + shownFolder, Role.Body));

        var filterEnd = Spans(Geometry.Pad, 1, ("filter ", Role.Muted), (_filter, Role.Body), ("▏", Role.Selection));
        if (_filter.Length == 0) Put(filterEnd, 1, "type to filter", Role.Muted);

        var showing = _everyFile ? "every file · ctrl-a accepted only" : "pictures, video, sound · ctrl-a every file";
        var showingAt = width - Geometry.Pad - Glyphs.Columns(showing);
        Put(showingAt, 1, showing, Role.Muted);
        _showingAt = new Rectangle(showingAt, 1, Glyphs.Columns(showing), 1);

        Put(Geometry.Pad, 2, new string('─', Math.Max(0, width - Geometry.Pad * 2)), Role.PanelBorder);

        for (var line = 0; line < ListHeight; line++)
        {
            var index = _top + line;

            if (index >= _shown.Count) break;

            var entry = _shown[index];
            var y = ListTop + line;
            var current = index == _cursor && HasFocus;

            if (current) Put(Geometry.Pad - 1, y, "▌", Role.Selection); // against the box, as on the Media rows

            var box = entry.Up ? "◂ " : entry.Folder ? "▸ " : _chosen.Contains(entry.Path) ? "☑ " : "☐ ";
            var name = entry.Folder && !entry.Up ? entry.Name + "/" : entry.Name;
            var role = current ? Role.SelectedText : entry.Folder ? Role.Link : entry.Takeable ? Role.Body : Role.Muted;

            Put(Geometry.Pad, y, box, _chosen.Contains(entry.Path) ? Role.Selection : Role.Muted);

            var detail = entry.Folder ? "" : $"{Instance.Of(entry.Path)?.ToString().ToLowerInvariant() ?? "not accepted"}  {Size(entry.Bytes),8}  {entry.Changed:MMM d}";
            var room = listWidth - Geometry.Pad - 2 - Glyphs.Columns(detail) - 3;

            Put(Geometry.Pad + 2, y, name, role, Math.Max(4, room));
            Put(listWidth - 1 - Glyphs.Columns(detail), y, detail, Role.Muted);
        }

        if (_shown.Count == 0 || (_shown.Count == 1 && _shown[0].Up))
        {
            Put(Geometry.Pad + 2, ListTop + _shown.Count, _filter.Length > 0 ? $"nothing here matches \"{_filter}\"" : "nothing this instance accepts in here", Role.Muted);
        }

        if (listWidth < width && Current is { } under)
        {
            var preview = new Rectangle(listWidth + 2, ListTop, width - listWidth - 2 - Geometry.Pad, Math.Max(1, ListHeight - 3));

            for (var y = ListTop - 1; y < Viewport.Height - 1; y++) Put(listWidth, y, "│", Role.PanelBorder);

            if (under.Folder)
            {
                Put(preview.X, preview.Y, under.Up ? "up a folder" : "a folder · enter or → opens it", Role.Muted);
            }
            else if (Instance.Of(under.Path) is not null)
            {
                var drawn = Pics.Paint(this, preview, under.Path, Theme);
                var image = Pics.Loaded(under.Path);
                var said = image is null ? under.Name : $"{under.Name} · {image.Width}×{image.Height}";
                Put(preview.X, drawn.Bottom + 1, said, Role.Muted, preview.Width);
            }
            else
            {
                Put(preview.X, preview.Y, "not a type this instance accepts", Role.Muted);
            }
        }

        var footer = _chosen.Count > 0
            ? $"{_chosen.Count} chosen · {Room - _chosen.Count} more fit · enter (or click here) attaches them"
            : $"{Room} more fit on this post";
        Put(Geometry.Pad, Viewport.Height - 1, footer, _chosen.Count > 0 ? Role.Body : Role.Muted);
    }

    private static string Size(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F0} KB",
        _ => $"{bytes / 1024.0 / 1024.0:F1} MB",
    };
}
