namespace Wooly.Core.Credentials;

/// <summary>
///     Where this machine's access tokens are kept, in words. One wording, in one place, so that the command that
///     stores a token, the command that reports on one and the TUI's profiles screen can never describe the same store
///     two different ways.
/// </summary>
/// <remarks>
///     In Core rather than beside the CLI's commands, which is where it started, because the TUI owes the same warning
///     (ADR-0003, ADR-0020) and a second copy of the words would be a second place for them to drift.
/// </remarks>
public static class TokenStorageDescription
{
    /// <summary>Describes <paramref name="storage" /> as the end of the sentence "the access token is …".</summary>
    public static string For(CredentialStorage storage, WoolyPaths paths) => storage switch
    {
        CredentialStorage.OsKeyring => "kept in this machine's keyring",

        // Named in full, because the whole point of saying this is that the user can go and look at the file, or
        // delete it.
        _ => $"kept in the clear in {paths.CredentialFile}",
    };

    /// <summary>
    ///     The whole of the warning ADR-0003 owes wherever tokens are kept in the clear, as a sentence with nothing in
    ///     front of it — each front end puts its own "warning" there, in its own case and colour.
    /// </summary>
    public static string InTheClear(WoolyPaths paths) =>
        $"no OS keyring answered on this machine, so the access token is {For(CredentialStorage.PlaintextFile, paths)}.";
}
