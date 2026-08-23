using Wooly.Tui.Screens;

namespace Wooly.Tui.Shell;

/// <summary>
///     What every key means on every screen: the contract in <c>docs/tui-shell.md</c>'s tables, as one table code can
///     be asked. A <see cref="ShellKey" /> and a <see cref="Screen" /> go in and a <see cref="Verb" /> comes out.
/// </summary>
/// <remarks>
///     One place rather than four. The frame keys, the keys that act on a post, the four whose meaning collides by
///     screen and the two a compose screen alone answers were each bound where they happened to arrive — a window, a
///     shell and an if-chain apiece — and none of them was the contract. Here they are one <c>switch</c> a reader can
///     read down against the document (#147).
///     <para>
///         This is the only place in the TUI that names a screen type to decide what a key means, and that is the
///         point of it: <c>ShellWindow</c> translates a press and hands it on, <see cref="Shell.Do" /> carries a verb
///         out, and neither has to know that <c>d</c> is dismiss on one screen and delete on every other.
///     </para>
///     <para>
///         <strong>What it does not answer is whether the press was used.</strong> <c>←</c>, <c>→</c> and the digits
///         are consumed only where there is something to walk or toggle, so that a compose editor still gets its own
///         arrows and a digit on a post with no poll falls through — and that is settled by the screen, which is the
///         only thing that knows what is on the post, and comes back as the <see langword="bool" />
///         <see cref="Shell.Do" /> returns. Answering it here would mean asking the same question in two places, and
///         two places for one question is how the two come to disagree.
///     </para>
///     <para>
///         Nor does it say what a screen <em>announces</em>. <see cref="Screen.Keys" /> and <see cref="PostKeys" /> are
///         deliberately untouched by this: what a screen offers and what it answers are asserted against each other
///         rather than derived from one another, because a key announced and then refused reads as a shell that missed
///         the press, and a single source would make that class of bug untestable (<see cref="Screen.Refreshes" />).
///     </para>
/// </remarks>
public static class Keymap
{
    /// <summary>What <paramref name="key" /> means on <paramref name="screen" />.</summary>
    /// <remarks>
    ///     Read down as the contract is written: the frame first, since those mean the same thing everywhere and a
    ///     screen may not take them back; then the two a compose screen alone answers; then the four collisions, each
    ///     with the screens that take it away above the meaning it has on all the rest; then the keys that mean one
    ///     thing wherever they are pressed.
    /// </remarks>
    public static Verb Means(ShellKey key, Screen screen) => (key, screen) switch
    {
        // The frame. What may not vary by screen (docs/tui-shell.md) — and the one exception, a prompt taking `/` and
        // `?` as letters, never reaches here: a screen that is typing takes them before a key is looked up at all.
        (ShellKey.CtrlQ, _) => Verb.Quit,
        (ShellKey.Escape, _) => Verb.Back,
        (ShellKey.Question, _) => Verb.Help,
        (ShellKey.Slash, _) => Verb.Search,
        (ShellKey.Tab, _) => Verb.NextDestination,
        (ShellKey.ShiftTab, _) => Verb.PreviousDestination,

        // Screen-local, and the reason this pair is here rather than on the editor widget alone: the editor gives up
        // focus while the warning is taking letters, and from there neither key would reach it (#123). Off a compose
        // screen they mean nothing and are left to whatever else wants them.
        (ShellKey.CtrlS, ComposeScreen) => Verb.Send,
        (ShellKey.CtrlW, ComposeScreen) => Verb.WriteWarning,

        // The four that collide. A picked reference is a level of its own inside the screen, so ⏎ means the reference
        // wherever one is picked — ahead of whatever the screen's own ⏎ would have meant (#85).
        (ShellKey.Enter, _) when screen.Reference is not null => Verb.OpenReference,
        (ShellKey.Enter, SearchScreen search) => search.IsTyping ? Verb.Find : Verb.OpenResult,
        (ShellKey.Enter, FollowRequestsScreen) => Verb.OpenAsker,
        (ShellKey.Enter, DirectMessagesScreen) => Verb.OpenConversation,
        (ShellKey.A, FollowRequestsScreen) => Verb.AcceptRequest,
        (ShellKey.D, NotificationsScreen) => Verb.Dismiss,
        (ShellKey.X, FollowRequestsScreen) => Verb.RejectRequest,
        (ShellKey.Enter, _) => Verb.OpenPost,
        (ShellKey.A, _) => Verb.OpenAuthor,
        (ShellKey.D, _) => Verb.Delete,
        (ShellKey.X, _) => Verb.Reveal,

        // The two movements that used to be one key (#51). k is the next post and j the one before it, which is the
        // other way round from vim (docs/tui-shell.md).
        (ShellKey.K, _) => Verb.NextPost,
        (ShellKey.J, _) => Verb.PreviousPost,
        (ShellKey.Home, _) => Verb.FirstPost,
        (ShellKey.End, _) => Verb.LastPost,
        (ShellKey.Down, _) => Verb.ScrollDown,
        (ShellKey.Up, _) => Verb.ScrollUp,
        (ShellKey.PageDown, _) => Verb.PageDown,
        (ShellKey.PageUp, _) => Verb.PageUp,

        // The third movement, and the one that goes inside a post rather than along it (#83).
        (ShellKey.Right, _) => Verb.NextReference,
        (ShellKey.Left, _) => Verb.PreviousReference,

        // The keys that mean one thing wherever they are pressed, so that a screen with nothing for them to act on
        // turns them down rather than meaning something else by them.
        (ShellKey.B, _) => Verb.Boost,
        (ShellKey.C, _) => Verb.Compose,
        (ShellKey.E, _) => Verb.Edit,
        (ShellKey.F, _) => Verb.Favorite,
        (ShellKey.G, _) => Verb.Refresh,
        (ShellKey.M, _) => Verb.MarkRead,
        (ShellKey.P, _) => Verb.Pin,
        (ShellKey.R, _) => Verb.Reply,
        (ShellKey.V, _) => Verb.Vote,

        // The capitals, which are keys of their own for exactly this reason: a lower-case mark key can never fire a
        // tie or empty an inbox by accident (docs/tui-shell.md).
        (ShellKey.CapitalF, _) => Verb.Follow,
        (ShellKey.CapitalM, _) => Verb.Mute,
        (ShellKey.CapitalB, _) => Verb.Block,
        (ShellKey.CapitalD, _) => Verb.ClearAll,

        // Every digit means the same thing; which answer it addresses is Answer's, below.
        (ShellKey.One or ShellKey.Two or ShellKey.Three or ShellKey.Four or ShellKey.Five, _) => Verb.Toggle,
        (ShellKey.Six or ShellKey.Seven or ShellKey.Eight or ShellKey.Nine or ShellKey.Zero, _) => Verb.Toggle,

        // ctrl-s and ctrl-w off a compose screen, and nothing else.
        _ => Verb.None,
    };

    /// <summary>
    ///     Which poll answer a digit addresses, counted from zero, or <see langword="null" /> where the key is not a
    ///     digit at all. <c>1</c>-<c>9</c> then <c>0</c>, so that ten answers are reachable along one row of keys and
    ///     the reader counts from where a person counts from (<c>docs/tui-shell.md</c>).
    /// </summary>
    /// <remarks>
    ///     Beside <see cref="Means" /> rather than carried on the verb, because it is the one thing about a key that no
    ///     screen changes: <c>3</c> is the third answer on every screen that has a poll and on every screen that has
    ///     none.
    /// </remarks>
    public static int? Answer(ShellKey key) => key switch
    {
        ShellKey.One => 0,
        ShellKey.Two => 1,
        ShellKey.Three => 2,
        ShellKey.Four => 3,
        ShellKey.Five => 4,
        ShellKey.Six => 5,
        ShellKey.Seven => 6,
        ShellKey.Eight => 7,
        ShellKey.Nine => 8,
        ShellKey.Zero => 9,
        _ => null,
    };
}
