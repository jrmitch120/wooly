using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     A paste, which a terminal in bracketed-paste mode delivers as one string rather than as keys — so a field the
///     shell types into itself takes it whole, or it goes nowhere at all. The compose editor is a widget of its own and
///     takes its own pastes, which is why a screen that is not typing leaves the paste alone.
/// </summary>
public class ShellPasteTests
{
    /// <summary>The search prompt takes a paste the way it takes letters.</summary>
    [Fact]
    public async Task Paste_GoesIntoTheSearchPrompt()
    {
        var opened = await new AShell().Opened();

        opened.Press(ShellKey.Slash);

        Assert.True(opened.Paste("#dotnet"));
        Assert.Equal("#dotnet", Assert.IsType<SearchScreen>(opened.Screen).Query);
    }

    /// <summary>
    ///     The add screen's token field, where a paste is what the field is for (#245) — masked like typed letters, and
    ///     the line break a copy brings with it no part of the token.
    /// </summary>
    [Fact]
    public async Task Paste_GoesIntoTheTokenField_Masked_WithItsLineBreakLeftOut()
    {
        var shell = new AShell { Authorizer = FakeBrowserAuthorizer.Holding() };
        var opened = await shell.Opened();

        opened.Press(ShellKey.CtrlP);
        opened.Press(ShellKey.A);
        opened.Paste("hachyderm.io");
        opened.Press(ShellKey.Enter);
        shell.Host.Drain();
        opened.Press(ShellKey.T);

        Assert.True(opened.Paste("secret-token\r\n"));

        var drawn = string.Join(" ", AShell.Drawn(opened.Screen));
        Assert.DoesNotContain("secret", drawn);

        opened.Press(ShellKey.Enter);
        shell.Host.Drain();

        Assert.Equal(["secret-token"], shell.Verifier.Tokens);
    }

    /// <summary>
    ///     Off a field, a paste is nobody's — not a run of keys, which would boost, compose and delete by the letters
    ///     in it — and is left for whatever has focus.
    /// </summary>
    [Fact]
    public async Task Paste_IsLeftAlone_WhereNothingIsBeingTyped()
    {
        var shell = new AShell();
        var opened = await shell.Opened();
        var before = shell.Requests;

        Assert.False(opened.Paste("bcd"));
        shell.Host.Drain();

        Assert.IsType<FeedScreen>(opened.Screen);
        Assert.Equal(before, shell.Requests);
    }

    /// <summary>A compose screen's body is the editor widget's, which takes its own pastes.</summary>
    [Fact]
    public async Task Paste_IsLeftToTheEditor_OnCompose()
    {
        var opened = await new AShell().Opened();

        opened.Press(ShellKey.C);

        Assert.False(opened.Paste("hello"));
    }
}
