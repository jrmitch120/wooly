namespace Wooly.Tui.Screens;

/// <summary>
///     The keys that act on whichever post is picked out — and, since #83, the ones that act on a reference picked
///     out inside it. Listed once, because the shell acts on <see cref="Screen.Picked" /> without caring which screen
///     it came from — so a screen whose status row left one of these out would be a screen where a key fires
///     unannounced.
/// </summary>
/// <remarks>
///     The rule runs the other way too, which is the one reason a screen may leave one of these off: a key that has
///     nothing to act on here must not be announced either. Which is every one of these bar <see cref="Composing" />
///     on a screen with no post picked out — asked once by <see cref="Screen.Keys" /> rather than screen by screen
///     (<see cref="OffAPost" />, #193) — and <see cref="Opening" /> inside a post, where the post it would open is the
///     one already on screen (#48).
/// </remarks>
public static class PostKeys
{
    /// <summary>
    ///     Drilling into the picked post. Named on its own because it is the one key of these a screen may have a post
    ///     to act on and still nothing to do with: inside a post, the post itself is already open (#48).
    /// </summary>
    public static KeyHint Opening { get; } = new("⏎", "read");

    /// <summary>
    ///     Writing a fresh post. Named on its own because it is the one key of these that does not act on the picked
    ///     post at all: a post is written from anywhere, so it is what stays on the row where nothing is picked out
    ///     (#193).
    /// </summary>
    public static KeyHint Composing { get; } = new("c", "compose");

    /// <summary>What every screen with posts on it answers to, in the order the status row reads best.</summary>
    public static IReadOnlyList<KeyHint> OnAPost { get; } =
    [
        Opening,
        new("a", "author"),
        Composing,
        new("r", "reply"),
        new("b", "boost"),
        new("f", "favorite"),
        new("p", "pin"),
        new("e", "edit"),
        new("d", "delete"),
        new("x", "show warning"),
    ];

    /// <summary>
    ///     What a picked reference answers to: walking the references inside the post, opening the one picked out, and
    ///     letting it go again (#83). <c>⏎</c> is announced once and means three things — a hashtag's timeline, the
    ///     account a mention names, or an address in the platform's browser (#85) — because what a reader has to know
    ///     is that the key opens whatever is bracketed, not which of the three this one is.
    /// </summary>
    private static IReadOnlyList<KeyHint> Walking { get; } =
    [
        new("←/→", "reference"),
        new("⏎", "open"),
        new("esc", "back"),
    ];

    /// <summary>
    ///     What a poll on the picked post answers to: the digits that address its answers, and the key that casts what
    ///     they toggled (#87). <c>1</c>-<c>9</c> then <c>0</c> reach up to ten options, said as the range rather than
    ///     as ten hints — a status row listing every digit would be the whole row.
    /// </summary>
    private static IReadOnlyList<KeyHint> Voting { get; } =
    [
        new("1-0", "option"),
        new("v", "vote"),
    ];

    /// <summary>
    ///     Those two in front of <paramref name="keys" />, on a screen whose picked post carries a poll — and nowhere
    ///     else, since a digit pressed on a post with no poll does nothing and must not be announced as if it did.
    /// </summary>
    /// <remarks>
    ///     In front for the reason <see cref="Around(KeyHint, IReadOnlyList{KeyHint}, KeyHint[])" /> gives: the row is
    ///     cut off at the right, and these two mean something only on the post being read right now.
    /// </remarks>
    public static IReadOnlyList<KeyHint> OnAPoll(IReadOnlyList<KeyHint> keys) => InFrontOf(Voting, keys);

    /// <summary>
    ///     The ones of these that act on the picked post and nothing else, which is all of them bar
    ///     <see cref="Composing" /> — what a screen leaves off its row while no post is picked out
    ///     (<c>docs/tui-shell.md</c>, ADR-0019).
    /// </summary>
    /// <remarks>
    ///     Named here rather than by the screens that have somewhere else to stand, because the rule they follow is
    ///     this module's: a key with nothing to act on must not be announced. A follow notification is the precedent —
    ///     it carries no post, and the keys act on nothing rather than guessing at something (#179) — and the account
    ///     screen's header block, an empty feed and an empty inbox are the same position (#193).
    /// </remarks>
    private static IReadOnlyList<KeyHint> ActingOnAPost { get; } = [.. OnAPost.Where(key => key != Composing)];

    /// <summary>
    ///     <paramref name="keys" /> with those taken out, for a screen announcing its keys while no post is picked
    ///     out. Asked by <see cref="Screen.Keys" /> for every screen at once, so that a screen added later inherits
    ///     the rule without knowing it exists.
    /// </summary>
    /// <remarks>
    ///     Taken out by hint rather than by key, so that a screen saying something of its own by one of those letters
    ///     keeps it: <c>d</c> dismisses a notification, <c>m</c> marks a conversation read, and <c>a</c> accepts a
    ///     follow request, none of which is a post key and all of which act. Matching on the letter alone was enough
    ///     while the account screen was the one caller (#179) and is wrong shell-wide (#193) — a key means what its
    ///     screen says it means, which is the whole reason a <see cref="KeyHint" /> is a pair.
    /// </remarks>
    public static IReadOnlyList<KeyHint> OffAPost(IReadOnlyList<KeyHint> keys) =>
        [.. keys.Where(key => !ActingOnAPost.Contains(key))];

    /// <summary>
    ///     Those keys in front of <paramref name="keys" />, standing in for any of them they share a key with — so that
    ///     <c>⏎</c> is announced once, as what it does to the reference rather than to the post it is inside.
    /// </summary>
    /// <remarks>
    ///     In front for the reason <see cref="Around(KeyHint, IReadOnlyList{KeyHint}, KeyHint[])" /> gives: the status
    ///     row is one row and a longer list is cut off at the right, so the keys that only mean something right now
    ///     are the ones that have to survive the cut.
    /// </remarks>
    public static IReadOnlyList<KeyHint> OnAReference(IReadOnlyList<KeyHint> keys) => InFrontOf(Walking, keys);

    /// <summary>
    ///     <paramref name="inside" /> ahead of <paramref name="keys" />, standing in for any of them it shares a key
    ///     with — so that a key doing two things at once is announced as the innermost of them.
    /// </summary>
    /// <remarks>
    ///     Said once for both of the things a reader can be inside on the picked post, a reference and a poll: two
    ///     copies of this would be two chances for one of them to announce <c>⏎</c> twice.
    /// </remarks>
    private static IReadOnlyList<KeyHint> InFrontOf(
        IReadOnlyList<KeyHint> inside,
        IReadOnlyList<KeyHint> keys)
    {
        var taken = inside.Select(key => key.Key).ToHashSet(StringComparer.Ordinal);

        return [.. inside, .. keys.Where(key => !taken.Contains(key.Key))];
    }

    /// <summary>
    ///     Walking the screen a row at a time, which every screen with rows on it answers to and none of them owns —
    ///     the other half of the movement <c>j</c> and <c>k</c> make, and the half that can reach the foot of a post
    ///     taller than the terminal (#51).
    /// </summary>
    /// <remarks>
    ///     Shared, so it goes behind a screen's own keys rather than in front of them, for the reason
    ///     <see cref="Around(KeyHint, IReadOnlyList{KeyHint}, KeyHint[])" /> gives: it means the same thing everywhere
    ///     and can be learned somewhere else, and the row is cut off at the right.
    /// </remarks>
    public static KeyHint Scrolling { get; } = new("↓/↑", "row");

    /// <summary>Those keys, after whatever this screen calls moving the selection and before the way out of it.</summary>
    public static IReadOnlyList<KeyHint> Around(KeyHint moving, params KeyHint[] after) =>
        Around(moving, [], after);

    /// <summary>
    ///     The same, with the keys this screen alone answers to in front of the shared ones — and standing in for any
    ///     of them they share a letter with, so that <c>d</c> is announced as dismiss where it dismisses.
    /// </summary>
    /// <remarks>
    ///     In front rather than behind, because the status row is one row and a list longer than it is cut off at the
    ///     right (<c>docs/tui-shell.md</c>). The keys a reader can find on no other screen are the ones that have to
    ///     survive the cut; the ones that mean the same thing everywhere can be learned somewhere else.
    /// </remarks>
    /// <param name="moving">What this screen calls moving the selection.</param>
    /// <param name="its">
    ///     The keys this screen answers to that the shared ones below do not carry — the ones a reader can find
    ///     nowhere else, and <see cref="Screen.Refreshing" /> where this screen has something to ask again (#84).
    /// </param>
    /// <param name="after">The way out of it, and anything else that belongs at the end.</param>
    public static IReadOnlyList<KeyHint> Around(KeyHint moving, IReadOnlyList<KeyHint> its, params KeyHint[] after)
    {
        var taken = its.Select(key => key.Key).ToHashSet(StringComparer.Ordinal);

        return
        [
            moving,
            .. its,
            Scrolling,
            .. OnAPost.Where(key => !taken.Contains(key.Key)),
            .. after,
            new KeyHint("?", "keys"),
        ];
    }
}
