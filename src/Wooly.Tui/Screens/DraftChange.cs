namespace Wooly.Tui.Screens;

/// <summary>
///     What a change to a <see cref="IDraft" /> made — a compose screen, or the edit public profile form — which is all
///     drafting needs to settle what follows from it (#364): whether a notice over the draft is spent, and whether the
///     screen is drawn again.
/// </summary>
public enum DraftChange
{
    /// <summary>Nothing: the change found the draft already as it would have left it.</summary>
    None,

    /// <summary>
    ///     The typing or the choosing moved, or a list opened or closed — focus, walking the fields, choosing on To —
    ///     with what is written as it was.
    /// </summary>
    Moved,

    /// <summary>What is written changed: a post's text, its warning or its language, or a field of the form.</summary>
    Edited,
}
