using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace Wooly.Tui.Views;

// Terminal.Gui 2.4 marks TextView superseded by a separate Editor package. That package is a new NuGet dependency, and
// this project takes none without an ADR saying why (#28's conventions) — so the shipped, still-working editor is used
// and the warning is turned off here, at the one place that touches it, rather than for the assembly.
#pragma warning disable CS0618

/// <summary>
///     The editor a post is written in. A <see cref="TextView" /> with two keys taken off it: the two that are the
///     shell's rather than the editor's.
/// </summary>
/// <remarks>
///     Taken off by overriding the hook that runs before the editor sees a key, because a focused view consumes what
///     it handles and an ancestor's handler only ever sees what was left. That is why <c>esc</c> reaches the shell
///     from inside an editor at all.
/// </remarks>
internal sealed class ComposeEditor(Action send, Action cancel) : TextView
{
    protected override bool OnKeyDown(Key key)
    {
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

        return base.OnKeyDown(key);
    }
}
