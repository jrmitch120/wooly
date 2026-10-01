using Terminal.Gui.Drawing;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Wooly.Tui.Prototype;

// PROTOTYPE — throwaway. Skin B: lazygit / btop / k9s, Catppuccin Mocha accents on the terminal's own background,
// with blue as the one accent (round 2: no green, no ┤├ around titles, no rail title, a thicker pick mark). The rail and the content are each a
// rounded panel with a title in its border; the breadcrumb row and the gutter go, because the content panel's title
// is the breadcrumb and the borders are the seam. The panel being read has the bright border. The status row is
// lazygit's "Does: key | Does: key" strip.

internal class PanelsSkin : Skin
{
    protected const string Base = "#1e1e2e", Mantle = "#181825", Surface0 = "#313244", Surface1 = "#45475a";
    protected const string Surface2 = "#585b70", Overlay = "#6c7086", Subtext = "#a6adc8", Text = "#cdd6f4";
    protected const string Mauve = "#cba6f7", Blue = "#89b4fa", Sapphire = "#74c7ec", Teal = "#94e2d5", Green = "#a6e3a1";
    /// <summary>
    ///     The picked post's band. Measured against a black terminal (#000000),
    ///     so it is a lift off black rather than a shade of grey: as dark as it can go and still read as a band.
    /// </summary>
    protected const string Band = "#16171c";

    /// <summary>The rail's current entry, kept a step lighter than the post band: the shade it had when it was liked.</summary>
    protected const string RailBand = "#1f2128";

    /// <summary>The pick mark's near-white, the same as main's dark theme draws it.</summary>
    protected const string PickMark_ = "#f2f0f7";

    protected const string Yellow = "#f9e2af", Peach = "#fab387", Red = "#f38ba8", Pink = "#f5c2e7";

    /// <summary>The page is the terminal's own background, so the app runs edge to edge into its padding.</summary>
    protected const string Page = "none";

    private readonly Dictionary<Role, Attribute> _table = Table(Page, Text,
        (Role.Hashtag, Teal, null, false),
        (Role.Mention, Mauve, null, false),
        (Role.Link, Blue, null, false),
        (Role.Muted, Overlay, null, false),
        (Role.BylineName, Text, null, true),
        (Role.BylineHandle, Sapphire, null, false),
        (Role.Audience, Overlay, null, false),
        (Role.ContentWarning, Peach, null, true),
        (Role.Media, Teal, null, false),
        (Role.Poll, Mauve, null, false),
        (Role.ReferencePicked, PickMark_, Page, true),
        (Role.Boost, Green, null, false),
        (Role.BoostMine, Green, null, true),
        (Role.Favorite, Yellow, null, false),
        (Role.FavoriteMine, Yellow, null, true),
        (Proto.Replies, Mauve, null, false),
        (Role.Selection, PickMark_, Band, true),
        (Role.Rail, Subtext, null, false),
        (Role.RailCurrent, Blue, RailBand, true),
        (Role.RailUnread, Peach, null, true),
        (Role.Quota, Overlay, null, false),
        (Role.QuotaLow, Red, null, true),
        (Role.Chrome, Surface2, null, false),
        (Role.Key, Sapphire, null, false),
        (Role.Crumb, Subtext, null, false),
        (Role.CrumbCurrent, Blue, null, true),
        (Role.Seam, Surface2, null, false),
        (Role.Loading, Overlay, null, false),
        (Role.Destructive, Red, null, true),
        (Role.Error, Red, null, true),
        (Proto.Chip, Blue, null, false),
        (Proto.ChipKey, Sapphire, null, true),
        (Proto.Bar, Surface2, null, false),
        (Proto.Band, Text, Surface0, false),
        (Proto.Gauge, Blue, null, false),
        (Proto.GaugeEmpty, Surface1, null, false),
        (Proto.Dim, Overlay, null, false),
        (Proto.Heading, Overlay, null, false),
        (Proto.Border, Surface2, null, false),
        (Proto.BorderFocus, Blue, null, false),
        (Proto.Title, Blue, null, true),
        (Proto.Pill, Pink, null, false),
        (Proto.Tab, Overlay, null, false),
        (Proto.TabCurrent, Blue, Band, true),
        (Proto.TopBar, Subtext, Band, false),
        (Proto.TopBarBrand, Base, Blue, true),
        (Proto.TopBarCount, Peach, Band, true),
        (Proto.TopBarGauge, Blue, Band, false),
        (Proto.TopBarGaugeEmpty, Surface1, Band, false));

    public override string Key => "B";
    public override string Name => "Panels";
    public override string Inspiration => "lazygit / btop / k9s · Catppuccin";

    public override Attribute? For(Role role) => _table.TryGetValue(role, out var a) ? a : null;

    public override string PickMark => "▌";
    public override Color? PickBand => C(Band);
    public override bool Boxed => true;

    public override string Title(Shell.Shell shell)
    {
        var trail = string.Join(" › ", shell.Crumbs);

        return shell.Dots > 0 ? $"{trail}  {Spin[shell.Dots % Spin.Length]}" : trail;
    }

    public override IReadOnlyList<Line>? Rail(Shell.Shell shell, int width, int height)
    {
        var rail = shell.Rail;
        var lines = new List<Line>();

        for (var at = 0; at < rail.Destinations.Count; at++)
        {
            lines.Add(Entry(shell, at, width));

            if (at is 3 or 8)
            {
                lines.Add(Line.Of(new string('─', width), Proto.Border));
            }
        }

        return Footed(lines, Foot(shell, width), width, height);
    }

    /// <summary>One destination: its label, its unread count at the right, and the band where it is current.</summary>
    protected static Line Entry(Shell.Shell shell, int at, int width)
    {
        var rail = shell.Rail;
        var d = rail.Destinations[at];
        var current = at == rail.Current;
        var cursor = at == rail.Cursor && !current;
        var role = current ? Role.RailCurrent : cursor ? Proto.Band : Role.Rail;
        List<Span> right = d.Unread > 0 ? [new(Count(d.Unread), current ? Role.RailCurrent : Role.RailUnread)] : [];

        return Fit([new Span(d.Label, role)], right, width, role);
    }

    /// <summary>The instance, where there is more than one profile, and the api budget as a gauge.</summary>
    protected static List<Line> Foot(Shell.Shell shell, int width)
    {
        var foot = new List<Line>();

        if (shell.Instance is { } instance)
        {
            foot.Add(Fit([new Span(instance, Proto.Dim)], [], width, Role.Rail));
        }

        if (shell.Quota is { } quota)
        {
            foot.Add(Fit([new Span("api ", Proto.Dim)], [], width, Role.Rail));
            foot.Add(Gauge(quota, width));
        }

        return foot;
    }

    protected static Line Gauge(Core.Http.RateLimitQuota quota, int width)
    {
        var cells = width - 4;
        var full = (int)Math.Round(cells * quota.Fraction);
        var low = quota.Fraction <= 0.1;

        return Fit(
            [new Span(new string('█', full), low ? Role.QuotaLow : Proto.Gauge), new Span(new string('░', cells - full), Proto.GaugeEmpty)],
            [new Span($"{(int)(quota.Fraction * 100),3}%", low ? Role.QuotaLow : Proto.Dim)], width, Role.Rail);
    }

    /// <summary>Rows padded out to the height with the foot held at the bottom.</summary>
    protected static List<Line> Footed(List<Line> lines, List<Line> foot, int width, int height)
    {
        while (lines.Count < height - foot.Count)
        {
            lines.Add(Fit([], [], width, Role.Rail));
        }

        lines.AddRange(foot);

        return lines;
    }

    public override Line? Status(Shell.Shell shell, int width)
    {
        if (shell.Asking is not null || shell.Notice is not null)
        {
            return null;
        }

        var strip = Hints(shell.Keys, width - 1, (k, first) =>
        [
            .. first ? Array.Empty<Span>() : [new Span(" | ", Proto.Bar)],
            new Span($"{char.ToUpperInvariant(k.Does[0])}{k.Does[1..]}: ", Proto.Chip),
            new Span(k.Key, Proto.ChipKey),
        ], Proto.Dim);

        return Fit([new Span(" ", Proto.Bar), .. strip], [], width, Proto.Bar);
    }
}
