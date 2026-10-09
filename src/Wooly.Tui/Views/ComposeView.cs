using System.Drawing;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Views;

/// <summary>
///     Compose's fields, in one view laid over the content viewport (#365): the editor, and the warning, To and Lang
///     fields, with the list of people to mention hung under the post and the list of languages under Lang (#366). Each
///     field sits where the compose screen lays it, shows while compose is on top and opens on what the draft
///     holds, and where the typing is stays one fact — the screen's — whichever way it moved. Every change it makes goes
///     through the shell's one way in (<see cref="Shell.Shell.ChangeCompose" />).
/// </summary>
/// <remarks>
///     The headers, hairlines and count around the fields are the screen's rows, painted by the content panel behind
///     this view, so it draws nothing of its own and lets every click it has no field under through to the panel.
///     <para>
///         The compose screen and the shell stay free of Terminal.Gui (ADR-0005 amendment, ADR-0015): this is the side
///         of the line the widgets are on.
///     </para>
/// </remarks>
internal sealed class ComposeView : View
{
    private readonly Shell.Shell _shell;
    private readonly ComposeEditor _editor;

    /// <summary>The content warning over the post, laid over its header's value column (#320).</summary>
    private readonly ComposeWarningField _warning;

    /// <summary>Who the post goes to, a row of radio buttons laid over To's value column (#338).</summary>
    private readonly ComposeToField _to;

    /// <summary>What language the post is in, a field laid over Lang's value column (#340).</summary>
    private readonly ComposeLangField _lang;

    /// <summary>
    ///     The Media header and the rows under it, laid over them to take the walk's typing and the mouse (#378, #379).
    /// </summary>
    private readonly ComposeMediaField _media;

    /// <summary>The people to mention, hung under the @-word being typed in the post (#318).</summary>
    private readonly MentionList _mentions;

    /// <summary>The languages, hung under Lang (#340).</summary>
    private readonly LanguageList _languages;

    /// <summary>Where the terminal's mouse events arrive, once the view is running; asked ahead of the views.</summary>
    private IMouse? _mouse;

    /// <summary>
    ///     Whether the fields are being shown as compose comes on top, while which of them Terminal.Gui focuses says
    ///     nothing about where the typing is.
    /// </summary>
    private bool _showing;

    public ComposeView(ITheme theme, Shell.Shell shell)
    {
        _shell = shell;

        CanFocus = true;
        Visible = false;

        // Nothing of its own to draw or to click: the screen's rows show through, and a click between the fields is
        // the content panel's.
        ViewportSettings |= ViewportSettingsFlags.Transparent | ViewportSettingsFlags.TransparentMouse;

        // Each field is wherever the compose screen says, inside the viewport this view is laid over (#315): the
        // headers and hairlines (#317) are painted behind, so the screen that paints them is the one that knows where
        // the editor has to start for them to be seen, and on which row each header's value column is — Lang's nowhere
        // where a short viewport has given its row up.
        _editor = Placed(
            new ComposeEditor(
                theme,
                ComposeScreen.EmptyPostHint,
                Send,
                Back,
                WriteWarning,
                () => _shell.ChangeCompose(compose => compose.Walk(-1)))
            {
                WordWrap = true,

                // tab is the frame's on every screen, compose included (docs/tui-shell.md, ADR-0024): it moves the
                // rail's cursor rather than putting a tab into the post.
                TabKeyAddsTab = false,
            },
            (compose, room) => compose.EditorAt(room));

        // The screen's text follows the editor on every edit rather than only at ctrl-s, so whatever reads it while a
        // post is being written — a count, a list of people to mention — sees what has been typed so far.
        _editor.ContentsChanged += (_, _) => _shell.ChangeCompose(compose => compose.Rewrite(_editor.Text));

        _warning = Placed(
            new ComposeWarningField(
                theme,
                () => (_shell.Screen as ComposeScreen)?.WarningHint ?? string.Empty,
                Send,
                Back,
                WriteWarning),
            (compose, room) => compose.WarningAt(room));

        // The same for the warning. The count is painted behind, on rows an edit announces as changed.
        _warning.ValueChanged += (_, _) => _shell.ChangeCompose(compose => compose.RewriteWarning(_warning.Text));

        _to = Placed(
            new ComposeToField(
                theme,
                room => (_shell.Screen as ComposeScreen)?.ToSpans(room) ?? [],
                (column, room) => _shell.ChangeCompose(compose => compose.ClickTo(column, room))),
            (compose, room) => compose.ToAt(room));

        _lang = Placed(
            new ComposeLangField(
                theme,
                () => (_shell.Screen as ComposeScreen)?.LanguageHint ?? string.Empty,
                Send,
                Back,
                WriteWarning),
            (compose, room) => compose.LangAt(room));

        // A click on the toggle at the end of Media's line flips it; anywhere else on the header — its words — opens
        // the file browser (#376), or, where a short terminal folded the rows into the line, lists them on a screen of
        // their own (story 58). A click on a row is the field's own (#378, #377).
        _media = Placed(
            new ComposeMediaField(shell, column =>
            {
                if (_shell.Screen is not ComposeScreen compose)
                {
                    return;
                }

                if (compose.OnTheSensitiveToggle(column, Viewport.Size))
                {
                    _shell.ToggleSensitive();
                }
                else if (compose.RowsFolded)
                {
                    _shell.ListAttachments();
                }
                else
                {
                    _shell.Browse();
                }
            }),
            (compose, room) => compose.MediaAt(room));

        // A click into any field moves the typing there: where the typing is is one fact, the screen's, and whichever
        // way it moved the screen is told so that the status row, the header's mark and the hint keep up.
        _to.HasFocusChanged += (_, focus) => TypingMoved(focus.NewValue, ComposeField.To);
        _lang.HasFocusChanged += (_, focus) => TypingMoved(focus.NewValue, ComposeField.Lang);
        _warning.HasFocusChanged += (_, focus) => TypingMoved(focus.NewValue, ComposeField.Warning);
        _media.HasFocusChanged += (_, focus) => TypingMoved(focus.NewValue, ComposeField.Media);
        _editor.HasFocusChanged += (_, focus) => TypingMoved(focus.NewValue, ComposeField.Post);

        // The lists hang under the fields they serve, laid in this view after the fields so they draw over them.
        _mentions = new MentionList(theme, shell, _editor);
        _editor.Ahead = _mentions.Took;

        // ctrl-p is the frame's here as everywhere (ADR-0020, #373), where TextView would take it for a line up.
        _editor.KeyBindings.Remove(Key.P.WithCtrl);

        // A question open on the status row takes every key ahead of whichever field has the typing, since the fields
        // take their keys before the window sees them (#373).
        _editor.Answering = _to.Answering = _lang.Answering = _warning.Answering = _media.Answering = Answered;

        // ctrl-v or alt-v in any field that takes a paste attaches a picture or copied files from the clipboard, where
        // it holds either, and is the field's own paste otherwise (#380). The fields that take none, To and Media, leave
        // both keys to the window, which asks the keymap the same question.
        _editor.FromTheClipboard = _lang.FromTheClipboard = _warning.FromTheClipboard = FromTheClipboard;

        _languages = new LanguageList(theme, shell, _lang, this);

        Add(_editor, _to, _lang, _warning, _media, _mentions.View, _languages.View);

        // A click outside an open list closes it whichever view it lands on — the editor's caret, the content behind
        // this view, the rail — so it is asked about where the terminal's mouse events arrive, ahead of every view
        // under the pointer (#335).
        Initialized += (_, _) =>
        {
            _mouse = App?.Mouse;

            if (_mouse is not null)
            {
                _mouse.MouseEvent += AheadOfTheViews;
            }
        };

        _shell.Changed += Refresh;

        Refresh();
    }

    /// <summary>
    ///     The keys no field took that walk the fields or choose on To — <c>↑</c>/<c>↓</c>, <c>←</c>/<c>→</c> — made
    ///     here, as the keymap says they are on compose, rather than left to Terminal.Gui to move the focus with. At
    ///     either end of the walk the key is spent on nothing (#337). And <c>s</c>, which the Media header leaves for
    ///     the sensitive toggle (#379).
    /// </summary>
    protected override bool OnKeyDown(Key key) =>
        ShellKeys.Of(key) is { } pressed
        && Keymap.Means(pressed, _shell.Screen) is var verb
            and (Verb.PreviousField or Verb.NextField or Verb.PreviousChoice or Verb.NextChoice or Verb.ToggleSensitive)
        && _shell.Do(verb, Keymap.Answer(pressed));

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _shell.Changed -= Refresh;

            if (_mouse is not null)
            {
                _mouse.MouseEvent -= AheadOfTheViews;
                _mouse = null;
            }
        }

        base.Dispose(disposing);
    }

    /// <summary>
    ///     A mouse event wherever it landed, before the view under the pointer sees it: a right click while compose is in
    ///     front, which is its <c>esc</c> wherever it lands (#373); any other press over a field while a question is
    ///     open, which the question takes (<see cref="Withheld" />); spent on closing an open list where it is a click
    ///     outside it (#335); and left alone otherwise.
    /// </summary>
    private void AheadOfTheViews(object? sender, Mouse mouse)
    {
        if (!mouse.Handled)
        {
            mouse.Handled = RightClicked(mouse)
                            || Withheld(mouse)
                            || _mentions.ClosedBy(mouse)
                            || _languages.ClosedBy(mouse);
        }
    }

    /// <summary>
    ///     A button or the wheel over one of compose's fields while a question is open on the status row: kept from the
    ///     field, so no caret moves and no draft scrolls behind the question, and a click or a notch declines it, as
    ///     they do anywhere else in the shell (open questions win, #373). Elsewhere the window declines it itself.
    /// </summary>
    /// <returns>Whether the event was over a field and spent.</returns>
    private bool Withheld(Mouse mouse)
    {
        if (_shell.Asking is null || !Visible || !Over(mouse.ScreenPosition))
        {
            return false;
        }

        if (mouse.Flags.HasFlag(MouseFlags.LeftButtonClicked)
            || mouse.Flags.HasFlag(MouseFlags.WheeledDown)
            || mouse.Flags.HasFlag(MouseFlags.WheeledUp))
        {
            _ = _shell.Answer(pressed: null);
        }

        return mouse.Flags != MouseFlags.None && mouse.Flags != MouseFlags.PositionReport;
    }

    /// <summary>Whether <paramref name="at" /> is over one of the fields, which take the pointer before the window does.</summary>
    private bool Over(Point at) =>
        new View[] { _editor, _to, _lang, _warning, _media }.Any(field => field.Visible && field.FrameToScreen().Contains(at));

    /// <summary>
    ///     A right click with compose in front: <c>esc</c>, wherever it lands — over a field, which would otherwise keep
    ///     it to itself, as anywhere else. It asks before a touched draft is thrown away and, pressed again on that
    ///     question, keeps the draft, the way <c>esc</c> does (#373). Neither list under the pointer closes or picks for
    ///     it, and the editor's own context menu never opens.
    /// </summary>
    /// <returns>Whether it was one, and spent.</returns>
    private bool RightClicked(Mouse mouse)
    {
        if (_shell.Screen is not ComposeScreen || ShellKeys.Of(mouse) is not { } pressed)
        {
            return false;
        }

        if (!Answered(pressed))
        {
            _shell.Back();
        }

        return true;
    }

    /// <summary>
    ///     <paramref name="key" /> as the answer to a question the shell has open, where it has one (<see cref="Answered(ShellKey?)" />).
    /// </summary>
    private bool Answered(Key key) => Answered(ShellKeys.Of(key));

    /// <summary>
    ///     Whether <paramref name="key" /> is compose's paste from the clipboard, as the keymap says, and if so whether
    ///     it attached anything — <see langword="null" /> where it is no paste at all, so the field takes it as any key.
    /// </summary>
    private bool? FromTheClipboard(Key key) =>
        ShellKeys.Of(key) is { } pressed && Keymap.Means(pressed, _shell.Screen) is Verb.PasteFromTheClipboard
            ? _shell.Do(Verb.PasteFromTheClipboard, answer: null)
            : null;

    /// <summary>
    ///     <paramref name="pressed" /> as the answer to a question the shell has open on the status row — <c>y</c>
    ///     agrees, anything else keeps — where it has one, and nothing where it has none.
    /// </summary>
    /// <returns>Whether there was a question, and the press was spent on it.</returns>
    private bool Answered(ShellKey? pressed)
    {
        if (_shell.Asking is null)
        {
            return false;
        }

        _ = _shell.Answer(pressed);

        return true;
    }

    /// <summary>
    ///     Shows the fields as compose comes on top, filled from the draft, and takes them away as it goes. Which of the
    ///     two is a fact about the stack, not a mode this view keeps of its own.
    /// </summary>
    private void Refresh()
    {
        var composing = _shell.Screen as ComposeScreen;

        if (composing is { } compose && !_editor.Visible)
        {
            // Terminal.Gui hands focus to a field as it is shown, which would tell the screen the typing went there.
            _showing = true;
            Visible = true;

            // Laid out before it is filled: where it goes is read only on a layout pass, this screen's block may be a
            // different height than the last one that opened the editor, and an editor still at the nothing it was
            // sized to while no post was being written wraps its text to no width and loses the caret below.
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

            // Filled as the screen holds it rather than typed, which opens no list of languages.
            _lang.Visible = true;
            _lang.Layout();
            _languages.Fill(compose.Lang.Held);

            // An edit's Media header is read-only (#381): nothing there to walk to or click on.
            _media.Visible = compose.TakesAttachments;

            // Where the screen says the typing is, rather than the post: compose comes back from the description
            // editor with the row described still picked, and focusing the post took the walk off it (review of #372).
            // A fresh compose is typing into the post anyway.
            FieldFor(compose.Typing).SetFocus();
            _showing = false;

            // After whatever the screen opened with rather than in front of it: an editor opened on `@maria ` or on
            // a post being edited puts the caret where the reader's next word goes, which is the end of what is
            // already written (ADR-0013, #85). A caret left at nought types into somebody's name.
            _editor.MoveEnd();
        }
        else if (composing is null && _editor.Visible)
        {
            var focused = HasFocus;

            // Closed outright rather than left to Lang losing the typing, so that no list is still open over the next
            // compose, whose screen offers none (#366).
            _languages.Close();

            _editor.Visible = false;
            _warning.Visible = false;
            _to.Visible = false;
            _lang.Visible = false;
            _media.Visible = false;
            Visible = false;

            if (focused)
            {
                SuperView?.SetFocus();
            }
        }

        // The caret is wherever the typing is going: ctrl-w or an arrow moved it, or a click did and the screen caught
        // up. Every field takes focus at all times, so this only moves it where it is not already.
        if (composing is { Typing: var typing } && _editor.Visible)
        {
            var field = FieldFor(typing);

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

    /// <summary>The field the typing going into <paramref name="typing" /> is focused on.</summary>
    private View FieldFor(ComposeField typing) => typing switch
    {
        ComposeField.To => _to,
        ComposeField.Lang => _lang,
        ComposeField.Warning => _warning,
        ComposeField.Media or ComposeField.Attachment => _media,
        _ => _editor,
    };

    /// <summary>
    ///     <paramref name="field" />, hidden until compose comes on top and laid wherever <paramref name="at" /> says
    ///     the compose screen puts it in this view's viewport — nowhere while no post is being written.
    /// </summary>
    private T Placed<T>(T field, Func<ComposeScreen, Size, Rectangle> at)
        where T : View
    {
        Rectangle Laid() =>
            Viewport.Width > 0 && _shell.Screen is ComposeScreen compose ? at(compose, Viewport.Size) : Rectangle.Empty;

        field.X = Pos.Func(_ => Laid().X);
        field.Y = Pos.Func(_ => Laid().Y);
        field.Width = Dim.Func(_ => Laid().Width);
        field.Height = Dim.Func(_ => Laid().Height);
        field.Visible = false;

        return field;
    }

    /// <summary><c>ctrl-s</c> in any of compose's fields.</summary>
    private void Send() => _ = _shell.Send();

    /// <summary><c>esc</c> in any of compose's fields.</summary>
    private void Back() => _shell.Back();

    /// <summary><c>ctrl-w</c> in any of compose's fields: the typing to the warning and back (#123).</summary>
    private void WriteWarning() => _shell.ChangeCompose(compose => compose.WriteTheWarning());

    /// <summary>
    ///     One of the fields gained focus — by a click, or by <see cref="Refresh" /> moving it — and the screen is
    ///     brought into step where it says the typing is somewhere else.
    /// </summary>
    private void TypingMoved(bool gained, ComposeField field)
    {
        if (gained && _editor.Visible && !_showing)
        {
            _shell.ChangeCompose(compose => compose.TypeInto(field));
        }
    }
}
