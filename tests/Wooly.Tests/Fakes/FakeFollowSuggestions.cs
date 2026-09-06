using Wooly.Core.Accounts;
using Wooly.Core.Discovery;
using Wooly.Core.Errors;
using Wooly.Core.Profiles;

namespace Wooly.Tests.Fakes;

/// <summary>
///     An instance's suggestions without the instance. ADR-0005's primary seam for anything above the API layer: a
///     screen test says who is being offered and why, then asks what was read and who was dismissed, and never fakes
///     HTTP to do it.
///     <para>
///         Dismissals are recorded and nothing else happens to them. The port's own contract is that a dismissal is
///         one-way and quiet about a refusal, so a fake that dropped the person from the next read would let a screen
///         be written that depends on a list moving under it — which is the one thing the Discover screen must not do.
///     </para>
/// </summary>
internal sealed class FakeFollowSuggestions : IFollowSuggestions
{
    private readonly RateLimitedException? _refusal;
    private readonly IReadOnlyList<Suggestion> _suggestions;

    private FakeFollowSuggestions(IReadOnlyList<Suggestion> suggestions, RateLimitedException? refusal = null)
    {
        _suggestions = suggestions;
        _refusal = refusal;
    }

    /// <summary>Every read it was asked for, in order — where a test proves what a screen asked for on arrival.</summary>
    public List<Asked> Reads { get; } = [];

    /// <summary>Every account it was told to stop suggesting, in order.</summary>
    public List<Dismissed> Dismissals { get; } = [];

    /// <summary>An instance offering <paramref name="suggestions" />, in the order it is asked to offer them.</summary>
    public static FakeFollowSuggestions Offering(params Suggestion[] suggestions) =>
        new(suggestions.Length == 0 ? [ASuggestion.With()] : suggestions);

    /// <summary>
    ///     An instance offering nobody — a new account, a small instance, or one with suggestions turned off. A real
    ///     answer rather than a failure, and the one a screen draws <c>Nobody suggested.</c> for.
    /// </summary>
    public static FakeFollowSuggestions OfferingNobody() => new([]);

    /// <summary>
    ///     An instance whose rate limit refuses the read outright. There is no half-answer to hand back: the screen is
    ///     one call, and what a caller does with this is draw the shell's notice rather than an empty screen.
    /// </summary>
    public static FakeFollowSuggestions RateLimited() =>
        new([], new RateLimitedException("mastodon.social", new DateTimeOffset(2026, 7, 29, 13, 0, 0, TimeSpan.Zero)));

    public Task<IReadOnlyList<Suggestion>> Read(ActiveProfile profile, int limit, CancellationToken cancellationToken)
    {
        Reads.Add(new Asked(profile.Name, limit));

        return _refusal is null
            ? Task.FromResult(_suggestions)
            : Task.FromException<IReadOnlyList<Suggestion>>(_refusal);
    }

    public Task Dismiss(ActiveProfile profile, string accountId, CancellationToken cancellationToken)
    {
        Dismissals.Add(new Dismissed(profile.Name, accountId));

        return _refusal is null ? Task.CompletedTask : Task.FromException(_refusal);
    }

    /// <summary>One read: which profile asked, and how many it asked for.</summary>
    internal sealed record Asked(string Profile, int Limit);

    /// <summary>One dismissal: which profile made it, and who it was about.</summary>
    internal sealed record Dismissed(string Profile, string AccountId);
}
