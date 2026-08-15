namespace Wooly.Tui.Rendering;

/// <summary>
///     What this reader has done to one post: asked past its content warning, walked to a reference inside its text.
///     Everything a post is drawn differently for because of who is reading it, rather than because of what it says.
/// </summary>
/// <remarks>
///     One thing rather than an argument apiece, because each of them is keyed by the same question — which post is
///     this — and each of them arrived a ticket at a time, paying the same edit in seven drawing sites and forty-odd
///     tests (#95). What comes next is a field here, filled where <see cref="Screens.Screen.ReadingOf" /> already
///     puts the others together.
///     <para>
///         Everything defaults, so the common case is <see langword="default" />: most of a feed is posts nobody has
///         touched, and saying so twice over at each of them was the noise this replaces.
///     </para>
/// </remarks>
/// <param name="Revealed">Whether the reader has asked to see past this post's content warning.</param>
/// <param name="Reference">
///     The reference in this post's text the reader has walked to, or <see langword="null" /> where none is picked —
///     which is every post but the one being read (#83).
/// </param>
/// <param name="Chosen">
///     Which of this post's poll options the reader has toggled and not yet cast, as indices into the poll's own
///     answers (#87). What turns the poll's bars into a ballot: an option is drawn <c>[x]</c> or <c>[ ]</c> while there
///     is a toggle standing, and back to <c>✓ </c> the moment it is cast or let go. Empty and absent say the same
///     thing — nobody is voting in this poll — which is what every post but the one being read is, and what that one
///     is until a digit is pressed.
/// </param>
public readonly record struct Reading(
    bool Revealed = false,
    Reference? Reference = null,
    IReadOnlySet<int>? Chosen = null);
