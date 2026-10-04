using Wooly.Core.Profiles;

namespace Wooly.Core.Posts;

/// <summary>
///     Asks an instance how long it lets a post be (#319). A port of its own, ADR-0005's narrow kind: the compose
///     screen's count is the only thing that wants it. Beside <see cref="PostLimits" /> and <see cref="PostLength" />,
///     which are what it is asked for and what it is asked for to do.
/// </summary>
public interface IInstanceLimits
{
    /// <summary>The limits the instance <paramref name="profile" /> signs in to sets.</summary>
    /// <returns>
    ///     What the instance said, with <see cref="PostLimits.Default" />'s value standing in for anything it left
    ///     unsaid — an instance that answers without a limit is one running a version from before it had a setting.
    ///     <see langword="null" /> where it did not answer at all: rate-limited, unreachable, too slow, refusing, or
    ///     answering with something that is not Mastodon's. Nothing the limit is wanted for is worth a failure, so
    ///     those are silence here, as <see cref="Relationships.IAccountRelationships.Standing" />'s are — and silence
    ///     rather than the default, so a caller can tell a guess from an answer and ask again later.
    /// </returns>
    /// <exception cref="Errors.AuthenticationException">
    ///     The token was refused, which is left to be said by whatever can sign the profile in again.
    /// </exception>
    Task<PostLimits?> Read(ActiveProfile profile, CancellationToken cancellationToken);
}
