using Wooly.Core.Accounts;
using Wooly.Core.Errors;
using Wooly.Core.Paging;
using Wooly.Core.Profiles;
using Wooly.Core.Relationships;

namespace Wooly.Tests.Fakes;

/// <summary>
///     Relationships without an instance to keep them on. ADR-0005's primary seam for anything above the API layer: a
///     command test says what came back and then asks what was managed, and never fakes HTTP to do it.
/// </summary>
internal sealed class FakeAccountRelationships : IAccountRelationships
{
    private readonly Fetch<Account> _list;
    private readonly Exception? _refusal;
    private Account _subject;

    private FakeAccountRelationships(Account subject, Fetch<Account> list, Exception? refusal = null)
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

    /// <summary>Every ask for who two accounts have in common, in order.</summary>
    public List<Familiar> Familiars { get; } = [];

    /// <summary>
    ///     Every batched ask for where the profile stands with a page of accounts that <em>reached the instance</em>,
    ///     in order. An empty ask is not one of them, this fake gating it exactly as the adapter does — so a test
    ///     reading this is asking "what did the instance hear", which is what a call costs anything to make.
    /// </summary>
    public List<Stood> Standings { get; } = [];

    /// <summary>
    ///     Where the profile stands with everyone a batched ask names — the same standing for all of them, since a
    ///     test asserting a row is about what the row says rather than about who is on it. Set to
    ///     <see langword="null" /> for an instance that never answered, which is the state a list draws silent rows
    ///     for (#180).
    /// </summary>
    public AccountStanding? Stands { get; set; } = AnAccount.Standing();

    /// <summary>
    ///     Who the profile and the account asked about both follow. Nobody by default, since that is the ordinary
    ///     answer; set to <see langword="null" /> for an instance that never answered the question, which is the state
    ///     a screen draws nothing at all for.
    /// </summary>
    public IReadOnlyList<Account>? InCommon { get; set; } = [];

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
        new(subject ?? AnAccount.With(), Fetch<Account>.Complete(listing.Length == 0 ? [AnAccount.With()] : listing));

    /// <summary>
    ///     Who this instance answers about from here on — what an instance did while the reader was reading it, which
    ///     is what a refresh is asked to notice (<see cref="FakeTimelineReader.NowHolding" /> for the timelines).
    ///     Answering with a different id is how a test says that somebody moved instances.
    /// </summary>
    public void NowShowing(Account subject) => _subject = subject;

    /// <summary>An instance whose lists have nobody on them.</summary>
    public static FakeAccountRelationships HoldingNobody() => new(AnAccount.With(), Fetch<Account>.Complete([]));

    /// <summary>An instance whose rate limit stopped the list with <paramref name="listing" /> already in hand.</summary>
    public static FakeAccountRelationships RateLimitedAfter(params Account[] listing) =>
        new(
            AnAccount.With(),
            Fetch<Account>.StoppedShort(
                listing,
                new RateLimitedException("mastodon.social", new DateTimeOffset(2026, 7, 29, 13, 0, 0, TimeSpan.Zero))));

    /// <summary>An instance that refuses everything with <paramref name="refusal" />, having recorded the attempt.</summary>
    public static FakeAccountRelationships Refusing(Exception refusal) =>
        new(AnAccount.With(), Fetch<Account>.Complete([]), refusal);

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

    public Task<Fetch<Account>> List(
        ActiveProfile profile,
        FollowSide side,
        NamedAccount? account,
        int limit,
        CancellationToken cancellationToken)
    {
        Lists.Add(new Listed(profile.Name, side, account, limit));

        return _refusal is null ? Task.FromResult(_list) : Task.FromException<Fetch<Account>>(_refusal);
    }

    /// <summary>
    ///     Answers <see cref="InCommon" />, and answers it even when this instance is refusing everything else: the
    ///     port says a failure here is reported as nothing rather than thrown, and a fake that threw would let a screen
    ///     be written that only works against an instance that never refuses.
    /// </summary>
    public Task<IReadOnlyList<Account>?> FamiliarFollowers(
        ActiveProfile profile,
        string accountId,
        CancellationToken cancellationToken)
    {
        Familiars.Add(new Familiar(profile.Name, accountId));

        return Task.FromResult(_refusal is null ? InCommon : null);
    }

    /// <summary>
    ///     Answers with the accounts it was given, each carrying <see cref="Stands" /> — or with nothing at all where
    ///     that is what a test set, which is the state a list draws no standing on at all.
    /// </summary>
    /// <remarks>
    ///     An empty ask answers its own input and is not recorded, exactly as the adapter does: it returns before it
    ///     creates a client, so no instance hears about it. That the port really does gate it — rather than this fake
    ///     alone believing so — is pinned against real HTTP by
    ///     <c>AccountRelationshipsTests.Standing_AsksNothingOfTheInstanceForAnEmptyList</c>, which is what lets the
    ///     shell's own callers go on with no guard of their own (#204).
    /// </remarks>
    public Task<IReadOnlyList<Account>?> Standing(
        ActiveProfile profile,
        IReadOnlyList<Account> accounts,
        CancellationToken cancellationToken)
    {
        if (accounts.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<Account>?>(accounts);
        }

        Standings.Add(new Stood(profile.Name, [.. accounts.Select(account => account.Id)]));

        return Task.FromResult<IReadOnlyList<Account>?>(
            Stands is null ? null : [.. accounts.Select(account => account with { Standing = Stands })]);
    }

    public Task<Fetch<Account>> PendingRequests(ActiveProfile profile, int limit, CancellationToken cancellationToken)
    {
        Lists.Add(new Listed(profile.Name, Side: null, Account: null, limit));

        return _refusal is null ? Task.FromResult(_list) : Task.FromException<Fetch<Account>>(_refusal);
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

    /// <summary>
    ///     One list: which profile asked, which side of a follow — none for the pending requests — and about whom.
    ///     Whether the account named carried an id is what a test reads to prove a caller passed on the resolution it
    ///     was holding rather than paying for another (#184).
    /// </summary>
    internal sealed record Listed(string Profile, FollowSide? Side, NamedAccount? Account, int Limit);

    /// <summary>One answered request: which profile answered, whose request, and what they said.</summary>
    internal sealed record Answered(string Profile, string AccountId, bool Accepted);

    /// <summary>One account read: which profile asked, and whose account they asked about.</summary>
    internal sealed record Shown(string Profile, AccountAddress Account);

    /// <summary>One ask for familiar followers: which profile asked, and whose they asked for.</summary>
    internal sealed record Familiar(string Profile, string AccountId);

    /// <summary>One batched ask for standing: which profile asked, and about whom.</summary>
    internal sealed record Stood(string Profile, IReadOnlyList<string> AccountIds);
}
