using System.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;

namespace Wooly.Tests.Tui;

/// <summary>
///     An open pick list is always on top (#577): any picture it covers is hidden exactly where it is covered, as text
///     underneath is, and comes back whole when the list closes — nothing showing through, nothing moved — on all three
///     ways a picture is drawn. Read off what the terminal shows (<see cref="ShownPictures" />) over compose's pending
///     attachments, which the language list hangs over and the people to mention can be flipped over. Every key and
///     click is drawn as the running loop draws it, only what it changed, since a whole screen drawn again would hide
///     whatever was left behind.
/// </summary>
public class ListOverPictureTests : IDisposable
{
    private readonly TemporaryDirectory _files = new();

    /// <summary>How a list is closed.</summary>
    public enum Closing
    {
        /// <summary><c>esc</c>, leaving what was typed.</summary>
        Esc,

        /// <summary><c>tab</c>, picking the thing picked.</summary>
        Pick,

        /// <summary>A click outside the list, on the blank row over From.</summary>
        ClickOutside,

        /// <summary>The caret taken off the word the list is open on, by <c>home</c>.</summary>
        CaretAway,
    }

    /// <summary>Five people on the home timeline, every one of whom <c>@ma</c> finds.</summary>
    private static AShell Five => new()
    {
        Timelines = FakeTimelineReader.Holding(
            APost.With(id: "1", account: "maa@a.social", author: "Maa"),
            APost.With(id: "2", account: "mab@a.social", author: "Mab"),
            APost.With(id: "3", account: "mac@a.social", author: "Mac"),
            APost.With(id: "4", account: "mad@a.social", author: "Mad"),
            APost.With(id: "5", account: "mae@a.social", author: "Mae")),
        Accounts = FakeAccountRelationships.HoldingNobody(),
    };

    public void Dispose() => _files.Dispose();

    /// <summary>
    ///     The language list, hung under Lang, runs down over the Media rows and covers the right-hand column of each
    ///     picture on them: that column shows the list and nothing of the picture, the rest of each still shows it, and
    ///     however the list is closed every picture is back whole where it was.
    /// </summary>
    [Theory]
    [InlineData(PictureWay.Placeholders, Closing.Esc)]
    [InlineData(PictureWay.Placeholders, Closing.Pick)]
    [InlineData(PictureWay.Placeholders, Closing.ClickOutside)]
    [InlineData(PictureWay.Kitty, Closing.Esc)]
    [InlineData(PictureWay.Kitty, Closing.Pick)]
    [InlineData(PictureWay.Kitty, Closing.ClickOutside)]
    [InlineData(PictureWay.Sixel, Closing.Esc)]
    [InlineData(PictureWay.Sixel, Closing.Pick)]
    [InlineData(PictureWay.Sixel, Closing.ClickOutside)]
    public async Task TheLanguageListHidesWhatItCoversOfAPictureAndGivesItBackWhole(PictureWay way, Closing closing)
    {
        using var drawn = await Attached(way, height: 30);
        using var shown = ShownPictures.Over(drawn, way);
        var pictured = shown.Cells();
        var rows = drawn.Rows();

        WalkTo(drawn, ComposeField.Lang);
        drawn.PressAsTheLoopDraws(new Key('e'));

        var list = Languages(drawn).FrameToScreen();

        Assert.True(Languages(drawn).Visible);
        Assert.All(Pictures(pictured), picture => Assert.Contains(picture, list.Contains));
        Assert.All(Pictures(pictured), picture => Assert.Contains(picture, cell => !list.Contains(cell)));
        Assert.Equal(Uncovered(pictured, list), shown.Cells());

        Close(drawn, closing);

        Assert.False(Languages(drawn).Visible);
        Assert.Equal(pictured, shown.Cells());
        Assert.Equal(rows[list.Top..list.Bottom], drawn.Rows()[list.Top..list.Bottom]);
    }

    /// <summary>
    ///     The people to mention, flipped over the post where there is no room under its line, reach up over the Media
    ///     rows. Hung on an <c>@</c> typed further and further along, they cover the whole of each picture, then all of
    ///     it but its left-hand column, then its right-hand column alone: only what they cover is hidden, and all of it
    ///     comes back as they close.
    /// </summary>
    [Theory]
    [InlineData(PictureWay.Placeholders, 5)]
    [InlineData(PictureWay.Placeholders, 6)]
    [InlineData(PictureWay.Placeholders, 7)]
    [InlineData(PictureWay.Kitty, 5)]
    [InlineData(PictureWay.Kitty, 6)]
    [InlineData(PictureWay.Kitty, 7)]
    [InlineData(PictureWay.Sixel, 5)]
    [InlineData(PictureWay.Sixel, 6)]
    [InlineData(PictureWay.Sixel, 7)]
    public async Task ThePeopleToMentionFlippedOverAPictureHideWhatTheyCoverAndGiveItBackWhole(PictureWay way, int at)
    {
        using var drawn = await Attached(way, height: 24, Five);
        using var shown = ShownPictures.Over(drawn, way);
        var pictured = shown.Cells();
        var rows = drawn.Rows();

        Type(drawn, new string(' ', at) + "@ma");

        var list = Flipped(drawn);

        Assert.All(Pictures(pictured), picture => Assert.Equal(8 - at, picture.Count(list.Contains)));
        Assert.Equal(Uncovered(pictured, list), shown.Cells());

        Close(drawn, Closing.Esc);

        Assert.False(Mentions(drawn).Visible);
        Assert.Equal(pictured, shown.Cells());
        Assert.Equal(rows[list.Top..list.Bottom], drawn.Rows()[list.Top..list.Bottom]);
    }

    /// <summary>
    ///     However else the flipped list closes — a pick, a click outside it, or the caret leaving the word — the pictures
    ///     it covered come back whole, and nothing of the list is left over the rows it covered.
    /// </summary>
    [Theory]
    [InlineData(PictureWay.Placeholders, Closing.Pick)]
    [InlineData(PictureWay.Placeholders, Closing.ClickOutside)]
    [InlineData(PictureWay.Placeholders, Closing.CaretAway)]
    [InlineData(PictureWay.Kitty, Closing.Pick)]
    [InlineData(PictureWay.Kitty, Closing.ClickOutside)]
    [InlineData(PictureWay.Kitty, Closing.CaretAway)]
    [InlineData(PictureWay.Sixel, Closing.Pick)]
    [InlineData(PictureWay.Sixel, Closing.ClickOutside)]
    [InlineData(PictureWay.Sixel, Closing.CaretAway)]
    public async Task HoweverTheFlippedPeopleToMentionCloseThePicturesComeBackWhole(PictureWay way, Closing closing)
    {
        using var drawn = await Attached(way, height: 24, Five);
        using var shown = ShownPictures.Over(drawn, way);
        var pictured = shown.Cells();
        var rows = drawn.Rows();

        Type(drawn, "      @ma");

        var list = Flipped(drawn);

        Close(drawn, closing);

        Assert.False(Mentions(drawn).Visible);
        Assert.Equal(pictured, shown.Cells());
        Assert.Equal(rows[list.Top..list.Bottom], drawn.Rows()[list.Top..list.Bottom]);
    }

    /// <summary>
    ///     The flipped list opened again with nothing typed — the caret taken off the word and back to it — hides the
    ///     pictures under it just as it did when it opened on a letter: left as it was, a Kitty placement would still sit
    ///     over the list, and a sixel would show through its blank cells.
    /// </summary>
    [Theory]
    [InlineData(PictureWay.Placeholders)]
    [InlineData(PictureWay.Kitty)]
    [InlineData(PictureWay.Sixel)]
    public async Task TheFlippedPeopleToMentionOpenedAgainByTheCaretHideWhatTheyCover(PictureWay way)
    {
        using var drawn = await Attached(way, height: 24, Five);
        using var shown = ShownPictures.Over(drawn, way);
        var pictured = shown.Cells();

        Type(drawn, "      @ma");
        Close(drawn, Closing.CaretAway);

        Assert.Equal(pictured, shown.Cells());

        drawn.PressAsTheLoopDraws(Key.End);

        Assert.Equal(Uncovered(pictured, Flipped(drawn)), shown.Cells());
    }

    /// <summary>
    ///     The compose screen on a 100-column terminal <paramref name="height" /> rows tall drawing pictures
    ///     <paramref name="way" />, with four pictures attached, their pixels here.
    /// </summary>
    private async Task<DrawnShell> Attached(PictureWay way, int height, AShell? built = null)
    {
        var files = Enumerable.Range(0, 4).Select(at => _files.WriteFile($"cat{at}.png")).ToList();
        var pictures = new FakePictures();

        foreach (var file in files)
        {
            pictures.HoldingPhotograph(Drawn.Attaching(file).Id, 40, 30);
        }

        var drawn = await DrawnShell.Of(
            100,
            height,
            Themes.Dark,
            built,
            pictures: pictures,
            drawsPictures: way is PictureWay.Sixel,
            answersKitty: way is PictureWay.Kitty,
            drawsPlaceholders: way is PictureWay.Placeholders,
            raster: way switch
            {
                PictureWay.Sixel => ARaster.Sixel(),
                PictureWay.Kitty => ARaster.Kitty(),
                _ => ARaster.Placeholders(),
            });

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

    /// <summary>
    ///     <c>↑</c> pressed in compose, drawn as the loop draws it, until the typing is in <paramref name="field" />.
    /// </summary>
    private static void WalkTo(DrawnShell drawn, ComposeField field)
    {
        var compose = Assert.IsType<ComposeScreen>(drawn.Shell.Screen);

        for (var pressed = 0; compose.Typing != field; pressed++)
        {
            Assert.True(pressed < 20, $"↑ never reached {field}");
            drawn.PressAsTheLoopDraws(Key.CursorUp);
        }
    }

    /// <summary>The open list closed by <paramref name="closing" />, drawn as the loop draws it.</summary>
    private static void Close(DrawnShell drawn, Closing closing)
    {
        switch (closing)
        {
            case Closing.Esc:
                drawn.PressAsTheLoopDraws(Key.Esc);

                break;

            case Closing.Pick:
                drawn.PressAsTheLoopDraws(Key.Tab);

                break;

            case Closing.CaretAway:
                drawn.PressAsTheLoopDraws(Key.Home);

                break;

            default:
                var row = Array.FindIndex(drawn.Rows(), text => text.Contains("From  ", StringComparison.Ordinal)) - 1;

                drawn.Reports(Sgr.Press(60, row), Sgr.Release(60, row));

                break;
        }
    }

    /// <summary>The people to mention, open over the post's line rather than under it, on the screen.</summary>
    private static Rectangle Flipped(DrawnShell drawn)
    {
        var list = Mentions(drawn);
        var editor = drawn.Window.SubViews.OfType<ComposeView>().Single().SubViews.OfType<ComposeEditor>().Single();

        Assert.True(list.Visible);
        Assert.True(list.FrameToScreen().Bottom <= editor.FrameToScreen().Y, string.Join('\n', drawn.Rows()));

        return list.FrameToScreen();
    }

    private static View Languages(DrawnShell drawn) => List(drawn, LanguageList.Id);

    private static View Mentions(DrawnShell drawn) => List(drawn, MentionList.Id);

    private static View List(DrawnShell drawn, string id) =>
        drawn.Window.SubViews.OfType<ComposeView>().Single().SubViews.Single(view => view.Id == id);

    /// <summary>The cells of each picture, one row of cells a picture, as the attachments' are.</summary>
    private static List<HashSet<Point>> Pictures(HashSet<Point> pictured) =>
        [.. pictured.GroupBy(cell => cell.Y).Select(row => row.ToHashSet())];

    /// <summary>The cells of <paramref name="pictured" /> outside <paramref name="list" />.</summary>
    private static HashSet<Point> Uncovered(HashSet<Point> pictured, Rectangle list) =>
        [.. pictured.Where(cell => !list.Contains(cell))];
}
