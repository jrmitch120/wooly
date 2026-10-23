using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     The warning a compose screen carries, as far as the shell has any part in it: none, since it carries no letters
///     into either field. Everything else about the warning — what it opens on, what it sends, and the keys that reach
///     it — is drafting's (<see cref="DraftingWarningTests" />).
/// </summary>
public class WarnedReplyTests
{
    private static readonly Post Warned = APost.With(
        id: "220",
        account: "ben@hachyderm.io",
        contentWarning: "spoilers");

    /// <summary>
    ///     The shell carries no letters into a compose screen, the warning included: the field is a widget and takes
    ///     its own (#320), so a letter or a backspace the shell is handed reaches neither field.
    /// </summary>
    [Fact]
    public async Task Type_ReachesNeitherField()
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(Warned) };
        var opened = await shell.Opened();

        opened.Press(ShellKey.R);
        opened.Press(ShellKey.CtrlW);

        var compose = Assert.IsType<ComposeScreen>(opened.Screen);

        Assert.False(compose.IsTyping);

        opened.Type('x');
        opened.Backspace();

        Assert.False(opened.Paste("pasted"));
        Assert.Equal("spoilers", compose.Warning);
        Assert.Equal("@ben@hachyderm.io ", compose.Text);
    }
}
