using Wooly.Tui.Media;

namespace Wooly.Tests.Tui;

/// <summary>
///     A terminal recognised from its own environment as one that draws Kitty's Unicode placeholders — at once, and
///     only where it is sure: speaking Kitty graphics is not enough, because WezTerm speaks them and draws the
///     placeholders as boxes (#292).
/// </summary>
public class KnownTerminalTests
{
    [Theory]
    [InlineData("TERM_PROGRAM", "ghostty")]
    [InlineData("TERM", "xterm-ghostty")]
    [InlineData("TERM", "xterm-kitty")]
    [InlineData("KITTY_WINDOW_ID", "1")]
    [InlineData("GHOSTTY_RESOURCES_DIR", "/Applications/Ghostty.app/Contents/Resources/ghostty")]
    public void DrawsPlaceholders_WhereTheTerminalSaysWhichItIs(string name, string value) =>
        Assert.True(KnownTerminal.DrawsPlaceholders(Environment(name, value)));

    /// <summary>
    ///     Everything else draws through a box. WezTerm among them: it speaks Kitty graphics, but its placeholders are
    ///     not merged (wezterm/wezterm#7924), and it prints them as boxes.
    /// </summary>
    [Theory]
    [InlineData("TERM_PROGRAM", "WezTerm")]
    [InlineData("TERM_PROGRAM", "WarpTerminal")]
    [InlineData("TERM_PROGRAM", "Apple_Terminal")]
    [InlineData("TERM_PROGRAM", "iTerm.app")]
    [InlineData("TERM", "xterm-256color")]
    public void DrawsPlaceholders_NotForATerminalItCannotNameAsOne(string name, string value) =>
        Assert.False(KnownTerminal.DrawsPlaceholders(Environment(name, value)));

    [Fact]
    public void DrawsPlaceholders_NotWithNothingToGoOn() => Assert.False(KnownTerminal.DrawsPlaceholders(_ => null));

    /// <summary>
    ///     Windows Terminal draws sixel and not Kitty, so its own variable outweighs anything else in the environment
    ///     — a <c>TERM</c> carried in from somewhere else included.
    /// </summary>
    [Fact]
    public void DrawsPlaceholders_NeverInWindowsTerminal() =>
        Assert.False(KnownTerminal.DrawsPlaceholders(Environment(
            ("WT_SESSION", "0c5b2a3e-0000-0000-0000-000000000000"),
            ("TERM", "xterm-kitty"))));

    /// <summary>
    ///     WezTerm sets a <c>TERM</c> of its own only where its terminfo is installed, and passes on whatever it was
    ///     started with otherwise — so its own variables outweigh a <c>TERM</c> that says kitty.
    /// </summary>
    [Fact]
    public void DrawsPlaceholders_NeverInWezTerm() =>
        Assert.False(KnownTerminal.DrawsPlaceholders(Environment(
            ("WEZTERM_PANE", "0"),
            ("TERM", "xterm-kitty"))));

    /// <summary>
    ///     A multiplexer inherits the variables of the terminal it was started in but does not pass Kitty graphics
    ///     through (tmux passthrough is out of scope).
    /// </summary>
    [Theory]
    [InlineData("TMUX", "/private/tmp/tmux-501/default,1234,0")]
    [InlineData("STY", "1234.ttys001.host")]
    public void DrawsPlaceholders_NotInsideAMultiplexer(string name, string value) =>
        Assert.False(KnownTerminal.DrawsPlaceholders(Environment(
            (name, value),
            ("TERM_PROGRAM", "ghostty"),
            ("GHOSTTY_RESOURCES_DIR", "/Applications/Ghostty.app/Contents/Resources/ghostty"))));

    private static Func<string, string?> Environment(string name, string value) => Environment((name, value));

    private static Func<string, string?> Environment(params (string Name, string Value)[] variables) =>
        name => variables.FirstOrDefault(variable => variable.Name == name).Value;
}
