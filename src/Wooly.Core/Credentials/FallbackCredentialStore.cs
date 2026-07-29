namespace Wooly.Core.Credentials;

/// <summary>
///     Picks the store this machine can actually use: the OS keyring where one answers, and the plaintext file where
///     none does (ADR-0003). The choice is made once, on first use, and reported through <see cref="Storage" /> so a
///     front end can tell the user which tradeoff they are living with.
/// </summary>
/// <param name="openKeyring">
///     Opens this machine's keyring, or throws if it has none. Kept a delegate so the no-keyring path is reachable in
///     a test — and so the keyring, which may prompt or block, is never touched until a token is actually wanted.
/// </param>
/// <param name="whenNoKeyring">The store to use instead if <paramref name="openKeyring" /> cannot deliver one.</param>
public sealed class FallbackCredentialStore(
    Func<ICredentialStore> openKeyring,
    ICredentialStore whenNoKeyring) : ICredentialStore
{
    /// <summary>
    ///     Lazy so that opening happens on first use, and only ever once: the TUI can reach this store from more than
    ///     one place at a time, and two threads racing into a keyring means two prompts at the user.
    /// </summary>
    private readonly Lazy<ICredentialStore> _chosen = new(() => Choose(openKeyring, whenNoKeyring));

    /// <inheritdoc />
    public CredentialStorage Storage => _chosen.Value.Storage;

    /// <inheritdoc />
    public string? FindAccessToken(string profileName) => _chosen.Value.FindAccessToken(profileName);

    /// <inheritdoc />
    public void SaveAccessToken(string profileName, string accessToken) =>
        _chosen.Value.SaveAccessToken(profileName, accessToken);

    /// <inheritdoc />
    public bool DeleteAccessToken(string profileName) => _chosen.Value.DeleteAccessToken(profileName);

    private static ICredentialStore Choose(Func<ICredentialStore> openKeyring, ICredentialStore whenNoKeyring)
    {
        try
        {
            return openKeyring();
        }
        catch (Exception)
        {
            // Every platform's keyring backend has its own way of saying "not here", and none of them is a type this
            // client can enumerate. Whatever it was, this machine cannot store secrets securely, and the answer is
            // the same either way: fall back rather than refuse to run. Never retried — a keyring that is missing
            // once is missing for the rest of the run, and asking again would only cost another prompt or timeout.
            return whenNoKeyring;
        }
    }
}
