#!/usr/bin/env bash
# The whole integration suite in one command: seeds a throwaway Mastodon instance, runs the live tests against it,
# and tears the instance down again on the way out — win or lose. README.md walks through the same three steps by
# hand for whoever wants to leave the instance up between runs; this is for whoever does not.
#
# Usage: tests/integration/run.sh
# Exits with dotnet test's own exit code, so this is safe to wire into anything that checks one.
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")"

cleanup() {
  echo "Tearing down the instance..." >&2
  # Best-effort: a teardown failure should never hide a test failure behind it.
  docker compose -f docker-compose.yml down --volumes >&2 || true
}
trap cleanup EXIT

eval "$(./seed.sh)"
export WOOLY_INTEGRATION_INSTANCE WOOLY_INTEGRATION_TOKEN WOOLY_INTEGRATION_VOTER_TOKEN

dotnet test ../Wooly.Tests/Wooly.Tests.csproj --filter "Category=Integration"
