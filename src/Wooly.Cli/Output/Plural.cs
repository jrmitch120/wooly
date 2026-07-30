namespace Wooly.Cli.Output;

/// <summary>
///     Counting things in a sentence a person reads. One place, so that "1 favorites" cannot appear under a post in one
///     command and read correctly in another.
/// </summary>
internal static class Plural
{
    /// <param name="plural">Given only where adding an <c>s</c> would not make one.</param>
    public static string Of(long count, string singular, string? plural = null) =>
        $"{count} {(count == 1 ? singular : plural ?? singular + "s")}";
}
