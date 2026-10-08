using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Mastonet;
using Mastonet.Entities;
using Wooly.Core.Errors;
using Wooly.Core.Http;
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
///     <para>
///         The TUI attaches the other way round (ADR-0026): each file goes up on its own as it is attached
///         (<see cref="Attach" />), and the post names what went up by id (<see cref="PublishAttached" />). That upload
///         is sent by hand rather than through Mastonet, whose upload says nothing of how far it has got and which has
///         no call to ask whether the instance has finished processing a file.
///     </para>
///     Nothing here retries and nothing here waits but that processing: a publish is a write, which ADR-0006 never
///     resends, and a rate limit is reported rather than slept off.
/// </summary>
public sealed class PostAuthor(
    IMastodonClientFactory clientFactory,
    IHttpClientFactory httpClientFactory,
    AttachmentPolling polling) : IPostAuthor
{
    /// <inheritdoc />
    public async Task<Post> Publish(ActiveProfile profile, PostDraft draft, CancellationToken cancellationToken)
    {
        if (draft.Problem is { } problem)
        {
            throw new ArgumentException(problem, nameof(draft));
        }

        if (draft.Attached.Count > 0)
        {
            throw new ArgumentException("Attachments already sent are published with PublishAttached.", nameof(draft));
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

        // Before anything is uploaded, because this is the call that can refuse the whole publish. Only a reply pays
        // for it, and only a reply needs it — see Reaching.
        var reaching = await Reaching(client, draft, cancellationToken);

        var attachmentIds = await Upload(client, draft.Media, cancellationToken);

        return await Published(client, profile, draft, reaching, attachmentIds, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PendingAttachment> Attach(
        ActiveProfile profile,
        string path,
        IProgress<AttachmentProgress> progress,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            throw new MediaNotFoundException(path);
        }

        var media = await SentUp(profile, path, progress, cancellationToken);

        // An instance answers a file it has not finished with no address for it — 202 to the upload, 206 to the ask —
        // and a post naming it before then is refused, so it is asked again until it has one.
        while (media.Url is null)
        {
            progress.Report(new AttachmentProgress.Processing(media.Id));

            await Task.Delay(polling.Every, cancellationToken);

            media = await Asked(() => RawMastodonCall.Get<MediaWire>(
                        httpClientFactory,
                        profile,
                        $"api/v1/media/{Uri.EscapeDataString(media.Id)}",
                        [],
                        cancellationToken))
                    ?? throw new AttachmentRefusedException(null);
        }

        return new PendingAttachment(media.Id, PostWire.ToKind(media.Type));
    }

    /// <inheritdoc />
    /// <remarks>
    ///     By hand rather than through Mastonet, as the upload is, so that a refusal reads as the instance said it and
    ///     the call can be called off with the screen it was made for.
    /// </remarks>
    public async Task Describe(
        ActiveProfile profile,
        string attachmentId,
        string description,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            new Uri($"https://{profile.Instance}/api/v1/media/{Uri.EscapeDataString(attachmentId)}"))
        {
            Content = new FormUrlEncodedContent([new KeyValuePair<string, string>("description", description)]),
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", profile.AccessToken);

        var http = httpClientFactory.CreateClient(WoolyClient.HttpClientName);

        using var response = await http.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            throw new AttachmentRefusedException(await InstanceError.Reason(response, cancellationToken));
        }

        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc />
    public async Task<Post> PublishAttached(ActiveProfile profile, PostDraft draft, CancellationToken cancellationToken)
    {
        if (draft.Problem is { } problem)
        {
            throw new ArgumentException(problem, nameof(draft));
        }

        if (draft.Media.Count > 0)
        {
            throw new ArgumentException("Files by path are published with Publish.", nameof(draft));
        }

        var client = clientFactory.CreateClient(profile.Instance, profile.AccessToken);
        var reaching = await Reaching(client, draft, cancellationToken);

        return await Published(
            client,
            profile,
            draft,
            reaching,
            [.. draft.Attached.Select(attached => attached.Id)],
            cancellationToken);
    }

    /// <summary>
    ///     Publishes <paramref name="draft" /> carrying <paramref name="attachmentIds" />, at
    ///     <paramref name="reaching" /> — the one call both routes into publishing end in.
    /// </summary>
    private static async Task<Post> Published(
        IMastodonClient client,
        ActiveProfile profile,
        PostDraft draft,
        PostVisibility? reaching,
        IReadOnlyList<string> attachmentIds,
        CancellationToken cancellationToken)
    {
        // Mastonet's own calls take no cancellation token, so a Ctrl-C lands between calls rather than during one.
        // Between the last upload and the publish is the last moment stopping means nothing was published.
        cancellationToken.ThrowIfCancellationRequested();

        var published = await client.PublishStatus(
            draft.Text,
            reaching is { } visibility ? PostWire.ToWire(visibility) : null,
            draft.InReplyTo,
            attachmentIds,

            // A warning nothing knows to honour is not a warning. Mastodon carries the text and the "hide this" flag as
            // two fields, so a warning sets the flag whatever else does (ADR-0008); the author's own toggle can only
            // add to that, for attachments to go behind a click with no warning written.
            sensitive: draft.Sensitive || draft.ContentWarning is not null,
            spoilerText: draft.ContentWarning,
            language: draft.Language,
            poll: draft.Poll is null ? null : ToWire(draft.Poll));

        return PostWire.ToPost(published, profile);
    }

    /// <summary>
    ///     Sends the file at <paramref name="path" /> to <c>/api/v2/media</c> by hand, as Mastonet would but saying how
    ///     far it has got, and reads back what the instance made of it.
    /// </summary>
    private async Task<MediaWire> SentUp(
        ActiveProfile profile,
        string path,
        IProgress<AttachmentProgress> progress,
        CancellationToken cancellationToken)
    {
        // Opened here and closed here, as Upload's are: an upload that fails part way through leaves no file held open.
        await using var file = File.OpenRead(path);

        var body = new ReportingContent(file, done => progress.Report(new AttachmentProgress.Sending(done)));

        if (AttachmentTypes.Of(path) is { } type)
        {
            body.Headers.ContentType = new MediaTypeHeaderValue(type);
        }

        using var form = new MultipartFormDataContent { { body, "file", Path.GetFileName(path) } };
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"https://{profile.Instance}/api/v2/media"))
        {
            Content = form,
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", profile.AccessToken);

        var http = httpClientFactory.CreateClient(WoolyClient.HttpClientName);

        using var response = await http.SendAsync(request, cancellationToken);

        // Too large, of a type the instance will not take, or one it could not make sense of — the instance's to say,
        // and the author's to fix, so it is said in the instance's words rather than as a failure of the call.
        if (response.StatusCode is HttpStatusCode.UnprocessableEntity or HttpStatusCode.RequestEntityTooLarge)
        {
            throw new AttachmentRefusedException(await InstanceError.Reason(response, cancellationToken));
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<MediaWire>(cancellationToken)
               ?? throw new AttachmentRefusedException(null);
    }

    /// <summary>
    ///     Asks after a file being processed: an instance that could not process it answers 422, which is the file
    ///     refused as surely as a refusal of the upload.
    /// </summary>
    private static async Task<MediaWire?> Asked(Func<Task<MediaWire?>> ask)
    {
        try
        {
            return await ask();
        }
        catch (HttpRequestException refused) when (refused.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            throw new AttachmentRefusedException(null);
        }
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
            spoilerText: contentWarning,

            // Left out unless the edit names one, which is how Mastodon is told to keep the post's language.
            language: edit.LanguageWanted);

        return PostWire.ToPost(edited, profile);
    }

    /// <inheritdoc />
    public async Task Delete(ActiveProfile profile, string postId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var client = clientFactory.CreateClient(profile.Instance, profile.AccessToken);

        await client.DeleteStatus(postId);
    }

    /// <summary>
    ///     Who the post should reach when it goes out, which for a reply is not simply what the draft said.
    /// </summary>
    /// <remarks>
    ///     A reply is never published wider than the post it answers (ADR-0013). Mastodon does not enforce that — the
    ///     API takes whatever visibility a request names, whatever it is answering — so a reply to a direct message
    ///     composed at an account's own default would be published to the world, which is not a mistake anybody can
    ///     take back. Knowing what is being answered takes a read before the write, the same trade
    ///     <see cref="Edit" /> makes and for the same kind of reason.
    ///     <para>
    ///         A post answering nothing has nothing to be wider than, and pays for no read.
    ///     </para>
    /// </remarks>
    /// <exception cref="WiderReplyException">
    ///     The draft named a visibility wider than the post it answers. A standing preference is narrowed instead — see
    ///     <see cref="PostDraft.VisibilityChosen" />.
    /// </exception>
    private static async Task<PostVisibility?> Reaching(
        IMastodonClient client,
        PostDraft draft,
        CancellationToken cancellationToken)
    {
        if (draft.InReplyTo is not { } answeredId)
        {
            return draft.Visibility;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var answered = PostWire.ToVisibility((await client.GetStatus(answeredId)).Visibility);

        // Silence is answered as narrowly as the thing being answered, which is what every Mastodon client does and
        // the only safe reading of it here: the account's own default on the instance is a value this client cannot
        // see, so leaving the choice to it would be leaving it to something that might be wider.
        if (draft.Visibility is not { } asked)
        {
            return answered;
        }

        if (draft.VisibilityChosen && PostAudience.IsWiderThan(asked, answered))
        {
            throw new WiderReplyException(asked, answered);
        }

        return PostAudience.Narrower(asked, answered);
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

    private static PollParameters ToWire(PollDraft poll) => new()
    {
        Options = poll.Answers,
        ExpiresIn = poll.OpenFor,
        Multiple = poll.MultipleChoice,
    };

    /// <summary>
    ///     The little of an instance's answer about an attachment this reads: its id, its kind, and its address, which
    ///     it has none of until it has finished processing.
    /// </summary>
    private sealed record MediaWire
    {
        [JsonPropertyName("id")]
        public required string Id { get; init; }

        [JsonPropertyName("type")]
        public string? Type { get; init; }

        [JsonPropertyName("url")]
        public string? Url { get; init; }
    }
}
