using Terminal.Gui.Input;
using Wooly.Core.Errors;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
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
    ///     Typing on in a <c>:</c>-word while its read is in flight asks nothing more and opens nothing; the answer
    ///     landing opens nothing by itself, even with the shell changing under the word; the next keystroke in the word
    ///     opens the list.
    /// </summary>
    [Fact]
    public async Task WhileTheReadIsInFlightNothingMoreIsAsked_AndTheNextKeystrokeAfterItLandsOpensTheList()
    {
        var emoji = FakeInstanceEmoji.Offering("blobcat", "blobfox");
        emoji.Holding = true;

        using var drawn = await ComposedView.Of(new AShell { Emoji = emoji });

        drawn.Type(":bl");

        Assert.Single(emoji.Reads);
        Assert.False(drawn.Hanging.Visible);

        drawn.Type("o");
        drawn.Settle();

        Assert.Single(emoji.Reads);
        Assert.False(drawn.Hanging.Visible);

        Assert.Single(emoji.Held).Land();
        drawn.Settle();
        drawn.Shell.StatusRow.SayForAMoment("Something else.");
        drawn.Settle();

        Assert.False(drawn.Hanging.Visible);

        drawn.Type("b");

        Assert.True(drawn.Hanging.Visible);
        Assert.Equal([":blobcat:", ":blobfox:"], Shortcodes(drawn));
        Assert.Single(emoji.Reads);
    }

    /// <summary>
    ///     After a read that answered nothing, more typing in the same word asks nothing — nor does taking it back below
    ///     two letters and typing them again, or the caret going back inside it and out to its end, since the caret never
    ///     left it; a new <c>:</c>-word asks once more, and that one answering opens the list at the next keystroke.
    /// </summary>
    [Fact]
    public async Task AFailedReadIsAskedAgainOnlyOnANewWord_AndASuccessThenOpensTheList()
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

        drawn.Type(" :bl");

        Assert.Equal(2, emoji.Reads.Count);

        emoji.Held[1].Land();
        drawn.Settle();
        drawn.Type("o");

        Assert.Equal([":blobcat:"], Shortcodes(drawn));
        Assert.Equal(2, emoji.Reads.Count);
    }

    /// <summary>
    ///     A read in flight says nothing on the status row, nor one that answered nothing, nor one whose token was
    ///     refused — that is said by the reads that fill the screen.
    /// </summary>
    [Theory]
    [InlineData("nothing")]
    [InlineData("refused")]
    public async Task NothingReachesTheStatusRow_InFlightOrFailed(string ending)
    {
        var emoji = ending == "refused"
            ? FakeInstanceEmoji.Refusing(new AuthenticationException("The token was refused."))
            : FakeInstanceEmoji.Unanswering();
        emoji.Holding = true;

        using var drawn = await ComposedView.Of(new AShell { Emoji = emoji });
        var before = drawn.Status();

        drawn.Type(":bl");
        drawn.Settle();

        Assert.Null(drawn.Shell.StatusRow.Notice);
        Assert.Equal(before, drawn.Status());

        Assert.Single(emoji.Held).Land();
        drawn.Settle();
        drawn.Type("o");

        Assert.Null(drawn.Shell.StatusRow.Notice);
        Assert.Equal(before, drawn.Status());
        Assert.IsType<ComposeScreen>(drawn.Shell.Screen);
        Assert.False(drawn.Hanging.Visible);
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
