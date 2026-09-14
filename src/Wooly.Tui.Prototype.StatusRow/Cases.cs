using Wooly.Tui.Screens;

namespace Wooly.Tui.Prototype.StatusRow;

/// <summary>
///     A screen with something picked out on it, holding the keys the shipped shell would announce there — taken from
///     the real <see cref="PostKeys" /> rather than typed out, so the widths below are the widths the terminal draws.
/// </summary>
/// <param name="Name">How the case is headed in the output.</param>
/// <param name="What">The state that produces it, in a sentence.</param>
/// <param name="Keys">What <c>Screen.Keys</c> would hand the status row.</param>
/// <param name="Idle">
///     The keys on it that are announced and can do nothing here — own-posts-only keys on somebody else's post,
///     <c>x</c> on a post hiding nothing. Not a thing the shipped shell knows: working out which these are is one of
///     the candidates.
/// </param>
public sealed record Case(string Name, string What, IReadOnlyList<KeyHint> Keys, string[] Idle)
{
    /// <summary>Whether this key can do anything on this screen right now.</summary>
    public bool Acts(KeyHint key) => !Idle.Contains(key.Key);
}

/// <summary>The screens the row has to hold on, worst first.</summary>
public static class Cases
{
    /// <summary>Own-posts-only keys, plus the reveal on a post with nothing to reveal.</summary>
    private static readonly string[] NotYours = ["p", "e", "d", "x"];

    /// <summary>What a post screen announces: the thread walk, <c>g</c>, the post keys, and the way back out.</summary>
    private static IReadOnlyList<KeyHint> Post { get; } = PostKeys.Around(
        new KeyHint("j/k", "thread"),
        [Screen.Refreshing],
        new KeyHint("esc", "back"));

    /// <summary>What a feed announces. The bottom of the stack, so <c>tab</c> rather than <c>esc</c>.</summary>
    private static IReadOnlyList<KeyHint> Feed { get; } = PostKeys.Around(
        new KeyHint("j/k", "post"),
        [Screen.Refreshing],
        new KeyHint("tab", "destination"));

    /// <summary>An account screen, which owns more letters than any other: the three ties, the follows, the sections.</summary>
    private static IReadOnlyList<KeyHint> Account { get; } = PostKeys.Around(
        new KeyHint("j/k", "post"),
        [
            new KeyHint("[/]", "section"),
            new KeyHint("F", "follow"),
            new KeyHint("M", "mute"),
            new KeyHint("B", "block"),
            new KeyHint("w", "follows"),
            Screen.Refreshing,
        ],
        new KeyHint("esc", "back"));

    /// <summary>The inbox, where <c>d</c> means dismiss and stands in for <c>d delete</c>.</summary>
    private static IReadOnlyList<KeyHint> Notifications { get; } = PostKeys.Around(
        new KeyHint("j/k", "notification"),
        [
            new KeyHint("d", "dismiss", NeedsAPick: true),
            new KeyHint("D", "clear all", NeedsAPick: true),
            Screen.Refreshing,
        ],
        new KeyHint("tab", "destination"));

    /// <summary>The six, worst first.</summary>
    public static IReadOnlyList<Case> All { get; } =
    [
        new(
            "post · a poll of your own, unvoted, under a warning",
            "The worst row the shell can draw: the poll keys in front of a full post row, every key acting.",
            PostKeys.OnAPoll(Post),
            []),
        new(
            "post · a reference picked inside somebody else's post",
            "The other in-front case. `OnAReference` stands in for ⏎ and esc, so it is two hints cheaper than the poll.",
            PostKeys.OnAReference(Post),
            NotYours),
        new(
            "post · somebody else's post, hiding nothing",
            "The ordinary post screen, four of whose keys can do nothing here.",
            Post,
            NotYours),
        new(
            "account · @maria, a post of theirs picked",
            "The screen that owns the most letters of its own: six in front of the shared ten.",
            Account,
            NotYours),
        new(
            "notifications · a mention picked",
            "Where `d` means dismiss and `D` clears the lot.",
            Notifications,
            ["p", "e", "x"]),
        new(
            "feed · home, somebody else's post",
            "The row a reader sees most.",
            Feed,
            NotYours),
    ];
}
