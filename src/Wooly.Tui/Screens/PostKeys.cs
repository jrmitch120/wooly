namespace Wooly.Tui.Screens;

/// <summary>
///     The keys that act on whichever post is picked out — and, since #83, the ones that act on a reference picked
///     out inside it. Listed once, because the shell acts on <see cref="Screen.Picked" /> without caring which screen
///     it came from — so a screen whose status row left one of these out would be a screen where a key fires
///     unannounced.
/// </summary>
/// <remarks>
///     The rule runs the other way too, which is the one reason a screen may leave one of these off: a key that has
///     nothing to act on here must not be announced either. Only <see cref="Opening" /> is ever in that position, and
///     only inside a post (#48).
/// </remarks>
public static class PostKeys
{
    /// <summary>
    ///     Drilling into the picked post. Named on its own because it is the one key of these a screen may have a post
    ///     to act on and still nothing to do with: inside a post, the post itself is already open (#48).
    /// </summary>
    public static KeyHint Opening { get; } = new("⏎", "read");

    /// <summary>What every screen with posts on it answers to, in the order the status row reads best.</summary>
    public static IReadOnlyList<KeyHint> OnAPost { get; } =
    [
        Opening,
        new("a", "author"),
        new("c", "compose"),
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
