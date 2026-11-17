using Terminal.Gui.Input;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Views;

/// <summary>
///     What is being searched for (#608): a one-line field laid over the search prompt's box (#605), as compose's
///     content warning is laid over its header (#320), so a query is edited and selected rather than only added to and
///     taken back from the end — the caret moved with <c>←</c>, <c>→</c>, <c>home</c> and <c>end</c>, a selection made
///     with the shift-arrows or a drag of the mouse and typed over, and copied, cut and pasted by the keys compose's
///     fields use.
/// </summary>
/// <remarks>
///     Every printable key is a letter here, <c>/</c> and <c>?</c> among them, which a web address and a question are
///     entitled to. The few the frame means something by from the field too are left for the window rather than typed
///     (<paramref name="leaves" />): <c>\</c>, which shows and hides the rail from every prompt (#447), and <c>[</c> and
///     <c>]</c>, which move between sections and are never typed into a query (#605). Anything else the field has no
///     use for — <c>tab</c>, <c>esc</c>, <c>ctrl-p</c>, the arrows up and down — goes on to the window as it would from
///     any screen.
///     <para>
///         It takes letters only while it is picked, the top of the prompt's walk; with a trend under it picked, it
///         takes no focus and draws what it holds dimmed, with no caret (#605). A press of the mouse on it then picks
///         it (<paramref name="picking" />), as a press on any row picks what the row is part of.
///     </para>
/// </remarks>
/// <param name="theme">What every colour the field is asked for comes from.</param>
/// <param name="entered"><c>⏎</c>: what the field holds, asked for — from wherever the caret is.</param>
/// <param name="leaves">Whether a key the field would type is the frame's instead, and left for the window.</param>
/// <param name="picking">A press on the field while a trend is picked: the field picked, and taking letters.</param>
internal sealed class SearchField(ITheme theme, Action<string> entered, Func<Key, bool> leaves, Action picking)
    : OneLineField(theme, Role.Body, () => SearchScreen.Placeholder)
{
    /// <inheritdoc />
    /// <remarks>Dimmed while it is not taking letters, as its box is.</remarks>
    protected override Role TextRole => CanFocus ? Role.Body : Role.Muted;

    protected override bool Takes(Key key)
    {
        if (key == Key.Enter)
        {
            entered(Text);

            return true;
        }

        // Pasted by hand, since alt-v is no paste of TextField's own: it is compose's paste besides ctrl-v, for the
        // terminals that keep ctrl-v for their own (#380), and the same key does the same thing in every field.
        if (key == Key.V.WithAlt)
        {
            Paste();

            return true;
        }

        return false;
    }

    /// <summary>
    ///     A key that would be typed, unless it is one the frame takes from the field (<see cref="SearchField" />'s
    ///     remarks): left unhandled, it goes on to the window.
    /// </summary>
    protected override bool OnKeyDownNotHandled(Key key) => !leaves(key) && base.OnKeyDownNotHandled(key);

    /// <summary>
    ///     A right click opens no menu of the field's own: the shell has no menus, and a right click on a field means
    ///     what it means on compose's, which is nothing in it. A press while the field is not taking letters picks it
    ///     first, so that the caret lands where it was pressed in a field that now takes them.
    /// </summary>
    protected override bool OnMouseEvent(Mouse mouse)
    {
        if (ShellKeys.Of(mouse) is not null)
        {
            return true;
        }

        if (!CanFocus && mouse.Flags.HasFlag(MouseFlags.LeftButtonPressed))
        {
            picking();
        }

        return base.OnMouseEvent(mouse);
    }
}
