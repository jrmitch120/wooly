using Wooly.Core.Timelines;

namespace Wooly.Tui.Shell;

/// <summary>Which of the rail's nine places this is.</summary>
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

    /// <summary>What is waiting for this profile. Opens onto a screen in #29.</summary>
    Notifications,

    /// <summary>The conversations this profile is in. Opens onto a screen in #30.</summary>
    Messages,

    /// <summary>The follows waiting to be answered. Opens onto a screen in #29.</summary>
    Requests,

    /// <summary>Finding accounts, hashtags and posts. Opens onto a screen in #29.</summary>
    Search,

    /// <summary>The profile's own account.</summary>
    Profile,
}

/// <summary>
///     One place the rail can send you (CONTEXT.md): what it is called, what it costs a fetch to arrive at, and how
///     many unread things are waiting there.
/// </summary>
/// <remarks>
///     Four of the nine open onto a timeline and one onto an account; the other four are listed here and open onto
///     their screens in #29 and #30. Listing them from the start is deliberate — the rail's shape is what this ticket
///     is settling, and a rail that grows four entries later is a different rail.
/// </remarks>
/// <param name="Kind">Which of the nine this is.</param>
/// <param name="Label">What it is called on the rail.</param>
/// <param name="Timeline">
///     The timeline arriving here reads, or <see langword="null" /> for a destination that reads something else or
///     nothing yet.
/// </param>
public sealed record Destination(DestinationKind Kind, string Label, Timeline? Timeline = null)
{
    /// <summary>How many unread things are waiting here, or zero where nothing is or nothing counts them.</summary>
    public int Unread { get; init; }
}
