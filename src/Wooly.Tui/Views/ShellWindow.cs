using System.Diagnostics;
using System.Drawing;
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
///     The shell laid out on a terminal: the rail down the left, the content panel titled with the breadcrumb beside
///     it, and the status row along the bottom (<c>docs/tui-shell.md</c>). Everything it draws it asks the shell for, and
///     everything a key means it asks <see cref="Keymap" /> — so this file has no idea what a boost is.
/// </summary>
/// <remarks>
///     It names exactly one screen type, and never to decide what a key means (#147): a
///     <see cref="ComposeScreen" /> is the one screen with widgets of its own laid over the content region — the editor
///     and the warning field — so laying them where the screen says they go, which has focus and what text they open
///     with are this window's questions about its own furniture. Everything else it knows about screens it knows as
///     <c>Screen</c>.
/// </remarks>
internal sealed class ShellWindow : Window
{
    /// <summary>
    ///     How far one press of <c>↓</c> or <c>↑</c> moves the screen. A wheel notch's worth rather than a single row:
    ///     a picture is sixteen rows, so a row a press is sixteen presses to get past one — and it is a post nobody
    ///     could reach the foot of that these keys exist for (#51).
    /// </summary>
    private const int RowsAPress = 3;

    /// <summary>
    ///     How far one wheel notch moves the screen: a row, the finest step a terminal has. A trackpad sends a stream of
    ///     small events where a wheel sends a click, and at <see cref="RowsAPress" /> each one a slow scroll read as a run
    ///     of lurches (#292). Three notches are as far as one press.
    /// </summary>
    private const int RowsANotch = 1;

    /// <summary>
    ///     The columns and rows a panel's edge takes off each side of what is inside it: the one-cell ring
    ///     <see cref="PaintedView" /> lays a frame on, which the editor laid over the content panel has to sit inside.
    /// </summary>
    private const int Edge = 1;

    /// <summary>
    ///     What the content region answers to among its siblings — four regions are painted the same way and only
    ///     this one shows posts, so it is the one worth being able to name.
    /// </summary>
    /// <remarks>
    ///     Its <c>Frame</c> is the whole panel and its <c>Viewport</c> the inside of it, which is the room every screen
    ///     is drawn in.
    /// </remarks>
    internal const string ContentId = "content";

    private readonly PaintedView _content;
    private readonly ComposeEditor _editor;

    /// <summary>The content warning over the post, laid over its header's value column (#320).</summary>
    private readonly ComposeWarningField _warning;

    /// <summary>Who the post goes to, a row of radio buttons laid over To's value column (#338).</summary>
    private readonly ComposeToField _to;

    /// <summary>What language the post is in, a field laid over Lang's value column (#340).</summary>
    private readonly ComposeLangField _lang;

    /// <summary>The people to mention, hung under the @-word being typed in <see cref="_editor" /> (#318).</summary>
    private readonly MentionList _mentions;

    /// <summary>The languages, hung under <see cref="_lang" /> (#340).</summary>
    private readonly LanguageList _languages;

    /// <summary>Where the terminal's mouse events arrive, once the window is running; asked ahead of the views.</summary>
    private IMouse? _mouse;

    /// <summary>The rail, which <see cref="Railed" /> takes away and puts back.</summary>
    private readonly PaintedView _rail;

    /// <summary>
    ///     The content panel's top edge, drawn again over the edge the panel draws there — the one row a tick of the
    ///     fetch mark redraws (<see cref="Shell.Shell.Ticked" />).
    /// </summary>
    private readonly PaintedView _title;
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

    /// <summary>Whether the rail is laid out, which it is as built.</summary>
    private bool _railed = true;

    /// <summary>
    ///     Whether the last click was spent declining an open question, which spends the double click it turns out to be
    ///     the first half of (#291).
    /// </summary>
    private bool _clickDeclinedQuestion;

    /// <param name="quit">
    ///     What <c>ctrl-q</c> does. Passed in rather than reached for, because the application is the thing that owns
    ///     the run loop and this window is one of the things running in it.
    /// </param>
    /// <param name="pictures">Where a drawn attachment's pixels come from.</param>
    /// <param name="hideDrawnCaption">
    ///     The reader's <c>hide_drawn_caption</c> preference (#71): whether a picture's caption hides once it is
    ///     actually drawn.
    /// </param>
    /// <param name="placeholders">
    ///     What a Kitty terminal holds, for drawing a picture as placeholder cells (ADR-0022), or
    ///     <see langword="null" /> to draw every picture through a box.
    /// </param>
    public ShellWindow(
        Shell.Shell shell,
        ITheme theme,
        TimeProvider clock,
        Action quit,
        IPictures pictures,
        bool hideDrawnCaption = false,
        Placeholders? placeholders = null)
    {
        _shell = shell;
        _clock = clock;
        _quit = quit;

        // No border and no title of Terminal.Gui's: the panels are this client's own, drawn in roles (ADR-0021), and a
        // box around the outside would cost two rows and two columns and say nothing.
        BorderStyle = Terminal.Gui.Drawing.LineStyle.None;

        // The cells no region covers, which are the window's own. There are none now that the panels meet the rail and
        // the status row edge to edge, but every region clears itself in the theme's page before it draws, so a cell
        // inside one is themed by construction — and a cell inside none keeps whatever Terminal.Gui's default scheme
        // put there, a grey the theme never chose. A gutter column and a blank row both did, until #216 and #271.
        //
        // Role.Body's attribute is the page, and it is the borrow PaintedView already makes to clear a row: a blank
        // cell shows a background, and the page is what every role without one of its own is drawn on. Nothing here
        // constructs a colour, which is the rule this is keeping rather than breaking (ADR-0014).
        SetScheme(new Terminal.Gui.Drawing.Scheme(theme.For(Role.Body)));

        var rail = _rail = new PaintedView(theme, (_, height) => RailLines.Of(shell.Rail, shell.Quota, height, shell.Instance, theme.DrawsColour))
        {
            X = 0,
            Y = 0,
            Width = RailLines.Width,
            Height = Dim.Fill(1),
            CanFocus = false,
        };

        // The one region that shows posts, so the one region with pictures to draw in place (docs/tui-shell.md) — and
        // the one that scrolls, which is why the arrow keys below are handed to it and to nothing else. It stops
        // scrolling while a post is being written, which Refresh settles.
        //
        // It is a panel titled with the breadcrumb (ADR-0021), its edges laid round its viewport, so the width every
        // screen is drawn at and every picture's box are the inside of it: 58 columns at an 80-column terminal.
        _content = new PaintedView(
            theme,
            (width, height) => shell.Screen.Lines(
                new Drawing(width, clock.GetUtcNow(), pictures, hideDrawnCaption, height)),
            pictures,
            // No rows of the panel's own: the view paints only the frame's edges, round the screen's rows.
            (width, height) => Panel.Framed(
                ChromeLines.Breadcrumb(shell.Crumbs, shell.SpinnerFrame, width),
                [],
                width,
                height,
                active: true),
            placeholders)
        {
            Id = ContentId,
            X = RailLines.Width,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            CanFocus = false,
            Scrolls = true,
        };

        // The same top edge again, as a row of its own laid over the panel's, so that a tick of the fetch mark has one
        // row to redraw rather than the panel — which places every picture on it each time it is drawn, two and a half
        // times a second for as long as anything is in flight, for one spinner frame (#217). Both draw the edge from
        // the one function, so whichever was drawn last, it says the same thing.
        var title = _title = new PaintedView(
            theme,
            (width, _) => [ChromeLines.Breadcrumb(shell.Crumbs, shell.SpinnerFrame, width)])
        {
            X = RailLines.Width,
            Y = 0,
            Width = Dim.Fill(),
            Height = 1,
            CanFocus = false,
        };

        _editor = new ComposeEditor(
            theme,
            ComposeScreen.EmptyPostHint,
            () => _ = shell.Send(),
            () => shell.Back(),
            shell.WriteWarning,
            () => shell.WalkField(-1))
        {
            // Wherever the compose screen says, inside the content panel's viewport (#315): its headers and hairlines
            // (#317) are painted on _content, which this sits in front of, so the screen that paints them is the one
            // that knows how far down the editor has to start for them to be seen. Every one is read
            // off _content, which Pos.Func and Dim.Func hand back as "the view where the data will be retrieved" — the
            // panel's own viewport is the room the screen lays itself out in, and its frame is where that room sits.
            X = Pos.Func(content => ViewportOrigin(content).X + EditorAt(content).X, _content),
            Y = Pos.Func(content => ViewportOrigin(content).Y + EditorAt(content).Y, _content),
            Width = Dim.Func(content => EditorAt(content).Width, _content),
            Height = Dim.Func(content => EditorAt(content).Height, _content),
            Visible = false,
            WordWrap = true,

            // tab is the frame's on every screen, compose included (docs/tui-shell.md, ADR-0024): it moves the rail's
            // cursor rather than putting a tab into the post.
            TabKeyAddsTab = false,
        };

        // The screen's text follows the editor on every edit rather than only at ctrl-s, so whatever reads it while a
        // post is being written — a count, a list of people to mention — sees what has been typed so far.
        _editor.ContentsChanged += (_, _) => _shell.Rewrite(_editor.Text);

        _warning = new ComposeWarningField(
            theme,
            () => (_shell.Screen as ComposeScreen)?.WarningHint ?? string.Empty,
            () => _ = shell.Send(),
            () => shell.Back(),
            shell.WriteWarning)
        {
            // Wherever the compose screen says, as for the editor: its header's value column, on whichever row the
            // headers above it leave it.
            X = Pos.Func(content => ViewportOrigin(content).X + WarningAt(content).X, _content),
            Y = Pos.Func(content => ViewportOrigin(content).Y + WarningAt(content).Y, _content),
            Width = Dim.Func(content => WarningAt(content).Width, _content),
            Height = Dim.Func(content => WarningAt(content).Height, _content),
            Visible = false,
        };

        // The same for the warning, and the count is painted on the content region, which has to be told.
        _warning.ValueChanged += (_, _) =>
        {
            _shell.RewriteWarning(_warning.Text);
            _content.SetNeedsDraw();
        };

        _to = new ComposeToField(
            theme,
            room => (_shell.Screen as ComposeScreen)?.ToSpans(room) ?? [],
            shell.ClickTo)
        {
            // Wherever the compose screen says, as for the warning: To's value column, under From.
            X = Pos.Func(content => ViewportOrigin(content).X + ToAt(content).X, _content),
            Y = Pos.Func(content => ViewportOrigin(content).Y + ToAt(content).Y, _content),
            Width = Dim.Func(content => ToAt(content).Width, _content),
            Height = Dim.Func(content => ToAt(content).Height, _content),
            Visible = false,
        };

        _lang = new ComposeLangField(
            theme,
            () => (_shell.Screen as ComposeScreen)?.LanguageHint ?? string.Empty,
            () => _ = shell.Send(),
            () => shell.Back(),
            shell.WriteWarning)
        {
            // Wherever the compose screen says, as for To: Lang's value column, under To — nowhere where a short
            // terminal has given the row up.
            X = Pos.Func(content => ViewportOrigin(content).X + LangAt(content).X, _content),
            Y = Pos.Func(content => ViewportOrigin(content).Y + LangAt(content).Y, _content),
            Width = Dim.Func(content => LangAt(content).Width, _content),
            Height = Dim.Func(content => LangAt(content).Height, _content),
            Visible = false,
        };

        // A click into any field moves the typing there: where the typing is is one fact, the screen's, and whichever
        // way it moved the screen is told so that the status row, the header's mark and the hint keep up.
        _to.HasFocusChanged += (_, focus) => TypingMoved(focus.NewValue, ComposeField.To);
        _lang.HasFocusChanged += (_, focus) => TypingMoved(focus.NewValue, ComposeField.Lang);
        _warning.HasFocusChanged += (_, focus) => TypingMoved(focus.NewValue, ComposeField.Warning);
        _editor.HasFocusChanged += (_, focus) => TypingMoved(focus.NewValue, ComposeField.Post);

        var status = new PaintedView(theme, (width, _) =>
            [ChromeLines.Status(shell.Keys, shell.Notice, shell.NoticeIsError, shell.Asking, width)])
        {
            X = 0,
            Y = Pos.AnchorEnd(1),
            Width = Dim.Fill(),
            Height = 1,
            CanFocus = false,
        };

        _mentions = new MentionList(theme, shell, _editor);
        _editor.Ahead = _mentions.Took;

        _languages = new LanguageList(theme, shell, _lang, _content);

        Add(rail, _content, title, _editor, _to, _lang, _warning, _mentions.View, _languages.View, status);

        // A click outside an open list closes it whichever view it lands on, the editor's caret above all, so it is
        // asked about where the terminal's mouse events arrive — ahead of every view under the pointer (#335).
        Initialized += (_, _) =>
        {
            _mouse = App?.Mouse;

            if (_mouse is not null)
            {
                _mouse.MouseEvent += AheadOfTheViews;
            }
        };

        _showing = shell.Screen;

        Railed();

        shell.Changed += Refresh;

        // A tick of the fetch mark redraws the row it is on and nothing else. Changed would redraw the whole window,
        // and the content region re-places every picture on it each frame — two and a half times a second, for as
        // long as anything is in flight, for one spinner frame (#217).
        shell.Ticked += title.SetNeedsDraw;
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
    ///     Every mouse event the shell answers to, and where Terminal.Gui's stop, as <see cref="OnKeyDown" /> is for
    ///     keys. The window works out which panel the pointer is over and turns the gesture into a move the keys already
    ///     make; what that move means is the shell's (#286, <c>docs/tui-shell.md</c>).
    /// </summary>
    /// <remarks>
    ///     Reached by what the views under the pointer left: the compose editor takes its own wheel and clicks before
    ///     they bubble here, which is the whole of how a draft scrolls and places its caret.
    /// </remarks>
    protected override bool OnMouseEvent(Mouse mouse)
    {
        if (ShellKeys.Of(mouse) is { } clicked)
        {
            return RightClicked(clicked);
        }

        if (mouse.Flags.HasFlag(MouseFlags.LeftButtonClicked))
        {
            return Clicked(mouse.ScreenPosition) || base.OnMouseEvent(mouse);
        }

        if (mouse.Flags.HasFlag(MouseFlags.LeftButtonDoubleClicked))
        {
            return DoubleClicked(mouse.ScreenPosition) || base.OnMouseEvent(mouse);
        }

        if (!_content.FrameToScreen().Contains(mouse.ScreenPosition) || Notched(mouse) is not { } pressed)
        {
            return base.OnMouseEvent(mouse);
        }

        // A notch is the arrow it stands for, open question and all: anything but the agreeing key declines one, so a
        // notch declines it too and scrolls nothing behind it (story 43).
        if (Declined())
        {
            return true;
        }

        // What the arrow means is still the keymap's to say. Where it is a scroll, the wheel's own step is a row rather
        // than the arrow's three: a trackpad sends many small events, and three rows each read as lurches (#292).
        return Keymap.Means(pressed, _shell.Screen) switch
        {
            Verb.ScrollDown => Notch(RowsANotch),
            Verb.ScrollUp => Notch(-RowsANotch),
            _ => Do(pressed),
        };
    }

    /// <summary>
    ///     A mouse event wherever it landed, before the view under the pointer sees it: spent on closing the list of
    ///     people to mention where it is a click outside it (#335), and left alone otherwise.
    /// </summary>
    private void AheadOfTheViews(object? sender, Mouse mouse)
    {
        if (!mouse.Handled)
        {
            mouse.Handled = _mentions.ClosedBy(mouse) || _languages.ClosedBy(mouse);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _mouse is not null)
        {
            _mouse.MouseEvent -= AheadOfTheViews;
            _mouse = null;
        }

        base.Dispose(disposing);
    }

    /// <summary>
    ///     A click at <paramref name="at" />. An open question takes it wherever it lands, and is all it does; otherwise
    ///     a click on a destination on the rail arrives there (#288), a click on a crumb of the breadcrumb walks back to
    ///     it (#308), and a click on a row of a thing in the content picks it (#290), the first half of a double click
    ///     among them (<see cref="DoubleClicked" />). Everything else — a title, a heading, the API panel, a separator,
    ///     the fetch mark, a rule, a blank — is part of nothing and ignores it.
    /// </summary>
    /// <returns>Whether the click was the shell's, which is any click on the window while a question is open.</returns>
    private bool Clicked(Point at)
    {
        // A click anywhere declines a confirmation or closes a filter prompt, and is not carried out (story 30, 31).
        _clickDeclinedQuestion = _shell.DeclineOpenQuestion();

        if (_clickDeclinedQuestion)
        {
            return true;
        }

        // The breadcrumb is the content panel's top edge, so it is asked about first. Which crumb a column is comes off
        // the trail as drawn, elided or not, and the crumb in front walks back nowhere.
        if (_title.FrameToScreen().Contains(at))
        {
            if (_title.SpanItemAt(at) is { } depth)
            {
                _shell.WalkBack(depth);
            }

            return true;
        }

        if (_content.FrameToScreen().Contains(at))
        {
            _ = ClickedContent(at);

            return true;
        }

        if (!_railed || !_rail.FrameToScreen().Contains(at))
        {
            return false;
        }

        if (_rail.ItemAt(at) is { } destination)
        {
            _shell.Arrive(destination);
        }

        return true;
    }

    /// <summary>
    ///     A double click at <paramref name="at" />: in the content, a click on the row and then <c>⏎</c>, meaning
    ///     whatever <c>⏎</c> means on the screen in front, nothing included (#291). On a row that is part of nothing it
    ///     is nothing, rather than a <c>⏎</c> on whatever was picked before — and on the breadcrumb it is its first
    ///     click's walk back and nothing more, never a <c>⏎</c> on the screen that walk landed on (#308).
    /// </summary>
    /// <remarks>
    ///     Terminal.Gui reports the pair's first click on its own before the pair, so a pair whose first click was spent
    ///     on an open question is spent with it, and opens nothing behind the question it closed (story 30, 31).
    /// </remarks>
    /// <returns>Whether the double click was the shell's, as for <see cref="Clicked" />.</returns>
    private bool DoubleClicked(Point at)
    {
        // Spent once: a later pair must not be swallowed by a question its own first click never saw.
        var declined = _clickDeclinedQuestion;
        _clickDeclinedQuestion = false;

        if (declined || _shell.DeclineOpenQuestion())
        {
            return true;
        }

        if (_title.FrameToScreen().Contains(at))
        {
            return true;
        }

        if (!_content.FrameToScreen().Contains(at))
        {
            return false;
        }

        if (ClickedContent(at))
        {
            _ = Do(ShellKey.Enter);
        }

        return true;
    }

    /// <summary>
    ///     A right click, wherever the pointer is: <paramref name="pressed" />, the <c>esc</c> it stands for, through the
    ///     <see cref="Keymap" /> on the screen in front as <see cref="OnKeyDown" /> takes it, open question and all
    ///     (#307). Nothing on a screen holding a draft, which <c>esc</c> throws away — too much to spend on a button
    ///     pressed by accident.
    /// </summary>
    /// <returns>Always that it was the shell's, so nothing behind the window answers a right click either.</returns>
    private bool RightClicked(ShellKey pressed)
    {
        if (_shell.Screen.HoldsADraft || Declined())
        {
            return true;
        }

        _ = Do(pressed);

        return true;
    }

    /// <summary>
    ///     Declines an open confirmation, as any press but the agreeing key does (story 43), for the gestures that stand
    ///     for such a press.
    /// </summary>
    /// <returns>Whether there was one, and the gesture was spent on it.</returns>
    private bool Declined()
    {
        if (_shell.Asking is null)
        {
            return false;
        }

        _ = _shell.Answer(agreed: false);

        return true;
    }

    /// <summary>One notch of the wheel, which moves the page as the arrows do and by its own step.</summary>
    private bool Notch(int rows)
    {
        Scrolled(rows);

        return true;
    }

    /// <summary>
    ///     The arrow a wheel notch is — <c>↓</c> or <c>↑</c>, the window's three-row page step either way — or
    ///     <see langword="null" /> for anything that is not a notch.
    /// </summary>
    /// <remarks>
    ///     A key rather than a verb, so that what the notch does is whatever <see cref="Keymap" /> says that arrow does
    ///     on the screen in front: the wheel and the arrows cannot drift apart while they are the same press.
    ///     <para>
    ///         A sideways notch is none, and is asked about first: Terminal.Gui's <c>WheeledRight</c> carries
    ///         <c>WheeledDown</c>'s bit and <c>WheeledLeft</c> carries <c>WheeledUp</c>'s. A trackpad drifting sideways
    ///         sends a stream of them between the vertical notches, and read as down and up they jerked the page the
    ///         wrong way for a moment every few rows.
    ///     </para>
    /// </remarks>
    private static ShellKey? Notched(Mouse mouse) =>
        mouse.Flags.HasFlag(MouseFlags.WheeledLeft) || mouse.Flags.HasFlag(MouseFlags.WheeledRight) ? null
        : mouse.Flags.HasFlag(MouseFlags.WheeledDown) ? ShellKey.Down
        : mouse.Flags.HasFlag(MouseFlags.WheeledUp) ? ShellKey.Up
        : null;

    /// <summary>
    ///     What <paramref name="pressed" /> means here, done. The verbs that need a terminal are taken first and the
    ///     rest are the shell's, which is the whole of the division: this window knows how tall the page is, where the
    ///     rows have been scrolled to, what the editor widget is holding, and who owns the run loop — and nothing else
    ///     about what any of it is for.
    /// </summary>
    /// <returns>
    ///     Whether the press was used, which is <see cref="Shell.Shell.Do" />'s answer for everything the window does
    ///     not take (<see cref="Verbs.NeedsATerminal" />): an unused
    ///     <c>←</c>, <c>→</c> or digit falls through to whatever else wants it, the compose editor above all (#83,
    ///     #87).
    /// </returns>
    private bool Do(ShellKey pressed)
    {
        var verb = Keymap.Means(pressed, _shell.Screen);

        return verb.NeedsATerminal() ? Carry(verb) : _shell.Do(verb, Keymap.Answer(pressed));
    }

    /// <summary>Carries out one of the verbs that need a terminal (<see cref="Verbs.NeedsATerminal" />).</summary>
    /// <remarks>
    ///     Reached only for those, so a verb added to that list and not given an arm here fails the first time it is
    ///     pressed rather than being handed to the shell to spend on nothing.
    /// </remarks>
    private bool Carry(Verb verb)
    {
        switch (verb)
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

            case Verb.NextSection:
                Sectioned(1);

                return true;

            case Verb.PreviousSection:
                Sectioned(-1);

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
                _ = _shell.Send();

                return true;

            default:
                throw new UnreachableException($"{verb} needs a terminal and the window has no arm for it.");
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
    ///     <c>[</c> and <c>]</c>: the pick moves to the first thing of the run before or after this one, and the
    ///     run's heading comes onto the page with it where it is not already there (#166).
    /// </summary>
    /// <remarks>
    ///     Which thing that is comes from the rows, the same way <see cref="Walk" />'s reclaim does — so nothing here
    ///     knows what a search result is, and a screen that draws no headings simply has no run to answer with. At the
    ///     ends of the runs there is none, and the press does nothing rather than moving the page on its own.
    /// </remarks>
    private void Sectioned(int by)
    {
        if (_content.Along(by) is not { } at)
        {
            return;
        }

        _shell.Section(at);
        _content.Anchor();
    }

    /// <summary>
    ///     A click in the content: the thing the row under the pointer is part of is picked, and the page stays where it
    ///     is, so that what was clicked does not move out from under the pointer — following the pick would put a tall
    ///     post's byline at the top of the page for a click on its picture (#290).
    /// </summary>
    /// <returns>Whether the row was part of a thing, and so picked it.</returns>
    private bool ClickedContent(Point at)
    {
        if (_content.ItemAt(at) is not { } item)
        {
            return false;
        }

        _content.Hold();
        _shell.Section(item);

        return true;
    }

    /// <summary>
    ///     <c>↓</c> and <c>↑</c>: the screen moves and the selection stays where it is.
    /// </summary>
    /// <remarks>
    ///     The whole frame is redrawn, not the content region alone. Every other key that changes what is on screen
    ///     ends at the shell's <c>Changed</c>, which redraws everything; these two change nothing the shell knows
    ///     about, so they are the only keys that could have redrawn one region — and the panel's top edge is two
    ///     regions, the panel's own and the title laid over it, which a redraw of one leaves to whichever was drawn
    ///     last.
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
    ///     Where the content panel's viewport sits in this window: inside the panel's edge, wherever the rail has left
    ///     the panel.
    /// </summary>
    private static Point ViewportOrigin(View? content) =>
        content is null ? Point.Empty : content.Frame.Location + new Size(Edge, Edge);

    /// <summary>
    ///     Where the compose screen says its editor goes inside <paramref name="content" />'s viewport, or nowhere
    ///     while no post is being written.
    /// </summary>
    private Rectangle EditorAt(View? content) =>
        content is { Viewport.Width: > 0 } && _shell.Screen is ComposeScreen compose
            ? compose.EditorAt(content.Viewport.Size)
            : Rectangle.Empty;

    /// <summary>The same for the warning field.</summary>
    private Rectangle WarningAt(View? content) =>
        content is { Viewport.Width: > 0 } && _shell.Screen is ComposeScreen compose
            ? compose.WarningAt(content.Viewport.Size)
            : Rectangle.Empty;

    /// <summary>The same for To.</summary>
    private Rectangle ToAt(View? content) =>
        content is { Viewport.Width: > 0 } && _shell.Screen is ComposeScreen compose
            ? compose.ToAt(content.Viewport.Size)
            : Rectangle.Empty;

    /// <summary>The same for Lang.</summary>
    private Rectangle LangAt(View? content) =>
        content is { Viewport.Width: > 0 } && _shell.Screen is ComposeScreen compose
            ? compose.LangAt(content.Viewport.Size)
            : Rectangle.Empty;

    /// <summary>
    ///     One of the fields gained focus — by a click, or by <see cref="Refresh" /> moving it — and the screen is
    ///     brought into step where it says the typing is somewhere else.
    /// </summary>
    private void TypingMoved(bool gained, ComposeField field)
    {
        if (gained && _editor.Visible)
        {
            _shell.TypeInto(field);
        }
    }

    /// <summary>
    ///     Lays the rail out where the shell has one and takes it away where it has none — with nobody to act as, which
    ///     leaves only adding a profile and every column to it (#247). Only on the frame where that changes.
    /// </summary>
    private void Railed()
    {
        if (_railed == _shell.ShowsRail)
        {
            return;
        }

        _railed = _shell.ShowsRail;
        _rail.Visible = _railed;

        var left = _railed ? RailLines.Width : 0;

        _title.X = left;
        _content.X = left;
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
        Railed();

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

        if (composing && !_editor.Visible)
        {
            var compose = (ComposeScreen)_shell.Screen;

            // Laid out before it is filled: where it goes is read only on a layout pass, this screen's block may be a
            // different height than the last one that pushed the editor open, and an editor still at the nothing it
            // was sized to while no post was being written wraps its text to no width and loses the caret below.
            _editor.Visible = true;
            _editor.Layout();
            _editor.Text = compose.Text;

            _warning.Visible = true;
            _warning.Layout();
            _warning.Text = compose.Warning;
            _warning.MoveEnd();

            // On an edit To takes nothing, so it takes no focus either: a click on it is the screen's to ignore.
            _to.Visible = true;
            _to.CanFocus = compose.Takes(ComposeField.To);

            _lang.Visible = true;
            _lang.Layout();
            _languages.Fill(compose.Lang.Held);

            _editor.SetFocus();

            // After whatever the screen opened with rather than in front of it: an editor opened on `@maria ` or on
            // a post being edited puts the caret where the reader's next word goes, which is the end of what is
            // already written (ADR-0013, #85). A caret left at nought types into somebody's name.
            _editor.MoveEnd();
        }
        else if (!composing && _editor.Visible)
        {
            _editor.Visible = false;
            _warning.Visible = false;
            _to.Visible = false;
            _languages.Close();
            _lang.Visible = false;
            SetFocus();
        }

        // The caret is wherever the typing is going: ctrl-w or an arrow moved it, or a click did and the screen caught up.
        // Both fields take focus at all times, so this only moves it where it is not already.
        if (_shell.Screen is ComposeScreen { Typing: var typing } && _editor.Visible)
        {
            View field = typing switch
            {
                ComposeField.To => _to,
                ComposeField.Lang => _lang,
                ComposeField.Warning => _warning,
                _ => _editor,
            };

            if (!field.HasFocus)
            {
                field.SetFocus();
            }
        }

        // After the editor has been shown or hidden, and on every change of the shell's — a profile switch, or more
        // people arriving — since either can change what the word being typed matches.
        _mentions.Follow();

        SetNeedsDraw();
    }
}
