using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Prototype;

// PROTOTYPE — throwaway. The floating bar that says which skin is showing: loud on purpose, so it is never mistaken
// for part of a design. < and > cycle; so does clicking its arrows. Only built in a Debug build.

internal sealed class SkinSwitcher : View
{
    private readonly ITheme _theme;

    public SkinSwitcher(ITheme theme)
    {
        _theme = theme;
        CanFocus = false;
        Height = 1;
        X = Pos.Center();
        Y = Pos.AnchorEnd(3);
        Fit();
    }

    private static string Label => $" ◀  {Skins.Label} — {Skins.Current.Inspiration}  ▶ ";

    private const string Hint = "  < > ";

    public void Fit() => Width = Label.Length + Hint.Length;

    protected override bool OnDrawingContent(DrawContext? context)
    {
        SetAttribute(_theme.For(Proto.Switcher));
        AddStr(0, 0, Label);
        SetAttribute(_theme.For(Proto.SwitcherHint));
        AddStr(Label.Length, 0, Hint);

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
            Skins.Cycle(-1);
        }
        else if (at.X >= Label.Length - 4 && at.X < Label.Length)
        {
            Skins.Cycle(1);
        }

        return true;
    }
}
