using Wooly.Core.Profiles;

namespace Wooly.Core.Posts;

/// <summary>
///     Writes a profile's own posts: publishes them, changes them, and takes them down. The narrow port ADR-0005 asks
///     for over Mastonet's whole REST surface, and the counterpart to <see cref="Timelines.ITimelineReader" /> — front
///     ends depend on this, and their tests fake this rather than the network.
/// </summary>
public interface IPostAuthor
{
    /// <summary>Publishes <paramref name="draft" /> as <paramref name="profile" />.</summary>
    /// <remarks>
    ///     Attaching media is part of publishing rather than a step before it: the files named by the draft are uploaded
    ///     here and the post goes out carrying them, so no caller has to hold an upload's id between two calls, and a
    ///     caller that fails part way through has published nothing.
    /// </remarks>
    /// <returns>The post as the instance published it — its id, its address, and the visibility it actually went out at.</returns>
    /// <exception cref="ArgumentException">
    ///     The draft is not one an instance would take (<see cref="PostDraft.Problem" />). A caller is expected to have
    ///     rejected that against what the user gave; reaching here with one is a defect, not user error.
    /// </exception>
    /// <exception cref="Errors.MediaNotFoundException">One of the draft's attachments names a file that is not there.</exception>
    Task<Post> Publish(ActiveProfile profile, PostDraft draft, CancellationToken cancellationToken);

    /// <summary>
    ///     Sends the file at <paramref name="path" /> up to <paramref name="profile" />'s instance as a pending
    ///     attachment, for a post not yet written to name (ADR-0026, #375) — reporting to <paramref name="progress" />
    ///     how far it has got, and waiting out the instance's processing of it before handing it back.
    /// </summary>
    /// <remarks>
    ///     The TUI's way of attaching, where <see cref="Publish" /> is the CLI's: a compose screen sends each file the
    ///     moment it is attached, so a refusal arrives while there is still a draft to change. Never retried here; trying
    ///     again is the author's choice (ADR-0006).
    /// </remarks>
    /// <returns>The attachment, ready for a post to name it.</returns>
    /// <exception cref="Errors.MediaNotFoundException">There is no file at <paramref name="path" />.</exception>
    /// <exception cref="Errors.AttachmentRefusedException">
    ///     The instance would not take the file, or could not process it, and said why.
    /// </exception>
    /// <exception cref="Errors.TransientNetworkException">The instance could not be reached, retries included.</exception>
    Task<PendingAttachment> Attach(
        ActiveProfile profile,
        string path,
        IProgress<AttachmentProgress> progress,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Sets the description of the pending attachment <paramref name="attachmentId" /> names to
    ///     <paramref name="description" />, or takes it off where that is empty (#377) — what a post naming it will
    ///     carry, for people who cannot see it.
    /// </summary>
    /// <remarks>
    ///     A step of its own rather than part of <see cref="Attach" />: an author writes a description while the file is
    ///     still going up, and changes it after, so it is sent once there is an attachment to put it on and again
    ///     whenever it changes. Never retried here (ADR-0006).
    /// </remarks>
    /// <exception cref="Errors.AttachmentRefusedException">
    ///     The instance would not take the description — longer than it allows, most often — and said why.
    /// </exception>
    /// <exception cref="Errors.TransientNetworkException">The instance could not be reached, retries included.</exception>
    Task Describe(
        ActiveProfile profile,
        string attachmentId,
        string description,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Publishes <paramref name="draft" /> as <paramref name="profile" />, carrying the pending attachments it names
    ///     by id (<see cref="PostDraft.Attached" />) rather than files by path (ADR-0026, #375).
    /// </summary>
    /// <remarks>
    ///     Everything else as <see cref="Publish" /> does it, a reply's reach included. A post that names no attachment
    ///     at all is published this way too, which is how the TUI publishes every post.
    /// </remarks>
    /// <returns>The post as the instance published it.</returns>
    /// <exception cref="ArgumentException">
    ///     The draft is not one an instance would take (<see cref="PostDraft.Problem" />), or names files by path, which
    ///     are <see cref="Publish" />'s.
    /// </exception>
    /// <exception cref="Errors.PostRefusedException">
    ///     The instance would not take the post — among other things, one naming an attachment it has not finished
    ///     processing.
    /// </exception>
    Task<Post> PublishAttached(ActiveProfile profile, PostDraft draft, CancellationToken cancellationToken);

    /// <summary>
    ///     Changes the post <paramref name="postId" /> names, leaving everything <paramref name="edit" /> does not
    ///     mention as it was.
    /// </summary>
    /// <remarks>
    ///     Keeping that promise takes a read as well as a write. Mastodon's edit does not amend a post, it replaces one:
    ///     attachments left out of the request are dropped from the post, and so is a warning left out of it. So the
    ///     post is read first and its own attachments are carried into the edit — otherwise <c>post edit</c> would be a
    ///     way to lose a photograph while fixing a typo.
    /// </remarks>
    /// <returns>The post as it now stands.</returns>
    /// <exception cref="Errors.UneditablePostException">
    ///     The post carries a poll, which this client cannot carry through an edit without destroying it.
    /// </exception>
    Task<Post> Edit(ActiveProfile profile, string postId, PostEdit edit, CancellationToken cancellationToken);

    /// <summary>Takes the post <paramref name="postId" /> names down.</summary>
    /// <remarks>
    ///     There is no undoing this, and nothing here asks whether the caller is sure — a front end that wants to ask is
    ///     the place that can, because it is the only one that knows whether there is anybody there to answer.
    /// </remarks>
    Task Delete(ActiveProfile profile, string postId, CancellationToken cancellationToken);
}
