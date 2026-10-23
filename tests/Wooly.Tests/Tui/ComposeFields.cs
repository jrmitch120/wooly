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

    /// <summary>The first <typeparamref name="T" /> among the edit public profile form's fields in <paramref name="window" /> (#406).</summary>
    public static T ProfileField<T>(this View window)
        where T : View =>
        window.SubViews.OfType<PublicProfileView>().Single().SubViews.OfType<T>().First();

    /// <summary>The <typeparamref name="T" /> among the description editor's fields in <paramref name="window" /> (#377).</summary>
    public static T DescriptionField<T>(this View window)
        where T : View =>
        window.SubViews.OfType<DescriptionView>().Single().SubViews.OfType<T>().Single();
}
