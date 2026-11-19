# Integration suite

Runs the tests against a real, dockerized Mastodon instance instead of a fake. Requires Docker.

```sh
tests/integration/run.sh
```

That seeds an instance, runs the suite, and tears it down again. First run is slower (pulling images); after that,
well under a minute.

The instance runs Mastodon's streaming service (`streaming`, the `mastodon-streaming` image at the same version as
`web`) beside it. Caddy routes `/api/v1/streaming` to it and everything else to `web`, and the instance advertises its
stream at Caddy's port (`STREAMING_API_BASE_URL` in `mastodon.env`), which is where the Inbox stream tests connect.

The instance translates through a LibreTranslate container (`libretranslate`) loading German and English only, at
`LIBRE_TRANSLATE_ENDPOINT` in `mastodon.env`. It downloads those two models each time it starts, which adds about a
minute; `seed.sh` waits until it serves German before handing over.

Skipped automatically in the default `dotnet test` run — no Docker needed for everyday work.
