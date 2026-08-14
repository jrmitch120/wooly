using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Wooly.Core.Posts;
using Wooly.Core.Search;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;

namespace Wooly.Tests.Tui;

/// <summary>
///     Walking the references inside a post with <c>←</c> and <c>→</c> (#83): what enters the walk, where it stops,
///     what lets it go, and what a picked reference looks like once it is picked.
/// </summary>
/// <remarks>
///     Asked of the window where the question is which key does it, and of the screen where the question is what the
///     screen then holds — the same split <see cref="ShellKeyTests" /> makes, and for the same reason: a test that
///     says <c>WalkReference(1)</c> proves nothing about what a reader pressed.
/// </remarks>
public class ReferenceWalkTests
{
    /// <summary>A post with one of each kind of reference in it, in the order they are walked.</summary>
    private const string Said = "Thanks @maria@fosstodon.org — notes at https://example.com/notes #dotnet";

    /// <summary>Every screen with a post to walk the references of, which is the breadth the contract asks for.</summary>
    public static TheoryData<string> Screens =>
    [
        "feed",
        "post",
        "conversation",
        "search",
        "notifications",
        "messages",
        "account",
    ];

    /// <summary>The three references in <see cref="Said" />, in the order they are walked.</summary>
    private const string First = "@maria@fosstodon.org";

    /// <inheritdoc cref="First" />
    private const string Last = "#dotnet";

    /// <summary><c>→</c> enters at the first reference and <c>←</c> at the last.</summary>
    [Theory]
    [InlineData(true, First)]
    [InlineData(false, Last)]
    public async Task TheArrowsEnterAtOneEndOrTheOther(bool forwards, string expected)
    {
        var (window, shell, host) = await Opened();

        using (window)
        {
            Assert.Null(shell.Screen.Reference);

            window.NewKeyDownEvent(forwards ? Key.CursorRight : Key.CursorLeft);

            Assert.Equal(expected, shell.Screen.Reference?.Text);
        }
    }

    /// <summary>
    ///     And further motion in the same direction at either end clamps rather than wrapping, which is the convention
    ///     <see cref="Picked{T}" /> already walks a list by.
    /// </summary>
    [Theory]
    [InlineData(true, Last)]
    [InlineData(false, First)]
    public async Task TheArrowsClampAtTheEndsRatherThanWrapping(bool forwards, string expected)
    {
        var (window, shell, host) = await Opened();

        using (window)
        {
            for (var pressed = 0; pressed < 6; pressed++)
            {
                window.NewKeyDownEvent(forwards ? Key.CursorRight : Key.CursorLeft);
            }

            Assert.Equal(expected, shell.Screen.Reference?.Text);
        }
    }

    /// <summary>
    ///     <c>esc</c> is up one level of whichever kind of level is open: the first press lets the pick go and the
    ///     next pops the screen (<c>docs/tui-shell.md</c>).
    /// </summary>
    [Fact]
    public async Task Esc_LetsThePickGoBeforeItPopsTheScreen()
    {
        var (window, shell, host) = await Opened();

        using (window)
        {
            window.NewKeyDownEvent(Key.Enter);

            host.Drain();

            Assert.Equal(2, shell.Depth);

            window.NewKeyDownEvent(Key.CursorRight);

            Assert.Equal(First, shell.Screen.Reference?.Text);

            window.NewKeyDownEvent(Key.Esc);

            Assert.Null(shell.Screen.Reference);
            Assert.Equal(2, shell.Depth);

            window.NewKeyDownEvent(Key.Esc);

            Assert.Equal(1, shell.Depth);
        }
    }

    /// <summary>
    ///     <c>j</c> and <c>k</c> let it go too, since the reader has left the post it was inside — where the arrows
    ///     that scroll the screen leave it alone, a reference pick living inside the picked post rather than on a row.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WalkingOffThePostLetsThePickGoAndScrollingDoesNot(bool down)
    {
        var (window, shell, host) = await Opened();

        using (window)
        {
            window.NewKeyDownEvent(Key.CursorRight);
            window.NewKeyDownEvent(down ? Key.CursorDown : Key.CursorUp);

            Assert.Equal(First, shell.Screen.Reference?.Text);

            window.NewKeyDownEvent(down ? Key.K : Key.J);

            Assert.Null(shell.Screen.Reference);
        }
    }

    /// <summary>
    ///     A screen with no references on it leaves the arrows alone, which is what keeps them moving the caret in the
    ///     compose editor rather than being swallowed by a walk with nothing to walk.
    /// </summary>
    [Fact]
    public async Task AScreenWithNoReferencesLeavesTheArrowsToWhateverElseWantsThem()
    {
        var built = new AShell { Timelines = FakeTimelineReader.Holding(APost.With(content: "Hello world")) };
        var shell = await built.Opened();

        Assert.False(shell.WalkReference(1));

        shell.Compose();

        Assert.False(shell.WalkReference(1));
        Assert.Null(shell.Screen.Reference);
    }

    /// <summary>
    ///     The same three references, and the same walk, on every screen a post is drawn on — the breadth
    ///     <see cref="PostKeys.OnAPost" /> already has, plus the conversations list, where the post is the last thing
    ///     said in the conversation picked out.
    /// </summary>
    [Theory]
    [MemberData(nameof(Screens))]
    public void EveryScreenWithAPostOnItWalksTheReferencesInIt(string kind)
    {
        var screen = Of(kind);

        Assert.Equal(3, screen.References.Count);

        Assert.True(screen.WalkReference(1));
        Assert.Equal(First, screen.Reference?.Text);

        screen.WalkReference(1);

        Assert.Equal("https://example.com/notes", screen.Reference?.Text);

        Assert.True(screen.ClearReference());
        Assert.Null(screen.Reference);
    }

    /// <summary>And it is drawn as picked on every one of them: brackets around it, in their own role.</summary>
    [Theory]
    [MemberData(nameof(Screens))]
    public void EveryScreenDrawsThePickedReferenceInBrackets(string kind)
    {
        var screen = Of(kind);

        Assert.DoesNotContain(Drawn(screen), span => span.Role == Role.ReferencePicked);

        screen.WalkReference(1);

        var spans = Drawn(screen);

        Assert.Equal(2, spans.Count(span => span.Role == Role.ReferencePicked));
        Assert.Contains(screen.Lines(61, AShell.Now), line => line.Text.Contains("‹@maria@fosstodon.org›"));
    }

    /// <summary>
    ///     The pick is on the post being read and on no other, so a feed of posts that all say the same thing brackets
    ///     one of them.
    /// </summary>
    [Fact]
    public void OnlyThePickedPostShowsThePickedReference()
    {
        var feed = new FeedScreen(
            new Destination(DestinationKind.Home, "Home"),
            [APost.With(id: "1", content: Said), APost.With(id: "2", content: Said)]);

        feed.WalkReference(1);

        Assert.Single(feed.Lines(61, AShell.Now), line => line.Text.Contains("‹@maria@fosstodon.org›"));
    }

    /// <summary>
    ///     Text still behind a content warning has nothing to walk: the brackets would be behind the warning too, so a
    ///     pick there would be one nobody could see.
    /// </summary>
    [Fact]
    public void TextBehindAContentWarningHasNoReferencesUntilItIsShown()
    {
        var feed = new FeedScreen(
            new Destination(DestinationKind.Home, "Home"),
            [APost.With(content: Said, contentWarning: "spoilers")]);

        Assert.Empty(feed.References);
        Assert.False(feed.WalkReference(1));

        feed.Reveal();

        Assert.Equal(3, feed.References.Count);
        Assert.True(feed.WalkReference(1));
    }

    /// <summary>
    ///     The status row swaps to the reference-mode keys while one is picked, ahead of the screen's own — and
    ///     <c>⏎</c> is said once, as what it does to the reference rather than to the post it is inside.
    /// </summary>
    [Fact]
    public void TheStatusRowSwapsToTheReferenceKeysWhileOneIsPicked()
    {
        var feed = new FeedScreen(new Destination(DestinationKind.Home, "Home"), [APost.With(content: Said)]);

        Assert.DoesNotContain("←/→", Status(feed));

        feed.WalkReference(1);

        var row = Status(feed);

        Assert.StartsWith(" ←/→:reference · ⏎:open · esc:back", row, StringComparison.Ordinal);
        Assert.DoesNotContain("⏎:read", row);

        // The screen's own keys are still behind them, cut off at the right the same way they always are.
        Assert.Contains("j/k:post", row);

        feed.ClearReference();

        Assert.Contains("⏎:read", Status(feed));
    }

    /// <summary>The spans of every row of a screen, which is what a role is asserted against.</summary>
    private static IReadOnlyList<Span> Drawn(Screen screen) =>
        [.. screen.Lines(61, AShell.Now).SelectMany(line => line.Spans)];

    /// <summary>The status row as it reads at 80 columns, which is the width the contract is written for.</summary>
    private static string Status(Screen screen) =>
        ChromeLines.Status(screen.Keys, notice: null, noticeIsError: false, asking: null, 80).Text;

    /// <summary>One of each screen a post is drawn on, each with the same post picked out.</summary>
    private static Screen Of(string kind)
    {
        var post = APost.With(content: Said);

        switch (kind)
        {
            case "post":
                return new PostScreen(post, [APost.With(id: "2")]);

            case "conversation":
                return new ConversationScreen(
                    AConversation.Thread(AConversation.With(), post with { Visibility = PostVisibility.Direct }));

            case "search":
                var search = new SearchScreen();

                search.Found("sheep", new SearchResults { Posts = [post] });

                return search;

            case "notifications":
                return new NotificationsScreen([ANotification.With(post: post)]);

            case "messages":
                return new DirectMessagesScreen([AConversation.With(latest: post)]);

            case "account":
                return new AccountScreen(AnAccount.With(), [post]);

            default:
                return new FeedScreen(new Destination(DestinationKind.Home, "Home"), [post]);
        }
    }

    /// <summary>A shell on a feed whose first post carries all three kinds of reference, ready for keys.</summary>
    private static async Task<(ShellWindow Window, Wooly.Tui.Shell.Shell Shell, FakeShellHost Host)> Opened()
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "110", content: Said),
                APost.With(id: "220", content: Said)),
        };

        var shell = await built.Opened();

        var window = new ShellWindow(shell, Themes.Plain, built.Clock, () => { }, FakePictures.DrawingNothing())
        {
            Width = 80,
            Height = 20,
        };

        window.Layout();

        return (window, shell, built.Host);
    }
}
