using SixLabors.ImageSharp;
using Color = Terminal.Gui.Drawing.Color;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;

namespace Wooly.Tests.Tui;

/// <summary>
///     When a picture is sent to a Kitty terminal's image store, and when it is forgotten again (#292, ADR-0022). The
///     store is a fake that records what it was told, so these are about the calls rather than the escape sequences.
/// </summary>
public class PlaceholdersTests
{
    private static readonly CellSize Cell = new(10, 20);

    [Fact]
    public void Ready_SendsAPictureOnceAndThenOnlyNamesIt()
    {
        var terminal = new FakeTerminalImages();
        using var placeholders = Inline(terminal);
        var inset = Box("m1", columns: 8, rows: 4);
        var picture = APicture(80, 80);

        var first = placeholders.Ready(inset, picture, Cell);
        var again = placeholders.Ready(inset, picture, Cell);

        Assert.NotNull(first);
        Assert.Equal(first, again);

        var sent = Assert.Single(terminal.Transmitted);

        Assert.Equal((first.Value, 8, 4), (sent.Id, sent.Columns, sent.Rows));
    }

    /// <summary>The PNG is the box's pixels exactly, so the picture is neither stretched nor scaled by the terminal.</summary>
    [Fact]
    public void Ready_SendsAPngTheSizeOfTheBoxInPixels()
    {
        var terminal = new FakeTerminalImages();
        using var placeholders = Inline(terminal);

        placeholders.Ready(Box("m1", columns: 8, rows: 4), APicture(300, 200), Cell);

        var sent = Image.Identify(Assert.Single(terminal.Transmitted).Png);

        Assert.Equal((80, 80), (sent.Width, sent.Height));
    }

    /// <summary>
    ///     A placeholder cell names an image rather than a placement, so one id can only ever mean one size: the same
    ///     picture in a box of another shape is a second image.
    /// </summary>
    [Fact]
    public void Ready_SendsThePictureAgainForABoxOfAnotherSize()
    {
        var terminal = new FakeTerminalImages();
        using var placeholders = Inline(terminal);
        var picture = APicture(80, 80);

        var small = placeholders.Ready(Box("m1", columns: 4, rows: 2), picture, Cell);
        var large = placeholders.Ready(Box("m1", columns: 8, rows: 4), picture, Cell);

        Assert.NotEqual(small, large);
        Assert.Equal(2, terminal.Transmitted.Count);
    }

    /// <summary>
    ///     The same avatar over a run of one author's posts is one image, however many boxes show it, and where each
    ///     box starts makes no difference to it.
    /// </summary>
    [Fact]
    public void Ready_SharesOneImageBetweenBoxesOfTheSameSize()
    {
        var terminal = new FakeTerminalImages();
        using var placeholders = Inline(terminal);
        var picture = APicture(96, 96);

        var one = placeholders.Ready(Box("avatar:onion", columns: 4, rows: 2), picture, Cell);
        var other = placeholders.Ready(Box("avatar:onion", columns: 4, rows: 2) with { Column = 6 }, picture, Cell);

        Assert.Equal(one, other);
        Assert.Single(terminal.Transmitted);
    }

    /// <summary>
    ///     The encode is not done on the frame that wants it: the box stays as it is until the PNG is ready, a redraw
    ///     is asked for, and the frame after that sends it.
    /// </summary>
    [Fact]
    public void Ready_AnswersNothingUntilThePictureHasBeenEncodedElsewhere()
    {
        var terminal = new FakeTerminalImages();
        var waiting = new List<Action>();
        var redraws = 0;
        using var placeholders = new Placeholders(terminal, true, () => redraws++, waiting.Add);
        var inset = Box("m1", columns: 8, rows: 4);
        var picture = APicture(80, 80);

        Assert.Null(placeholders.Ready(inset, picture, Cell));
        Assert.Null(placeholders.Ready(inset, picture, Cell));

        // Asked twice, encoded once.
        var encode = Assert.Single(waiting);

        Assert.Empty(terminal.Transmitted);

        encode();

        Assert.Equal(1, redraws);
        Assert.Empty(terminal.Transmitted);

        Assert.NotNull(placeholders.Ready(inset, picture, Cell));
        Assert.Single(terminal.Transmitted);
    }

    /// <summary>Ids are never reused within a run, and fit in the 24 bits of colour a placeholder cell carries them in.</summary>
    [Fact]
    public void Ready_GivesEveryImageAnIdOfItsOwnThatFitsInAColour()
    {
        var terminal = new FakeTerminalImages();
        using var placeholders = Inline(terminal);
        var picture = APicture(40, 40);

        var ids = Enumerable.Range(1, 5)
            .Select(columns => placeholders.Ready(Box("m1", columns, rows: 2), picture, Cell))
            .ToList();

        Assert.Equal(5, ids.Distinct().Count());
        Assert.All(ids, id => Assert.InRange(id!.Value, 1, 0xFFFFFF));
    }

    /// <summary>
    ///     Two runs in one terminal must not share ids: a placeholder is drawn with the first placement the terminal
    ///     holds for its id, and a stale one from an earlier run is a picture fitted into the wrong box.
    /// </summary>
    [Fact]
    public void Ready_StartsEachRunsIdsSomewhereElse()
    {
        var picture = APicture(40, 40);
        var firsts = Enumerable.Range(0, 8)
            .Select(_ =>
            {
                using var placeholders = Inline(new FakeTerminalImages());

                return placeholders.Ready(Box("m1", 4, 2), picture, Cell);
            })
            .ToList();

        Assert.True(firsts.Distinct().Count() > 1);
    }

    /// <summary>
    ///     A picture dropped from the cache is forgotten by the terminal, every size of it — but only on the frame
    ///     after, because a drop can be said on any thread and the terminal is written to on one.
    /// </summary>
    [Fact]
    public void Drop_ForgetsEverySizeOfThePictureOnTheNextFlush()
    {
        var terminal = new FakeTerminalImages();
        using var placeholders = Inline(terminal);
        var picture = APicture(80, 80);

        var small = placeholders.Ready(Box("m1", columns: 4, rows: 2), picture, Cell)!.Value;
        var large = placeholders.Ready(Box("m1", columns: 8, rows: 4), picture, Cell)!.Value;
        var other = placeholders.Ready(Box("m2", columns: 4, rows: 2), picture, Cell)!.Value;

        placeholders.Drop("m1");

        Assert.Empty(terminal.Forgotten);

        placeholders.Flush();

        Assert.Equal([small, large], terminal.Forgotten.Order());
        Assert.DoesNotContain(other, terminal.Forgotten);

        // And it is a new image if it is wanted again.
        Assert.NotEqual(small, placeholders.Ready(Box("m1", columns: 4, rows: 2), picture, Cell));
    }

    /// <summary>A picture that was never sent has nothing to forget.</summary>
    [Fact]
    public void Drop_ForgetsNothingTheTerminalWasNeverSent()
    {
        var terminal = new FakeTerminalImages();
        using var placeholders = Inline(terminal);

        placeholders.Drop("m1");
        placeholders.Flush();

        Assert.Empty(terminal.Forgotten);
    }

    /// <summary>
    ///     Everything is forgotten before the first picture of a run is sent and again on the way out, so that nothing
    ///     an earlier run left in the terminal is drawn and nothing this one sent outlives it.
    /// </summary>
    [Fact]
    public void Ready_ForgetsEverythingBeforeTheFirstPictureAndDisposeAgainOnTheWayOut()
    {
        var terminal = new FakeTerminalImages();
        var placeholders = Inline(terminal);
        var picture = APicture(40, 40);

        placeholders.Ready(Box("m1", 4, 2), picture, Cell);
        placeholders.Ready(Box("m2", 4, 2), picture, Cell);

        Assert.Equal(1, terminal.ForgotAll);

        placeholders.Dispose();

        Assert.Equal(2, terminal.ForgotAll);
    }

    /// <summary>A terminal that never drew a picture is never sent anything at all, not even a clean-up.</summary>
    [Fact]
    public void Dispose_SaysNothingToATerminalItNeverSentAPicture()
    {
        var terminal = new FakeTerminalImages();

        Inline(terminal).Dispose();

        Assert.Equal(0, terminal.ForgotAll);
    }

    /// <summary>
    ///     A box wider or taller than there are diacritics to number its cells cannot be drawn in placeholders, and
    ///     keeps the rows it reserved rather than being drawn wrong.
    /// </summary>
    [Fact]
    public void Ready_AnswersNothingForABoxTooLargeToNumber()
    {
        var terminal = new FakeTerminalImages();
        using var placeholders = Inline(terminal);

        Assert.Null(placeholders.Ready(
            Box("m1", columns: KittyPlaceholder.MostCells + 1, rows: 2),
            APicture(40, 40),
            Cell));

        Assert.Empty(terminal.Transmitted);
    }

    /// <summary>One row of a box as placeholder cells: the placeholder, the row's diacritic, then the column's.</summary>
    [Fact]
    public void Row_NumbersEveryCellByItsRowAndColumn()
    {
        Assert.Equal("\U0010EEEE\u030E\u030D", KittyPlaceholder.Of(2, 1));
        Assert.Equal(
            KittyPlaceholder.Of(2, 0) + KittyPlaceholder.Of(2, 1) + KittyPlaceholder.Of(2, 2),
            KittyPlaceholder.Row(2, columns: 3));
    }

    /// <summary>Encoded on the spot, which is what a test wants and what a frame must never do.</summary>
    private static Placeholders Inline(FakeTerminalImages terminal) =>
        new(terminal, true, () => { }, work => work());

    private static Inset Box(string id, int columns, int rows) =>
        new(new Drawn(id, $"https://files.example/{id}.png"), Column: 0, columns, rows);

    private static Picture APicture(int width, int height) => new(new Color[width, height]);
}
