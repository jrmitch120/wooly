using Wooly.Core.Accounts;
using Wooly.Tui.Rendering;
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
    public override string Crumb => "follow requests";

    /// <inheritdoc />
    public override bool Refreshes => true;

    /// <inheritdoc />
    protected override IReadOnlyList<KeyHint> OwnKeys =>
    [
        new("j/k", "request"),
        new("⏎", "read them"),
        new("a", "accept"),
        new("x", "reject"),
        Refreshing,
        PostKeys.Scrolling,
        new("tab", "destination"),
        new("?", "keys"),
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

    /// <summary>Takes the account <paramref name="accountId" /> names off the list, once their request was answered.</summary>
    public void Answered(string accountId) => _waiting.Remove(account => account.Id == accountId);

    /// <inheritdoc />
    public override IReadOnlyList<Line> Lines(Drawing drawing)
    {
        var lines = new List<Line>();

        if (Notice is { } notice)
        {
            lines.Add(Line.Of(TextWrap.Clip(notice, drawing.Width), Role.Muted));
            lines.Add(Line.Blank);
        }

        lines.AddRange(_waiting.Rows(
            drawing.Width,
            (account, _, room) => [AccountLines.Byline(account, room), AccountLines.Presence(account, room)]));

        return lines;
    }
}
