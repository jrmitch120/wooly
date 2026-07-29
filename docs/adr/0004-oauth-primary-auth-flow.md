# Browser-based OAuth as the primary auth flow; manual token entry as the headless fallback

Connecting a profile to a Mastodon account needs an authentication flow. We use browser-based OAuth as the primary path, since it's the flow Mastodon instances expect and never exposes a password to the client. For headless environments where a browser redirect isn't possible, we fall back to manually pasting an access token obtained out-of-band. Password-grant authentication (username/password directly to the client) is explicitly ruled out.

## Consequences

Ruling out password grant means there is no "just type your password" path, even though users may expect one from other CLI tools — this is deliberate, not an oversight, since it would mean handling and potentially mishandling account passwords directly.
