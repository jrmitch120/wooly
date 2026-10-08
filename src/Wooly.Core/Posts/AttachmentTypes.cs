namespace Wooly.Core.Posts;

/// <summary>
///     What type of file an attachment is, as an instance names types — by MIME type — and what kind of attachment that
///     makes it (#375). Read off the file's name, which is all a front end knows of a file before it sends it, and which
///     is what an instance's list of accepted types (<see cref="PostLimits.MediaTypes" />) is checked against.
/// </summary>
public static class AttachmentTypes
{
    /// <summary>
    ///     The types Mastodon accepts out of the box, which an instance that does not list its own is taken to accept.
    /// </summary>
    public static readonly IReadOnlyList<string> Mastodon =
    [
        "image/jpeg", "image/png", "image/gif", "image/heic", "image/heif", "image/webp", "image/avif",
        "video/webm", "video/mp4", "video/quicktime", "video/ogg",
        "audio/wave", "audio/wav", "audio/x-wav", "audio/x-pn-wave", "audio/vnd.wave", "audio/ogg", "audio/vorbis",
        "audio/mpeg", "audio/mp3", "audio/webm", "audio/flac", "audio/aac", "audio/m4a", "audio/x-m4a", "audio/mp4",
        "audio/3gpp", "video/x-ms-asf",
    ];

    /// <summary>Each extension an attachment is likely to have, and the type an instance knows it by.</summary>
    private static readonly Dictionary<string, string> ByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".gif"] = "image/gif",
        [".heic"] = "image/heic",
        [".heif"] = "image/heif",
        [".webp"] = "image/webp",
        [".avif"] = "image/avif",
        [".webm"] = "video/webm",
        [".mp4"] = "video/mp4",
        [".m4v"] = "video/mp4",
        [".mov"] = "video/quicktime",
        [".ogv"] = "video/ogg",
        [".wav"] = "audio/wav",
        [".ogg"] = "audio/ogg",
        [".oga"] = "audio/ogg",
        [".opus"] = "audio/ogg",
        [".mp3"] = "audio/mpeg",
        [".flac"] = "audio/flac",
        [".aac"] = "audio/aac",
        [".m4a"] = "audio/mp4",
        [".3gp"] = "audio/3gpp",
    };

    /// <summary>The type of the file at <paramref name="path" />, by its name, or nothing for a name this does not know.</summary>
    public static string? Of(string path) => ByExtension.GetValueOrDefault(Path.GetExtension(path));

    /// <summary>
    ///     The kind of attachment a file of <paramref name="type" /> becomes: a GIF is an animation — Mastodon turns
    ///     one into a looping clip — and otherwise the type's own family says it.
    /// </summary>
    public static MediaKind KindOf(string? type) => type switch
    {
        "image/gif" => MediaKind.Animation,
        not null when type.StartsWith("image/", StringComparison.OrdinalIgnoreCase) => MediaKind.Image,
        not null when type.StartsWith("video/", StringComparison.OrdinalIgnoreCase) => MediaKind.Video,
        not null when type.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) => MediaKind.Audio,
        _ => MediaKind.Unknown,
    };
}
