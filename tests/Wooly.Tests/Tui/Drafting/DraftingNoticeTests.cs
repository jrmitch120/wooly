using Wooly.Core.Errors;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     A notice said over a post being written goes once the writer does what it asked: edits the draft (#319). The
///     status row holds a notice or the keymap and never both, and every key compose answers to was hidden behind a
///     refusal that only <c>esc</c> — which throws the draft away — would clear. Asked of drafting's one way in for a
///     change to a draft (<see cref="Wooly.Tui.Drafting.Drafting.Edit{T}" />, ADR-0029, #421): an edit spends the notice
///     by saying nothing, which is itself announced, and anything else but no change at all is announced.
/// </summary>
public class DraftingNoticeTests
{
    private const string TooLong = "Text character limit of 500 exceeded";

    /// <summary>Changing the post takes the refusal off the row, and is announced so that it is drawn again.</summary>
    [Fact]
    public async Task EditingThePostClearsTheNoticeAndIsAnnounced()
    {
        var (drafting, compose) = await Refused();
        var announced = Announced(drafting);

        drafting.Drafting.Edit<ComposeScreen>(screen => screen.Rewrite("Shorter"));

        Assert.Null(drafting.Notice);
        Assert.Equal("Shorter", compose.Text);
        Assert.Equal(1, announced());
    }

    /// <summary>
    ///     Typing in the warning, which counts against the same limit, clears the refusal and is announced, so the
    ///     count at the foot is drawn again with the warning in it.
    /// </summary>
    [Fact]
    public async Task TypingInTheWarningUpdatesTheCountAndClearsTheRefusal()
    {
        var (drafting, compose) = await Refused(writingTheWarning: true);
        var announced = Announced(drafting);

        drafting.Drafting.Edit<ComposeScreen>(screen => screen.RewriteWarning("!!!"));

        Assert.Null(drafting.Notice);
        Assert.Equal(1, announced());

        // "Far too long" is twelve, and the warning's three count with it.
        Assert.Equal("15 / 500", compose.Lines(new Drawing(60, AShell.Now, Height: 17))[^1].Text.Trim());
    }

    /// <summary>Changing what Lang holds is an edit too.</summary>
    [Fact]
    public async Task ChangingTheLanguageClearsTheNoticeAndIsAnnounced()
    {
        var (drafting, compose) = await Refused();
        var announced = Announced(drafting);

        drafting.Drafting.Edit<ComposeScreen>(screen => screen.RewriteLanguage("fr"));

        Assert.Null(drafting.Notice);
        Assert.Equal("fr", compose.Lang.Held);
        Assert.Equal(1, announced());
    }

    /// <summary>The same for a language picked off the list under Lang.</summary>
    [Fact]
    public async Task PickingALanguageClearsTheNoticeAndIsAnnounced()
    {
        var (drafting, compose) = await Refused();
        var announced = Announced(drafting);
        var french = PostLanguageName.Matching("French")[0];

        drafting.Drafting.Edit<ComposeScreen>(screen => screen.PickLanguage(french));

        Assert.Null(drafting.Notice);
        Assert.Equal(ComposeLang.Spoken(french), compose.Lang.Held);
        Assert.Equal(1, announced());
    }

    /// <summary>
    ///     Moving the typing about — walking the fields, a click into one, <c>ctrl-w</c> — and opening the list under
    ///     Lang leave the notice where it is, so that it does not go before it has been read; the move is still
    ///     announced, so the screen follows it. Before #364 walking and <c>ctrl-w</c> took it down.
    /// </summary>
    [Theory]
    [InlineData("walk")]
    [InlineData("type into")]
    [InlineData("ctrl-w")]
    [InlineData("open the language list")]
    public async Task AMoveLeavesTheNoticeButIsAnnounced(string move)
    {
        var (drafting, _) = await Refused();
        var announced = Announced(drafting);
        var refusal = drafting.Notice;

        drafting.Drafting.Edit<ComposeScreen>(move switch
        {
            "walk" => screen => screen.Walk(-1),
            "type into" => screen => screen.TypeInto(ComposeField.Lang),
            "ctrl-w" => screen => screen.WriteTheWarning(),
            _ => screen => screen.OfferLanguages(true),
        });

        Assert.Equal(refusal, drafting.Notice);
        Assert.Equal(1, announced());
    }

    /// <summary>
    ///     Choosing who the post goes to on To is a move under the one rule (#364), as walking to To is: the notice
    ///     stays. Before #364 a choice took it down.
    /// </summary>
    [Fact]
    public async Task ChoosingOnToLeavesTheNotice()
    {
        var (drafting, compose) = await Refused();
        var refusal = drafting.Notice;

        drafting.Drafting.Edit<ComposeScreen>(screen => screen.TypeInto(ComposeField.To));

        var was = compose.Visibility;
        var announced = Announced(drafting);

        drafting.Drafting.Edit<ComposeScreen>(screen => screen.Choose(1));

        Assert.NotEqual(was, compose.Visibility);
        Assert.Equal(refusal, drafting.Notice);
        Assert.Equal(1, announced());
    }

    /// <summary>An edit that changed nothing leaves the notice and announces nothing.</summary>
    [Fact]
    public async Task AnEditThatChangesNothingIsNotAnnounced()
    {
        var (drafting, _) = await Refused();
        var announced = Announced(drafting);
        var refusal = drafting.Notice;

        var change = drafting.Drafting.Edit<ComposeScreen>(screen => screen.TypeInto(ComposeField.Post));

        Assert.Equal(DraftChange.None, change);
        Assert.Equal(refusal, drafting.Notice);
        Assert.Equal(0, announced());
    }

    /// <summary>
    ///     Saying back what the draft already holds is no edit: the post's own text, or the language Lang already holds
    ///     picked again. Before #364 either took the notice down.
    /// </summary>
    [Theory]
    [InlineData("the post")]
    [InlineData("the language")]
    public async Task SayingBackWhatTheDraftHoldsIsNoEdit(string field)
    {
        var french = PostLanguageName.Matching("French")[0];
        var (drafting, _) = await Refused(inLanguage: french);
        var refusal = drafting.Notice;
        var announced = Announced(drafting);

        var change = drafting.Drafting.Edit<ComposeScreen>(field == "the post"
            ? screen => screen.Rewrite("Far too long")
            : screen => screen.PickLanguage(french));

        Assert.NotNull(refusal);
        Assert.Equal(DraftChange.None, change);
        Assert.Equal(refusal, drafting.Notice);
        Assert.Equal(0, announced());
    }

    /// <summary>Nothing at all where compose is not on top: the edit is not made.</summary>
    [Fact]
    public void NothingIsEditedWhereComposeIsNotOnTop()
    {
        var drafting = new ADrafting().Build();
        var announced = Announced(drafting);
        var made = false;

        var change = drafting.Drafting.Edit<ComposeScreen>(_ =>
        {
            made = true;

            return DraftChange.Edited;
        });

        Assert.False(made);
        Assert.Equal(DraftChange.None, change);
        Assert.Equal(0, announced());
    }

    private static async Task<(ADrafting Drafting, ComposeScreen Compose)> Refused(
        bool writingTheWarning = false,
        PostLanguage? inLanguage = null)
    {
        var drafting = new ADrafting { Fakes = { Author = FakePostAuthor.Refusing(new PostRefusedException(TooLong)) } }
            .Build();

        drafting.Press(ShellKey.C);

        var compose = drafting.Composing;

        compose.Text = "Far too long";

        if (writingTheWarning)
        {
            drafting.Drafting.Edit<ComposeScreen>(screen => screen.WriteTheWarning());
            drafting.Drafting.Edit<ComposeScreen>(screen => screen.RewriteWarning("!!"));
        }

        if (inLanguage is { } language)
        {
            drafting.Drafting.Edit<ComposeScreen>(screen => screen.PickLanguage(language));
        }

        await drafting.Drafting.Send();
        drafting.Fakes.Host.Drain();

        Assert.Contains(TooLong, drafting.Notice);

        return (drafting, compose);
    }

    /// <summary>
    ///     Counts every change drafting announces from here on: a redraw asked for, or the notice said away — which the
    ///     shell announces as a change of its own.
    /// </summary>
    private static Func<int> Announced(ADrafting drafting)
    {
        var from = drafting.Changes + drafting.Said;

        return () => drafting.Changes + drafting.Said - from;
    }
}
