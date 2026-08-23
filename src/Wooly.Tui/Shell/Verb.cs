namespace Wooly.Tui.Shell;

/// <summary>
///     What a key means once the screen it was pressed on is known: the other half of <see cref="Keymap" />'s table.
///     One of these is what a keypress becomes, and what a test asserts a binding by.
/// </summary>
/// <remarks>
///     Named for what a reader is asking for rather than for the method that carries it out, because several of them
///     are the same key on different screens and one — <see cref="Delete" />, <see cref="Vote" /> — is a question put
///     before anything is done.
///     <para>
///         What each becomes is <see cref="Shell.Do" />'s, but for ten of them, which need a terminal and are
///         <c>ShellWindow</c>'s: <see cref="Quit" />, which ends a run loop the application owns;
///         <see cref="ScrollDown" />, <see cref="ScrollUp" />, <see cref="PageDown" /> and <see cref="PageUp" />,
///         which walk the page rather than the list; <see cref="NextPost" />, <see cref="PreviousPost" />,
///         <see cref="FirstPost" /> and <see cref="LastPost" />, which move the pick and the page both; and
///         <see cref="Send" />, which has to take the editor widget's text before the shell sends it.
///     </para>
/// </remarks>
public enum Verb
{
    /// <summary>
    ///     Nothing here. The key is one this shell knows, and this screen has no use for it — <c>ctrl-s</c> off a
    ///     compose screen — so it is left to whatever else wants it.
    /// </summary>
    None,

    /// <summary><c>ctrl-q</c>: ends the run.</summary>
    Quit,

    /// <summary><c>esc</c>: up one level, of whichever kind of level is open.</summary>
    Back,

    /// <summary><c>?</c>: this screen's keymap, which is itself a screen.</summary>
    Help,

    /// <summary><c>/</c>: the search destination, or a fresh prompt where that is already what is showing.</summary>
    Search,

    /// <summary><c>tab</c>: the next destination down the rail.</summary>
    NextDestination,

    /// <summary><c>shift-tab</c>: the one above it.</summary>
    PreviousDestination,

    /// <summary><c>k</c>: the next post, with the screen following it.</summary>
    NextPost,

    /// <summary><c>j</c>: the one before it.</summary>
    PreviousPost,

    /// <summary><c>Home</c>: the first thing on the screen.</summary>
    FirstPost,

    /// <summary><c>End</c>: the last.</summary>
    LastPost,

    /// <summary><c>↓</c>: the screen moves a few rows and the pick stays where it was put.</summary>
    ScrollDown,

    /// <summary><c>↑</c>: the same, upwards.</summary>
    ScrollUp,

    /// <summary><c>PgDn</c>: the same, a screenful at a time.</summary>
    PageDown,

    /// <summary><c>PgUp</c>: the same, upwards.</summary>
    PageUp,

    /// <summary><c>⏎</c>: read the picked post, with what has been said in answer to it.</summary>
    OpenPost,

    /// <summary><c>a</c>: the account of whoever wrote the picked post.</summary>
    OpenAuthor,

    /// <summary><c>c</c>: a fresh post.</summary>
    Compose,

    /// <summary><c>r</c>: an answer to the picked post.</summary>
    Reply,

    /// <summary><c>e</c>: a change to one of the profile's own.</summary>
    Edit,

    /// <summary><c>b</c>: boost the picked post, or take the boost off.</summary>
    Boost,

    /// <summary><c>f</c>: favorite it, or take that off.</summary>
    Favorite,

    /// <summary><c>p</c>: pin it, or unpin it. Own posts only.</summary>
    Pin,

    /// <summary><c>d</c>: ask before taking the picked post down (story 43).</summary>
    Delete,

    /// <summary><c>x</c>: show what the picked post is hiding.</summary>
    Reveal,

    /// <summary><c>→</c>: the next reference inside the picked post, entering at the first.</summary>
    NextReference,

    /// <summary><c>←</c>: the one before it, entering at the last.</summary>
    PreviousReference,

    /// <summary><c>⏎</c> while a reference is picked: open whatever it points at.</summary>
    OpenReference,

    /// <summary>
    ///     <c>1</c>-<c>9</c> and <c>0</c>: toggle one of the picked post's poll answers. Which one is
    ///     <see cref="Keymap.Answer" />'s, being a fact about the key and not about the screen.
    /// </summary>
    Toggle,

    /// <summary><c>v</c>: ask before casting what those toggled (story 43).</summary>
    Vote,

    /// <summary><c>g</c>: ask this screen for what is there now.</summary>
    Refresh,

    /// <summary><c>F</c>: follow the account being shown, or unfollow it.</summary>
    Follow,

    /// <summary><c>M</c>: mute it, or unmute it.</summary>
    Mute,

    /// <summary><c>B</c>: block it, or unblock it.</summary>
    Block,

    /// <summary><c>d</c> on the notifications screen: dismiss the picked notification by its own id.</summary>
    Dismiss,

    /// <summary><c>D</c>: ask before emptying the inbox.</summary>
    ClearAll,

    /// <summary><c>a</c> on the follow requests screen: let the picked asker in.</summary>
    AcceptRequest,

    /// <summary><c>x</c> there: turn them away.</summary>
    RejectRequest,

    /// <summary><c>⏎</c> there: open whoever is asking, so the question can be answered knowing who asked.</summary>
    OpenAsker,

    /// <summary><c>⏎</c> on the conversations list: read the picked conversation.</summary>
    OpenConversation,

    /// <summary><c>m</c>: take the unread mark off the conversation being read, or the one picked out.</summary>
    MarkRead,

    /// <summary><c>⏎</c> on a search prompt taking a query: put it to the instance.</summary>
    Find,

    /// <summary><c>⏎</c> on what it found: open the picked result.</summary>
    OpenResult,

    /// <summary>
    ///     <c>ctrl-s</c> on a compose screen: send it, or save it. The editor widget is where the text was typed and
    ///     the screen is where it lives, and this is the one moment the two have to agree.
    /// </summary>
    Send,

    /// <summary><c>ctrl-w</c> there: move the typing between the post and the warning over it.</summary>
    WriteWarning,
}
