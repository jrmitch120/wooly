using Wooly.Core.Profiles;

namespace Wooly.Core.Posts;

/// <summary>
///     Asks an instance which custom emoji it offers its people to write (#528, #578), for the <c>:</c> list a post and
///     a bio hang under a shortcode being typed. A port of its own, ADR-0005's narrow kind, shaped like
///     <see cref="IInstanceLimits" />: the list is the only thing that wants it.
/// </summary>
public interface IInstanceEmoji
{
    /// <summary>The custom emoji the instance <paramref name="profile" /> signs in to offers, each at its still.</summary>
    /// <returns>
    ///     What the instance offers, leaving out any it hides from its picker, and any with no shortcode or no picture,
    ///     which could be neither written nor drawn. An instance with none answers an empty list.
    ///     <see langword="null" /> where it did not answer at all: rate-limited, unreachable, too slow, refusing, or
    ///     answering with something that is not Mastodon's. Nothing the list is wanted for is worth a failure, so those
    ///     are silence, as <see cref="IInstanceLimits.Read" />'s are — and silence rather than an empty list, so a
    ///     caller can tell an instance with no emoji from one that did not say, and ask again later.
    /// </returns>
    /// <exception cref="Errors.AuthenticationException">
    ///     The token was refused, which is left to be said by whatever can sign the profile in again.
    /// </exception>
    Task<IReadOnlyList<CustomEmoji>?> Read(ActiveProfile profile, CancellationToken cancellationToken);
}
