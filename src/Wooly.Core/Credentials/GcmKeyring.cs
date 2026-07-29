using GitCredentialManager;
using OsKeyring = GitCredentialManager.ICredentialStore;

namespace Wooly.Core.Credentials;

/// <summary>
///     Git Credential Manager's answer to "open a keyring": the store it hands back, paired with the name of the
///     backing store it chose to put behind that store. The two are separate answers, and only the second one is
///     worth anything — GCM returns a working object whether it resolved to the OS keyring, a file of cleartext, or
///     nothing at all, so "it did not throw" says nothing about where a secret written through it lands.
/// </summary>
/// <param name="BackingStoreName">
///     GCM's own name for the store it chose — <c>keychain</c>, <c>plaintext</c>, <c>none</c> — or
///     <see langword="null" /> where nothing selected one and GCM was left to pick per platform.
/// </param>
/// <param name="Keyring">The store itself, whatever ended up behind it.</param>
internal sealed record GcmKeyring(string? BackingStoreName, OsKeyring Keyring)
{
    /// <summary>
    ///     The environment variable GCM reads its backing store from, ahead of Git's own <c>credential.credentialStore</c>
    ///     config key. Spelled out rather than read from the library: it is <c>internal</c> there, and it is a name
    ///     users type, so it belongs to GCM's published contract rather than to any one version of the package.
    /// </summary>
    public const string BackingStoreVariable = "GCM_CREDENTIAL_STORE";

    /// <summary>
    ///     The one backing store this client will accept on the machine it is running on, under GCM's own name for it.
    ///     Literals for the same reason as <see cref="BackingStoreVariable" />.
    ///     <para>
    ///         One per platform, deliberately, and the cost of that is on Linux: GCM's <c>gpg</c>/<c>pass</c> store is
    ///         secure, and a user who configured it for Git is refused here and falls back to the plaintext file. It
    ///         is not an oversight and not a special case to add — accepting it would mean reading the configuration
    ///         this pin exists to stop reading. ADR-0003's Consequences carries the whole tradeoff.
    ///     </para>
    /// </summary>
    public static string BackingStoreForThisMachine =>
        OperatingSystem.IsWindows() ? "wincredman"
        : OperatingSystem.IsMacOS() ? "keychain"
        : "secretservice";

    /// <summary>
    ///     Takes the choice of backing store away from Git and gives it to this client, for this process only.
    ///     <para>
    ///         GCM picks its backing store from <c>GCM_CREDENTIAL_STORE</c> and Git's <c>credential.credentialStore</c>
    ///         — settings a user configured for Git, for reasons that have nothing to do with Mastodon. Inheriting them
    ///         means an unrelated tool's configuration decides whether this client's access tokens reach the keyring,
    ///         a cleartext file, or nowhere. Setting the variable ourselves, before the context that reads it is built,
    ///         overrides both.
    ///     </para>
    /// </summary>
    public static void PinBackingStore() =>
        Environment.SetEnvironmentVariable(BackingStoreVariable, BackingStoreForThisMachine);

    /// <summary>
    ///     Asks GCM for a keyring under this client's own choice of backing store, and reports back both what it
    ///     handed over and what it decided to put behind it. The pin goes in first: GCM reads its settings once, while
    ///     the context is being built.
    /// </summary>
    public static GcmKeyring Open()
    {
        PinBackingStore();

        // The context is deliberately not disposed. The store it resolves is kept for the rest of the run, and
        // disposing the context that produced it is not something the library documents as safe — its own
        // CredentialManager.Create leaves the context alive for exactly the same reason.
        var context = CredentialManager.CreateContext(WoolyClient.Name);

        return new GcmKeyring(context.Settings.CredentialBackingStore, context.CredentialStore);
    }
}
