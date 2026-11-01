using System.Drawing;
using Wooly.Core;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;
using static Wooly.Tests.Tui.ContentClicks;
using Color = Terminal.Gui.Drawing.Color;

namespace Wooly.Tests.Tui;

/// <summary>
///     Custom emoji on the ways that draw through a box — sixel, and Kitty on a terminal not known to draw its
///     placeholders — read back off the whole shell drawn headless (#527, ADR-0035): each a 2×1 picture in its room
///     once it lands, through a pool of boxes of their own, so that a page of them never leaves an attachment or an
///     avatar undrawn, and its shade wherever no box is left for it.
/// </summary>
public class BoxDrawnCustomEmojiTests
{
    private static readonly CustomEmoji Php = new("php", "https://files.mastodon.social/emoji/php.png");

    /// <summary>A custom emoji's colour, all over.</summary>
    private static readonly Color CustomEmojiColour = new(119, 123, 180);

    /// <summary>
    ///     The dark theme on a page of its own, as a theme naming <c>background</c> draws one — so that what a picture's
    ///     transparent pixels are laid on is the theme's to say, not the headless terminal's.
    /// </summary>
    private static readonly ITheme OnAPage =
        ((Palette)Themes.Dark).Overlaid(new Color(30, 30, 46), new Dictionary<Role, (Color?, Color?)>());

    /// <summary>
    ///     A listed custom emoji is its shaded room until its picture lands, and then a picture two columns by one in
    ///     exactly that room — nothing after it moved.
    /// </summary>
    [Theory]
    [MemberData(nameof(BoxWays))]
    public async Task ACustomEmojiIsDrawnAsATwoByOnePictureInItsRoomOnceItLands(PictureWay way)
    {
        var pictures = new FakePictures();

        using var drawn = await Boxed(
            way,
            pictures,
            APost.With(id: "220", content: "I write :php: daily", customEmoji: [Php]));

        var row = RowOf(drawn, "I write ░░ daily");
        var room = drawn.Rows()[row].IndexOf("░░", RailLines.Width, StringComparison.Ordinal);

        Assert.Empty(Pictures(drawn));

        pictures.HoldingCustomEmoji(Php);
        drawn.Redraw();

        Assert.Equal([new Rectangle(room, row, CustomEmojiBox.Columns, CustomEmojiBox.Rows)], Pictures(drawn));
        Assert.Equal(
            room + CustomEmojiBox.Columns,
            drawn.Rows()[row].IndexOf(" daily", room, StringComparison.Ordinal));
    }

    /// <summary>
    ///     A page with more custom emoji on it than their pool has boxes draws as many as it has, and the rest as the
    ///     shaded rooms they were before they landed — and still draws the attachment and the avatar below them, which
    ///     come from boxes of their own however many emoji are above.
    /// </summary>
    [Theory]
    [MemberData(nameof(BoxWays))]
    public async Task CustomEmojiPastTheirPoolAreShadedAndEveryAttachmentAndAvatarIsStillDrawn(PictureWay way)
    {
        const int past = 6;

        var pictures = new FakePictures()
            .HoldingCustomEmoji(Php)
            .Holding("m1", 800, 200)
            .HoldingAvatarOf("jeff@mastodon.social");

        using var drawn = await Boxed(
            way,
            pictures,
            APost.With(
                id: "220",
                content: string.Join(' ', Enumerable.Repeat(":php:", Placing.MostCustomEmojiBoxes + past)),
                customEmoji: [Php]),
            APost.With(
                id: "330",
                content: "Shearing day",
                avatarUrl: "https://files.mastodon.social/avatars/jeff.png",
                media: [APost.APicture("m1", shape: new PictureShape(800, 200))]));

        var drawnAsPictures = Pictures(drawn);

        Assert.Equal(Placing.MostCustomEmojiBoxes, drawnAsPictures.Count(EmojiSized));
        Assert.Equal(past, drawn.Rows().Sum(row => row.Count(cell => cell == StandIn.Shade)) / CustomEmojiBox.Columns);
        Assert.Contains(
            drawnAsPictures,
            cells => cells.Size == new Size(Avatar.BylineColumns(ARaster.Sixel()), Avatar.BylineRows));
        Assert.Contains(drawnAsPictures, cells => cells.Height > Avatar.BylineRows);
    }

    /// <summary>
    ///     On sixel a custom emoji is fitted whole into its box and centred, as through Kitty's placeholders, and the
    ///     rest of the box is laid on whatever is behind it: the band on the picked post (#269), the page on any other.
    ///     On a cell of 20 by 45 its box is 40 by 45, narrower than square: a square <c>:ubuntu:</c> keeps its left and
    ///     right edges in the middle forty rows, two above and three below the backdrop, and a wide <c>:php:</c>, twice
    ///     as wide as it is tall, its middle twenty, twelve above and thirteen below.
    /// </summary>
    [Theory]
    [InlineData(36, 36, 2, 42)]
    [InlineData(72, 36, 12, 32)]
    public async Task OnSixelACustomEmojiIsFittedWholeOnTheBandWherePickedAndOnThePageWhereNot(
        int width,
        int height,
        int top,
        int bottom)
    {
        using var drawn = await DrawnShell.Of(
            80,
            Tall,
            OnAPage,
            new AShell
            {
                Timelines = FakeTimelineReader.Holding(
                    APost.With(id: "110", content: "Picked :php: here", customEmoji: [Php]),
                    APost.With(id: "220", content: "Not :php: here", customEmoji: [Php])),
            },
            pictures: new FakePictures().HoldingCustomEmoji(Php, APicture.Solid(width, height, CustomEmojiColour)),
            drawsPictures: true,
            raster: ARaster.Sixel(new CellSize(20, 45)));

        var boxes = drawn.Application.Driver!.GetOutputBuffer().GetRasterImages()
                         .OrderBy(image => image.DestinationCells.Y)
                         .ToList();

        Assert.Equal(2, boxes.Count);
        Assert.Equal(RowOf(drawn, "Picked"), boxes[0].DestinationCells.Y);
        Assert.Equal(RowOf(drawn, "Not"), boxes[1].DestinationCells.Y);

        foreach (var (box, behind) in new[]
                 {
                     (boxes[0], OnAPage.Banded(Role.Body).Background),
                     (boxes[1], OnAPage.For(Role.Body).Background),
                 })
        {
            Assert.Equal((40, 45), (box.Pixels!.GetLength(0), box.Pixels.GetLength(1)));

            for (var x = 0; x < 40; x++)
            {
                for (var y = 0; y < 45; y++)
                {
                    Assert.Equal(
                        (x, y, APicture.Rgba(y >= top && y < bottom ? CustomEmojiColour : behind)),
                        (x, y, APicture.Rgba(box.Pixels[x, y])));
                }
            }
        }
    }

    public static TheoryData<PictureWay> BoxWays() => [PictureWay.Sixel, PictureWay.Kitty];

    /// <summary>Whether <paramref name="cells" /> are a custom emoji's two columns by one.</summary>
    private static bool EmojiSized(Rectangle cells) =>
        cells.Size == new Size(CustomEmojiBox.Columns, CustomEmojiBox.Rows);

    /// <summary>
    ///     The feed through a box on <paramref name="way" />, tall, in the dark theme, with <paramref name="posts" />
    ///     after one with nothing in it — so that none of them is the one picked out.
    /// </summary>
    private static Task<DrawnShell> Boxed(PictureWay way, FakePictures pictures, params Post[] posts) =>
        DrawnShell.Of(
            80,
            Tall,
            Themes.Dark,
            new AShell { Timelines = FakeTimelineReader.Holding([APost.With(id: "110"), .. posts]) },
            pictures: pictures,
            drawsPictures: way == PictureWay.Sixel,
            answersKitty: way == PictureWay.Kitty);

    /// <summary>Every picture the terminal was handed through a box, as the cells it was placed over.</summary>
    private static List<Rectangle> Pictures(DrawnShell drawn) =>
        [.. drawn.Application.Driver!.GetOutputBuffer().GetRasterImages().Select(image => image.DestinationCells)];
}
