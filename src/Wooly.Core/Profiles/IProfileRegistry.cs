using Wooly.Core.Configuration;
using Wooly.Core.Credentials;

namespace Wooly.Core.Profiles;

/// <summary>
///     The profiles this machine has, across both of the places one is kept: the config file that says where a profile
///     points, and the credential store that holds its access token (ADR-0003). Front ends go through here rather than
///     through the two stores, so that neither can be updated without the other.
/// </summary>
public interface IProfileRegistry
{
    /// <summary>
    ///     Where this machine's access tokens are being kept, so a front end can show the weaker of ADR-0003's two
    ///     stores as a visible tradeoff. Reading it settles which store is in use, which may open the keyring.
    /// </summary>
    CredentialStorage TokenStorage { get; }

    /// <summary>Every profile that has been set up, ordered by name. Reads no access tokens.</summary>
    IReadOnlyList<ProfileSummary> List();

    /// <summary>
    ///     Stores <paramref name="profile" /> under <paramref name="name" /> along with its access token, replacing any
    ///     profile that already had that name — which is how a profile whose token was revoked gets a working one
    ///     again. Where no profile is current, this one becomes it: a client with profiles and no current profile can
    ///     do nothing at all until something chooses. Choosing otherwise is <see cref="Switch" />'s job, so a profile
    ///     added alongside a current one does not displace it.
    /// </summary>
    /// <returns>What that turned out to do, including whether this profile is now the one commands default to.</returns>
    /// <exception cref="ArgumentException">
    ///     The profile's instance is not a bare domain (<see cref="InstanceDomain" />). A caller is expected to have
    ///     rejected that against the value the user gave; reaching here with one is a defect, not user error.
    /// </exception>
    ProfileAddition Add(string name, ProfileConfig profile, string accessToken);

    /// <summary>Makes <paramref name="name" /> the profile commands default to, until something switches again.</summary>
    /// <exception cref="Errors.UnknownProfileException">No profile by that name has been set up.</exception>
    void Switch(string name);

    /// <summary>
    ///     The profile to act as: <paramref name="requestedName" /> if an invocation named one, and the current profile
    ///     otherwise. Naming one here changes nothing about which profile is current.
    /// </summary>
    /// <param name="requestedName">The <c>--profile</c> override, or <see langword="null" /> to use the current one.</param>
    /// <exception cref="Errors.UnknownProfileException">A profile was named that has not been set up.</exception>
    /// <exception cref="Errors.AuthenticationException">No profile is current, or the profile has no token stored.</exception>
    /// <exception cref="Errors.ConfigurationException">
    ///     The config file names a current profile that does not exist, or gives the profile an instance that is not a
    ///     bare domain — both of which a hand-edit can produce.
    /// </exception>
    ActiveProfile Resolve(string? requestedName);
}
