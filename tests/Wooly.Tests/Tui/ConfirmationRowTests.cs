using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     A confirmation reserves the columns its answer needs, draws the question in what is left, and clips the
///     question — never the answer (#219, <c>docs/tui-shell.md</c>). The row may lose what it is asking about; it may
///     never lose how to answer.
/// </summary>
/// <remarks>
///     Asserted against the row, a value computed from a width (ADR-0005): no terminal is needed to know what it says.
/// </remarks>
public class ConfirmationRowTests
{
    /// <summary>The three confirmations the shell asks, as it asks them — the vote at one answer and at several.</summary>
    public static TheoryData<string> Asked => new("delete", "vote", "vote-one", "clear");

    private static Confirmation Of(string asked) => asked switch
    {
        "delete" => new Confirmation("Delete this post?"),
        "vote" => new Confirmation("Cast the 3 answers you ticked?", Going: "vote"),
        "vote-one" => new Confirmation("Cast the answer you ticked?", Going: "vote"),
        "clear" => new Confirmation("Clear every notification?", Going: "clear"),
        _ => throw new ArgumentOutOfRangeException(nameof(asked), asked, null),
    };

    private static string Answer(Confirmation asking) => $"{asking.Confirm} {asking.Going} · esc keep";

    private static Line Row(Confirmation asking, int width) =>
        ChromeLines.Status([], notice: null, noticeIsError: false, asking, width);

    /// <summary>At 80 columns, the contract's floor, every confirmation is drawn whole: the ask, the warning, the answer.</summary>
    [Theory]
    [InlineData("delete", " Delete this post? This cannot be undone.  y delete · esc keep")]
    [InlineData("vote", " Cast the 3 answers you ticked? This cannot be undone.  y vote · esc keep")]
    [InlineData("vote-one", " Cast the answer you ticked? This cannot be undone.  y vote · esc keep")]
    [InlineData("clear", " Clear every notification? This cannot be undone.  y clear · esc keep")]
    public void AtEightyColumns_EachConfirmationFitsWhole(string asked, string whole) =>
        Assert.Equal(whole, Row(Of(asked), 80).Text);

    /// <summary>
    ///     One column short of the whole row, it is the question that gives way — and the answer is still whole, at the
    ///     end of the row.
    /// </summary>
    [Theory]
    [MemberData(nameof(Asked))]
    public void OneColumnShortOfTheWholeRow_TheQuestionGivesWayAndTheAnswerIsWhole(string asked)
    {
        var asking = Of(asked);
        var whole = Row(asking, 120);
        var narrower = Row(asking, whole.Width - 1);

        Assert.True(narrower.Width <= whole.Width - 1, narrower.Text);
        Assert.EndsWith($"  {Answer(asking)}", narrower.Text, StringComparison.Ordinal);
        Assert.StartsWith($" {asking.Ask}", narrower.Text, StringComparison.Ordinal);
    }

    /// <summary>At every width from the floor to 120, the way to answer is on the row, unclipped, and ends it.</summary>
    [Theory]
    [MemberData(nameof(Asked))]
    public void FromTheFloorTo120_TheAnswerIsWhole(string asked)
    {
        var asking = Of(asked);

        for (var width = 80; width <= 120; width++)
        {
            var row = Row(asking, width);

            Assert.True(row.Width <= width, $"{width}: {row.Text}");
            Assert.EndsWith($"  {Answer(asking)}", row.Text, StringComparison.Ordinal);
        }
    }

    /// <summary>
    ///     Where the ask fits and the warning does not, the warning goes whole — no <c>…</c>, which would say there is
    ///     more about a sentence the reader has just read whole.
    /// </summary>
    [Theory]
    [InlineData(61)]
    [InlineData(40)]
    public void WhereTheAskFitsButNotTheWarning_TheWarningIsDroppedWhole(int width)
    {
        var row = Row(Of("delete"), width);

        Assert.Equal(" Delete this post?  y delete · esc keep", row.Text);
        Assert.DoesNotContain("…", row.Text, StringComparison.Ordinal);
    }

    /// <summary>Where not even the ask fits, the ask is clipped with a <c>…</c> — and the answer is still whole.</summary>
    [Fact]
    public void WhereNeitherFits_TheAskIsClippedAndTheAnswerIsWhole()
    {
        var row = Row(Of("delete"), 31);

        Assert.Equal(" Delete t…  y delete · esc keep", row.Text);
        Assert.Equal(31, row.Width);
    }

    /// <summary>The answer is kept in its own role, and the question in the one that says what kind of thing is asked.</summary>
    [Fact]
    public void TheQuestionAndTheAnswer_KeepTheirRoles()
    {
        var row = Row(Of("delete"), 61);

        Assert.Equal(" Delete this post?", row.Spans[0].Text);
        Assert.Equal(Role.Destructive, row.Spans[0].Role);
        Assert.DoesNotContain(row.Spans.Skip(1), span => span.Role == Role.Destructive);
    }

    /// <summary>
    ///     The warning is the same sentence on every confirmation, carrying nothing particular to what is asked — so
    ///     it is defaulted, and is <c>post delete</c>'s and <c>notification clear</c>'s own words.
    /// </summary>
    [Fact]
    public void AConfirmationBuiltWithOnlyAnAsk_CarriesTheDefaultWarning() =>
        Assert.Equal("This cannot be undone.", new Confirmation("Delete this post?").Warning);
}
