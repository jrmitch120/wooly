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
///     (<see cref="OffAPost" />, #193) — <see cref="Opening" /> inside a post, where the post it would open is the
///     one already on screen (#48) — and, on a post that is picked, the ones that would refuse it: pin, edit and delete
///     on somebody else's (<see cref="NotYours" />), and <c>x</c> on one with nothing left to ask past
///     (<see cref="NothingHidden" />, #220).
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

    /// <summary>
    ///     The ones of these worth reminding somebody of, in rank: what a reader does to a post most, and the two
    ///     that reach past it to its author and to a fresh post (#218).
    /// </summary>
    private static IReadOnlyList<KeyHint> Reminding { get; } =
    [
        Opening,
        new("r", "reply"),
        new("b", "boost"),
        new("f", "favorite"),
        new("a", "author"),
        Composing,
    ];

    /// <summary>
    ///     Asking past the picked post's warning. Named on its own because it acts only on a post with something still
    ///     hidden on this screen, and a reveal is one-way — so it comes off the row once pressed (#220).
    /// </summary>
    private static KeyHint Revealing { get; } = new("x", "show warning");

    /// <summary>
    ///     The three an instance lets a post's author do and nobody else, which is why they come off the row on
    ///     anybody else's post (#220).
    /// </summary>
    private static IReadOnlyList<KeyHint> Yours { get; } =
    [
        new("p", "pin"),
        new("e", "edit"),
        new("d", "delete"),
    ];

    /// <summary>
    ///     And the rest, which rank behind <see cref="Scrolling" /> in the tail: <c>x</c> is learned on the first
    ///     warning a reader meets, and the other three act only on your own posts (#218).
    /// </summary>
    private static IReadOnlyList<KeyHint> Learnable { get; } = [Revealing, .. Yours];

    /// <summary>What every screen with posts on it answers to, in the rank the status row draws them in.</summary>
    public static IReadOnlyList<KeyHint> OnAPost { get; } = [.. Reminding, .. Learnable];

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
    ///     In front for the reason <see cref="Around(KeyHint, IReadOnlyList{KeyHint}, KeyHint[])" /> gives: the row
    ///     draws from the front, and these two mean something only on the post being read right now.
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
    ///     <paramref name="keys" /> with pin, edit and delete taken out, for a screen whose picked post is somebody
    ///     else's — which an instance refuses all three on, so announcing them is announcing a refusal (#220).
    /// </summary>
    /// <remarks>By hint rather than by letter, for the reason <see cref="OffAPost" /> gives: <c>d</c> dismisses.</remarks>
    public static IReadOnlyList<KeyHint> NotYours(IReadOnlyList<KeyHint> keys) =>
        [.. keys.Where(key => !Yours.Contains(key))];

    /// <summary>
    ///     <paramref name="keys" /> with <c>x</c> taken out, for a screen whose picked post has nothing left to ask past
    ///     — because it hides nothing, or because it has been asked past here already (#220).
    /// </summary>
    public static IReadOnlyList<KeyHint> NothingHidden(IReadOnlyList<KeyHint> keys) =>
        [.. keys.Where(key => key != Revealing)];

    /// <summary>
    ///     Those keys in front of <paramref name="keys" />, standing in for any of them they share a key with — so that
    ///     <c>⏎</c> is announced once, as what it does to the reference rather than to the post it is inside.
    /// </summary>
    /// <remarks>
    ///     In front for the reason <see cref="Around(KeyHint, IReadOnlyList{KeyHint}, KeyHint[])" /> gives: the status
    ///     row is one row and draws from the front, so the keys that only mean something right now are the ones that
    ///     have to be there.
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
    ///     In the tail of the rank, behind <c>b</c> and <c>f</c> as well as a screen's own keys, for the reason
    ///     <see cref="Around(KeyHint, IReadOnlyList{KeyHint}, KeyHint[])" /> gives: it means the same thing everywhere
    ///     and is the definition of a key learnable somewhere else (#218).
    /// </remarks>
    public static KeyHint Scrolling { get; } = new("↓/↑", "row");

    /// <summary>
    ///     The pin at the end of every row that answers to it, where the keys the row had no room for can be found.
    ///     <see cref="ChromeLines.Status" /> never cuts it.
    /// </summary>
    public static KeyHint Asking { get; } = new("?", "keys");

    /// <summary>
    ///     The end of the rank on a screen with no posts on it: the way out, then the tail, then <see cref="Asking" />
    ///     — what a screen building its own row puts after its own keys, so that it inherits the order rather than
    ///     restating it (#218).
    /// </summary>
    /// <param name="way">The way out of it: <c>esc</c>, or <c>tab</c> at the bottom of the stack.</param>
    public static IReadOnlyList<KeyHint> Leaving(KeyHint way) => [way, Scrolling, Asking];

    /// <summary>Those keys, after whatever this screen calls moving the selection, in the rank the row draws them.</summary>
    public static IReadOnlyList<KeyHint> Around(KeyHint moving, params KeyHint[] after) =>
        Around(moving, [], after);

    /// <summary>
    ///     The same, with the keys this screen alone answers to in front of the shared ones — and standing in for any
    ///     of them they share a letter with, so that <c>d</c> is announced as dismiss where it dismisses.
    /// </summary>
    /// <remarks>
    ///     The order is the rank, stated here once so that a screen adding a key inherits it (#218,
    ///     <c>docs/tui-shell.md</c>): the walk; the screen's own keys, including <c>g</c>; the way out; the shared
    ///     post keys worth a reminder; the tail, which can be learned anywhere or acts only on your own posts; and
    ///     <see cref="Asking" />. The status row draws as many as it has room for from the front and counts the rest,
    ///     so the keys a reader can find on no other screen are the ones that have to be at the front.
    /// </remarks>
    /// <param name="moving">What this screen calls moving the selection.</param>
    /// <param name="its">
    ///     The keys this screen answers to that the shared ones below do not carry — the ones a reader can find
    ///     nowhere else, and <see cref="Screen.Refreshing" /> where this screen has something to ask again (#84).
    /// </param>
    /// <param name="after">
    ///     The way out of it: <c>esc</c>, or <c>tab</c> at the bottom of the stack. Ahead of the shared keys rather
    ///     than at the end, where <c>tab:destination</c> used to fall off the row on the feed.
    /// </param>
    public static IReadOnlyList<KeyHint> Around(KeyHint moving, IReadOnlyList<KeyHint> its, params KeyHint[] after)
    {
        var taken = its.Select(key => key.Key).ToHashSet(StringComparer.Ordinal);

        return
        [
            moving,
            .. its,
            .. after,
            .. Reminding.Where(key => !taken.Contains(key.Key)),
            Scrolling,
            .. Learnable.Where(key => !taken.Contains(key.Key)),
            Asking,
        ];
    }
}
