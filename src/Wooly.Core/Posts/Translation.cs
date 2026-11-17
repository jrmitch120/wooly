namespace Wooly.Core.Posts;

/// <summary>
///     A post's words as its instance rendered them in another language — GLOSSARY.md's <b>Translation</b>: the text,
///     the warning, the poll's options and the attachments' descriptions together, the language the instance took the
///     post to be written in, the language it actually translated into, and who translated it (ADR-0037).
/// </summary>
public sealed record Translation
{
    /// <summary>The post's text translated, its HTML flattened exactly as a post's own is.</summary>
    public required string Content { get; init; }

    /// <summary>The warning translated, or <see langword="null" /> on a post written behind none.</summary>
    public string? ContentWarning { get; init; }

    /// <summary>
    ///     The id of the poll whose options <see cref="PollOptions" /> are, or <see langword="null" /> on a post carrying
    ///     none — what matches them to the post's poll, the wire keeping no id on an option.
    /// </summary>
    public string? PollId { get; init; }

    /// <summary>The poll's options translated, in the poll's own order, and empty where there is no poll.</summary>
    public IReadOnlyList<string> PollOptions { get; init; } = [];

    /// <summary>
    ///     The attachments' descriptions translated, keyed by the attachment's id, and empty where nothing attached was
    ///     described.
    /// </summary>
    public IReadOnlyDictionary<string, string> Descriptions { get; init; } = new Dictionary<string, string>();

    /// <summary>
    ///     The code of the language the instance took the post to be written in, or <see langword="null" /> where its
    ///     translator did not say.
    /// </summary>
    public string? From { get; init; }

    /// <summary>
    ///     The code of the language the instance translated into — which is not always the one asked for: an instance
    ///     honours only its own interface locales, and quietly uses the account's otherwise.
    /// </summary>
    public required string To { get; init; }

    /// <summary>Who translated it, as the instance named its translator — <c>DeepL.com</c>, <c>LibreTranslate</c>.</summary>
    public required string Provider { get; init; }

    /// <summary>
    ///     <paramref name="post" /> with these words in place of its own — its text, its warning, its poll's options and
    ///     its attachments' descriptions — and everything else as it was. A boost's words are the boosted post's, so a
    ///     boost is answered with the post it carries translated.
    /// </summary>
    /// <remarks>
    ///     Matched rather than trusted: the options go onto the poll whose id they were sent with and only if there are as
    ///     many, and each description onto the attachment its id names, so a translation of an edited post can never put
    ///     an answer's words beside another's. A part the instance sent nothing for stays as written.
    /// </remarks>
    public Post Over(Post post)
    {
        if (post.Boosted is { } boosted)
        {
            return post with { Boosted = Over(boosted) };
        }

        return post with
        {
            Content = Content,
            ContentWarning = post.ContentWarning is null ? null : ContentWarning ?? post.ContentWarning,
            Poll = post.Poll is { } poll && poll.Id == PollId && poll.Options.Count == PollOptions.Count
                ? poll with
                {
                    Options = [.. poll.Options.Select((option, at) => option with { Text = PollOptions[at] })],
                }
                : post.Poll,
            Media =
            [
                .. post.Media.Select(attached => Descriptions.TryGetValue(attached.Id, out var description)
                    ? attached with { Description = description }
                    : attached),
            ],
        };
    }
}
