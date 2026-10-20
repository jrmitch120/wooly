using System.Drawing;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Wooly.Tui.Media;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Views;

/// <summary>
///     The edit public profile form's fields (#406), in one view laid over the content viewport as compose's are
///     (<see cref="ComposeView" />): the display name, the bio in the editor a post is written in, each custom field's
///     label and value, the avatar and the header (#407), and the three toggles. Each sits where the form lays it, shows while the form is on top and
///     opens on what the form holds, and where the typing is stays one fact — the form's — whichever way it moved.
///     Every change goes through the shell's one way in (<see cref="Shell.Shell.ChangePublicProfile" />).
/// </summary>
/// <remarks>
///     The labels, the boxes and the words beside them are the form's rows, painted by the content panel behind this
///     view, so it draws nothing of its own and lets every click it has no field under through to the panel. The form
///     and the shell stay free of Terminal.Gui (ADR-0005 amendment, ADR-0015): this is the side of the line the widgets
///     are on.
/// </remarks>
internal sealed class PublicProfileView : View
{
    private readonly ITheme _theme;
    private readonly Shell.Shell _shell;

    private readonly PublicProfileTextField _name;
    private readonly ComposeEditor _bio;
    private readonly List<PublicProfileTextField> _labels = [];
    private readonly List<PublicProfileTextField> _values = [];
    private readonly Dictionary<PublicProfilePart, PublicProfileToggle> _toggles = [];
    private readonly Dictionary<PublicProfilePart, PublicProfileToggle> _pictures = [];

    /// <summary>How this terminal paints pixels, which says how tall the avatar's and the header's rows are.</summary>
    private readonly Func<Raster?> _raster;

    /// <summary>The people to mention, hung under the @-word being typed in the bio, as under compose's post (#408).</summary>
    private readonly MentionList _mentions;

    /// <summary>The form the fields were last filled from, or none while no form is on top.</summary>
    private PublicProfileScreen? _showing;

    /// <summary>Whether the fields are being filled as the form comes on top, while focus moving says nothing.</summary>
    private bool _filling;

    /// <summary>Where the terminal's mouse events arrive, once the view is running; asked ahead of the views.</summary>
    private IMouse? _mouse;

    public PublicProfileView(ITheme theme, Shell.Shell shell, Func<Raster?>? raster = null)
    {
        _theme = theme;
        _shell = shell;
        _raster = raster ?? (() => null);

        CanFocus = true;
        Visible = false;
        ViewportSettings |= ViewportSettingsFlags.Transparent | ViewportSettingsFlags.TransparentMouse;

        _name = Field(PublicProfileScreen.NameHint, new PublicProfileStop(PublicProfilePart.DisplayName));
        _name.ValueChanged += (_, _) => Change(form => form.RewriteDisplayName(_name.Text));

        _bio = Placed(
            new ComposeEditor(
                theme,
                PublicProfileScreen.BioHint,
                Save,
                Back,
                () => { },
                () => Walk(-1),
                () => Walk(1))
            {
                WordWrap = true,

                // tab is the frame's here as everywhere (docs/tui-shell.md): it moves the rail's cursor.
                TabKeyAddsTab = false,
            },
            new PublicProfileStop(PublicProfilePart.Bio));

        // ctrl-p is the frame's here as everywhere (ADR-0020), where TextView would take it for a line up.
        _bio.KeyBindings.Remove(Key.P.WithCtrl);
        _bio.Answering = Answered;
        _bio.ContentsChanged += (_, _) => Change(form => form.RewriteBio(_bio.Text));

        _mentions = new MentionList(theme, shell, _bio, () => _shell.Screen is PublicProfileScreen);
        _bio.Ahead = _mentions.Took;

        foreach (var part in new[] { PublicProfilePart.Locked, PublicProfilePart.Bot, PublicProfilePart.Discoverable })
        {
            var stop = new PublicProfileStop(part);
            var toggle = Placed(new PublicProfileToggle(() => Flip(stop)), stop);

            toggle.Answering = Answered;
            toggle.HasFocusChanged += (_, focus) => TypingMoved(focus.NewValue, stop);

            _toggles[part] = toggle;
        }

        // The avatar and the header take no typing either: a click on one, or ⏎, opens the file browser for it (#407).
        foreach (var part in new[] { PublicProfilePart.Avatar, PublicProfilePart.Header })
        {
            var stop = new PublicProfileStop(part);
            var picture = Placed(new PublicProfileToggle(() => Choose(stop)), stop);

            picture.Answering = Answered;
            picture.HasFocusChanged += (_, focus) => TypingMoved(focus.NewValue, stop);

            _pictures[part] = picture;
        }

        _bio.HasFocusChanged += (_, focus) => TypingMoved(focus.NewValue, new PublicProfileStop(PublicProfilePart.Bio));

        Add(_name, _bio);
        Add([.. _pictures.Values]);
        Add([.. _toggles.Values]);
        Add(_mentions.View);

        // A right click over a field is esc, which the fields would otherwise keep to themselves, so it is asked about
        // where the terminal's mouse events arrive, ahead of every view under the pointer — as compose's are (#373).
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
    ///     The keys no field took that mean something on the form, made here as the keymap says they do rather than
    ///     left to Terminal.Gui: <c>↑</c>/<c>↓</c>, which walk the fields and stop at either end, <c>space</c> and
    ///     <c>⏎</c> on a toggle, <c>⏎</c> and the paste on a picture (#407), and <c>ctrl-s</c>.
    /// </summary>
    protected override bool OnKeyDown(Key key) =>
        ShellKeys.Of(key) is { } pressed
        && Keymap.Means(pressed, _shell.Screen) is var verb
            and (Verb.PreviousField or Verb.NextField or Verb.FlipToggle or Verb.SavePublicProfile
                or Verb.OpenBrowser or Verb.PasteFromTheClipboard)
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

    /// <summary>Every field and toggle, in the order the arrows walk them.</summary>
    private IEnumerable<View> Fields =>
    [
        _name, _bio, .. _pictures.Values, .. _labels.Zip(_values).SelectMany(row => new View[] { row.First, row.Second }),
        .. _toggles.Values,
    ];

    /// <summary>
    ///     A mouse event wherever it landed, before the view under the pointer sees it: a right click while the form is
    ///     in front, which is its <c>esc</c> wherever it lands; any other press over a field while a question is open,
    ///     which the question takes; spent on closing the list of people to mention where it is a click outside it, as
    ///     on compose (#335); and left alone otherwise.
    /// </summary>
    private void AheadOfTheViews(object? sender, Mouse mouse)
    {
        if (!mouse.Handled)
        {
            mouse.Handled = RightClicked(mouse) || Withheld(mouse) || _mentions.ClosedBy(mouse);
        }
    }

    /// <summary>
    ///     A right click with the form in front: <c>esc</c>, wherever it lands, asking before a touched form is thrown
    ///     away and, pressed again on that question, keeping it — the way <c>esc</c> does.
    /// </summary>
    private bool RightClicked(Mouse mouse)
    {
        if (_shell.Screen is not PublicProfileScreen || ShellKeys.Of(mouse) is not { } pressed)
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
    ///     A button or the wheel over one of the fields while a question is open on the status row: kept from the field,
    ///     and a click or a notch declines it, as anywhere else in the shell (#373).
    /// </summary>
    private bool Withheld(Mouse mouse)
    {
        if (_shell.Asking is null || !Visible
            || !Fields.Any(field => field.Visible && field.FrameToScreen().Contains(mouse.ScreenPosition)))
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

    /// <summary><paramref name="key" /> as the answer to a question the shell has open, where it has one.</summary>
    private bool Answered(Key key) => Answered(ShellKeys.Of(key));

    /// <summary>
    ///     <paramref name="pressed" /> as the answer to a question the shell has open on the status row — <c>y</c>
    ///     agrees, anything else keeps — where it has one, and nothing where it has none.
    /// </summary>
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
    ///     Shows the fields as the form comes on top, filled from it, and takes them away as it goes; and keeps the
    ///     focus wherever the form says the typing is.
    /// </summary>
    private void Refresh()
    {
        var form = _shell.Screen as PublicProfileScreen;

        if (form is not null && !ReferenceEquals(form, _showing))
        {
            Fill(form);
        }
        else if (form is null && _showing is not null)
        {
            var focused = HasFocus;

            _showing = null;

            foreach (var field in Fields)
            {
                field.Visible = false;
            }

            Visible = false;

            if (focused)
            {
                SuperView?.SetFocus();
            }
        }

        if (form is not null && FieldFor(form.Typing) is { HasFocus: false } typing)
        {
            Focus(typing);
        }

        // After the bio has been shown or hidden, and on every change of the shell's — a profile switch, or more people
        // arriving — since either can change what the word being typed matches.
        _mentions.Follow();

        SetNeedsDraw();
    }

    /// <summary>The fields shown and filled from <paramref name="form" />, the typing where it says.</summary>
    private void Fill(PublicProfileScreen form)
    {
        // Terminal.Gui hands focus to a field as it is shown, which would tell the form the typing went there.
        _filling = true;
        _showing = form;
        Visible = true;

        while (_labels.Count < form.FieldRows)
        {
            AddRow(_labels.Count);
        }

        // Laid out before they are filled: where each goes is read only on a layout pass, and an editor still at the
        // nothing it was sized to while no form was up wraps its text to no width (as compose's does, #365).
        _name.Visible = _bio.Visible = true;

        foreach (var toggle in _toggles.Values.Concat(_pictures.Values))
        {
            toggle.Visible = true;
        }

        for (var row = 0; row < _labels.Count; row++)
        {
            _labels[row].Visible = _values[row].Visible = row < form.FieldRows;
        }

        foreach (var field in Fields.Where(field => field.Visible))
        {
            field.Layout();
        }

        _name.Text = form.DisplayName;
        _bio.Text = form.Bio;

        for (var row = 0; row < form.FieldRows; row++)
        {
            _labels[row].Text = form.LabelOf(row);
            _values[row].Text = form.ValueOf(row);
        }

        Focus(FieldFor(form.Typing));
        _filling = false;
    }

    /// <summary>
    ///     The typing into <paramref name="field" />, the caret at the end of what it holds: a field walked into is
    ///     added to, as compose's are, and never has what it holds selected for the first letter to replace.
    /// </summary>
    private static void Focus(View field)
    {
        field.SetFocus();

        switch (field)
        {
            case PublicProfileTextField text:
                text.MoveEnd();

                break;

            case ComposeEditor editor:
                editor.MoveEnd();

                break;
        }
    }

    /// <summary>A custom-field row's label and value fields, for the <paramref name="row" />th row.</summary>
    private void AddRow(int row)
    {
        var label = Field(PublicProfileScreen.LabelHint, new PublicProfileStop(PublicProfilePart.Label, row));
        var value = Field(PublicProfileScreen.ValueHint, new PublicProfileStop(PublicProfilePart.Value, row));

        label.ValueChanged += (_, _) => Change(form => form.RewriteLabel(row, label.Text));
        value.ValueChanged += (_, _) => Change(form => form.RewriteValue(row, value.Text));

        _labels.Add(label);
        _values.Add(value);

        Add(label, value);
    }

    /// <summary>The widget the typing going into <paramref name="stop" /> is focused on.</summary>
    private View FieldFor(PublicProfileStop stop) => stop.Part switch
    {
        PublicProfilePart.DisplayName => _name,
        PublicProfilePart.Bio => _bio,
        PublicProfilePart.Label => _labels[stop.Row],
        PublicProfilePart.Value => _values[stop.Row],
        PublicProfilePart.Avatar or PublicProfilePart.Header => _pictures[stop.Part],
        _ => _toggles[stop.Part],
    };

    /// <summary>A one-line field at <paramref name="stop" />, hinting <paramref name="hint" /> while it is empty.</summary>
    private PublicProfileTextField Field(string hint, PublicProfileStop stop)
    {
        var field = Placed(new PublicProfileTextField(_theme, hint, Save, Back, () => Walk(1)), stop);

        field.Answering = Answered;
        field.HasFocusChanged += (_, focus) => TypingMoved(focus.NewValue, stop);

        // ctrl-p is the frame's here as everywhere (ADR-0020).
        field.KeyBindings.Remove(Key.P.WithCtrl);

        return field;
    }

    /// <summary>
    ///     <paramref name="field" />, hidden until the form comes on top and laid wherever the form puts
    ///     <paramref name="stop" /> in this view's viewport — nowhere while no form is up.
    /// </summary>
    private T Placed<T>(T field, PublicProfileStop stop)
        where T : View
    {
        Rectangle Laid() =>
            Viewport.Width > 0 && _shell.Screen is PublicProfileScreen form
                ? form.At(stop, Viewport.Size, _raster())
                : Rectangle.Empty;

        field.X = Pos.Func(_ => Laid().X);
        field.Y = Pos.Func(_ => Laid().Y);
        field.Width = Dim.Func(_ => Laid().Width);
        field.Height = Dim.Func(_ => Laid().Height);
        field.Visible = false;

        return field;
    }

    /// <summary>A change to the form from one of its fields, while it is not being filled.</summary>
    private void Change(Func<PublicProfileScreen, ComposeChange> change)
    {
        if (!_filling)
        {
            _ = _shell.ChangePublicProfile(change);
        }
    }

    /// <summary>The typing on by <paramref name="by" />: <c>↑</c> off the bio's first line, <c>↓</c> off its last, or <c>⏎</c>.</summary>
    private void Walk(int by) => _ = _shell.ChangePublicProfile(form => form.Walk(by));

    /// <summary>A click on the toggle at <paramref name="stop" />: the walk there, and it flipped.</summary>
    private void Flip(PublicProfileStop stop)
    {
        _ = _shell.ChangePublicProfile(form => form.TypeInto(stop));
        _ = _shell.ChangePublicProfile(form => form.Flip(stop.Part));
    }

    /// <summary>A click on the avatar or the header at <paramref name="stop" />: the walk there, and the file browser for it.</summary>
    private void Choose(PublicProfileStop stop)
    {
        _ = _shell.ChangePublicProfile(form => form.TypeInto(stop));
        _shell.Browse();
    }

    /// <summary><c>ctrl-s</c> in any of the form's fields.</summary>
    private void Save() => _ = _shell.SavePublicProfile();

    /// <summary><c>esc</c> in any of the form's fields.</summary>
    private void Back() => _shell.Back();

    /// <summary>
    ///     One of the fields gained focus — by a click, or by <see cref="Refresh" /> moving it — and the form is brought
    ///     into step where it says the typing is somewhere else.
    /// </summary>
    private void TypingMoved(bool gained, PublicProfileStop stop)
    {
        if (gained && !_filling && _showing is not null)
        {
            _ = _shell.ChangePublicProfile(form => form.TypeInto(stop));
        }
    }
}
