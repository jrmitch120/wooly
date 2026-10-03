using System.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;

namespace Wooly.Tests.Tui;

/// <summary>
///     Where the editor sits while a reply is being written. ADR-0015 has said since it was written that a reply
///     "draws what it is answering at the top ... and then the editor underneath", but the editor is a separate view
///     laid over the one that paints those rows — so until it was moved down off them, it covered every one of them
///     and the block was never seen (#82).
/// </summary>
/// <remarks>
///     Worth pinning because the failure is silent both ways. The block being painted is not the block being visible,
///     so a test of the rows alone passes over an editor drawn on top of them; and the position is a
///     <c>Pos.Func</c> read only on a layout pass, which the first attempt at this got wrong by leaving <c>Y</c> at 1
///     with nothing to say so.
/// </remarks>
public class ShellComposeLayoutTests
{
    /// <summary>
    ///     The content panel's inside at an 80-column terminal: what the rail leaves, less the panel's two sides
    ///     (RailLines, ADR-0021).
    /// </summary>
    private const int ContentWidth = 60;

    /// <summary>
    ///     The editor starts below what is being answered rather than on top of it — two rows here: the label and the
    ///     one row "Hello world" wraps to — and below the warning band under those (#123), which is the field and the
    ///     blank above it.
    /// </summary>
    [Fact]
    public async Task Reply_StartsTheEditorBelowWhatIsBeingAnsweredAndTheWarningField()
    {
        var (window, editor, compose) = await Replying();

        using (window)
        {
            Assert.Equal(2, compose.AnsweringHeight(ContentWidth));
            Assert.Equal(2, compose.WarningHeight);
            Assert.Equal(5, editor.Frame.Y);
        }
    }

    /// <summary>
    ///     And a post with no reply behind it starts two rows down: nothing above it to clear but the warning band,
    ///     which both of the composes that publish a post carry (#139) and which is the field and the blank over it.
    /// </summary>
    [Fact]
    public async Task Post_StartsTheEditorUnderTheWarningField()
    {
        var (window, shell) = await Opened(height: 20);

        using (window)
        {
            shell.Compose();
            window.Layout();

            Assert.Equal(3, Editor(window).Frame.Y);
        }
    }

    /// <summary>
    ///     An edit starts it in the same place, on a band of the same two rows — held blank while an edit had no
    ///     warning to write (#142) and holding a field of its own since #140, which is the point of having priced the
    ///     band the same on all three: giving an edit its field moved nothing.
    /// </summary>
    [Fact]
    public async Task Edit_StartsTheEditorWhereAFreshPostDoes()
    {
        var (window, shell) = await Opened(height: 20, post: APost.With(id: "220", account: "jeff@mastodon.social"));

        using (window)
        {
            shell.Compose();
            window.Layout();

            var composing = Editor(window).Frame.Y;

            shell.Back();
            shell.Edit();
            window.Layout();

            var compose = Assert.IsType<ComposeScreen>(shell.Screen);

            var rows = compose.Lines(new Drawing(ContentWidth, AShell.Now));

            Assert.Equal(ComposeFor.Edit, compose.Purpose);
            Assert.Equal(string.Empty, rows[0].Text);
            Assert.Equal("⚠ no content warning", rows[1].Text);
            Assert.Equal(composing, Editor(window).Frame.Y);
        }
    }

    /// <summary>
    ///     A terminal too short for both keeps the editor and gives up the tail of what is being answered — the block
    ///     wants three rows and there is only room for one, because an editor pushed to the foot is one nobody can
    ///     type in.
    /// </summary>
    /// <remarks>
    ///     Eight rows: the content panel's top and bottom edges and the status row leave the inside of the panel
    ///     five, of which the editor keeps three and the warning field one. The field is the last row to give way
    ///     rather than the first — it is a row the reader types into, and one they cannot see is worse than a quote
    ///     that stops early. The panel's bottom edge costs the row the blank one under the breadcrumb did (#216,
    ///     ADR-0021), so the smallest terminal this holds on is the same.
    /// </remarks>
    [Fact]
    public async Task Reply_NeverPushesTheEditorPastTheRoomLeftToTypeIn()
    {
        var (window, editor, compose) = await Replying(height: 8);

        using (window)
        {
            Assert.Equal(2, compose.AnsweringHeight(ContentWidth));
            Assert.Equal(2, editor.Frame.Y - 1);
            Assert.Equal(3, editor.Frame.Height);
        }
    }

    /// <summary>
    ///     What is being answered stays put. The region it is painted on is the one the arrows scroll, and everything
    ///     below the block on it is behind the editor — so a scroll here takes the block off the top and puts rows in
    ///     its place that are half of a post nobody can see the rest of.
    /// </summary>
    /// <remarks>
    ///     It never came back, either: a compose screen has no selection on it, so <c>Scroll.To</c> takes its "nothing
    ///     picked out — stay exactly where the arrows left us" branch and the offset stands for as long as the screen
    ///     does. ADR-0015 said the timeline does not scroll while composing; this is what says it.
    /// </remarks>
    [Fact]
    public async Task Reply_DoesNotLetTheArrowsScrollWhatIsBeingAnswered()
    {
        var (window, editor, _) = await Replying();

        using (window)
        {
            var content = window.SubViews.OfType<PaintedView>().Single(view => view.Id == ShellWindow.ContentId);

            Assert.False(content.Scrolls);

            for (var pressed = 0; pressed < 10; pressed++)
            {
                window.NewKeyDownEvent(Key.CursorDown);
            }

            Assert.Null(content.Reclaimable);
            Assert.Equal(5, editor.Frame.Y);
        }
    }

    /// <summary>
    ///     Three rows of what was said, and blank ones are not among them (#141). A post's paragraphs arrive as blank
    ///     lines, so a quote that took its first three rows in order spent one of them on a gap — two rows of words
    ///     where there was room for three, and, with the warning field underneath, a hole the reader could see.
    /// </summary>
    [Fact]
    public async Task Reply_QuotesThreeRowsOfWordsFromAPostOfSeveralParagraphs()
    {
        var paragraphs = APost.With(
            id: "220",
            account: "ben@hachyderm.io",
            content: "The first thing said.\n\nThe second thing said.\n\nThe third thing said.");

        var shell = new AShell { Timelines = FakeTimelineReader.Holding(paragraphs) };
        var opened = await shell.Opened();

        opened.Reply();

        var compose = Assert.IsType<ComposeScreen>(opened.Screen);
        var quoted = compose.Lines(new Drawing(ContentWidth, AShell.Now))
                            .Take(compose.AnsweringHeight(ContentWidth))
                            .ToList();

        Assert.Equal(
            [
                "↳ answering @ben@hachyderm.io",
                "  The first thing said.",
                "  The second thing said.",
                "  The third thing said.",
            ],
            quoted.Select(line => line.Text));
    }

    /// <summary>A blank row stands between the quote and the warning field, which is what a compose has too (#143).</summary>
    [Fact]
    public async Task Reply_PutsABlankRowAboveTheWarningField()
    {
        var (window, _, compose) = await Replying();

        using (window)
        {
            var lines = compose.Lines(new Drawing(ContentWidth, AShell.Now));

            Assert.Equal(2, compose.AnsweringHeight(ContentWidth));
            Assert.Equal("↳ answering @ben@hachyderm.io", lines[0].Text);
            Assert.Equal("  Hello world", lines[1].Text);
            Assert.Equal(string.Empty, lines[2].Text);
            Assert.Equal("⚠ no content warning", lines[3].Text);
        }
    }

    /// <summary>
    ///     And the band reads the same on all three, once whatever a screen has above it is taken off: a blank, then
    ///     the field. The blank belongs to the warning rather than to the reply block, which is the whole of what
    ///     puts it on the two screens that have no block at all (#143) — and since #140 the field on the edit is a
    ///     field, where it was a row held blank for one.
    /// </summary>
    [Theory]
    [InlineData("compose")]
    [InlineData("reply")]
    [InlineData("edit")]
    public async Task Warning_IsSpacedTheSameOnEveryCompose(string opening)
    {
        var (window, shell) = await Opened(height: 20, post: APost.With(id: "220", account: "jeff@mastodon.social"));

        using (window)
        {
            switch (opening)
            {
                case "compose":
                    shell.Compose();

                    break;
                case "reply":
                    shell.Reply();

                    break;
                default:
                    shell.Edit();

                    break;
            }

            var compose = Assert.IsType<ComposeScreen>(shell.Screen);
            var band = compose.Lines(new Drawing(ContentWidth, AShell.Now))
                              .Skip(compose.AnsweringHeight(ContentWidth))
                              .Take(compose.WarningHeight)
                              .Select(line => line.Text);

            Assert.Equal([string.Empty, "⚠ no content warning"], band);
        }
    }

    /// <summary>
    ///     <c>ctrl-w</c> hands the keys to the warning field above the editor and takes them back again (#123). The
    ///     editor keeps its text and its place and gives up focus, so what is typed lands in the field rather than in
    ///     the post — and the terminal's own cursor is not left blinking in a body nobody is writing in.
    /// </summary>
    [Fact]
    public async Task Warning_TakesWhatIsTypedWhileCtrlWHasIt()
    {
        var (window, editor, compose) = await Replying();

        using (window)
        {
            Assert.True(editor.CanFocus);

            window.NewKeyDownEvent(Key.W.WithCtrl);

            Assert.True(compose.WritingTheWarning);
            Assert.False(editor.CanFocus);
            Assert.False(editor.HasFocus);

            window.NewKeyDownEvent(Key.C);
            window.NewKeyDownEvent(Key.W);

            Assert.Equal("cw", compose.Warning);
            Assert.Equal("@ben@hachyderm.io ", compose.Text);

            window.NewKeyDownEvent(Key.W.WithCtrl);

            Assert.False(compose.WritingTheWarning);
            Assert.True(editor.CanFocus);
        }
    }

    /// <summary>
    ///     A compose thrown away while its warning had the keys gives them back: the next one opens with the editor
    ///     focused, the way every compose before it did. Worth pinning because the failure is silent — an editor whose
    ///     focus was refused still draws, still sits in the right place, and takes not one letter.
    /// </summary>
    [Fact]
    public async Task Warning_LeavesTheNextComposeFocusedOnItsEditor()
    {
        var (window, shell) = await Opened(height: 20);

        using (window)
        {
            shell.Reply();
            window.NewKeyDownEvent(Key.W.WithCtrl);
            shell.Back();

            shell.Reply();
            window.Layout();

            var editor = Editor(window);

            Assert.True(editor.CanFocus);
            Assert.True(editor.HasFocus);
            Assert.False(Assert.IsType<ComposeScreen>(shell.Screen).WritingTheWarning);
        }
    }

    /// <summary>
    ///     And the letters that were going into the field stop going anywhere near the post: <c>c</c> is a compose key
    ///     everywhere else in the shell, and pressing it while writing a warning types a letter rather than opening a
    ///     second screen over the first.
    /// </summary>
    [Fact]
    public async Task Warning_TakesTheKeysThatWouldOtherwiseActOnTheScreen()
    {
        var (window, shell) = await Opened(height: 20);

        using (window)
        {
            shell.Reply();
            window.Layout();

            var compose = Assert.IsType<ComposeScreen>(shell.Screen);

            window.NewKeyDownEvent(Key.W.WithCtrl);
            window.NewKeyDownEvent(Key.C);

            Assert.Same(compose, shell.Screen);
            Assert.Equal("c", compose.Warning);
        }
    }

    /// <summary>
    ///     The screen says where its editor goes inside the content panel's viewport (#315): under the reply block and
    ///     the warning band, across the whole width, and down to the foot.
    /// </summary>
    [Theory]
    [InlineData("compose", 60, 17, 2)]
    [InlineData("reply", 60, 17, 4)]
    [InlineData("edit", 60, 17, 2)]
    [InlineData("reply", 40, 30, 4)]
    public async Task EditorAt_SitsUnderWhatIsAboveItAndRunsToTheFoot(string opening, int width, int height, int top)
    {
        var (window, shell) = await Opened(height: 20, post: APost.With(id: "220", account: "jeff@mastodon.social"));

        using (window)
        {
            var compose = Opening(shell, opening);

            Assert.Equal(
                new Rectangle(0, top, width, height - top),
                compose.EditorAt(new Size(width, height)));
        }
    }

    /// <summary>
    ///     On a viewport too short for everything, the quote gives way first and the editor keeps its three rows — and
    ///     where there are not even those, it keeps what there is rather than a height below nothing.
    /// </summary>
    [Theory]
    [InlineData(5, 2, 3)]
    [InlineData(3, 2, 1)]
    [InlineData(1, 2, 0)]
    public async Task EditorAt_GivesUpTheQuoteBeforeTheEditor(int height, int top, int rows)
    {
        var (window, _, compose) = await Replying();

        using (window)
        {
            Assert.Equal(new Rectangle(0, top, ContentWidth, rows), compose.EditorAt(new Size(ContentWidth, height)));
        }
    }

    /// <summary>
    ///     The window lays the editor exactly where the screen says, offset by where the content panel's viewport sits
    ///     in the window — with or without the rail beside it.
    /// </summary>
    [Theory]
    [InlineData("compose")]
    [InlineData("reply")]
    [InlineData("edit")]
    public async Task Window_LaysTheEditorWhereTheScreenSays(string opening)
    {
        var (window, shell) = await Opened(height: 20, post: APost.With(id: "220", account: "jeff@mastodon.social"));

        using (window)
        {
            var compose = Opening(shell, opening);

            window.Layout();

            var content = window.SubViews.OfType<PaintedView>().Single(view => view.Id == ShellWindow.ContentId);
            var at = compose.EditorAt(content.Viewport.Size);
            var origin = content.Frame.Location + new Size(1, 1);

            Assert.Equal(at with { Location = at.Location + new Size(origin) }, Editor(window).Frame);
        }
    }

    /// <summary>
    ///     The screen's text follows the editor on every edit rather than only at <c>ctrl-s</c> (#315), so anything
    ///     reading it mid-draft sees what has been typed so far.
    /// </summary>
    [Fact]
    public async Task Text_FollowsTheEditorAsItIsTyped()
    {
        var (window, _, compose) = await Replying();

        using (window)
        {
            window.NewKeyDownEvent(Key.H);
            window.NewKeyDownEvent(Key.I);

            Assert.Equal("@ben@hachyderm.io hi", compose.Text);

            window.NewKeyDownEvent(Key.Backspace);

            Assert.Equal("@ben@hachyderm.io h", compose.Text);
        }
    }

    /// <summary>
    ///     Not only typing: a paste, which reaches the editor as one string inserted at the caret rather than as keys,
    ///     and an undo are edits too, and the screen follows both. How much of a paste one undo takes back is the
    ///     editor's own business, so what is pinned is that the screen ends up where the editor does.
    /// </summary>
    [Fact]
    public async Task Text_FollowsAPasteAndItsUndo()
    {
        var (window, editor, compose) = await Replying();

        using (window)
        {
            editor.InsertText("thanks!");

            Assert.Equal("@ben@hachyderm.io thanks!", compose.Text);

            window.NewKeyDownEvent(Key.Z.WithCtrl);

            Assert.NotEqual("@ben@hachyderm.io thanks!", editor.Text);
            Assert.Equal(editor.Text, compose.Text);
        }
    }

    /// <summary>
    ///     And <c>ctrl-s</c> sends what was typed into the editor, now that the screen learns it as it is typed rather
    ///     than being handed it at the moment of sending.
    /// </summary>
    [Fact]
    public async Task Send_PublishesWhatWasTypedIntoTheEditor()
    {
        var built = new AShell { Timelines = FakeTimelineReader.Holding(APost.With(id: "220")) };
        var shell = await built.Opened();

        using var window = new ShellWindow(shell, Themes.Plain, built.Clock, () => { }, FakePictures.DrawingNothing())
        {
            Width = 80,
            Height = 20,
        };

        window.Layout();
        shell.Compose();
        window.Layout();

        window.NewKeyDownEvent(Key.H);
        window.NewKeyDownEvent(Key.I);
        window.NewKeyDownEvent(Key.S.WithCtrl);
        built.Host.Drain();

        Assert.Equal("hi", Assert.Single(built.Author.Published).Draft.Text);
    }

    /// <summary>And it scrolls again the moment the reply is thrown away, since the feed is back underneath.</summary>
    [Fact]
    public async Task Back_LetsTheFeedScrollAgainOnceTheReplyIsGone()
    {
        var (window, shell) = await Opened(height: 20);

        using (window)
        {
            var content = window.SubViews.OfType<PaintedView>().Single(view => view.Id == ShellWindow.ContentId);

            shell.Reply();

            Assert.False(content.Scrolls);

            shell.Back();

            Assert.True(content.Scrolls);
        }
    }

    /// <summary>
    ///     A window showing one post by somebody else, laid out and ready — or by whoever <paramref name="post" />
    ///     names, for the one test that needs the profile's own post to have something to edit.
    /// </summary>
    private static async Task<(ShellWindow Window, Wooly.Tui.Shell.Shell Shell)> Opened(int height, Post? post = null)
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(post ?? APost.With(id: "220", account: "ben@hachyderm.io")),
        };

        var shell = await built.Opened();

        var window = new ShellWindow(shell, Themes.Plain, built.Clock, () => { }, FakePictures.DrawingNothing())
        {
            Width = 80,
            Height = height,
        };

        window.Layout();

        return (window, shell);
    }

    /// <summary>The same, with a reply to that post open in the editor.</summary>
    private static async Task<(ShellWindow Window, ComposeEditor Editor, ComposeScreen Compose)> Replying(
        int height = 20)
    {
        var (window, shell) = await Opened(height);

        shell.Reply();
        window.Layout();

        return (window, Editor(window), (ComposeScreen)shell.Screen);
    }

    /// <summary>Opens a compose of the kind <paramref name="opening" /> names on the post the shell is showing.</summary>
    private static ComposeScreen Opening(Wooly.Tui.Shell.Shell shell, string opening)
    {
        switch (opening)
        {
            case "compose":
                shell.Compose();

                break;
            case "reply":
                shell.Reply();

                break;
            default:
                shell.Edit();

                break;
        }

        return Assert.IsType<ComposeScreen>(shell.Screen);
    }

    private static ComposeEditor Editor(View window) => window.SubViews.OfType<ComposeEditor>().Single();
}
