using Terminal.Gui.ViewBase;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Shell;

namespace Wooly.Tui.Views;

/// <summary>
///     What the content panel draws pictures with — the picture cache, the Kitty image store and how the terminal paints
///     pixels — and the views laid over the panel that draw pictures from the same (#580): a pick list's custom emoji.
///     What those views want of the cache the panel says with its own frame's wants.
/// </summary>
/// <remarks>
///     The one cache and the one image store, rather than a view's own, so that a custom emoji already drawn in a post is
///     neither fetched nor sent again for a list (ADR-0035). The cache takes a frame's wants whole, once a frame, so a
///     view over the panel says nothing to it itself: it draws first, and the panel, drawn after it, asks for what it
///     drew with what the panel draws (<see cref="Placing.Over" />). A view that is not shown wants nothing.
/// </remarks>
/// <param name="pictures">Where the pixels come from, or <see langword="null" /> with pictures off.</param>
/// <param name="placeholders">What a Kitty terminal holds, where pictures may be drawn as placeholders.</param>
/// <param name="asked">
///     How this terminal paints pixels, asked by the panel once a frame as it settles it, or <see langword="null" /> for a
///     terminal that draws none.
/// </param>
internal sealed class PanelPictures(IPictures? pictures, Placeholders? placeholders, Func<Raster>? asked)
{
    /// <summary>How many custom emoji a view over the panel draws through a box at once: a hanging list's rows.</summary>
    public const int MostCustomEmojiBoxes = HangingWord.MostOffered;

    private readonly List<PaintedView> _over = [];

    /// <summary>The panel drawn with these, whose frame settles the Raster; none until it is built.</summary>
    private PaintedView? _panel;

    /// <summary>None at all: nothing draws a picture, as where nothing does.</summary>
    public static PanelPictures None { get; } = new(null, null, null);

    /// <summary>Where the pixels come from, or <see langword="null" /> with pictures off.</summary>
    public IPictures? Pictures => pictures;

    /// <summary>What a Kitty terminal holds, where pictures may be drawn as placeholders.</summary>
    public Placeholders? Placeholders => placeholders;

    /// <summary>How this terminal paints pixels, asked afresh: what the panel settles each of its frames by.</summary>
    public Raster Asked() => asked?.Invoke() ?? Raster.None;

    /// <summary>
    ///     How this terminal paints pixels, as the panel last settled it — the previous frame's, to a view over the panel,
    ///     which draws before it — or <see cref="Media.Raster.None" /> before its first.
    /// </summary>
    public Raster Raster => _panel?.Raster ?? Raster.None;

    /// <summary>
    ///     Every picture the views over the panel that are shown want, as the last frame each drew settled them: all of
    ///     them on screen.
    /// </summary>
    public IReadOnlyList<Drawn> Wanted => [.. _over.Where(Shown).SelectMany(view => view.Wanted)];

    /// <summary>Says that <paramref name="panel" /> is the content panel, drawing with these.</summary>
    public void Drawing(PaintedView panel) => _panel = panel;

    /// <summary>Says that <paramref name="view" />, laid over the panel, draws pictures from these.</summary>
    public void DrawingOver(PaintedView view) => _over.Add(view);

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
