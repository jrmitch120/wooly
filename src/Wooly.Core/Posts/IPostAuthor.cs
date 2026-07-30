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
