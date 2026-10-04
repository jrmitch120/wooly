using Wooly.Tui.Theme;

namespace Wooly.Tui.Views;

/// <summary>
///     The content warning over a post being written (#320): a one-line field laid over the warning header's value
///     column, the way <see cref="ComposeEditor" /> is laid over the body — so the warning selects, moves by word, takes
///     a paste and takes the mouse exactly as the post does. The same three keys are taken off it that are taken off the
///     editor, and <c>enter</c> besides.
/// </summary>
/// <remarks>
///     What is typed is in <see cref="Role.ContentWarning" />, the role a warned post's own warning is drawn in, so a
///     warning being written looks like the warning it will become.
/// </remarks>
/// <param name="warn">
///     <c>ctrl-w</c> and <c>enter</c>: what hands the typing back to the post. <c>enter</c> because a warning is one
///     line, and finishing a one-line field is finishing it.
/// </param>
internal sealed class ComposeWarningField(
    ITheme theme,
    Func<string> hint,
    Action send,
    Action cancel,
    Action warn) : ComposeHeaderField(theme, Role.ContentWarning, hint, send, cancel, warn)
{
    protected override void Entered() => Warn();
}
