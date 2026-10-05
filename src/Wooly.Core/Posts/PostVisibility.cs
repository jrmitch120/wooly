namespace Wooly.Core.Posts;

/// <summary>Who can see a post, in Mastodon's four settings, from the widest audience to the narrowest.</summary>
public enum PostVisibility
{
    /// <summary>Visible to anyone, and listed on public timelines.</summary>
    Public,

    /// <summary>Visible to anyone who has the link, but kept off public timelines.</summary>
    Unlisted,

    /// <summary>Visible only to the author's followers. Mastodon calls it <c>private</c> on the wire (ADR-0024).</summary>
    Followers,

    /// <summary>Visible only to the accounts mentioned in it — what this client's <c>dm</c> commands send.</summary>
    Direct,
}
