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
    ///     The style <paramref name="name" /> spells, or <see langword="null" /> if it spells none of them — matched exactly,
    ///     as written, since nothing but these five has ever been read.
    /// </summary>
    public static ScrollbarStyle? Parse(string name) =>
        Names.Where(each => string.Equals(each.Name, name, StringComparison.Ordinal))
             .Select(each => (ScrollbarStyle?)each.Style)
             .FirstOrDefault();
}
