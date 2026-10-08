using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     What is attached, drawn (#382): a small picture on each pending attachment's row under the Media header, on a
///     terminal that can draw, through the picture path the feed's pictures take (ADR-0022, ADR-0023, ADR-0025) — and
///     nothing drawn, and nothing moved, on one that cannot.
/// </summary>
public class ComposePicturesTests : IDisposable
{
    private const int Width = 98;

    private const int Height = 27;

    /// <summary>Where a row's picture column starts: after the grip, a column of air either side of it.</summary>
    private const int PictureColumn = 7;

    private readonly TemporaryDirectory _files = new();

    public void Dispose() => _files.Dispose();

    /// <summary>
    ///     An attached picture whose pixels are here is set into its row's picture column, three wide and one tall, on
    ///     each way a terminal draws — sixel, Kitty through a box, and Kitty's placeholders — and the row says it wants
    ///     the picture, which is what has it read off the disk.
    /// </summary>
    [Theory]
    [InlineData(PictureWay.Sixel)]
    [InlineData(PictureWay.Kitty)]
    [InlineData(PictureWay.Placeholders)]
    public async Task AnAttachedPictureIsSetIntoItsRow(PictureWay way)
    {
        var (compose, cat) = await Attached("cat.png");
        var pictures = new FakePictures().Holding(Drawn.Attaching(cat).Id, 40, 30);

        var row = Row(compose, pictures, Of(way), "cat.png");

        Assert.Equal(new Inset(Drawn.Attaching(cat), PictureColumn, 3, 1), Assert.Single(row.Insets));
        Assert.Equal(Drawn.Attaching(cat), row.Wants);
        Assert.Equal("   ", row.Text[PictureColumn..(PictureColumn + 3)]);
    }

    /// <summary>
    ///     While the picture is on its way the column holds the <b>Stand-in</b>'s shade, and the row still wants the
    ///     picture, so that it is read and drawn into the same three columns without anything beside it moving.
    /// </summary>
    [Fact]
    public async Task APictureOnItsWayHoldsItsColumnWithTheStandIn()
    {
        var (compose, cat) = await Attached("cat.png");

        var row = Row(compose, new FakePictures(), ARaster.Sixel(), "cat.png");

        Assert.Empty(row.Insets);
        Assert.Equal(Drawn.Attaching(cat), row.Wants);
        Assert.Contains(row.Spans, span => span is { Text: "░░░", Role: Role.StandIn });
    }

    /// <summary>
    ///     A terminal that draws no pictures is asked for none and shown none: the column stays blank, and every other
    ///     column of the row is where it is on a terminal that draws — so a picture arriving moves nothing either.
    /// </summary>
    [Fact]
    public async Task NothingIsDrawnOrWantedOnATerminalThatCannotDraw()
    {
        var (compose, cat) = await Attached("cat.png");
        var pictures = new FakePictures().Holding(Drawn.Attaching(cat).Id, 40, 30);

        var undrawn = Row(compose, pictures, Raster.None, "cat.png");
        var drawn = Row(compose, pictures, ARaster.Sixel(), "cat.png");

        Assert.Empty(undrawn.Insets);
        Assert.Null(undrawn.Wants);
        Assert.Equal(drawn.Text, undrawn.Text);
        Assert.Equal(
            "     ⠶     cat.png                           picture      25 B  x  ████░░░░  54%",
            undrawn.Text.TrimEnd());
    }

    /// <summary>
    ///     A video or a sound has no picture to read off the disk, nor does a picture of a kind this client cannot
    ///     decode: their column stays blank and nothing is wanted, rather than a shade held for a picture never coming.
    /// </summary>
    [Theory]
    [InlineData("clip.mp4")]
    [InlineData("song.mp3")]
    [InlineData("phone.heic")]
    public async Task WhatCannotBeDecodedIsNotWanted(string name)
    {
        var (compose, _) = await Attached(name);

        var row = Row(compose, new FakePictures(), ARaster.Sixel(), name);

        Assert.Empty(row.Insets);
        Assert.Null(row.Wants);
        Assert.Equal("   ", row.Text[PictureColumn..(PictureColumn + 3)]);
    }

    /// <summary>
    ///     A picture's arrival changes no row's height: the screen has as many rows, each the same, before and after,
    ///     but for the shade in the picture's own three columns.
    /// </summary>
    [Fact]
    public async Task APictureArrivingChangesNoRowsHeight()
    {
        var (compose, cat) = await Attached("cat.png");

        var before = compose.Lines(new Drawing(Width, AShell.Now, new FakePictures(), ARaster.Sixel(), Height: Height));
        var after = compose.Lines(new Drawing(
            Width,
            AShell.Now,
            new FakePictures().Holding(Drawn.Attaching(cat).Id, 40, 30),
            ARaster.Sixel(),
            Height: Height));

        Assert.Equal(before.Count, after.Count);
        Assert.Equal(
            before.Select(line => line.Text.Replace("⠶ ░░░", "⠶    ", StringComparison.Ordinal)),
            after.Select(line => line.Text));
    }

    /// <summary>
    ///     Wiring: a sixel terminal draws an attached picture through one of the content panel's boxes, at the row's
    ///     picture column — what no test of the rows alone covers.
    /// </summary>
    [Fact]
    public async Task ASixelTerminalDrawsTheRowsPictureThroughABox()
    {
        var cat = _files.WriteFile("cat.png");
        var built = new AShell();

        using var drawn = await DrawnShell.Of(
            100,
            30,
            Themes.Plain,
            built,
            pictures: new FakePictures().HoldingPhotograph(Drawn.Attaching(cat).Id, 40, 30),
            drawsPictures: true);

        drawn.Shell.Compose();
        drawn.Shell.Paste(cat);
        drawn.Settle();

        var box = Assert.Single(drawn.Content.SubViews.OfType<PictureView>(), view => view.Visible);
        var row = Array.FindIndex(drawn.Rows(), text => text.Contains("cat.png", StringComparison.Ordinal));

        Assert.Equal(row, box.FrameToScreen().Y);
        Assert.Equal(Drawn.Attaching(cat).Id, box.PictureId);
    }

    /// <summary>A shell opened on a fresh post with <paramref name="name" /> dropped onto it and a third of the way up.</summary>
    private async Task<(ComposeScreen Compose, string Path)> Attached(string name)
    {
        var built = new AShell();
        var shell = await built.Opened();
        var compose = ComposeRows.Open(shell, ComposeFor.Post);
        var path = _files.WriteFile(name, "a picture of 25 bytes....");

        shell.Paste(path);
        built.Author.Attaching.Single().Progress(0.54);
        built.Host.Drain();

        return (compose, path);
    }

    private static Line Row(ComposeScreen compose, IPictures pictures, Raster raster, string name) =>
        compose.Lines(new Drawing(Width, AShell.Now, pictures, raster, Height: Height))
               .Single(line => line.Text.Contains(name, StringComparison.Ordinal));

    private static Raster Of(PictureWay way) => way switch
    {
        PictureWay.Sixel => ARaster.Sixel(),
        PictureWay.Kitty => ARaster.Kitty(),
        _ => ARaster.Placeholders(),
    };
}
