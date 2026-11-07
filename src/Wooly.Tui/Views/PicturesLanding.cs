using Terminal.Gui.App;

namespace Wooly.Tui.Views;

/// <summary>
///     What the screen does when a picture lands — fetched and decoded, or encoded for a Kitty terminal — on whatever
///     thread finished it: drawn again on the application's.
/// </summary>
/// <remarks>
///     Drawn again rather than told to the shell, because nothing about the shell has changed — the same rows are
///     wanted, with the box that was waiting now filled in. One draw for a burst, not one each: pictures arrive
///     together, and a draw per arrival is a frame per arrival.
///     <para>
///         Every view is drawn again, but the screen is not cleared first: a cleared screen is a frame that sends every
///         cell and every sixel on it again, and a scroll into posts whose custom emoji and avatars land a burst at a
///         time sent the whole page again for each burst. Drawn over what was there, only what changed is sent — the
///         picture that landed.
///     </para>
/// </remarks>
internal sealed class PicturesLanding(IApplication application)
{
    private int _drawing;

    /// <summary>A picture has landed: the screen is drawn again on the application's thread, once for a burst.</summary>
    public void Landed()
    {
        if (Interlocked.Exchange(ref _drawing, 1) == 1)
        {
            return;
        }

        application.Invoke(() =>
        {
            Interlocked.Exchange(ref _drawing, 0);
            application.TopRunnableView?.SetNeedsDraw();
            application.LayoutAndDraw();
        });
    }
}
