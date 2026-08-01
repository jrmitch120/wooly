namespace Wooly.Core.Timelines;

/// <summary>The four timelines a profile can read, which differ only in what an instance is asked for.</summary>
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
}
