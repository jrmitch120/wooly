using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Wooly.Tui.Prototype;
using Terminal.Gui.Views;

namespace Wooly.Tui.Views;

// Terminal.Gui 2.4 marks TextView superseded by a separate Editor package. That package is a new NuGet dependency, and
// this project takes none without an ADR saying why (#28's conventions) — so the shipped, still-working editor is used
// and the warning is turned off here, at the one place that touches it, rather than for the assembly.
#pragma warning disable CS0618

/// <summary>
///     The editor a post is written in. A <see cref="TextView" /> with three keys taken off it: the three that are the
///     shell's rather than the editor's.
/// </summary>
/// <remarks>
///     Taken off by overriding the hook that runs before the editor sees a key, because a focused view consumes what
///     it handles and an ancestor's handler only ever sees what was left. That is why <c>esc</c> reaches the shell
///     from inside an editor at all.
/// </remarks>
/// <param name="warn">
///     <c>ctrl-w</c>: what moves the typing to the content warning over this post (#123). Taken off here for the same
///     reason the other two are — a <see cref="TextView" /> has its own uses for a control key, and the field above is
///     not one of them.
/// </param>
internal sealed class ComposeEditor(Action send, Action cancel, Action warn) : TextView
{
    // PROTOTYPE: the variant's colours for the editor (null = Terminal.Gui's own), and its dim placeholder.
    public Func<VisualRole, Terminal.Gui.Drawing.Attribute?>? Colours { get; set; }

    public Func<string?>? Placeholder { get; set; }

    public Terminal.Gui.Drawing.Attribute PlaceholderColour { get; set; }

    protected override bool OnGettingAttributeForRole(in VisualRole role, ref Terminal.Gui.Drawing.Attribute currentAttribute)
    {
        if (Colours?.Invoke(role) is { } colour)
        {
            currentAttribute = colour;

            return true;
        }

        return base.OnGettingAttributeForRole(role, ref currentAttribute);
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        var drawn = base.OnDrawingContent(context);

        if (Text.Length == 0 && Placeholder?.Invoke() is { } placeholder)
        {
            SetAttribute(PlaceholderColour);
            AddStr(0, 0, placeholder.Length > Viewport.Width ? placeholder[..Math.Max(0, Viewport.Width)] : placeholder);
        }

        return drawn;
    }

    // PROTOTYPE: first refusal on every key, for the mention list while it is open.
    public Func<Key, bool>? Intercept { get; set; }

    protected override bool OnKeyDown(Key key)
    {
        if (Intercept?.Invoke(key) == true)
        {
            return true;
        }

        // PROTOTYPE: F2 / F3 cycle the compose variants.
        if (key == Key.F2 || key == Key.F3)
        {
            ComposeVariants.Cycle(key == Key.F2 ? -1 : 1);

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
            warn();

            return true;
        }

        return base.OnKeyDown(key);
    }
}
