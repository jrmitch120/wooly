using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Wooly.Core.Posts;
using Wooly.Core.Relationships;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Views;

/// <summary>
///     The shell laid out on a terminal: the rail down the left, the breadcrumb above the content, the content, and
///     the status row along the bottom (<c>docs/tui-shell.md</c>). Everything it draws it asks the shell for, and
///     everything a key means it asks <see cref="Keymap" /> — so this file has no idea what a boost is.
/// </summary>
/// <remarks>
///     It names exactly one screen type, and never to decide what a key means (#147): a
///     <see cref="ComposeScreen" /> is the one screen with a widget of its own laid over the content region, so where
///     that widget starts, whether it has focus and what text it opens with are this window's questions about its own
///     furniture. Everything else it knows about screens it knows as <c>Screen</c>.
/// </remarks>
internal sealed class ShellWindow : Window
{
    /// <summary>
    ///     How far one press of <c>↓</c> or <c>↑</c> moves the screen. A wheel notch's worth rather than a single row:
    ///     a picture is sixteen rows, so a row a press is sixteen presses to get past one — and it is a post nobody
    ///     could reach the foot of that these keys exist for (#51).
    /// </summary>
    private const int RowsAPress = 3;

    /// <summary>The first row under the breadcrumb, where the content region and anything laid over it begin.</summary>
    private const int ContentTop = 1;

    /// <summary>
    ///     What the content region answers to among its siblings — four regions are painted the same way and only
    ///     this one shows posts, so it is the one worth being able to name.
    /// </summary>
    internal const string ContentId = "content";

    /// <summary>
    ///     The fewest rows the editor is ever left with, however much of what is being answered wants to sit above it
    ///     (<see cref="EditorTop" />). Three: a line being written, and one either side of it to see.
    /// </summary>
    private const int LeastEditorRows = 3;

    private readonly PaintedView _content;
    private readonly ComposeEditor _editor;
    private readonly Shell.Shell _shell;
    private readonly TimeProvider _clock;
    private readonly Action _quit;

    /// <summary>
    ///     Which screen the content region is showing, so that a screen being replaced can be told apart from the same
    ///     one changing. The scroll is settled from what the incoming screen remembers on the first — nothing, on one
    ///     nobody has read yet, which is the top — and left alone on the second.
    /// </summary>
    /// <remarks>
    ///     Set from the shell it is built over rather than left empty until the first change, because the region draws
    ///     <see cref="Shell.Shell.Screen" /> from the moment it exists — a window built over a shell that has already
    ///     opened is showing that screen, and calling it "none" makes the very next replacement look like the first.
    /// </remarks>
    private Screen? _showing;


    /// <param name="quit">
    ///     What <c>ctrl-q</c> does. Passed in rather than reached for, because the application is the thing that owns
    ///     the run loop and this window is one of the things running in it.
    /// </param>
    /// <param name="pictures">Where a drawn attachment's pixels come from.</param>
    /// <param name="hideDrawnCaption">
    ///     The reader's <c>hide_drawn_caption</c> preference (#71): whether a picture's caption hides once it is
    ///     actually drawn.
    /// </param>
    public ShellWindow(
        Shell.Shell shell,
        ITheme theme,
        TimeProvider clock,
        Action quit,
        IPictures pictures,
        bool hideDrawnCaption = false)
    {
        _shell = shell;
        _clock = clock;
        _quit = quit;

        // No border and no title: the contract gives the frame two rows, and both of them say something. A box around
        // the outside would cost two more and say nothing.
        BorderStyle = Terminal.Gui.Drawing.LineStyle.None;

        var rail = new PaintedView(theme, (_, height) => RailLines.Of(shell.Rail, shell.Quota, height))
        {
            X = 0,
            Y = 0,
            Width = RailLines.Width,
            Height = Dim.Fill(1),
            CanFocus = false,
        };

        var breadcrumb = new PaintedView(theme, (width, _) =>
            [ChromeLines.Breadcrumb(shell.Breadcrumb, shell.Fetching, width)])
        {
            X = RailLines.Width + 1,
            Y = 0,
            Width = Dim.Fill(),
            Height = 1,
            CanFocus = false,
        };

        // The one region that shows posts, so the one region with pictures to draw in place (docs/tui-shell.md) — and
        // the one that scrolls, which is why the arrow keys below are handed to it and to nothing else. It stops
        // scrolling while a post is being written, which Refresh settles.
        _content = new PaintedView(
            theme,
            (width, _) => shell.Screen.Lines(width, clock.GetUtcNow(), pictures, hideDrawnCaption),
            pictures)
        {
            Id = ContentId,
            X = RailLines.Width + 1,
            Y = ContentTop,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            CanFocus = false,
            Scrolls = true,
        };

        _editor = new ComposeEditor(() => _ = Send(), () => shell.Back(), shell.WriteWarning)
        {
            X = RailLines.Width + 1,
            // A reply's "answering" block is painted on _content, which this sits in front of and exactly the same
            // size as (below) — so without this, the block is never seen: the editor is opaque and covers it on every
            // frame it is visible. Dim.Fill(1) starting from here still reaches the same floor it always did.
            // The second argument is the view the function is handed (Pos.Func's own words: "the view where the data
            // will be retrieved") — _content, because it is _content's own width the block is wrapped against, and
            // measuring anything else means deriving that width a second way. Omitting it defaults to null, which is
            // what the first attempt at this did: EditorTop got no Viewport to measure, so Y silently stayed 1 forever.
            Y = Pos.Func(EditorTop, _content),
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            Visible = false,
            WordWrap = true,
        };

        var status = new PaintedView(theme, (width, _) =>
            [ChromeLines.Status(shell.Keys, shell.Notice, shell.NoticeIsError, shell.Asking, width)])
        {
            X = 0,
            Y = Pos.AnchorEnd(1),
            Width = Dim.Fill(),
            Height = 1,
            CanFocus = false,
        };

        Add(rail, breadcrumb, _content, _editor, status);

        _showing = shell.Screen;

        shell.Changed += Refresh;
    }

    /// <summary>
    ///     Every key the shell answers to, in three steps and no bindings of its own: what a terminal sent becomes a
    ///     <see cref="ShellKey" />, <see cref="Keymap" /> says what that means on the screen on top, and the verb is
    ///     carried out — here where it needs a terminal, and by the shell where it does not (#147).
    /// </summary>
    protected override bool OnKeyDown(Key key)
    {
        // A confirmation is the only thing on screen worth answering, so nothing else is listened to while one is up
        // (story 43). Anything that is not the agreeing key is a no.
        if (_shell.Asking is { } asking)
        {
            _ = _shell.Answer(agreed: key == Key.Y && asking.Confirm == "y");

            return true;
        }

        // A prompt taking a query takes the letters too, so that searching for "backfeed" is not a boost, an author,
        // a compose and two more besides. Ahead of the keymap rather than inside it: this is the one place a key the
        // contract has settled means something else, and what it means instead is a letter rather than another verb.
        // Which screens do that is a fact about the screen, not a mode kept here.
        if (_shell.Screen.IsTyping && Typing(key))
        {
            return true;
        }

        return ShellKeys.Of(key) is { } pressed && Do(pressed) || base.OnKeyDown(key);
    }

    /// <summary>
    ///     What <paramref name="pressed" /> means here, done. The verbs that need a terminal are taken first and the
    ///     rest are the shell's, which is the whole of the division: this window knows how tall the page is, where the
    ///     rows have been scrolled to, what the editor widget is holding, and who owns the run loop — and nothing else
    ///     about what any of it is for.
    /// </summary>
    /// <returns>
    ///     Whether the press was used, which is <see cref="Shell.Shell.Do" />'s answer for everything below: an unused
    ///     <c>←</c>, <c>→</c> or digit falls through to whatever else wants it, the compose editor above all (#83,
    ///     #87).
    /// </returns>
    private bool Do(ShellKey pressed)
    {
        switch (Keymap.Means(pressed, _shell.Screen))
        {
            case Verb.Quit:
                _quit();

                return true;

            case Verb.NextPost:
                Walk(1);

                return true;

            case Verb.PreviousPost:
                Walk(-1);

                return true;

            case Verb.FirstPost:
                Jump(int.MinValue);

                return true;

            case Verb.LastPost:
                Jump(int.MaxValue);

                return true;

            case Verb.ScrollDown:
                Scrolled(RowsAPress);

                return true;

            case Verb.ScrollUp:
                Scrolled(-RowsAPress);

                return true;

            case Verb.PageDown:
                Turned(1);

                return true;

            case Verb.PageUp:
                Turned(-1);

                return true;

            case Verb.Send:
                _ = Send();

                return true;

            case var verb:
                return _shell.Do(verb, Keymap.Answer(pressed));
        }
    }

    /// <summary>
    ///     <c>j</c> and <c>k</c>: the selection walks, and the screen goes back to following it. Whether this press
    ///     moves or reclaims is the content region's to answer, since it is the only thing that knows what is on
    ///     screen — this window translates keys and leaves what they mean to <see cref="Keymap" /> (ADR-0014, #147).
    /// </summary>
    private void Walk(int by)
    {
        _shell.Walk(by, _content.Reclaimable);
        _content.Follow();
    }

    /// <summary>
    ///     <c>↓</c> and <c>↑</c>: the screen moves and the selection stays where it is.
    /// </summary>
    /// <remarks>
    ///     The whole frame is redrawn, not the content region alone. Every other key that changes what is on screen
    ///     ends at the shell's <c>Changed</c>, which redraws everything; these two change nothing the shell knows
    ///     about, so they are the only keys that could have redrawn one region — and the column between the rail and
    ///     the content belongs to no region. It is the window's own background, so a region redrawn on its own leaves
    ///     it holding whatever the terminal last put there, which beside a picture is part of the picture.
    /// </remarks>
    private void Scrolled(int rows)
    {
        _content.Step(rows);

        SetNeedsDraw();
    }

    /// <summary>
    ///     <c>PgUp</c> and <c>PgDn</c>: the same movement a screenful at a time, rather than a run of posts. They walk
    ///     the screen because that is what a page is — a reader asking for the next page is asking about what they are
    ///     looking at, not about how many posts happen to be on it.
    /// </summary>
    private void Turned(int pages)
    {
        _content.Turn(pages);

        SetNeedsDraw();
    }

    /// <summary>
    ///     <c>Home</c> and <c>End</c>: the first post and the last. These move the selection rather than the screen,
    ///     since the ends of a list are things rather than places, and nothing is reclaimed — a reader asking for the
    ///     top of the list is not asking about the page they were on.
    /// </summary>
    private void Jump(int by)
    {
        _shell.Move(by);
        _content.Follow();
    }

    /// <summary>
    ///     A key going into a prompt rather than at the shell: a printable character, or a backspace taking one back
    ///     out. Anything that is not printable — <c>⏎</c>, <c>esc</c>, <c>tab</c>, <c>ctrl-q</c> — falls through,
    ///     because those mean the same thing while typing as they do everywhere else.
    /// </summary>
    /// <remarks>
    ///     Two of the frame's own keys do not: <c>/</c> and <c>?</c> are typed here rather than acted on, because a
    ///     web address and a question are both things somebody is entitled to search for, and a prompt that could not
    ///     take a slash would be a prompt that refuses the one query most likely to be pasted into it. It is the only
    ///     place in the shell where a frame key means something else, and the screen says so on the status row
    ///     (<c>docs/tui-shell.md</c>).
    /// </remarks>
    private bool Typing(Key key)
    {
        if (key == Key.Backspace)
        {
            _shell.Backspace();

            return true;
        }

        if (key.IsCtrl || key.IsAlt || key.AsRune.Value < ' ')
        {
            return false;
        }

        _shell.Type((char)key.AsRune.Value);

        return true;
    }

    /// <summary>
    ///     Where the editor starts: the top of the content region, plus however many rows the screen underneath wants
    ///     for what it is answering — the block <see cref="ComposeScreen.AnsweringHeight" /> counts, painted on
    ///     <see cref="_content" /> and otherwise hidden by the editor sitting on top of it at the same position.
    /// </summary>
    /// <remarks>
    ///     Never so far down that there is no editor left. The block is up to five rows and ADR-0015 priced the
    ///     editor's share of a 24-row terminal at more than that, but a terminal can be any size, and
    ///     <c>Dim.Fill(1)</c> from a row past the bottom is an editor nobody can type in. Pushed off the foot, what
    ///     goes is the tail of what is being answered rather than the room to answer it.
    /// </remarks>
    /// <param name="content">
    ///     <see cref="_content" />, handed over by <c>Pos.Func</c>. Its viewport is the region the block is wrapped
    ///     against and shares a foot with the editor, so both the width to measure at and the room to leave come off
    ///     the one view — rather than off this window, whose own viewport counts the rail and would have to have it
    ///     taken back off.
    /// </param>
    private int EditorTop(View? content)
    {
        var width = content?.Viewport.Width ?? 0;
        var height = content?.Viewport.Height ?? 0;

        if (width <= 0 || _shell.Screen is not ComposeScreen compose)
        {
            return ContentTop;
        }

        // The editor runs from here to the same foot _content does, so whatever is spent above it comes straight off
        // its own height — which makes the room to leave a subtraction rather than a second layout.
        //
        // The warning field is the last row to give way rather than the first: it is a row the reader types into, and
        // one they cannot see is worse than a quote of what is being answered that stops early.
        var room = Math.Max(0, height - LeastEditorRows - compose.WarningHeight);
        var answering = Math.Min(compose.AnsweringHeight(width), room);

        return ContentTop + answering + compose.WarningHeight;
    }

    private async Task Send()
    {
        if (_shell.Screen is ComposeScreen compose)
        {
            // The editor is where the text was typed and the screen is where it lives; this is the one moment the two
            // have to agree.
            compose.Text = _editor.Text;
        }

        await _shell.Send();
    }

    /// <summary>
    ///     Puts the editor in front of the content while a post is being written, and takes it away again. Which of
    ///     the two is showing is a fact about the stack, not a mode this view keeps of its own.
    /// </summary>
    /// <remarks>
    ///     Also where a screen being replaced is noticed, which is what settles the scroll: pushing a screen and
    ///     arriving at a destination both mean different rows, and an offset the arrows made on the last lot says
    ///     nothing about this one — so each screen is left where it was and resumed where it was, and a screen nobody
    ///     has read yet resumes at the top (#133).
    ///     <para>
    ///         A refresh is a replacement like any other here, which is what puts the reader at the top of a freshly
    ///         read list — the thing <c>g</c> is for (#84). It builds a new screen, so what it resumes is nought.
    ///     </para>
    ///     <para>
    ///         The row is read off the region rather than kept here, and it is the one the last frame settled: the
    ///         offset is worked out inside the draw, and a screen is only ever replaced between frames.
    ///     </para>
    /// </remarks>
    private void Refresh()
    {
        if (!ReferenceEquals(_showing, _shell.Screen))
        {
            if (_showing is { } left)
            {
                left.Began = _content.Top;
                left.Followed = _content.Following;
            }

            _showing = _shell.Screen;

            _content.Resume(_showing.Began, _showing.Followed);
        }

        var composing = _shell.Screen is ComposeScreen;

        // Nothing below the block being answered is readable while composing — the editor is in front of all of it —
        // so scrolling the region can only take that block away and put rows nobody can place in its stead. ADR-0015
        // priced this as "you cannot scroll the timeline while composing" and left it to the editor to make true,
        // which it does not: a key the editor declines at the end of its own text still reaches this window, and a
        // screen with nothing picked out on it is one Scroll.To never scrolls back.
        _content.Scrolls = !composing;

        // The caret is where the typing is going, which while the warning has it is the row above: the editor keeps
        // its text and its place and gives up focus, so the letters fall through this window's own typing path into
        // the field, and the terminal's cursor is not left blinking in a body nobody is writing in.
        //
        // The two being equal is what says they are out of step — the editor may be focused exactly when the warning
        // is not being written — so this runs on the frames that change one and passes over every other.
        if (_shell.Screen is ComposeScreen compose && _editor.CanFocus == compose.WritingTheWarning)
        {
            _editor.CanFocus = !compose.WritingTheWarning;

            if (_editor.CanFocus && _editor.Visible)
            {
                _editor.SetFocus();
            }
        }

        if (composing && !_editor.Visible)
        {
            _editor.Text = ((ComposeScreen)_shell.Screen).Text;
            _editor.Visible = true;
            _editor.SetFocus();

            // After whatever the screen opened with rather than in front of it: an editor opened on `@maria ` or on
            // a post being edited puts the caret where the reader's next word goes, which is the end of what is
            // already written (ADR-0013, #85). A caret left at nought types into somebody's name.
            _editor.MoveEnd();

            // Y is Pos.Func(EditorTop), read afresh only on a layout pass — and this screen's "answering" block may
            // be a different height than the last one that pushed the editor open.
            _editor.SetNeedsLayout();
        }
        else if (!composing && _editor.Visible)
        {
            _editor.Visible = false;
            SetFocus();
        }

        SetNeedsDraw();
    }
}
