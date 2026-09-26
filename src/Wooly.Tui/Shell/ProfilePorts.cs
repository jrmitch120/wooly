using Wooly.Core;
using Wooly.Core.Credentials;
using Wooly.Core.Profiles;

namespace Wooly.Tui.Shell;

/// <summary>
///     Everything the shell reaches this machine's profiles through: the profiles screen's list, and the store its
///     tokens are kept in (ADR-0020).
/// </summary>
/// <remarks>
///     Kept off <see cref="ShellPorts" /> on purpose. Every port there reaches an instance, and this reaches the local
///     config — as the web browser reaches neither and is kept off it too (#85). The browser authorizer and the
///     access-token verifier join this when adding a profile is built, being how a profile comes to be on this machine
///     rather than anything read off an instance a reader is acting as.
/// </remarks>
/// <param name="Registry">
///     The profiles set up on this machine — the one path both front ends read and write them through (ADR-0003).
/// </param>
/// <param name="Paths">Where the files are, which is what the plaintext-token warning names.</param>
public sealed record ProfilePorts(IProfileRegistry Registry, WoolyPaths Paths)
{
    /// <summary>
    ///     The warning ADR-0003 owes wherever tokens are kept in the clear, or <see langword="null" /> where they are
    ///     in the keyring. The same words the CLI says it in, since both read <see cref="TokenStorageDescription" />.
    /// </summary>
    /// <remarks>Reading which store is in use may open the keyring, which is local — nothing here reaches an instance.</remarks>
    public string? PlaintextWarning => Registry.TokenStorage is CredentialStorage.PlaintextFile and var storage
        ? "Warning: no OS keyring answered on this machine, so access tokens are "
          + $"{TokenStorageDescription.For(storage, Paths)}."
        : null;
}
