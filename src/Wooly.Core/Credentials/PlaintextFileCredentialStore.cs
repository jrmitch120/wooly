using Tomlyn;
using Tomlyn.Model;

namespace Wooly.Core.Credentials;

/// <summary>
///     Access tokens in a file this client owns, in the clear. The weaker half of ADR-0003's pair, reached only where
///     the machine has no keyring to hold them instead — a bare Linux server, typically. It is a deliberate choice to
///     stay usable there rather than refuse to run, so the file says as much at the top of itself, and is created
///     readable by nobody but its owner.
/// </summary>
public sealed class PlaintextFileCredentialStore(WoolyPaths paths) : ICredentialStore
{
    private const string TokensKey = "access_tokens";

    private const string Header =
        """
        # Mastodon access tokens, stored in the clear because no OS keyring was available on this machine.
        # Anyone who can read this file can act as you. Delete it once a keyring is available.

        """;

    private static readonly UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    /// <inheritdoc />
    public CredentialStorage Storage => CredentialStorage.PlaintextFile;

    /// <inheritdoc />
    public string? FindAccessToken(string profileName) =>
        Read().TryGetValue(profileName, out var token) ? token as string : null;

    /// <inheritdoc />
    public void SaveAccessToken(string profileName, string accessToken)
    {
        var tokens = Read();
        tokens[profileName] = accessToken;

        Write(tokens);
    }

    /// <inheritdoc />
    public bool DeleteAccessToken(string profileName)
    {
        var tokens = Read();

        if (!tokens.Remove(profileName))
        {
            return false;
        }

        Write(tokens);

        return true;
    }

    private TomlTable Read()
    {
        // A malformed file is reported rather than overwritten: replacing it would silently sign the user out of
        // every profile at once.
        var root = TomlFile.Read(paths.CredentialFile);

        return root.TryGetValue(TokensKey, out var tokens) && tokens is TomlTable table ? table : new TomlTable();
    }

    private void Write(TomlTable tokens) => TomlFile.Write(
        paths.CredentialFile,
        Header + TomlSerializer.Serialize(new TomlTable { [TokensKey] = tokens }),
        OwnerOnly);
}
