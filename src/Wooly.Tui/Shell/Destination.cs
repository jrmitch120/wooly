using Wooly.Core.Timelines;

namespace Wooly.Tui.Shell;

/// <summary>
///     Which of the rail's places this is: one of the nine everybody has, or one of the reader's tag sets.
/// </summary>
public enum DestinationKind
{
    /// <summary>The posts of the accounts this profile follows.</summary>
    Home,

    /// <summary>The public posts of accounts on this profile's own instance.</summary>
    Local,

    /// <summary>The public posts reaching this instance from everywhere it federates with.</summary>
    Federated,

    /// <summary>
    ///     The public posts carrying any of one of the reader's <b>Tag set</b>s' tags (ADR-0033): one destination per
    ///     set, so the one kind the rail can hold any number of, or none.
    /// </summary>
    TagSet,

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
///     Which of the five rail groups a destination is drawn in, by what it is for (GLOSSARY.md, ADR-0021, ADR-0033).
///     The rail's order is the groups' order, which is this one.
/// </summary>
public enum RailGroup
{
    /// <summary>What you read: Home, Local and Federated.</summary>
    Timelines,

    /// <summary>
    ///     The reader's tag sets, in the order they wrote them, never carrying an unread count — and not drawn at all
    ///     where there are none, a group being the run of its destinations.
    /// </summary>
    Hashtags,

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
///     One place the rail can send you (GLOSSARY.md): what it is called, what it costs a fetch to arrive at, and how
///     many unread things are waiting there.
/// </summary>
/// <remarks>
///     Three of the nine everybody has open onto a timeline, one onto an account, and the other five onto a list of
///     their own; every tag set opens onto a timeline too. The fixed ones were listed here from the start, before four
///     of them had a screen — deliberately, because the rail's shape is what #28 settled, and a rail that grows four
///     entries later is a different rail. The reader's tag sets are the rule broken on purpose (ADR-0033): what is on
///     the rail is partly theirs to write, and changes only when the config file does.
///     <para>
///         <see cref="DestinationKind.Discover" /> is the one entry the rail has ever grown by, and it was argued for
///         on those terms rather than added to the list (ADR-0014's amendment, ADR-0019): what earned it is that the
///         screen behind it is built in sections, so a second kind of suggestion later is a heading on it rather than
///         an eleventh entry here (#171).
///     </para>
/// </remarks>
/// <param name="Kind">Which of the rail's kinds this is.</param>
/// <param name="Label">What it is called on the rail.</param>
/// <param name="Timeline">
///     The timeline arriving here reads, or <see langword="null" /> for a destination that reads something else or
///     nothing yet.
/// </param>
public sealed record Destination(DestinationKind Kind, string Label, Timeline? Timeline = null)
{
    /// <summary>How many unread things are waiting here, or zero where nothing is or nothing counts them.</summary>
    public int Unread { get; init; }

    /// <summary>
    ///     Whether the read that counted <see cref="Unread" /> came back full, so that at least that many are waiting
    ///     and perhaps more — drawn <c>40+</c> rather than as an exact number that may be wrong (#396).
    /// </summary>
    public bool Full { get; init; }

    /// <summary>
    ///     Whether <paramref name="other" /> is the same place: the same kind, called the same, reading the same
    ///     timeline. What its badge says is not part of it — a count moving changes the entry and not which destination
    ///     it is — so the rail's cursor, the cache and the reads in flight are keyed on the destination itself, and two
    ///     of one kind stand on the rail apart (#510).
    /// </summary>
    public bool Equals(Destination? other) =>
        other is not null && other.Kind == Kind && other.Label == Label && Equals(other.Timeline, Timeline);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Kind, Label, Timeline);

    /// <summary>The rail group this is drawn in, which follows from what it is and nothing else.</summary>
    public RailGroup Group => Kind switch
    {
        DestinationKind.Home or DestinationKind.Local or DestinationKind.Federated => RailGroup.Timelines,
        DestinationKind.TagSet => RailGroup.Hashtags,
        DestinationKind.Discover or DestinationKind.Search => RailGroup.Explore,
        DestinationKind.Notifications or DestinationKind.Messages or DestinationKind.Requests => RailGroup.Inbox,
        DestinationKind.Profile => RailGroup.You,
        _ => throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "A destination in no rail group."),
    };
}
