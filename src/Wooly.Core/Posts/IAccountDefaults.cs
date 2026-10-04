using Wooly.Core.Profiles;

namespace Wooly.Core.Posts;

/// <summary>
///     Asks an instance what the account a profile signs in as posts at by default (#339, ADR-0024). A port of its
///     own, ADR-0005's narrow kind, apart from <see cref="Relationships.IAccountRelationships" />: what a compose screen
///     starts on is the only thing that wants it. Beside <see cref="PostDefaults" />, which is what it answers.
/// </summary>
public interface IAccountDefaults
{
    /// <summary>The defaults of the account <paramref name="profile" /> signs in as.</summary>
    /// <returns>
    ///     What the account's settings say, with anything unsaid or unrecognised unknown — and
    ///     <see cref="PostDefaults.Unknown" /> where the instance did not answer at all: rate-limited, unreachable, too
    ///     slow, refusing, or answering with something that is not Mastodon's. Nothing the defaults are wanted for is
    ///     worth a failure, since a compose screen without them sends what it always did.
    /// </returns>
    /// <exception cref="Errors.AuthenticationException">
    ///     The token was refused, which is left to be said by whatever can sign the profile in again.
    /// </exception>
    Task<PostDefaults> Read(ActiveProfile profile, CancellationToken cancellationToken);
}
