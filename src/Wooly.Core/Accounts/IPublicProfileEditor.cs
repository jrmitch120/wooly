using Wooly.Core.Profiles;

namespace Wooly.Core.Accounts;

/// <summary>
///     Reads and saves the public profile (GLOSSARY.md) of the account a profile signs in as — the only account whose
///     public profile this client can change. A port of its own, ADR-0005's narrow kind: both front ends edit through
///     it, and their tests fake it rather than the network. ADR-0028 says why it reads as written and saves as changed.
/// </summary>
public interface IPublicProfileEditor
{
    /// <summary>The public profile as the account wrote it, read fresh.</summary>
    /// <exception cref="Errors.PublicProfileRefusedException">The instance would not say, and why where it said.</exception>
    /// <exception cref="Errors.InstanceFailedException">The instance failed to answer.</exception>
    /// <exception cref="Errors.AuthenticationException">The token was refused.</exception>
    Task<PublicProfile> Read(ActiveProfile profile, CancellationToken cancellationToken);

    /// <summary>
    ///     Sends <paramref name="change" /> and nothing else: what it leaves <see langword="null" /> is not sent. A
    ///     caller holding a whole edited public profile narrows it first, with <see cref="PublicProfileChange.Against" />.
    /// </summary>
    /// <returns>The account as the instance now has it.</returns>
    /// <exception cref="Errors.PictureNotFoundException">A picture named has no file at it. Nothing was sent.</exception>
    /// <exception cref="Errors.PublicProfileRefusedException">
    ///     The instance turned the change down — longer than it allows, most often — in its own words where it gave any.
    /// </exception>
    /// <exception cref="Errors.InstanceFailedException">The instance failed to answer.</exception>
    /// <exception cref="Errors.AuthenticationException">The token was refused.</exception>
    Task<Account> Save(ActiveProfile profile, PublicProfileChange change, CancellationToken cancellationToken);
}
