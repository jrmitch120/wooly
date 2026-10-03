using System.Globalization;
using System.Text;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Prototype;

// PROTOTYPE — throwaway (prototype/compose). Three compose layouts, plus today's as "0", switched in place on the real
// compose screen with F2 / F3 or the floating bar's arrows. Start on one with WOOLY_COMPOSE=A (or B, C, 0).
//
// Each variant lays out the whole content region: the rows painted on it, and the box the live editor is laid over.
// Nothing here sends anything; the real ctrl-s / ctrl-w / esc still do what they always did.

/// <summary>Where the live editor sits, inside the content panel's viewport.</summary>
internal readonly record struct EditorBox(int Left, int Top, int Width, int Height);

/// <summary>What a variant gets to lay itself out with.</summary>
internal sealed record ComposeContext(
    Shell.Shell Shell,
    ComposeScreen Compose,
    int Width,
    int Height,
    Func<Screen, int, IReadOnlyList<Line>> Render);

internal abstract class ComposeVariant
{
    /// <summary>Mastodon counts a post against this; most instances leave it at the default.</summary>
    protected const int Limit = 500;

    public abstract string Key { get; }
    public abstract string Name { get; }
    public abstract string Inspiration { get; }

    /// <summary>Whether the editor is drawn in the theme's roles rather than Terminal.Gui's own (blue) scheme.</summary>
    public virtual bool Themed => true;

    /// <summary>What an empty editor shows, dimmed, where the first letter will go.</summary>
    public virtual string? Placeholder => "What's on your mind?";

    public abstract (IReadOnlyList<Line> Rows, EditorBox Editor) Lay(ComposeContext c);

    // ---- shared pieces ------------------------------------------------------------------------------------------

    protected static Span Gap(int columns) => new(new string(' ', Math.Max(0, columns)), Role.Body);

    protected static string Me(Shell.Shell shell) =>
        shell.Rail.Destinations.FirstOrDefault(d => d.Kind == DestinationKind.Profile)?.Label ?? "@you";

    protected static bool Replying(ComposeScreen c) => c.Purpose == ComposeFor.Reply && c.About is not null;

    protected static string Title(ComposeScreen c) => c.Purpose switch
    {
        ComposeFor.Reply => $"Reply to @{c.About?.Account}",
        ComposeFor.Edit => "Edit post",
        _ => "New post",
    };

    protected static string SendVerb(ComposeScreen c) => c.Purpose == ComposeFor.Edit ? "save" : "send";

    /// <summary>The words being answered, blank rows dropped, at most <paramref name="rows" /> of them.</summary>
    protected static IReadOnlyList<string> Quoted(ComposeScreen c, int width, int rows) =>
        c.About is { } about
            ? [.. TextWrap.Wrap(about.Content, Math.Max(1, width)).Where(row => row.Length > 0).Take(rows)]
            : [];

    /// <summary>The used-of-limit count, coloured as it nears and passes the limit.</summary>
    protected static Span Counter(ComposeScreen c, string format = "{0}/{1}")
    {
        var used = c.Text.Length;
        var role = used > Limit ? Role.Error : used > Limit - 50 ? Role.QuotaLow : Role.Muted;

        return new Span(string.Format(CultureInfo.InvariantCulture, format, used, Limit), role);
    }

    /// <summary>
    ///     The warning's value: what was written, or a dim hint where nothing was — with a caret while it has the
    ///     typing, which sits in front of the hint rather than replacing it.
    /// </summary>
    protected static IReadOnlyList<Span> WarningValue(ComposeScreen c, int room, string hint)
    {
        if (c.WritingTheWarning)
        {
            var written = TextWrap.Clip(c.Warning, Math.Max(0, room - 1));
            var after = c.Warning.Length == 0 ? TextWrap.Clip(hint, Math.Max(0, room - 1)) : string.Empty;

            return [new Span(written, Role.ContentWarning), new Span("▏", Role.ContentWarning), new Span(after, Role.Muted)];
        }

        return c.Warning.Length > 0
            ? [new Span(TextWrap.Clip(c.Warning, room), Role.ContentWarning)]
            : [new Span(TextWrap.Clip(hint, room), Role.Muted)];
    }

    /// <summary>Spans laid out to exactly <paramref name="width" /> columns: left, a gap, then right.</summary>
    protected static Line Spread(IReadOnlyList<Span> left, IReadOnlyList<Span> right, int width)
    {
        var used = left.Sum(s => s.Width) + right.Sum(s => s.Width);

        return used > width ? new Line(left) : new Line([.. left, Gap(width - used), .. right]);
    }

    protected static void PadTo(List<Line> rows, int height)
    {
        while (rows.Count < height)
        {
            rows.Add(Line.Blank);
        }
    }

    /// <summary>What is left of <paramref name="text" /> past its first <paramref name="columns" /> columns.</summary>
    protected static string After(string text, int columns)
    {
        var walk = StringInfo.GetTextElementEnumerator(text);
        var at = 0;
        var kept = new StringBuilder();

        while (walk.MoveNext())
        {
            var element = (string)walk.Current;
            var width = Glyphs.Columns(element);

            if (at >= columns)
            {
                kept.Append(element);
            }
            else if (at + width > columns)
            {
                kept.Append(' ', at + width - columns);
            }

            at += width;
        }

        return kept.ToString();
    }
}

internal static class ComposeVariants
{
    public static readonly ComposeVariant[] All =
    [
        new TodayVariant(),
        new HeadersVariant(),
        new FieldsVariant(),
        new OverlayVariant(),
    ];

    private static int _at = Math.Max(
        0,
        Array.FindIndex(All, v => v.Key == (Environment.GetEnvironmentVariable("WOOLY_COMPOSE") ?? "A")));

    public static event Action? Changed;

    public static ComposeVariant Current => All[_at];

    public static string Label => $"{Current.Key} ({Current.Name})";

    public static void Cycle(int by)
    {
        _at = (_at + by + All.Length) % All.Length;
        Changed?.Invoke();
    }

    public static void Select(string key)
    {
        _at = Math.Max(0, Array.FindIndex(All, v => v.Key == key));
        Changed?.Invoke();
    }
}

/// <summary>0 — what ships today, for comparison: the warning row, then Terminal.Gui's own editor scheme.</summary>
internal sealed class TodayVariant : ComposeVariant
{
    public override string Key => "0";
    public override string Name => "today";
    public override string Inspiration => "what ships now";
    public override bool Themed => false;
    public override string? Placeholder => null;

    public override (IReadOnlyList<Line> Rows, EditorBox Editor) Lay(ComposeContext c)
    {
        var rows = c.Render(c.Compose, c.Width);
        var top = Math.Min(c.Compose.AnsweringHeight(c.Width), Math.Max(0, c.Height - 3 - 2)) + c.Compose.WarningHeight;

        return (rows, new EditorBox(0, top, c.Width, Math.Max(1, c.Height - top)));
    }
}

/// <summary>
///     A — a mail client's compose: a block of right-aligned headers (From, Re, CW), a hairline, then the body on the
///     terminal's own page. No fill, no box: the post is the only thing with weight.
/// </summary>
internal sealed class HeadersVariant : ComposeVariant
{
    private const int Pad = 2;
    private const int Label = 4;

    public override string Key => "A";
    public override string Name => "Headers";
    public override string Inspiration => "aerc / neomutt compose";

    public override (IReadOnlyList<Line> Rows, EditorBox Editor) Lay(ComposeContext c)
    {
        var inner = Math.Max(1, c.Width - Pad * 2);
        var value = Math.Max(1, inner - Label - 2);
        var rows = new List<Line> { Line.Blank };

        Line Header(string label, IReadOnlyList<Span> spans, bool lit = false) =>
            new([Gap(Pad), new Span(label.PadLeft(Label), lit ? Role.PanelTitle : Role.Muted), Gap(2), .. spans]);

        rows.Add(Header("From", [
            new Span(Me(c.Shell), Role.BylineHandle),
            new Span(c.Shell.Instance is { } instance ? $" · {instance}" : string.Empty, Role.Muted),
        ]));

        if (Replying(c.Compose))
        {
            var quoted = Quoted(c.Compose, value - 2, 2);
            rows.Add(Header("Re", [new Span($"@{c.Compose.About!.Account}", Role.Mention)]));

            foreach (var row in quoted)
            {
                rows.Add(new Line([Gap(Pad + Label + 2), new Span("│ ", Role.PanelBorder), new Span(row, Role.Muted)]));
            }
        }

        var hint = c.Compose.WritingTheWarning ? "say what it's about" : "none · ctrl-w to add";
        rows.Add(Header("CW", WarningValue(c.Compose, value, hint), lit: c.Compose.WritingTheWarning));
        rows.Add(new Line([Gap(Pad), new Span(new string('─', inner), Role.PanelBorder)]));
        rows.Add(Line.Blank);

        var top = rows.Count;
        var foot = 2;
        var height = Math.Max(1, c.Height - top - foot);

        PadTo(rows, top + height);
        rows.Add(new Line([Gap(Pad), new Span(new string('─', inner), Role.PanelBorder)]));
        rows.Add(new Line([Gap(Pad), .. Spread([], [Counter(c.Compose, "{0} / {1}")], inner).Spans]));

        return (rows, new EditorBox(Pad, top, inner, height));
    }
}

/// <summary>
///     B — stacked form fields, each a label and a value behind a left bar. The bar of the field with the typing is
///     the accent and heavy; the rest are hairlines. The count is a thin gauge under the post.
/// </summary>
internal sealed class FieldsVariant : ComposeVariant
{
    private const int Pad = 1;
    private const int Indent = 2;

    public override string Key => "B";
    public override string Name => "Fields";
    public override string Inspiration => "charm's huh / gum forms";

    public override (IReadOnlyList<Line> Rows, EditorBox Editor) Lay(ComposeContext c)
    {
        var inner = Math.Max(1, c.Width - Pad * 2 - Indent);
        var warning = c.Compose.WritingTheWarning;
        var rows = new List<Line> { Line.Blank };

        Span Bar(bool lit) => new(lit ? "┃ " : "│ ", lit ? Role.PanelBorderActive : Role.PanelBorder);
        Line Row(bool lit, params Span[] spans) => new([Gap(Pad), Bar(lit), .. spans]);

        if (Replying(c.Compose))
        {
            rows.Add(new Line([Gap(Pad), new Span("  Replying to ", Role.Muted), new Span($"@{c.Compose.About!.Account}", Role.Mention)]));

            foreach (var row in Quoted(c.Compose, inner, 3))
            {
                rows.Add(new Line([Gap(Pad + Indent), new Span(row, Role.Muted)]));
            }

            rows.Add(Line.Blank);
        }

        rows.Add(Row(warning, new Span("Content warning", warning ? Role.PanelTitle : Role.Body)));
        rows.Add(Row(warning, [.. WarningValue(c.Compose, inner, "Optional — readers see this before they open the post")]));
        rows.Add(Line.Blank);

        var label = c.Compose.Purpose == ComposeFor.Edit ? "Post (editing)" : "Post";
        rows.Add(Row(!warning, new Span(label, warning ? Role.Body : Role.PanelTitle)));

        var top = rows.Count;
        var foot = 2;
        var height = Math.Max(1, c.Height - top - foot);

        for (var at = 0; at < height; at++)
        {
            rows.Add(Row(!warning));
        }

        // A thin gauge, as long as a line of prose wants to be rather than the whole width.
        var used = c.Compose.Text.Length;
        var length = Math.Min(inner - 12, 40);
        var filled = Math.Clamp((int)Math.Round(length * (double)used / Limit), 0, length);
        rows.Add(Line.Blank);
        rows.Add(new Line([
            Gap(Pad + Indent),
            new Span(new string('━', filled), used > Limit - 50 ? Role.QuotaLow : Role.Gauge),
            new Span(new string('━', Math.Max(0, length - filled)), Role.GaugeEmpty),
            Gap(2),
            Counter(c.Compose),
        ]));

        return (rows, new EditorBox(Pad + Indent, top, inner, height));
    }
}

/// <summary>
///     C — a floating window over whatever you were reading, which stays on screen dimmed behind it. The post you are
///     answering is in plain sight in the backdrop, so the window itself only names who it is to.
/// </summary>
internal sealed class OverlayVariant : ComposeVariant
{
    public override string Key => "C";
    public override string Name => "Overlay";
    public override string Inspiration => "lazygit's commit popup";

    public override (IReadOnlyList<Line> Rows, EditorBox Editor) Lay(ComposeContext c)
    {
        var width = c.Width;
        var height = c.Height;

        // The backdrop: the screen underneath, where it was scrolled to, flattened into one dim role.
        var rows = new List<Line>();

        if (c.Shell.Under is { } under)
        {
            foreach (var line in c.Render(under, width).Skip(under.Began).Take(height))
            {
                rows.Add(Line.Of(Strip(line.Text), Role.GaugeEmpty));
            }
        }

        PadTo(rows, height);

        var boxWidth = width < 40 ? width : Math.Min(80, width - 8);
        var boxHeight = Math.Max(8, Math.Min(height - 2, Math.Max(14, height / 2)));
        var left = (width - boxWidth) / 2;
        var top = Math.Max(0, (height - boxHeight) / 3);
        var inside = boxWidth - 4;
        var box = new List<Line>();

        // Top edge: the title on the left, who it goes out as on the right.
        var title = new Span($" {Title(c.Compose)} ", Role.PanelTitle);
        var me = new Span($" {Me(c.Shell)} ", Role.Muted);
        box.Add(new Line([
            new Span("╭─", Role.PanelBorderActive), title,
            new Span(new string('─', Math.Max(0, boxWidth - 5 - title.Width - me.Width)), Role.PanelBorderActive),
            me, new Span("─╮", Role.PanelBorderActive),
        ]));

        Line Inner(params Span[] spans)
        {
            var used = spans.Sum(s => s.Width);

            return new Line([new Span("│ ", Role.PanelBorderActive), .. spans, Gap(inside - used), new Span(" │", Role.PanelBorderActive)]);
        }

        Line Divider() => new([
            new Span("├", Role.PanelBorderActive),
            new Span(new string('┄', boxWidth - 2), Role.PanelBorder),
            new Span("┤", Role.PanelBorderActive),
        ]);

        if (Replying(c.Compose))
        {
            foreach (var row in Quoted(c.Compose, inside - 2, 2))
            {
                box.Add(Inner(new Span("│ ", Role.PanelBorder), new Span(row, Role.Muted)));
            }

            box.Add(Divider());
        }

        var hint = c.Compose.WritingTheWarning ? "say what it's about" : "content warning (ctrl-w)";
        var mark = new Span("⚠ ", c.Compose.WritingTheWarning || c.Compose.Warning.Length > 0 ? Role.ContentWarning : Role.Muted);
        box.Add(Inner([mark, .. WarningValue(c.Compose, inside - mark.Width, hint)]));
        box.Add(Divider());

        var editorTop = box.Count;
        var editorHeight = Math.Max(1, boxHeight - editorTop - 1);

        for (var at = 0; at < editorHeight; at++)
        {
            box.Add(Inner());
        }

        // Bottom edge: the count, and the two keys that end the window.
        Span[] keys =
        [
            new(" ctrl-s", Role.Key), new($" {SendVerb(c.Compose)}  ", Role.Muted),
            new("esc", Role.Key), new(" discard ", Role.Muted),
        ];
        var count = Counter(c.Compose);
        var span = boxWidth - 4 - 2 - count.Width - keys.Sum(k => k.Width);
        box.Add(new Line([
            new Span("╰─ ", Role.PanelBorderActive), count, new Span(" ", Role.Body),
            new Span(new string('─', Math.Max(0, span)), Role.PanelBorderActive),
            .. keys, new Span("─╯", Role.PanelBorderActive),
        ]));

        for (var at = 0; at < box.Count && top + at < height; at++)
        {
            var behind = rows[top + at].Text;
            rows[top + at] = new Line([
                new Span(Glyphs.Padded(Glyphs.Cut(behind, left), left), Role.GaugeEmpty),
                .. box[at].Spans,
                new Span(After(behind, left + boxWidth), Role.GaugeEmpty),
            ]);
        }

        return (rows, new EditorBox(left + 2, top + editorTop, inside, editorHeight));
    }

    /// <summary>Kitty's placeholder cells mean nothing once recoloured, so they go.</summary>
    private static string Strip(string text)
    {
        var kept = new StringBuilder(text.Length);

        foreach (var rune in text.EnumerateRunes())
        {
            kept.Append(rune.Value >= 0x100000 ? " " : rune.Value is >= 0x0300 and <= 0x036F ? string.Empty : rune.ToString());
        }

        return kept.ToString();
    }
}
