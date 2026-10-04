namespace Wooly.Core.Posts;

/// <summary>
///     How long an instance lets a post be, and what it counts an address in one as (#319). Each instance sets its own;
///     <see cref="Default" /> is what Mastodon ships with, and what is assumed until an instance says otherwise.
/// </summary>
/// <param name="Characters">The most a post may be, warning and all, as <see cref="PostLength" /> counts it.</param>
/// <param name="PerAddress">What any address counts as, however long it is.</param>
public sealed record PostLimits(int Characters, int PerAddress)
{
    /// <summary>Mastodon's own: 500 characters, an address counting as 23.</summary>
    public static readonly PostLimits Default = new(500, 23);
}
