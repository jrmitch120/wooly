using Wooly.Core.Accounts;
using Wooly.Tui.Media;
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
    private readonly List<Account> _waiting = [.. waiting];

    /// <inheritdoc />
    public override string Crumb => "follow requests";

    /// <inheritdoc />
    public override IReadOnlyList<KeyHint> Keys =>
    [
        new("j/k", "request"),
        PostKeys.Scrolling,
        new("⏎", "read them"),
        new("a", "accept"),
        new("x", "reject"),
        new("tab", "destination"),
        new("?", "keys"),
    ];

    /// <summary>Which request is picked out, as an index into what is on screen.</summary>
    public int At { get; private set; }

    /// <summary>Who is waiting, in the order the instance listed them.</summary>
    public IReadOnlyList<Account> Waiting => _waiting;

    /// <summary>
    ///     Something the shell has to say about the list rather than about anybody on it — that nobody is waiting, or
    ///     that a rate limit cut the read short.
    /// </summary>
    public string? Notice { get; } = notice;

    /// <summary>
    ///     Whoever is picked out, or <see langword="null" /> where nobody is waiting. What <c>a</c> and <c>x</c>
    ///     answer, named by their id — which is what answering a request takes (ADR-0012).
    /// </summary>
    public Account? PickedAccount => _waiting.Count == 0 ? null : _waiting[At];

    /// <inheritdoc />
    public override void Move(int by)
    {
        if (_waiting.Count > 0)
        {
            At = PickedPosts.Clamped(At, by, _waiting.Count - 1);
        }
    }

    /// <inheritdoc />
    public override void Pick(int at)
    {
        if (_waiting.Count > 0)
        {
            At = PickedPosts.Chosen(at, _waiting.Count - 1);
        }
    }

    /// <summary>Takes the account <paramref name="accountId" /> names off the list, once their request was answered.</summary>
    public void Answered(string accountId)
    {
        _waiting.RemoveAll(account => account.Id == accountId);

        At = _waiting.Count == 0 ? 0 : Math.Clamp(At, 0, _waiting.Count - 1);
    }

    /// <inheritdoc />
    public override IReadOnlyList<Line> Lines(int width, DateTimeOffset now, IPictures? pictures = null)
    {
        var lines = new List<Line>();

        if (Notice is { } notice)
        {
            lines.Add(Line.Of(TextWrap.Clip(notice, width), Role.Muted));
            lines.Add(Line.Blank);
        }

        var room = Math.Max(1, width - 1);

        for (var at = 0; at < _waiting.Count; at++)
        {
            var account = _waiting[at];

            lines.Add(AccountLines.Byline(account, room).After(PickedPosts.Gutter(at == At)).PartOf(at));
            lines.Add(AccountLines.Presence(account, room).After(PickedPosts.Gutter(at == At)).PartOf(at));
            lines.Add(Line.Blank);
        }

        return lines;
    }
}
