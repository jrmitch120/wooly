using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The warning a compose screen carries: pre-filled from the post being answered on a reply (#123) and from the
///     post being changed on an edit (#140), empty on a post answering nothing (#139), and in every case the author's
///     to keep, edit or clear before it goes out. A reply to a warned post is usually about the warned thing, and an
///     author who has to remember to re-type the warning is one who sometimes will not.
/// </summary>
/// <remarks>
///     All of it at the compose seam — the screen and the shell over the same ports the CLI uses (ADR-0005) — since
///     what is pre-filled, what is editable and what is sent are decisions rather than drawing. Where the row sits and
///     which of the two fields the typing goes into is <see cref="ShellComposeLayoutTests" />'.
/// </remarks>
public class WarnedReplyTests
{
    private static readonly Post Warned = APost.With(
        id: "220",
        account: "ben@hachyderm.io",
        contentWarning: "spoilers");

    private static readonly Post Plain = APost.With(id: "220", account: "ben@hachyderm.io");

    /// <summary>One of the profile's own, which is the only kind <c>e</c> opens.</summary>
    private static readonly Post Mine = APost.With(
        id: "110",
        account: "jeff@mastodon.social",
        contentWarning: "spoilers");

    private static readonly Post MinePlain = APost.With(id: "110", account: "jeff@mastodon.social");

    /// <summary>The first acceptance criterion: the warning being replied to is what the field opens holding.</summary>
    [Fact]
    public async Task Reply_OpensOnTheWarningOfThePostItAnswers()
    {
        var compose = await Replying(Warned);

        Assert.Equal("spoilers", compose.Warning);
        Assert.Equal("spoilers", compose.ContentWarning);
    }

    /// <summary>And a post carrying none opens on an empty field, exactly as it did before any of this.</summary>
    [Fact]
    public async Task Reply_OpensOnNoWarningWhereThePostCarriesNone()
    {
        var compose = await Replying(Plain);

        Assert.Equal(string.Empty, compose.Warning);
        Assert.Null(compose.ContentWarning);
    }

    /// <summary>
    ///     A boost is answered with the warning of the post inside it — the same post the reply targets, and the same
    ///     resolution every other key on a boost already makes.
    /// </summary>
    [Fact]
    public async Task Reply_OpensOnTheWarningOfThePostInsideABoost()
    {
        var boost = APost.With(id: "330", account: "maria@example.social", content: string.Empty, boosted: Warned);

        var compose = await Replying(boost);

        Assert.Equal("220", compose.About?.Id);
        Assert.Equal("spoilers", compose.Warning);
    }

    /// <summary>
    ///     The instance's sensitive flag is a mark over somebody else's attachments, and a fresh compose has none — so
    ///     a reply to a flagged post carrying no warning of its own opens on nothing and goes out unflagged.
    /// </summary>
    [Fact]
    public async Task Reply_LeavesTheSensitiveFlagBehind()
    {
        var flagged = APost.With(
            id: "220",
            account: "ben@hachyderm.io",
            sensitive: true,
            media: [APost.APicture()]);

        var shell = new AShell { Timelines = FakeTimelineReader.Holding(flagged) };
        var opened = await shell.Opened();

        opened.Reply();

        var compose = Assert.IsType<ComposeScreen>(opened.Screen);
        Assert.Equal(string.Empty, compose.Warning);

        compose.Text += "Answering you";

        await opened.Send();
        shell.Host.Drain();

        Assert.Null(Assert.Single(shell.Author.Published).Draft.ContentWarning);
    }

    /// <summary>
    ///     A post answering nothing carries the field too, empty: there is nothing here for it to have been filled
    ///     from, and the field is the way the TUI warns a post at all (#139). Left alone it sends no warning, which is
    ///     what a fresh post has always done.
    /// </summary>
    [Fact]
    public async Task Compose_OpensOnAnEmptyWarningField()
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(Warned) };
        var opened = await shell.Opened();

        opened.Compose();

        var compose = Assert.IsType<ComposeScreen>(opened.Screen);

        Assert.Equal(string.Empty, compose.Warning);
        Assert.Contains(compose.Lines(61, AShell.Now), line => line.Text == "⚠ no content warning");

        compose.Text = "Saying something of my own";

        await opened.Send();
        shell.Host.Drain();

        Assert.Null(Assert.Single(shell.Author.Published).Draft.ContentWarning);
    }

    /// <summary>
    ///     And a warning written into it goes out on the post, which is the whole of what #139 adds: before it, the
    ///     TUI was the one surface of this client that could not warn a post it was composing.
    /// </summary>
    [Fact]
    public async Task Compose_SendsAWarningWrittenIntoTheField()
    {
        var shell = new AShell();
        var opened = await shell.Opened();

        opened.Compose();
        opened.WriteWarning();

        foreach (var letter in "spoilers")
        {
            opened.Type(letter);
        }

        var compose = Assert.IsType<ComposeScreen>(opened.Screen);
        compose.Text = "Saying something of my own";

        await opened.Send();
        shell.Host.Drain();

        var published = Assert.Single(shell.Author.Published);

        Assert.Equal("spoilers", published.Draft.ContentWarning);
        Assert.Null(published.Draft.InReplyTo);
    }

    /// <summary>
    ///     What is sent is whatever the field holds at that moment — pre-filled, not imposed. The reply is the
    ///     author's post and its warning is theirs to change.
    /// </summary>
    [Fact]
    public async Task Send_CarriesWhateverTheFieldHolds()
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(Warned) };
        var opened = await shell.Opened();

        opened.Reply();

        var compose = Assert.IsType<ComposeScreen>(opened.Screen);
        compose.Text += "Answering you";
        compose.Warning = "spoilers, and one of my own";

        await opened.Send();
        shell.Host.Drain();

        var published = Assert.Single(shell.Author.Published);
        Assert.Equal("spoilers, and one of my own", published.Draft.ContentWarning);
        Assert.Equal("220", published.Draft.InReplyTo);
    }

    /// <summary>
    ///     Cleared, the reply goes out behind nothing. An instance reads an empty warning as no warning at all, so
    ///     what a cleared field sends is nothing rather than a post hidden behind a blank.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Send_SendsNoWarningWhereTheFieldWasCleared(string cleared)
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(Warned) };
        var opened = await shell.Opened();

        opened.Reply();

        var compose = Assert.IsType<ComposeScreen>(opened.Screen);
        compose.Text += "Answering you";
        compose.Warning = cleared;

        await opened.Send();
        shell.Host.Drain();

        Assert.Null(Assert.Single(shell.Author.Published).Draft.ContentWarning);
    }

    /// <summary>
    ///     Spaces inside a warning the author did write are theirs and go out as typed. Whitespace decides between a
    ///     warning and none; it is not tidied out of one there is, since that would be editing their words on the way
    ///     past.
    /// </summary>
    [Fact]
    public async Task Send_CarriesTheFieldLetterForLetter()
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(Warned) };
        var opened = await shell.Opened();

        opened.Reply();

        var compose = Assert.IsType<ComposeScreen>(opened.Screen);
        compose.Text += "Answering you";
        compose.Warning = " spoilers, of a sort ";

        await opened.Send();
        shell.Host.Drain();

        Assert.Equal(" spoilers, of a sort ", Assert.Single(shell.Author.Published).Draft.ContentWarning);
    }

    /// <summary>
    ///     An edit opens on the warning the post is already behind, which is what lets one field say all three of the
    ///     things <see cref="PostEdit.ContentWarning" /> distinguishes (#140): a field opening <em>empty</em> could not
    ///     tell "leave it alone" from "take it away", but one the author is looking at can, because clearing it is
    ///     emptying something that had text in it.
    /// </summary>
    [Fact]
    public async Task Edit_OpensOnTheWarningThePostIsAlreadyBehind()
    {
        var compose = await Editing(Mine);

        Assert.Equal("spoilers", compose.Warning);
    }

    /// <summary>And a post carrying none opens on an empty field, ready to be given one.</summary>
    [Fact]
    public async Task Edit_OpensOnAnEmptyFieldWhereThePostCarriesNoWarning()
    {
        var compose = await Editing(MinePlain);

        Assert.Equal(string.Empty, compose.Warning);
        Assert.Contains(compose.Lines(61, AShell.Now), line => line.Text == "⚠ no content warning");
    }

    /// <summary>
    ///     Untouched, the field sends the same warning back and the post keeps it. The edit says something about the
    ///     warning either way now — what it says is "this one", which is the one it already had.
    /// </summary>
    [Fact]
    public async Task Edit_LeavesTheWarningAsItWasWhereTheFieldWasNotTouched()
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(Mine) };
        var opened = await shell.Opened();

        opened.Edit();

        var compose = Assert.IsType<ComposeScreen>(opened.Screen);
        compose.Text = "Hello world, fixed";

        await opened.Send();
        shell.Host.Drain();

        var saved = Assert.Single(shell.Author.Edits).Edit;

        Assert.True(saved.ChangesContentWarning);
        Assert.Equal("spoilers", saved.ContentWarningWanted);
    }

    /// <summary>
    ///     Cleared, the warning comes off the post — unambiguously, because the author emptied a field that had text
    ///     in it. Spaces amount to none the same way they do on a fresh post.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Edit_TakesTheWarningOffWhereTheFieldWasCleared(string cleared)
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(Mine) };
        var opened = await shell.Opened();

        opened.Edit();

        var compose = Assert.IsType<ComposeScreen>(opened.Screen);
        compose.Warning = cleared;

        await opened.Send();
        shell.Host.Drain();

        var saved = Assert.Single(shell.Author.Edits).Edit;

        Assert.True(saved.ChangesContentWarning);
        Assert.Equal(string.Empty, saved.ContentWarningWanted);
    }

    /// <summary>And a warning typed into a post that had none puts it behind that warning.</summary>
    [Fact]
    public async Task Edit_PutsAPostThatHadNoWarningBehindOneTypedIn()
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(MinePlain) };
        var opened = await shell.Opened();

        opened.Edit();
        opened.WriteWarning();

        foreach (var letter in "spoilers")
        {
            opened.Type(letter);
        }

        await opened.Send();
        shell.Host.Drain();

        Assert.Equal("spoilers", Assert.Single(shell.Author.Edits).Edit.ContentWarningWanted);
    }

    /// <summary>
    ///     Letter for letter here too: the spaces inside a warning an author wrote are theirs, and only a field with
    ///     nothing but spaces in it amounts to no warning at all.
    /// </summary>
    [Fact]
    public async Task Edit_SavesTheFieldLetterForLetter()
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(Mine) };
        var opened = await shell.Opened();

        opened.Edit();

        var compose = Assert.IsType<ComposeScreen>(opened.Screen);
        compose.Warning = " spoilers, of a sort ";

        await opened.Send();
        shell.Host.Drain();

        Assert.Equal(" spoilers, of a sort ", Assert.Single(shell.Author.Edits).Edit.ContentWarningWanted);
    }

    /// <summary>
    ///     The row above the editor says what the post is going behind, in the same mark and the same role a warned
    ///     post's own warning is drawn in — so a warning cannot come to look like two different things.
    /// </summary>
    [Fact]
    public async Task Reply_DrawsTheWarningItOpenedOn()
    {
        var compose = await Replying(Warned);
        var lines = compose.Lines(61, AShell.Now);

        var warning = Assert.Single(lines, line => line.Has(Role.ContentWarning));
        Assert.Equal("⚠ spoilers", warning.Text);
    }

    /// <summary>
    ///     An empty field still says it is there, muted: a row a reader can type into is one they have to be able to
    ///     find, and the status row's <c>ctrl-w</c> is the other half of saying so.
    /// </summary>
    [Fact]
    public async Task Reply_SaysThereIsNoWarningWhereTheFieldIsEmpty()
    {
        var compose = await Replying(Plain);
        var lines = compose.Lines(61, AShell.Now);

        Assert.DoesNotContain(lines, line => line.Has(Role.ContentWarning));
        Assert.Contains(lines, line => line.Text == "⚠ no content warning");
    }

    /// <summary>
    ///     Typing goes into the warning while <c>ctrl-w</c> has it, and back into the post afterwards — with the caret
    ///     drawn where the next letter lands, the way the search prompt's is, so a terminal with no colours still says
    ///     where the typing is going.
    /// </summary>
    [Fact]
    public async Task WriteWarning_PutsWhatIsTypedIntoTheWarningUntilItIsPressedAgain()
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(Plain) };
        var opened = await shell.Opened();

        opened.Reply();

        var compose = Assert.IsType<ComposeScreen>(opened.Screen);
        Assert.False(compose.WritingTheWarning);
        Assert.False(compose.IsTyping);

        opened.WriteWarning();

        Assert.True(compose.WritingTheWarning);
        Assert.True(compose.IsTyping);

        foreach (var letter in "cw!")
        {
            opened.Type(letter);
        }

        opened.Backspace();

        Assert.Equal("cw", compose.Warning);
        Assert.Contains(compose.Lines(61, AShell.Now), line => line.Text == "⚠ cw▌");

        opened.WriteWarning();

        Assert.False(compose.WritingTheWarning);
        Assert.False(compose.IsTyping);
        Assert.DoesNotContain(compose.Lines(61, AShell.Now), line => line.Text.Contains('▌'));
    }

    /// <summary>Backspacing an empty field is nothing at all, rather than the letters of the post behind it.</summary>
    [Fact]
    public async Task Backspace_TakesNothingOffAnEmptyWarning()
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(Plain) };
        var opened = await shell.Opened();

        opened.Reply();
        opened.WriteWarning();
        opened.Backspace();

        var compose = Assert.IsType<ComposeScreen>(opened.Screen);

        Assert.Equal(string.Empty, compose.Warning);
        Assert.Equal("@ben@hachyderm.io ", compose.Text);
    }

    /// <summary>
    ///     The status row names the key wherever there is a field to reach, and says which way it goes — and while the
    ///     field is taking letters it stops offering <c>?</c>, which is going into the warning like every other
    ///     printable key (the rule the search prompt already keeps).
    /// </summary>
    [Fact]
    public async Task Keys_NameTheWarningKeyAndWhatItDoesNext()
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(Warned) };
        var opened = await shell.Opened();

        opened.Reply();

        var compose = Assert.IsType<ComposeScreen>(opened.Screen);

        Assert.Contains(compose.Keys, key => key is { Key: "ctrl-w", Does: "content warning" });
        Assert.Contains(compose.Keys, key => key.Key == "?");

        opened.WriteWarning();

        Assert.Contains(compose.Keys, key => key is { Key: "ctrl-w", Does: "back to the post" });
        Assert.DoesNotContain(compose.Keys, key => key.Key == "?");
    }

    /// <summary>An edit reaches its field with the same key, since it now has one to reach (#140).</summary>
    [Fact]
    public async Task Keys_NameTheWarningKeyOnAnEditToo()
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(Mine) };
        var opened = await shell.Opened();

        opened.Edit();
        opened.WriteWarning();

        var compose = Assert.IsType<ComposeScreen>(opened.Screen);

        Assert.True(compose.WritingTheWarning);
        Assert.Contains(compose.Keys, key => key is { Key: "ctrl-w", Does: "back to the post" });
    }

    /// <summary>A reply to <paramref name="post" />, opened the way <c>r</c> opens one.</summary>
    private static async Task<ComposeScreen> Replying(Post post)
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(post) };
        var opened = await shell.Opened();

        opened.Reply();

        return Assert.IsType<ComposeScreen>(opened.Screen);
    }

    /// <summary>A change to <paramref name="post" />, opened the way <c>e</c> opens one.</summary>
    private static async Task<ComposeScreen> Editing(Post post)
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(post) };
        var opened = await shell.Opened();

        opened.Edit();

        return Assert.IsType<ComposeScreen>(opened.Screen);
    }
}
