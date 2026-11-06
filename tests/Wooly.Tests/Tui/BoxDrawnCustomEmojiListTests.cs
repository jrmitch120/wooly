using System.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;
using Color = Terminal.Gui.Drawing.Color;

namespace Wooly.Tests.Tui;

/// <summary>
///     On the ways that draw through a box — sixel, and Kitty on a terminal not known to draw its placeholders — each
///     row of the list a <c>:</c>-word hangs shows its custom emoji's picture beside its shortcode (#528, #581), as it
///     does on Kitty's placeholders. Driven with the keys a reader would use, each drawn as the running loop draws it,
///     only what it changed, and read back off what the terminal shows (<see cref="ShownPictures" />), since a whole
///     screen drawn again would hide whatever was left behind.
/// </summary>
public class BoxDrawnCustomEmojiListTests : IDisposable
{
    private readonly TemporaryDirectory _files = new();

    /// <summary>Emoji whose pictures are each a colour of their own.</summary>
    private static readonly Dictionary<string, Color> Coloured = new()
    {
        [":blobcat:"] = new Color(200, 40, 40),
        [":blobfox:"] = new Color(40, 200, 40),
        [":blobowl:"] = new Color(40, 40, 200),
        [":blob_a:"] = new Color(220, 220, 40),
        [":blob_b:"] = new Color(220, 40, 220),
        [":blob_c:"] = new Color(40, 220, 220),
        [":blob_d:"] = new Color(120, 60, 20),
        [":blob_e:"] = new Color(20, 60, 120),
    };

    /// <summary>Five emoji that <c>:blob_</c> finds, enough to fill the list.</summary>
    private static readonly string[] Five = ["blob_a", "blob_b", "blob_c", "blob_d", "blob_e"];

    /// <summary>
    ///     The dark theme on a page of its own, as a theme naming <c>background</c> draws one — so that what a picture's
    ///     transparent pixels are laid on is the theme's to say, not the headless terminal's.
    /// </summary>
    private static readonly ITheme OnAPage =
        ((Palette)Themes.Dark).Overlaid(new Color(30, 30, 46), new Dictionary<Role, (Color?, Color?)>());

    public void Dispose() => _files.Dispose();

    public static TheoryData<PictureWay> BoxWays() => [PictureWay.Sixel, PictureWay.Kitty];

    /// <summary>
    ///     Each row is its emoji's picture in the two columns after the mark, then a space, then its shortcode — and no
    ///     picture is shown anywhere else.
    /// </summary>
    [Theory]
    [MemberData(nameof(BoxWays))]
    public async Task EachRowIsItsEmojisPictureThenASpaceThenItsShortcode(PictureWay way)
    {
        using var drawn = await Composing(way);
        using var shown = ShownPictures.Over(drawn, way);

        Type(drawn, ":bl");

        Assert.Equal(OnTheList(drawn, ":blobcat:", ":blobfox:", ":blobowl:"), Pictured(drawn, shown));
    }

    /// <summary>
    ///     A row's room is settled before its picture lands: the Stand-in's shade in the box, and once the pictures land
    ///     — the screen drawn again, as the client draws it whenever one does — each is in exactly that room, and nothing
    ///     else in the list has moved.
    /// </summary>
    [Theory]
    [MemberData(nameof(BoxWays))]
    public async Task ARowsRoomIsSettledBeforeItsPictureLands_AndNothingMovesWhenItDoes(PictureWay way)
    {
        var pictures = new FakePictures();

        using var drawn = await Composing(way, pictures: pictures);
        using var shown = ShownPictures.Over(drawn, way);

        Type(drawn, ":bl");

        var list = List(drawn).FrameToScreen();
        var waiting = drawn.Rows();

        Assert.Equal("│▌ ░░ :blobcat:", waiting[list.Y + 1][list.X..(list.X + 15)]);
        Assert.Equal("│  ░░ :blobfox:", waiting[list.Y + 2][list.X..(list.X + 15)]);
        Assert.Empty(shown.Showing());

        foreach (var shortcode in (string[])["blobcat", "blobfox", "blobowl"])
        {
            pictures.HoldingCustomEmoji(FakeInstanceEmoji.Emoji(shortcode), APicture.Solid(36, 36, Coloured[$":{shortcode}:"]));
        }

        drawn.Redraw();

        Assert.Equal(list, List(drawn).FrameToScreen());
        Assert.Equal(OnTheList(drawn, ":blobcat:", ":blobfox:", ":blobowl:"), Pictured(drawn, shown));
        Assert.Equal(
            waiting.Select(row => row.Replace("░░", "  ", StringComparison.Ordinal)),
            drawn.Rows());
    }

    /// <summary>
    ///     A picture is part of its row to the mouse, as its shortcode is: the wheel over it moves the pick, and a click
    ///     on it writes its emoji and closes the list, leaving no picture behind.
    /// </summary>
    [Theory]
    [MemberData(nameof(BoxWays))]
    public async Task TheWheelAndAClickOnAPictureAreOnItsRow(PictureWay way)
    {
        using var drawn = await Composing(way);
        using var shown = ShownPictures.Over(drawn, way);

        Type(drawn, "hi :bl");

        var list = List(drawn).FrameToScreen();

        drawn.Reports(Sgr.Wheel(list.X + 3, list.Y + 1));

        Assert.StartsWith("│▌ ", drawn.Rows()[list.Y + 2][list.X..], StringComparison.Ordinal);

        drawn.Reports(Sgr.Press(list.X + 4, list.Y + 3), Sgr.Release(list.X + 4, list.Y + 3));

        Assert.Equal("hi :blobowl: ", Editor(drawn).Text);
        Assert.False(List(drawn).Visible);
        Assert.Empty(shown.Showing());
    }

    /// <summary>
    ///     A row's picture is fitted whole into its box and centred, as a custom emoji is everywhere (ADR-0035): on a cell
    ///     of 10 by 20 its box is 20 by 20, and an emoji twice as wide as it is tall keeps the middle ten rows of it. The
    ///     rest is laid on the row behind it on sixel, picked or not, and left transparent through Kitty.
    /// </summary>
    [Theory]
    [MemberData(nameof(BoxWays))]
    public async Task ARowsPictureIsFittedWholeAndCentredInItsBox(PictureWay way)
    {
        using var drawn = await Composing(way, wide: true, theme: OnAPage);
        using var shown = ShownPictures.Over(drawn, way);

        Type(drawn, ":bl");

        var list = List(drawn).FrameToScreen();

        foreach (var (row, shortcode) in (ReadOnlySpan<(int, string)>)[(list.Y + 1, ":blobcat:"), (list.Y + 2, ":blobfox:")])
        {
            var pixels = shown.Pixels(Assert.Single(shown.Showing(), cell => cell.Cell == new Point(list.X + 3, row)).Id);
            var behind = way is PictureWay.Sixel ? drawn.Cell(row, list.X + 5).Background : new Color(0, 0, 0, 0);

            Assert.Equal((20, 20), (pixels.GetLength(0), pixels.GetLength(1)));

            for (var x = 0; x < 20; x++)
            {
                for (var y = 0; y < 20; y++)
                {
                    Assert.Equal(
                        (x, y, APicture.Rgba(y is >= 5 and < 15 ? Coloured[shortcode] : behind)),
                        (x, y, APicture.Rgba(pixels[x, y])));
                }
            }
        }
    }

    /// <summary>
    ///     Moving the pick, typing more, closing the list and opening it again keep each picture on its own row, beside
    ///     its own shortcode — and leave none anywhere else, on a row the list has left or once it is closed.
    /// </summary>
    [Theory]
    [MemberData(nameof(BoxWays))]
    public async Task EachPictureStaysOnItsOwnRow_AndNoneIsLeftBehind(PictureWay way)
    {
        using var drawn = await Composing(way);
        using var shown = ShownPictures.Over(drawn, way);

        Type(drawn, "hi :bl");

        Assert.Equal(OnTheList(drawn, ":blobcat:", ":blobfox:", ":blobowl:"), Pictured(drawn, shown));

        drawn.PressAsTheLoopDraws(Key.CursorDown);
        drawn.PressAsTheLoopDraws(Key.CursorDown);

        Assert.Equal(OnTheList(drawn, ":blobcat:", ":blobfox:", ":blobowl:"), Pictured(drawn, shown));

        Type(drawn, "obf");

        Assert.Equal(OnTheList(drawn, ":blobfox:"), Pictured(drawn, shown));

        drawn.PressAsTheLoopDraws(Key.Esc);

        Assert.Empty(Pictured(drawn, shown));

        Type(drawn, " ");
        drawn.PressAsTheLoopDraws(Key.Backspace);

        Assert.Equal(OnTheList(drawn, ":blobfox:"), Pictured(drawn, shown));

        drawn.PressAsTheLoopDraws(Key.Backspace);
        drawn.PressAsTheLoopDraws(Key.Backspace);

        Assert.Equal(OnTheList(drawn, ":blobcat:", ":blobfox:", ":blobowl:"), Pictured(drawn, shown));

        drawn.PressAsTheLoopDraws(Key.Home);

        Assert.Empty(Pictured(drawn, shown));
    }

    /// <summary>
    ///     A row's picture is sent to the terminal when it comes onto the list, and again only where its place changed:
    ///     never for a letter that leaves the list's rows as they were, nor for the pick moving over them. Typing on to
    ///     one emoji moves it up a row — a sixel is pixels laid into cells, so it goes again there, where through Kitty
    ///     the terminal holds it already and only places it anew — and taking the letter back brings the two that left
    ///     the list back onto it, and the one that stayed back down a row. Closing the list takes its pictures off the
    ///     terminal, so the one it opens on again is sent again.
    /// </summary>
    [Theory]
    [InlineData(PictureWay.Sixel, 1)]
    [InlineData(PictureWay.Kitty, 0)]
    public async Task ARowsPictureIsSentAgainOnlyWhereItsPlaceChanged(PictureWay way, int movedUp)
    {
        using var drawn = await Composing(way);

        Assert.Equal([0, 0, 3], [.. ":bl".Select(letter => Typed(drawn, new Key(letter)))]);
        Assert.Equal([0, 0], [.. "ob".Select(letter => Typed(drawn, new Key(letter)))]);
        Assert.Equal([0, 0, 0], [.. new[] { Key.CursorDown, Key.CursorDown, Key.CursorUp }.Select(key => Typed(drawn, key))]);
        Assert.Equal(movedUp, Typed(drawn, new Key('f')));
        Assert.Equal(movedUp + 2, Typed(drawn, Key.Backspace));
        Assert.Equal(movedUp, Typed(drawn, new Key('f')));
        Assert.Equal(0, Typed(drawn, Key.Esc));
        Assert.Equal(0, Typed(drawn, new Key(' ')));
        Assert.Equal(1, Typed(drawn, Key.Backspace));
    }

    /// <summary>
    ///     The list flipped over compose's attachment pictures, where there is no room under the post's line, is on top
    ///     of them: where it covers them it shows its own emoji's pictures and nothing of theirs, and the rest of theirs
    ///     shows as before. Typing on until it fits under the line flips it there, and taking the letter back flips it
    ///     over them again — and once it closes theirs are back whole where they were, and nothing of its is left.
    /// </summary>
    [Theory]
    [MemberData(nameof(BoxWays))]
    public async Task TheListFlippedOverAttachedPicturesDrawsItsOwnOnTop_AndGivesTheirsBackWhole(PictureWay way)
    {
        using var drawn = await Composing(way, height: 24, attached: 4, shortcodes: Five);
        using var shown = ShownPictures.Over(drawn, way);
        var attached = shown.Showing();

        Assert.NotEmpty(attached);

        Type(drawn, "  :blob_");

        var list = List(drawn).FrameToScreen();

        Assert.True(list.Bottom <= Editor(drawn).FrameToScreen().Y);
        Assert.Contains(attached, cell => list.Contains(cell.Cell));
        OnTop(drawn, shown, attached, ":blob_a:", ":blob_b:", ":blob_c:", ":blob_d:", ":blob_e:");

        drawn.PressAsTheLoopDraws(Key.CursorDown);

        OnTop(drawn, shown, attached, ":blob_a:", ":blob_b:", ":blob_c:", ":blob_d:", ":blob_e:");

        Type(drawn, "e");

        Assert.True(List(drawn).FrameToScreen().Y > Editor(drawn).FrameToScreen().Y);
        OnTop(drawn, shown, attached, ":blob_e:");

        drawn.PressAsTheLoopDraws(Key.Backspace);

        Assert.Equal(list, List(drawn).FrameToScreen());
        OnTop(drawn, shown, attached, ":blob_a:", ":blob_b:", ":blob_c:", ":blob_d:", ":blob_e:");

        drawn.PressAsTheLoopDraws(Key.Esc);

        Assert.False(List(drawn).Visible);
        Assert.Equal(Order(attached), Order(shown.Showing()));
    }

    /// <summary>
    ///     With the list open over compose's attachment pictures, a letter that leaves its rows as they were, or the pick
    ///     moving over them, sends no picture again — neither the list's nor what is left showing of theirs, whose place
    ///     and cover are as they were.
    /// </summary>
    [Theory]
    [MemberData(nameof(BoxWays))]
    public async Task OverAttachedPicturesNothingIsSentAgainWhileNothingMovesOrCoversAnew(PictureWay way)
    {
        using var drawn = await Composing(way, height: 24, attached: 4, shortcodes: Five);

        Type(drawn, "  :bl");

        Assert.True(List(drawn).FrameToScreen().Bottom <= Editor(drawn).FrameToScreen().Y);
        Assert.Equal([0, 0], [.. "ob".Select(letter => Typed(drawn, new Key(letter)))]);
        Assert.Equal([0, 0], [.. new[] { Key.CursorDown, Key.CursorUp }.Select(key => Typed(drawn, key))]);
    }

    /// <summary>
    ///     That the open list shows its <paramref name="shortcodes" />' pictures on its rows and no other picture
    ///     anywhere within it, each of its cells showing one picture at most, and that outside it the terminal shows just
    ///     what of <paramref name="attached" /> it showed before the list opened.
    /// </summary>
    private static void OnTop(
        DrawnShell drawn,
        ShownPictures shown,
        List<(Point Cell, string Id)> attached,
        params string[] shortcodes)
    {
        var list = List(drawn).FrameToScreen();
        var showing = shown.Showing();
        var inside = showing.Where(cell => list.Contains(cell.Cell)).ToList();

        Assert.Equal(showing.Count, showing.Select(cell => cell.Cell).Distinct().Count());
        Assert.Equal(
            [.. OnTheList(drawn, shortcodes).SelectMany(row => (Point[])[new(row.Column, row.Row), new(row.Column + 1, row.Row)])],
            Order(inside).Select(cell => cell.Cell));
        Assert.All(
            inside,
            cell => Assert.Equal(
                Coloured[shortcodes[cell.Cell.Y - list.Y - 1]],
                Middle(shown.Pixels(cell.Id))));
        Assert.Equal(
            Order(attached.Where(cell => !list.Contains(cell.Cell))),
            Order(showing.Where(cell => !list.Contains(cell.Cell))));
    }

    /// <summary><paramref name="cells" /> top to bottom, each row left to right.</summary>
    private static List<(Point Cell, string Id)> Order(IEnumerable<(Point Cell, string Id)> cells) =>
        [.. cells.OrderBy(cell => cell.Cell.Y).ThenBy(cell => cell.Cell.X)];

    /// <summary>
    ///     <paramref name="key" /> pressed and drawn as the loop draws it, and how many pictures that frame sent to the
    ///     terminal: sixels, or images handed over through Kitty.
    /// </summary>
    private static int Typed(DrawnShell drawn, Key key)
    {
        drawn.PressAsTheLoopDraws(key);

        var written = drawn.Application.Driver!.GetOutput().GetLastOutput();

        return written.Split("\u001bP").Length - 1 + written.Split("\u001b_Ga=t").Length - 1;
    }

    /// <summary>
    ///     What the open list should show: on each of its rows in turn, at the box's place after the mark, the picture
    ///     of the emoji <paramref name="shortcodes" /> names, beside that shortcode.
    /// </summary>
    private static List<(int Row, int Column, string Shortcode, Color Colour)> OnTheList(
        DrawnShell drawn,
        params string[] shortcodes)
    {
        var list = List(drawn).FrameToScreen();

        Assert.True(List(drawn).Visible);
        Assert.Equal(shortcodes.Length + 2, list.Height);

        return [.. shortcodes.Select((shortcode, at) => (list.Y + 1 + at, list.X + 3, shortcode, Coloured[shortcode]))];
    }

    /// <summary>
    ///     Every picture the terminal shows, top to bottom: the row and column its two cells start at, the shortcode
    ///     written after it, and its colour — each cell showing one picture, and a picture showing in two cells of a row.
    /// </summary>
    private static List<(int Row, int Column, string Shortcode, Color Colour)> Pictured(
        DrawnShell drawn,
        ShownPictures shown)
    {
        var rows = drawn.Rows();
        var showing = shown.Showing();

        Assert.Equal(showing.Count, showing.Select(cell => cell.Cell).Distinct().Count());

        return
        [
            .. showing.GroupBy(cell => cell.Id).Select(picture =>
            {
                var cells = picture.Select(cell => cell.Cell).OrderBy(cell => cell.X).ToList();

                Assert.Equal(2, cells.Count);
                Assert.Equal(cells[0].Y, cells[1].Y);
                Assert.Equal(cells[0].X + 1, cells[1].X);

                var after = rows[cells[0].Y][(cells[1].X + 2)..];
                var shortcode = after[..after.IndexOfAny([' ', '│'])];

                Assert.Equal("   ", rows[cells[0].Y][cells[0].X..(cells[1].X + 2)]);

                return (cells[0].Y, cells[0].X, shortcode, Middle(shown.Pixels(picture.Key)));
            }).OrderBy(picture => picture.Item1),
        ];
    }

    /// <summary>The colour in the middle of <paramref name="pixels" />.</summary>
    private static Color Middle(Color[,] pixels) => pixels[pixels.GetLength(0) / 2, pixels.GetLength(1) / 2];

    /// <summary>
    ///     Compose, on a 100-column terminal <paramref name="height" /> rows tall drawing pictures through a box
    ///     <paramref name="way" />, on an instance offering <paramref name="shortcodes" /> — <c>blobcat</c>,
    ///     <c>blobfox</c> and <c>blobowl</c> where none are said — every emoji's picture a colour of its own and landed,
    ///     twice as wide as it is tall where <paramref name="wide" />, square if not, unless <paramref name="pictures" />
    ///     are said, which hold what they hold — in <paramref name="theme" />, dark where none is said, with
    ///     <paramref name="attached" /> pictures attached to the post, their pixels here.
    /// </summary>
    private async Task<DrawnShell> Composing(
        PictureWay way,
        int height = 30,
        int attached = 0,
        bool wide = false,
        ITheme? theme = null,
        FakePictures? pictures = null,
        params string[] shortcodes)
    {
        string[] offered = shortcodes is [] ? ["blobcat", "blobfox", "blobowl"] : shortcodes;
        var files = Enumerable.Range(0, attached).Select(at => _files.WriteFile($"cat{at}.png")).ToList();

        if (pictures is null)
        {
            pictures = new FakePictures();

            foreach (var shortcode in offered)
            {
                pictures.HoldingCustomEmoji(
                    FakeInstanceEmoji.Emoji(shortcode),
                    APicture.Solid(wide ? 72 : 36, 36, Coloured[$":{shortcode}:"]));
            }
        }

        foreach (var file in files)
        {
            pictures.HoldingPhotograph(Drawn.Attaching(file).Id, 40, 30);
        }

        var drawn = await DrawnShell.Of(
            100,
            height,
            theme ?? Themes.Dark,
            new AShell { Emoji = FakeInstanceEmoji.Offering(offered) },
            pictures: pictures,
            drawsPictures: way is PictureWay.Sixel,
            answersKitty: way is PictureWay.Kitty,
            raster: way is PictureWay.Sixel ? ARaster.Sixel() : ARaster.Kitty());

        drawn.Shell.Press(ShellKey.C);

        foreach (var file in files)
        {
            drawn.Shell.Paste(file);
        }

        drawn.Settle();
        drawn.DrawAsTheLoopDraws();

        return drawn;
    }

    /// <summary>Each letter of <paramref name="text" /> typed, each drawn as the loop draws it.</summary>
    private static void Type(DrawnShell drawn, string text)
    {
        foreach (var letter in text)
        {
            drawn.PressAsTheLoopDraws(new Key(letter));
        }
    }

    /// <summary>The post's editor.</summary>
    private static ComposeEditor Editor(DrawnShell drawn) =>
        drawn.Window.SubViews.OfType<ComposeView>().Single().SubViews.OfType<ComposeEditor>().Single();

    /// <summary>The list hung under the word being typed in the post.</summary>
    private static View List(DrawnShell drawn) =>
        drawn.Window.SubViews.OfType<ComposeView>().Single().SubViews.Single(view => view.Id == HangingList.Id);
}
