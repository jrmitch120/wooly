# Integration suite

Runs the tests against a real, dockerized Mastodon instance instead of a fake. Requires Docker.

```sh
tests/integration/run.sh
```

That seeds an instance, runs the suite, and tears it down again. First run is slower (pulling images); after that,
well under a minute.

Skipped automatically in the default `dotnet test` run — no Docker needed for everyday work.
