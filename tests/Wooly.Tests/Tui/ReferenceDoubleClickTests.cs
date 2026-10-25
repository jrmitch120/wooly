using Terminal.Gui.Input;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;
using static Wooly.Tests.Tui.ContentClicks;

namespace Wooly.Tests.Tui;

/// <summary>
///     A double click on a reference, sent at its cells on a headless terminal: its first click walks to the reference
///     (#475) and the pair is the <c>⏎</c> that opens it and lets it go (#474) — the reference the first click walked
///     to, whatever its brackets then moved under the pointer (#479).
/// </summary>
public class ReferenceDoubleClickTests
{
    /// <summary>A post with one of each kind of reference in its text.</summary>
    private const string Said = "<p>Thanks @maria@fosstodon.org — notes at https://example.com/notes #dotnet</p>";

    /// <summary>
    ///     A double click on a hashtag pushes its screen and one on a mention opens the account, and once back on the
    ///     feed nothing is held there: the one <c>esc</c> that came back was not spent letting a bracket go.
    /// </summary>
    [Theory]
    [InlineData("#dotnet")]
    [InlineData("@maria@fosstodon.org")]
    public async Task ADoubleClickOnAHashtagOrAMentionOpensIt(string reference)
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "110"),
                APost.With(id: "220", content: Said, mentions: ["maria@fosstodon.org"])),
            Accounts = FakeAccountRelationships.Holding(AnAccount.With(address: "maria@fosstodon.org")),
        };

        using var drawn = await DrawnShell.Of(100, Tall, Themes.Plain, built);

        DoubleClickOn(drawn, reference);

        Assert.Equal(2, drawn.Shell.Depth);

        if (reference.StartsWith('#'))
        {
            Assert.Equal("dotnet", built.Timelines.Reads[^1].Timeline.Hashtag);
        }
        else
        {
            Assert.IsType<AccountScreen>(drawn.Shell.Screen);
            Assert.Equal("maria@fosstodon.org", built.Accounts.Reads[^1].Account.Text);
        }

        drawn.Press(Key.Esc);
        drawn.Settle();

        Assert.Equal(1, drawn.Shell.Depth);
        Assert.Equal("220", drawn.Shell.Screen.Picked?.Id);
        Assert.Null(drawn.Shell.Screen.Reference);
        Assert.DoesNotContain(drawn.Rows(), row => row.Contains('‹'));
    }

    /// <summary>
    ///     A double click on an address in the text, an attachment's own address drawn as its kind, or a link preview's
    ///     drawn as its title hands that address to the browser, and leaves nothing held.
    /// </summary>
    [Theory]
    [InlineData("https://example.com/notes", "https://example.com/notes")]
    [InlineData("Video", "https://files.mastodon.social/m1/original.png")]
    [InlineData("Sheep, at length", "https://example.com/sheep")]
    public async Task ADoubleClickOnAnAddressHandsItToTheBrowser(string drawnAs, string address)
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "110"),
                APost.With(
                    id: "220",
                    content: "Notes at https://example.com/notes",
                    media: [APost.Attached(MediaKind.Video, description: null)],
                    linkPreview: APost.ALinkPreview())),
        };

        using var drawn = await DrawnShell.Of(100, Tall, Themes.Plain, built);

        var feed = drawn.Shell.Screen;

        DoubleClickOn(drawn, drawnAs);

        Assert.Equal(address, Assert.Single(built.Browser.Opened).AbsoluteUri);
        Assert.Same(feed, drawn.Shell.Screen);
        Assert.Equal("220", drawn.Shell.Screen.Picked?.Id);
        Assert.Null(drawn.Shell.Screen.Reference);
        Assert.DoesNotContain(drawn.Rows(), row => row.Contains('‹'));
    }

    /// <summary>
    ///     A double click on a reference's first character opens that reference, though the first click's opening
    ///     bracket took the character's place under the pointer and pushed the reference a column on.
    /// </summary>
    [Fact]
    public async Task ADoubleClickOnTheFirstCharacterOpensThatReference()
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "110"),
                APost.With(id: "220", content: "Notes at https://example.com/notes and https://example.com/more")),
        };

        using var drawn = await DrawnShell.Of(100, Tall, Themes.Plain, built);

        var row = RowOf(drawn, "https://example.com/more");

        drawn.DoubleClick(ColumnOf(drawn, row, "https://example.com/more"), row);
        drawn.Settle();

        Assert.Equal("https://example.com/more", Assert.Single(built.Browser.Opened).AbsoluteUri);
        Assert.Null(drawn.Shell.Screen.Reference);
    }

    /// <summary>
    ///     A double click on the second row of a long address opens that address, though the first click's brackets
    ///     wrapped it anew and moved what is under the pointer.
    /// </summary>
    [Fact]
    public async Task ADoubleClickOnAnAddressTheBracketsRewrapOpensIt()
    {
        var address = $"https://example.com/{new string('a', 50)}{new string('b', 50)}";

        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "110"),
                APost.With(id: "220", content: $"Shearing {address} today")),
        };

        using var drawn = await DrawnShell.Of(80, Tall, Themes.Plain, built);

        var row = RowOf(drawn, "bbbb");
        var column = ColumnOf(drawn, row, "bbbb");
        var before = drawn.Rows();

        drawn.Click(column, row);

        Assert.Equal(address, drawn.Shell.Screen.Reference?.Text);
        Assert.NotEqual(before[row], drawn.Rows()[row]);

        // The pair as Terminal.Gui sends it once the first click has drawn its brackets.
        drawn.Point(column, row, MouseFlags.LeftButtonPressed);
        drawn.Point(column, row, MouseFlags.LeftButtonReleased);
        drawn.Point(column, row, MouseFlags.LeftButtonDoubleClicked);
        drawn.Settle();

        Assert.Equal(address, Assert.Single(built.Browser.Opened).AbsoluteUri);
        Assert.Null(drawn.Shell.Screen.Reference);
    }

    /// <summary>A double click on a post's text outside any reference reads the post, as it always has.</summary>
    [Fact]
    public async Task ADoubleClickOutsideAnyReferenceReadsThePost()
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "110"),
                APost.With(id: "220", content: "<p>Shearing day #sheep</p>")),
        };

        using var drawn = await DrawnShell.Of(80, Tall, Themes.Plain, built);

        DoubleClickOn(drawn, "Shearing day");

        var post = Assert.IsType<PostScreen>(drawn.Shell.Screen);

        Assert.Equal("220", post.Picked?.Id);
        Assert.Empty(built.Browser.Opened);
    }

    /// <summary>
    ///     A double click whose first click was spent declining an open question opens nothing, even where the pair
    ///     lands on a reference.
    /// </summary>
    [Fact]
    public async Task ADoubleClickThatDeclinedAQuestionOpensNothing()
    {
        var answers = Enumerable.Range(1, 3).Select(at => APost.AnAnswer($"Answer {at}", at)).ToList();

        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "110", poll: APost.APoll(options: answers)),
                APost.With(id: "220", content: "Notes at https://example.com/notes")),
        };

        using var drawn = await DrawnShell.Of(80, Tall, Themes.Plain, built);

        var feed = drawn.Shell.Screen;

        drawn.Press(new Key('2'));
        drawn.Press(Key.V);

        Assert.NotNull(drawn.Shell.Asking);

        DoubleClickOn(drawn, "https://example.com/notes");

        Assert.Null(drawn.Shell.Asking);
        Assert.Same(feed, drawn.Shell.Screen);
        Assert.Equal("110", drawn.Shell.Screen.Picked?.Id);
        Assert.Null(drawn.Shell.Screen.Reference);
        Assert.Empty(built.Browser.Opened);
    }

    /// <summary>
    ///     A double click on the toggle's mark after a click that walked to a reference is the toggle and nothing more:
    ///     its first click spent the walk, so the pair opens nothing and the reference stays held.
    /// </summary>
    [Fact]
    public async Task ADoubleClickThatToggledTheRailOpensNothing()
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "110"),
                APost.With(id: "220", content: "Notes at https://example.com/notes")),
        };

        using var drawn = await DrawnShell.Of(80, Tall, Themes.Plain, built);

        var row = RowOf(drawn, "https://example.com/notes");

        drawn.Click(ColumnOf(drawn, row, "https://example.com/notes") + 4, row);

        Assert.NotNull(drawn.Shell.Screen.Reference);

        var rail = drawn.Shell.RailState;

        // The toggle's « at the shell's bottom-left.
        drawn.DoubleClick(1, drawn.Rows().Length - 2);
        drawn.Settle();

        Assert.NotEqual(rail, drawn.Shell.RailState);
        Assert.Empty(built.Browser.Opened);
        Assert.NotNull(drawn.Shell.Screen.Reference);
    }

    /// <summary>A double click on the middle of the first place in the content <paramref name="text" /> is drawn.</summary>
    private static void DoubleClickOn(DrawnShell drawn, string text)
    {
        var row = RowOf(drawn, text);

        drawn.DoubleClick(ColumnOf(drawn, row, text) + text.Length / 2, row);
        drawn.Settle();
    }

    /// <summary>The column <paramref name="text" /> starts at on <paramref name="row" />, in the content.</summary>
    private static int ColumnOf(DrawnShell drawn, int row, string text)
    {
        var column = drawn.Rows()[row].IndexOf(text, RailLines.Width, StringComparison.Ordinal);

        Assert.True(column >= 0, $"Row {row} does not read {text}:\n{string.Join('\n', drawn.Rows())}");

        return column;
    }
}
