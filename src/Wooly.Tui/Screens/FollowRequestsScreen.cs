using Wooly.Core.Accounts;
using Wooly.Tui.Rendering;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     The follows waiting to be answered, which only a locked account ever has any of — an unlocked one is followed
///     rather than asked (CONTEXT.md). A rail destination, carrying its own count.
/// </summary>
/// <remarks>
///     Each row is a person, not a post, so this screen answers to none of the keys that act on one: <c>a</c> accepts
///     and <c>x</c> rejects here, where on a feed they open an author and show a warning. That collision is what the
///     status row exists to make workable (<c>docs/tui-shell.md</c>).
/// </remarks>
public sealed class FollowRequestsScreen(IReadOnlyList<Account> waiting, string? notice = null) : Screen
{
    // Named by the id the instance listed them under, which is what answering a request takes (ADR-0012) — and what a
    // refresh puts the reader back on (#84).
    private readonly Picked<Account> _waiting = new(waiting);

    /// <inheritdoc />
    public override string Crumb => "Follow requests";

    /// <inheritdoc />
    public override bool Refreshes => true;

    /// <inheritdoc />
    protected override IReadOnlyList<KeyHint> OwnKeys =>
    [
        new("j/k", "request"),
        new("⏎", "read them", NeedsAPick: true),
        new("a", "accept", NeedsAPick: true),
        new("x", "reject", NeedsAPick: true),
        Refreshing,
        .. PostKeys.Leaving(new KeyHint("tab", "destination")),
    ];

    /// <summary>Which request is picked out, as an index into what is on screen.</summary>
    public int At => _waiting.At;

    /// <summary>Who is waiting, in the order the instance listed them.</summary>
    public IReadOnlyList<Account> Waiting => _waiting.All;

    /// <summary>
    ///     Something the shell has to say about the list rather than about anybody on it — that nobody is waiting, or
    ///     that a rate limit cut the read short.
    /// </summary>
    public string? Notice { get; } = notice;

    /// <summary>
    ///     Whoever is picked out, or <see langword="null" /> where nobody is waiting. What <c>a</c> and <c>x</c>
    ///     answer, named by their id — which is what answering a request takes (ADR-0012).
    /// </summary>
    public Account? PickedAccount => _waiting.Out;

    /// <inheritdoc />
    protected override IPicked Walking => _waiting;

    /// <summary>
    ///     What this screen's own keys do: <c>a</c> accepts the picked request, <c>x</c> turns it away, and <c>⏎</c>
    ///     opens whoever is asking, so the question can be answered knowing who asked.
    /// </summary>
    public override Task Answer(Verb verb, Reach reach) => (verb, PickedAccount) switch
    {
        (Verb.AcceptRequest, { } picked) => AnswerRequest(reach, picked, accepted: true),
        (Verb.RejectRequest, { } picked) => AnswerRequest(reach, picked, accepted: false),
        (Verb.OpenAsker, { } picked) => reach.Open(
            new Subject.Account(AccountAddress.Parse(picked.Address), WithReplies: false)),
        _ => Task.CompletedTask,
    };

    /// <summary>Accepts or turns away <paramref name="picked" />'s request.</summary>
    private Task AnswerRequest(Reach reach, Account picked, bool accepted) =>
        // By id, as the list reports it, because that is what answering one takes: an address would cost a lookup to
        // arrive back at the id already in hand (ADR-0012).
        reach.Put(
            ask => ask.Of(token => reach.Ports.Accounts.Answer(reach.Profile, picked.Id, accepted, token)),
            // Whether or not the reader is still here: the request was answered on the instance, and what that makes
            // stale is a fact about the instance rather than about where anybody is standing (#233, #234).
            eitherWay: _ => reach.Tell(new Change.RequestAnswered(picked.Id)),
            ifStillHere: _ =>
            {
                reach.Say(
                    accepted ? $"@{picked.Address} can follow you." : $"@{picked.Address} was turned away.",
                    isError: false);
            });

    /// <inheritdoc />
    /// <remarks>Whoever was answered comes off the list, their request being no longer waiting on anybody.</remarks>
    public override bool Heard(Change change)
    {
        if (change is Change.RequestAnswered(var id))
        {
            _waiting.Remove(account => account.Id == id);
        }

        return false;
    }

    /// <inheritdoc />
    public override IReadOnlyList<Line> Lines(Drawing drawing)
    {
        var lines = new List<Line>();

        if (Notice is { } notice)
        {
            lines.Add(Line.Of(TextWrap.Clip(notice, drawing.Width), Role.Muted));
            lines.Add(Line.Blank);
        }

        // A blank between people rather than the rule this list used to draw after each of them: four-row Account
        // blocks laid end to end run together, and a blank costs the one row a rule would while leaving the list
        // looking like the list of people it is (#198).
        //
        // Nothing implied: what this list claims of everyone on it is that they do not follow the reader, which is a
        // negative, and the compact standing only ever adds words for positives — so there is nothing here to
        // suppress and "following" is the word this screen is read for (#204).
        lines.AddRange(_waiting.Rows(
            drawing.Width,
            (account, _, room) => AccountLines.Block(
                account,
                drawing.In(room),
                AccountLines.Compact(account.Standing)),
            Line.Blank));

        return lines;
    }
}
