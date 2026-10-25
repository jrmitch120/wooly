using Terminal.Gui.Input;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Theme;
using static Wooly.Tests.Tui.ContentClicks;

namespace Wooly.Tests.Tui;

/// <summary>
///     <c>→</c> and <c>←</c> with the pick scrolled off the page, and entering a walk at the page (#477): with the pick
///     off it they reclaim and enter the reclaimed thing's references in the one press, and either way they enter at
///     the first or last reference on the page. Pressed and wheeled on the whole shell drawn headless, and read back off
///     what is picked, what reference is held and the rows as drawn.
/// </summary>
public class ReferenceEnterAtThePageTests
{
    /// <summary>How many rows the shell is drawn in: short enough that six posts run well past the foot of the page.</summary>
    private const int Height = 16;

    /// <summary>The terminal row the page begins on, under the content panel's top edge.</summary>
    private const int PageTop = 1;

    /// <summary>The terminal row the page ends on, over the content panel's bottom edge and the status row.</summary>
    private const int PageBottom = Height - 3;

    /// <summary>A row inside the content panel, below its top edge.</summary>
    private const int Inside = 3;

    /// <summary>Twenty rows of a post's text with no reference in them.</summary>
    private static readonly string Filler = string.Concat(
        Enumerable.Range(1, 20).Select(line => $"Line {line} of the fleece\n"));

    /// <summary>
    ///     A post far taller than the terminal, with a hashtag at its top, two side by side in its middle and one at
    ///     its foot.
    /// </summary>
    private static readonly string Tall = $"Shearing #one\n{Filler}Carding #two and #three\n{Filler}Spinning #four";

    /// <summary>
    ///     With the pick off the page, <c>→</c> picks the first post whose top is on it and brackets that post's first
    ///     reference, and <c>←</c> its last, in the one press.
    /// </summary>
    [Theory]
    [InlineData(true, "#a110")]
    [InlineData(false, "#b110")]
    public async Task OffThePageAnArrowReclaimsAndEntersInOnePress(bool right, string entered)
    {
        using var drawn = await Feed(Six(Tagged));

        LeaveThePickBelowThePage(drawn);

        drawn.Press(right ? Key.CursorRight : Key.CursorLeft);

        Assert.Equal("110", drawn.Shell.Screen.Picked?.Id);
        Assert.Equal(entered, drawn.Shell.Screen.Reference?.Text);
        Assert.InRange(RowOf(drawn, $"‹{entered}›"), PageTop, PageBottom);
    }

    /// <summary>
    ///     Mid-walk, with the post walked inside scrolled off the page, <c>→</c> reclaims and enters the reclaimed
    ///     post's references rather than stepping on inside the one nobody can see.
    /// </summary>
    [Fact]
    public async Task MidWalkAnArrowReclaimsRatherThanSteppingOn()
    {
        using var drawn = await Feed(Six(Tagged));

        drawn.Press(Key.End);
        drawn.Press(Key.CursorRight);
        Assert.Equal("#a660", drawn.Shell.Screen.Reference?.Text);

        WheelToTheTop(drawn);

        drawn.Press(Key.CursorRight);

        Assert.Equal("110", drawn.Shell.Screen.Picked?.Id);
        Assert.Equal("#a110", drawn.Shell.Screen.Reference?.Text);
    }

    /// <summary>Where the reclaimed post has no references, the press only reclaims and no bracket is drawn.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AReclaimedPostWithNoReferencesIsOnlyPicked(bool right)
    {
        var posts = Six(Tagged);
        posts[0] = APost.With(id: "110", content: "Nothing to walk to");

        using var drawn = await Feed(posts);

        LeaveThePickBelowThePage(drawn);

        drawn.Press(right ? Key.CursorRight : Key.CursorLeft);

        Assert.Equal("110", drawn.Shell.Screen.Picked?.Id);
        Assert.Null(drawn.Shell.Screen.Reference);
        Assert.DoesNotContain(drawn.Rows(), row => row.Contains('‹'));
    }

    /// <summary>
    ///     On a tall post scrolled so its first reference is above the page, <c>→</c> enters at the first reference on
    ///     the page and <c>←</c> at the last one on it, rather than at one scrolled off.
    /// </summary>
    [Theory]
    [InlineData(true, "#two")]
    [InlineData(false, "#three")]
    public async Task OnATallPostAnArrowEntersAtTheReferencesOnThePage(bool right, string entered)
    {
        using var drawn = await Feed([APost.With(id: "110", content: Tall), APost.With(id: "220")]);

        WheelUntil(drawn, "Carding", PageTop + 4);
        var top = drawn.Content.Top;

        drawn.Press(right ? Key.CursorRight : Key.CursorLeft);

        Assert.Equal("110", drawn.Shell.Screen.Picked?.Id);
        Assert.Equal(entered, drawn.Shell.Screen.Reference?.Text);
        Assert.Equal(top, drawn.Content.Top);
    }

    /// <summary>
    ///     Where none of the pick's references are on the page, <c>→</c> enters at the first and <c>←</c> at the last,
    ///     and the page moves to show it.
    /// </summary>
    [Theory]
    [InlineData(true, "#one")]
    [InlineData(false, "#four")]
    public async Task WithNoneOnThePageAnArrowEntersAtAnEndAndBringsItOn(bool right, string entered)
    {
        using var drawn = await Feed([APost.With(id: "110", content: Tall), APost.With(id: "220")]);

        WheelUntil(drawn, "Line 2 of", PageTop);
        Assert.DoesNotContain(drawn.Rows(), row => row.Contains('#'));

        drawn.Press(right ? Key.CursorRight : Key.CursorLeft);

        Assert.Equal(entered, drawn.Shell.Screen.Reference?.Text);
        Assert.InRange(RowOf(drawn, $"‹{entered}›"), PageTop, PageBottom);
    }

    /// <summary>Once inside, further presses walk on through every one of the post's references and clamp at the ends.</summary>
    [Fact]
    public async Task OnceInsideTheArrowsWalkEveryReferenceAndClamp()
    {
        using var drawn = await Feed([APost.With(id: "110", content: Tall), APost.With(id: "220")]);

        WheelUntil(drawn, "Carding", PageTop + 4);

        string[] walked =
        [
            .. new[] { Key.CursorRight, Key.CursorRight, Key.CursorRight, Key.CursorRight }.Select(Walked),
            .. new[] { Key.CursorLeft, Key.CursorLeft, Key.CursorLeft, Key.CursorLeft }.Select(Walked),
        ];

        Assert.Equal(["#two", "#three", "#four", "#four", "#three", "#two", "#one", "#one"], walked);

        string Walked(Key key)
        {
            drawn.Press(key);

            return drawn.Shell.Screen.Reference?.Text ?? "";
        }
    }

    /// <summary>
    ///     The status row's <c>←/→ reference</c> hint describes the pick, not what the arrows would reclaim: a pick with
    ///     no references scrolled off a page of posts with them offers no walk until an arrow has reclaimed one.
    /// </summary>
    [Fact]
    public async Task TheHintDescribesThePick()
    {
        var posts = Six(Tagged);
        posts[5] = APost.With(id: "660", content: "Nothing to walk to");

        using var drawn = await Feed(posts, width: 160);

        LeaveThePickBelowThePage(drawn);

        Assert.DoesNotContain("←/→", drawn.Rows()[^1]);

        drawn.Press(Key.CursorRight);

        Assert.Contains("←/→", drawn.Rows()[^1]);
    }

    /// <summary>A post reading two hashtags named for it.</summary>
    private static Post Tagged(string id) => APost.With(id: id, content: $"Shearing #a{id} and #b{id}");

    /// <summary>Six posts, from 110 up to 660, each made by <paramref name="post" />.</summary>
    private static Post[] Six(Func<string, Post> post) =>
        [.. new[] { "110", "220", "330", "440", "550", "660" }.Select(post)];

    /// <summary>The shell drawn short over a feed of <paramref name="posts" />.</summary>
    private static Task<DrawnShell> Feed(Post[] posts, int width = 80) =>
        DrawnShell.Of(width, Height, Themes.Plain, new AShell { Timelines = FakeTimelineReader.Holding(posts) });

    /// <summary>The last thing picked, and the page wheeled back up to the top, leaving the pick well below it.</summary>
    private static void LeaveThePickBelowThePage(DrawnShell drawn)
    {
        drawn.Press(Key.End);
        WheelToTheTop(drawn);
    }

    /// <summary>The page wheeled up until it begins on the first row.</summary>
    private static void WheelToTheTop(DrawnShell drawn)
    {
        while (drawn.Content.Top > 0)
        {
            drawn.Wheel(OverContent, Inside, down: false);
        }
    }

    /// <summary>The page wheeled down a notch at a time until <paramref name="text" /> is drawn on the terminal row <paramref name="row" />.</summary>
    private static void WheelUntil(DrawnShell drawn, string text, int row)
    {
        for (var notch = 0; Array.FindIndex(drawn.Rows(), line => line.Contains(text, StringComparison.Ordinal)) is var at
             && (at < 0 || at > row); notch++)
        {
            Assert.True(notch < 100, $"No page draws {text} on row {row}:\n{string.Join('\n', drawn.Rows())}");

            drawn.Wheel(OverContent, Inside);
        }
    }
}
