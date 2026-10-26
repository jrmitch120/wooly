using Wooly.Core.Posts;

namespace Wooly.Tui.Screens;

/// <summary>
///     What the screen says of one side of a post's <b>Actors</b> (#489): the crumb over the list, and what an empty
///     list says. The crumb words are the ones a favorite or a boost <see cref="KindLook" /> gives its group's actors,
///     and come from here for both, so that the two actors screens cannot come to call the same people two things.
/// </summary>
public static class ActorSideWords
{
    /// <summary>
    ///     What the people on <paramref name="side" /> are called on the screen listing them: "Favorited by".
    /// </summary>
    public static string Crumb(this ActorSide side) => side.Either("Favorited by", "Boosted by");

    /// <summary>What a side that read back with nobody on it says — the count on the post having been stale.</summary>
    public static string Nobody(this ActorSide side) =>
        side.Either("Nobody has favorited this.", "Nobody has boosted this.");
}
