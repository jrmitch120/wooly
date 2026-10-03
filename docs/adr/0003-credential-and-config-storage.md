# OS-native credential storage with a plaintext fallback; TOML for config

Access tokens are secrets and need OS-native protection; everything else (profiles, current-profile selection, preferences) is ordinary config. We store tokens via `Devlooped.CredentialManager` (a repackaged Git Credential Manager), which reaches Keychain on macOS, Credential Manager on Windows, and Secret Service on Linux. When no Linux keyring is available (e.g. a bare server), it fails cleanly rather than hanging, and we fall back to a plaintext file rather than refusing to run — a deliberate choice to keep the tool usable in that environment, with the security tradeoff made obvious rather than silent. Non-secret config is a human-readable TOML file, parsed via `Tomlyn`, at the OS-conventional path (.NET's `SpecialFolder`).

## Consequences

A future reader seeing plaintext credentials on some Linux systems should not assume it's a bug — it's the intended fallback when no OS keyring exists, not the default path on macOS/Windows/most Linux desktops.

There are two roads to that fallback, not one. The first is the case above: nothing is there to hold a secret. The second arrived with #34, which pins the backing store this client asks Git Credential Manager for rather than inheriting whatever the user configured for Git — so a keyring that exists but is not the pinned one is refused too. That refusal is deliberate, and on Linux it has a cost: GCM's `gpg`/`pass` store is genuinely secure, and a user who had configured it for Git now lands on the plaintext file instead. Accepting it was not an option worth taking, because honouring GCM's configured store is the exact thing #34 stopped doing. Anyone revisiting this should treat it as one question — whether the pin should name a single store per platform or a set of acceptable ones — rather than as a missing special case for `gpg`.

## Amended: Windows reaches Credential Manager through its own API

Git Credential Manager opens only where Git is installed. Building its context looks for `git` on the `PATH` and
throws when there is none, so on a machine without Git the keyring "would not open" and every token went to the
plaintext file. A Windows machine cannot be assumed to have Git. Windows' Credential Manager also refused the service
name this client filed tokens under, since GCM's Windows store parses it as a URI, and that crashed the TUI the first
time a profile was added there.

So on Windows the keyring is Credential Manager through Windows' own API (`CredReadW`, `CredWriteW` and `CredDeleteW` in
`advapi32.dll`, which every Windows has), in `WindowsCredentialStore`. Each profile is one generic credential named
`wooly:access-token:<profile>`, persisted for this user on this machine. macOS and Linux still go through GCM, and the
same Git requirement applies there: without Git they fall back to the plaintext file and say so. On macOS `git` is
always on the `PATH`, as Xcode's command-line shim, so in practice this is a Linux-without-Git consequence.

A keyring that opens and then refuses a save no longer loses the token either. It goes to the plaintext file, the store
reports the file from then on so the warning is shown, and a token is looked for in both places
(`FallbackCredentialStore`).
