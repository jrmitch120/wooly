using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     How the compose screen's headers are laid (#317, #375): a label right-aligned in a column of its own, a gap, then
///     the value — said once for the compose screen, which draws them, and for its Media rows, which draw the Media
///     header's line on the attachments screen too (#387). With the hairlines and the count that go round what is
///     written, which the edit public profile form puts round its bio as compose puts them round the post.
/// </summary>
internal static class ComposeHeaders
{
    /// <summary>The columns left blank either side of everything on the screen (#317).</summary>
    internal const int Pad = 2;

    /// <summary>
    ///     How wide the headers' right-aligned label column is: wide enough for <c>Media</c>, the widest of the words
    ///     every header is labelled with (#375).
    /// </summary>
    internal const int LabelWidth = 5;

    /// <summary>The columns between a header's label and its value.</summary>
    internal const int LabelGap = 2;

    /// <summary>
    ///     A header: <paramref name="label" /> right-aligned in the label column, in <paramref name="role" />, then
    ///     <paramref name="value" />.
    /// </summary>
    internal static Line Header(string label, Role role, params Span[] value) =>
        Line.Of([Gap(Pad + LabelWidth - Glyphs.Columns(label)), new Span(label, role), Gap(LabelGap), .. value]);

    /// <summary>How wide a header's value is on a screen <paramref name="width" /> wide: past its label, inside the pad.</summary>
    internal static int ValueWidth(int width) => Math.Max(0, width - (Pad * 2) - LabelWidth - LabelGap);

    /// <summary>A hairline across a screen <paramref name="width" /> wide, inside the pad: above and below what is written.</summary>
    internal static Line Hairline(int width) =>
        Line.Of(Gap(Pad), new Span(new string('─', Math.Max(0, width - (Pad * 2))), Role.PanelBorder));

    /// <summary>
    ///     The count under what is written, right-aligned inside the pad: <paramref name="used" /> of
    ///     <paramref name="limit" /> (#319) — muted while there is room, in the quota's low colour within the last tenth,
    ///     and an error's past the limit, so that a reader notices before the instance refuses it.
    /// </summary>
    internal static Line Count(int used, int limit, int width)
    {
        var count = $"{used} / {limit}";

        // The most the count can reach and stay muted: past it is the last tenth of the limit.
        var nearlyFull = limit * 9 / 10;

        var role = used > limit ? Role.Error
            : used > nearlyFull ? Role.QuotaLow
            : Role.Muted;

        return Line.Of(Gap(width - Pad - Glyphs.Columns(count)), new Span(count, role));
    }

    /// <summary><paramref name="columns" /> blank columns, none where that is fewer than one.</summary>
    internal static Span Gap(int columns) => new(new string(' ', Math.Max(0, columns)), Role.Body);
}
