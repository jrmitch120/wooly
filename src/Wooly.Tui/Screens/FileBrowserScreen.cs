using System.Globalization;
using Wooly.Core.Posts;
using Wooly.Tui.Rendering;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     The file browser (#376): a screen pushed over a compose or a reply to choose files to attach from, working on the
///     real file system, in the look the prototype settled (#374). It lists a folder's subfolders and the files the
///     instance accepts, filters them fuzzily as the author types, and lets several be chosen at once, up to what the
///     post still has room for.
/// </summary>
/// <remarks>
///     A screen like any other on the stack (ADR-0015's rule for anything new), and as inert as the compose screen
///     under it: it reads the folder it is shown, which is the local machine's rather than an instance's (ADR-0020),
///     and says what is chosen — attaching it is the shell's, which does it the way a drop does (#375).
///     <para>
///         It takes letters, all of them, into its filter: so <c>space</c>, which a name never needs typed since the
///         filter is fuzzy, chooses, and <c>backspace</c> only ever edits the filter — held down, it stops at empty
///         rather than walking up the folders.
///     </para>
/// </remarks>
public sealed class FileBrowserScreen : Screen
{
    /// <summary>The columns left blank either side, as on the compose screen under it (#317).</summary>
    private const int Pad = 2;

    /// <summary>
    ///     The narrowest a terminal can be and still give the picture under the cursor a pane of its own beside the
    ///     list (#382). Narrower, the list has every column.
    /// </summary>
    private const int LeastForAPreview = 90;

    /// <summary>The fewest columns the folder is cut to before how many are chosen gives way to it.</summary>
    private const int LeastFolder = 12;

    /// <summary>The kind column's width, the word and the gap after it.</summary>
    private const int KindColumn = 9;

    /// <summary>The size column's width, right-aligned.</summary>
    private const int SizeColumn = 8;

    /// <summary>The date column's width, right-aligned.</summary>
    private const int DateColumn = 6;

    /// <summary>What a click on one of a row's runs means, carried on the run (<see cref="Span.Item" />).</summary>
    private enum Part
    {
        /// <summary>The box a file is chosen by.</summary>
        Box = 1,

        /// <summary>The label that shows every file, or only the accepted ones again.</summary>
        EveryFile,
    }

    /// <summary>What the instance accepts, and so which files are listed.</summary>
    private readonly PostLimits _limits;

    /// <summary>How many more the post has room for, which is as many as may be chosen.</summary>
    private readonly int _room;

    /// <summary>What is chosen, in the order it was chosen, which is the order it is attached in.</summary>
    private readonly List<string> _chosen = [];

    /// <summary>Everything listed in the folder, before the filter: <c>..</c>, the folders, then the files.</summary>
    private List<Entry> _entries = [];

    /// <summary>What the filter leaves, with the cursor on one of them.</summary>
    private Picked<Entry> _walking = new([]);

    /// <param name="folder">The folder it opens in.</param>
    /// <param name="limits">What the instance accepts.</param>
    /// <param name="room">How many more the post has room for.</param>
    public FileBrowserScreen(string folder, PostLimits limits, int room)
    {
        _limits = limits;
        _room = room;

        Open(folder);
    }

    /// <summary>The folder being shown.</summary>
    public string Folder { get; private set; } = string.Empty;

    /// <summary>What has been typed to narrow the list, or empty where nothing narrows it.</summary>
    public string Filter { get; private set; } = string.Empty;

    /// <summary>Whether every file is listed, rather than only the types the instance accepts.</summary>
    public bool EveryFile { get; private set; }

    /// <inheritdoc />
    public override string Crumb => "Attach";

    /// <inheritdoc />
    /// <remarks>Always: every letter is the filter's, and space is the choosing.</remarks>
    public override bool IsTyping => true;

    /// <inheritdoc />
    protected override IPicked Walking => _walking;

    /// <inheritdoc />
    /// <remarks>
    ///     None of them needs a pick (#195): the list always holds <c>..</c> to stand on, but at the very root of the
    ///     disk.
    /// </remarks>
    protected override IReadOnlyList<KeyHint> OwnKeys =>
    [
        new("space", "choose"),
        new("⏎", "attach"),
        new("←→", "folder"),
        new("ctrl-a", EveryFile ? "accepted only" : "every file"),
        new("esc", Filter.Length > 0 ? "clear" : "back"),
    ];

    /// <summary>The entry under the cursor, or none where the filter left nothing.</summary>
    private Entry? Current => _walking.Out;

    /// <inheritdoc />
    /// <remarks>A space chooses the file under the cursor and moves on; anything else narrows the list.</remarks>
    public override void Type(char letter)
    {
        if (letter == ' ')
        {
            Choose();
            Move(1);

            return;
        }

        Filter += letter;
        Narrowed();
    }

    /// <inheritdoc />
    public override void Backspace()
    {
        Filter = Backspaced(Filter);
        Narrowed();
    }

    /// <inheritdoc />
    public override bool ClearFilter()
    {
        if (Filter.Length == 0)
        {
            return false;
        }

        Filter = string.Empty;
        Narrowed();

        return true;
    }

    /// <inheritdoc />
    /// <remarks>The browser's own keys and clicks, which change nothing but what it shows and what is chosen.</remarks>
    public override Task Answer(Verb verb, Reach reach)
    {
        switch (verb)
        {
            case Verb.Choose:
                Choose();

                break;

            case Verb.IntoFolder when Current is { Folder: true } folder:
                Open(folder.Path);

                break;

            case Verb.UpFolder when Directory.GetParent(Folder) is { } parent:
                Open(parent.FullName);

                break;

            case Verb.EveryFile:
                EveryFile = !EveryFile;
                Read();

                break;

            default:
                return Task.CompletedTask;
        }

        reach.Changed();

        return Task.CompletedTask;
    }

    /// <summary>
    ///     A click on one of this screen's rows, where it means more than putting the cursor there (#376): on a box, or
    ///     with ctrl or shift held, it chooses; on the label it shows every file.
    /// </summary>
    public override Verb Clicked(int? item, int? part, bool chorded) => (Part?)part switch
    {
        Part.EveryFile => Verb.EveryFile,
        Part.Box when item is not null => Verb.Choose,
        _ when chorded && item is not null => Verb.Choose,
        _ => Verb.None,
    };

    /// <summary>
    ///     <c>⏎</c>: opens the folder under the cursor and answers nothing; otherwise answers what is to be attached —
    ///     what is chosen, in the order chosen, or the file under the cursor where nothing is.
    /// </summary>
    public IReadOnlyList<string> Take()
    {
        if (Current is { Folder: true } folder)
        {
            Open(folder.Path);

            return [];
        }

        return _chosen.Count > 0 ? [.. _chosen] : Current is { } file ? [file.Path] : [];
    }

    /// <inheritdoc />
    /// <remarks>
    ///     The folder and how many are chosen; the filter, and which files are listed; a rule; then a row per entry, the
    ///     cursor's marked with the selection bar against its box. On a wide terminal the list keeps to the left and
    ///     leaves a pane on the right, top left of which the picture under the cursor goes (#382).
    /// </remarks>
    public override IReadOnlyList<Line> Lines(Drawing drawing)
    {
        var width = drawing.Width;
        var list = width >= LeastForAPreview ? width * 11 / 20 : width;
        var lines = new List<Line>
        {
            FolderRow(width),
            Spread(width, FilterSpans(), Listing(shortly: false), Listing(shortly: true)),
            Line.Of(Gap(Pad), new Span(new string('─', Math.Max(0, width - (Pad * 2))), Role.PanelBorder)),
        };

        for (var at = 0; at < _walking.Count; at++)
        {
            var row = Row(_walking.All[at], at, list);

            lines.Add(list < width ? row.Respanned([.. row.Spans, new Span("│", Role.PanelBorder), .. Preview]) : row);
        }

        if (_walking.All.All(entry => entry.Up))
        {
            lines.Add(Line.Of(
                Gap(Pad + 2),
                new Span(
                    Filter.Length > 0 ? $"nothing here matches \"{Filter}\"" : "nothing this instance accepts in here",
                    Role.Muted)));
        }

        return lines;
    }

    /// <summary>
    ///     What the preview pane says beside a row of the list: nothing yet. The picture under the cursor goes top left
    ///     in it, level with the first row, with its name and size under it (#382).
    /// </summary>
    private static Span[] Preview => [];

    /// <summary>How many are chosen and how many more fit, or how many fit where none are chosen yet.</summary>
    private string Counted => _chosen.Count > 0
        ? $"{_chosen.Count} chosen · {_room - _chosen.Count} more fit"
        : $"{_room} more fit on this post";

    /// <summary>The filter as typed, with a caret, or what typing does where nothing has been.</summary>
    private Span[] FilterSpans() => Filter.Length > 0
        ? [new Span("filter ", Role.Muted), new Span(Filter, Role.Body), new Span("▏", Role.Selection)]
        : [new Span("type to filter", Role.Muted)];

    /// <summary>
    ///     The folder, cut from its start where it is long so that where it ends is what shows, and how many are chosen
    ///     against the right.
    /// </summary>
    private Line FolderRow(int width)
    {
        var counted = new Span(Counted, Role.Muted);
        var room = width - (Pad * 2) - counted.Width - 2;
        var folder = Shown(Folder);

        if (room < LeastFolder)
        {
            return Line.Of(Gap(Pad), new Span(Tail(folder, width - (Pad * 2)), Role.BylineName));
        }

        var shown = new Span(Tail(folder, room), Role.BylineName);

        return Line.Of(Gap(Pad), shown, Gap(width - (Pad * 2) - shown.Width - counted.Width), counted);
    }

    /// <summary>
    ///     Which files are listed — the kinds the instance accepts, or every file — and the key that changes it, the
    ///     whole of it a click away from the change too. <paramref name="shortly" />, only the key, for a terminal too
    ///     narrow for the rest.
    /// </summary>
    private Span Listing(bool shortly)
    {
        var key = EveryFile ? "ctrl-a accepted only" : "ctrl-a every file";
        var kinds = _limits.MediaTypes.Select(AttachmentTypes.KindOf).ToHashSet();
        var words = new (MediaKind[] Kinds, string Word)[]
            {
                ([MediaKind.Image, MediaKind.Animation], "pictures"),
                ([MediaKind.Video], "video"),
                ([MediaKind.Audio], "sound"),
            }
            .Where(said => said.Kinds.Any(kinds.Contains))
            .Select(said => said.Word);
        var listed = EveryFile ? "every file" : string.Join(", ", words);

        return new Span(shortly ? key : $"{listed} · {key}", Role.Muted) { Item = (int)Part.EveryFile };
    }

    /// <summary>
    ///     An entry's row in <paramref name="width" /> columns: the selection bar where the cursor is, set against the
    ///     box; the box — <c>◂</c> for <c>..</c>, <c>▸</c> for a folder, <c>☐</c> or <c>☑</c> for a file — so the bar
    ///     sits against something on every row; the name; and, for a file, its kind, size and date, right-aligned.
    /// </summary>
    private Line Row(Entry entry, int at, int width)
    {
        var current = at == _walking.At;
        var chosen = _chosen.Contains(entry.Path);
        var box = entry.Up ? "◂" : entry.Folder ? "▸" : chosen ? "☑" : "☐";
        var accepted = entry.Folder || _limits.Accepts(entry.Path);
        var detail = entry.Folder
            ? string.Empty
            : Glyphs.Padded(accepted ? KindWord(entry.Path) : "file", KindColumn)
              + Size(entry.Bytes).PadLeft(SizeColumn)
              + "  "
              + entry.Changed.ToString("MMM d", CultureInfo.InvariantCulture).PadLeft(DateColumn);
        var room = Math.Max(1, width - Pad - 4 - Glyphs.Columns(detail) - (detail.Length > 0 ? 2 : 0));
        var name = entry.Folder && !entry.Up ? $"{entry.Name}/" : entry.Name;
        var role = current ? Role.SelectedText : entry.Folder ? Role.Link : accepted ? Role.Body : Role.Muted;

        return Line.Of(
                   Gap(1),
                   current ? new Span("▌", Role.Selection) : Gap(1),
                   new Span(box, chosen ? Role.Selection : Role.Muted) { Item = (int)Part.Box },
                   Gap(1),
                   new Span(Glyphs.Padded(TextWrap.Clip(name, room), room), role),
                   Gap(detail.Length > 0 ? 2 : 0),
                   new Span(detail, Role.Muted),
                   Gap(Pad))
               .PartOf(at) with { Picked = current };
    }

    /// <summary>Chooses the file under the cursor, or lets it go — but no more than the post has room for.</summary>
    private void Choose()
    {
        if (Current is not { Folder: false } file || _chosen.Remove(file.Path) || _chosen.Count >= _room)
        {
            return;
        }

        _chosen.Add(file.Path);
    }

    /// <summary>
    ///     Shows <paramref name="folder" />, the filter cleared, on its first entry under <c>..</c> — or, going up, on
    ///     the folder just left. What is chosen stays chosen.
    /// </summary>
    private void Open(string folder)
    {
        var left = Folder;

        Folder = Path.GetFullPath(folder);
        Filter = string.Empty;
        Read();

        // Going up lands on the folder just left; anywhere else, on the first entry, which Narrowed put it on.
        if (_walking.All.ToList().FindIndex(entry => !entry.Up && entry.Path == left) is >= 0 and var at)
        {
            _walking.Pick(at);
        }
    }

    /// <summary>Lists the folder again, as it is on the disk, keeping the cursor on the entry it was on.</summary>
    private void Read()
    {
        var on = Current?.Path;
        var entries = new List<Entry>();

        try
        {
            var here = new DirectoryInfo(Folder);

            if (here.Parent is { } parent)
            {
                entries.Add(new Entry(parent.FullName, "..", Folder: true, Up: true, 0, parent.LastWriteTime));
            }

            entries.AddRange(here.EnumerateDirectories()
                                 .Where(folder => !Hidden(folder))
                                 .OrderBy(folder => folder.Name, StringComparer.OrdinalIgnoreCase)
                                 .Select(folder => new Entry(
                                     folder.FullName,
                                     folder.Name,
                                     Folder: true,
                                     Up: false,
                                     0,
                                     folder.LastWriteTime)));

            entries.AddRange(here.EnumerateFiles()
                                 .Where(file => !Hidden(file) && (EveryFile || _limits.Accepts(file.FullName)))
                                 .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
                                 .Select(file => new Entry(
                                     file.FullName,
                                     file.Name,
                                     Folder: false,
                                     Up: false,
                                     file.Length,
                                     file.LastWriteTime)));
        }
        catch (Exception failed) when (failed is UnauthorizedAccessException or IOException)
        {
            // A folder that cannot be read lists only the way back up, and says there is nothing here.
        }

        _entries = entries;
        Narrowed();

        if (on is not null && _walking.All.ToList().FindIndex(entry => entry.Path == on) is >= 0 and var at)
        {
            _walking.Pick(at);
        }
    }

    /// <summary>
    ///     Lists what the filter leaves, the closest matches first and folders ahead of files, with <c>..</c> always on
    ///     top — and the cursor on the closest match, so that <c>⏎</c> takes it.
    /// </summary>
    private void Narrowed()
    {
        IReadOnlyList<Entry> shown = Filter.Length == 0
            ? _entries
            :
            [
            .. _entries.Where(entry => entry.Up),
            .. _entries.Where(entry => !entry.Up)
                       .Select(entry => (Entry: entry, Score: FuzzyName.Score(entry.Name, Filter)))
                       .Where(match => match.Score is not null)
                       .OrderBy(match => match.Entry.Folder ? 0 : 1)
                       .ThenByDescending(match => match.Score)
                       .ThenBy(match => match.Entry.Name, StringComparer.OrdinalIgnoreCase)
                       .Select(match => match.Entry),
            ];

        _walking = new Picked<Entry>(shown);
        _walking.Pick(_walking.All.Count > 0 && _walking.All[0].Up ? 1 : 0);
    }

    /// <summary>A file or folder left out of every listing: one named with a leading dot, or marked hidden.</summary>
    private static bool Hidden(FileSystemInfo info) =>
        info.Name.StartsWith('.') || info.Attributes.HasFlag(FileAttributes.Hidden);

    /// <summary>A folder as the author would write it, under <c>~</c> where it is in their home.</summary>
    private static string Shown(string folder)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        return home.Length > 0 && folder.StartsWith(home, StringComparison.Ordinal)
            ? $"~{folder[home.Length..]}"
            : folder;
    }

    /// <summary>What kind of attachment a file would make, as a row of the compose screen says it.</summary>
    private static string KindWord(string path) =>
        AttachmentTypes.KindOf(AttachmentTypes.Of(path)) switch
        {
            MediaKind.Image => "picture",
            MediaKind.Animation => "animation",
            MediaKind.Video => "video",
            MediaKind.Audio => "sound",
            _ => "file",
        };

    /// <summary>How large a file is, as a row of the compose screen says it.</summary>
    private static string Size(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => string.Create(CultureInfo.InvariantCulture, $"{bytes / 1024.0:F0} KB"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{bytes / 1024.0 / 1024.0:F1} MB"),
    };

    /// <summary>
    ///     <paramref name="left" /> from the left padding and the first of <paramref name="rights" /> that fits beside it
    ///     against the right, in <paramref name="width" /> columns — none of them where none does.
    /// </summary>
    private static Line Spread(int width, Span[] left, params Span[] rights)
    {
        var used = Pad + left.Sum(span => span.Width);

        foreach (var right in rights)
        {
            var gap = width - Pad - used - right.Width;

            if (gap >= 2)
            {
                return Line.Of([Gap(Pad), .. left, Gap(gap), right]);
            }
        }

        return Line.Of([Gap(Pad), .. left]);
    }

    /// <summary><paramref name="text" /> in <paramref name="columns" />, cut from its start with an ellipsis where it does not fit.</summary>
    private static string Tail(string text, int columns)
    {
        if (Glyphs.Columns(text) <= columns)
        {
            return text;
        }

        if (columns <= 0)
        {
            return string.Empty;
        }

        var from = 0;

        while (from < text.Length && Glyphs.Columns(text[from..]) > columns - 1)
        {
            from++;
        }

        return $"…{text[from..]}";
    }

    /// <summary>Blank columns.</summary>
    private static Span Gap(int columns) => new(new string(' ', Math.Max(0, columns)), Role.Body);

    /// <summary>One thing in a folder: <c>..</c>, a folder in it, or a file.</summary>
    private sealed record Entry(string Path, string Name, bool Folder, bool Up, long Bytes, DateTime Changed);
}

/// <summary>
///     How closely a typed filter matches a name, the way fzf scores (#376): the letters typed must all be in the name,
///     in order, anywhere. Each letter found earns a little, one right after the letter before it earns more, one that
///     starts a word or a run of digits earns more again, and every letter skipped between two matches costs a little —
///     so <c>ss0229</c> puts <c>Screenshot 2024-02-29 …</c> ahead of anything that merely contains the letters.
/// </summary>
internal static class FuzzyName
{
    private const int Found = 16;
    private const int Adjacent = 24;
    private const int WordStart = 12;
    private const int Skipped = 1;
    private const int Never = int.MinValue / 2;

    /// <summary>
    ///     How well <paramref name="filter" /> matches <paramref name="name" />, higher being closer, by the best
    ///     placing of its letters rather than the first — or <see langword="null" /> where they are not all there in
    ///     order. Case is ignored.
    /// </summary>
    public static int? Score(string name, string filter)
    {
        var length = name.Length;

        if (filter.Length == 0)
        {
            return 0;
        }

        if (filter.Length > length)
        {
            return null;
        }

        // best[j]: the best score with the letters so far matched and the last of them on name[j].
        var best = new int[length];
        var next = new int[length];

        for (var letter = 0; letter < filter.Length; letter++)
        {
            var wanted = char.ToLowerInvariant(filter[letter]);

            // The best of best[k] less what skipping from k to j costs, over every k before j - 1, kept as j moves on.
            var carried = Never;

            for (var at = 0; at < length; at++)
            {
                next[at] = Never;

                if (letter > 0 && at > 1)
                {
                    carried = Math.Max(carried - Skipped, best[at - 2]);
                }

                if (char.ToLowerInvariant(name[at]) != wanted)
                {
                    continue;
                }

                var earned = Found + (StartsWord(name, at) ? WordStart : 0);

                if (letter == 0)
                {
                    next[at] = earned;

                    continue;
                }

                var after = at > 0 && best[at - 1] > Never ? best[at - 1] + Adjacent : Never;
                var skipping = carried > Never ? carried - Skipped : Never;
                var before = Math.Max(after, skipping);

                if (before > Never)
                {
                    next[at] = before + earned;
                }
            }

            (best, next) = (next, best);
        }

        var top = best.Max();

        return top > Never ? top : null;
    }

    /// <summary>
    ///     Whether the letter at <paramref name="at" /> starts a word: the first, one after anything that is not a letter
    ///     or a digit, a digit after something that is not, or a capital after a small letter.
    /// </summary>
    private static bool StartsWord(string name, int at) =>
        at == 0
        || !char.IsLetterOrDigit(name[at - 1])
        || (char.IsDigit(name[at]) && !char.IsDigit(name[at - 1]))
        || (char.IsUpper(name[at]) && char.IsLower(name[at - 1]));
}
