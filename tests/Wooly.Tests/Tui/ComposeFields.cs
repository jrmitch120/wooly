using Terminal.Gui.ViewBase;
using Wooly.Tui.Views;

namespace Wooly.Tests.Tui;

/// <summary>Where the shell's window keeps compose's fields: in the one <see cref="ComposeView" /> it adds (#365).</summary>
internal static class ComposeFields
{
    /// <summary>The <typeparamref name="T" /> among compose's fields in <paramref name="window" />.</summary>
    public static T ComposeField<T>(this View window)
        where T : View =>
        window.SubViews.OfType<ComposeView>().Single().SubViews.OfType<T>().Single();
}
