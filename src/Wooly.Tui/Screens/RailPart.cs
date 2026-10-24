namespace Wooly.Tui.Screens;

/// <summary>
///     What a click on a run of the rail means, as its <see cref="Rendering.Span.Item" />, where the run means more than
///     the row it is on: a row's <see cref="Rendering.Line.Item" /> carries the destination instead (#288, #443).
/// </summary>
public enum RailPart
{
    /// <summary>The toggle's mark, <c>«</c> or <c>»</c>, a click on which is <c>\</c> (#443).</summary>
    ToggleMark,
}
