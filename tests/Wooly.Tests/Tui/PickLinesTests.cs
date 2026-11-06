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

    /// <summary>
    ///     A picture set into what a row says is set where it is drawn in the box, past the side and the mark (#580) —
    ///     and a custom emoji's box the row has no room for is dropped whole with the rest of what is cut, its picture
    ///     with it, never cut in half, and the cut marked.
    /// </summary>
    [Fact]
    public void ARowsPictureIsSetPastTheMark_AndDroppedWholeWhereItDoesNotFit()
    {
        var emoji = new Wooly.Core.CustomEmoji("blobcat", "https://files.example/emoji/blobcat.png");
        var drawn = Wooly.Tui.Media.Drawn.CustomEmoji(emoji);

        Line Pictured(string _, bool __) =>
            new Line([new Span("  ", Role.Media) { CustomEmoji = emoji }, new Span(" :blobcat:", Role.CustomEmoji)])
            {
                Insets = [new Inset(drawn, 0, 2, 1)],
            };

        var roomy = PickLines.Rows(["blobcat"], 0, 0, 3, 40, Pictured);
        var narrow = PickLines.Rows(["blobcat"], 0, 0, 3, 5, Pictured);

        Assert.Equal([new Inset(drawn, 3, 2, 1)], roomy[1].Insets);
        Assert.Equal("│▌ …│", narrow[1].Text);
        Assert.Empty(narrow[1].Insets);
    }

    private static Line Says(string language, bool picked) =>
        Line.Of(new Span(language, picked ? Role.ReferencePicked : Role.Body));
}
