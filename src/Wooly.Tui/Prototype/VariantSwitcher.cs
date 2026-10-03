using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace Wooly.Tui.Prototype;

// PROTOTYPE — throwaway. The floating bar that says which compose variant is showing: loud on purpose (black on
// yellow), so it is never mistaken for part of a design. F2 / F3 cycle; so does clicking its arrows. Debug builds only.

internal sealed class VariantSwitcher : View
{
    private static readonly Terminal.Gui.Drawing.Attribute Loud = new(new Color(0, 0, 0), new Color(249, 226, 175));
    private static readonly Terminal.Gui.Drawing.Attribute Hint = new(new Color(249, 226, 175), new Color(30, 30, 46));

    private const string Keys = " F2 / F3 ";

    public VariantSwitcher()
    {
        CanFocus = false;
        Height = 1;
        X = Pos.Center();
        Y = Pos.AnchorEnd(2);
        Fit();
    }

    private static string Label => $" ◀  {ComposeVariants.Label} · {ComposeVariants.Current.Inspiration}  ▶ ";

    public void Fit() => Width = Label.Length + Keys.Length;

    protected override bool OnDrawingContent(DrawContext? context)
    {
        SetAttribute(Loud);
        AddStr(0, 0, Label);
        SetAttribute(Hint);
        AddStr(Label.Length, 0, Keys);

        return true;
    }

    protected override bool OnMouseEvent(Mouse mouse)
    {
        if (!mouse.IsSingleClicked || mouse.Position is not { } at)
        {
            return false;
        }

        if (at.X < 4)
        {
            ComposeVariants.Cycle(-1);
        }
        else if (at.X >= Label.Length - 4 && at.X < Label.Length)
        {
            ComposeVariants.Cycle(1);
        }

        return true;
    }
}
