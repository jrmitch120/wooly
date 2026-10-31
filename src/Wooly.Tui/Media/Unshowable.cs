namespace Wooly.Tui.Media;

/// <summary>
///     Why a picture sent for could not be had (<see cref="IPictures.WhyNot" />): what the Picture screen says on the
///     status row where the original it sent for will not come, and the smaller copy is drawn in its place (ADR-0034).
/// </summary>
public enum Unshowable
{
    /// <summary>Its file is larger than this client reads for it, and was refused before more of it was read.</summary>
    TooLarge,

    /// <summary>It could not be fetched, or what came was not a picture this client can read.</summary>
    Failed,
}
