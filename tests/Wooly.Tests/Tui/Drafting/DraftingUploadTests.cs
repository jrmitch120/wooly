using Wooly.Core.Errors;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     Pending attachments going up (ADR-0026, #375), asked of drafting, which sends each one up as it is attached and
///     feeds what the instance answers into the compose it belongs to (ADR-0029): its progress, ready, and refused.
/// </summary>
public class DraftingUploadTests : IDisposable
{
    /// <summary>The content panel's inside at an 80 × 24 terminal.</summary>
    private const int Width = 78;

    private readonly TemporaryDirectory _files = new();

    /// <summary>
    ///     A file dropped on a compose is attached rather than typed, and starts going up as the profile acted as at
    ///     once (ADR-0026).
    /// </summary>
    [Fact]
    public void ADroppedFileIsAttachedAndSentUp()
    {
        var drafting = Composing();
        var cat = _files.WriteFile("cat.png");

        Assert.True(drafting.Drafting.Paste(cat));

        Assert.Equal(cat, Assert.Single(drafting.Fakes.Author.Attaching).Path);
        Assert.Equal("cat.png", Assert.Single(drafting.Composing.Media.Attachments).Name);
        Assert.Equal("personal", drafting.Fakes.Author.Attaching[0].Profile);
    }

    public void Dispose() => _files.Dispose();

    /// <summary>Drafting with a compose open over Home, its instance setting <paramref name="limits" />.</summary>
    private static ADrafting Composing(PostLimits? limits = null)
    {
        var drafting = new ADrafting { Fakes = { Limits = FakeInstanceLimits.Setting(limits) } }.Build();

        drafting.Press(ShellKey.C);
        drafting.Fakes.Host.Drain();

        return drafting;
    }

    /// <summary>The row naming <paramref name="name" />, drawn <paramref name="width" /> wide.</summary>
    private static string Row(ComposeScreen compose, string name, int width = Width) =>
        compose.Lines(new Drawing(width, AShell.Now, Height: 21))
            .Select(line => line.Text)
            .Single(row => row.Contains(name, StringComparison.Ordinal));
}
