namespace Wooly.Core.Configuration;

/// <summary>
///     How a <see cref="ScrollbarStyle" /> is spelled in the config file's <c>scrollbar</c> key (#505), read and written
///     through the one place so that the file can never be written with a word it would refuse to read.
/// </summary>
public static class ScrollbarStyleName
{
    private static readonly IReadOnlyList<(ScrollbarStyle Style, string Name)> Names =
    [
        (ScrollbarStyle.Block, "block"),
        (ScrollbarStyle.Line, "line"),
        (ScrollbarStyle.LeftHalf, "left-half"),
        (ScrollbarStyle.RightHalf, "right-half"),
        (ScrollbarStyle.None, "none"),
    ];

    /// <summary>Every spelling, listed the way an error message wants them.</summary>
    public static string Accepted => string.Join(", ", Names.Select(name => $"\"{name.Name}\""));

    /// <summary>The spelling of <paramref name="style" />, as the file takes it.</summary>
    public static string Of(ScrollbarStyle style) => Names.Single(name => name.Style == style).Name;

    /// <summary>
    ///     The style <paramref name="name" /> spells, or <see langword="null" /> if it spells none of them.
    /// </summary>
    /// <remarks>
    ///     Matched exactly, deliberately: no trimming and no case folding, unlike a visibility, since nothing but these
    ///     five lower-case words has ever been read here.
    /// </remarks>
    public static ScrollbarStyle? Parse(string name) =>
        Names.Where(each => string.Equals(each.Name, name, StringComparison.Ordinal))
             .Select(each => (ScrollbarStyle?)each.Style)
             .FirstOrDefault();

    /// <summary>
    ///     How a value that is not a style is described, kept beside the spellings so that the message lists the words
    ///     <see cref="Parse" /> takes and no others.
    /// </summary>
    public static string Rejection(string name) => $"'{name}' is not a scrollbar style. Use one of: {Accepted}.";
}
