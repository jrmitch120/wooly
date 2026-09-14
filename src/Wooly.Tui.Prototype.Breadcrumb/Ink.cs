namespace Wooly.Tui.Prototype.Breadcrumb;

/// <summary>What a run of the breadcrumb is, in the vocabulary the candidates need rather than the shipped one.</summary>
public enum Paint
{
    /// <summary>A crumb you walked through. `chrome` today.</summary>
    Ancestor,

    /// <summary>The crumb you are standing on.</summary>
    Here,

    /// <summary>The `›` between two crumbs.</summary>
    Separator,

    /// <summary>The `fetching…` mark. `loading` today.</summary>
    Mark,
}

/// <summary>A run of the row, what it is, and whether it sits in a band of its own.</summary>
public readonly record struct Ribbon(string Text, Paint Paint, bool Banded = false);

/// <summary>
///     Colours for the three terminals this has to hold in, written as hex the way `Themes` writes them. The real
///     table has no role for `Here` — that is the ticket's question — so these are candidates to look at.
/// </summary>
public sealed record Ink(string? Page, string? Ancestor, string? Here, string? Separator, string? Mark, string? Band)
{
    private const string Reset = "[0m";

    /// <summary>The dark built-in: `chrome` #7c7891, `loading` #5c5872, on page #12111a, band #2a2942.</summary>
    public static readonly Ink Dark = new("#12111a", "#7c7891", "#f2f0f7", "#5c5872", "#5c5872", "#2a2942");

    /// <summary>The light built-in: `chrome` #6a6780, `loading` #9b98ad, on page #faf9fb, band #dcd9e8.</summary>
    public static readonly Ink Light = new("#faf9fb", "#6a6780", "#100e18", "#9b98ad", "#9b98ad", "#dcd9e8");

    /// <summary>No colour at all — `Themes.Plain`. Every escape goes away and the glyphs are on their own.</summary>
    public static readonly Ink Mono = new(null, null, null, null, null, null);

    /// <summary>The row, with escapes where this terminal has colour and bare text where it does not.</summary>
    public string Print(IReadOnlyList<Ribbon> row, bool banded)
    {
        if (Page is null)
        {
            return string.Concat(row.Select(run => run.Text));
        }

        return string.Concat(
            row.Select(run => Escaped(Of(run.Paint), banded || run.Banded ? Band! : Page) + run.Text)) + Reset;
    }

    private string Of(Paint paint) => (paint switch
    {
        Paint.Here => Here,
        Paint.Separator => Separator,
        Paint.Mark => Mark,
        _ => Ancestor,
    })!;

    private static string Escaped(string foreground, string background)
    {
        var (fr, fg, fb) = Rgb(foreground);
        var (br, bg, bb) = Rgb(background);

        return $"[38;2;{fr};{fg};{fb}m[48;2;{br};{bg};{bb}m";
    }

    private static (int R, int G, int B) Rgb(string hex) => (
        Convert.ToInt32(hex[1..3], 16),
        Convert.ToInt32(hex[3..5], 16),
        Convert.ToInt32(hex[5..7], 16));
}
