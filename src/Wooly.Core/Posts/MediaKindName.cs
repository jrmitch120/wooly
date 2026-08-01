namespace Wooly.Core.Posts;

/// <summary>
///     What this client calls a kind of attachment when it has to say so out loud — with the article it reads with,
///     because the sentence it goes in is <em>"a picture, undescribed"</em> and <em>"some audio, undescribed"</em> and
///     the article is not the same one.
/// </summary>
/// <remarks>
///     One table, shared by both surfaces, for the reason every other name table here exists: the CLI and the TUI both
///     have to describe an attachment nobody described, and two spellings of that is how <c>post show</c> comes to call
///     a thing something the timeline calls something else.
/// </remarks>
public static class MediaKindName
{
    /// <summary>How <paramref name="kind" /> is said.</summary>
    public static string Of(MediaKind kind) => kind switch
    {
        MediaKind.Image => "a picture",
        MediaKind.Animation => "an animation",
        MediaKind.Video => "a video",
        MediaKind.Audio => "some audio",

        // Including the instance's own "unknown", which is what it says about an attachment it has not finished
        // processing. There is still something there, so it is still named.
        _ => "an attachment",
    };
}
