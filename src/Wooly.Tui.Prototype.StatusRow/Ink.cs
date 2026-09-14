namespace Wooly.Tui.Prototype.StatusRow;

/// <summary>What a run of the status row is, in the vocabulary the candidates need rather than the shipped one.</summary>
public enum Paint
{
    /// <summary>A key, e.g. <c>j/k</c>. `chrome` today.</summary>
    Key,

    /// <summary>What it does. `muted` today — which is the *same hex* as `chrome` in both built-ins.</summary>
    Does,

    /// <summary>The <c> · </c> between two hints. `chrome` today.</summary>
    Sep,

    /// <summary>An overflow mark saying how many hints did not fit. No role today; this is a candidate.</summary>
    More,

    /// <summary>A notice the shell has to say instead of the keys.</summary>
    Notice,

    /// <summary>A confirmation, which displaces the keys entirely.</summary>
    Ask,
}

/// <summary>A run of the row and what it is.</summary>
public readonly record struct Ribbon(string Text, Paint Paint);

/// <summary>
///     Colours for the three terminals this has to hold in, written as hex the way <c>Themes</c> writes them. `More`
///     has no role in the real table — that is part of the ticket's question — so it is drawn in `loading`'s grey as a
///     candidate.
/// </summary>
public sealed record Ink(string? Page, string? Key, string? Does, string? More, string? Ask)
{
    private const string Reset = "[0m";

    /// <summary>The dark built-in: `chrome` and `muted` both #7c7891, `loading` #5c5872, page #12111a.</summary>
    public static readonly Ink Dark = new("#12111a", "#7c7891", "#7c7891", "#5c5872", "#ff7a93");

    /// <summary>The light built-in: `chrome` and `muted` both #6a6780, `loading` #9b98ad, page #faf9fb.</summary>
    public static readonly Ink Light = new("#faf9fb", "#6a6780", "#6a6780", "#9b98ad", "#b3123f");

    /// <summary>No colour at all — `Themes.Plain`. Every escape goes away and the glyphs are on their own.</summary>
    public static readonly Ink Mono = new(null, null, null, null, null);

    /// <summary>The row, with escapes where this terminal has colour and bare text where it does not.</summary>
    public string Print(IReadOnlyList<Ribbon> row)
    {
        if (Page is null)
        {
            return string.Concat(row.Select(run => run.Text));
        }

        return string.Concat(row.Select(run => Escaped(Of(run.Paint)) + run.Text)) + Reset;
    }

    private string Of(Paint paint) => (paint switch
    {
        Paint.Does or Paint.Notice => Does,
        Paint.More => More,
        Paint.Ask => Ask,
        _ => Key,
    })!;

    private string Escaped(string foreground)
    {
        var (fr, fg, fb) = Rgb(foreground);
        var (br, bg, bb) = Rgb(Page!);

        return $"[38;2;{fr};{fg};{fb}m[48;2;{br};{bg};{bb}m";
    }

    private static (int R, int G, int B) Rgb(string hex) => (
        Convert.ToInt32(hex[1..3], 16),
        Convert.ToInt32(hex[3..5], 16),
        Convert.ToInt32(hex[5..7], 16));
}
