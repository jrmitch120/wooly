using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Wooly.Tui.Media;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Views;

/// <summary>
///     The description editor's field (#377), in one view laid over the content viewport as compose's are
///     (<see cref="ComposeView" />): shown while a <see cref="DescriptionScreen" /> is on top, opened on what the
///     attachment's description says, and laid wherever the screen puts it. Every change goes through drafting
///     (<see cref="Drafting.Drafting.Edit{T}" />).
/// </summary>
/// <remarks>
///     The label, the counter and the picture's place are the screen's rows, painted by the content panel behind this
///     view, which draws nothing of its own and lets every click it has no field under through to the panel.
/// </remarks>
internal sealed class DescriptionView : View
{
    private readonly Shell.Shell _shell;

    /// <summary>What every change to the description goes through.</summary>
    private readonly Drafting.Drafting _drafting;

    /// <summary>
    ///     The field, the compose editor's own kind: <c>esc</c> and <c>ctrl-s</c> are both "done" here, there being no
    ///     cancel, and <c>ctrl-w</c> and <c>↑</c> off its first line have no field to move to.
    /// </summary>
    private readonly ComposeEditor _editor;

    /// <summary>
    ///     How the content panel under this paints pixels as of its last frame, which says whether the picture takes a
    ///     place above the field on a narrow panel (#382).
    /// </summary>
    private readonly Func<Raster> _raster;

    /// <summary>What the field does with an open question and with the mouse, as every draft's do (#373, #430).</summary>
    private readonly DraftFields _draft;

    public DescriptionView(ITheme theme, Shell.Shell shell, Drafting.Drafting drafting, Func<Raster> raster)
    {
        _shell = shell;
        _drafting = drafting;
        _raster = raster;
        var place = new Placement<DescriptionScreen>(this, shell);

        CanFocus = true;
        Visible = false;
        ViewportSettings |= ViewportSettingsFlags.Transparent | ViewportSettingsFlags.TransparentMouse;

        _editor = place.Placed(
            new ComposeEditor(theme, DescriptionScreen.EmptyHint, Done, Done, () => { }, () => { })
            {
                WordWrap = true,
                TabKeyAddsTab = false,
            },
            (describing, room) => describing.FieldAt(room, _raster()));

        // ctrl-p is the frame's here as everywhere (ADR-0020), where TextView would take it for a line up.
        _editor.KeyBindings.Remove(Key.P.WithCtrl);

        // A right click over the field is esc, which the field would otherwise keep to itself, and a press over it
        // while a question is open is the question's — as on compose and the form (#373, #430).
        _draft = new DraftFields(this, shell, () => _shell.Screen is DescriptionScreen, () => [_editor]);

        // A question open on the status row takes every key ahead of the field — leaving by tab asks before a touched
        // draft under this is thrown away (#373).
        _editor.Answering = _draft.Answered;

        _editor.ContentsChanged += (_, _) =>
            _ = _drafting.Edit<DescriptionScreen>(describing => describing.Rewrite(_editor.Text));

        Add(_editor);

        _shell.Changed += Refresh;

        Refresh();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _shell.Changed -= Refresh;
        }

        base.Dispose(disposing);
    }

    /// <summary>
    ///     Shows the field as the description editor comes on top, filled from the attachment, and takes it away as the
    ///     editor goes.
    /// </summary>
    private void Refresh()
    {
        var describing = _shell.Screen as DescriptionScreen;

        if (describing is not null && !_editor.Visible)
        {
            Visible = true;
            _editor.Visible = true;
            _editor.Layout();
            _editor.Text = describing.Description;
            _editor.SetFocus();
            _editor.MoveEnd();
        }
        else if (describing is null && _editor.Visible)
        {
            var focused = HasFocus;

            _editor.Visible = false;
            Visible = false;

            if (focused)
            {
                SuperView?.SetFocus();
            }
        }

        SetNeedsDraw();
    }

    /// <summary><c>esc</c> or <c>ctrl-s</c> in the field: done, keeping what was typed.</summary>
    private void Done() => _shell.Back();
}
