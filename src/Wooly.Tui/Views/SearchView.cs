using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Views;

/// <summary>
///     The search field (#608), in one view laid over the content viewport as the drafts' are (ADR-0030): shown and
///     taking letters while the search prompt is on top, and laid wherever the prompt puts it
///     (<see cref="SearchScreen.FieldAt" />).
/// </summary>
/// <remarks>
///     What has been typed is the field's, not the prompt's: it stays in the field while what the prompt opened is in
///     front of it, so <c>esc</c> or the <c>Search</c> crumb back to the prompt finds the query as it was left, the caret
///     at its end — and a prompt the field has not shown before is a fresh one, arrived at by the rail or <c>/</c>, which
///     empties it (#600).
///     <para>
///         Unlike a draft's, the field does not hold the content panel still while it shows (<c>ShellWindow</c>): it is
///         the top of a screen that goes on below it, which is read by scrolling (#605). Nothing else here is a draft's
///         either — a right click over it is no <c>esc</c> of a draft's, and there is nothing to discard.
///     </para>
///     <para>
///         The prompt's label and the sentence under it are the prompt's rows, painted by the content panel behind this
///         view, which draws nothing of its own and lets every click it has no field under through to the panel.
///     </para>
/// </remarks>
internal sealed class SearchView : View
{
    private readonly Shell.Shell _shell;

    private readonly SearchField _field;

    /// <summary>
    ///     The prompt the field was last shown on — kept while something opened from it is in front, so that coming back
    ///     to it can be told apart from arriving at a fresh one.
    /// </summary>
    private SearchScreen? _showing;

    /// <param name="theme">What every colour the field is asked for comes from.</param>
    /// <param name="shell">Whose screen is on top, and what <c>⏎</c> asks.</param>
    /// <param name="room">
    ///     How wide the screen on top is drawn in a viewport so wide — the width the prompt's rows, and so its field,
    ///     are laid out in.
    /// </param>
    public SearchView(ITheme theme, Shell.Shell shell, Func<Screen, int, int> room)
    {
        _shell = shell;

        CanFocus = true;
        Visible = false;
        ViewportSettings |= ViewportSettingsFlags.Transparent | ViewportSettingsFlags.TransparentMouse;

        _field = new Placement<SearchScreen>(this, shell).Placed(
            new SearchField(theme, shell.Find, LeftForTheFrame),
            (prompt, size) => prompt.FieldAt(room(prompt, size.Width)));

        // ctrl-p is the frame's here as everywhere (ADR-0020).
        _field.KeyBindings.Remove(Key.P.WithCtrl);

        // A question open on the status row takes every key ahead of the field, as it does ahead of compose's (#373).
        _field.Answering = key => shell.StatusRow.Answered(ShellKeys.Of(key));

        Add(_field);

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
    ///     Whether <paramref name="key" />, which the field would type, means something to the frame on the prompt that is
    ///     never typed into a query: <c>\</c> where it shows and hides the rail, and <c>[</c> and <c>]</c>, which move
    ///     between sections. What they mean is the keymap's, read rather than restated.
    /// </summary>
    private bool LeftForTheFrame(Key key) =>
        ShellKeys.Of(key) is { } pressed
        && Keymap.Means(pressed, _shell.Screen) is Verb.ToggleRail or Verb.NextSection or Verb.PreviousSection;

    /// <summary>
    ///     Shows the field as a prompt comes on top — emptied, where it is a prompt it has not shown before — with the
    ///     caret at the end of what it holds; and takes it away while anything else is in front.
    /// </summary>
    private void Refresh()
    {
        var prompt = _shell.Screen as SearchScreen;

        if (prompt is not null)
        {
            // A fresh prompt can replace the one showing without anything coming between: the rail's Search clicked
            // while it is shown, with something typed.
            if (!ReferenceEquals(prompt, _showing))
            {
                _showing = prompt;
                _field.Text = string.Empty;
            }

            if (!_field.Visible)
            {
                Visible = true;
                _field.Visible = true;
                _field.Layout();
                _field.MoveEnd();
            }

            if (!_field.HasFocus)
            {
                _field.SetFocus();
            }
        }
        else if (prompt is null && _field.Visible)
        {
            var focused = HasFocus;

            _field.Visible = false;
            Visible = false;

            if (focused)
            {
                SuperView?.SetFocus();
            }
        }

        SetNeedsDraw();
    }
}
