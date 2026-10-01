using Terminal.Gui.Drawing;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;
using Attribute = Terminal.Gui.Drawing.Attribute;
using Glyphs = Wooly.Tui.Rendering.Glyphs;

namespace Wooly.Tui.Prototype;

// PROTOTYPE — throwaway. Skin A: helix / lualine / zellij / starship, in Tokyo Night. The frame is made of coloured
// segments rather than text and rules: a mode pill and crumb segments across the top, key chips along the bottom,
// icons and count badges on the rail, and regions told apart by background rather than by a │. The picked post gets a
// band as well as a mark.

internal sealed class StatuslineSkin : Skin
{
    private const string Bg = "#1a1b26", BgDark = "#16161e", BgHi = "#292e42", Fg = "#c0caf5", FgDark = "#a9b1d6";
    private const string Comment = "#565f89", Blue = "#7aa2f7", Cyan = "#7dcfff", Magenta = "#bb9af7";
    private const string Green = "#9ece6a", Orange = "#ff9e64", Yellow = "#e0af68", Red = "#f7768e", Black = "#414868";

    private readonly Dictionary<Role, Attribute> _table = Table(Bg, Fg,
        (Role.Hashtag, Cyan, null, false),
        (Role.Mention, Magenta, null, false),
        (Role.Link, Blue, null, false),
        (Role.Muted, Comment, null, false),
        (Proto.Replies, Comment, null, false),
        (Role.BylineName, Fg, null, true),
        (Role.BylineHandle, Blue, null, false),
        (Role.Audience, Comment, null, false),
        (Role.ContentWarning, Orange, null, true),
        (Role.Media, Cyan, null, false),
        (Role.Poll, Magenta, null, false),
        (Role.ReferencePicked, Fg, BgHi, true),
        (Role.Boost, Green, null, false),
        (Role.BoostMine, Green, null, true),
        (Role.Favorite, Yellow, null, false),
        (Role.FavoriteMine, Yellow, null, true),
        (Role.Selection, Blue, BgHi, false),
        (Role.Rail, FgDark, BgDark, false),
        (Role.RailCurrent, Fg, BgHi, true),
        (Role.RailUnread, BgDark, Red, true),
        (Role.Quota, Comment, BgDark, false),
        (Role.QuotaLow, Red, BgDark, true),
        (Role.Chrome, Comment, null, false),
        (Role.Key, Blue, null, true),
        (Role.Crumb, FgDark, BgHi, false),
        (Role.CrumbCurrent, Magenta, BgHi, true),
        (Role.Seam, BgDark, BgDark, false),
        (Role.Loading, Comment, BgDark, false),
        (Role.Destructive, Red, null, true),
        (Role.Error, Red, null, true),
        (Proto.Pill, BgDark, Blue, true),
        (Proto.Seg, FgDark, BgHi, false),
        (Proto.SegCurrent, Magenta, BgHi, true),
        (Proto.Bar, Comment, BgDark, false),
        (Proto.ChipKey, BgDark, Blue, true),
        (Proto.Chip, FgDark, BgHi, false),
        (Proto.Badge, BgDark, Red, true),
        (Proto.Accent, Blue, BgHi, false),
        (Proto.Icon, Comment, BgDark, false),
        (Proto.Heading, Comment, BgDark, false),
        (Proto.Gauge, Green, BgDark, false),
        (Proto.GaugeEmpty, Black, BgDark, false),
        (Proto.Spinner, Magenta, BgDark, false),
        (Proto.Dim, Comment, BgDark, false));

    public override string Key => "A";
    public override string Name => "Statusline";
    public override string Inspiration => "helix / lualine / zellij · Tokyo Night";

    public override Attribute? For(Role role) => _table.TryGetValue(role, out var a) ? a : null;

    public override string PickMark => "▎";
    public override Color? PickBand => C(BgHi);

    public override IReadOnlyList<Line>? Rail(Shell.Shell shell, int width, int height)
    {
        var rail = shell.Rail;
        var lines = new List<Line> { Fit([new Span(" ◆ wooly", Proto.Heading)], [], width, Role.Rail) };

        for (var at = 0; at < rail.Destinations.Count; at++)
        {
            var d = rail.Destinations[at];
            var current = at == rail.Current;
            var cursor = at == rail.Cursor;
            var role = current ? Role.RailCurrent : Role.Rail;
            List<Span> left =
            [
                new(current || cursor ? "▎" : " ", current ? Proto.Accent : cursor ? Proto.Accent : Role.Rail),
                new($"{Icon(d.Kind)} ", current ? Proto.Accent : Proto.Icon),
                new(d.Label, role),
            ];
            List<Span> right = d.Unread > 0 ? [new($" {Count(d.Unread)} ", Proto.Badge)] : [];
            lines.Add(Fit(left, right, width, role));

            if (at is 3 or 8)
            {
                lines.Add(Fit([], [], width, Role.Rail));
            }
        }

        var foot = new List<Line>();

        if (shell.Instance is { } instance)
        {
            foot.Add(Fit([new Span($" {instance}", Proto.Dim)], [], width, Role.Rail));
        }

        if (shell.Quota is { } quota)
        {
            var cells = width - 7;
            var full = (int)Math.Round(cells * quota.Fraction);
            foot.Add(Fit(
                [new Span(" ", Role.Rail), new Span(new string('▰', full), quota.Fraction <= 0.1 ? Role.QuotaLow : Proto.Gauge),
                    new Span(new string('▱', cells - full), Proto.GaugeEmpty)],
                [new Span($"{Count(quota.Remaining)} ", Proto.Dim)], width, Role.Rail));
        }

        while (lines.Count < height - foot.Count)
        {
            lines.Add(Fit([], [], width, Role.Rail));
        }

        lines.AddRange(foot);

        return lines;
    }

    public override Line? Breadcrumb(Shell.Shell shell, int width)
    {
        var crumbs = shell.Crumbs;
        var left = new List<Span> { new(" ◆ WOOLY ", Proto.Pill), new(" ", Proto.Bar) };

        for (var i = 0; i < crumbs.Count; i++)
        {
            var last = i == crumbs.Count - 1;

            if (i > 0)
            {
                left.Add(new Span("›", Proto.Seg));
            }

            left.Add(new Span($" {crumbs[i]} ", last ? Proto.SegCurrent : Proto.Seg));
        }

        var right = new List<Span>();

        if (shell.Dots > 0)
        {
            right.Add(new Span($"{Spin[shell.Dots % Spin.Length]} fetching ", Proto.Spinner));
        }

        if (shell.Instance is { } instance)
        {
            right.Add(new Span($" {instance} ", Proto.Seg));
        }

        return Fit(left, right, width, Proto.Bar);
    }

    public override IReadOnlyList<Line>? Gutter(int height) =>
        [.. Enumerable.Repeat(Line.Of(" ", Role.Seam), Math.Max(0, height))];

    public override Line? Status(Shell.Shell shell, int width)
    {
        if (shell.Asking is not null || shell.Notice is not null)
        {
            return null;
        }

        var mode = $" {Mode(shell)} ";
        var room = width - Glyphs.Columns(mode) - 1;
        var chips = Hints(shell.Keys, room, (k, _) =>
            [new Span($" {k.Key} ", Proto.ChipKey), new Span($" {k.Does} ", Proto.Chip), new Span(" ", Proto.Bar)], Proto.Bar);

        return Fit([new Span(mode, Proto.Pill), new Span(" ", Proto.Bar), .. chips], [], width, Proto.Bar);
    }
}
