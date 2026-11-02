using System.Drawing;
using Terminal.Gui.Drivers;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;
using static Wooly.Tests.Tui.ContentClicks;
using static Wooly.Tests.Tui.CustomEmojiNameTests;
using Color = Terminal.Gui.Drawing.Color;

namespace Wooly.Tests.Tui;

/// <summary>
///     An avatar's box, read back off the whole shell drawn headless (#532): as near square in pixels as whole columns
///     allow on the cell the Raster knows, and its picture fitted whole inside it and centred, as a custom emoji is —
///     never cropped, so that on a cell that is not 1:2 a square avatar keeps its sides.
/// </summary>
public class AvatarBoxTests
{
    private const string Jeff = "jeff@mastodon.social";

    private const string JeffsAvatar = "https://files.mastodon.social/avatars/jeff.png";

    /// <summary>An avatar's colour, all over.</summary>
    private static readonly Color AvatarColour = new(203, 166, 247);

    private static readonly Color Transparent = new(0, 0, 0, 0);

    /// <summary>
    ///     The dark theme on a page of its own, so that what sixel lays a fitted avatar's room on is the theme's to
    ///     say.
    /// </summary>
    private static readonly ITheme OnAPage =
        ((Palette)Themes.Dark).Overlaid(new Color(30, 30, 46), new Dictionary<Role, (Color?, Color?)>());

    /// <summary>
    ///     A square avatar is fitted whole into its byline's box and centred, on Kitty's placeholders, on sixel and on
    ///     Kitty through a box alike (#533), with the rest of the box transparent through Kitty and laid on the picked
    ///     post's band on sixel. On a cell of 20 by 45 the box is five columns by two rows, 100 by 90 pixels: the avatar 90 square,
    ///     five columns of pixels either side of it — where a crop cut its sides off. On a cell of 8 by 17 it is four
    ///     by two, 32 by 34: the avatar 32 square, a row of pixels above and below it.
    /// </summary>
    [Theory]
    [InlineData(PictureWay.Placeholders, 20, 45, 5, 0)]
    [InlineData(PictureWay.Sixel, 20, 45, 5, 0)]
    [InlineData(PictureWay.Kitty, 20, 45, 5, 0)]
    [InlineData(PictureWay.Placeholders, 8, 17, 4, 1)]
    [InlineData(PictureWay.Sixel, 8, 17, 4, 1)]
    [InlineData(PictureWay.Kitty, 8, 17, 4, 1)]
    public async Task ASquareAvatarIsFittedWholeIntoItsBylinesBox(
        PictureWay way,
        int cellWidth,
        int cellHeight,
        int columns,
        int above)
    {
        var cell = new CellSize(cellWidth, cellHeight);
        var (width, height) = (columns * cell.Width, Avatar.BylineRows * cell.Height);
        var side = Math.Min(width, height);
        var beside = (width - side) / 2;
        var terminal = new FakeTerminalImages();

        using var drawn = await Drawn(
            way,
            cell,
            new FakePictures().HoldingAvatarOf(Jeff, APicture.Solid(60, 60, AvatarColour)),
            terminal);

        var avatar = way == PictureWay.Placeholders
            ? Assert.Single(terminal.Transmitted).Pixels()
            : new Picture(Assert.Single(Images(drawn)).Pixels!);

        var behind = way == PictureWay.Sixel ? OnAPage.Banded(Role.Body).Background : Transparent;

        Assert.Equal((width, height), (avatar.Width, avatar.Height));

        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                var inside = x >= beside && x < beside + side && y >= above && y < above + side;

                Assert.Equal(
                    (x, y, APicture.Rgba(inside ? AvatarColour : behind)),
                    (x, y, APicture.Rgba(avatar.Pixels[x, y])));
            }
        }
    }

    /// <summary>
    ///     On Kitty through a box one avatar in two sizes of box — the account screen's header and the byline of a post
    ///     of theirs below it — is handed over at each box's own size in pixels, not the first one's (#533).
    /// </summary>
    [Fact]
    public async Task OnKittyThroughABoxOneAvatarInTwoSizesOfBoxIsHandedOverAtEach()
    {
        var cell = new CellSize(20, 45);

        using var drawn = await Drawn(
            PictureWay.Kitty,
            cell,
            new FakePictures().HoldingAvatarOf(Jeff, APicture.Solid(60, 60, AvatarColour)),
            accounts: FakeAccountRelationships.Holding(
                AnAccount.With(address: Jeff, author: "Jeff Mitchell", avatarUrl: JeffsAvatar)));

        await drawn.Shell.OpenAuthor();
        drawn.Built.Host.Drain();
        drawn.Redraw();

        var avatars = Images(drawn).OrderBy(image => image.DestinationCells.Y).ToList();

        Assert.Equal(
            [
                new Size(Avatar.Columns(Avatar.HeaderRows, ARaster.Kitty(cell)), Avatar.HeaderRows),
                new Size(Avatar.Columns(Avatar.BylineRows, ARaster.Kitty(cell)), Avatar.BylineRows),
            ],
            avatars.Select(image => image.DestinationCells.Size));
        Assert.All(
            avatars,
            image => Assert.Equal(
                (image.DestinationCells.Width * cell.Width, image.DestinationCells.Height * cell.Height),
                Size(image.Pixels!)));
    }

    /// <summary>
    ///     A byline's avatar takes the number of columns nearest square for its two rows on the cell the Raster knows —
    ///     four on a cell of 10 by 20, as it always was, five on 20 by 45 (4.5, rounded up) and four on 8 by 17 (4.25)
    ///     — and the name and the handle start one column after it.
    /// </summary>
    [Theory]
    [InlineData(10, 20, 4)]
    [InlineData(20, 45, 5)]
    [InlineData(8, 17, 4)]
    public async Task ABylinesAvatarIsAsNearSquareAsWholeColumnsAllow(int cellWidth, int cellHeight, int columns)
    {
        using var drawn = await Drawn(
            PictureWay.Sixel,
            new CellSize(cellWidth, cellHeight),
            new FakePictures().HoldingAvatarOf(Jeff));

        var box = Assert.Single(Boxes(drawn));
        var name = RowOf(drawn, "Jeff");

        Assert.Equal(new Size(columns, Avatar.BylineRows), box.Size);
        Assert.Equal(name, box.Y);
        Assert.Equal(box.X + columns + 1, ColumnOf(drawn, name, "Jeff"));
        Assert.Equal(box.X + columns + 1, ColumnOf(drawn, name + 1, "@jeff@mastodon.social"));
    }

    /// <summary>
    ///     An account screen's avatar takes the number of columns nearest square for its four rows: eight on a cell of
    ///     10 by 20, as it always was, and nine on 20 by 45 — 180 by 180 pixels, exactly square, so a square avatar
    ///     fills it edge to edge — and the name starts two columns after it.
    /// </summary>
    [Theory]
    [InlineData(10, 20, 8)]
    [InlineData(20, 45, 9)]
    public async Task AnAccountsAvatarIsAsNearSquareAsWholeColumnsAllow(int cellWidth, int cellHeight, int columns)
    {
        var cell = new CellSize(cellWidth, cellHeight);

        using var drawn = await Drawn(
            PictureWay.Sixel,
            cell,
            new FakePictures().HoldingAvatarOf(Jeff, APicture.Solid(60, 60, AvatarColour)),
            accounts: FakeAccountRelationships.Holding(
                AnAccount.With(address: Jeff, author: "Jeff Mitchell", avatarUrl: JeffsAvatar)));

        await drawn.Shell.OpenAuthor();
        drawn.Built.Host.Drain();
        drawn.Redraw();

        var header = Assert.Single(
            Images(drawn),
            image => image.DestinationCells.Height == Avatar.HeaderRows);
        var box = header.DestinationCells;
        var name = RowOf(drawn, "Jeff Mitchell", after: 0);

        Assert.Equal(new Size(columns, Avatar.HeaderRows), box.Size);
        Assert.Equal(box.X + columns + 2, ColumnOf(drawn, name, "Jeff Mitchell"));
        Assert.Equal((columns * cell.Width, Avatar.HeaderRows * cell.Height), Size(header.Pixels!));
        Assert.All(Colours(header.Pixels!), colour => Assert.Equal(APicture.Rgba(AvatarColour), APicture.Rgba(colour)));
    }

    /// <summary>
    ///     Where the terminal said it draws sixel but not how big its cells are, the cell is the 10 by 20 assumed, and
    ///     a byline's avatar is four columns by two as it always was.
    /// </summary>
    [Fact]
    public async Task WhereTheCellIsNotKnownABylinesAvatarIsFourColumnsByTwo()
    {
        using var drawn = await DrawnShell.Of(
            80,
            Tall,
            OnAPage,
            new AShell { Timelines = FakeTimelineReader.Holding(APost.With(id: "110", avatarUrl: JeffsAvatar)) },
            pictures: new FakePictures().HoldingAvatarOf(Jeff),
            drawsPictures: true);

        Assert.Equal(new Size(4, Avatar.BylineRows), Assert.Single(Boxes(drawn)).Size);
    }

    /// <summary>
    ///     Before the avatar lands, its Stand-in's shade fills the whole of its box — five columns on both rows on a
    ///     cell of 20 by 45 — and the name is where it will be once it has.
    /// </summary>
    [Fact]
    public async Task BeforeTheAvatarLandsItsStandInFillsTheBox()
    {
        var pictures = new FakePictures();

        using var drawn = await Drawn(PictureWay.Sixel, new CellSize(20, 45), pictures);

        var name = RowOf(drawn, "Jeff");
        var at = ColumnOf(drawn, name, "Jeff");
        var shade = new string(StandIn.Shade, 5);

        Assert.Empty(Boxes(drawn));
        Assert.Equal(shade + " ", drawn.Rows()[name][(at - 6)..at]);
        Assert.Equal(shade + " ", drawn.Rows()[name + 1][(at - 6)..at]);

        pictures.HoldingAvatarOf(Jeff);
        drawn.Redraw();

        Assert.Equal(new Rectangle(at - 6, name, 5, Avatar.BylineRows), Assert.Single(Boxes(drawn)));
        Assert.Equal(at, ColumnOf(drawn, name, "Jeff"));
    }

    /// <summary>
    ///     The feed on <paramref name="way" /> at <paramref name="cell" />, on a page of its own, its one post Jeff's
    ///     with his avatar named — and picked, so that its rows are on the band.
    /// </summary>
    private static Task<DrawnShell> Drawn(
        PictureWay way,
        CellSize cell,
        FakePictures pictures,
        FakeTerminalImages? terminal = null,
        FakeAccountRelationships? accounts = null) =>
        DrawnShell.Of(
            80,
            Tall,
            OnAPage,
            new AShell
            {
                Timelines = FakeTimelineReader.Holding(APost.With(id: "110", avatarUrl: JeffsAvatar)),
                Accounts = accounts ?? FakeAccountRelationships.Holding(),
            },
            pictures: pictures,
            drawsPictures: way == PictureWay.Sixel,
            kittyImages: terminal,
            drawsPlaceholders: way == PictureWay.Placeholders,
            answersKitty: way == PictureWay.Kitty,
            raster: way switch
            {
                PictureWay.Sixel => ARaster.Sixel(cell),
                PictureWay.Kitty => ARaster.Kitty(cell),
                _ => ARaster.Placeholders(cell),
            });

    /// <summary>Every picture the terminal was handed through a box, as the cells it was placed over.</summary>
    private static List<Rectangle> Boxes(DrawnShell drawn) => [.. Images(drawn).Select(image => image.DestinationCells)];

    /// <summary>Every picture the terminal was handed through a box.</summary>
    private static IEnumerable<RasterImageCommand> Images(DrawnShell drawn) =>
        drawn.Application.Driver!.GetOutputBuffer().GetRasterImages();

    private static (int Width, int Height) Size(Color[,] pixels) => (pixels.GetLength(0), pixels.GetLength(1));

    private static IEnumerable<Color> Colours(Color[,] pixels) => pixels.Cast<Color>();
}
