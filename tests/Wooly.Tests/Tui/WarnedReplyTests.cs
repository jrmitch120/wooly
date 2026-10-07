using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The warning a compose screen carries: pre-filled from the post being answered on a reply (#123) and from the
///     post being changed on an edit (#140), empty on a post answering nothing (#139), and in every case the author's
///     to keep, edit or clear before it goes out. A reply to a warned post is usually about the warned thing, and an
///     author who has to remember to re-type the warning is one who sometimes will not.
/// </summary>
/// <remarks>
///     Asked of the screen itself, because the screen is what answers what goes out (<see cref="Outgoing" />, #146):
///     the shell opens the compose, the field is typed into, and the draft-or-edit is read off it directly rather than
///     through a fake author standing at the port to catch it. That the shell puts that value to the port, pops and
///     says so is <see cref="ShellActionTests" />'; where the row sits and which of the two fields the typing goes into
///     is <see cref="ShellComposeLayoutTests" />'.
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
        Assert.Equal("spoilers", Publishing(compose).ContentWarning);
    }

    /// <summary>And a post carrying none opens on an empty field, exactly as it did before any of this.</summary>
    [Fact]
    public async Task Reply_OpensOnNoWarningWhereThePostCarriesNone()
    {
        var compose = await Replying(Plain);

        Assert.Equal(string.Empty, compose.Warning);
        Assert.Null(Publishing(compose).ContentWarning);
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
        Assert.Equal("220", Publishing(compose).InReplyTo);
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

        Assert.Null(Publishing(compose).ContentWarning);
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
        Assert.Contains(compose.Lines(new Drawing(61, AShell.Now)), line => line.Text == ComposeRows.NoWarning);

        compose.Text = "Saying something of my own";

        Assert.Null(Publishing(compose).ContentWarning);
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
        Writing(opened, "spoilers");

        var compose = Assert.IsType<ComposeScreen>(opened.Screen);
        compose.Text = "Saying something of my own";

        var draft = Publishing(compose);

        Assert.Equal("spoilers", draft.ContentWarning);
        Assert.Null(draft.InReplyTo);
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
        Writing(opened, "spoilers, and one of my own");

        var draft = Publishing(compose);

        Assert.Equal("spoilers, and one of my own", draft.ContentWarning);
        Assert.Equal("220", draft.InReplyTo);
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
        Writing(opened, cleared);

        Assert.Null(Publishing(compose).ContentWarning);
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
        Writing(opened, " spoilers, of a sort ");

        Assert.Equal(" spoilers, of a sort ", Publishing(compose).ContentWarning);
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
        Assert.Equal("110", Saving(compose).PostId);
    }

    /// <summary>And a post carrying none opens on an empty field, ready to be given one.</summary>
    [Fact]
    public async Task Edit_OpensOnAnEmptyFieldWhereThePostCarriesNoWarning()
    {
        var compose = await Editing(MinePlain);

        Assert.Equal(string.Empty, compose.Warning);
        Assert.Contains(compose.Lines(new Drawing(61, AShell.Now)), line => line.Text == ComposeRows.NoWarning);
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

        var saved = Saving(compose).Edit;

        Assert.Equal("Hello world, fixed", saved.Text);
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
        Writing(opened, cleared);

        var saved = Saving(Assert.IsType<ComposeScreen>(opened.Screen)).Edit;

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
        Writing(opened, "spoilers");

        var saved = Saving(Assert.IsType<ComposeScreen>(opened.Screen)).Edit;

        Assert.Equal("spoilers", saved.ContentWarningWanted);
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
        Writing(opened, " spoilers, of a sort ");

        var saved = Saving(Assert.IsType<ComposeScreen>(opened.Screen)).Edit;

        Assert.Equal(" spoilers, of a sort ", saved.ContentWarningWanted);
    }

    /// <summary>
    ///     The row above the editor says what the post is going behind, in the same mark and the same role a warned
    ///     post's own warning is drawn in — so a warning cannot come to look like two different things.
    /// </summary>
    [Fact]
    public async Task Reply_DrawsTheWarningItOpenedOn()
    {
        var compose = await Replying(Warned);
        var lines = compose.Lines(new Drawing(61, AShell.Now));

        var warning = Assert.Single(lines, line => line.Has(Role.ContentWarning));
        Assert.Equal(ComposeRows.Warning("spoilers"), warning.Text);
    }

    /// <summary>
    ///     An empty field still says it is there, muted: a row a reader can type into is one they have to be able to
    ///     find, and the status row's <c>ctrl-w</c> is the other half of saying so.
    /// </summary>
    [Fact]
    public async Task Reply_SaysThereIsNoWarningWhereTheFieldIsEmpty()
    {
        var compose = await Replying(Plain);
        var lines = compose.Lines(new Drawing(61, AShell.Now));

        Assert.DoesNotContain(lines, line => line.Has(Role.ContentWarning));
        Assert.Contains(lines, line => line.Text == ComposeRows.NoWarning);
    }

    /// <summary>
    ///     <c>ctrl-w</c> moves the typing to the warning and back, and the screen learns what the field holds as it is
    ///     written (#320) — the row showing it as written, with no caret of the screen's own, the caret being the
    ///     field's.
    /// </summary>
    [Fact]
    public async Task WriteWarning_MovesTheTypingAndTheScreenLearnsTheField()
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(Plain) };
        var opened = await shell.Opened();

        opened.Reply();

        var compose = Assert.IsType<ComposeScreen>(opened.Screen);
        Assert.False(compose.WritingTheWarning);

        opened.EditCompose(compose => compose.WriteTheWarning());

        Assert.True(compose.WritingTheWarning);

        opened.EditCompose(compose => compose.RewriteWarning("cw"));

        Assert.Equal("cw", compose.Warning);
        Assert.Contains(compose.Lines(new Drawing(61, AShell.Now)), line => line.Text == ComposeRows.Warning("cw"));

        opened.EditCompose(compose => compose.WriteTheWarning());

        Assert.False(compose.WritingTheWarning);
        Assert.Equal("cw", compose.Warning);
    }

    /// <summary>
    ///     The shell carries no letters into a compose screen, the warning included: the field is a widget and takes
    ///     its own (#320), so a letter or a backspace the shell is handed reaches neither field.
    /// </summary>
    [Fact]
    public async Task Type_ReachesNeitherField()
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(Warned) };
        var opened = await shell.Opened();

        opened.Reply();
        opened.EditCompose(compose => compose.WriteTheWarning());

        var compose = Assert.IsType<ComposeScreen>(opened.Screen);

        Assert.False(compose.IsTyping);

        opened.Type('x');
        opened.Backspace();

        Assert.False(opened.Paste("pasted"));
        Assert.Equal("spoilers", compose.Warning);
        Assert.Equal("@ben@hachyderm.io ", compose.Text);
    }

    /// <summary>
    ///     The status row names the key wherever there is a field to reach, and says which way it goes — and never
    ///     offers <c>?</c>, which goes into whichever field has the typing as a letter like any other: there is no
    ///     press on a compose screen that opens the keymap, so naming one would be a key that does nothing (#320).
    /// </summary>
    [Fact]
    public async Task Keys_NameTheWarningKeyAndWhatItDoesNext()
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(Warned) };
        var opened = await shell.Opened();

        opened.Reply();

        var compose = Assert.IsType<ComposeScreen>(opened.Screen);

        Assert.Contains(compose.Keys, key => key is { Key: "ctrl-w", Does: "content warning" });
        Assert.DoesNotContain(compose.Keys, key => key.Key == "?");

        opened.EditCompose(compose => compose.WriteTheWarning());

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
        opened.EditCompose(compose => compose.WriteTheWarning());

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

    /// <summary>
    ///     Writes <paramref name="warning" /> into the field the way a reader does — <c>ctrl-w</c>, the field's text
    ///     changed, and the typing handed back to the post.
    /// </summary>
    /// <remarks>
    ///     Through the shell rather than assigned, the field being the screen's own since #146: the field widget hands
    ///     its text to the screen through the shell (#320), which is the path the warning takes on its way to being sent.
    /// </remarks>
    private static void Writing(Shell shell, string warning)
    {
        shell.EditCompose(compose => compose.WriteTheWarning());
        shell.EditCompose(compose => compose.RewriteWarning(warning));
        shell.EditCompose(compose => compose.WriteTheWarning());
    }

    /// <summary>The draft this compose screen answers with, which is what <c>ctrl-s</c> hands the shell to publish.</summary>
    private static PostDraft Publishing(ComposeScreen compose) =>
        Assert.IsType<Outgoing.Publishing>(compose.Outgoing).Draft;

    /// <summary>The change it answers with instead, on the screen <c>e</c> opened, and the post that change is to.</summary>
    private static Outgoing.Saving Saving(ComposeScreen compose) =>
        Assert.IsType<Outgoing.Saving>(compose.Outgoing);
}
