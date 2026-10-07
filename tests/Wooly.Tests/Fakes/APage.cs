using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;
using Line = Wooly.Tui.Rendering.Line;

namespace Wooly.Tests.Fakes;

/// <summary>
///     A page of rows for <see cref="Placing" /> to be handed, with no view or terminal in the room:
///     forty columns by ten rows of page over sixty rows of text, and boxes reserved wherever a test says.
/// </summary>
internal static class APage
{
    /// <summary>How many columns the page has.</summary>
    public const int Width = 40;

    /// <summary>How many rows the page has.</summary>
    public const int Height = 10;

    /// <summary>A screen's worth of rows, plenty more below it, and boxes reserved where each says.</summary>
    public static Line[] Rows(params (int At, Inset Inset)[] boxes)
    {
        var rows = Enumerable.Range(0, 60).Select(at => Line.Of($"row {at}", Role.Body)).ToArray();

        foreach (var (at, inset) in boxes)
        {
            for (var row = 0; row < inset.Rows; row++)
            {
                rows[at + row] = Line.Of(new string('▒', inset.Columns), Role.Media);
            }

            rows[at] = rows[at] with { Insets = [.. rows[at].Insets, inset] };
        }

        return rows;
    }

    /// <summary>A box for <paramref name="id" /> on row <paramref name="at" />, four cells square unless said.</summary>
    public static (int At, Inset Inset) Box(string id, int at, int column = 0, int columns = 4, int rows = 4) =>
        (at, new Inset(Picture(id), column, columns, rows));

    /// <summary>A drawn picture named <paramref name="id" />.</summary>
    public static Drawn Picture(string id) => new(id, $"https://files.mastodon.social/{id}.png");
}
