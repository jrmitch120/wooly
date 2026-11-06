using Terminal.Gui.ViewBase;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;

namespace Wooly.Tui.Views;

/// <summary>
///     What a view laid over the content panel draws pictures with (#580) — the panel's own picture cache, its Kitty
///     image store and its <b>Raster</b> — and what those views want of the cache, which the panel says with its own
///     frame's wants.
/// </summary>
/// <remarks>
///     The one cache and the one image store, rather than a view's own, so that a custom emoji already drawn in a post is
///     neither fetched nor sent again for a list (ADR-0035). The cache takes a frame's wants whole, once a frame, so a
///     view over the panel says nothing to it itself: it draws first, and the panel, drawn after it, asks for what it
///     drew with what the panel draws (<see cref="Placing.Over" />). A view that is not shown wants nothing.
/// </remarks>
/// <param name="pictures">Where the pixels come from, or <see langword="null" /> with pictures off.</param>
/// <param name="placeholders">What a Kitty terminal holds, where pictures may be drawn as placeholders.</param>
/// <param name="raster">How this terminal paints pixels, as the panel last settled it.</param>
internal sealed class OverlayPictures(IPictures? pictures, Placeholders? placeholders, Func<Raster> raster)
{
    /// <summary>How many custom emoji a view over the panel draws through a box at once: a pick list's rows.</summary>
    public const int MostCustomEmojiBoxes = Shell.PeopleToMention.Most;

    private readonly List<PaintedView> _drawing = [];

    /// <summary>None at all: nothing over the panel draws a picture, as where nothing does.</summary>
    public static OverlayPictures None { get; } = new(null, null, () => Media.Raster.None);

    /// <summary>Where the pixels come from, or <see langword="null" /> with pictures off.</summary>
    public IPictures? Pictures => pictures;

    /// <summary>What a Kitty terminal holds, where pictures may be drawn as placeholders.</summary>
    public Placeholders? Placeholders => placeholders;

    /// <summary>How this terminal paints pixels, as the panel last settled it.</summary>
    public Raster Raster => raster();

    /// <summary>
    ///     Every picture the views over the panel that are shown want, as the last frame each drew settled them: all of
    ///     them on screen.
    /// </summary>
    public IReadOnlyList<Drawn> Wanted => [.. _drawing.Where(Shown).SelectMany(view => view.Wanted)];

    /// <summary>Says that <paramref name="view" />, laid over the panel, draws pictures from these.</summary>
    public void Drawing(PaintedView view) => _drawing.Add(view);

    /// <summary>Whether <paramref name="view" /> is shown, which is whether it and everything it is in is.</summary>
    private static bool Shown(View view)
    {
        for (View? at = view; at is not null; at = at.SuperView)
        {
            if (!at.Visible)
            {
                return false;
            }
        }

        return true;
    }
}
