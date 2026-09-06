using Wooly.Core.Accounts;
using Wooly.Core.Http;
using Wooly.Core.Profiles;

namespace Wooly.Core.Discovery;

/// <summary>
///     Reads and dismisses follow suggestions, over the two halves of Mastodon's suggestions surface this client needs
///     and the two ways it has of reaching them.
///     <para>
///         The read is a raw <c>GET</c> through <see cref="RawMastodonCall" />, because it must be <c>v2</c> and
///         Mastonet 3.1.3 has <c>v1</c> only. The two run the same thing underneath and differ in the serializer:
///         <c>v1</c> answers with the accounts and throws <c>sources</c> away, and the reason is the entire feature
///         (ADR-0019). The call still goes over the named <see cref="HttpClient" /> every Mastonet call runs through,
///         so it is retried, rate-limit-checked and reported like the rest — this is the second time ADR-0011's way out
///         of a library gap has been taken, and it stays cheap only while <see cref="RawMastodonCall" /> is the one
///         place it is done.
///     </para>
///     The dismissal is Mastonet's, there being no <c>v2</c> of that endpoint to want — and being a call the library can
///     already make, which is what keeps the raw way out to the one place it is needed. It is quieter about a refusal
///     than the read is; the port's own doc comment says what that costs and why it is worn.
///     <para>
///         Nothing here is caught. This is the Discover screen's only call, so a failure it swallowed would be drawn as
///         "nobody suggested" — the screen's enquiry turns a thrown failure into the shell's notice instead.
///     </para>
/// </summary>
public sealed class FollowSuggestions(IMastodonClientFactory clientFactory, IHttpClientFactory httpClientFactory)
    : IFollowSuggestions
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<Suggestion>> Read(
        ActiveProfile profile,
        int limit,
        CancellationToken cancellationToken)
    {
        var answered = await RawMastodonCall.Get<IReadOnlyList<SuggestionWire>>(
            httpClientFactory,
            profile,
            "api/v2/suggestions",
            [new KeyValuePair<string, string>("limit", limit.ToString())],
            cancellationToken);

        // A body that is nothing at all is not a payload this endpoint documents. Read as nobody rather than reported
        // as unanswered, because there is no unanswered to report: a caller that failed to ask heard an exception, so
        // everything that gets this far is an instance that answered.
        return answered is null
            ? []
            : answered.Where(suggestion => suggestion.Account is not null)
                      .Select(suggestion => ToSuggestion(suggestion, profile.Instance))
                      .ToList();
    }

    /// <inheritdoc />
    public async Task Dismiss(ActiveProfile profile, string accountId, CancellationToken cancellationToken)
    {
        // Mastonet's own calls take no cancellation token, so a Ctrl-C lands before the call rather than during it.
        cancellationToken.ThrowIfCancellationRequested();

        var client = clientFactory.CreateClient(profile.Instance, profile.AccessToken);

        await client.DeleteFollowSuggestion(accountId);
    }

    /// <summary>
    ///     One suggestion as this client holds it. The sources cross through <see cref="SuggestionReasonName" /> and
    ///     any this client has no heading for drop out, leaving the account: an instance that has learned a sixth
    ///     reason has still named somebody worth showing, and what a row with no heading over it does is the screen's
    ///     decision rather than this adapter's.
    /// </summary>
    private static Suggestion ToSuggestion(SuggestionWire suggestion, string instance) => new()
    {
        // Null-forgiven: the entries without an account were dropped before this was reached, an entry naming nobody
        // being a suggestion of nobody rather than a suggestion with a hole in it.
        Account = AccountWire.ToAccount(suggestion.Account!, instance),
        Reasons = (suggestion.Sources ?? [])
                  .Select(SuggestionReasonName.Parse)
                  .OfType<SuggestionReason>()
                  .ToList(),
    };
}
