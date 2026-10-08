using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Views;

/// <summary>
///     The description editor's field (#377), in one view laid over the content viewport as compose's are
///     (<see cref="ComposeView" />): shown while a <see cref="DescriptionScreen" /> is on top, opened on what the
///     attachment's description says, and laid wherever the screen puts it. Every change goes through the shell
///     (<see cref="Shell.Shell.WriteDescription" />).
/// </summary>
/// <remarks>
///     The label, the counter and the picture's place are the screen's rows, painted by the content panel behind this
///     view, which draws nothing of its own and lets every click it has no field under through to the panel.
/// </remarks>
internal sealed class DescriptionView : View
{
    private readonly Shell.Shell _shell;

    /// <summary>
    ///     The field, the compose editor's own kind: <c>esc</c> and <c>ctrl-s</c> are both "done" here, there being no
    ///     cancel, and <c>ctrl-w</c> and <c>↑</c> off its first line have no field to move to.
    /// </summary>
    private readonly ComposeEditor _editor;

    public DescriptionView(ITheme theme, Shell.Shell shell)
    {
        _shell = shell;

        CanFocus = true;
        Visible = false;
        ViewportSettings |= ViewportSettingsFlags.Transparent | ViewportSettingsFlags.TransparentMouse;

        _editor = new ComposeEditor(theme, DescriptionScreen.EmptyHint, Done, Done, () => { }, () => { })
        {
            WordWrap = true,
            TabKeyAddsTab = false,
            Visible = false,
            X = Pos.Func(_ => Laid().X),
            Y = Pos.Func(_ => Laid().Y),
            Width = Dim.Func(_ => Laid().Width),
            Height = Dim.Func(_ => Laid().Height),
        };

        // ctrl-p is the frame's here as everywhere (ADR-0020), where TextView would take it for a line up.
        _editor.KeyBindings.Remove(Key.P.WithCtrl);

        // A question open on the status row takes every key ahead of the field — leaving by tab asks before a touched
        // draft under this is thrown away (#373).
        _editor.Answering = key =>
        {
            if (_shell.Asking is null)
            {
                return false;
            }

            _ = _shell.Answer(ShellKeys.Of(key));

            return true;
        };

        _editor.ContentsChanged += (_, _) => _shell.WriteDescription(_editor.Text);

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

    /// <summary>Where the screen on top lays the field, or nowhere while it is not the description editor.</summary>
    private System.Drawing.Rectangle Laid() =>
        Viewport.Width > 0 && _shell.Screen is DescriptionScreen describing
            ? describing.FieldAt(Viewport.Size)
            : System.Drawing.Rectangle.Empty;

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
