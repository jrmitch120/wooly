namespace Wooly.Core.Errors;

/// <summary>
///     A file to be shown as the avatar or header of the own account's public profile is not where it was said to be —
///     a mistyped path, most often. Raised before anything is sent, so a save never goes half made: the name changed
///     and the picture that was meant to go with it missing.
/// </summary>
public sealed class PictureNotFoundException(string path)
    : WoolyException($"There is no file at '{path}' to show on your public profile.")
{
    /// <summary>The path that had no file at it.</summary>
    public string Path { get; } = path;
}
