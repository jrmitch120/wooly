using Wooly.Core.Notifications;
using Wooly.Core.Posts;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     How this client draws one <see cref="NotificationKind" /> beside a notification group (#453, #455): the icon that
///     carries the kind where there is no colour, as a post's marks carry theirs (ADR-0014), the role it is drawn in,
///     and what the actors screen's crumb calls the people in a group of it. One row per kind, so that a kind given
///     its own look is given it once rather than in three places.
/// </summary>
/// <remarks>
///     A follow and a kind this client has no word for take <see cref="Role.BylineHandle" />, the role of an account
///     named — a follow is somebody arriving, and a kind with no word of its own has no mark's colour to borrow. Such a
///     kind is marked <c>•</c> and its actors are called by the instance's own word for it (ADR-0010).
/// </remarks>
/// <param name="Icon">The mark that says the kind without colour.</param>
/// <param name="Role">The kind's colour.</param>
/// <param name="Crumb">
///     What a group's actors are called on the screen listing them: "Favorited by" — for a favorite and a boost, the
///     words a post's own actors are called by (<see cref="ActorSideWords" />).
/// </param>
public sealed record KindLook(string Icon, Role Role, string Crumb)
{
    private static readonly Dictionary<NotificationKind, KindLook> Known = new()
    {
        [NotificationKind.Favorite] = new("★", Role.Favorite, ActorSide.Favorited.Crumb()),
        [NotificationKind.Boost] = new("↺", Role.Boost, ActorSide.Boosted.Crumb()),
        [NotificationKind.Mention] = new("@", Role.Mention, "Mentioned you"),
        [NotificationKind.Follow] = new("+", Role.BylineHandle, "Followed you"),
    };

    /// <summary>How <paramref name="kind" /> is drawn.</summary>
    public static KindLook Of(NotificationKind kind) =>
        Known.GetValueOrDefault(kind) ?? new KindLook("•", Role.BylineHandle, kind.Name);
}
