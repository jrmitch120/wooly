using Terminal.Gui.Drawing;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Wooly.Tui.Prototype;

// PROTOTYPE — throwaway. Skin C: Charm's Bubble Tea / Lip Gloss house style (glow, gum, soft-serve, gh-dash). Almost
// no lines at all: whitespace does the dividing, the rail gets small uppercase section headings, the thing you are on
// is a hot-pink ┃ and pink text, the breadcrumb leads with a purple title badge, and the status row is Bubble Tea's
// help bubble — "j/k post • g refresh" in two greys.

internal sealed class CharmSkin : Skin
{
    private const string Page = "#171721", Text = "#dddddd", Soft = "#a8a8a8", Dim = "#6c6c6c", Faint = "#3a3a4a";
    private const string Pink = "#ff5f87", Purple = "#7d56f4", Violet = "#ad8cff", Cream = "#fffdf5";
    private const string Green = "#04b575", Yellow = "#eccc68", Teal = "#6eefc0", Coral = "#ff8787";

    private readonly Dictionary<Role, Attribute> _table = Table(Page, Text,
        (Role.Hashtag, Violet, null, false),
        (Role.Mention, Pink, null, false),
        (Role.Link, Teal, null, false),
        (Role.Muted, Dim, null, false),
        (Proto.Replies, Dim, null, false),
        (Role.BylineName, Cream, null, true),
        (Role.BylineHandle, Violet, null, false),
        (Role.Audience, Dim, null, false),
        (Role.ContentWarning, Coral, null, true),
        (Role.Media, Teal, null, false),
        (Role.Poll, Violet, null, false),
        (Role.ReferencePicked, Cream, Purple, true),
        (Role.Boost, Green, null, false),
        (Role.BoostMine, Green, null, true),
        (Role.Favorite, Yellow, null, false),
        (Role.FavoriteMine, Yellow, null, true),
        (Role.Selection, Pink, null, false),
        (Role.Rail, Soft, null, false),
        (Role.RailCurrent, Pink, null, true),
        (Role.RailUnread, Pink, null, true),
        (Role.Quota, Dim, null, false),
        (Role.QuotaLow, Pink, null, true),
        (Role.Chrome, Faint, null, false),
        (Role.Key, Soft, null, false),
        (Role.Crumb, Dim, null, false),
        (Role.CrumbCurrent, Cream, null, true),
        (Role.Seam, Page, null, false),
        (Role.Loading, Dim, null, false),
        (Role.Destructive, Pink, null, true),
        (Role.Error, Pink, null, true),
        (Proto.Pill, Cream, Purple, true),
        (Proto.Heading, Dim, null, true),
        (Proto.Accent, Pink, null, false),
        (Proto.ChipKey, Soft, null, false),
        (Proto.Chip, Dim, null, false),
        (Proto.Bar, Faint, null, false),
        (Proto.Spinner, Pink, null, false),
        (Proto.Dim, Dim, null, false));

    public override string Key => "C";
    public override string Name => "Charm";
    public override string Inspiration => "Bubble Tea / Lip Gloss · glow, gum, gh-dash";

    public override Attribute? For(Role role) => _table.TryGetValue(role, out var a) ? a : null;

    public override string PickMark => "┃";

    public override IReadOnlyList<Line>? Rail(Shell.Shell shell, int width, int height)
    {
        var rail = shell.Rail;
        var lines = new List<Line> { Fit([], [], width, Role.Rail) };

        for (var at = 0; at < rail.Destinations.Count; at++)
        {
            var heading = at switch { 0 => "TIMELINES", 4 => "PLACES", 9 => "YOU", _ => null };

            if (heading is not null)
            {
                if (at > 0)
                {
                    lines.Add(Fit([], [], width, Role.Rail));
                }

                lines.Add(Fit([new Span($"  {heading}", Proto.Heading)], [], width, Role.Rail));
            }

            var d = rail.Destinations[at];
            var current = at == rail.Current;
            var cursor = at == rail.Cursor;
            List<Span> left =
            [
                new(current ? "┃ " : cursor ? "│ " : "  ", Proto.Accent),
                new(d.Label, current ? Role.RailCurrent : Role.Rail),
            ];
            List<Span> right = d.Unread > 0 ? [new($"{Count(d.Unread)} ", Role.RailUnread)] : [];
            lines.Add(Fit(left, right, width, Role.Rail));
        }

        var foot = new List<Line>();

        if (shell.Instance is { } instance)
        {
            foot.Add(Fit([new Span($"  {instance}", Proto.Dim)], [], width, Role.Rail));
        }

        if (shell.Quota is { } quota)
        {
            foot.Add(Fit([new Span($"  {Count(quota.Remaining)} of {Count(quota.Limit)}", quota.Fraction <= 0.1 ? Role.QuotaLow : Proto.Dim)], [], width, Role.Rail));
        }

        foot.Add(Fit([], [], width, Role.Rail));

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
        var left = new List<Span> { new($" {crumbs[0]} ", Proto.Pill) };

        for (var i = 1; i < crumbs.Count; i++)
        {
            left.Add(new Span("  ›  ", Role.Crumb));
            left.Add(new Span(crumbs[i], i == crumbs.Count - 1 ? Role.CrumbCurrent : Role.Crumb));
        }

        List<Span> right = shell.Dots > 0 ? [new($"{Spin[shell.Dots % Spin.Length]} ", Proto.Spinner), new("fetching ", Proto.Dim)] : [];

        return Fit(left, right, width, Role.Crumb);
    }

    public override IReadOnlyList<Line>? Gutter(int height) =>
        [.. Enumerable.Repeat(Line.Of(" ", Role.Seam), Math.Max(0, height))];

    public override Line? Status(Shell.Shell shell, int width)
    {
        if (shell.Asking is not null || shell.Notice is not null)
        {
            return null;
        }

        var help = Hints(shell.Keys, width - 2, (k, first) =>
        [
            .. first ? Array.Empty<Span>() : [new Span(" • ", Proto.Bar)],
            new Span(k.Key, Proto.ChipKey),
            new Span($" {k.Does}", Proto.Chip),
        ], Proto.Chip);

        return Fit([new Span("  ", Proto.Bar), .. help], [], width, Proto.Bar);
    }
}
