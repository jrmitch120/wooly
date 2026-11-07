using Terminal.Gui.Input;
using Wooly.Core.Errors;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;

namespace Wooly.Tests.Tui;

/// <summary>
///     Reading the instance's custom emoji for the <c>:</c> list when the read is slow, fails, or is shared between
///     profiles (#528, #579): the list never nags, never hammers an instance, and never costs a request it does not need.
///     Keys go into compose over a shell, what is drawn is read back off the terminal, and the reads are counted on the
///     fake.
/// </summary>
public class CustomEmojiReadTests
{
    /// <summary>
    ///     While the read is in flight, a <c>:</c>-word that would open the list opens it on one dim row saying so, hung
    ///     and lined up as the list is — under the word, left on its <c>:</c>, the row where a shortcode would be — with
    ///     its usual foot, and nothing on the status row. One letter after the <c>:</c> opens nothing yet.
    /// </summary>
    [Fact]
    public async Task WhileTheReadIsInFlightTheWordOpensTheListOnALoadingRow()
    {
        var emoji = FakeInstanceEmoji.Offering("blobcat");
        emoji.Holding = true;

        using var drawn = await ComposedView.Of(new AShell { Emoji = emoji });
        var before = drawn.Status();

        drawn.Type("hi :b");

        Assert.False(drawn.Hanging.Visible);

        drawn.Type("l");

        var list = drawn.Hanging.FrameToScreen();
        var editor = drawn.Editor.FrameToScreen();
        var rows = drawn.RowsOf(drawn.Hanging);

        Assert.True(drawn.Hanging.Visible);
        Assert.Equal(editor.Y + 1, list.Y);
        Assert.Equal(editor.X + 3, list.X);
        Assert.Equal(3, rows.Count);
        Assert.Matches("^│  reading custom emoji… +│$", rows[1]);
        Assert.Contains("↑↓ pick  tab insert  esc close", rows[^1], StringComparison.Ordinal);
        Assert.Equal(Themes.Dark.For(Role.Loading), drawn.Cell(list.Y + 1, list.X + 3));
        Assert.Null(drawn.Shell.StatusRow.Notice);
        Assert.Equal(before, drawn.Status());
    }

    /// <summary>
    ///     The loading row is nothing to pick: <c>tab</c>, <c>enter</c>, a click and the wheel on it insert nothing and
    ///     leave it open, while every other key goes on to the editor — <c>↑</c> moving the caret up a line, off the
    ///     word, which closes it.
    /// </summary>
    [Fact]
    public async Task TheLoadingRowPicksNothing_AndEveryOtherKeyGoesOnToTheEditor()
    {
        var emoji = FakeInstanceEmoji.Offering("blobcat");
        emoji.Holding = true;

        using var drawn = await ComposedView.Of(new AShell { Emoji = emoji });

        drawn.Type("first");
        drawn.Press(Key.Enter);
        drawn.Type("hi :bl");

        var text = drawn.Editor.Text;
        var at = drawn.Hanging.FrameToScreen();

        drawn.Press(Key.Tab);
        drawn.Press(Key.Enter);
        drawn.Click(at.X + 4, at.Y + 1);
        drawn.Wheel(at.X + 4, at.Y + 1);

        Assert.Equal(text, drawn.Editor.Text);
        Assert.Equal(["reading custom emoji…"], Shortcodes(drawn));

        drawn.Press(Key.CursorUp);

        Assert.False(drawn.Hanging.Visible);

        drawn.Type("X");

        Assert.StartsWith("firstX", drawn.Editor.Text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Near the editor's foot the loading row hangs over the line, as the list does; the answer landing grows the
    ///     list upward from its bottom edge, which stays put on the row over the word.
    /// </summary>
    [Fact]
    public async Task NearTheFootTheLoadingRowHangsOverTheLine_AndTheListGrowsUpwardFromIt()
    {
        var emoji = FakeInstanceEmoji.Offering("blobcat", "blobfox", "blobowl");
        emoji.Holding = true;

        using var drawn = await ComposedView.Of(new AShell { Emoji = emoji });
        var editor = drawn.Editor.FrameToScreen();

        for (var line = 0; line < editor.Height - 2; line++)
        {
            drawn.Press(Key.Enter);
        }

        drawn.Type(":bl");

        var loading = drawn.Hanging.FrameToScreen();

        Assert.Equal(editor.Y + editor.Height - 2, loading.Bottom);
        Assert.Equal(3, loading.Height);

        Assert.Single(emoji.Held).Land();
        drawn.Settle();

        var filled = drawn.Hanging.FrameToScreen();

        Assert.Equal([":blobcat:", ":blobfox:", ":blobowl:"], Shortcodes(drawn));
        Assert.Equal(loading.Bottom, filled.Bottom);
        Assert.Equal(loading.X, filled.X);
        Assert.Equal(5, filled.Height);
    }

    /// <summary>
    ///     Where there is room under the line for the loading row but not for the most the list offers, the loading row
    ///     already hangs over the line, sided by the tallest the list can become rather than by its own one row — so the
    ///     answer landing grows the list upward from the bottom edge it hung on, never over to the other side of the word.
    /// </summary>
    [Fact]
    public async Task WhereThereIsRoomUnderTheLineForTheLoadingRowButNotTheListItHangsOverTheLine_AndTheListStaysThere()
    {
        var emoji = FakeInstanceEmoji.Offering("blobcat", "blobfox", "blobowl", "blobsad", "blobwave");
        emoji.Holding = true;

        using var drawn = await ComposedView.Of(new AShell { Emoji = emoji });
        var editor = drawn.Editor.FrameToScreen();

        // Four rows under the line: room for the loading row and its edges, not for five shortcodes and theirs.
        for (var line = 0; line < editor.Height - 5; line++)
        {
            drawn.Press(Key.Enter);
        }

        drawn.Type(":bl");

        var caret = editor.Y + editor.Height - 5;
        var loading = drawn.Hanging.FrameToScreen();

        Assert.Equal(["reading custom emoji…"], Shortcodes(drawn));
        Assert.Equal(caret, loading.Bottom);
        Assert.Equal(3, loading.Height);

        Assert.Single(emoji.Held).Land();
        drawn.Settle();

        var filled = drawn.Hanging.FrameToScreen();

        Assert.Equal([":blobcat:", ":blobfox:", ":blobowl:", ":blobsad:", ":blobwave:"], Shortcodes(drawn));
        Assert.Equal(caret, filled.Bottom);
        Assert.Equal(loading.X, filled.X);
        Assert.Equal(7, filled.Height);
    }

    /// <summary>
    ///     Near the editor's right edge the loading row is pushed left of the <c>:</c> as the list is, to keep it in the
    ///     editor; the answer landing never moves it sideways, whether what lands is narrower than the loading row's list
    ///     or wider — a shortcode too long for the room to the right is cut at the list's edge rather than the list
    ///     moving off the word.
    /// </summary>
    [Theory]
    [InlineData("blobcat")]
    [InlineData("blobcat_with_a_shortcode_far_too_long_to_fit")]
    public async Task NearTheRightEdgeTheAnswerLandingNeverMovesTheListSideways(string shortcode)
    {
        var emoji = FakeInstanceEmoji.Offering(shortcode);
        emoji.Holding = true;

        using var drawn = await ComposedView.Of(new AShell { Emoji = emoji });
        var editor = drawn.Editor.FrameToScreen();

        drawn.Type($"{new string('x', editor.Width - 10)} :bl");

        var loading = drawn.Hanging.FrameToScreen();

        Assert.Equal(["reading custom emoji…"], Shortcodes(drawn));
        Assert.Equal(editor.Right, loading.Right);

        Assert.Single(emoji.Held).Land();
        drawn.Settle();

        var filled = drawn.Hanging.FrameToScreen();

        Assert.True(drawn.Hanging.Visible);
        Assert.Equal(loading.X, filled.X);
        Assert.Equal(loading.Y, filled.Y);
        Assert.True(filled.Right <= editor.Right);
    }

    /// <summary>
    ///     Typing on in a <c>:</c>-word while its read is in flight asks nothing more and keeps the loading row, as does
    ///     the shell changing under it; the answer landing fills the list in at once, with no keystroke, picked on the
    ///     first, the list growing down from where the loading row hung.
    /// </summary>
    [Fact]
    public async Task WhileTheReadIsInFlightNothingMoreIsAsked_AndItsAnswerLandingFillsTheListIn()
    {
        var emoji = FakeInstanceEmoji.Offering("blobcat", "blobfox", "thinking");
        emoji.Holding = true;

        using var drawn = await ComposedView.Of(new AShell { Emoji = emoji });

        drawn.Type(":bl");
        drawn.Type("o");
        drawn.Shell.StatusRow.SayForAMoment("Something else.");
        drawn.Settle();

        var loading = drawn.Hanging.FrameToScreen();

        Assert.Single(emoji.Reads);
        Assert.Equal(["reading custom emoji…"], Shortcodes(drawn));

        Assert.Single(emoji.Held).Land();
        drawn.Settle();

        Assert.Equal([":blobcat:", ":blobfox:"], Shortcodes(drawn));
        Assert.StartsWith("│▌ :blobcat:", drawn.RowsOf(drawn.Hanging)[1], StringComparison.Ordinal);
        Assert.Equal(loading.Y, drawn.Hanging.FrameToScreen().Y);
        Assert.Equal(loading.X, drawn.Hanging.FrameToScreen().X);
        Assert.Single(emoji.Reads);
    }

    /// <summary>
    ///     The answer fills in only the list standing open on the word that waited for it: one landing after the caret
    ///     left the word, or moved back inside it, or after <c>esc</c> closed the loading row, opens nothing by itself.
    ///     The next keystroke at the end of a word opens it, as ever — but never on the word <c>esc</c> closed.
    /// </summary>
    [Theory]
    [InlineData("left")]
    [InlineData("inside")]
    [InlineData("esc")]
    public async Task AnAnswerLandingFillsInNothing_WhereTheLoadingRowIsNoLongerOpen(string how)
    {
        var emoji = FakeInstanceEmoji.Offering("blobcat");
        emoji.Holding = true;

        using var drawn = await ComposedView.Of(new AShell { Emoji = emoji });

        drawn.Type(":bl");

        Assert.True(drawn.Hanging.Visible);

        switch (how)
        {
            case "left":
                drawn.Type(" ");
                break;
            case "inside":
                drawn.Press(Key.CursorLeft);
                break;
            default:
                drawn.Press(Key.Esc);
                break;
        }

        Assert.False(drawn.Hanging.Visible);
        Assert.IsType<ComposeScreen>(drawn.Shell.Screen);

        Assert.Single(emoji.Held).Land();
        drawn.Settle();

        Assert.False(drawn.Hanging.Visible);

        if (how == "esc")
        {
            drawn.Type("o");

            Assert.False(drawn.Hanging.Visible);
            Assert.Equal(":blo", drawn.Editor.Text);

            return;
        }

        drawn.Press(how == "left" ? Key.Backspace : Key.End);

        Assert.Equal([":blobcat:"], Shortcodes(drawn));
        Assert.Single(emoji.Reads);
    }

    /// <summary>
    ///     After a read that answered nothing, more typing in the same word asks nothing — nor does taking it back below
    ///     two letters and typing them again, or the caret going back inside it and out to its end, since the caret never
    ///     left it; a new <c>:</c>-word asks once more, on its bare <c>:</c>, and that one answering fills the list in.
    /// </summary>
    [Fact]
    public async Task AFailedReadIsAskedAgainOnlyOnANewWord_AndASuccessThenFillsTheListIn()
    {
        var emoji = FakeInstanceEmoji.Offering("blobcat");
        emoji.Holding = true;

        using var drawn = await ComposedView.Of(new AShell { Emoji = emoji });

        drawn.Type(":bl");
        emoji.Held[0].LandNothing();
        drawn.Settle();
        drawn.Type("ob");

        Assert.Single(emoji.Reads);
        Assert.False(drawn.Hanging.Visible);

        drawn.Press(Key.Backspace);
        drawn.Press(Key.Backspace);
        drawn.Press(Key.Backspace);
        drawn.Type("l");

        Assert.Single(emoji.Reads);

        drawn.Press(Key.CursorLeft);
        drawn.Press(Key.End);

        Assert.Single(emoji.Reads);

        drawn.Type(" :");

        Assert.Equal(2, emoji.Reads.Count);

        drawn.Type("bl");
        emoji.Held[1].Land();
        drawn.Settle();

        Assert.Equal([":blobcat:"], Shortcodes(drawn));
        Assert.Equal(2, emoji.Reads.Count);
    }

    /// <summary>
    ///     A read in flight says nothing on the status row, nor one that answered nothing, nor one whose token was
    ///     refused — that is said by the reads that fill the screen — nor an instance offering none. Each of those
    ///     landing just closes the loading row, and the next keystroke opens nothing.
    /// </summary>
    [Theory]
    [InlineData("nothing")]
    [InlineData("refused")]
    [InlineData("none")]
    public async Task NothingReachesTheStatusRow_AndTheLoadingRowJustCloses_FailedOrOfferingNone(string ending)
    {
        var emoji = ending switch
        {
            "refused" => FakeInstanceEmoji.Refusing(new AuthenticationException("The token was refused.")),
            "none" => FakeInstanceEmoji.Offering(),
            _ => FakeInstanceEmoji.Unanswering(),
        };
        emoji.Holding = true;

        using var drawn = await ComposedView.Of(new AShell { Emoji = emoji });
        var before = drawn.Status();

        drawn.Type(":bl");
        drawn.Settle();

        Assert.True(drawn.Hanging.Visible);
        Assert.Null(drawn.Shell.StatusRow.Notice);
        Assert.Equal(before, drawn.Status());

        Assert.Single(emoji.Held).Land();
        drawn.Settle();

        Assert.False(drawn.Hanging.Visible);

        drawn.Type("o");

        Assert.Null(drawn.Shell.StatusRow.Notice);
        Assert.Equal(before, drawn.Status());
        Assert.IsType<ComposeScreen>(drawn.Shell.Screen);
        Assert.False(drawn.Hanging.Visible);
    }

    /// <summary>
    ///     The read starts on a <c>:</c>-word typed, never on the caret walked alone (#528 story 29): an edit opening on
    ///     <c>:blobcat:</c> and an unfinished <c>:blob</c> reads nothing as it opens, nor as the caret is walked by the
    ///     keys or clicked into either word, or to the end of the one that would open the list. The first letter typed
    ///     there reads, once, standing the list open on its loading row; a bare <c>:</c> typed at a new word's start
    ///     reads too, once more.
    /// </summary>
    [Fact]
    public async Task WalkingOrClickingIntoAColonWordReadsNothing_AndTypingInOneReadsOnce()
    {
        var emoji = FakeInstanceEmoji.Offering("blobcat");
        emoji.Holding = true;

        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "110", account: "jeff@mastodon.social", content: "hi :blobcat: and :blob")),
            Emoji = emoji,
        };

        using var drawn = await ComposedView.Of(built, ComposeFor.Edit);

        Assert.Equal("hi :blobcat: and :blob", drawn.Editor.Text);
        Assert.Empty(emoji.Reads);

        drawn.Press(Key.CursorLeft);
        drawn.Press(Key.Home);

        for (var step = 0; step < "hi :blob".Length; step++)
        {
            drawn.Press(Key.CursorRight);
        }

        var editor = drawn.Editor.FrameToScreen();

        drawn.Click(editor.X + "hi :blo".Length, editor.Y);
        drawn.Click(editor.X + "hi :blobcat: and :bl".Length, editor.Y);
        drawn.Press(Key.End);

        Assert.Empty(emoji.Reads);
        Assert.False(drawn.Hanging.Visible);

        drawn.Type("c");

        Assert.Single(emoji.Reads);
        Assert.Equal(["reading custom emoji…"], Shortcodes(drawn));

        emoji.Held[0].LandNothing();
        drawn.Settle();
        drawn.Type(" :");

        Assert.Equal(2, emoji.Reads.Count);
    }

    /// <summary>An instance offering no emoji never opens the list, and is read only once in the session.</summary>
    [Fact]
    public async Task AnInstanceWithNoEmojiNeverOpensTheList_AndIsReadOnce()
    {
        var emoji = FakeInstanceEmoji.Offering();

        using var drawn = await ComposedView.Of(new AShell { Emoji = emoji });

        drawn.Type(":bl");

        Assert.False(drawn.Hanging.Visible);

        drawn.Type("ob :th");
        drawn.Press(Key.Enter);
        drawn.Type(":cat");
        drawn.Settle();

        Assert.False(drawn.Hanging.Visible);
        Assert.Single(emoji.Reads);
    }

    /// <summary>
    ///     The emoji are the instance's: two profiles on one instance share one read, and switching away and back reads
    ///     nothing again; a profile on another instance is read on its own first <c>:</c>-word, once.
    /// </summary>
    [Fact]
    public async Task TwoProfilesOnOneInstanceShareOneRead_AndAnotherInstanceReadsItsOwn()
    {
        var emoji = FakeInstanceEmoji.Each(new Dictionary<string, string[]>
        {
            ["mastodon.social"] = ["blobcat"],
            ["hachyderm.io"] = ["blahaj"],
        });

        var built = new AShell
        {
            Profiles = FakeProfileRegistry.Holding(
                "personal",
                FakeProfileRegistry.Profile("personal", "mastodon.social", "jeff@mastodon.social"),
                FakeProfileRegistry.Profile("art", "mastodon.social", "jeffdraws@mastodon.social"),
                FakeProfileRegistry.Profile("work", "hachyderm.io", "jeff@hachyderm.io")),
            Emoji = emoji,
        };

        using var drawn = await ComposedView.Of(built, opening: null);

        Assert.Equal([":blobcat:"], ShortcodesComposing(drawn, ":bl"));

        SwitchTo(drawn, "art");

        Assert.Equal([":blobcat:"], ShortcodesComposing(drawn, ":bl"));

        SwitchTo(drawn, "personal");

        Assert.Equal([":blobcat:"], ShortcodesComposing(drawn, ":bl"));
        Assert.Equal(["personal"], emoji.Reads.Select(profile => profile.Name));

        SwitchTo(drawn, "work");

        Assert.Equal([":blahaj:"], ShortcodesComposing(drawn, ":bl"));

        SwitchTo(drawn, "art");

        Assert.Equal([":blobcat:"], ShortcodesComposing(drawn, ":bl"));

        SwitchTo(drawn, "work");

        Assert.Equal([":blahaj:"], ShortcodesComposing(drawn, ":bl"));
        Assert.Equal(["personal", "work"], emoji.Reads.Select(profile => profile.Name));
    }

    /// <summary>A post opened, <paramref name="typed" /> into it, and the shortcodes the list offers for it.</summary>
    private static IReadOnlyList<string> ShortcodesComposing(ComposedView drawn, string typed)
    {
        ComposeRows.Open(drawn.Shell, ComposeFor.Post);
        drawn.Redraw();
        drawn.Type(typed);
        drawn.Settle();

        return Shortcodes(drawn);
    }

    /// <summary>Leaves the post, and acts as <paramref name="name" /> from here on.</summary>
    private static void SwitchTo(ComposedView drawn, string name)
    {
        while (drawn.Shell.Screen is ComposeScreen)
        {
            drawn.Shell.Back();

            if (drawn.Shell.StatusRow.Asking is not null)
            {
                drawn.Shell.StatusRow.Answered(ShellKey.Y);
            }
        }

        drawn.Shell.Press(ShellKey.CtrlP);
        Assert.IsType<ProfilesScreen>(drawn.Shell.Screen).Pick(name);
        drawn.Shell.Press(ShellKey.Enter);
        drawn.Settle();
    }

    /// <summary>The shortcode each of the list's rows names, top to bottom; none while it is closed.</summary>
    private static IReadOnlyList<string> Shortcodes(ComposedView drawn) =>
        drawn.Hanging.Visible
            ? [.. drawn.RowsOf(drawn.Hanging).Skip(1).SkipLast(1).Select(row => row.Trim(['│', '▌', ' ']))]
            : [];
}
