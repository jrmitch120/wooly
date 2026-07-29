# Use Mastonet as the Mastodon API client library

We need a .NET library to talk to the Mastodon REST and streaming API rather than hand-rolling HTTP calls and response models. We chose `Mastonet` (MIT-licensed): it covers the full REST surface plus both WebSocket and HTTP-streaming transports, and exposes `IMastodonClient`/`IAuthenticationClient` interfaces over an injectable `HttpClient`, which gives us a clean seam for testing without a live server.

## Considered Options

- **Hand-rolled HTTP client** — full control, but means owning JSON models and pagination/error handling for the entire Mastodon API surface ourselves.
- **Mastonet** — chosen. Its maintenance pace is slow but not abandoned, and no better-maintained alternative covering the same surface was found.

## Consequences

Because Mastonet's release cadence is slow, drift between its models and the live Mastodon API is a real risk — this is why the testing strategy (ADR-0005) includes a small integration suite against a real instance rather than relying on mocked unit tests alone.
