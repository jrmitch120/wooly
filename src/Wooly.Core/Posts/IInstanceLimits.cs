using Wooly.Core.Profiles;

namespace Wooly.Core.Posts;

/// <summary>
///     Asks an instance how long it lets a post be (#319). A port of its own, ADR-0005's narrow kind: the compose
///     screen's count is the only thing that wants it, and it is a question about the instance rather than about any
///     post.
/// </summary>
public interface IInstanceLimits
{
    /// <summary>The limits the instance <paramref name="profile" /> signs in to sets.</summary>
    /// <returns>
    ///     What the instance said, with <see cref="PostLimits.Default" />'s value standing in for anything it left
    ///     unsaid — an instance that answers without a limit is one running a version from before it had a setting.
    /// </returns>
    Task<PostLimits> Read(ActiveProfile profile, CancellationToken cancellationToken);
}
