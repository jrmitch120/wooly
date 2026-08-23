using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;

namespace Wooly.Tests.Tui;

/// <summary>
///     The keys a window still keeps for itself, pressed on a real one: the movements that walk the page rather than
///     the list, the key that ends the run, and the bargain by which an unused key falls through to whatever else
///     wants it.
/// </summary>
/// <remarks>
///     Worth pinning here rather than at the shell, where a test says <c>Walk(1)</c> and proves nothing about what a
///     reader pressed. <c>k</c> being the next post and <c>j</c> the one before it is the opposite way round from vim
///     (<c>docs/tui-shell.md</c>), which is exactly the kind of thing that gets quietly reversed.
///     <para>
///         Everything a window hands straight on is asserted in <see cref="KeymapTests" /> instead, which needs no
///         terminal at all — what is left here is what a page, an editor widget and a run loop are needed to see.
///     </para>
/// </remarks>
public class ShellKeyTests
{
    [Fact]
    public async Task K_WalksToTheNextPostAndJToTheOneBeforeIt()
    {
        var (window, shell) = await Opened();

        using (window)
        {
            window.NewKeyDownEvent(Key.K);

            Assert.Equal("220", shell.Screen.Picked?.Id);

            window.NewKeyDownEvent(Key.K);

            Assert.Equal("330", shell.Screen.Picked?.Id);

            window.NewKeyDownEvent(Key.J);

            Assert.Equal("220", shell.Screen.Picked?.Id);
        }
    }

    /// <summary>The arrows are the other movement: the screen walks and the selection stays where it was put.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TheArrowsLeaveTheSelectionAlone(bool down)
    {
        var (window, shell) = await Opened();

        using (window)
        {
            window.NewKeyDownEvent(Key.K);

            for (var pressed = 0; pressed < 30; pressed++)
            {
                window.NewKeyDownEvent(down ? Key.CursorDown : Key.CursorUp);
            }

            Assert.Equal("220", shell.Screen.Picked?.Id);
        }
    }

    /// <summary>
    ///     <c>PgDn</c> is a screenful of the same movement, so it leaves the selection alone too — it used to walk it
    ///     ten posts, which is several screens on a feed with pictures on it.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ThePageKeysLeaveTheSelectionAlone(bool down)
    {
        var (window, shell) = await Opened();

        using (window)
        {
            window.NewKeyDownEvent(Key.K);
            window.NewKeyDownEvent(down ? Key.PageDown : Key.PageUp);

            Assert.Equal("220", shell.Screen.Picked?.Id);
        }
    }

    /// <summary><c>Home</c> and <c>End</c> are the ends of the list, which are things rather than places.</summary>
    [Theory]
    [InlineData(true, "440")]
    [InlineData(false, "110")]
    public async Task HomeAndEndPickOutTheFirstPostAndTheLast(bool end, string expected)
    {
        var (window, shell) = await Opened();

        using (window)
        {
            window.NewKeyDownEvent(end ? Key.End : Key.Home);

            Assert.Equal(expected, shell.Screen.Picked?.Id);
        }
    }

    /// <summary>
    ///     And the two together: arrows far enough to lose the selection, then <c>k</c>, which takes back the post on
    ///     screen rather than moving on from the one that is not.
    /// </summary>
    [Fact]
    public async Task K_ReclaimsThePostOnScreenAfterTheArrowsHaveWalkedPastIt()
    {
        var (window, shell) = await Opened();

        using (window)
        {
            // Far enough down that the first post has no row left on the page.
            for (var pressed = 0; pressed < 30; pressed++)
            {
                window.NewKeyDownEvent(Key.CursorDown);
            }

            window.NewKeyDownEvent(Key.K);

            // The last post, which is what is on screen down there — not the second, which is where a step from the
            // selection the arrows left behind would have landed.
            Assert.Equal("440", shell.Screen.Picked?.Id);
        }
    }

    /// <summary>
    ///     The digits address the answers of the picked post's poll directly: <c>1</c>-<c>9</c> then <c>0</c>, so that
    ///     ten of them are reachable along one row of keys and the tenth is where a person's own counting puts it
    ///     (<c>docs/tui-shell.md</c>, #87).
    /// </summary>
    [Theory]
    [InlineData('1', 0)]
    [InlineData('3', 2)]
    [InlineData('9', 8)]
    [InlineData('0', 9)]
    public async Task TheDigitsToggleTheAnswerTheyAddress(char digit, int answer)
    {
        var (window, shell) = await Polled();

        using (window)
        {
            window.NewKeyDownEvent(new Key(digit));

            Assert.Equal([answer], shell.Screen.Chosen);
        }
    }

    /// <summary><c>v</c> puts the question, and nothing is cast until it has been answered (story 43).</summary>
    [Fact]
    public async Task V_AsksBeforeCastingTheToggledVote()
    {
        var (window, shell) = await Polled();

        using (window)
        {
            window.NewKeyDownEvent(new Key('2'));
            window.NewKeyDownEvent(Key.V);

            Assert.NotNull(shell.Asking);
            Assert.Equal("vote", shell.Asking.Going);
        }
    }

    /// <summary><c>ctrl-q</c> is the one key that ends the run, and the application owns the loop it ends.</summary>
    [Fact]
    public async Task CtrlQ_EndsTheRun()
    {
        var quits = 0;
        var built = new AShell();
        var shell = await built.Opened();

        using var window = new ShellWindow(
            shell,
            Themes.Plain,
            built.Clock,
            () => quits++,
            FakePictures.DrawingNothing());

        Assert.True(window.NewKeyDownEvent(Key.Q.WithCtrl));
        Assert.Equal(1, quits);
    }

    /// <summary>
    ///     Half the fall-through bargain: a digit addresses a poll answer where there is one to address, and where
    ///     there is not the window leaves the key to whatever else wants it rather than swallowing it (#87).
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ADigitIsConsumedOnlyWhereThereIsAnAnswerToToggle(bool poll)
    {
        var (window, shell) = poll ? await Polled() : await Opened();

        using (window)
        {
            Assert.Equal(poll, window.NewKeyDownEvent(new Key('3')));
            Assert.Equal(poll ? [2] : Array.Empty<int>(), shell.Screen.Chosen.Order());
        }
    }

    /// <summary>
    ///     The other half, and the one it exists for: <c>←</c> and <c>→</c> walk the references inside the picked post
    ///     where there are any, and on a compose screen — which has none — they are the editor's own caret keys (#83).
    /// </summary>
    [Fact]
    public async Task TheArrowsReachTheComposeEditorWhereThereIsNoReferenceToWalk()
    {
        var (window, shell) = await Opened(
            APost.With(account: "ben@hachyderm.io", content: "Read https://example.com/sheep"));

        using (window)
        {
            window.NewKeyDownEvent(Key.CursorRight);

            Assert.NotNull(shell.Screen.Reference);

            shell.Reply();
            window.Layout();

            // A reply opens on the handle it is answering, with the caret after it — so there is somewhere to the
            // left of the caret for the key to move it to.
            var editor = window.SubViews.OfType<ComposeEditor>().Single();
            var at = editor.CurrentColumn;

            Assert.Equal("@ben@hachyderm.io ".Length, at);

            window.NewKeyDownEvent(Key.CursorLeft);

            // Nothing was walked — a compose screen has no post picked out on it — and the caret moved instead.
            Assert.Null(shell.Screen.Reference);
            Assert.Equal(at - 1, editor.CurrentColumn);
        }
    }

    /// <summary>
    ///     The one exception the contract makes to the frame: a prompt taking letters takes <c>/</c> and <c>?</c> too.
    ///     A web address and a question are both things somebody is entitled to search for, and a prompt that could
    ///     not take a slash would refuse the query most likely to be pasted into it.
    /// </summary>
    /// <remarks>
    ///     Asserted on a window rather than at the keymap, because that is where the exception lives: the keymap says
    ///     <c>/</c> is search and <c>?</c> is the keys, on every screen and this one too, and the window takes them
    ///     first while the screen is taking letters.
    /// </remarks>
    [Fact]
    public async Task APromptTakingLettersTakesSlashAndQuestionToo()
    {
        var built = new AShell();
        var shell = await built.Opened();

        using var window = new ShellWindow(
            shell,
            Themes.Plain,
            built.Clock,
            () => { },
            FakePictures.DrawingNothing());

        shell.Search();
        built.Host.Drain();

        foreach (var letter in "who/what?")
        {
            window.NewKeyDownEvent(new Key(letter));
        }

        var search = Assert.IsType<SearchScreen>(shell.Screen);

        Assert.Equal("who/what?", search.Query);

        // Neither key acted: no fresh prompt was started over the one being typed into, and no keymap screen was
        // pushed on top of it.
        Assert.Equal(1, shell.Depth);
    }

    /// <summary>And the rest of the frame still means what it means everywhere, on that very prompt.</summary>
    [Fact]
    public async Task EveryOtherFrameKeyStillMeansWhatItDoesWhileAPromptIsTakingLetters()
    {
        var quits = 0;
        var built = new AShell();
        var shell = await built.Opened();

        using var window = new ShellWindow(
            shell,
            Themes.Plain,
            built.Clock,
            () => quits++,
            FakePictures.DrawingNothing());

        shell.Search();
        built.Host.Drain();

        var was = shell.Rail.Cursor;

        window.NewKeyDownEvent(Key.Tab);

        Assert.NotEqual(was, shell.Rail.Cursor);

        window.NewKeyDownEvent(Key.Q.WithCtrl);

        Assert.Equal(1, quits);
    }

    /// <summary>A shell showing one post with a ten-answer poll on it, laid out and ready for keys.</summary>
    private static async Task<(ShellWindow Window, Wooly.Tui.Shell.Shell Shell)> Polled()
    {
        var answers = Enumerable.Range(1, 10).Select(at => APost.AnAnswer($"Answer {at}", at)).ToList();

        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "110", poll: APost.APoll(options: answers))),
        };

        var shell = await built.Opened();

        var window = new ShellWindow(shell, Themes.Plain, built.Clock, () => { }, FakePictures.DrawingNothing())
        {
            Width = 80,
            Height = 20,
        };

        window.Layout();

        return (window, shell);
    }

    /// <summary>A shell of four posts on a window with room for eighteen rows, laid out and ready for keys.</summary>
    /// <remarks>
    ///     Room enough for two and a half of them, which is what these tests need: a window is laid out here but never
    ///     painted, so the scroll stays where it started and a selection walked past the foot of the page is one
    ///     <c>j</c> reclaims rather than steps from. A post takes seven rows since #77 gave it a two-row byline, a
    ///     footer blank and a rule.
    /// </remarks>
    /// <param name="only">
    ///     One post in place of the four, for a test about what is on a post rather than about walking between them.
    /// </param>
    private static async Task<(ShellWindow Window, Wooly.Tui.Shell.Shell Shell)> Opened(Post? only = null)
    {
        var built = new AShell
        {
            Timelines = only is not null
                ? FakeTimelineReader.Holding(only)
                : FakeTimelineReader.Holding(
                    APost.With(id: "110"),
                    APost.With(id: "220"),
                    APost.With(id: "330"),
                    APost.With(id: "440")),
        };

        var shell = await built.Opened();

        var window = new ShellWindow(shell, Themes.Plain, built.Clock, () => { }, FakePictures.DrawingNothing())
        {
            Width = 80,
            Height = 20,
        };

        window.Layout();

        return (window, shell);
    }
}
