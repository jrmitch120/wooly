using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Wooly.Tui.Prototype;

/// <summary>One shell to try, and the one-line pitch for it.</summary>
internal sealed record Variant(string Key, string Name, string Idea, Func<VariantWindow> Open);

internal static class Variants
{
    public static IReadOnlyList<Variant> All { get; } =
    [
        new("A", "Tabbed reader", "One column, tabs across the top, everything else is a modal screen.", () => new TabbedReader()),
        new("B", "Split reading pane", "Index left, whole post right — a mail reader for the timeline.", () => new SplitReadingPane()),
        new("C", "Workspace rail", "A rail of destinations, a feed, and a context pane. Nothing is modal.", () => new WorkspaceRail()),
        new("D", "Command bar", "No chrome but one line. Vim/weechat keys, and the CLI's own verbs on `:`.", () => new CommandBar()),

        // The C family: one design — rail, no right column, drill into a post or an account — with four ways of
        // choosing a destination. Watch the fetch count on the bottom row.
        new("C0", "Rail · cycle", "Tab walks the rail and every step loads. The baseline you are trying to beat.", () => new CycleRail()),
        new("C1", "Rail · highlight then enter", "A cursor on the rail that costs nothing to move. Enter commits.", () => new HighlightRail()),
        new("C2", "Rail · direct keys", "Every destination wears its key. One press, one fetch, no transit.", () => new DirectRail()),
        new("C3", "Rail · jump list", "`g`, then type enough of the name. Scales past the alphabet.", () => new PaletteRail()),
    ];

    public static int IndexOf(string? key)
    {
        var found = All.ToList().FindIndex(variant => string.Equals(variant.Key, key, StringComparison.OrdinalIgnoreCase));

        return found < 0 ? 0 : found;
    }
}

/// <summary>
///     The bar that is not part of the design. Deliberately loud and pinned to the last row so nobody mistakes it for
///     a thing the real TUI would have.
/// </summary>
internal sealed class PrototypeBar : View
{
    private readonly int _index;

    public PrototypeBar(int index)
    {
        _index = index;
        X = 0;
        Y = Pos.AnchorEnd(1);
        Width = Dim.Fill();
        Height = 1;
        CanFocus = false;
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        var variant = Variants.All[_index];
        var width = Viewport.Width;

        SetAttribute(Ink.Prototype);
        AddStr(0, 0, new string(' ', Math.Max(0, width)));

        var left = $" PROTOTYPE  ◀ F9   {variant.Key} — {variant.Name}   F10 ▶ ";
        AddStr(0, 0, Ink.Clip(left, width));

        var right = " F1 notes · Ctrl-Q quit ";

        if (width > left.Length + right.Length)
        {
            AddStr(width - right.Length, 0, right);
        }

        return true;
    }
}

/// <summary>
///     What every variant shares and nothing more: the loud bar, the keys that cycle variants, and a canvas that is
///     everything above the bar. Each variant lays that canvas out however it likes — there is no shared layout.
/// </summary>
internal abstract class VariantWindow : Window
{
    private readonly int _index;

    protected VariantWindow(int index)
    {
        _index = index;
        BorderStyle = LineStyle.None;
        SchemeName = "TopLevel";

        Canvas = new View
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),

            // Without this the canvas is skipped by focus navigation and nothing inside it ever sees a keystroke.
            CanFocus = true,
        };

        Add(Canvas);
        Add(new PrototypeBar(index));

        KeyDown += (_, key) =>
        {
            if (key == Key.F9)
            {
                Switch(-1);
                key.Handled = true;
            }
            else if (key == Key.F10)
            {
                Switch(1);
                key.Handled = true;
            }
            else if (key == Key.F1)
            {
                Notes();
                key.Handled = true;
            }
        };
    }

    /// <summary>Everything above the prototype bar.</summary>
    protected View Canvas { get; }

    /// <summary>Which variant to open next, or <c>-1</c> to stop.</summary>
    public int Next { get; private set; } = -1;

    private void Switch(int step)
    {
        Next = (_index + step + Variants.All.Count) % Variants.All.Count;
        GetApp()?.RequestStop();
    }

    private void Notes()
    {
        var variant = Variants.All[_index];

        MessageBox.Query(
            GetApp()!,
            60,
            9,
            $"{variant.Key} — {variant.Name}",
            $"{variant.Idea}\n\nF9/F10 switch shells. Nothing here talks to an instance:\nevery post, notification and DM is fake, and every action\njust says what it would have done.",
            "OK");
    }

    /// <summary>What a keypress would have done, said out loud instead of done.</summary>
    protected void Pretend(string what) => MessageBox.Query(GetApp()!, 58, 7, "Would have", $"{what}\n\n(Prototype — nothing was sent.)", "OK");

    /// <summary>The confirmation a delete has to pass (spec story 43), so the shells can be judged carrying it.</summary>
    protected void ConfirmDelete(string what)
    {
        var chosen = MessageBox.Query(GetApp()!, 58, 8, "Delete post?", $"{what}\n\nThis cannot be undone.", "Cancel", "Delete");

        if (chosen == 1)
        {
            Pretend("Deleted the post");
        }
    }
}
