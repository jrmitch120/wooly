using System.Globalization;
using Wooly.Core.Posts;

namespace Wooly.Tui.Screens;

/// <summary>
///     Where a pending attachment on a compose screen has got to (#375): going up, being processed, ready for the post
///     to name, or refused. Fed to the screen by the shell, which is what sends it up (ADR-0015, ADR-0026); the screen
///     only holds it and draws it.
/// </summary>
public abstract record AttachmentState
{
    /// <remarks>Closed, so that a row has these four things to say and no others.</remarks>
    private AttachmentState()
    {
    }

    /// <summary>Going up, <paramref name="Done" /> of the way, from nought to one.</summary>
    public sealed record Sending(double Done) : AttachmentState;

    /// <summary>Up, and being processed by the instance.</summary>
    public sealed record Processing : AttachmentState;

    /// <summary>On the instance and ready for a post to name, as <paramref name="Pending" />.</summary>
    public sealed record Ready(PendingAttachment Pending) : AttachmentState;

    /// <summary>
    ///     The instance would not take it, or it could not be sent, and <paramref name="Why" /> says so —
    ///     <paramref name="Retryable" /> where sending it again could mend that, as for a dropped connection and never
    ///     for a file too large or of a type the instance refuses (#378).
    /// </summary>
    public sealed record Refused(string Why, bool Retryable = false) : AttachmentState;
}

/// <summary>What a click on a pending attachment's row means, by where on the row it lands (#378).</summary>
public enum AttachmentPart
{
    /// <summary>The row itself, which a click picks.</summary>
    Row,

    /// <summary>Its <c>x</c>, which takes it off the post.</summary>
    Remove,

    /// <summary>Its <c>retry (r)</c>, which sends it up again.</summary>
    Retry,
}

/// <summary>
///     A <b>pending attachment</b> as a compose screen holds it (#375): the file it was attached from, what kind of
///     thing it is and how large, and where it has got to on its way up — one row under the Media header.
/// </summary>
/// <remarks>
///     A thing with an identity rather than a value: the shell feeds each upload's progress back to the attachment it
///     started it for (<see cref="ComposeScreen.Progressed" />), and two drops of the same file are two attachments.
///     What a test or the shell reads off it is what was attached and how far it has got; only the screen moves it on.
/// </remarks>
/// <param name="path">The file it was attached from.</param>
/// <param name="kind">What kind of attachment the file makes, by its type.</param>
/// <param name="bytes">How large the file is.</param>
public sealed class ComposeAttachment(string path, MediaKind kind, long bytes)
{
    /// <summary>The file it was attached from.</summary>
    public string Path { get; } = path;

    /// <summary>The file's own name, which is what its row says.</summary>
    public string Name { get; } = System.IO.Path.GetFileName(path);

    /// <summary>What kind of attachment it is.</summary>
    public MediaKind Kind { get; } = kind;

    /// <summary>How large the file is.</summary>
    public long Bytes { get; } = bytes;

    /// <summary>Where it has got to, which the shell feeds in as it hears.</summary>
    public AttachmentState State { get; internal set; } = new AttachmentState.Sending(0);

    /// <summary>
    ///     What its author says it shows: nothing yet, which its row marks quietly. Written by the description editor
    ///     (#377).
    /// </summary>
    public string Description { get; internal set; } = string.Empty;

    /// <summary>Whether it has a description, which the quiet mark and the fold's count read.</summary>
    public bool Described => Description.Trim().Length > 0;

    /// <summary>Whether it is still on its way: going up, or being processed.</summary>
    public bool Unfinished => State is AttachmentState.Sending or AttachmentState.Processing;

    /// <summary>The kind as its row says it: picture, animation, video, sound.</summary>
    public string KindWord => Kind switch
    {
        MediaKind.Image => "picture",
        MediaKind.Animation => "animation",
        MediaKind.Video => "video",
        MediaKind.Audio => "sound",
        _ => "file",
    };

    /// <summary>How large it is as its row says it: <c>812 B</c>, <c>217 KB</c>, <c>3.6 MB</c>.</summary>
    public string Size => Bytes switch
    {
        < 1024 => $"{Bytes} B",
        < 1024 * 1024 => string.Create(CultureInfo.InvariantCulture, $"{Bytes / 1024.0:F0} KB"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{Bytes / 1024.0 / 1024.0:F1} MB"),
    };

    /// <summary>
    ///     An attachment of the file at <paramref name="path" />, its kind read off its name and its size off the disk —
    ///     what the shell attaches once it has found the file is there.
    /// </summary>
    public static ComposeAttachment Of(string path) =>
        new(path, AttachmentTypes.KindOf(AttachmentTypes.Of(path)), new FileInfo(path).Length);
}
