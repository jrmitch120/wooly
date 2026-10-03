using System.Collections.Concurrent;
using Wooly.Core.Errors;

namespace Wooly.Core.Credentials;

/// <summary>
///     Picks the store this machine can actually use: the OS keyring where one answers, and the plaintext file where
///     none does (ADR-0003). The choice is made once, on first use, and reported through <see cref="Storage" /> so a
///     front end can tell the user which tradeoff they are living with.
/// </summary>
/// <remarks>
///     A keyring that opens can still refuse a save — Windows' did, on a service name it could not parse, and the
///     exception took the TUI down. A refused save is not a token lost: it goes to the plaintext file, the store says
///     the file is where tokens are for as long as that token is still in it, and a token found in neither place is looked for in both, so
///     one put in the file on an earlier run is not a profile signed out on this one.
/// </remarks>
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

    /// <summary>
    ///     The profiles whose token a save the keyring refused has put in the plaintext file this run, and that are
    ///     still there: a later save the keyring takes, or the profile's removal, takes one off.
    /// </summary>
    private readonly ConcurrentDictionary<string, bool> _refused = new();

    /// <summary>
    ///     The profiles the keyring would not read for this run, with what it said: a locked keyring, a prompt
    ///     declined, a service down. A later answer for the profile takes it off, as does its token being found in the
    ///     file after all (#296).
    /// </summary>
    private readonly ConcurrentDictionary<string, string> _unanswered = new();

    /// <inheritdoc />
    public CredentialStorage Storage => _refused.IsEmpty ? _chosen.Value.Storage : whenNoKeyring.Storage;

    /// <inheritdoc />
    public IReadOnlyList<UnansweredKeyring> Unanswered =>
    [
        .. _unanswered.OrderBy(entry => entry.Key, StringComparer.Ordinal)
                      .Select(entry => new UnansweredKeyring(entry.Key, entry.Value)),
    ];

    /// <summary>The keyring, where one opened, and <see langword="null" /> where the file is all there is.</summary>
    private ICredentialStore? Keyring => ReferenceEquals(_chosen.Value, whenNoKeyring) ? null : _chosen.Value;

    /// <inheritdoc />
    public string? FindAccessToken(string profileName)
    {
        if (Keyring is { } keyring)
        {
            try
            {
                var token = keyring.FindAccessToken(profileName);
                _unanswered.TryRemove(profileName, out _);

                if (token is not null)
                {
                    return token;
                }
            }
            catch (Exception refusal)
            {
                // The file may still have the token. If it does not, the profile looks signed out, so Unanswered says
                // why rather than leaving the user to sign in again over a keyring that is only locked.
                _unanswered[profileName] = refusal.Message;
            }
        }

        var fromFile = whenNoKeyring.FindAccessToken(profileName);

        if (fromFile is not null)
        {
            // Found after all, so the profile does not look signed out and there is nothing to warn about.
            _unanswered.TryRemove(profileName, out _);
        }

        return fromFile;
    }

    /// <inheritdoc />
    public void SaveAccessToken(string profileName, string accessToken)
    {
        if (Keyring is not { } keyring)
        {
            whenNoKeyring.SaveAccessToken(profileName, accessToken);

            return;
        }

        try
        {
            keyring.SaveAccessToken(profileName, accessToken);
        }
        catch (Exception)
        {
            // Every backend refuses in its own words, as Choose says of opening. Whatever it was, the token is kept
            // rather than lost, in the file, and Storage says so from here on so the user is told.
            _refused[profileName] = true;
            whenNoKeyring.SaveAccessToken(profileName, accessToken);

            return;
        }

        // Held where it belongs now, so a copy an earlier refusal left in the clear goes.
        whenNoKeyring.DeleteAccessToken(profileName);
        _refused.TryRemove(profileName, out _);
        _unanswered.TryRemove(profileName, out _);
    }

    /// <inheritdoc />
    /// <exception cref="KeyringUnansweredException">The keyring would not give the token up.</exception>
    public bool DeleteAccessToken(string profileName)
    {
        var fromKeyring = false;

        if (Keyring is { } keyring)
        {
            try
            {
                fromKeyring = keyring.DeleteAccessToken(profileName);
                _unanswered.TryRemove(profileName, out _);
            }
            catch (Exception refusal)
            {
                // Not passed over, as a read is: carrying on would let the profile go and leave its token in the
                // keyring, out of this client's reach. Nothing has been touched, so trying again finishes the job.
                throw new KeyringUnansweredException(profileName, refusal);
            }
        }

        var fromFile = whenNoKeyring.DeleteAccessToken(profileName);
        _refused.TryRemove(profileName, out _);

        return fromKeyring || fromFile;
    }

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
