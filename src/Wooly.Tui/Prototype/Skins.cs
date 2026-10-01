using System.Globalization;
using Terminal.Gui.Drawing;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;
using Attribute = Terminal.Gui.Drawing.Attribute;
using Glyphs = Wooly.Tui.Rendering.Glyphs;

namespace Wooly.Tui.Prototype;

// PROTOTYPE — throwaway. Question: the shell's bones (rail, breadcrumb, content, status row) are right but bland —
// what do modern-TUI tropes do for them? Three skins laid over the real shell, switchable live with < and >, beside
// the shell as it is today. A skin can answer roles (palette and bold), redraw the rail, breadcrumb, gutter and status
// row, change the picked-post mark, band the picked post, and box the regions. It cannot touch what a screen draws.
// Never merge: this whole folder, and every "Skins." call site, lives on prototype/tui-skins only.

/// <summary>Roles only the skins know. Cast rather than declared so nothing outside this folder has to answer them.</summary>
internal static class Proto
{
    public const Role Pill = (Role)900;
    public const Role Seg = (Role)901;
    public const Role SegCurrent = (Role)902;
    public const Role Bar = (Role)903;
    public const Role ChipKey = (Role)904;
    public const Role Chip = (Role)905;
    public const Role Badge = (Role)906;
    public const Role Accent = (Role)907;
    public const Role Band = (Role)908;
    public const Role BandText = (Role)909;
    public const Role Heading = (Role)910;
    public const Role Dim = (Role)911;
    public const Role Gauge = (Role)912;
    public const Role GaugeEmpty = (Role)913;
    public const Role Icon = (Role)914;
    public const Role Spinner = (Role)915;
    public const Role Switcher = (Role)916;
    public const Role SwitcherHint = (Role)917;
    public const Role Border = (Role)918;
    public const Role BorderFocus = (Role)919;
    public const Role Title = (Role)920;
    public const Role Replies = (Role)921;
    public const Role Tab = (Role)922;
    public const Role TabCurrent = (Role)923;
    public const Role TopBar = (Role)924;
    public const Role TopBarBrand = (Role)925;
    public const Role TopBarCount = (Role)926;
    public const Role TopBarGauge = (Role)927;
    public const Role TopBarGaugeEmpty = (Role)928;
}

internal abstract class Skin
{
    public abstract string Key { get; }
    public abstract string Name { get; }
    public abstract string Inspiration { get; }

    /// <summary>What this skin draws a role in, or null to leave it to the configured theme.</summary>
    public virtual Attribute? For(Role role) => null;

    public virtual IReadOnlyList<Line>? Rail(Shell.Shell shell, int width, int height) => null;
    public virtual Line? Breadcrumb(Shell.Shell shell, int width) => null;
    public virtual IReadOnlyList<Line>? Gutter(int height) => null;
    public virtual Line? Status(Shell.Shell shell, int width) => null;

    /// <summary>The picked post's gutter mark, in place of ▌.</summary>
    public virtual string PickMark => "▌";

    /// <summary>A background laid across every row of the picked post, or null for none.</summary>
    public virtual Color? PickBand => null;

    /// <summary>Whether the rail and content are boxed, which retires the breadcrumb row and the gutter.</summary>
    public virtual bool Boxed => false;

    /// <summary>The boxed content's title, where <see cref="Boxed" />.</summary>
    public virtual string Title(Shell.Shell shell) => string.Join(" › ", shell.Crumbs);

    /// <summary>The title as drawn over the content's top border.</summary>
    public virtual Line TitleLine(Shell.Shell shell) => Line.Of($" {Title(shell)} ", Proto.Title);

    /// <summary>Whether the rail gets Terminal.Gui's border, or draws whatever framing it wants itself.</summary>
    public virtual bool RailFramed => Boxed;

    /// <summary>
    ///     How far ` (forward) or ~ (back) should move the rail's cursor to switch boxes, or null where the skin has
    ///     no boxes and the keys do nothing.
    /// </summary>
    public virtual int? Box(Shell.Rail rail, int by) => null;

    /// <summary>
    ///     The order the rail's entries are shown in, top to bottom, where it differs from the rail's own — so that Tab
    ///     and Shift-Tab walk what is on screen. Null leaves them to the rail.
    /// </summary>
    public virtual int[]? Order => null;

    /// <summary>Whether a boxed skin keeps the breadcrumb row, full width across the top, above both panels.</summary>
    public virtual bool TopBar => false;

    /// <summary>What the content region shows, given what the screen would draw at a width.</summary>
    public virtual IReadOnlyList<Line> Content(Func<int, IReadOnlyList<Line>> screen, int width) => screen(width);

    protected static readonly string[] Spin = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];

    protected static string Icon(DestinationKind kind) => kind switch
    {
        DestinationKind.Home => "⌂",
        DestinationKind.Local => "◎",
        DestinationKind.Federated => "◍",
        DestinationKind.Hashtag => "#",
        DestinationKind.Notifications => "◈",
        DestinationKind.Messages => "✉",
        DestinationKind.Requests => "⚑",
        DestinationKind.Search => "⌕",
        DestinationKind.Discover => "✦",
        _ => "☺",
    };

    protected static string Count(int n) => n.ToString(CultureInfo.CurrentCulture);

    /// <summary>A colour, or "none" for whatever the terminal's own is (CSI 39m / 49m).</summary>
    protected static Color C(string hex) =>
        hex == "none" ? Color.None : ColourName.Parse(hex) ?? throw new InvalidOperationException(hex);

    /// <summary>A role table: every role on the page unless named, and the named ones as given.</summary>
    protected static Dictionary<Role, Attribute> Table(string page, string text,
        params (Role Role, string Fg, string? Bg, bool Bold)[] named)
    {
        var table = Enum.GetValues<Role>().ToDictionary(role => role, _ => new Attribute(C(text), C(page)));

        foreach (var (role, fg, bg, bold) in named)
        {
            table[role] = new Attribute(C(fg), C(bg ?? page), bold ? TextStyle.Bold : TextStyle.None);
        }

        return table;
    }

    /// <summary>Spans padded (or clipped) to exactly <paramref name="width" />, the gap in <paramref name="fill" />.</summary>
    protected static Line Fit(IEnumerable<Span> left, IEnumerable<Span> right, int width, Role fill)
    {
        var l = left.ToList();
        var r = right.ToList();
        var gap = width - l.Sum(s => s.Width) - r.Sum(s => s.Width);

        if (gap < 0)
        {
            // The right-hand side (a badge, a count) is the part worth keeping; the left is clipped to make room.
            gap = 0;
        }

        var room = width - r.Sum(s => s.Width);
        var spans = new List<Span>();
        var used = 0;

        foreach (var span in l)
        {
            if (used + span.Width > room)
            {
                spans.Add(span with { Text = TextWrap.Clip(span.Text, room - used) });
                used = room;

                break;
            }

            spans.Add(span);
            used += span.Width;
        }

        if (gap > 0)
        {
            spans.Add(new Span(new string(' ', gap), fill));
        }

        spans.AddRange(r);

        return new Line(spans);
    }

    /// <summary>Key hints fitted greedily, with "…+n" for the ones that do not fit.</summary>
    protected static List<Span> Hints(IReadOnlyList<KeyHint> keys, int width, Func<KeyHint, bool, IReadOnlyList<Span>> hint,
        Role overflow)
    {
        var spans = new List<Span>();
        var used = 0;

        for (var i = 0; i < keys.Count; i++)
        {
            var said = hint(keys[i], i == 0);
            var w = said.Sum(s => s.Width);
            var more = $" …+{keys.Count - i} ";

            if (used + w + (i < keys.Count - 1 ? Glyphs.Columns(more) : 0) > width)
            {
                spans.Add(new Span(more, overflow));

                break;
            }

            spans.AddRange(said);
            used += w;
        }

        return spans;
    }

    protected static string Mode(Shell.Shell shell) =>
        shell.Screen.GetType().Name.Replace("Screen", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
}

internal static class Skins
{
    public static readonly Skin[] All =
    [
        new CurrentSkin(), new PanelsSkin(), new TabbedPanelsSkin(), new StackedPanelsSkin(), new CardPanelsSkin(),
        new TopBarPanelsSkin(),
    ];

    private static int _at = Start();

    public static Skin Current => All[_at];

    public static event Action? Changed;

    public static string Label => $"{Current.Key} · {Current.Name}";

    public static void Cycle(int by)
    {
        _at = ((_at + by) % All.Length + All.Length) % All.Length;
        Changed?.Invoke();
    }

    public static void Select(string key)
    {
        _at = Math.Max(0, Array.FindIndex(All, s => s.Key.Equals(key, StringComparison.OrdinalIgnoreCase)));
        Changed?.Invoke();
    }

    /// <summary>WOOLY_SKIN=B starts on skin B, so a reload lands where you were looking.</summary>
    private static int Start() =>
        Math.Max(0, Array.FindIndex(All, s => s.Key.Equals(Environment.GetEnvironmentVariable("WOOLY_SKIN"), StringComparison.OrdinalIgnoreCase)));
}

/// <summary>Whatever the current skin answers, and the configured theme for everything it leaves alone.</summary>
internal sealed class SkinTheme(ITheme configured) : ITheme
{
    private static readonly Attribute Switcher = new(new Color(17, 17, 27), new Color(249, 226, 175), TextStyle.Bold);
    private static readonly Attribute SwitcherHint = new(new Color(249, 226, 175), new Color(17, 17, 27));

    public Attribute For(Role role) => role switch
    {
        Proto.Switcher => Switcher,
        Proto.SwitcherHint => SwitcherHint,
        _ => Skins.Current.For(role) ?? (Enum.IsDefined(role) ? configured.For(role)
            : role == Proto.Replies ? configured.For(Role.Muted) : configured.For(Role.Body)),
    };
}

/// <summary>0 — the shell as it is on main, for comparison.</summary>
internal sealed class CurrentSkin : Skin
{
    public override string Key => "0";
    public override string Name => "Current";
    public override string Inspiration => "main today";
}
