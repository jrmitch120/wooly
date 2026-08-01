using Wooly.Core.Accounts;
using Wooly.Core.Errors;
using Wooly.Core.Profiles;
using Wooly.Core.Relationships;

namespace Wooly.Tests.Fakes;

/// <summary>
///     Relationships without an instance to keep them on. ADR-0005's primary seam for anything above the API layer: a
///     command test says what came back and then asks what was managed, and never fakes HTTP to do it.
/// </summary>
internal sealed class FakeAccountRelationships : IAccountRelationships
{
    private readonly AccountFetch _list;
    private readonly Exception? _refusal;
    private readonly Account _subject;

    private FakeAccountRelationships(Account subject, AccountFetch list, Exception? refusal = null)
    {
        _subject = subject;
        _list = list;
        _refusal = refusal;
    }

    /// <summary>Every tie it was asked to put on or take off, in order — where a test proves what a command asked for.</summary>
    public List<Tied> Ties { get; } = [];

    /// <summary>Every list it was asked for, in order.</summary>
    public List<Listed> Lists { get; } = [];

    /// <summary>Every follow request it was asked to answer, in order.</summary>
    public List<Answered> Answers { get; } = [];

    /// <summary>Every account it was asked to read, in order — where a test proves whose account a screen opened.</summary>
    public List<Shown> Reads { get; } = [];

    /// <summary>
    ///     What putting a tie on or taking one off answers with, where that is not what reading the account answers
    ///     with. An instance answers <c>Set</c> with the standing as it now is, so this is how a test says what
    ///     changed — without it, a follow would read back as the standing before it.
    /// </summary>
    public Account? Becoming { get; set; }

    /// <summary>
    ///     An instance that takes whatever it is asked, answers about <paramref name="subject" />, and holds
    ///     <paramref name="listing" /> in every list it is asked for.
    /// </summary>
    public static FakeAccountRelationships Holding(Account? subject = null, params Account[] listing) =>
        new(subject ?? AnAccount.With(), AccountFetch.Complete(listing.Length == 0 ? [AnAccount.With()] : listing));

    /// <summary>An instance whose lists have nobody on them.</summary>
    public static FakeAccountRelationships HoldingNobody() => new(AnAccount.With(), AccountFetch.Complete([]));

    /// <summary>An instance whose rate limit stopped the list with <paramref name="listing" /> already in hand.</summary>
    public static FakeAccountRelationships RateLimitedAfter(params Account[] listing) =>
        new(
            AnAccount.With(),
            AccountFetch.StoppedShort(
                listing,
                new RateLimitedException("mastodon.social", new DateTimeOffset(2026, 7, 29, 13, 0, 0, TimeSpan.Zero))));

    /// <summary>An instance that refuses everything with <paramref name="refusal" />, having recorded the attempt.</summary>
    public static FakeAccountRelationships Refusing(Exception refusal) =>
        new(AnAccount.With(), AccountFetch.Complete([]), refusal);

    public Task<Account> Set(
        ActiveProfile profile,
        AccountAddress account,
        AccountTie tie,
        bool wanted,
        CancellationToken cancellationToken)
    {
        Ties.Add(new Tied(profile.Name, account, tie, wanted));

        return Answer(Becoming ?? _subject);
    }

    public Task<Account> Show(ActiveProfile profile, AccountAddress account, CancellationToken cancellationToken)
    {
        Reads.Add(new Shown(profile.Name, account));

        return Answer(_subject);
    }

    public Task<AccountFetch> List(
        ActiveProfile profile,
        FollowSide side,
        AccountAddress? account,
        int limit,
        CancellationToken cancellationToken)
    {
        Lists.Add(new Listed(profile.Name, side, account, limit));

        return _refusal is null ? Task.FromResult(_list) : Task.FromException<AccountFetch>(_refusal);
    }

    public Task<AccountFetch> PendingRequests(ActiveProfile profile, int limit, CancellationToken cancellationToken)
    {
        Lists.Add(new Listed(profile.Name, Side: null, Account: null, limit));

        return _refusal is null ? Task.FromResult(_list) : Task.FromException<AccountFetch>(_refusal);
    }

    public Task<Account> Answer(
        ActiveProfile profile,
        string accountId,
        bool accepted,
        CancellationToken cancellationToken)
    {
        Answers.Add(new Answered(profile.Name, accountId, accepted));

        return Answer(_subject);
    }

    private Task<Account> Answer(Account account) =>
        _refusal is null ? Task.FromResult(account) : Task.FromException<Account>(_refusal);

    /// <summary>One tie: which profile put it there, on whom, and whether it was being put on or taken off.</summary>
    internal sealed record Tied(string Profile, AccountAddress Account, AccountTie Tie, bool Wanted);

    /// <summary>One list: which profile asked, which side of a follow — none for the pending requests — and about whom.</summary>
    internal sealed record Listed(string Profile, FollowSide? Side, AccountAddress? Account, int Limit);

    /// <summary>One answered request: which profile answered, whose request, and what they said.</summary>
    internal sealed record Answered(string Profile, string AccountId, bool Accepted);

    /// <summary>One account read: which profile asked, and whose account they asked about.</summary>
    internal sealed record Shown(string Profile, AccountAddress Account);
}
