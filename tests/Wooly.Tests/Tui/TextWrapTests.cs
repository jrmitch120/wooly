using Wooly.Tui;
using Wooly.Tui.Rendering;

namespace Wooly.Tests.Tui;

/// <summary>
///     Fitting somebody else's text into the columns a terminal has, and the two other scraps the shell needs before
///     it can draw anything: how long ago a post was, and which profile a run was started as.
/// </summary>
public class TextWrapTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The narrow case the whole shape was chosen for.</summary>
    [Fact]
    public void Wrap_BreaksTextToTheColumnsThereAre()
    {
        var wrapped = TextWrap.Wrap("Finally shipped the terminal client rewrite at last", 20);

        Assert.All(wrapped, row => Assert.True(Glyphs.Columns(row) <= 20, $"'{row}' is {Glyphs.Columns(row)} columns"));
        Assert.Equal("Finally shipped the terminal client rewrite at last", string.Join(" ", wrapped));
    }

    /// <summary>A post laid out in short lines was laid out that way on purpose.</summary>
    [Fact]
    public void Wrap_KeepsTheAuthorsOwnLineBreaks()
    {
        Assert.Equal(["one", "two", string.Empty, "three"], TextWrap.Wrap("one\ntwo\n\nthree", 40));
    }

    /// <summary>
    ///     A word longer than the whole row is cut rather than allowed to run off the side. Half a URL is worth more to
    ///     a reader than an ellipsis where one was.
    /// </summary>
    [Fact]
    public void Wrap_CutsAWordTooLongForTheRowRatherThanOverflowing()
    {
        var wrapped = TextWrap.Wrap("see https://example.com/a/very/long/path/indeed/that/keeps/going", 20);

        Assert.All(wrapped, row => Assert.True(Glyphs.Columns(row) <= 20, $"'{row}' is {Glyphs.Columns(row)} columns"));
        Assert.Contains(wrapped, row => row.StartsWith("https://", StringComparison.Ordinal));
    }

    [Fact]
    public void Wrap_AnswersNothingForARowWithNoColumnsInIt()
    {
        Assert.Empty(TextWrap.Wrap("anything at all", 0));
    }

    /// <summary>
    ///     Every row is a slice of the text it came out of, at the offset it says it is at (#83). The property the
    ///     whole reference walk rests on: a reference is found once on the flattened text, and a row's own offset is
    ///     what says which of them are written on it.
    /// </summary>
    [Theory]
    [InlineData("Finally shipped the terminal client rewrite at last", 20)]
    [InlineData("one\ntwo\n\nthree", 40)]
    [InlineData("see https://example.com/a/very/long/path/indeed/that/keeps/going", 20)]
    [InlineData("spaced  out  wider  than  anybody  meant  it  to  be", 12)]
    [InlineData("", 20)]
    [InlineData("ドット絵のアカウントです、よろしくおねがいします", 20)]
    [InlineData("Family \U0001F468\u200D\U0001F469\u200D\U0001F467 photos \U0001F389 today", 12)]
    public void Rows_AreSlicesOfTheTextAtTheOffsetTheySayTheyAreAt(string text, int width)
    {
        foreach (var row in TextWrap.Rows(text, width))
        {
            Assert.Equal(row.Text, text.Substring(row.At, row.Text.Length));
        }
    }

    /// <summary>The rows are the same rows either way, so nothing that only wanted the text has to know about offsets.</summary>
    [Fact]
    public void Rows_AreTheRowsWrapAnswersWith()
    {
        const string Said = "Finally shipped it.\nNotes at https://example.com/a/long/enough/path/to/be/cut and more";

        Assert.Equal(TextWrap.Wrap(Said, 20), TextWrap.Rows(Said, 20).Select(row => row.Text));
    }

    /// <summary>
    ///     A word cut in half is two rows of the same word, and the second one says where in the word it starts —
    ///     which is what stops the second half of a long address matching nothing (#83).
    /// </summary>
    [Fact]
    public void Rows_SayWhereEachHalfOfAWordTooLongForTheRowStarts()
    {
        const string Said = "see https://example.com/a/very/long/path/indeed/that/keeps/going now";

        var rows = TextWrap.Rows(Said, 20);

        // "see", then the address cut at the twentieth column of it and picked up again at the twenty-first.
        Assert.Equal("https://example.com/", rows[1].Text);
        Assert.Equal(4, rows[1].At);
        Assert.Equal(24, rows[2].At);
        Assert.StartsWith("a/very/", rows[2].Text, StringComparison.Ordinal);
    }

    /// <summary>Clipping marks where it cut, so a reader can tell a shortened name from a short one.</summary>
    [Theory]
    [InlineData("Maria Ochoa", 20, "Maria Ochoa")]
    [InlineData("Maria Ochoa", 8, "Maria O…")]
    [InlineData("Maria Ochoa", 1, "…")]
    [InlineData("Maria Ochoa", 0, "")]
    public void Clip_CutsToWidthAndSaysThatItDid(string text, int width, string expected)
    {
        Assert.Equal(expected, TextWrap.Clip(text, width));
    }

    /// <summary>
    ///     A row is as many columns as the terminal has, not as many characters: a post written in a script whose
    ///     characters take two columns each wrapped at twice the width it was given, every row (#207).
    /// </summary>
    [Theory]
    [InlineData("ドット絵のアカウントです、よろしくおねがいします", 20)]
    [InlineData("ドット絵のアカウントです、よろしくおねがいします", 61)]
    [InlineData("絵文字 \U0001F389 and \U0001F468\u200D\U0001F469\u200D\U0001F467 mixed into a line of plain words", 20)]
    public void Wrap_BreaksTextToTheColumnsRatherThanToTheCharacters(string text, int width)
    {
        var wrapped = TextWrap.Wrap(text, width);

        Assert.All(
            wrapped,
            row => Assert.True(Glyphs.Columns(row) <= width, $"'{row}' is {Glyphs.Columns(row)} columns"));

        // And nothing dropped on the way: every character is still on one row or another, in the order written.
        Assert.Equal(text.Replace(" ", string.Empty), string.Concat(wrapped).Replace(" ", string.Empty));
    }

    /// <summary>
    ///     A character too wide for the whole row is drawn anyway, on a row of its own. The region is narrower than
    ///     anything that could go in it, and a row holding nothing would be a row that never got past this character.
    /// </summary>
    [Fact]
    public void Wrap_DrawsACharacterWiderThanTheWholeRowRatherThanStalling()
    {
        Assert.Equal(["ド", "ッ", "ト"], TextWrap.Wrap("ドット", 1));
    }

    /// <summary>Clipping counts the columns too, and marks the cut in the one column an ellipsis takes.</summary>
    [Theory]
    [InlineData("ドット絵アカウント", 20, "ドット絵アカウント")]
    [InlineData("ドット絵アカウント", 18, "ドット絵アカウント")]
    [InlineData("ドット絵アカウント", 10, "ドット絵…")]
    // A column short rather than a column over: the ninth column cannot hold half of a two-column character.
    [InlineData("ドット絵アカウント", 9, "ドット絵…")]
    [InlineData("ドット絵アカウント", 2, "…")]
    public void Clip_CutsToTheColumnsRatherThanToTheCharacters(string text, int width, string expected)
    {
        Assert.Equal(expected, TextWrap.Clip(text, width));
        Assert.True(Glyphs.Columns(TextWrap.Clip(text, width)) <= width);
    }

    /// <summary>
    ///     And never inside one of the things a reader would call a character: half of a ZWJ sequence is an emoji
    ///     nobody wrote, and a base character parted from its combining mark is a word misspelt.
    /// </summary>
    [Theory]
    [InlineData("\U0001F468\u200D\U0001F469\u200D\U0001F467 and me", 3, "\U0001F468\u200D\U0001F469\u200D\U0001F467…")]
    [InlineData("e\u0301clair time", 4, "e\u0301cl…")]
    public void Clip_NeverCutsInsideACharacterAsAReaderCountsThem(string text, int width, string expected) =>
        Assert.Equal(expected, TextWrap.Clip(text, width));

    /// <summary>The two or three characters a feed has room for at the end of a byline.</summary>
    [Theory]
    [InlineData(30, "now")]
    [InlineData(60 * 12, "12m")]
    [InlineData(60 * 60, "1h")]
    [InlineData(60 * 119, "1h")]
    [InlineData(60 * 60 * 24 * 3, "3d")]
    [InlineData(60 * 60 * 24 * 400, "1y")]
    public void Since_SaysHowLongAgoInTheRoomABylineHas(int secondsAgo, string expected)
    {
        Assert.Equal(expected, Elapsed.Since(Now - TimeSpan.FromSeconds(secondsAgo), Now));
    }

    /// <summary>An instance whose clock is ahead of this machine's has not posted in the future.</summary>
    [Fact]
    public void Since_ReadsAMomentInTheFutureAsNowRatherThanAsANegativeAge()
    {
        Assert.Equal("now", Elapsed.Since(Now + TimeSpan.FromMinutes(2), Now));
    }

    /// <summary>Story 9's flag, in both the spellings a user might type it.</summary>
    [Theory]
    [InlineData(new[] { "--profile", "work" }, "work")]
    [InlineData(new[] { "--profile=work" }, "work")]
    [InlineData(new string[0], null)]
    [InlineData(new[] { "--profile" }, null)]
    [InlineData(new[] { "--profile", "  " }, null)]
    [InlineData(new[] { "--something-else", "work" }, null)]
    public void NamedIn_ReadsTheProfileTheRunWasStartedAs(string[] args, string? expected)
    {
        Assert.Equal(expected, StartupProfile.NamedIn(args));
    }
}
