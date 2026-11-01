using Terminal.Gui.Input;
using Wooly.Core;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;
using static Wooly.Tests.Tui.ContentClicks;

namespace Wooly.Tests.Tui;

/// <summary>
///     Custom emoji in a post's words, read back off the whole shell drawn headless (#523, ADR-0035): two columns and
///     one row wherever the <b>Raster</b> draws, shaded until the picture lands and drawn as placeholder cells once it
///     has, and the shortcode as written, in a role of its own, where nothing draws.
/// </summary>
public class CustomEmojiTests
{
    private static readonly CustomEmoji Php = new("php", "https://files.mastodon.social/emoji/php.png");

    private static readonly CustomEmoji Arizona = new("US_AZ", "https://files.mastodon.social/emoji/US_AZ.png");

    /// <summary>
    ///     Where the terminal draws no pictures the shortcode stays as written, its letters in the custom emoji's own
    ///     role, so it reads as a picture that was meant rather than as words someone typed — and the words around it
    ///     are the body's.
    /// </summary>
    [Fact]
    public async Task ACustomEmojiIsItsShortcodeInItsOwnRoleWhereNothingDraws()
    {
        using var drawn = await Shown(
            APost.With(id: "220", content: "I write :php: daily", customEmoji: [Php]),
            pictures: new FakePictures().HoldingCustomEmoji(Php),
            raster: Raster.None);

        var row = RowOf(drawn, "I write :php: daily");

        Assert.Equal(ShortcodeColour, Foreground(drawn, row, ":php:"));
        Assert.Equal(Body, Foreground(drawn, row, "write"));
    }

    /// <summary>
    ///     With pictures off — nowhere for their pixels to come from, on a terminal that would draw them — the shortcode
    ///     is as it is on a terminal that draws nothing: its letters as written, in their own role, taking no room
    ///     beyond them and wanting no picture (#523, story 37).
    /// </summary>
    [Fact]
    public async Task ACustomEmojiIsItsShortcodeInItsOwnRoleWithPicturesOff()
    {
        using var drawn = await DrawnShell.Of(
            80,
            Tall,
            Themes.Dark,
            new AShell
            {
                Timelines = FakeTimelineReader.Holding(
                    APost.With(id: "110"),
                    APost.With(id: "220", content: "I write :php: daily", customEmoji: [Php])),
            },
            picturesOff: true,
            drawsPlaceholders: true,
            raster: ARaster.Placeholders());

        var row = RowOf(drawn, "I write :php: daily");

        Assert.Equal(ShortcodeColour, Foreground(drawn, row, ":php:"));
        Assert.Equal(Body, Foreground(drawn, row, "write"));
        Assert.Empty(Placeholders(drawn));
    }

    /// <summary>
    ///     A <c>:word:</c> the post does not list is the words someone wrote — an emoji since deleted, one from
    ///     another instance, or a time — and drawn as the rest of the text is, on a terminal that draws nothing and on
    ///     one that draws alike.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AShortcodeThePostDoesNotListIsPlainText(bool draws)
    {
        using var drawn = await Shown(
            APost.With(id: "220", content: "Up at 10:30:45 with :ubuntu: and :php:", customEmoji: [Php]),
            pictures: new FakePictures().HoldingCustomEmoji(Php),
            raster: draws ? ARaster.Placeholders() : Raster.None);

        var row = RowOf(drawn, "Up at 10:30:45 with :ubuntu: and");

        Assert.Equal(Body, Foreground(drawn, row, "10:30:45"));
        Assert.Equal(Body, Foreground(drawn, row, ":ubuntu:"));
    }

    /// <summary>
    ///     A shortcode written inside an address is part of the address, which is walked to and opened whole: it keeps
    ///     its letters, in the address's role, though the post lists it.
    /// </summary>
    [Fact]
    public async Task AShortcodeInsideAnAddressIsPartOfTheAddress()
    {
        using var drawn = await Shown(
            APost.With(id: "220", content: "See https://example.com/:php:/notes", customEmoji: [Php]),
            pictures: new FakePictures().HoldingCustomEmoji(Php),
            raster: ARaster.Placeholders());

        var row = RowOf(drawn, "https://example.com/:php:/notes");

        Assert.Equal(Themes.Dark.For(Role.Link).Foreground, Foreground(drawn, row, ":php:"));
    }

    /// <summary>
    ///     On a terminal that draws, a custom emoji takes its two columns from the first frame, shaded as any picture's
    ///     box is until it lands — and is sent for, as on screen.
    /// </summary>
    [Fact]
    public async Task ACustomEmojiIsAShadedRoomTwoColumnsWideUntilItLands()
    {
        var pictures = new FakePictures();

        using var drawn = await Shown(
            APost.With(id: "220", content: "I write :php: daily", customEmoji: [Php]),
            pictures: pictures,
            raster: ARaster.Placeholders());

        var row = RowOf(drawn, "I write ░░ daily");

        Assert.Equal(Shade, Foreground(drawn, row, "░░"));
        Assert.Contains(
            pictures.Frames[^1],
            wanted => wanted.Drawn == Drawn.CustomEmoji(Php) && wanted.OnScreen);
    }

    /// <summary>
    ///     A picture the instance refused, or one that never comes, leaves its room as it was: shaded, and two columns
    ///     wide, so nothing after it moves.
    /// </summary>
    [Fact]
    public async Task ACustomEmojiRefusedStaysAShadedRoom()
    {
        using var drawn = await Shown(
            APost.With(id: "220", content: "I write :php: daily", customEmoji: [Php]),
            pictures: new FakePictures().Refusing(Drawn.CustomEmoji(Php).Id, Unshowable.Failed),
            raster: ARaster.Placeholders());

        Assert.Equal(Shade, Foreground(drawn, RowOf(drawn, "I write ░░ daily"), "░░"));
    }

    /// <summary>
    ///     Once its picture has landed a custom emoji is two placeholder cells where the shade was, naming the one
    ///     image the terminal holds for it — sent once for every post showing it, since they share a box size.
    /// </summary>
    [Fact]
    public async Task ACustomEmojiThatHasLandedIsPlaceholderCellsSentOnceForEveryPost()
    {
        var terminal = new FakeTerminalImages();

        using var drawn = await Shown(
            APost.With(id: "220", content: "I write :php: daily", customEmoji: [Php]),
            pictures: new FakePictures().HoldingCustomEmoji(Php),
            raster: ARaster.Placeholders(),
            terminal: terminal,
            APost.With(id: "330", content: "And :php: again", customEmoji: [Php]),
            APost.With(id: "440", content: "Twice :php: :php:", customEmoji: [Php]));

        var sent = Assert.Single(terminal.Transmitted);

        Assert.Equal((CustomEmojiBox.Columns, CustomEmojiBox.Rows), (sent.Columns, sent.Rows));

        var cells = Placeholders(drawn);

        Assert.Equal(4 * CustomEmojiBox.Columns, cells.Count);
        Assert.All(cells, cell => Assert.Equal(sent.Id, cell.Id));

        // Each where its shortcode's room was, the words after it where they were before it landed.
        var row = RowOf(drawn, "I write");
        var at = drawn.Rows()[row].IndexOf("I write", StringComparison.Ordinal) + "I write ".Length;

        Assert.Equal(
            [KittyPlaceholder.Of(0, 0), KittyPlaceholder.Of(0, 1)],
            cells.Where(cell => cell.Row == row).OrderBy(cell => cell.Column).Select(cell => cell.Grapheme));
        Assert.Equal([at, at + 1], cells.Where(cell => cell.Row == row).Select(cell => cell.Column).Order());
        Assert.Equal(" daily", Text(drawn, row, at + CustomEmojiBox.Columns, " daily".Length));
    }

    /// <summary>
    ///     Nothing moves when the pictures land: every row holds what it held, each emoji's two placeholder cells where
    ///     its shade was, and the paragraph wraps where it wrapped.
    /// </summary>
    [Fact]
    public async Task NothingMovesWhenACustomEmojiLands()
    {
        var pictures = new FakePictures();

        using var drawn = await Shown(
            APost.With(
                id: "220",
                content: string.Join(' ', Enumerable.Repeat("Shearing :php: in :US_AZ: today,", 8)),
                customEmoji: [Php, Arizona]),
            pictures: pictures,
            raster: ARaster.Placeholders());

        var waiting = Seen(drawn);

        pictures.HoldingCustomEmoji(Php).HoldingCustomEmoji(Arizona);
        drawn.Redraw();

        Assert.NotEmpty(Placeholders(drawn));
        Assert.Equal(waiting, Seen(drawn));
    }

    /// <summary>
    ///     On the ways that draw through a box, sixel and Kitty's, a custom emoji holds the same two shaded columns
    ///     from the first frame, so a line is laid out alike on every terminal that draws.
    /// </summary>
    [Theory]
    [InlineData(PictureWay.Sixel)]
    [InlineData(PictureWay.Kitty)]
    public async Task ACustomEmojiHoldsItsRoomOnTheWaysThatDrawThroughABox(PictureWay way)
    {
        using var drawn = await Shown(
            APost.With(id: "220", content: "I write :php: daily", customEmoji: [Php]),
            raster: way == PictureWay.Sixel ? ARaster.Sixel() : ARaster.Kitty());

        Assert.Equal(Shade, Foreground(drawn, RowOf(drawn, "I write ░░ daily"), "░░"));
    }

    /// <summary>
    ///     A custom emoji is no reference: a click on it picks the post and walks to nothing, and a double click on it
    ///     inside the post opens no Picture screen; <c>→</c> walks past it to the tag after it, and on to nothing more.
    /// </summary>
    [Fact]
    public async Task ACustomEmojiIsNotWalkedToClickedOpenOrShownOnThePictureScreen()
    {
        using var drawn = await Shown(
            APost.With(id: "220", content: "I write :php: #daily", customEmoji: [Php]),
            pictures: new FakePictures().HoldingCustomEmoji(Php),
            raster: ARaster.Placeholders());

        // Where the emoji is drawn on the first row reading the post's words.
        (int Column, int Row) CustomEmojiAt()
        {
            var row = RowOf(drawn, "I write");

            return (drawn.Rows()[row].IndexOf("I write", StringComparison.Ordinal) + "I write ".Length, row);
        }

        drawn.Click(CustomEmojiAt().Column, CustomEmojiAt().Row);

        Assert.Equal("220", drawn.Shell.Screen.Picked?.Id);
        Assert.Null(drawn.Shell.Screen.Reference);

        drawn.Press(Key.Enter);
        drawn.Settle();

        Assert.IsType<PostScreen>(drawn.Shell.Screen);

        drawn.DoubleClick(CustomEmojiAt().Column, CustomEmojiAt().Row);
        drawn.Settle();

        Assert.IsType<PostScreen>(drawn.Shell.Screen);
        Assert.Null(drawn.Shell.Screen.Reference);

        drawn.Press(Key.CursorRight);
        drawn.Press(Key.CursorRight);

        Assert.Equal("#daily", drawn.Shell.Screen.Reference?.Text);
    }

    /// <summary>
    ///     A custom emoji wraps as a Unicode emoji two columns wide does: <c>:US_AZ:Phoenix</c>, with no space to break
    ///     at, is cut where <c>🌵Phoenix</c> would be, before the emoji where it does not fit and never inside it —
    ///     drawn here at every place across the edge of the row.
    /// </summary>
    [Fact]
    public async Task ACustomEmojiWrapsAsATwoColumnUnicodeEmojiDoes()
    {
        async Task<List<string>> Wrapped(string emoji)
        {
            var written = string.Join(
                '\n',
                Enumerable.Range(50, 25).Select(lead => $"{new string('x', lead)}{emoji}Phoenix"));

            using var drawn = await DrawnShell.Of(
                80,
                100,
                Themes.Plain,
                new AShell
                {
                    Timelines = FakeTimelineReader.Holding(
                        APost.With(id: "110", content: written, customEmoji: [Arizona])),
                },
                pictures: new FakePictures(),
                drawsPlaceholders: true,
                raster: ARaster.Placeholders());

            // The cactus's second column is a cell of its own, read back as a blank: both are read as the room.
            return
            [
                .. drawn.Rows()
                        .Select(row => row[RailLines.Width..].Replace("🌵 ", "░░").Trim(' ', '│', '▌'))
                        .Where(row => row.StartsWith('x') || row.StartsWith("░░") || row.StartsWith("Phoenix")),
            ];
        }

        var custom = await Wrapped(":US_AZ:");

        Assert.Contains(custom, row => row.EndsWith("x░░", StringComparison.Ordinal));
        Assert.Contains(custom, row => row.StartsWith("░░Phoenix", StringComparison.Ordinal));
        Assert.Equal(await Wrapped("🌵"), custom);
    }

    /// <summary>
    ///     A custom emoji is never cut in half where a row is clipped — the preview of the reader's own post under a
    ///     notification — but dropped whole with the rest of what is cut, the ellipsis after the last that fits.
    /// </summary>
    [Fact]
    public async Task ACustomEmojiIsDroppedWholeWhereARowIsClipped()
    {
        var mine = APost.With(
            id: "110",
            content: $"Morning {string.Concat(Enumerable.Repeat(":php:", 90))}",
            isMine: true,
            customEmoji: [Php]);

        var built = new AShell
        {
            Notifications = FakeNotificationInbox.Holding(
                ANotification.With(kind: Wooly.Core.Notifications.NotificationKind.Favorite, post: mine)),
        };

        using var drawn = await DrawnShell.Of(
            80,
            Tall,
            Themes.Plain,
            built,
            pictures: new FakePictures(),
            drawsPlaceholders: true,
            raster: ARaster.Placeholders());

        drawn.Shell.Step(ToNotifications);
        built.Host.Settle();
        drawn.Redraw();

        var clipped = drawn.Rows()[RowOf(drawn, "…")][RailLines.Width..].Trim(' ', '│', '▌');

        Assert.Matches("^(░░)+ ?…$", clipped);
    }

    /// <summary>Where the rail's notifications destination is, counting from Home.</summary>
    private const int ToNotifications = 5;

    private static readonly Terminal.Gui.Drawing.Color Shade = Themes.Dark.For(Role.StandIn).Foreground;

    /// <summary>What a custom emoji's shortcode is drawn in where nothing draws, in the dark theme.</summary>
    internal static readonly Terminal.Gui.Drawing.Color ShortcodeColour = Themes.Dark.For(Role.CustomEmoji).Foreground;

    private static readonly Terminal.Gui.Drawing.Color Body = Themes.Dark.For(Role.Body).Foreground;

    /// <summary>
    ///     The feed, tall, in the dark theme so that roles can be told apart by colour, with <paramref name="post" />
    ///     second — so that it is not the one picked out.
    /// </summary>
    private static Task<DrawnShell> Shown(
        Post post,
        FakePictures? pictures = null,
        Raster? raster = null,
        FakeTerminalImages? terminal = null,
        params Post[] more) =>
        DrawnShell.Of(
            80,
            Tall,
            Themes.Dark,
            new AShell { Timelines = FakeTimelineReader.Holding([APost.With(id: "110"), post, .. more]) },
            pictures: pictures ?? new FakePictures(),
            kittyImages: terminal,
            drawsPlaceholders: raster?.Way == PictureWay.Placeholders,
            raster: raster);

    /// <summary>What <paramref name="columns" /> cells of a row hold from <paramref name="column" /> on.</summary>
    internal static string Text(DrawnShell drawn, int row, int column, int columns) =>
        string.Concat(Enumerable.Range(column, columns)
                                .Select(at => drawn.Application.Driver!.Contents![row, at].Grapheme));

    /// <summary>Every row as drawn, each placeholder cell read as the shade it took the place of.</summary>
    internal static string[] Seen(DrawnShell drawn)
    {
        var contents = drawn.Application.Driver!.Contents!;

        return
        [
            .. Enumerable.Range(0, contents.GetLength(0)).Select(row => string.Concat(
                Enumerable.Range(0, contents.GetLength(1)).Select(column =>
                    contents[row, column].Grapheme.StartsWith(Placeholder, StringComparison.Ordinal)
                        ? StandIn.Shade.ToString()
                        : contents[row, column].Grapheme))),
        ];
    }

    /// <summary>Every placeholder cell on screen, with where it is and the image id its colour carries.</summary>
    internal static List<PlaceholderCell> Placeholders(DrawnShell drawn)
    {
        var contents = drawn.Application.Driver!.Contents!;
        var cells = new List<PlaceholderCell>();

        for (var row = 0; row < contents.GetLength(0); row++)
        {
            for (var column = 0; column < contents.GetLength(1); column++)
            {
                var grapheme = contents[row, column].Grapheme;

                if (grapheme.StartsWith(Placeholder, StringComparison.Ordinal))
                {
                    var colour = drawn.Cell(row, column).Foreground;

                    var id = (colour.R << 16) | (colour.G << 8) | colour.B;

                    cells.Add(new PlaceholderCell(row, column, grapheme, id));
                }
            }
        }

        return cells;
    }

    internal const string Placeholder = "\U0010EEEE";

    internal sealed record PlaceholderCell(int Row, int Column, string Grapheme, int Id);

    /// <summary>The foreground <paramref name="text" /> is drawn in on a row, the same in each cell.</summary>
    internal static Terminal.Gui.Drawing.Color Foreground(DrawnShell drawn, int row, string text)
    {
        var column = drawn.Rows()[row].IndexOf(text, RailLines.Width, StringComparison.Ordinal);

        Assert.True(column >= 0, $"Row {row} does not read {text}:\n{string.Join('\n', drawn.Rows())}");

        var colours = Enumerable.Range(column, text.Length).Select(at => drawn.Cell(row, at).Foreground).Distinct();

        return Assert.Single(colours);
    }
}
