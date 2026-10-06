namespace Wooly.Tui.Media;

/// <summary>
///     One picture a frame wants, and whether it is on screen in that frame or only near it.
/// </summary>
/// <remarks>
///     What a frame says to <see cref="IPictures.Want" />, nearest first. Being on screen is what the cache is never
///     to let go of, and only the view that drew the frame knows it, so it travels with the picture rather than being
///     worked out again from where the picture came in the list (ADR-0025).
/// </remarks>
/// <param name="Drawn">The picture.</param>
/// <param name="OnScreen">Whether any of its box, or the row that wants it, is on the page in this frame.</param>
public readonly record struct WantedPicture(Drawn Drawn, bool OnScreen);
