# OS-native credential storage with a plaintext fallback; TOML for config

Access tokens are secrets and need OS-native protection; everything else (profiles, current-profile selection, preferences) is ordinary config. We store tokens via `Devlooped.CredentialManager` (a repackaged Git Credential Manager), which reaches Keychain on macOS, Credential Manager on Windows, and Secret Service on Linux. When no Linux keyring is available (e.g. a bare server), it fails cleanly rather than hanging, and we fall back to a plaintext file rather than refusing to run — a deliberate choice to keep the tool usable in that environment, with the security tradeoff made obvious rather than silent. Non-secret config is a human-readable TOML file, parsed via `Tomlyn`, at the OS-conventional path (.NET's `SpecialFolder`).

## Consequences

A future reader seeing plaintext credentials on some Linux systems should not assume it's a bug — it's the intended fallback when no OS keyring exists, not the default path on macOS/Windows/most Linux desktops.
