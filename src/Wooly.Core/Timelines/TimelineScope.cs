namespace Wooly.Core.Timelines;

/// <summary>The six timelines a profile can read, which differ only in what an instance is asked for.</summary>
public enum TimelineScope
{
    /// <summary>The posts of the accounts this profile follows.</summary>
    Home,

    /// <summary>The public posts of accounts on this profile's own instance.</summary>
    Local,

    /// <summary>The public posts reaching this instance from everywhere it federates with.</summary>
    Federated,

    /// <summary>The public posts carrying one hashtag.</summary>
    Tag,

    /// <summary>
    ///     The posts of one account, which is what an account screen shows underneath who they are. A timeline rather
    ///     than a reading of its own: it is posts, newest first, paged the way the other four are, and the one thing
    ///     that differs is which endpoint is asked.
    /// </summary>
    Account,

    /// <summary>
    ///     The posts one account has fastened to the top of their own profile. The account's posts again, asked for
    ///     with both of <see cref="Account" />'s filters flipped: a pin is often a reply and always a pin, so leaving
    ///     replies out — which is right for a timeline — is what makes a pinned reply unreachable. Complete in one
    ///     page, and in the order the account chose rather than by date.
    /// </summary>
    Pinned,
}
