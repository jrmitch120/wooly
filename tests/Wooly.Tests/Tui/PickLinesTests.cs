using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The list a compose field offers things from (#335) holds whatever the field gives it, not only people: the box,
///     the pick and the scroll are the list's, and what a row says is the field's.
/// </summary>
public class PickLinesTests
{
    private static readonly string[] Languages = ["en English", "eo Esperanto", "es Español", "et Eesti", "eu Euskara"];

    /// <summary>Rows of another kind are drawn in the same box, the picked one marked, each row the thing it shows.</summary>
    [Fact]
    public void RowsOfAnotherKindAreDrawnInTheSameBox()
    {
        var rows = PickLines.Rows(Languages, 1, 0, 7, 40, Says);

        Assert.Equal(
            [
                "╭──────────────────────────────────────╮",
                "│  en English                          │",
                "│▌ eo Esperanto                        │",
                "│  es Español                          │",
                "│  et Eesti                            │",
                "│  eu Euskara                          │",
                "╰───── ↑↓ pick  tab insert  esc close ─╯",
            ],
            rows.Select(row => row.Text));
        Assert.Equal([null, 0, 1, 2, 3, 4, null], rows.Select(row => row.Item));
        Assert.Equal(Role.Selection, rows[2].Spans[1].Role);
    }

    /// <summary>Given fewer rows than things, it draws as many as fit, from the first one asked for.</summary>
    [Fact]
    public void FewerRowsThanThingsShowsAWindowOfThem()
    {
        var rows = PickLines.Rows(Languages, 3, 2, 4, 40, Says);

        Assert.Equal([null, 2, 3, null], rows.Select(row => row.Item));
        Assert.StartsWith("│▌ et Eesti", rows[2].Text, StringComparison.Ordinal);
    }

    private static IEnumerable<Span> Says(string language, bool picked) =>
        [new Span(language, picked ? Role.ReferencePicked : Role.Body)];
}
