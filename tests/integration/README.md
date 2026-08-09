# Integration suite

A small, non-exhaustive suite that runs against a real, dockerized Mastodon instance instead of a fake, to catch
drift between Mastonet's model classes and what an instance actually returns (ADR-0001, ADR-0005). It is tagged
`Category=Integration` and skips itself when no live instance is configured, so the default `dotnet test` run never
needs Docker.

## Running it locally

Requires Docker and `docker compose`.

```sh
tests/integration/run.sh
```

Seeds the instance, runs the suite against it, and tears it down again on the way out — win or lose, its exit code
is `dotnet test`'s own. The first run is slower — pulling images and loading the Mastodon schema — after which it
takes well under a minute.

### Iterating without paying the seed cost every time

`run.sh` tears the instance down after one run, which is wasteful if you are fixing a failing test and want to run
it again a minute later. Do the same three steps by hand instead, and leave the instance up between runs:

```sh
eval "$(tests/integration/seed.sh 2>/dev/null)"
dotnet test tests/Wooly.Tests/Wooly.Tests.csproj --filter "Category=Integration"
```

`seed.sh` brings up the stack below, creates a test account, mints it an access token, and prints two lines on
stdout — `WOOLY_INTEGRATION_INSTANCE=<host:port>` and `WOOLY_INTEGRATION_TOKEN=<token>` — with everything else it
does going to stderr. The `eval` above discards stderr and exports those two lines, which is what the tests read the
live instance's coordinates from (see `tests/Wooly.Tests/Integration/LiveInstance.cs`). Run `seed.sh` on its own
first if you would rather see its progress, then export its two lines yourself. Re-run the `dotnet test` line as
many times as you like against the same instance; run `seed.sh` again only once the instance is gone.

Tear the instance down when finished:

```sh
docker compose -f tests/integration/docker-compose.yml down --volumes
```

## What's in this directory

- **`docker-compose.yml`** — postgres, redis, Mastodon (`web` and `sidekiq`), and Caddy. Mastonet hardcodes
  `https://` onto every request it makes, so a plain-HTTP Mastodon would not be reachable through the same client
  code the product itself uses; Caddy exists solely to terminate TLS with a self-signed certificate for that reason.
- **`mastodon.env`** — fixed, checked-in secrets for the stack. Not a leak: the instance they belong to exists only
  for the length of one test run, on loopback only, and holds nothing worth protecting.
- **`Caddyfile`** — the TLS termination in front of Mastodon's `web` service.
- **`seed.sh`** — brings the stack up and mints a token. It mints the token directly (`tootctl` plus a `rails
  runner` snippet) rather than through the browser OAuth flow ADR-0004 describes for the product — that flow needs a
  human at a browser, which neither this script nor a CI runner has.

## CI

`.github/workflows/ci.yml` runs this suite in its own `integration` job, separate from the fast `test` job
(`--filter "Category!=Integration"`) that every push and pull request waits on. The integration job seeds the stack
the same way a local run does, then tears it down afterward regardless of outcome.
