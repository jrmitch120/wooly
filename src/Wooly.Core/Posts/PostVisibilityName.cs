namespace Wooly.Core.Posts;

/// <summary>
///     How a <see cref="PostVisibility" /> is spelled wherever a person or a script reads or writes one, in the one place
///     every entry point can reach it: the <c>--visibility</c> flag, the <c>default_visibility</c> key in the config
///     file, the CLI's report, <c>--json</c> and the feed's byline. A user who writes <c>followers</c> in the file and
///     <c>followers</c> on the command line has written the same word, so the two cannot be allowed to accept different
///     sets of them.
/// </summary>
/// <remarks>
///     Mastodon's wire word for followers is <c>private</c>, which reads as "only me" to anybody who has not learned
///     otherwise, so this project says followers (ADR-0024). <c>private</c> is accepted here as an alias, permanently,
///     so that config files and scripts written before the change keep working; it is never written, and never offered
///     in <see cref="Accepted" />. The wire's own spelling is <see cref="PostWire" />'s, not this.
/// </remarks>
public static class PostVisibilityName
{
    /// <summary>Mastodon's word for <see cref="PostVisibility.Followers" />, still read as it.</summary>
    private const string FollowersAlias = "private";

    /// <summary>The spelling of <paramref name="visibility" />: lower case, as both the flag and the file take it.</summary>
    public static string Of(PostVisibility visibility) => visibility.ToString().ToLowerInvariant();

    /// <summary>Every spelling this client offers, listed the way an error message wants them.</summary>
    public static string Accepted => string.Join(", ", Enum.GetValues<PostVisibility>().Select(Of));

    /// <summary>
    ///     The visibility <paramref name="name" /> spells, or <see langword="null" /> if it spells none of them.
    /// </summary>
    /// <remarks>
    ///     Matched against the spellings by hand rather than through <see cref="Enum.TryParse{TEnum}(string, out TEnum)" />,
    ///     which would also accept the underlying numbers ("1") and comma-separated combinations ("public,direct") —
    ///     neither of which is a thing a user could have meant to write.
    /// </remarks>
    public static PostVisibility? Parse(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var trimmed = name.Trim();

        if (string.Equals(FollowersAlias, trimmed, StringComparison.OrdinalIgnoreCase))
        {
            return PostVisibility.Followers;
        }

        foreach (var visibility in Enum.GetValues<PostVisibility>())
        {
            if (string.Equals(Of(visibility), trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return visibility;
            }
        }

        return null;
    }

    /// <summary>
    ///     How a value that is not a visibility is described, shared so that the flag turning one down and the config
    ///     file turning one down cannot say different things about the same word.
    /// </summary>
    public static string Rejection(string name) => $"'{name}' is not a post visibility. Use one of: {Accepted}.";
}
