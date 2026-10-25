using Terminal.Gui.Input;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     <c>⏎</c> on a picked reference lets it go, whatever came of opening it (#474): a hashtag pushed, a mention's
///     account opened, an address handed to the browser, or a refusal. Walking back out of what it opened, the first
///     <c>esc</c> goes back, and on a link the next <c>→</c> starts again at the first reference.
/// </summary>
/// <remarks>
///     Pressed and read back off the whole shell drawn headless, with the fake browser, because what a reader sees is
///     the brackets and the status row: whether either is still saying <c>⏎ open</c> once the reference was opened.
/// </remarks>
public class ReferenceOpenLetGoTests
{
    /// <summary>A post with one of each kind of reference in its text, in the order they are walked.</summary>
    private const string Said = "<p>Thanks @maria@fosstodon.org — notes at https://example.com/notes #dotnet</p>";

    /// <summary>What the status row says only while a reference is picked.</summary>
    private const string ReferenceMode = "Open: ⏎";

    /// <summary>
    ///     An address handed to the browser leaves nothing held on the screen it was opened from — an address in the
    ///     text, an attachment's own and a link preview's alike — and the brackets and the status row say so.
    /// </summary>
    [Theory]
    [InlineData("link")]
    [InlineData("attachment")]
    [InlineData("preview")]
    public async Task OpeningAnAddressLetsTheReferenceGo(string kind)
    {
        var post = kind switch
        {
            "link" => APost.With(id: "110", content: "<p>Notes at https://example.com/notes</p>"),
            "attachment" => APost.With(id: "110", content: "Hello", media: [APost.Attached(MediaKind.Video)]),
            _ => APost.With(id: "110", content: "Hello", linkPreview: APost.ALinkPreview()),
        };

        var built = new AShell { Timelines = FakeTimelineReader.Holding(post) };

        using var drawn = await DrawnShell.Of(100, 30, Themes.Plain, built);

        drawn.Press(Key.CursorRight);

        Assert.Contains(drawn.Rows(), row => row.Contains(ReferenceMode, StringComparison.Ordinal));

        drawn.Press(Key.Enter);
        drawn.Settle();

        Assert.Single(built.Browser.Opened);
        Assert.Null(drawn.Shell.Screen.Reference);
        Assert.DoesNotContain(drawn.Rows(), row => row.Contains('‹'));
        Assert.DoesNotContain(drawn.Rows(), row => row.Contains(ReferenceMode, StringComparison.Ordinal));
    }

    /// <summary>
    ///     A hashtag or a mention pushes a screen, and the screen it was opened from holds nothing once the reader is
    ///     back on it: the one <c>esc</c> that came back was not spent letting an old bracket go.
    /// </summary>
    [Theory]
    [InlineData("hashtag")]
    [InlineData("mention")]
    public async Task OpeningAHashtagOrAMentionLetsTheReferenceGo(string kind)
    {
        var built = Reading();

        using var drawn = await DrawnShell.Of(100, 30, Themes.Plain, built);

        drawn.Press(kind == "hashtag" ? Key.CursorLeft : Key.CursorRight);
        drawn.Press(Key.Enter);
        drawn.Settle();

        Assert.Equal(2, drawn.Shell.Depth);

        drawn.Press(Key.Esc);
        drawn.Settle();

        Assert.Equal(1, drawn.Shell.Depth);
        Assert.Null(drawn.Shell.Screen.Reference);
        Assert.DoesNotContain(drawn.Rows(), row => row.Contains('‹'));
        Assert.DoesNotContain(drawn.Rows(), row => row.Contains(ReferenceMode, StringComparison.Ordinal));
    }

    /// <summary>
    ///     A refusal lets the reference go as well — an address this client will not hand to a machine, and a machine
    ///     with no browser to hand it to — so the rule has no exception. The refusal is still said.
    /// </summary>
    [Theory]
    [InlineData("refused", "That kind of address isn't opened.")]
    [InlineData("no browser", "No browser available.")]
    public async Task ARefusedOpenLetsTheReferenceGoToo(string kind, string notice)
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(
                    id: "110",
                    content: kind == "refused"
                        ? "<p>Diagram at www.[/x</p>"
                        : "<p>Notes at https://example.com/notes</p>")),
            Browser = kind == "refused" ? new FakeWebBrowser() : FakeWebBrowser.WithNothingToOpen(),
        };

        using var drawn = await DrawnShell.Of(100, 30, Themes.Plain, built);

        drawn.Press(Key.CursorRight);
        drawn.Press(Key.Enter);
        drawn.Settle();

        Assert.Equal(notice, drawn.Shell.Notice);
        Assert.Null(drawn.Shell.Screen.Reference);
        Assert.DoesNotContain(drawn.Rows(), row => row.Contains('‹'));
    }

    /// <summary>
    ///     Walking back out of a hashtag or an account a reference opened from inside a post: the first <c>esc</c>
    ///     comes back to the post and the next goes back again, neither spent letting a reference go.
    /// </summary>
    [Theory]
    [InlineData("hashtag")]
    [InlineData("mention")]
    public async Task EscWalksStraightBackOutOfWhatAReferenceOpened(string kind)
    {
        var built = Reading();

        using var drawn = await DrawnShell.Of(100, 30, Themes.Plain, built);

        drawn.Press(Key.Enter);
        drawn.Settle();

        Assert.IsType<PostScreen>(drawn.Shell.Screen);

        drawn.Press(kind == "hashtag" ? Key.CursorLeft : Key.CursorRight);
        drawn.Press(Key.Enter);
        drawn.Settle();

        Assert.Equal(3, drawn.Shell.Depth);

        drawn.Press(Key.Esc);
        drawn.Settle();

        Assert.IsType<PostScreen>(drawn.Shell.Screen);
        Assert.Equal(2, drawn.Shell.Depth);

        drawn.Press(Key.Esc);
        drawn.Settle();

        Assert.Equal(1, drawn.Shell.Depth);
    }

    /// <summary>
    ///     With a link opened in the browser, the next <c>→</c> starts again at the first reference rather than
    ///     carrying on from the link.
    /// </summary>
    [Fact]
    public async Task AfterALinkIsOpenedTheNextArrowEntersAtTheFirstReference()
    {
        var built = Reading();

        using var drawn = await DrawnShell.Of(100, 30, Themes.Plain, built);

        drawn.Press(Key.CursorRight);
        drawn.Press(Key.CursorRight);

        Assert.Equal(Role.Link, drawn.Shell.Screen.Reference?.Role);

        drawn.Press(Key.Enter);
        drawn.Settle();

        Assert.Equal("https://example.com/notes", Assert.Single(built.Browser.Opened).AbsoluteUri);

        drawn.Press(Key.CursorRight);

        Assert.Equal(Role.Mention, drawn.Shell.Screen.Reference?.Role);
    }

    /// <summary>A feed whose one post names <c>@maria@fosstodon.org</c>, whose account the instance holds.</summary>
    private static AShell Reading() => new()
    {
        Timelines = FakeTimelineReader.Holding(
            APost.With(id: "110", content: Said, mentions: ["maria@fosstodon.org"])),
        Accounts = FakeAccountRelationships.Holding(AnAccount.With(address: "maria@fosstodon.org")),
    };
}
