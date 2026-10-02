using Wooly.Core.Timelines;

namespace Wooly.Tui.Shell;

/// <summary>Which of the rail's ten places this is.</summary>
public enum DestinationKind
{
    /// <summary>The posts of the accounts this profile follows.</summary>
    Home,

    /// <summary>The public posts of accounts on this profile's own instance.</summary>
    Local,

    /// <summary>The public posts reaching this instance from everywhere it federates with.</summary>
    Federated,

    /// <summary>The public posts carrying the tag the reader keeps a place for.</summary>
    Hashtag,

    /// <summary>Who the instance offers this profile to follow, in sections by why it is offering them.</summary>
    Discover,

    /// <summary>Finding accounts, hashtags and posts.</summary>
    Search,

    /// <summary>What is waiting for this profile.</summary>
    Notifications,

    /// <summary>The conversations this profile is in, each of which opens onto its own thread.</summary>
    Messages,

    /// <summary>The follows waiting to be answered.</summary>
    Requests,

    /// <summary>The profile's own account.</summary>
    Profile,
}

/// <summary>
///     Which of the four rail groups a destination is drawn in, by what it is for (CONTEXT.md, ADR-0021). The rail's
///     order is the groups' order, which is this one.
/// </summary>
public enum RailGroup
{
    /// <summary>What you read: Home, Local, Federated and Hashtag.</summary>
    Timelines,

    /// <summary>What you go looking with: Discover and Search, neither of which ever carries an unread count.</summary>
    Explore,

    /// <summary>
    ///     What comes to you: Notifications, Direct messages and Follow requests, where every unread count lives.
    /// </summary>
    Inbox,

    /// <summary>The profile's own account.</summary>
    You,
}

/// <summary>
///     One place the rail can send you (CONTEXT.md): what it is called, what it costs a fetch to arrive at, and how
///     many unread things are waiting there.
/// </summary>
/// <remarks>
///     Four of the ten open onto a timeline, one onto an account, and the other five onto a list of their own. Nine of
///     them were listed here from the start, before four of them had a screen — deliberately, because the rail's shape
///     is what #28 settled, and a rail that grows four entries later is a different rail.
///     <para>
///         <see cref="DestinationKind.Discover" /> is the one entry the rail has ever grown by, and it was argued for
///         on those terms rather than added to the list (ADR-0014's amendment, ADR-0019): what earned it is that the
///         screen behind it is built in sections, so a second kind of suggestion later is a heading on it rather than
///         an eleventh entry here (#171).
///     </para>
/// </remarks>
/// <param name="Kind">Which of the ten this is.</param>
/// <param name="Label">What it is called on the rail.</param>
/// <param name="Timeline">
///     The timeline arriving here reads, or <see langword="null" /> for a destination that reads something else or
///     nothing yet.
/// </param>
public sealed record Destination(DestinationKind Kind, string Label, Timeline? Timeline = null)
{
    /// <summary>How many unread things are waiting here, or zero where nothing is or nothing counts them.</summary>
    public int Unread { get; init; }

    /// <summary>The rail group this is drawn in, which follows from what it is and nothing else.</summary>
    public RailGroup Group => Kind switch
    {
        DestinationKind.Home or DestinationKind.Local or DestinationKind.Federated or DestinationKind.Hashtag =>
            RailGroup.Timelines,
        DestinationKind.Discover or DestinationKind.Search => RailGroup.Explore,
        DestinationKind.Notifications or DestinationKind.Messages or DestinationKind.Requests => RailGroup.Inbox,
        DestinationKind.Profile => RailGroup.You,
        _ => throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "A destination in no rail group."),
    };
}
