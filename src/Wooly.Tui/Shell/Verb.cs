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
///         What each becomes is <see cref="Shell.Do" />'s, but for fourteen of them, which need a terminal and are
///         <c>ShellWindow</c>'s: <see cref="Quit" />, which ends a run loop the application owns;
///         <see cref="ScrollDown" />, <see cref="ScrollUp" />, <see cref="PageDown" /> and <see cref="PageUp" />,
///         which walk the page rather than the list; <see cref="NextPost" />, <see cref="PreviousPost" />,
///         <see cref="FirstPost" />, <see cref="LastPost" />, <see cref="NextSection" />,
///         <see cref="PreviousSection" />, <see cref="NextEntry" /> and <see cref="PreviousEntry" />, which move the pick
///         and the page both; and <see cref="Send" />, which has to take the editor widget's text before the shell sends
///         it. Which fourteen is <see cref="Verbs.NeedsATerminal" />, the one list the window and the shell both read.
///     </para>
/// </remarks>
public enum Verb
{
    /// <summary>
    ///     Nothing here. The key is one this shell knows, and this screen has no use for it — <c>ctrl-s</c> off a
    ///     compose screen — so it is left to whatever else wants it.
    /// </summary>
    None,

    /// <summary>
    ///     <c>w</c>: everyone the account being shown follows. One key rather than two, the account screen being
    ///     crowded enough that <c>f</c> and <c>b</c> are already spoken for — the other side is one <c>s</c> away
    ///     (#180).
    /// </summary>
    OpenFollows,

    /// <summary>
    ///     <c>s</c>: the other side of the same account's follows, in place. A toggle that pushed would grow the
    ///     stack on every flip.
    /// </summary>
    SwapSide,

    /// <summary>
    ///     <c>s</c> on an account screen: their posts and replies, or back to their posts alone, in place. A sibling
    ///     of <see cref="SwapSide" /> rather than the same verb, because a timeline has no sides — one verb meaning
    ///     both would be a name true of neither (#229).
    /// </summary>
    SwapPostsAndReplies,

    /// <summary><c>f</c> on a follow list: opens the prompt that narrows it.</summary>
    Filter,

    /// <summary>
    ///     <c>⏎</c> while that prompt is taking letters: back to walking, with what was typed still narrowing the
    ///     list. Distinct from <see cref="Find" />, which asks an instance — a filter has already read everything it
    ///     acts on.
    /// </summary>
    FilterDone,

    /// <summary>
    ///     <c>⏎</c> on a list of people — a follow list, or Discover: the account screen of whoever is picked out,
    ///     the same screen <c>a</c> opens from a feed rather than a row that expands where it stands.
    /// </summary>
    OpenPerson,

    /// <summary><c>ctrl-q</c>: ends the run.</summary>
    Quit,

    /// <summary><c>esc</c>: up one level, of whichever kind of level is open.</summary>
    Back,

    /// <summary><c>?</c>: this screen's keymap, which is itself a screen.</summary>
    Help,

    /// <summary><c>/</c>: the search destination, or a fresh prompt where that is already what is showing.</summary>
    Search,

    /// <summary>
    ///     <c>ctrl-p</c>: every profile on this machine, and which one this session is acting as (ADR-0020). Nothing on
    ///     compose, where a switch would drop the draft.
    /// </summary>
    Profiles,

    /// <summary><c>a</c> on the profiles screen: add a profile, from the instance up (#245).</summary>
    AddProfile,

    /// <summary>
    ///     <c>⏎</c> on the profiles screen: act as the picked profile for the rest of the session, starting again on
    ///     Home (#243). Nothing on the profile already acted as.
    /// </summary>
    ActAs,

    /// <summary>
    ///     <c>D</c> on the profiles screen: make the picked profile the default, which the CLI and the next launch act as
    ///     — without changing who this session is acting as (#244). Nothing on the profile already the default.
    /// </summary>
    MakeDefault,

    /// <summary>
    ///     <c>x</c> on the profiles screen: remove the picked profile, its config entry and its token, once the reader has
    ///     said so twice (#246). Refused on the profile this session is acting as.
    /// </summary>
    RemoveProfile,

    /// <summary>
    ///     <c>R</c> on the profiles screen: sign the picked profile in again, its instance and name fixed and its token
    ///     replaced without asking (#248).
    /// </summary>
    SignInAgain,

    /// <summary>
    ///     <c>⏎</c> on the add screen: on to the next step with what the one in front has — the instance to sign in
    ///     at, the token to check, the name to save under — or the browser tried again where it did not come back.
    /// </summary>
    Continue,

    /// <summary>
    ///     <c>t</c> on the add screen, while the browser is out or has failed: give it up and paste a token instead,
    ///     ADR-0004's fallback.
    /// </summary>
    PasteToken,

    /// <summary><c>tab</c>: the next destination down the rail.</summary>
    NextDestination,

    /// <summary><c>shift-tab</c>: the one above it.</summary>
    PreviousDestination,

    /// <summary><c>`</c>: the first destination of the next rail group down, wrapping (ADR-0021).</summary>
    NextGroup,

    /// <summary><c>~</c>: the first destination of the rail group above, wrapping.</summary>
    PreviousGroup,

    /// <summary><c>k</c>: the next post, with the screen following it.</summary>
    NextPost,

    /// <summary><c>j</c>: the one before it.</summary>
    PreviousPost,

    /// <summary><c>Home</c>: the first thing on the screen.</summary>
    FirstPost,

    /// <summary><c>End</c>: the last.</summary>
    LastPost,

    /// <summary>
    ///     <c>]</c>: the first thing of the next headed run — the posts a search found, from the accounts it found
    ///     (#166). The pick moves, and the run's heading comes onto the page with it.
    /// </summary>
    NextSection,

    /// <summary><c>[</c>: the first thing of the run before it.</summary>
    PreviousSection,

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

    /// <summary>
    ///     <c>d</c> on Discover: tell the instance to stop suggesting whoever is picked out. One-way, since there is
    ///     no un-dismiss endpoint — so a second press is a no-op rather than an undo — and unconfirmed, nothing of the
    ///     reader's being destroyed by it (#181).
    /// </summary>
    StopSuggesting,

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
    ///     <c>ctrl-s</c> on a compose screen: send it, or save it — whatever the screen holds, which follows the
    ///     editor widget on every edit (#315).
    /// </summary>
    Send,

    /// <summary><c>ctrl-w</c> there: move the typing between the post and the warning over it.</summary>
    WriteWarning,

    /// <summary>
    ///     <c>↑</c> there, where the field the typing is in leaves it: move the typing to the field above (ADR-0024, #337).
    /// </summary>
    PreviousField,

    /// <summary><c>↓</c> there, likewise: move the typing to the field below, the post being the last.</summary>
    NextField,

    /// <summary>
    ///     <c>←</c> there, where the field the typing is in leaves it — which only To does: choose the next visibility
    ///     to the left that To allows (ADR-0024, #338).
    /// </summary>
    PreviousChoice,

    /// <summary><c>→</c> there, likewise: choose the next one to the right.</summary>
    NextChoice,

    /// <summary>
    ///     <c>del</c> or <c>backspace</c> on a compose, where no field takes them — which is on a row under the Media
    ///     header: take that attachment off the post (#378).
    /// </summary>
    RemoveAttachment,

    /// <summary><c>ctrl-z</c> on the Media header or its rows: bring back the attachment last taken off (#378).</summary>
    BringBackAttachment,

    /// <summary><c>shift-↑</c> on a row under the Media header: that attachment a place earlier (#378).</summary>
    EarlierAttachment,

    /// <summary><c>shift-↓</c> there, likewise: one place later.</summary>
    LaterAttachment,

    /// <summary><c>r</c> on a refused row a retry could mend: send that attachment up again (#378).</summary>
    RetryAttachment,

    /// <summary>
    ///     <c>ctrl-o</c> on a compose or a reply, or <c>⏎</c> on its Media header: push the file browser over it (#376).
    /// </summary>
    OpenBrowser,

    /// <summary>
    ///     <c>ctrl-v</c> or <c>alt-v</c> on a compose or a reply, or on the attachments screen over one: attach a picture
    ///     or copied files from this machine's clipboard (#380). Unused where the clipboard holds neither, which leaves
    ///     the press to the field's own paste.
    /// </summary>
    PasteFromTheClipboard,

    /// <summary>
    ///     <c>⏎</c> in the file browser: open the folder under the cursor, or attach what is chosen — or, with nothing
    ///     chosen, the file under the cursor — to the compose screen under it (#376).
    /// </summary>
    AttachChosen,

    /// <summary>
    ///     <c>space</c> in the file browser, a click on a box there, or a ctrl- or shift-click on a row: choose the file
    ///     under the cursor, or let it go (#376).
    /// </summary>
    Choose,

    /// <summary>
    ///     <c>↓</c> on a list of entries that are not posts — the file browser's, whose letters are its filter (#376), and
    ///     the attachments screen's rows (story 58): the cursor to the next, as <c>k</c> walks to the next post elsewhere.
    /// </summary>
    NextEntry,

    /// <summary><c>↑</c> there, likewise: the cursor to the entry before.</summary>
    PreviousEntry,

    /// <summary><c>→</c> there: open the folder under the cursor.</summary>
    IntoFolder,

    /// <summary><c>←</c> there: go up a folder.</summary>
    UpFolder,

    /// <summary><c>ctrl-a</c> there: show every file, or only the types the instance accepts again.</summary>
    EveryFile,

    /// <summary>
    ///     <c>s</c> on compose's Media header: put what is attached behind a click, or take it back out — unless a
    ///     warning holds it there (#379).
    /// </summary>
    ToggleSensitive,

    /// <summary>
    ///     <c>⏎</c> there on a pending attachment's row: open the description editor on it (#377).
    /// </summary>
    Describe,

    /// <summary>
    ///     <c>⏎</c> on compose's Media header, or a click on its line, where a terminal too short for a row each folded
    ///     the rows into it: push the attachments screen listing them (story 58).
    /// </summary>
    ListAttachments,
}
