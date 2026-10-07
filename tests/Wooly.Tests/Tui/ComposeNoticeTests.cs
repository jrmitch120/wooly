using Wooly.Core.Errors;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;
using Key = Terminal.Gui.Input.Key;

namespace Wooly.Tests.Tui;

/// <summary>
///     A notice said over a post being written goes once the writer does what it asked: edits the draft (#319). The
///     status row holds a notice or the keymap and never both, and every key compose answers to was hidden behind a
///     refusal that only <c>esc</c> — which throws the draft away — would clear.
/// </summary>
public class ComposeNoticeTests
{
    private const string TooLong = "Text character limit of 500 exceeded";

    /// <summary>Changing the post takes the refusal off the row, and is announced so that it is drawn again.</summary>
    [Fact]
    public async Task EditingThePostClearsTheNoticeAndIsAnnounced()
    {
        var (shell, compose) = await Refused();
        var announced = Announced(shell);

        shell.EditCompose(screen => screen.Rewrite("Shorter"));

        Assert.Null(shell.Notice);
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
        var (shell, compose) = await Refused(writingTheWarning: true);
        var announced = Announced(shell);

        shell.EditCompose(screen => screen.RewriteWarning("!!!"));

        Assert.Null(shell.Notice);
        Assert.Equal(1, announced());

        // "Far too long" is twelve, and the warning's three count with it.
        Assert.Equal("15 / 500", compose.Lines(new Drawing(60, AShell.Now, Height: 17))[^1].Text.Trim());
    }

    /// <summary>Changing what Lang holds is an edit too.</summary>
    [Fact]
    public async Task ChangingTheLanguageClearsTheNoticeAndIsAnnounced()
    {
        var (shell, compose) = await Refused();
        var announced = Announced(shell);

        shell.EditCompose(screen => screen.RewriteLanguage("fr"));

        Assert.Null(shell.Notice);
        Assert.Equal("fr", compose.Lang.Held);
        Assert.Equal(1, announced());
    }

    /// <summary>The same for a language picked off the list under Lang.</summary>
    [Fact]
    public async Task PickingALanguageClearsTheNoticeAndIsAnnounced()
    {
        var (shell, compose) = await Refused();
        var announced = Announced(shell);
        var french = PostLanguageName.Matching("French")[0];

        shell.EditCompose(screen => screen.PickLanguage(french));

        Assert.Null(shell.Notice);
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
        var (shell, _) = await Refused();
        var announced = Announced(shell);
        var refusal = shell.Notice;

        shell.EditCompose(move switch
        {
            "walk" => screen => screen.Walk(-1),
            "type into" => screen => screen.TypeInto(ComposeField.Lang),
            "ctrl-w" => screen => screen.WriteTheWarning(),
            _ => screen => screen.OfferLanguages(true),
        });

        Assert.Equal(refusal, shell.Notice);
        Assert.Equal(1, announced());
    }

    /// <summary>
    ///     Choosing who the post goes to on To is a move under the one rule (#364), as walking to To is: the notice
    ///     stays. Before #364 a choice took it down.
    /// </summary>
    [Fact]
    public async Task ChoosingOnToLeavesTheNotice()
    {
        var (shell, compose) = await Refused();
        var refusal = shell.Notice;

        shell.EditCompose(screen => screen.TypeInto(ComposeField.To));

        var was = compose.Visibility;
        var announced = Announced(shell);

        shell.EditCompose(screen => screen.Choose(1));

        Assert.NotEqual(was, compose.Visibility);
        Assert.Equal(refusal, shell.Notice);
        Assert.Equal(1, announced());
    }

    /// <summary>An edit that changed nothing leaves the notice and announces nothing.</summary>
    [Fact]
    public async Task AnEditThatChangesNothingIsNotAnnounced()
    {
        var (shell, _) = await Refused();
        var announced = Announced(shell);
        var refusal = shell.Notice;

        var change = shell.EditCompose(screen => screen.TypeInto(ComposeField.Post));

        Assert.Equal(ComposeChange.None, change);
        Assert.Equal(refusal, shell.Notice);
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
        var (shell, _) = await Refused(inLanguage: french);
        var refusal = shell.Notice;
        var announced = Announced(shell);

        var change = shell.EditCompose(field == "the post"
            ? screen => screen.Rewrite("Far too long")
            : screen => screen.PickLanguage(french));

        Assert.NotNull(refusal);
        Assert.Equal(ComposeChange.None, change);
        Assert.Equal(refusal, shell.Notice);
        Assert.Equal(0, announced());
    }

    /// <summary>Nothing at all where compose is not on top: the edit is not made.</summary>
    [Fact]
    public async Task NothingIsEditedWhereComposeIsNotOnTop()
    {
        var shell = await new AShell().Opened();
        var announced = Announced(shell);
        var made = false;

        var change = shell.EditCompose(_ =>
        {
            made = true;

            return ComposeChange.Edited;
        });

        Assert.False(made);
        Assert.Equal(ComposeChange.None, change);
        Assert.Equal(0, announced());
    }

    /// <summary>Typed into the real editor, the status row's keys are back the moment the draft changes.</summary>
    [Fact]
    public async Task TypingIntoTheEditorBringsTheKeysBack()
    {
        var built = new AShell { Author = FakePostAuthor.Refusing(new PostRefusedException(TooLong)) };

        using var drawn = await DrawnShell.Of(80, 24, Themes.Dark, built);

        drawn.Shell.Compose();
        drawn.Redraw();
        drawn.Window.NewKeyDownEvent(new Key('a'));

        await drawn.Shell.Send();
        drawn.Settle();

        Assert.Contains(TooLong, drawn.Rows()[^1]);

        drawn.Press(new Key('b'));

        Assert.Contains("ctrl-s", drawn.Rows()[^1]);
    }

    private static async Task<(Wooly.Tui.Shell.Shell Shell, ComposeScreen Compose)> Refused(
        bool writingTheWarning = false,
        PostLanguage? inLanguage = null)
    {
        var built = new AShell { Author = FakePostAuthor.Refusing(new PostRefusedException(TooLong)) };
        var shell = await built.Opened();
        var compose = ComposeRows.Open(shell, ComposeFor.Post);

        compose.Text = "Far too long";

        if (writingTheWarning)
        {
            shell.EditCompose(screen => screen.WriteTheWarning());
            shell.EditCompose(screen => screen.RewriteWarning("!!"));
        }

        if (inLanguage is { } language)
        {
            shell.EditCompose(screen => screen.PickLanguage(language));
        }

        await shell.Send();
        built.Host.Drain();

        Assert.Contains(TooLong, shell.Notice);

        return (shell, compose);
    }

    /// <summary>Counts every change <paramref name="shell" /> announces from here on.</summary>
    private static Func<int> Announced(Wooly.Tui.Shell.Shell shell)
    {
        var changes = 0;

        shell.Changed += () => changes++;

        return () => changes;
    }
}
