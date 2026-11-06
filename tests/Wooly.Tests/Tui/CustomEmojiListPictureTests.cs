using System.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Wooly.Core;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;
using Color = Terminal.Gui.Drawing.Color;

namespace Wooly.Tests.Tui;

/// <summary>
///     On a terminal that draws Kitty's placeholders, each row of the list a <c>:</c>-word hangs shows its custom
///     emoji's picture beside its shortcode (#528, #580): the emoji's two-column box, a space, then <c>:shortcode:</c>
///     — driven through the shell's window with the keys a reader would use, each drawn as the running loop draws it,
///     and what is drawn read back off the terminal and the image store.
/// </summary>
public class CustomEmojiListPictureTests : IDisposable
{
    private static readonly Color BlobcatColour = new(200, 40, 40);

    private readonly TemporaryDirectory _files = new();

    public void Dispose() => _files.Dispose();

    private static readonly Color BlobfoxColour = new(40, 200, 40);

    private static readonly CustomEmoji Blobcat = FakeInstanceEmoji.Emoji("blobcat");

    private static readonly CustomEmoji Blobfox = FakeInstanceEmoji.Emoji("blobfox");

    /// <summary>Three emoji whose pictures are each a colour of their own.</summary>
    private static readonly Dictionary<string, Color> Coloured = new()
    {
        [":blobcat:"] = BlobcatColour,
        [":blobfox:"] = BlobfoxColour,
        [":blobowl:"] = new Color(40, 40, 200),
    };

    /// <summary>
    ///     Moving the pick, typing more and closing and opening the list again keep each picture on its own row, beside
    ///     its own shortcode — and leave none anywhere else, on a row the list has left or once it is closed.
    /// </summary>
    [Fact]
    public async Task EachPictureStaysOnItsOwnRow_AndNoneIsLeftBehind()
    {
        var terminal = new FakeTerminalImages();
        var pictures = new FakePictures();

        foreach (var (written, colour) in Coloured)
        {
            pictures.HoldingCustomEmoji(FakeInstanceEmoji.Emoji(written.Trim(':')), APicture.Solid(36, 36, colour));
        }

        using var drawn = await Composing(pictures, terminal, "blobcat", "blobfox", "blobowl");

        Type(drawn, "hi :bl");

        Assert.Equal(OnTheList(drawn, ":blobcat:", ":blobfox:", ":blobowl:"), Pictured(drawn, terminal));

        drawn.PressAsTheLoopDraws(Key.CursorDown);
        drawn.PressAsTheLoopDraws(Key.CursorDown);

        Assert.Equal(OnTheList(drawn, ":blobcat:", ":blobfox:", ":blobowl:"), Pictured(drawn, terminal));

        Type(drawn, "obf");

        Assert.Equal(OnTheList(drawn, ":blobfox:"), Pictured(drawn, terminal));

        drawn.PressAsTheLoopDraws(Key.Esc);

        Assert.Empty(Pictured(drawn, terminal));

        Type(drawn, " ");
        drawn.PressAsTheLoopDraws(Key.Backspace);

        Assert.Equal(OnTheList(drawn, ":blobfox:"), Pictured(drawn, terminal));

        drawn.PressAsTheLoopDraws(Key.Backspace);
        drawn.PressAsTheLoopDraws(Key.Backspace);

        Assert.Equal(OnTheList(drawn, ":blobcat:", ":blobfox:", ":blobowl:"), Pictured(drawn, terminal));
    }

    /// <summary>
    ///     Each row is its emoji's picture as two placeholder cells naming the image sent for it, then a space, then its
    ///     shortcode with the letters typed picked out.
    /// </summary>
    [Fact]
    public async Task EachRowIsItsEmojisPictureThenASpaceThenItsShortcode()
    {
        var terminal = new FakeTerminalImages();

        using var drawn = await Composing(Landed(), terminal);

        Type(drawn, ":bl");

        var list = List(drawn).FrameToScreen();

        foreach (var (row, shortcode, colour) in (ReadOnlySpan<(int, string, Color)>)
                 [(list.Y + 1, ":blobcat:", BlobcatColour), (list.Y + 2, ":blobfox:", BlobfoxColour)])
        {
            var cells = CustomEmojiTests.Placeholders(drawn).Where(cell => cell.Row == row).ToList();

            Assert.Equal([list.X + 3, list.X + 4], cells.Select(cell => cell.Column));
            Assert.Equal([KittyPlaceholder.Of(0, 0), KittyPlaceholder.Of(0, 1)], cells.Select(cell => cell.Grapheme));
            Assert.Equal(colour, Colour(terminal, Assert.Single(cells.Select(cell => cell.Id).Distinct())));
            Assert.Equal($" {shortcode}", CustomEmojiTests.Text(drawn, row, list.X + 5, shortcode.Length + 1));
            Assert.Equal(Themes.Dark.For(Role.CustomEmoji), drawn.Cell(row, list.X + 6));
            Assert.Equal(Themes.Dark.For(Role.Mention), drawn.Cell(row, list.X + 7));
            Assert.Equal(Themes.Dark.For(Role.Mention), drawn.Cell(row, list.X + 8));
            Assert.Equal(Themes.Dark.For(Role.CustomEmoji), drawn.Cell(row, list.X + 9));
        }
    }

    /// <summary>
    ///     A row's room is settled before its picture lands: the Stand-in's shade in the box, the picture asked of the
    ///     cache as on screen, and once it lands nothing in the list moves — every row reads as it did, the shade's cells
    ///     placeholder cells now, and the list where it was and as wide.
    /// </summary>
    [Fact]
    public async Task ARowsRoomIsSettledBeforeItsPictureLands_AndNothingMovesWhenItDoes()
    {
        var pictures = new FakePictures();

        using var drawn = await Composing(pictures, new FakeTerminalImages());

        Type(drawn, ":bl");

        var waiting = CustomEmojiTests.Seen(drawn);
        var list = List(drawn).FrameToScreen();

        Assert.Equal("│▌ ░░ :blobcat:", waiting[list.Y + 1][list.X..(list.X + 15)]);
        Assert.Equal("│  ░░ :blobfox:", waiting[list.Y + 2][list.X..(list.X + 15)]);
        Assert.Empty(CustomEmojiTests.Placeholders(drawn));
        Assert.Contains(pictures.Frames[^1], wanted => wanted.Drawn == Drawn.CustomEmoji(Blobcat) && wanted.OnScreen);
        Assert.Contains(pictures.Frames[^1], wanted => wanted.Drawn == Drawn.CustomEmoji(Blobfox) && wanted.OnScreen);

        pictures.HoldingCustomEmoji(Blobcat).HoldingCustomEmoji(Blobfox);
        drawn.Redraw();

        Assert.Equal(4, CustomEmojiTests.Placeholders(drawn).Count);
        Assert.Equal(waiting, CustomEmojiTests.Seen(drawn));
        Assert.Equal(list, List(drawn).FrameToScreen());
    }

    /// <summary>
    ///     A shortcode longer than the list can be wide is clipped at the list's edge, the cut marked, and its picture
    ///     drawn whole before it: the list no wider than the post, its edge where it always is.
    /// </summary>
    [Fact]
    public async Task ALongShortcodeIsClippedAtTheListsEdge_ItsPictureWhole()
    {
        var longest = "blob" + new string('x', 120);
        var terminal = new FakeTerminalImages();

        using var drawn = await Composing(
            new FakePictures().HoldingCustomEmoji(FakeInstanceEmoji.Emoji(longest)),
            terminal,
            longest);

        Type(drawn, ":bl");

        var list = List(drawn).FrameToScreen();
        var row = CustomEmojiTests.Seen(drawn)[list.Y + 1][list.X..list.Right];

        Assert.True(list.Right <= Editor(drawn).FrameToScreen().Right);
        Assert.Matches("^│▌ ░░ :blobx+…│$", row);
        Assert.Equal(
            [list.X + 3, list.X + 4],
            CustomEmojiTests.Placeholders(drawn).Select(cell => cell.Column).Order());
    }

    /// <summary>
    ///     Typing more asks the cache for the pictures of the emoji it brings onto the list, as on screen, and for none it
    ///     took off — on the frame the list changes, drawn as the loop draws it.
    /// </summary>
    [Fact]
    public async Task TypingMoreAsksForThePicturesItBringsOntoTheList()
    {
        var pictures = new FakePictures();

        using var drawn = await Composing(
            pictures,
            new FakeTerminalImages(),
            "blob_a",
            "blob_b",
            "blob_c",
            "blob_d",
            "blob_e",
            "blob_zzz");

        Type(drawn, ":bl");

        Assert.DoesNotContain(pictures.Frames[^1], wanted => wanted.Drawn == Drawn.CustomEmoji(FakeInstanceEmoji.Emoji("blob_zzz")));

        Type(drawn, "ob_z");

        Assert.Contains(
            pictures.Frames[^1],
            wanted => wanted.Drawn == Drawn.CustomEmoji(FakeInstanceEmoji.Emoji("blob_zzz")) && wanted.OnScreen);
        Assert.DoesNotContain(pictures.Frames[^1], wanted => wanted.Drawn == Drawn.CustomEmoji(FakeInstanceEmoji.Emoji("blob_a")));
    }

    /// <summary>
    ///     An emoji already drawn on screen in a post is the same picture in the list: from the one cache, fetched once,
    ///     and from the one image store, sent to the terminal once — its row naming the image the post's did.
    /// </summary>
    [Fact]
    public async Task AnEmojiAlreadyDrawnInAPostIsNeitherFetchedNorSentAgainForTheList()
    {
        var fetched = new List<string>();
        var terminal = new FakeTerminalImages();

        using var pictures = new Pictures(
            (address, _, _) =>
            {
                lock (fetched)
                {
                    fetched.Add(address);
                }

                return Task.FromResult(Fetched.Of(APng(36, 36)));
            },
            () => { });

        using var drawn = await DrawnShell.Of(
            100,
            30,
            Themes.Dark,
            new AShell
            {
                Timelines = FakeTimelineReader.Holding(
                    APost.With(id: "220", content: "I write :blobcat: daily", customEmoji: [Blobcat])),
                Emoji = FakeInstanceEmoji.Offering("blobcat"),
            },
            pictures: pictures,
            kittyImages: terminal,
            drawsPlaceholders: true,
            raster: ARaster.Placeholders());

        await Until(drawn, () => terminal.Transmitted.Any(EmojiSized));

        var inThePost = Assert.Single(terminal.Transmitted, EmojiSized);

        drawn.Shell.Press(ShellKey.C);
        drawn.Settle();
        Type(drawn, ":bl");

        var list = List(drawn).FrameToScreen();
        var cells = CustomEmojiTests.Placeholders(drawn).Where(cell => list.Contains(cell.Column, cell.Row)).ToList();

        Assert.Equal([list.X + 3, list.X + 4], cells.Select(cell => cell.Column));
        Assert.All(cells, cell => Assert.Equal((list.Y + 1, inThePost.Id), (cell.Row, cell.Id)));
        Assert.Equal(inThePost, Assert.Single(terminal.Transmitted, EmojiSized));
        Assert.Single(fetched, address => address == Blobcat.StaticUrl);
    }

    /// <summary>Whether <paramref name="sent" /> was sent at a custom emoji's size: two columns by one row.</summary>
    private static bool EmojiSized(FakeTerminalImages.Transmission sent) =>
        (sent.Columns, sent.Rows) == (CustomEmojiBox.Columns, CustomEmojiBox.Rows);

    /// <summary>
    ///     On a terminal that draws nothing, or with pictures off, a row is its shortcode alone, as it always was: no box,
    ///     no space for one, nothing asked of the cache for it.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WhereNothingIsDrawnARowIsItsShortcodeAlone(bool picturesOff)
    {
        var pictures = Landed();

        using var drawn = await DrawnShell.Of(
            100,
            30,
            Themes.Dark,
            new AShell { Emoji = FakeInstanceEmoji.Offering("blobcat", "blobfox") },
            pictures: pictures,
            picturesOff: picturesOff);

        drawn.Shell.Press(ShellKey.C);
        drawn.Settle();
        Type(drawn, ":bl");

        var list = List(drawn).FrameToScreen();
        var rows = drawn.Rows();

        Assert.Equal(PickLines.LeastWidth, list.Width);
        Assert.StartsWith("│▌ :blobcat: ", rows[list.Y + 1][list.X..], StringComparison.Ordinal);
        Assert.StartsWith("│  :blobfox: ", rows[list.Y + 2][list.X..], StringComparison.Ordinal);
        Assert.DoesNotContain(
            pictures.Frames.SelectMany(frame => frame),
            wanted => wanted.Drawn.IsCustomEmoji);
    }

    /// <summary>
    ///     The list flipped over compose's attachment pictures is on top of them: where it covers them it shows its own
    ///     emoji's pictures and nothing of theirs, and once it closes theirs are back whole where they were.
    /// </summary>
    [Fact]
    public async Task TheListFlippedOverAttachedPicturesDrawsItsOwnOnTop_AndGivesTheirsBackWhole()
    {
        string[] shortcodes = ["blob_a", "blob_b", "blob_c", "blob_d", "blob_e"];
        var files = Enumerable.Range(0, 4).Select(at => _files.WriteFile($"cat{at}.png")).ToList();
        var pictures = new FakePictures();

        foreach (var file in files)
        {
            pictures.HoldingPhotograph(Drawn.Attaching(file).Id, 40, 30);
        }

        foreach (var shortcode in shortcodes)
        {
            pictures.HoldingCustomEmoji(FakeInstanceEmoji.Emoji(shortcode));
        }

        var terminal = new FakeTerminalImages();

        using var drawn = await DrawnShell.Of(
            100,
            24,
            Themes.Dark,
            new AShell { Emoji = FakeInstanceEmoji.Offering(shortcodes) },
            pictures: pictures,
            kittyImages: terminal,
            drawsPlaceholders: true,
            raster: ARaster.Placeholders());

        drawn.Shell.Press(ShellKey.C);

        foreach (var file in files)
        {
            drawn.Shell.Paste(file);
        }

        drawn.Settle();
        drawn.DrawAsTheLoopDraws();

        var attached = CustomEmojiTests.Placeholders(drawn);

        Type(drawn, "  :bl");

        var list = List(drawn).FrameToScreen();
        var emoji = terminal.Transmitted.Where(EmojiSized).Select(sent => sent.Id).ToHashSet();
        var shown = CustomEmojiTests.Placeholders(drawn);

        Assert.True(list.Bottom <= Editor(drawn).FrameToScreen().Y);
        Assert.Contains(attached, cell => list.Contains(cell.Column, cell.Row));
        Assert.Equal(
            [.. Enumerable.Range(list.Y + 1, shortcodes.Length)
                          .SelectMany(row => (Point[])[new(list.X + 3, row), new(list.X + 4, row)])],
            shown.Where(cell => list.Contains(cell.Column, cell.Row))
                 .Select(cell => new Point(cell.Column, cell.Row))
                 .OrderBy(cell => cell.Y)
                 .ThenBy(cell => cell.X));
        Assert.All(shown.Where(cell => list.Contains(cell.Column, cell.Row)), cell => Assert.Contains(cell.Id, emoji));
        Assert.Equal(
            attached.Where(cell => !list.Contains(cell.Column, cell.Row)),
            shown.Where(cell => !list.Contains(cell.Column, cell.Row)));

        drawn.PressAsTheLoopDraws(Key.Esc);

        Assert.Equal(attached, CustomEmojiTests.Placeholders(drawn));
    }

    /// <summary>Both emoji's pictures, landed, each a colour of its own.</summary>
    private static FakePictures Landed() =>
        new FakePictures()
            .HoldingCustomEmoji(Blobcat, APicture.Solid(36, 36, BlobcatColour))
            .HoldingCustomEmoji(Blobfox, APicture.Solid(36, 36, BlobfoxColour));

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
    ///     Every picture on the terminal, top to bottom: the row and column its two placeholder cells start at, the
    ///     shortcode written after it, and the colour of the image they name.
    /// </summary>
    private static List<(int Row, int Column, string Shortcode, Color Colour)> Pictured(
        DrawnShell drawn,
        FakeTerminalImages terminal)
    {
        var seen = CustomEmojiTests.Seen(drawn);

        return
        [
            .. CustomEmojiTests.Placeholders(drawn).GroupBy(cell => cell.Row).OrderBy(row => row.Key).Select(row =>
            {
                var cells = row.OrderBy(cell => cell.Column).ToList();

                Assert.Equal([KittyPlaceholder.Of(0, 0), KittyPlaceholder.Of(0, 1)], cells.Select(cell => cell.Grapheme));
                Assert.Equal(cells[0].Column + 1, cells[1].Column);

                var after = seen[row.Key][(cells[1].Column + 2)..];
                var shortcode = after[..after.IndexOfAny([' ', '│'])];

                return (row.Key, cells[0].Column, shortcode, Colour(terminal, Assert.Single(cells.Select(cell => cell.Id).Distinct())));
            }),
        ];
    }

    /// <summary>
    ///     The loop's passes, run again and again — what a picture landing posts back to it among them — and the screen
    ///     drawn again after each, as the client draws whenever a picture lands, until <paramref name="landed" /> says
    ///     it has.
    /// </summary>
    private static async Task Until(DrawnShell drawn, Func<bool> landed)
    {
        var giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (true)
        {
            drawn.Iterate();
            drawn.Redraw();

            if (landed())
            {
                return;
            }

            Assert.True(DateTime.UtcNow < giveUp, "the picture never landed:\n" + string.Join("\n", drawn.Rows()));

            await Task.Delay(5, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>A PNG of <paramref name="width" /> by <paramref name="height" />, as a file server sends one.</summary>
    private static byte[] APng(int width, int height)
    {
        using var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(width, height);
        using var bytes = new MemoryStream();

        SixLabors.ImageSharp.ImageExtensions.SaveAsPng(image, bytes);

        return bytes.ToArray();
    }

    /// <summary>The colour the image the terminal holds under <paramref name="id" /> is, in its middle.</summary>
    private static Color Colour(FakeTerminalImages terminal, int id)
    {
        var pixels = terminal.Transmitted.Single(sent => sent.Id == id).Pixels();

        return pixels.Pixels[pixels.Width / 2, pixels.Height / 2];
    }

    /// <summary>
    ///     Compose, on a 100 by 30 terminal drawing Kitty's placeholders, on an instance offering
    ///     <paramref name="shortcodes" /> — <c>blobcat</c> and <c>blobfox</c> where none are said.
    /// </summary>
    private static async Task<DrawnShell> Composing(
        FakePictures pictures,
        FakeTerminalImages terminal,
        params string[] shortcodes)
    {
        var drawn = await DrawnShell.Of(
            100,
            30,
            Themes.Dark,
            new AShell { Emoji = FakeInstanceEmoji.Offering(shortcodes is [] ? ["blobcat", "blobfox"] : shortcodes) },
            pictures: pictures,
            kittyImages: terminal,
            drawsPlaceholders: true,
            raster: ARaster.Placeholders());

        drawn.Shell.Press(ShellKey.C);
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
