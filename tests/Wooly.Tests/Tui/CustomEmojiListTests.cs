using Terminal.Gui.Input;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;

namespace Wooly.Tests.Tui;

/// <summary>
///     Typing <c>:</c> and two letters at the start of a word in the post hangs the instance's custom emoji whose
///     shortcodes match under it (#528, #578), in the list an @-word opens (#576) — driven the way a reader drives it:
///     keys and clicks into <see cref="ComposeView" /> over a shell, and what is drawn read back off the terminal.
/// </summary>
public class CustomEmojiListTests
{
    /// <summary>A <c>:</c>-word opens the list on the row under it, left-aligned on its <c>:</c>, naming what matches.</summary>
    [Fact]
    public async Task AColonWordOpensTheListUnderIt()
    {
        using var drawn = await Composing("blobcat", "blobfox", "thinking");

        Type(drawn, "hi :bl");

        var list = List(drawn).FrameToScreen();
        var editor = Editor(drawn).FrameToScreen();
        var rows = ListRows(drawn);

        Assert.True(List(drawn).Visible);
        Assert.Equal(editor.Y + 1, list.Y);
        Assert.Equal(editor.X + 3, list.X);
        Assert.Contains(rows, row => row.Contains(":blobcat:", StringComparison.Ordinal));
        Assert.Contains(rows, row => row.Contains(":blobfox:", StringComparison.Ordinal));
        Assert.DoesNotContain(rows, row => row.Contains(":thinking:", StringComparison.Ordinal));
        Assert.Contains("↑↓ pick  tab insert  esc close", rows[^1], StringComparison.Ordinal);
    }

    /// <summary>
    ///     Only a <c>:</c> at the start of a line or after a space starts a word, and only two letters after it open the
    ///     list: a time, a colon ending a word, an emoticon, a <c>:</c> inside a word and a single letter open nothing;
    ///     nor does a word no shortcode holds.
    /// </summary>
    [Theory]
    [InlineData("10:30")]
    [InlineData("note: blob")]
    [InlineData(":)")]
    [InlineData(":D")]
    [InlineData("re:blob")]
    [InlineData("http://blob")]
    [InlineData(":b")]
    [InlineData("hi :b")]
    [InlineData(":zz")]
    public async Task NoListOpensWhereThereIsNoShortcodeWord(string typed)
    {
        using var drawn = await Composing("blobcat", "D_D", "30s", "bb");

        Type(drawn, typed);

        Assert.False(List(drawn).Visible);
    }

    /// <summary>
    ///     A word may start a later line, and runs over letters, digits and <c>_</c>, but no further: a letter after it
    ///     that is none of those ends it, and the list with it.
    /// </summary>
    [Fact]
    public async Task AWordRunsOverLettersDigitsAndUnderscores()
    {
        using var drawn = await Composing("us_az1");

        Type(drawn, "first");
        drawn.Press(Key.Enter);
        Type(drawn, ":us_a");

        Assert.Contains(ListRows(drawn), row => row.Contains(":us_az1:", StringComparison.Ordinal));

        Type(drawn, "-");

        Assert.False(List(drawn).Visible);
    }

    /// <summary>
    ///     The list opens only with the caret at the end of the word: moved back inside it, where the word's letters
    ///     carry on after the caret, it closes, and picking could not leave the rest of the word behind; back at the end,
    ///     it opens again.
    /// </summary>
    [Fact]
    public async Task TheListOpensOnlyWithTheCaretAtTheEndOfTheWord()
    {
        using var drawn = await Composing("blobcat", "blobfox");

        Type(drawn, "hi :blobcat");

        Assert.True(List(drawn).Visible);

        for (var back = 0; back < "obcat".Length; back++)
        {
            drawn.Press(Key.CursorLeft);
        }

        Assert.False(List(drawn).Visible);

        drawn.Press(Key.End);

        Assert.True(List(drawn).Visible);
    }

    /// <summary>
    ///     Shortcodes starting with what was typed come before those that only hold it; within each, the shorter come
    ///     first and then the alphabetical, case ignored throughout; and there are five at most.
    /// </summary>
    [Fact]
    public async Task ShortcodesStartingWithTheWordComeFirst_ThenShorter_ThenAlphabetical_FiveAtMost()
    {
        using var drawn = await Composing(
            "ablobcatwave", "blobcat", "Catjam", "cat_heart", "CATS", "catboy", "thinking", "bobcat");

        Type(drawn, ":cAt");

        Assert.Equal(
            [":CATS:", ":catboy:", ":Catjam:", ":cat_heart:", ":bobcat:"],
            Shortcodes(drawn));
    }

    /// <summary>Where the starts all run out, those holding the word follow, shorter first.</summary>
    [Fact]
    public async Task ShortcodesHoldingTheWordFollowThoseStartingWithIt()
    {
        using var drawn = await Composing("ablobcatwave", "blobcat", "cat");

        Type(drawn, ":cat");

        Assert.Equal([":cat:", ":blobcat:", ":ablobcatwave:"], Shortcodes(drawn));
    }

    /// <summary>
    ///     A shortcode typed in full stays in the list, first, so picking it finishes it; typing its closing <c>:</c>
    ///     finishes it by hand, and closes the list.
    /// </summary>
    [Fact]
    public async Task AShortcodeTypedInFullStaysFirst_AndItsClosingColonClosesTheList()
    {
        using var drawn = await Composing("blobcat_wave", "blobcat", "ablobcat");

        Type(drawn, ":blobcat");

        Assert.Equal([":blobcat:", ":blobcat_wave:", ":ablobcat:"], Shortcodes(drawn));

        Type(drawn, ":");

        Assert.False(List(drawn).Visible);
        Assert.Equal(":blobcat:", Editor(drawn).Text);
    }

    /// <summary>
    ///     <c>tab</c> writes the picked emoji's shortcode and a space in place of the word, <c>enter</c> likewise, and a
    ///     click on one writes that one; the list closes each time.
    /// </summary>
    [Fact]
    public async Task TabEnterOrAClickWriteTheShortcodeAndASpace()
    {
        using var drawn = await Composing("blobcat", "blobfox");

        Type(drawn, "hi :bl");
        drawn.Press(Key.Tab);

        Assert.Equal("hi :blobcat: ", Editor(drawn).Text);
        Assert.False(List(drawn).Visible);

        Type(drawn, ":bl");
        drawn.Press(Key.CursorDown);
        drawn.Press(Key.Enter);

        Assert.Equal("hi :blobcat: :blobfox: ", Editor(drawn).Text);
        Assert.False(List(drawn).Visible);

        Type(drawn, ":bl");

        var at = List(drawn).FrameToScreen();

        drawn.Click(at.X + 4, at.Y + 2);

        Assert.Equal("hi :blobcat: :blobfox: :blobfox: ", Editor(drawn).Text);
        Assert.Equal("hi :blobcat: :blobfox: :blobfox: ", Assert.IsType<ComposeScreen>(drawn.Shell.Screen).Text);
        Assert.False(List(drawn).Visible);
    }

    /// <summary>
    ///     A shortcode too long for the list is held whole by the clip at its edge, as a shortcode is wherever a row is
    ///     clipped: dropped whole and the drop marked, never cut through, while the rows that fit are whole.
    /// </summary>
    [Fact]
    public async Task AShortcodeTooLongForTheListIsDroppedWhole_NeverCutThrough()
    {
        var longest = "blob" + new string('x', 120);

        using var drawn = await Composing("blobcat", longest);

        Type(drawn, ":bl");

        var rows = ListRows(drawn);

        Assert.StartsWith("│▌ :blobcat: ", rows[1], StringComparison.Ordinal);
        Assert.Matches("^│  … +│$", rows[2]);
    }

    /// <summary>One undo takes the written shortcode back out, and puts back the word it replaced.</summary>
    [Fact]
    public async Task UndoPutsTheWordBack()
    {
        using var drawn = await Composing("blobcat");

        Type(drawn, "hi :blo");
        drawn.Press(Key.Tab);
        drawn.Press(Key.Z.WithCtrl);

        Assert.Equal("hi :blo", Editor(drawn).Text);
    }

    /// <summary><c>↓</c> and <c>↑</c> move the pick and wrap round at either end; the wheel moves it and stops.</summary>
    [Fact]
    public async Task TheArrowsAndTheWheelMoveThePick()
    {
        using var drawn = await Composing("blobcat", "blobfox");

        Type(drawn, ":bl");

        Assert.StartsWith("│▌ :blobcat:", ListRows(drawn)[1], StringComparison.Ordinal);

        drawn.Press(Key.CursorDown);

        Assert.StartsWith("│▌ :blobfox:", ListRows(drawn)[2], StringComparison.Ordinal);

        drawn.Press(Key.CursorDown);

        Assert.StartsWith("│▌ :blobcat:", ListRows(drawn)[1], StringComparison.Ordinal);

        drawn.Press(Key.CursorUp);

        Assert.StartsWith("│▌ :blobfox:", ListRows(drawn)[2], StringComparison.Ordinal);

        var at = List(drawn).FrameToScreen();

        drawn.Wheel(at.X + 4, at.Y + 1);

        Assert.StartsWith("│▌ :blobfox:", ListRows(drawn)[2], StringComparison.Ordinal);

        drawn.Wheel(at.X + 4, at.Y + 1, down: false);

        Assert.StartsWith("│▌ :blobcat:", ListRows(drawn)[1], StringComparison.Ordinal);
        Assert.Equal(":bl", Editor(drawn).Text);
    }

    /// <summary>
    ///     <c>esc</c> closes the list and leaves the draft, and the list stays shut for the rest of that word, so a
    ///     <c>:word:</c> it keeps offering to replace can be written; a new word opens it again.
    /// </summary>
    [Fact]
    public async Task EscClosesTheListForTheRestOfTheWord()
    {
        using var drawn = await Composing("blobcat", "bloblul");

        Type(drawn, ":bl");
        drawn.Press(Key.Esc);

        Assert.IsType<ComposeScreen>(drawn.Shell.Screen);
        Assert.Equal(":bl", Editor(drawn).Text);
        Assert.False(List(drawn).Visible);

        Type(drawn, "ob");
        drawn.Press(Key.CursorLeft);
        drawn.Press(Key.End);

        Assert.False(List(drawn).Visible);

        Type(drawn, ": :bl");

        Assert.True(List(drawn).Visible);
    }

    /// <summary>A click outside the open list closes it and does nothing else: the next letter carries on the word.</summary>
    [Fact]
    public async Task AClickOutsideTheListClosesIt()
    {
        using var drawn = await Composing("blobcat");

        Type(drawn, "hi :bl");

        var editor = Editor(drawn).FrameToScreen();

        drawn.Click(editor.X + 1, editor.Bottom - 1);

        Assert.False(List(drawn).Visible);

        Type(drawn, "o");

        Assert.Equal("hi :blo", Editor(drawn).Text);
    }

    /// <summary>
    ///     A row is the shortcode in the custom emoji role, with the letters typed picked out as a mention row picks
    ///     them out.
    /// </summary>
    [Fact]
    public async Task ARowPicksOutTheLettersTyped()
    {
        using var drawn = await Composing("ablobcat");

        Type(drawn, ":cat");

        var row = List(drawn).FrameToScreen().Y + 1;
        var shortcode = drawn.Rows()[row].IndexOf(":ablobcat:", StringComparison.Ordinal);

        Assert.Equal(Themes.Dark.For(Role.CustomEmoji), drawn.Cell(row, shortcode));
        Assert.Equal(Themes.Dark.For(Role.CustomEmoji), drawn.Cell(row, shortcode + 5));
        Assert.Equal(Themes.Dark.For(Role.Mention), drawn.Cell(row, shortcode + 6));
        Assert.Equal(Themes.Dark.For(Role.Mention), drawn.Cell(row, shortcode + 8));
        Assert.Equal(Themes.Dark.For(Role.CustomEmoji), drawn.Cell(row, shortcode + 9));
    }

    /// <summary>
    ///     The instance's emoji are read on the first <c>:</c> that starts a word, bare, before any of a shortcode's
    ///     letters follow it, as the profile acted as — not on composing, nor on a <c>:</c> inside or ending a word —
    ///     and then not again.
    /// </summary>
    [Fact]
    public async Task TheEmojiAreReadOnceOnTheFirstColonThatStartsAWord()
    {
        using var drawn = await Composing("blobcat", "thinking");

        Assert.Empty(drawn.Built.Emoji.Reads);

        Type(drawn, "at 10:30 re:bl note: ");

        Assert.Empty(drawn.Built.Emoji.Reads);

        Type(drawn, ":");

        Assert.Equal("personal", Assert.Single(drawn.Built.Emoji.Reads).Name);

        Type(drawn, "bl");
        drawn.Press(Key.Tab);
        Type(drawn, ":th");
        drawn.Press(Key.Esc);
        Type(drawn, " :zz :)");

        Assert.Single(drawn.Built.Emoji.Reads);
    }

    /// <summary>Near the editor's foot, where there is no room under the line, the list opens over it.</summary>
    [Fact]
    public async Task NearTheFootTheListOpensAbove()
    {
        using var drawn = await Composing("blobcat");
        var editor = Editor(drawn).FrameToScreen();

        for (var line = 0; line < editor.Height - 2; line++)
        {
            drawn.Press(Key.Enter);
        }

        Type(drawn, ":bl");

        Assert.Equal(editor.Y + editor.Height - 2, List(drawn).FrameToScreen().Bottom);
    }

    private static Task<ComposedView> Composing(params string[] shortcodes) =>
        ComposedView.Of(new AShell { Emoji = FakeInstanceEmoji.Offering(shortcodes) });

    private static void Type(ComposedView drawn, string text) => drawn.Type(text);

    private static ComposeEditor Editor(ComposedView drawn) => drawn.Editor;

    private static PaintedView List(ComposedView drawn) => drawn.Hanging;

    /// <summary>The shortcode each of the list's rows names, top to bottom.</summary>
    private static IReadOnlyList<string> Shortcodes(ComposedView drawn) =>
    [
        .. ListRows(drawn).Skip(1).SkipLast(1).Select(row => row.Trim(['│', '▌', ' '])),
    ];

    /// <summary>The list's rows as drawn on the terminal.</summary>
    private static IReadOnlyList<string> ListRows(ComposedView drawn) => drawn.RowsOf(drawn.Hanging);
}
