namespace Wooly.Tui.Screens;

/// <summary>
///     What a list of people has already said about everyone on it, and so what a row on it must not say again — the
///     rule a follow list turns on: your own following list is people you follow, and saying so on all 187 rows says
///     nothing at all (#180).
/// </summary>
/// <remarks>
///     A fact about the list rather than about the person, which is why it is passed to the row rather than read off
///     it: the same account draws differently on your own following list, on your own followers list and on somebody
///     else's, and only the list knows which of the three it is.
/// </remarks>
public enum Implied
{
    /// <summary>
    ///     Nothing — somebody else's list, where a reader stands in no particular relation to anyone on it, so the
    ///     whole compact standing is worth drawing.
    /// </summary>
    Nothing,

    /// <summary>Your own following list: every row is somebody you follow, so the words are dropped.</summary>
    YouFollowThem,

    /// <summary>
    ///     Your own followers list: every row is somebody who follows you. What is left is whether you follow them
    ///     back, and the rows that say nothing are the answer that list is read for.
    /// </summary>
    TheyFollowYou,
}
