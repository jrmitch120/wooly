using Wooly.Core;
using Wooly.Core.Credentials;

namespace Wooly.Cli.Commands;

/// <summary>
///     Where this machine's access tokens are kept, in words. One wording, in one place, so that the command that
///     stores a token and the command that reports on one can never describe the same store two different ways.
/// </summary>
internal static class TokenStorageDescription
{
    /// <summary>Describes <paramref name="storage" /> as the end of the sentence "the access token is …".</summary>
    public static string For(CredentialStorage storage, WoolyPaths paths) => storage switch
    {
        CredentialStorage.OsKeyring => "kept in this machine's keyring",

        // Named in full, because the whole point of saying this is that the user can go and look at the file, or
        // delete it.
        _ => $"kept in the clear in {paths.CredentialFile}",
    };
}
