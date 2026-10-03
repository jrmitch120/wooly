using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Wooly.Tui.Prototype;

// PROTOTYPE — throwaway. The content warning as a real one-line field, so it selects, moves and takes the mouse the way
// the post's editor does, rather than being painted text the shell types into. The same three keys taken off it as
// ComposeEditor, and F2 / F3 for the variants.

internal sealed class ComposeWarningField(Action send, Action cancel, Action warn) : TextField
{
    public Func<VisualRole, Terminal.Gui.Drawing.Attribute>? Colours { get; set; }

    public Func<string>? Placeholder { get; set; }

    public Terminal.Gui.Drawing.Attribute PlaceholderColour { get; set; }

    protected override bool OnGettingAttributeForRole(in VisualRole role, ref Terminal.Gui.Drawing.Attribute currentAttribute)
    {
        if (Colours is null)
        {
            return base.OnGettingAttributeForRole(role, ref currentAttribute);
        }

        currentAttribute = Colours(role);

        return true;
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        var drawn = base.OnDrawingContent(context);

        if (Text.Length == 0 && Placeholder?.Invoke() is { Length: > 0 } placeholder)
        {
            SetAttribute(PlaceholderColour);
            AddStr(0, 0, placeholder.Length > Viewport.Width ? placeholder[..Math.Max(0, Viewport.Width)] : placeholder);
        }

        return drawn;
    }

    protected override bool OnKeyDown(Key key)
    {
        if (key == Key.F2 || key == Key.F3)
        {
            ComposeVariants.Cycle(key == Key.F2 ? -1 : 1);

            return true;
        }

        // Enter, like ctrl-w, hands the typing back to the post: a warning is one line.
        if (key == Key.W.WithCtrl || key == Key.Enter)
        {
            warn();

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

        return base.OnKeyDown(key);
    }
}
