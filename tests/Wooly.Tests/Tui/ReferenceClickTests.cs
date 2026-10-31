using Terminal.Gui.Input;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;
using static Wooly.Tests.Tui.ContentClicks;

namespace Wooly.Tests.Tui;

/// <summary>
///     A click on a reference, sent at its cells on a headless terminal: it picks the thing the reference is inside and
///     walks to it, exactly as <c>→</c> would have, and opens nothing (#475).
/// </summary>
public class ReferenceClickTests
{
    /// <summary>
    ///     A hashtag, a mention and an address in a post's text each pick the post and walk to that reference — and the
    ///     browser hears nothing and no screen is pushed.
    /// </summary>
    [Theory]
    [InlineData("#sheep")]
    [InlineData("@ann@hachyderm.io")]
    [InlineData("https://example.com/flock")]
    public async Task AClickOnAReferenceInAPostsTextWalksToIt(string reference)
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "110"),
                APost.With(id: "220", content: "Shearing #sheep with @ann@hachyderm.io at https://example.com/flock")),
        };

        using var drawn = await DrawnShell.Of(80, Tall, Themes.Plain, built);

        var depth = drawn.Shell.Depth;

        ClickOn(drawn, reference);

        Assert.Equal("220", drawn.Shell.Screen.Picked?.Id);
        Assert.Equal(reference, drawn.Shell.Screen.Reference?.Text);
        Assert.Empty(built.Browser.Opened);
        Assert.Equal(depth, drawn.Shell.Depth);
    }

    /// <summary>
    ///     A reference in a bio and in a custom field is walked to the same way, picking the header block it is
    ///     written in.
    /// </summary>
    [Theory]
    [InlineData("#linocut")]
    [InlineData("https://maria.example/shop")]
    public async Task AClickOnAReferenceInTheHeaderWalksToIt(string reference)
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "110", account: "maria@hachyderm.io")),
            Accounts = FakeAccountRelationships.Holding(AnAccount.With(
                address: "maria@hachyderm.io",
                bio: "Occasional #linocut poster.",
                fields: [AnAccount.Field("Shop", "https://maria.example/shop")])),
        };

        using var drawn = await DrawnShell.Of(80, Tall, Themes.Plain, built);

        await drawn.Shell.OpenAuthor();
        built.Host.Drain();
        drawn.Redraw();
        Assert.Null(drawn.Shell.Screen.Reference);

        var depth = drawn.Shell.Depth;

        ClickOn(drawn, reference);

        // The header block is no post, so what is picked once it is is none.
        Assert.Null(drawn.Shell.Screen.Picked);
        Assert.Equal(reference, drawn.Shell.Screen.Reference?.Text);
        Assert.Empty(built.Browser.Opened);
        Assert.Equal(depth, drawn.Shell.Depth);
    }

    /// <summary>
    ///     An attachment's own address, drawn as its kind, and a link preview's address, drawn as its title, are walked
    ///     to off a click on what is drawn for them.
    /// </summary>
    [Theory]
    [InlineData("Video", "https://files.mastodon.social/m1/original.png")]
    [InlineData("Sheep, at length", "https://example.com/sheep")]
    public async Task AClickOnAnAttachmentOrALinkPreviewWalksToIt(string drawnAs, string address)
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "110"),
                APost.With(
                    id: "220",
                    content: "Two sheep",
                    media: [APost.Attached(MediaKind.Video, description: null)],
                    linkPreview: APost.ALinkPreview())),
        };

        using var drawn = await DrawnShell.Of(80, Tall, Themes.Plain, built);

        var depth = drawn.Shell.Depth;

        ClickOn(drawn, drawnAs);

        Assert.Equal("220", drawn.Shell.Screen.Picked?.Id);
        Assert.Equal(address, drawn.Shell.Screen.Reference?.Text);
        Assert.Empty(built.Browser.Opened);
        Assert.Equal(depth, drawn.Shell.Depth);
    }

    /// <summary>An address the wrap cut across two rows answers a click on either of them.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task AClickOnEitherRowOfAWrappedAddressWalksToIt(int half)
    {
        var address = $"https://example.com/{new string('a', 50)}{new string('b', 50)}";

        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "110"),
                APost.With(id: "220", content: address)),
        };

        using var drawn = await DrawnShell.Of(80, Tall, Themes.Plain, built);

        var first = RowOf(drawn, "https://example.com/");
        var row = first + half;
        var text = half == 0 ? "https://example.com/" : "bbbb";

        Assert.Contains(text, drawn.Rows()[row]);

        drawn.Click(ColumnOf(drawn, row, text), row);

        Assert.Equal("220", drawn.Shell.Screen.Picked?.Id);
        Assert.Equal(address, drawn.Shell.Screen.Reference?.Text);
    }

    /// <summary>A click on a reference in another post picks that post, and the reference held in the old one goes.</summary>
    [Fact]
    public async Task AClickOnAReferenceInAnotherPostLetsTheOldOneGo()
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "110", content: "Shearing #day"),
                APost.With(id: "220", content: "Carding #wool")),
        };

        using var drawn = await DrawnShell.Of(80, Tall, Themes.Plain, built);

        drawn.Press(Key.CursorRight);

        Assert.Equal("#day", drawn.Shell.Screen.Reference?.Text);

        ClickOn(drawn, "#wool");

        Assert.Equal("220", drawn.Shell.Screen.Picked?.Id);
        Assert.Equal("#wool", drawn.Shell.Screen.Reference?.Text);
        Assert.DoesNotContain("‹#day›", string.Join('\n', drawn.Rows()));
    }

    /// <summary>A click on either bracket of the reference already walked to keeps it walked to.</summary>
    [Theory]
    [InlineData("‹")]
    [InlineData("›")]
    public async Task AClickOnTheBracketOfThePickedReferenceKeepsIt(string bracket)
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "110", content: "Shearing #sheep and #wool")),
        };

        using var drawn = await DrawnShell.Of(80, Tall, Themes.Plain, built);

        drawn.Press(Key.CursorRight);
        drawn.Press(Key.CursorRight);

        var row = RowOf(drawn, "‹#wool›");

        drawn.Click(ColumnOf(drawn, row, bracket), row);

        Assert.Equal("110", drawn.Shell.Screen.Picked?.Id);
        Assert.Equal("#wool", drawn.Shell.Screen.Reference?.Text);
    }

    /// <summary>
    ///     A click on text outside any reference picks the post as it always has, and walks to nothing — a picture drawn
    ///     as a picture being a reference of its own, walked to by a click on it (<c>PictureClickTests</c>).
    /// </summary>
    [Fact]
    public async Task AClickOutsideAnyReferencePicksThePostAsBefore()
    {
        var pictures = new FakePictures().Holding("m1", 800, 600);

        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "110", content: "Shearing #day"),
                APost.With(id: "220", content: "Carding #wool", media: [APost.APicture("m1")])),
        };

        using var drawn = await DrawnShell.Of(80, Tall, Themes.Plain, built, pictures: pictures, drawsPictures: true);

        drawn.Press(Key.CursorRight);
        ClickOn(drawn, "Carding");

        Assert.Equal("220", drawn.Shell.Screen.Picked?.Id);
        Assert.Null(drawn.Shell.Screen.Reference);
    }

    /// <summary>A click on the middle of the first place in the content <paramref name="text" /> is drawn.</summary>
    private static void ClickOn(DrawnShell drawn, string text)
    {
        var row = RowOf(drawn, text);

        drawn.Click(ColumnOf(drawn, row, text) + text.Length / 2, row);
    }

    /// <summary>The column <paramref name="text" /> starts at on <paramref name="row" />, in the content.</summary>
    private static int ColumnOf(DrawnShell drawn, int row, string text)
    {
        var column = drawn.Rows()[row].IndexOf(text, RailLines.Width, StringComparison.Ordinal);

        Assert.True(column >= 0, $"Row {row} does not read {text}:\n{string.Join('\n', drawn.Rows())}");

        return column;
    }
}
