using Terminal.Gui.Input;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Wooly.Tests.Tui;

/// <summary>
///     A drawn still picture is a <b>Reference</b>: <c>←</c>/<c>→</c> reach it after the post's text references, and a
///     walked one is drawn between tall brackets in the columns either side of its box (#515, ADR-0034). Read back off
///     the whole shell drawn headless in the no-colour theme, because what a reader sees is the brackets.
/// </summary>
public class PictureWalkTests
{
    /// <summary>
    ///     The first column a screen draws in on an 80-column terminal: just past the rail's 20 columns, the panel's
    ///     left edge and its margin — the column a picture's left bracket is drawn in.
    /// </summary>
    private const int Left = RailLines.Width + 2;

    /// <summary>The last column a screen draws in there, which a picture's right bracket is drawn in.</summary>
    private const int Right = 77;

    /// <summary>Every glyph a walked picture's brackets are drawn in, either side, a one-row box's included.</summary>
    private const string Brackets = "⎡⎢⎣⎤⎥⎦[]";

    /// <summary>What the status row says only while a reference is walked to.</summary>
    private const string ReferenceMode = "Open: ⏎";

    /// <summary>Wider than any content region, so that the region's width is what bounds the box.</summary>
    private static readonly PictureShape Wide = new(4000, 400);

    [Fact]
    public async Task RightFromAPostsLastTextReferenceReachesItsFirstPictureAndBracketsEveryRowOfItsBox()
    {
        using var drawn = await Drawn(APost.With(
            id: "110",
            content: "Shearing #wool",
            media: [APost.APicture("m1", shape: Wide)]));

        drawn.Press(Key.CursorRight);

        Assert.Contains(drawn.Rows(), row => row.Contains("‹#wool›", StringComparison.Ordinal));
        Assert.Empty(Bracketed(drawn));

        drawn.Press(Key.CursorRight);

        var rows = Bracketed(drawn);

        Assert.DoesNotContain(drawn.Rows(), row => row.Contains('‹'));
        Assert.True(rows.Count > 2, string.Join('\n', drawn.Rows()));
        Assert.Equal(('⎡', '⎤'), rows[0].Glyphs);
        Assert.All(rows[1..^1], row => Assert.Equal(('⎢', '⎥'), row.Glyphs));
        Assert.Equal(('⎣', '⎦'), rows[^1].Glyphs);
        Assert.Equal(rows.Count, rows[^1].Row - rows[0].Row + 1);
    }

    /// <summary>
    ///     The walk goes down the post's attachments in the order they are drawn, a picture's box and a video's label
    ///     alike — the video's box never bracketed, its label being what is walked to.
    /// </summary>
    [Fact]
    public async Task TheWalkGoesThroughPicturesAndLabelsInTheOrderTheyAreDrawn()
    {
        using var drawn = await Drawn(APost.With(
            id: "110",
            content: "Shearing #wool",
            media:
            [
                APost.APicture("m1", description: "The first sheep", shape: Wide),
                APost.Attached(MediaKind.Video, "m2", description: "Shearing, at length", shape: Wide),
                APost.APicture("m3", description: "The second sheep", shape: Wide),
            ]));

        drawn.Press(Key.CursorRight);
        drawn.Press(Key.CursorRight);
        Assert.Equal("The first sheep", BracketedUnder(drawn));

        drawn.Press(Key.CursorRight);
        Assert.Empty(Bracketed(drawn));
        Assert.Contains(drawn.Rows(), row => row.Contains("‹Video› Shearing, at length", StringComparison.Ordinal));

        drawn.Press(Key.CursorRight);
        Assert.Equal("The second sheep", BracketedUnder(drawn));
        Assert.DoesNotContain(drawn.Rows(), row => row.Contains('‹'));

        drawn.Press(Key.CursorLeft);
        drawn.Press(Key.CursorLeft);
        Assert.Equal("The first sheep", BracketedUnder(drawn));
    }

    /// <summary>A box one row tall has room for no tall bracket, and is drawn between square ones.</summary>
    [Fact]
    public async Task AOneRowBoxIsDrawnBetweenSquareBrackets()
    {
        using var drawn = await Drawn(APost.With(id: "110", media: [APost.APicture("m1", shape: new(4000, 100))]));

        drawn.Press(Key.CursorRight);

        Assert.Equal(('[', ']'), Assert.Single(Bracketed(drawn)).Glyphs);
    }

    /// <summary>
    ///     Walking to a picture and letting it go change the two bracket columns beside its box and nothing else on the
    ///     page — not where its pixels are, nor any row around it — with the pixels landed and before they have.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WalkingToAPictureAndOffItMovesNoCellOutsideTheBracketColumns(bool landed)
    {
        var pictures = landed ? new FakePictures().Holding("m1", Wide.Width, Wide.Height) : new FakePictures();

        using var drawn = await Drawn(
            APost.With(id: "110", content: "Shearing", media: [APost.APicture("m1", shape: Wide)]),
            pictures);

        var unwalked = Page(drawn);

        drawn.Press(Key.CursorRight);

        var walked = Page(drawn);

        drawn.Press(Key.Esc);

        var letGo = Page(drawn);
        var bracketed = Bracketed(drawn);

        Assert.Empty(bracketed);
        Assert.Equal(unwalked, letGo);

        var changed = Enumerable.Range(0, unwalked.Length)
            .SelectMany(row => Enumerable.Range(0, unwalked[row].Length).Select(column => (row, column)))
            .Where(cell => unwalked[cell.row][cell.column] != walked[cell.row][cell.column])
            .ToList();

        Assert.NotEmpty(changed);
        Assert.All(changed, cell => Assert.Contains(cell.column, new[] { Left, Right }));
    }

    /// <summary>
    ///     A picture taller than the page, walked to and scrolled partly off, is still bracketed on every row of it on
    ///     the page — the rows scrolled off taking their brackets with them, and the box's foot keeping its own.
    /// </summary>
    [Fact]
    public async Task APartlyScrolledWalkedPictureIsBracketedOnTheRowsOnThePage()
    {
        using var drawn = await Drawn(
            APost.With(id: "110", media: [APost.APicture("m1", shape: new(400, 400))]),
            height: 16);

        drawn.Press(Key.CursorRight);

        for (var pressed = 0; pressed < 20 && Bracketed(drawn).Any(row => row.Glyphs.Left == '⎡'); pressed++)
        {
            drawn.Press(Key.CursorDown);
        }

        var rows = Bracketed(drawn);

        Assert.True(rows.Count > 1, string.Join('\n', drawn.Rows()));
        Assert.DoesNotContain(rows, row => row.Glyphs.Left == '⎡');
        Assert.All(rows[..^1], row => Assert.Equal(('⎢', '⎥'), row.Glyphs));
        Assert.Contains(rows[^1].Glyphs, new[] { ('⎢', '⎥'), ('⎣', '⎦') });
        Assert.Equal(rows.Count, rows[^1].Row - rows[0].Row + 1);
    }

    /// <summary>
    ///     A walked picture is let go the way a walked link is: by the pick moving off its post, and by every row of its
    ///     box leaving the page.
    /// </summary>
    [Fact]
    public async Task AWalkedPictureIsLetGoByMovingThePickAndByScrollingItOffThePage()
    {
        var post = APost.With(id: "110", media: [APost.APicture("m1", shape: Wide)]);

        using var moved = await Drawn(post);

        moved.Press(Key.CursorRight);
        moved.Press(Key.J);
        moved.Press(Key.K);

        Assert.Empty(Bracketed(moved));
        Assert.DoesNotContain(moved.Rows(), row => row.Contains(ReferenceMode, StringComparison.Ordinal));

        using var scrolled = await Drawn(post, height: 12);

        scrolled.Press(Key.CursorRight);

        for (var pressed = 0; pressed < 20 && Bracketed(scrolled).Count > 0; pressed++)
        {
            scrolled.Press(Key.CursorDown);
        }

        for (var pressed = 0; pressed < 20; pressed++)
        {
            scrolled.Press(Key.CursorUp);
        }

        Assert.Empty(Bracketed(scrolled));
        Assert.DoesNotContain(scrolled.Rows(), row => row.Contains(ReferenceMode, StringComparison.Ordinal));
    }

    /// <summary>
    ///     The brackets are glyphs rather than a colour, so they read the same on a terminal drawing no colour as on one
    ///     drawing the theme's.
    /// </summary>
    [Theory]
    [InlineData("plain")]
    [InlineData("dark")]
    public async Task TheBracketsShowWithAndWithoutColour(string theme)
    {
        using var drawn = await Drawn(
            APost.With(id: "110", media: [APost.APicture("m1", shape: Wide)]),
            theme: theme == "plain" ? Themes.Plain : Themes.Dark);

        drawn.Press(Key.CursorRight);

        Assert.Equal(('⎡', '⎤'), Bracketed(drawn)[0].Glyphs);
    }

    /// <summary>
    ///     A warned post's pictures are behind its warning, and not walked to until the reader asks past it with
    ///     <c>x</c> — whether it is warned by its author or only flagged sensitive.
    /// </summary>
    [Theory]
    [InlineData("Sheep", false)]
    [InlineData(null, true)]
    public async Task AWarnedPostsPicturesAreWalkedOnlyOnceItIsAskedPast(string? contentWarning, bool sensitive)
    {
        using var drawn = await Drawn(APost.With(
            id: "110",
            content: "Shearing",
            contentWarning: contentWarning,
            sensitive: sensitive,
            media: [APost.APicture("m1", shape: Wide)]));

        drawn.Press(Key.CursorRight);
        drawn.Press(Key.CursorRight);

        Assert.Empty(Bracketed(drawn));
        Assert.DoesNotContain(drawn.Rows(), row => row.Contains(ReferenceMode, StringComparison.Ordinal));

        drawn.Press(Key.X);
        drawn.Press(Key.CursorRight);

        Assert.Equal(('⎡', '⎤'), Bracketed(drawn)[0].Glyphs);
    }

    /// <summary>
    ///     On a terminal that cannot draw, a picture keeps its <c>⏵</c>, description and address and is not walked to:
    ///     there is no box to bracket.
    /// </summary>
    [Fact]
    public async Task APictureOnATerminalThatCannotDrawIsNotWalked()
    {
        using var drawn = await Drawn(
            APost.With(id: "110", content: "Shearing", media: [APost.APicture("m1", shape: Wide)]),
            drawsPictures: false);

        drawn.Press(Key.CursorRight);

        var rows = drawn.Rows();

        Assert.Contains(rows, row => row.Contains("⏵ A cartoon sheep", StringComparison.Ordinal));
        Assert.Contains(rows, row => row.Contains("https://files.mastodon.social/m1/original.png", StringComparison.Ordinal));
        Assert.Empty(Bracketed(drawn));
        Assert.DoesNotContain(rows, row => row.Contains(ReferenceMode, StringComparison.Ordinal));
    }

    /// <summary>
    ///     In a content region too narrow for a picture's box, only its description is drawn, and the picture is not
    ///     walked to: <c>→</c> stops on nothing nobody can see, and <c>⏎</c> opens the post rather than a picture.
    /// </summary>
    [Fact]
    public async Task APictureInARegionTooNarrowForItsBoxIsNotWalked()
    {
        // 25 columns leave a post two of the content region's, which is no room for a box between its brackets.
        using var drawn = await Drawn(APost.With(id: "110", media: [APost.APicture("m1", shape: Wide)]), width: 25);

        drawn.Press(Key.CursorRight);

        Assert.DoesNotContain(drawn.Rows(), row => row.Contains(StandIn.Shade));

        drawn.Press(Key.Enter);
        drawn.Settle();

        Assert.IsType<PostScreen>(drawn.Shell.Screen);
    }

    /// <summary>A video's or an animation's preview box is never walked to as a picture; its label is.</summary>
    [Theory]
    [InlineData(MediaKind.Video, "Video")]
    [InlineData(MediaKind.Animation, "Animation")]
    public async Task APreviewsBoxIsNeverBracketedAndItsLabelIsWalked(MediaKind kind, string label)
    {
        using var drawn = await Drawn(APost.With(id: "110", media: [APost.Attached(kind, "m1", shape: Wide)]));

        drawn.Press(Key.CursorRight);
        drawn.Press(Key.CursorRight);

        Assert.Empty(Bracketed(drawn));
        Assert.Contains(drawn.Rows(), row => row.Contains($"‹{label}›", StringComparison.Ordinal));
    }

    /// <summary>A link preview's picture is not something the author attached, and is never walked to as a picture.</summary>
    [Fact]
    public async Task ALinkPreviewsPictureIsNeverBracketed()
    {
        using var drawn = await Drawn(APost.With(id: "110", linkPreview: APost.ALinkPreview(shape: Wide)));

        drawn.Press(Key.CursorRight);
        drawn.Press(Key.CursorRight);

        Assert.Empty(Bracketed(drawn));
        Assert.Contains(drawn.Rows(), row => row.Contains('‹'));
    }

    /// <summary>Walking to a picture is the same on the post screen as in the feed it was opened from.</summary>
    [Fact]
    public async Task APictureIsWalkedToOnThePostScreenAsInTheFeed()
    {
        using var drawn = await Drawn(APost.With(
            id: "110",
            content: "Shearing #wool",
            media: [APost.APicture("m1", shape: Wide)]));

        drawn.Press(Key.Enter);
        drawn.Settle();

        Assert.IsType<PostScreen>(drawn.Shell.Screen);

        drawn.Press(Key.CursorRight);
        drawn.Press(Key.CursorRight);

        Assert.Equal(('⎡', '⎤'), Bracketed(drawn)[0].Glyphs);
    }

    /// <summary>
    ///     A picture is walked to by keys alone until mouse support comes: a click beside its box, where its bracket is
    ///     drawn, picks the post and walks to nothing, and a double click on the box hands the browser nothing.
    /// </summary>
    [Fact]
    public async Task AClickOnAPicturesBoxOrBesideItWalksToNothingAndOpensNothing()
    {
        using var drawn = await Drawn(APost.With(id: "110", media: [APost.APicture("m1", shape: Wide)]));

        drawn.Press(Key.CursorRight);

        var box = Bracketed(drawn)[1].Row;

        drawn.Press(Key.Esc);
        drawn.Click(Left, box);
        drawn.Redraw();

        Assert.Empty(Bracketed(drawn));

        drawn.DoubleClick(Left + 10, box);
        drawn.Settle();

        Assert.Empty(drawn.Built.Browser.Opened);
        Assert.Empty(Bracketed(drawn));
    }

    /// <summary>
    ///     The shell on an 80-column terminal, or one <paramref name="width" /> columns wide, drawing pictures in the
    ///     no-colour theme, reading <paramref name="post" /> and one more under it.
    /// </summary>
    private static Task<DrawnShell> Drawn(
        Post post,
        FakePictures? pictures = null,
        int height = 40,
        ITheme? theme = null,
        bool drawsPictures = true,
        int width = 80) =>
        DrawnShell.Of(
            width,
            height,
            theme ?? Themes.Plain,
            new AShell { Timelines = FakeTimelineReader.Holding(post, APost.With(id: "220")) },
            pictures: pictures ?? new FakePictures(),
            drawsPictures: drawsPictures);

    /// <summary>
    ///     What the row over the first bracketed one says past its mark — the description of the picture walked to,
    ///     which is drawn over its box.
    /// </summary>
    private static string BracketedUnder(DrawnShell drawn)
    {
        var over = drawn.Rows()[Bracketed(drawn)[0].Row - 1];

        return over[(over.IndexOf("▒▒▒▒ ", StringComparison.Ordinal) + 5)..].TrimEnd(' ', '│');
    }

    /// <summary>Every cell above the status row, as what is drawn in it and how.</summary>
    private static (string Grapheme, Attribute Attribute)[][] Page(DrawnShell drawn)
    {
        var contents = drawn.Application.Driver!.Contents!;

        return
        [
            .. Enumerable.Range(0, contents.GetLength(0) - 1).Select(row => Enumerable.Range(0, contents.GetLength(1))
                .Select(column => (contents[row, column].Grapheme, contents[row, column].Attribute!.Value))
                .ToArray()),
        ];
    }

    /// <summary>
    ///     Every row with a bracket in the left bracket column, and what is there and in the column past the box's
    ///     right edge — the last column a screen draws in where the box is the region's width, and nearer where a
    ///     tall picture's box is narrowed.
    /// </summary>
    private static List<(int Row, (char Left, char Right) Glyphs)> Bracketed(DrawnShell drawn) =>
    [
        .. Enumerable.Range(0, drawn.Application.Driver!.Contents!.GetLength(0))
            .Where(row => Brackets.Contains(At(drawn, row, Left)))
            .Select(row => (Row: row, Glyphs: (At(drawn, row, Left), Enumerable.Range(Left + 1, Right - Left)
                .Select(column => At(drawn, row, column))
                .LastOrDefault(glyph => Brackets.Contains(glyph), ' ')))),
    ];

    /// <summary>The one character drawn at <paramref name="column" /> of <paramref name="row" />.</summary>
    private static char At(DrawnShell drawn, int row, int column) =>
        drawn.Application.Driver!.Contents![row, column].Grapheme is [var only] ? only : '\0';
}
