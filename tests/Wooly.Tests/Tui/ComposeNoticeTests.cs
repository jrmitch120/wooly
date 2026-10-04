using Wooly.Core.Errors;
using Wooly.Tests.Fakes;
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

    /// <summary>Changing the post takes the refusal off the row.</summary>
    [Fact]
    public async Task EditingThePostClearsTheNotice()
    {
        var (shell, compose) = await Refused();

        shell.Rewrite("Shorter");

        Assert.Null(shell.Notice);
        Assert.Equal("Shorter", compose.Text);
    }

    /// <summary>So does changing the warning, which counts against the same limit.</summary>
    [Fact]
    public async Task EditingTheWarningClearsTheNotice()
    {
        var (shell, compose) = await Refused(writingTheWarning: true);

        shell.RewriteWarning("!");

        Assert.Null(shell.Notice);
        Assert.True(compose.WritingTheWarning);
    }

    /// <summary>And moving the typing between the two, which is the writer setting about it.</summary>
    [Fact]
    public async Task MovingTheTypingClearsTheNotice()
    {
        var (shell, _) = await Refused();

        shell.WriteWarning();

        Assert.Null(shell.Notice);
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

    private static async Task<(Wooly.Tui.Shell.Shell Shell, ComposeScreen Compose)> Refused(bool writingTheWarning = false)
    {
        var built = new AShell { Author = FakePostAuthor.Refusing(new PostRefusedException(TooLong)) };
        var shell = await built.Opened();
        var compose = ComposeRows.Open(shell, ComposeFor.Post);

        compose.Text = "Far too long";

        if (writingTheWarning)
        {
            shell.WriteWarning();
            shell.RewriteWarning("!!");
        }

        await shell.Send();
        built.Host.Drain();

        Assert.Contains(TooLong, shell.Notice);

        return (shell, compose);
    }
}
