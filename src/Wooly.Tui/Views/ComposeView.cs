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

    /// <summary>The people to mention, hung under the @-word being typed in the post (#318).</summary>
    private readonly MentionList _mentions;

    /// <summary>The languages, hung under Lang (#340).</summary>
    private readonly LanguageList _languages;

    /// <summary>Where the terminal's mouse events arrive, once the view is running; asked ahead of the views.</summary>
    private IMouse? _mouse;

    public ComposeView(ITheme theme, Shell.Shell shell)
    {
        _shell = shell;

        CanFocus = true;
        Visible = false;

        // Nothing of its own to draw or to click: the screen's rows show through, and a click between the fields is
        // the content panel's.
        ViewportSettings |= ViewportSettingsFlags.Transparent | ViewportSettingsFlags.TransparentMouse;

        _editor = new ComposeEditor(
            theme,
            ComposeScreen.EmptyPostHint,
            () => _ = shell.Send(),
            () => shell.Back(),
            WriteWarning,
            () => shell.ChangeCompose(compose => compose.Walk(-1)))
        {
            // Wherever the compose screen says, inside the viewport this view is laid over (#315): its headers and
            // hairlines (#317) are painted behind it, so the screen that paints them is the one that knows how far down
            // the editor has to start for them to be seen.
            X = Pos.Func(_ => EditorAt().X),
            Y = Pos.Func(_ => EditorAt().Y),
            Width = Dim.Func(_ => EditorAt().Width),
            Height = Dim.Func(_ => EditorAt().Height),
            Visible = false,
            WordWrap = true,

            // tab is the frame's on every screen, compose included (docs/tui-shell.md, ADR-0024): it moves the rail's
            // cursor rather than putting a tab into the post.
            TabKeyAddsTab = false,
        };

        // The screen's text follows the editor on every edit rather than only at ctrl-s, so whatever reads it while a
        // post is being written — a count, a list of people to mention — sees what has been typed so far.
        _editor.ContentsChanged += (_, _) => _shell.ChangeCompose(compose => compose.Rewrite(_editor.Text));

        _warning = new ComposeWarningField(
            theme,
            () => (_shell.Screen as ComposeScreen)?.WarningHint ?? string.Empty,
            () => _ = shell.Send(),
            () => shell.Back(),
            WriteWarning)
        {
            // Wherever the compose screen says, as for the editor: its header's value column, on whichever row the
            // headers above it leave it.
            X = Pos.Func(_ => WarningAt().X),
            Y = Pos.Func(_ => WarningAt().Y),
            Width = Dim.Func(_ => WarningAt().Width),
            Height = Dim.Func(_ => WarningAt().Height),
            Visible = false,
        };

        // The same for the warning. The count is painted behind, on rows an edit announces as changed.
        _warning.ValueChanged += (_, _) => _shell.ChangeCompose(compose => compose.RewriteWarning(_warning.Text));

        _to = new ComposeToField(
            theme,
            room => (_shell.Screen as ComposeScreen)?.ToSpans(room) ?? [],
            (column, room) => shell.ChangeCompose(compose => compose.ClickTo(column, room)))
        {
            // Wherever the compose screen says, as for the warning: To's value column, under From.
            X = Pos.Func(_ => ToAt().X),
            Y = Pos.Func(_ => ToAt().Y),
            Width = Dim.Func(_ => ToAt().Width),
            Height = Dim.Func(_ => ToAt().Height),
            Visible = false,
        };

        _lang = new ComposeLangField(
            theme,
            () => (_shell.Screen as ComposeScreen)?.LanguageHint ?? string.Empty,
            () => _ = shell.Send(),
            () => shell.Back(),
            WriteWarning)
        {
            // Wherever the compose screen says, as for To: Lang's value column, under To — nowhere where a short
            // viewport has given the row up.
            X = Pos.Func(_ => LangAt().X),
            Y = Pos.Func(_ => LangAt().Y),
            Width = Dim.Func(_ => LangAt().Width),
            Height = Dim.Func(_ => LangAt().Height),
            Visible = false,
        };

        // A click into any field moves the typing there: where the typing is is one fact, the screen's, and whichever
        // way it moved the screen is told so that the status row, the header's mark and the hint keep up.
        _to.HasFocusChanged += (_, focus) => TypingMoved(focus.NewValue, ComposeField.To);
        _lang.HasFocusChanged += (_, focus) => TypingMoved(focus.NewValue, ComposeField.Lang);
        _warning.HasFocusChanged += (_, focus) => TypingMoved(focus.NewValue, ComposeField.Warning);
        _editor.HasFocusChanged += (_, focus) => TypingMoved(focus.NewValue, ComposeField.Post);

        // The lists hang under the fields they serve, laid in this view after the fields so they draw over them.
        _mentions = new MentionList(theme, shell, _editor);
        _editor.Ahead = _mentions.Took;

        _languages = new LanguageList(theme, shell, _lang, this);

        Add(_editor, _to, _lang, _warning, _mentions.View, _languages.View);

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

        shell.Changed += Refresh;

        Refresh();
    }

    /// <summary>
    ///     The keys no field took that walk the fields or choose on To — <c>↑</c>/<c>↓</c>, <c>←</c>/<c>→</c> — made
    ///     here, as the keymap says they are on compose, rather than left to Terminal.Gui to move the focus with. At
    ///     either end of the walk the key is spent on nothing (#337).
    /// </summary>
    protected override bool OnKeyDown(Key key) =>
        ShellKeys.Of(key) is { } pressed
        && Keymap.Means(pressed, _shell.Screen) is var verb and (Verb.PreviousField or Verb.NextField or Verb.PreviousChoice or Verb.NextChoice)
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
    ///     A mouse event wherever it landed, before the view under the pointer sees it: spent on closing an open list
    ///     where it is a click outside it (#335), and left alone otherwise.
    /// </summary>
    private void AheadOfTheViews(object? sender, Mouse mouse)
    {
        if (!mouse.Handled)
        {
            mouse.Handled = _mentions.ClosedBy(mouse) || _languages.ClosedBy(mouse);
        }
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

            // Filled while it does not have the typing, which opens no list of languages.
            _lang.Visible = true;
            _lang.Layout();
            _lang.Text = compose.Lang.Held;
            _lang.MoveEnd();

            _editor.SetFocus();

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

    /// <summary>Where the compose screen says its editor goes in this view, or nowhere while no post is being written.</summary>
    private Rectangle EditorAt() => Laid(compose => compose.EditorAt(Viewport.Size));

    /// <summary>The same for the warning field.</summary>
    private Rectangle WarningAt() => Laid(compose => compose.WarningAt(Viewport.Size));

    /// <summary>The same for To.</summary>
    private Rectangle ToAt() => Laid(compose => compose.ToAt(Viewport.Size));

    /// <summary>The same for Lang.</summary>
    private Rectangle LangAt() => Laid(compose => compose.LangAt(Viewport.Size));

    private Rectangle Laid(Func<ComposeScreen, Rectangle> at) =>
        Viewport.Width > 0 && _shell.Screen is ComposeScreen compose ? at(compose) : Rectangle.Empty;

    /// <summary><c>ctrl-w</c> in any of compose's fields: the typing to the warning and back (#123).</summary>
    private void WriteWarning() => _shell.ChangeCompose(compose => compose.WriteTheWarning());

    /// <summary>
    ///     One of the fields gained focus — by a click, or by <see cref="Refresh" /> moving it — and the screen is
    ///     brought into step where it says the typing is somewhere else.
    /// </summary>
    private void TypingMoved(bool gained, ComposeField field)
    {
        if (gained && _editor.Visible)
        {
            _shell.ChangeCompose(compose => compose.TypeInto(field));
        }
    }
}
