using Wooly.Core;
using Wooly.Core.Credentials;
using Wooly.Core.Profiles;

namespace Wooly.Tui.Shell;

/// <summary>
///     Everything the shell reaches this machine's profiles through: the profiles screen's list, the store its tokens
///     are kept in, and the two ways a profile comes to be on this machine — signing in, and checking the token that
///     came of it (ADR-0004, ADR-0020).
/// </summary>
/// <remarks>
///     Kept off <see cref="ShellPorts" /> on purpose. Every port there reaches an instance a reader is acting as, and
///     this reaches the local config — as the web browser reaches neither and is kept off it too (#85). The authorizer
///     and the verifier do reach an instance, but not as anybody: they are how a profile comes to be on this machine
///     rather than anything read off an instance as the profile a session is acting as, which is why they joined this
///     when adding a profile was built (#245).
/// </remarks>
/// <param name="Registry">
///     The profiles set up on this machine — the one path both front ends read and write them through (ADR-0003).
/// </param>
/// <param name="Paths">Where the files are, which is what the plaintext-token warning names.</param>
/// <param name="Authorizer">Signs in through the browser, ADR-0004's primary way in.</param>
/// <param name="Verifier">
///     Asks an instance who a token belongs to, before anything is written for it — the same check <c>profile add</c>
///     makes, and how the account a profile signs in as is learned.
/// </param>
public sealed record ProfilePorts(
    IProfileRegistry Registry,
    WoolyPaths Paths,
    IBrowserAuthorizer Authorizer,
    IAccessTokenVerifier Verifier)
{
    /// <summary>
    ///     The warning ADR-0003 owes wherever tokens are kept in the clear, then one for each profile the keyring
    ///     would not answer for (#296) — none where tokens are in a keyring that answers. The same words the CLI says
    ///     them in, since both read <see cref="TokenStorageDescription" />.
    /// </summary>
    /// <remarks>Reading which store is in use may open the keyring, which is local — nothing here reaches an instance.</remarks>
    public IReadOnlyList<string> Warnings
    {
        get
        {
            var warnings = new List<string>();

            if (Registry.TokenStorage is CredentialStorage.PlaintextFile)
            {
                warnings.Add($"Warning: {TokenStorageDescription.InTheClear(Paths)}");
            }

            warnings.AddRange(Registry.KeyringUnanswered.Select(
                unanswered => $"Warning: {TokenStorageDescription.Unanswered(unanswered)}"));

            return warnings;
        }
    }
}
