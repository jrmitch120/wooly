using Mastonet;
using Mastonet.Entities;
using Wooly.Core.Errors;
using Wooly.Core.Profiles;

namespace Wooly.Core.Posts;

/// <summary>
///     Writes posts through Mastonet, turning a draft into the calls an instance takes and what comes back into a
///     <see cref="Post" />. Two things it does are worth knowing about before reading it:
///     <para>
///         Uploading is folded into publishing. The files a draft names are sent one at a time and the ids they come
///         back with go on the post, so a caller composes once rather than uploading, holding ids, and then posting. They
///         are sent in the draft's own order because an attachment's place on a post is part of what was composed, and
///         every path is checked before the first one goes, so a typo in the third attachment costs an author no
///         half-composed post they cannot take back.
///     </para>
///     <para>
///         Editing reads before it writes. Mastodon's edit endpoint replaces a post rather than amending it: whatever the
///         request leaves out is dropped from the post. So the post is read first and its attachments and — unless the
///         edit says otherwise — its content warning are carried into the request. A poll cannot be carried through at
///         all, and that is the one thing this refuses to do (see <see cref="UneditablePostException" />).
///     </para>
///     Nothing here retries and nothing here waits: a publish is a write, which ADR-0006 never resends, and a rate limit
///     is reported rather than slept off.
/// </summary>
public sealed class PostAuthor(IMastodonClientFactory clientFactory) : IPostAuthor
{
    /// <inheritdoc />
    public async Task<Post> Publish(ActiveProfile profile, PostDraft draft, CancellationToken cancellationToken)
    {
        if (draft.Problem is { } problem)
        {
            throw new ArgumentException(problem, nameof(draft));
        }

        // Before a single byte is uploaded. Finding the fourth path wrong after three files have gone up costs the user
        // three uploads they cannot see and have no way to tidy away.
        foreach (var attachment in draft.Media)
        {
            if (!File.Exists(attachment.Path))
            {
                throw new MediaNotFoundException(attachment.Path);
            }
        }

        var client = clientFactory.CreateClient(profile.Instance, profile.AccessToken);
        var attachmentIds = await Upload(client, draft.Media, cancellationToken);

        // Mastonet's own calls take no cancellation token, so a Ctrl-C lands between calls rather than during one.
        // Between the last upload and the publish is the last moment stopping means nothing was published.
        cancellationToken.ThrowIfCancellationRequested();

        var published = await client.PublishStatus(
            draft.Text,
            draft.Visibility is { } visibility ? PostWire.ToWire(visibility) : null,
            draft.InReplyTo,
            attachmentIds,

            // A warning nothing knows to honour is not a warning. Mastodon carries the text and the "hide this" flag as
            // two fields, so the flag is set from the text rather than asked for separately — there is no post this
            // client composes that wants one without the other.
            sensitive: draft.ContentWarning is not null,
            spoilerText: draft.ContentWarning,
            poll: draft.Poll is null ? null : ToWire(draft.Poll));

        return PostWire.ToPost(published, profile.Instance);
    }

    /// <inheritdoc />
    public async Task<Post> Edit(ActiveProfile profile, string postId, PostEdit edit, CancellationToken cancellationToken)
    {
        var client = clientFactory.CreateClient(profile.Instance, profile.AccessToken);

        // The read that makes the promise in IPostAuthor.Edit keepable: the post's own attachments and warning are
        // only knowable from the post.
        var existing = await client.GetStatus(postId);

        if (existing.Poll is not null)
        {
            throw new UneditablePostException(postId);
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Silence from the edit means "leave it as it was", which here has to be said out loud: an omitted spoiler_text
        // is how the API is told to take the warning off.
        var contentWarning = edit.ChangesContentWarning
            ? edit.ContentWarningWanted
            : existing.SpoilerText ?? string.Empty;

        var edited = await client.EditStatus(
            postId,
            edit.Text,
            existing.MediaAttachments.Select(attachment => attachment.Id),

            // Carried forward rather than worked out from the warning alone. A post can be marked as one to hide
            // because of what its pictures show, with no warning text at all, and deriving this flag from the text
            // would un-blur those pictures on an edit that only fixed a typo. Erring the other way — leaving something
            // hidden that need not be — is the harmless direction, so unhiding is not something an edit does here.
            sensitive: existing.Sensitive == true || !string.IsNullOrEmpty(contentWarning),
            spoilerText: contentWarning);

        return PostWire.ToPost(edited, profile.Instance);
    }

    /// <inheritdoc />
    public async Task Delete(ActiveProfile profile, string postId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var client = clientFactory.CreateClient(profile.Instance, profile.AccessToken);

        await client.DeleteStatus(postId);
    }

    /// <summary>
    ///     Sends each file and collects the id the instance gives it back. One at a time rather than all at once: the
    ///     ids have to come back in the author's own order, and a post's worth of large files sent in parallel is a way
    ///     to spend a rate limit on a single command.
    /// </summary>
    private static async Task<IReadOnlyList<string>> Upload(
        IMastodonClient client,
        IReadOnlyList<MediaAttachment> media,
        CancellationToken cancellationToken)
    {
        var ids = new List<string>(media.Count);

        foreach (var attachment in media)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Opened here and closed here, which is why nothing above this layer hands around a stream: an upload that
            // fails part way through should leave no file held open behind it.
            await using var file = File.OpenRead(attachment.Path);

            var uploaded = await client.UploadMedia(
                new MediaDefinition(file, Path.GetFileName(attachment.Path)),
                attachment.AltText);

            ids.Add(uploaded.Id);
        }

        return ids;
    }

    private static PollParameters ToWire(PostPoll poll) => new()
    {
        Options = poll.Answers,
        ExpiresIn = poll.OpenFor,
        Multiple = poll.MultipleChoice,
    };
}
