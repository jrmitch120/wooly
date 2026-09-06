using Wooly.Core.Profiles;

namespace Wooly.Core.Discovery;

/// <summary>
///     Reads who an instance offers this profile to follow, and tells it to stop offering one. The narrow port
///     ADR-0005 asks for, alongside <see cref="Timelines.ITimelineReader" />,
///     <see cref="Relationships.IAccountRelationships" /> and <see cref="Search.IInstanceSearch" /> — the TUI's
///     Discover screen depends on this, and its tests fake this rather than the network.
/// </summary>
/// <remarks>
///     A port of its own rather than a sixth call on <see cref="Relationships.IAccountRelationships" />, whose own doc
///     comment scopes it to ties reached through one family of endpoints. <c>/suggestions</c> is not in that family
///     (ADR-0019). The Discover screen composes this with the ties port, which is cheaper than one port meaning
///     "whatever Discover shows".
///     <para>
///         Two methods and no third: there is no un-dismissing. Mastodon has no endpoint for it, so dismissal is
///         one-way and a screen that offered to undo it would be promising something no instance can do.
///     </para>
/// </remarks>
public interface IFollowSuggestions
{
    /// <summary>Reads who the instance is offering, each carrying the reasons it offered them under.</summary>
    /// <param name="limit">
    ///     How many to ask for. One call and no paging: the screen shows all of what it asked for, so there is nothing
    ///     left over to page through.
    /// </param>
    /// <returns>
    ///     The suggestions in the instance's own order, which is its ranking and is kept — and empty where it is
    ///     offering nobody, which a new account, a small instance or one with suggestions turned off all really are.
    ///     Never <see langword="null" />: a caller always asks before it draws, so there is no not-asked to report.
    /// </returns>
    /// <remarks>
    ///     Unlike <see cref="Relationships.IAccountRelationships.FamiliarFollowers" />, a failure here throws rather
    ///     than being answered as nothing. That call decorates one row of a screen built from four others; this one is
    ///     the whole of its screen, so answering an empty list would draw "nobody suggested" over a rate limit — and
    ///     the reader would believe it.
    /// </remarks>
    /// <exception cref="Errors.RateLimitedException">The profile has spent its quota with the instance.</exception>
    /// <exception cref="Errors.TransientNetworkException">The instance could not be reached, retries included.</exception>
    Task<IReadOnlyList<Suggestion>> Read(ActiveProfile profile, int limit, CancellationToken cancellationToken);

    /// <summary>Tells the instance to stop suggesting an account, which it will not offer again.</summary>
    /// <param name="accountId">
    ///     Who to stop being offered, as <see cref="Suggestion" /> carries them. An id rather than an address because
    ///     this is asked from a screen holding the suggestion, and because a suggestion has no id of its own — the
    ///     endpoint names the account.
    /// </param>
    /// <remarks>
    ///     One-way, and quieter about a refusal than anything else on this port — though there is very little for it to
    ///     be quiet about. Mastonet 3.1.3's <c>DeleteFollowSuggestion</c> answers a bare <see cref="Task" />, so it
    ///     never deserializes a body and never reaches the check that turns Mastodon's error JSON into an exception.
    ///     What saves that from mattering is what the endpoint does: checked against a live instance, it answers
    ///     <c>200</c> to any account id at all — it records "stop suggesting this one" without asking whether the id
    ///     names anybody — so the refusals a caller might have wanted to hear about are not refusals it makes. What is
    ///     left is an auth failure, which is a broken profile rather than a failed dismissal.
    ///     <para>
    ///         Left going through the library rather than worked around, because the library has this endpoint
    ///         (ADR-0019) and reaching for a raw <c>DELETE</c> would spend ADR-0011's way out of a Mastonet gap on a
    ///         gap that is not there. It is self-correcting besides: the row sits on a list the server regenerates, so
    ///         a dismissal that did not land shows up as the same person suggested again on the next refresh.
    ///     </para>
    ///     A rate limit and an unreachable instance <i>are</i> still thrown — those are the handlers' business, and are
    ///     raised before the library is reached at all.
    /// </remarks>
    /// <exception cref="Errors.RateLimitedException">The profile has spent its quota with the instance.</exception>
    /// <exception cref="Errors.TransientNetworkException">The instance could not be reached, retries included.</exception>
    Task Dismiss(ActiveProfile profile, string accountId, CancellationToken cancellationToken);
}
