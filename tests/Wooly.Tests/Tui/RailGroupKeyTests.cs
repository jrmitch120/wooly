using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;

namespace Wooly.Tests.Tui;

/// <summary>
///     Where <c>`</c> and <c>~</c> are said (#265): with the frame's keys on the keymap screen, and on the status row
///     beside <c>tab</c> wherever <c>tab</c> is the way out — but not on a prompt taking letters, where they are
///     letters.
/// </summary>
public class RailGroupKeyTests
{
    /// <summary>The keymap lists the pair among the keys that mean the same thing everywhere.</summary>
    [Fact]
    public async Task Help_ListsTheGroupKeysWithTheFrame()
    {
        var opened = await new AShell().Opened();

        var everywhere = new HelpScreen(opened.Screen).Lines(new Drawing(80, AShell.Now))
            .Select(line => line.Text)
            .SkipWhile(text => text != "Everywhere")
            .ToList();

        Assert.Contains(everywhere, text => text.StartsWith("` / ~ ", StringComparison.Ordinal));
    }

    /// <summary>The row offers the next group straight after the next destination.</summary>
    [Fact]
    public async Task Keys_OfferTheNextGroupBesideTheNextDestination()
    {
        var opened = await new AShell().Opened();
        var keys = opened.Keys.Select(key => key.Key).ToList();

        Assert.Equal("`", keys[keys.IndexOf("tab") + 1]);
        Assert.Contains(opened.Keys, key => key is { Key: "`", Does: "group" });
    }

    /// <summary>A prompt taking letters types <c>`</c>, so it does not offer it.</summary>
    [Fact]
    public async Task Keys_LeaveTheGroupKeyOffAPromptTakingLetters()
    {
        var shell = new AShell();
        var opened = await shell.Opened();

        opened.Search();
        shell.Host.Drain();

        Assert.DoesNotContain(opened.Keys, key => key.Key == "`");
    }
}
