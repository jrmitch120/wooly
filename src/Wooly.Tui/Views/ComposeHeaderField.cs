using Terminal.Gui.Input;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Views;

/// <summary>
///     A one-line field laid over a compose header's value column — the warning's (#320), Lang's (#340) — and the keys
///     compose means the same thing by in every such field. Its colours and its hint are every one-line field's
///     (<see cref="OneLineField" />).
/// </summary>
/// <remarks>
///     Its keys are its own before they bubble, which is how <c>?</c> and <c>c</c> are letters here without a rule of
///     the shell's: a focused view consumes what it handles, and the window only ever sees what was left — the arrows
///     and <c>tab</c> among them, which is how a header joins the walk between the fields with no key rule of its own
///     (#337).
/// </remarks>
/// <param name="theme">What every colour the field is asked for comes from, as for the editor (#316).</param>
/// <param name="text">The role what is typed is drawn in.</param>
/// <param name="hint">What the field says, dimly, while it is empty.</param>
/// <param name="send"><c>ctrl-s</c>.</param>
/// <param name="cancel"><c>esc</c>: the draft thrown away, as everywhere in compose.</param>
/// <param name="warn"><c>ctrl-w</c>: the typing into the warning, or back out of it.</param>
internal abstract class ComposeHeaderField(
    ITheme theme,
    Role text,
    Func<string> hint,
    Action send,
    Action cancel,
    Action warn) : OneLineField(theme, text, hint)
{
    /// <summary>
    ///     Asked of every key whether it is compose's paste from the clipboard — <c>ctrl-v</c>, or <c>alt-v</c> where
    ///     the terminal keeps <c>ctrl-v</c> — and if so, whether the clipboard held a picture or files it attached
    ///     (#380): <see langword="null" /> where the key is no paste, and otherwise the field's own paste follows
    ///     only where nothing was attached.
    /// </summary>
    public Func<Key, bool?>? FromTheClipboard { get; set; }

    /// <summary>
    ///     First refusal on every key, ahead of compose's own — nothing, unless a field has something hung under it whose
    ///     keys come first.
    /// </summary>
    protected virtual bool TakesFirst(Key key) => false;

    /// <summary><c>enter</c>: finishing a one-line field, which means something different in each.</summary>
    protected abstract void Entered();

    /// <summary><c>ctrl-w</c>'s meaning, for a field whose <c>enter</c> means it too.</summary>
    protected void Warn() => warn();

    protected override bool Takes(Key key)
    {
        if (TakesFirst(key))
        {
            return true;
        }

        if (key == Key.Esc)
        {
            cancel();

            return true;
        }

        if (key == Key.S.WithCtrl)
        {
            send();

            return true;
        }

        if (key == Key.W.WithCtrl)
        {
            Warn();

            return true;
        }

        // Pasted by hand rather than left to the key, since alt-v is no paste of TextField's own.
        if (FromTheClipboard?.Invoke(key) is { } attached)
        {
            if (!attached)
            {
                Paste();
            }

            return true;
        }

        if (key == Key.Enter)
        {
            Entered();

            return true;
        }

        return false;
    }

    /// <summary>
    ///     A right click is <c>esc</c>, which on a draft means nothing (#307), and opens no context menu either.
    /// </summary>
    protected override bool OnMouseEvent(Mouse mouse) => ShellKeys.Of(mouse) is not null || base.OnMouseEvent(mouse);
}
