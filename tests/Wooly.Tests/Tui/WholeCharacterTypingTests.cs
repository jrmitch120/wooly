using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     A field the shell types into itself — the search prompt, a filter, the add screen's fields — takes and gives back
///     whole characters, read off the whole shell drawn headless: an emoji is two halves of a pair in the text, and half
///     of one is no character at all, which the terminal refuses to draw (#534).
/// </summary>
public class WholeCharacterTypingTests
{
    /// <summary>
    ///     A backspace after an emoji takes the whole emoji — and the frame after it draws, where taking half of it left
    ///     a half the terminal refused, and the client crashed.
    /// </summary>
    [Theory]
    [InlineData("sheep 🐑")]
    [InlineData("coder 👩‍💻")]
    [InlineData("flag 🇺🇸")]
    [InlineData("thumb 👍🏽")]
    public async Task ABackspaceAfterAnEmojiTakesTheWholeEmoji(string pasted)
    {
        using var drawn = await DrawnShell.Of(80, 24, Themes.Dark);

        drawn.Press(new Key('/'));
        drawn.Shell.Paste(pasted);
        drawn.Redraw();

        var word = pasted[..pasted.IndexOf(' ')];

        drawn.Press(Key.Backspace);

        Assert.Equal(word + " ", Assert.IsType<SearchScreen>(drawn.Shell.Screen).Query);
        Assert.Contains(drawn.Rows(), row => row.Contains($"Search: {word} ▌", StringComparison.Ordinal));
    }

    /// <summary>
    ///     An emoji typed as a key, as a terminal sends one picked from the system's emoji palette, goes into the prompt
    ///     as itself — not as the one half of it a character can hold.
    /// </summary>
    [Fact]
    public async Task AnEmojiTypedAsAKeyGoesInWhole()
    {
        using var drawn = await DrawnShell.Of(80, 24, Themes.Dark);

        drawn.Press(new Key('/'));
        drawn.Press(new Key((KeyCode)0x1F411));

        Assert.Equal("🐑", Assert.IsType<SearchScreen>(drawn.Shell.Screen).Query);
        Assert.Contains(drawn.Rows(), row => row.Contains("Search: 🐑", StringComparison.Ordinal));
    }
}
