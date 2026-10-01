using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Prototype;

// PROTOTYPE — throwaway. Four variations with B (Panels) as their base: everything B does, plus one idea each.

/// <summary>
///     B1 — lazygit's tabs-in-the-title: the four timelines leave the rail and become tabs on the content box's top
///     border, so the rail is only the places you go to. Tab still walks all ten.
/// </summary>
internal sealed class TabbedPanelsSkin : PanelsSkin
{
    public override string Key => "B1";
    public override string Name => "Panels + timeline tabs";
    public override string Inspiration => "lazygit's tabbed panel titles";

    public override Line TitleLine(Shell.Shell shell)
    {
        var rail = shell.Rail;

        if (shell.Crumbs.Count > 1 || rail.Current > 3)
        {
            return base.TitleLine(shell);
        }

        var spans = new List<Span> { new(" ", Proto.Tab) };

        for (var at = 0; at < 4; at++)
        {
            var d = rail.Destinations[at];
            var current = at == rail.Current;
            var role = current ? Proto.TabCurrent : at == rail.Cursor ? Proto.Title : Proto.Tab;

            spans.Add(new Span($" {d.Label}", role));
            spans.Add(d.Unread > 0 ? new Span($" {Count(d.Unread)} ", current ? Proto.TabCurrent : Role.RailUnread) : new Span(" ", role));
            spans.Add(new Span(" ", Proto.Tab));
        }

        if (shell.Dots > 0)
        {
            spans.Add(new Span($"{Spin[shell.Dots % Spin.Length]} ", Proto.Title));
        }

        return new Line(spans);
    }

    public override IReadOnlyList<Line>? Rail(Shell.Shell shell, int width, int height)
    {
        var lines = new List<Line>();

        for (var at = 4; at < shell.Rail.Destinations.Count; at++)
        {
            lines.Add(Entry(shell, at, width));

            if (at == 8)
            {
                lines.Add(Line.Of(new string('─', width), Proto.Border));
            }
        }

        return Footed(lines, Foot(shell, width), width, height);
    }
}

/// <summary>
///     B2 — lazygit's stacked side panels: the rail is three small titled boxes (Timelines, Places, You) and an API
///     box at the foot, and the box holding where you are lights up blue.
/// </summary>
internal sealed class StackedPanelsSkin : PanelsSkin
{
    public override string Key => "B2";
    public override string Name => "Panels + stacked rail";
    public override string Inspiration => "lazygit's stacked side panels";

    public override bool RailFramed => false;

    /// <summary>The boxes, as the rail's indices they hold.</summary>
    private static readonly int[][] Boxes = [[0, 1, 2, 3], [4, 5, 6, 7, 8], [9]];

    /// <summary>
    ///     ` goes on to the next box and ~ back to the one before, always landing on the box's first entry. Tab and
    ///     Shift-Tab are left as they are: one destination forward and back, across every box.
    /// </summary>
    public override int? Box(Shell.Rail rail, int by)
    {
        var box = Array.FindIndex(Boxes, b => b.Contains(rail.Cursor));

        return Boxes[(box + by + Boxes.Length) % Boxes.Length][0] - rail.Cursor;
    }

    /// <summary>The strip names ` beside Tab.</summary>
    public override Line? Status(Shell.Shell shell, int width)
    {
        if (base.Status(shell, width) is not { } line)
        {
            return null;
        }

        var spans = line.Spans.ToList();
        var at = spans.FindIndex(span => span.Role == Proto.ChipKey && span.Text == "tab");

        if (at > 0)
        {
            spans.InsertRange(at + 1, [new Span(" | ", Proto.Bar), new Span("Box: ", Proto.Chip), new Span("`", Proto.ChipKey)]);
        }

        return Fit(spans, [], width, Proto.Bar);
    }

    public override IReadOnlyList<Line>? Rail(Shell.Shell shell, int width, int height)
    {
        var current = shell.Rail.Current;
        var lines = new List<Line>();

        lines.AddRange(Box("Timelines", [.. Enumerable.Range(0, 4).Select(at => Entry(shell, at, width - 2))], width, current < 4));
        lines.AddRange(Box("Places", [.. Enumerable.Range(4, 5).Select(at => Entry(shell, at, width - 2))], width, current is >= 4 and < 9));
        lines.AddRange(Box("You", [Entry(shell, 9, width - 2)], width, current == 9));

        var api = Foot(shell, width - 2).Where(line => !line.Text.StartsWith("api", StringComparison.Ordinal)).ToList();
        var foot = api.Count > 0 ? Box("API", api, width, false) : [];

        return Footed(lines, foot, width, height);
    }

    private static List<Line> Box(string title, List<Line> rows, int width, bool active)
    {
        var edge = active ? Proto.BorderFocus : Proto.Border;
        var label = $" {title} ";
        var lines = new List<Line>
        {
            Line.Of(new Span("╭", edge), new Span(label, active ? Proto.Title : Proto.Dim),
                new Span(new string('─', Math.Max(0, width - 2 - label.Length)) + "╮", edge)),
        };

        lines.AddRange(rows.Select(row => new Line([new Span("│", edge), .. row.Spans, new Span("│", edge)])));
        lines.Add(Line.Of($"╰{new string('─', width - 2)}╯", edge));

        return lines;
    }
}

/// <summary>
///     B3 — cards (gh-dash, Posting, Textual): every post is its own rounded card inside the content box, the picked
///     card's border blue and the rest grey, with a gap between cards instead of a rule.
/// </summary>
internal sealed class CardPanelsSkin : PanelsSkin
{
    public override string Key => "B3";
    public override string Name => "Panels + cards";
    public override string Inspiration => "gh-dash / Posting card lists";

    public override IReadOnlyList<Line> Content(Func<int, IReadOnlyList<Line>> screen, int width)
    {
        if (width < 8)
        {
            return screen(width);
        }

        var inner = width - 4;
        var lines = screen(inner);
        var shown = new List<Line>();

        for (var at = 0; at < lines.Count;)
        {
            var line = lines[at];

            if (line.Item is not { } item)
            {
                // A rule between two things becomes the gap between two cards; anything else is left as it is.
                shown.Add(line.Text.Length > 0 && line.Text.All(c => c == '─') ? Line.Blank : line);
                at++;

                continue;
            }

            var end = at;

            while (end < lines.Count && lines[end].Item == item)
            {
                end++;
            }

            var group = lines.Skip(at).Take(end - at).ToList();
            var picked = group.Any(row => row.Has(Role.Selection));
            var edge = picked ? Proto.BorderFocus : Proto.Border;

            shown.Add(new Line([new Span($"╭{new string('─', width - 2)}╮", edge)]).PartOf(item));

            foreach (var row in group)
            {
                var after = row.After(new Span("│ ", edge));
                var pad = Math.Max(0, width - 1 - after.Width);

                shown.Add(new Line([.. after.Spans, new Span(new string(' ', pad), Role.Body), new Span("│", edge)])
                {
                    Insets = after.Insets,
                    Wants = after.Wants,
                    Item = after.Item,
                    Heads = after.Heads,
                });
            }

            shown.Add(new Line([new Span($"╰{new string('─', width - 2)}╯", edge)]).PartOf(item));
            at = end;
        }

        return shown;
    }
}

/// <summary>
///     B4 — btop / k9s header: a full-width bar across the top says who you are, what is waiting and how much API is
///     left, so the rail is only places and the panels sit under it.
/// </summary>
internal sealed class TopBarPanelsSkin : PanelsSkin
{
    public override string Key => "B4";
    public override string Name => "Panels + top bar";
    public override string Inspiration => "btop / k9s header";

    public override bool TopBar => true;

    public override IReadOnlyList<Line>? Rail(Shell.Shell shell, int width, int height)
    {
        var lines = new List<Line>();

        for (var at = 0; at < shell.Rail.Destinations.Count; at++)
        {
            lines.Add(Entry(shell, at, width));

            if (at is 3 or 8)
            {
                lines.Add(Line.Of(new string('─', width), Proto.Border));
            }
        }

        return Footed(lines, [], width, height);
    }

    public override Line? Breadcrumb(Shell.Shell shell, int width)
    {
        var rail = shell.Rail;
        var who = rail.Destinations[^1].Label + (shell.Instance is { } instance ? $"@{instance}" : string.Empty);
        var left = new List<Span> { new(" wooly ", Proto.TopBarBrand), new($"  {who}", Proto.TopBar) };

        foreach (var d in rail.Destinations.Where(d => d.Unread > 0))
        {
            left.Add(new Span($"   {d.Label.ToLowerInvariant()} ", Proto.TopBar));
            left.Add(new Span(Count(d.Unread), Proto.TopBarCount));
        }

        var right = new List<Span>();

        if (shell.Dots > 0)
        {
            right.Add(new Span($"{Spin[shell.Dots % Spin.Length]}  ", Proto.TopBar));
        }

        if (shell.Quota is { } quota)
        {
            var full = (int)Math.Round(10 * quota.Fraction);
            right.Add(new Span("api ", Proto.TopBar));
            right.Add(new Span(new string('█', full), quota.Fraction <= 0.1 ? Role.QuotaLow : Proto.TopBarGauge));
            right.Add(new Span(new string('░', 10 - full), Proto.TopBarGaugeEmpty));
            right.Add(new Span($" {(int)(quota.Fraction * 100)}% ", Proto.TopBar));
        }

        return Fit(left, right, width, Proto.TopBar);
    }
}
