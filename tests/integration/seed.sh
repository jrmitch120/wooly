#!/usr/bin/env bash
# Brings up the throwaway Mastodon instance in docker-compose.yml and mints a token the integration suite can sign in
# with. tootctl and a `rails runner` snippet do the minting rather than the browser OAuth flow ADR-0004 describes —
# that flow needs a human at a browser, which is exactly what a CI runner and this script do not have. That is a fact
# about how a token gets into this *test* instance, not a second way the product itself signs a profile in.
#
# Usage: tests/integration/seed.sh
# Prints two lines on success:
#   WOOLY_INTEGRATION_INSTANCE=<host:port>
#   WOOLY_INTEGRATION_TOKEN=<access token>
# which the caller is expected to export before running `dotnet test --filter Category=Integration`.
# See README.md in this directory for the full walkthrough, including teardown.
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")"

readonly USERNAME="woolytester"
readonly EMAIL="woolytester@example.com"
readonly APP_NAME="wooly-integration-tests"
readonly INSTANCE="localhost:34443"

compose() { docker compose -f docker-compose.yml "$@"; }

echo "Starting the database and cache..." >&2
compose up -d --wait db redis

echo "Preparing the Mastodon schema..." >&2
compose run --rm web bundle exec rails db:prepare >&2

echo "Starting Mastodon..." >&2
compose up -d --wait web sidekiq caddy

echo "Ensuring the test account exists..." >&2
if ! compose exec -T web bin/tootctl accounts create "$USERNAME" --email "$EMAIL" --confirmed --approve >&2; then
  echo "  ($USERNAME already exists, continuing)" >&2
fi

echo "Minting an access token for it..." >&2
# find_or_create_by keeps this idempotent across repeated local runs, which is not free with tootctl alone: it has no
# equivalent read/create for either a Doorkeeper application or a token.
token=$(compose exec -T web bin/rails runner "
  account = Account.find_local!('$USERNAME')
  app = Doorkeeper::Application.find_or_create_by!(name: '$APP_NAME') do |a|
    a.redirect_uri = 'urn:ietf:wg:oauth:2.0:oob'
    a.scopes = 'read write'
  end
  token = Doorkeeper::AccessToken.find_or_create_by!(resource_owner_id: account.user.id, application_id: app.id) do |t|
    t.scopes = 'read write'
  end
  puts token.token
" | tail -n 1)

echo "WOOLY_INTEGRATION_INSTANCE=$INSTANCE"
echo "WOOLY_INTEGRATION_TOKEN=$token"
