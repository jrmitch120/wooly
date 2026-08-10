using Wooly.Tui.Views;

namespace Wooly.Tests.Tui;

/// <summary>
///     Which of the pooled boxes draws which picture on a frame. The decision, not the drawing — pixels are a manual
///     smoke test (ADR-0005, ADR-0016), and this is the half a test can hold.
/// </summary>
/// <remarks>
///     Worth pinning on its own because the failure it exists to prevent is invisible until it is on somebody's
///     screen: a box neither moved nor released keeps its picture where it was, and a Kitty placement nobody deletes
///     is not erased by drawing text over it. It read as an avatar stuck across the middle of a post (#77).
/// </remarks>
public class PictureBoxTests
{
    /// <summary>
    ///     The bug itself. One picture wanted in one place and held in two — an account's avatar over a run of their
    ///     posts, one of which has just been scrolled past — leaves exactly one box drawing it and the other let go.
    /// </summary>
    [Fact]
    public void Boxes_LetGoOfASecondBoxHoldingAPictureThatIsNowWantedOnlyOnce()
    {
        var drawing = PaintedView.Boxes(["avatar:onion", "avatar:onion"], ["avatar:onion"]);

        var box = Assert.Single(drawing);

        Assert.NotNull(box);

        // The other box is spoken for by nothing, which is what gets it released.
        Assert.DoesNotContain(1 - box.Value, drawing.OfType<int>());
    }

    /// <summary>
    ///     The same picture wanted twice over gets two boxes, not one shared between them — two boxes sharing a view
    ///     would be one picture drawn and the other silently lost.
    /// </summary>
    [Fact]
    public void Boxes_GiveTheSamePictureWantedTwiceTwoBoxesOfItsOwn()
    {
        var drawing = PaintedView.Boxes(["avatar:onion", null, null], ["avatar:onion", "avatar:onion"]);

        Assert.Equal(2, drawing.OfType<int>().Distinct().Count());
        Assert.All(drawing, box => Assert.NotNull(box));
    }

    /// <summary>A box already holding what it has been given keeps it, rather than being handed somebody else's.</summary>
    [Fact]
    public void Boxes_KeepAPictureWhereItAlreadyIs()
    {
        var drawing = PaintedView.Boxes(["m1", "m2", null], ["m2", "m1"]);

        Assert.Equal([1, 0], drawing);
    }

    /// <summary>
    ///     A box holding nothing is preferred over one whose picture is being dropped this frame, so that a view never
    ///     goes from one picture straight to another within a frame.
    /// </summary>
    [Fact]
    public void Boxes_PreferAnEmptyBoxOverOneBeingReleasedThisFrame()
    {
        Assert.Equal([1], PaintedView.Boxes(["m1", null], ["m2"]));
    }

    /// <summary>
    ///     With nothing empty left, a box being dropped anyway is reused rather than the picture being skipped — a
    ///     screen fuller than the pool draws what it can.
    /// </summary>
    [Fact]
    public void Boxes_ReuseAReleasedBoxRatherThanDropAPicture()
    {
        Assert.Equal([0], PaintedView.Boxes(["m1"], ["m2"]));
    }

    /// <summary>More pictures than boxes: the ones there is no box for say so, rather than sharing one.</summary>
    [Fact]
    public void Boxes_SayWhichPicturesThereWasNoBoxLeftFor()
    {
        var drawing = PaintedView.Boxes([null, null], ["m1", "m2", "m3"]);

        Assert.Equal(2, drawing.OfType<int>().Distinct().Count());
        Assert.Single(drawing, box => box is null);
    }

    /// <summary>Nothing wanted, nothing spoken for — which is every box released.</summary>
    [Fact]
    public void Boxes_SpeakForNothingWhereNothingIsWanted()
    {
        Assert.Empty(PaintedView.Boxes(["m1", "m2"], []));
    }
}
