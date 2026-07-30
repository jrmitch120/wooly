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
}
