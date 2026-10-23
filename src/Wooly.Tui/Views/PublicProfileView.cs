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
///     Every change goes through drafting's one way in (<see cref="Drafting.Drafting.Edit{T}" />).
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
    private readonly Drafting.Drafting _drafting;

    private readonly PublicProfileTextField _name;
    private readonly ComposeEditor _bio;
    private readonly List<PublicProfileTextField> _labels = [];
    private readonly List<PublicProfileTextField> _values = [];

    /// <summary>The rows that take no typing — the avatar and the header, and the three toggles — by which they are.</summary>
    private readonly Dictionary<PublicProfilePart, PublicProfileButton> _buttons = [];

    /// <summary>How this terminal paints pixels, which says how tall the avatar's and the header's rows are.</summary>
    private readonly Func<Raster?> _raster;

    /// <summary>
    ///     Where the pixels come from, which with <see cref="_raster" /> says whether the pictures are drawn at all
    ///     (<see cref="Rendering.Drawing.Draws" />).
    /// </summary>
    private readonly IPictures? _pictures;

    /// <summary>The people to mention, hung under the @-word being typed in the bio, as under compose's post (#408).</summary>
    private readonly MentionList _mentions;

    /// <summary>The form the fields were last filled from, or none while no form is on top.</summary>
    private PublicProfileScreen? _showing;

    /// <summary>Whether the fields are being filled as the form comes on top, while focus moving says nothing.</summary>
    private bool _filling;

    /// <summary>What the fields do with an open question and with the mouse, as every draft's do (#373).</summary>
    private readonly DraftFields _draft;

    /// <summary>Where each field is laid: wherever the form puts its stop (ADR-0030).</summary>
    private readonly Placement<PublicProfileScreen> _place;

    public PublicProfileView(
        ITheme theme,
        Shell.Shell shell,
        Drafting.Drafting drafting,
        Func<Raster?>? raster = null,
        IPictures? pictures = null)
    {
        _theme = theme;
        _shell = shell;
        _drafting = drafting;
        _raster = raster ?? (() => null);
        _pictures = pictures;
        _place = new Placement<PublicProfileScreen>(this, shell);

        // A right click over a field is esc, which the fields would otherwise keep to themselves, and a click outside
        // the list of people to mention closes it, as on compose (#373, #335). The list hangs under the bio, which
        // answers a question through these rules, so it is made after them.
        _draft = new DraftFields(
            this,
            shell,
            () => _shell.Screen is PublicProfileScreen,
            () => Fields,
            mouse => _mentions?.ClosedBy(mouse) is true);

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
        _bio.Answering = _draft.Answered;
        _bio.ContentsChanged += (_, _) => Change(form => form.RewriteBio(_bio.Text));

        _mentions = new MentionList(theme, shell, _bio, () => _shell.Screen is PublicProfileScreen);
        _bio.Ahead = _mentions.Took;

        // The rows that take no typing: the avatar and the header, a click on either opening the file browser for it
        // (#407), and the toggles, a click on one flipping it.
        IEnumerable<(PublicProfilePart Part, Action<PublicProfileStop> Pressed)> buttons =
        [
            .. PublicProfilePicture.Both.Select(picture => (picture.Part, (Action<PublicProfileStop>)Choose)),
            .. PublicProfileToggle.All.Select(toggle => (toggle.Part, (Action<PublicProfileStop>)Flip)),
        ];

        foreach (var (part, pressed) in buttons)
        {
            var stop = new PublicProfileStop(part);
            var button = Placed(new PublicProfileButton(() => pressed(stop)), stop);

            button.Answering = _draft.Answered;
            button.HasFocusChanged += (_, focus) => TypingMoved(focus.NewValue, stop);

            _buttons[part] = button;
        }

        _bio.HasFocusChanged += (_, focus) => TypingMoved(focus.NewValue, new PublicProfileStop(PublicProfilePart.Bio));

        Add(_name, _bio);
        Add([.. _buttons.Values]);
        Add(_mentions.View);

        _shell.Changed += Refresh;

        Refresh();
    }

    /// <summary>
    ///     The keys no field took that mean something on the form, made here as the keymap says they do rather than
    ///     left to Terminal.Gui: <c>↑</c>/<c>↓</c>, which walk the fields and stop at either end, <c>space</c> and
    ///     <c>⏎</c> on a toggle, <c>⏎</c> and the paste on a picture (#407), and <c>ctrl-s</c>.
    ///     <para>
    ///         And <c>←</c>/<c>→</c> left over — on a picture, a toggle, or a field's caret at the end of what is written —
    ///         taken and spent on nothing: let through, they reach Terminal.Gui's own arrow navigation, which moved the
    ///         focus to whichever field came next in its order, the avatar's <c>←</c> landing in the bio.
    ///     </para>
    /// </summary>
    protected override bool OnKeyDown(Key key) =>
        key == Key.CursorLeft || key == Key.CursorRight
        || (ShellKeys.Of(key) is { } pressed
            && Keymap.Means(pressed, _shell.Screen) is var verb
                and (Verb.PreviousPublicProfileField or Verb.NextPublicProfileField or Verb.FlipToggle
                    or Verb.SavePublicProfile or Verb.ChoosePicture or Verb.PastePicture)
            && _shell.Do(verb, Keymap.Answer(pressed)));

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _shell.Changed -= Refresh;
        }

        base.Dispose(disposing);
    }

    /// <summary>Every field and toggle, in the order the arrows walk them.</summary>
    private IEnumerable<View> Fields =>
    [
        _name, _bio, .. PublicProfilePicture.Both.Select(picture => _buttons[picture.Part]),
        .. _labels.Zip(_values).SelectMany(row => new View[] { row.First, row.Second }),
        .. PublicProfileToggle.All.Select(toggle => _buttons[toggle.Part]),
    ];

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

        foreach (var button in _buttons.Values)
        {
            button.Visible = true;
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
        _ => _buttons[stop.Part],
    };

    /// <summary>A one-line field at <paramref name="stop" />, hinting <paramref name="hint" /> while it is empty.</summary>
    private PublicProfileTextField Field(string hint, PublicProfileStop stop)
    {
        var field = Placed(
            new PublicProfileTextField(
                _theme,
                hint,
                Save,
                Back,
                WalkOn,
                stop.Part == PublicProfilePart.Value ? () => Across(-1) : null,
                stop.Part == PublicProfilePart.Label ? () => Across(1) : null),
            stop);

        field.Answering = _draft.Answered;
        field.HasFocusChanged += (_, focus) => TypingMoved(focus.NewValue, stop);

        // ctrl-p is the frame's here as everywhere (ADR-0020).
        field.KeyBindings.Remove(Key.P.WithCtrl);

        return field;
    }

    /// <summary>
    ///     <paramref name="widget" />, hidden until the form comes on top and laid wherever the form puts
    ///     <paramref name="stop" /> in this view's viewport — nowhere while no form is up.
    /// </summary>
    /// <remarks>
    ///     Kept, where compose and the description editor use their placement inline, because every widget here is
    ///     laid by its stop the same way and a custom field's row is placed long after the constructor, as it is added.
    /// </remarks>
    private T Placed<T>(T widget, PublicProfileStop stop)
        where T : View =>
        _place.Placed(widget, (form, room) => form.At(stop, room, _raster(), _pictures));

    /// <summary>A change to the form from one of its fields, while it is not being filled.</summary>
    private void Change(Func<PublicProfileScreen, DraftChange> change)
    {
        if (!_filling)
        {
            _ = _drafting.Edit(change);
        }
    }

    /// <summary>The typing on by <paramref name="by" />: <c>↑</c> off the bio's first line, or <c>↓</c> off its last.</summary>
    private void Walk(int by) => _ = _drafting.Edit<PublicProfileScreen>(form => form.Walk(by));

    /// <summary><c>⏎</c> in a one-line field: the typing on to the next field in the order the form is read.</summary>
    private void WalkOn() => _ = _drafting.Edit<PublicProfileScreen>(form => form.WalkOn());

    /// <summary>
    ///     The typing across a custom field's row, <paramref name="by" /> <c>→</c> from its label or <c>←</c> from its
    ///     value — the caret put at the edge it came in by, so that the arrow back goes straight back.
    /// </summary>
    private void Across(int by)
    {
        if (_drafting.Edit<PublicProfileScreen>(form => form.Across(by)) != DraftChange.Moved || by < 0)
        {
            return;
        }

        if (MostFocused is PublicProfileTextField value)
        {
            value.InsertionPoint = 0;
        }
    }

    /// <summary>A click on the toggle at <paramref name="stop" />: the walk there, and it flipped.</summary>
    private void Flip(PublicProfileStop stop)
    {
        _ = _drafting.Edit<PublicProfileScreen>(form => form.TypeInto(stop));
        _ = _drafting.Edit<PublicProfileScreen>(form => form.Flip(stop.Part));
    }

    /// <summary>A click on the avatar or the header at <paramref name="stop" />: the walk there, and the file browser for it.</summary>
    private void Choose(PublicProfileStop stop)
    {
        _ = _drafting.Edit<PublicProfileScreen>(form => form.TypeInto(stop));
        _ = _drafting.Do(Verb.ChoosePicture);
    }

    /// <summary><c>ctrl-s</c> in any of the form's fields.</summary>
    private void Save() => _ = _drafting.Do(Verb.SavePublicProfile);

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
            _ = _drafting.Edit<PublicProfileScreen>(form => form.TypeInto(stop));
        }
    }
}
