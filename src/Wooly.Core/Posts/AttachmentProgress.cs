namespace Wooly.Core.Posts;

/// <summary>
///     How far a file being sent up as a pending attachment has got (#375), as <see cref="IPostAuthor.Attach" /> reports
///     it on the way to handing the attachment back: going up, then — for video, sound and anything large — waiting on
///     the instance to process it.
/// </summary>
public abstract record AttachmentProgress
{
    /// <remarks>Closed, so that what is reported is the two stages an upload has before it is done.</remarks>
    private AttachmentProgress()
    {
    }

    /// <summary>The file is going up, <paramref name="Done" /> of the way there, from nought to one.</summary>
    public sealed record Sending(double Done) : AttachmentProgress;

    /// <summary>
    ///     The file is up, as <paramref name=Id />, and the instance is still processing it — which a post cannot name
    ///     it until it has done.
    /// </summary>
    public sealed record Processing(string Id) : AttachmentProgress;
}
