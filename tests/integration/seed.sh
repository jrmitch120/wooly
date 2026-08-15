#!/usr/bin/env bash
# Brings up the throwaway Mastodon instance in docker-compose.yml and mints a token the integration suite can sign in
# with. tootctl and a `rails runner` snippet do the minting rather than the browser OAuth flow ADR-0004 describes —
# that flow needs a human at a browser, which is exactly what a CI runner and this script do not have. That is a fact
# about how a token gets into this *test* instance, not a second way the product itself signs a profile in.
#
# Usage: tests/integration/seed.sh
# Prints three lines on success:
#   WOOLY_INTEGRATION_INSTANCE=<host:port>
#   WOOLY_INTEGRATION_TOKEN=<access token>
#   WOOLY_INTEGRATION_VOTER_TOKEN=<access token>
# which the caller is expected to export before running `dotnet test --filter Category=Integration`.
# See README.md in this directory for the full walkthrough, including teardown.
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")"

readonly USERNAME="woolytester"
readonly EMAIL="woolytester@example.com"
# Mastodon refuses a vote in a poll's own author's name ("You cannot vote in your own polls"), so the suite needs a
# second, otherwise-unused account to cast one against a poll the first account published.
readonly VOTER_USERNAME="woolyvoter"
readonly VOTER_EMAIL="woolyvoter@example.com"
readonly APP_NAME="wooly-integration-tests"
readonly INSTANCE="localhost:34443"

compose() { docker compose -f docker-compose.yml "$@"; }

echo "Starting the database and cache..." >&2
compose up -d --wait db redis

echo "Preparing the Mastodon schema..." >&2
compose run --rm web bundle exec rails db:prepare >&2

echo "Starting Mastodon..." >&2
compose up -d --wait web sidekiq caddy

# Ensures one account exists and has a token, printing the token on stdout. find_or_create_by keeps the token half
# idempotent across repeated local runs, which is not free with tootctl alone: it has no equivalent read/create for
# either a Doorkeeper application or a token. Both accounts share one Doorkeeper application; a token is what scopes
# access to an account, not the application it was minted through.
mint_token() {
  local username="$1" email="$2"

  echo "Ensuring the $username account exists..." >&2
  if ! compose exec -T web bin/tootctl accounts create "$username" --email "$email" --confirmed --approve >&2; then
    echo "  ($username already exists, continuing)" >&2
  fi

  echo "Minting an access token for $username..." >&2
  compose exec -T web bin/rails runner "
    account = Account.find_local!('$username')
    app = Doorkeeper::Application.find_or_create_by!(name: '$APP_NAME') do |a|
      a.redirect_uri = 'urn:ietf:wg:oauth:2.0:oob'
      a.scopes = 'read write'
    end
    token = Doorkeeper::AccessToken.find_or_create_by!(resource_owner_id: account.user.id, application_id: app.id) do |t|
      t.scopes = 'read write'
    end
    puts token.token
  " | tail -n 1
}

token=$(mint_token "$USERNAME" "$EMAIL")
voter_token=$(mint_token "$VOTER_USERNAME" "$VOTER_EMAIL")

echo "WOOLY_INTEGRATION_INSTANCE=$INSTANCE"
echo "WOOLY_INTEGRATION_TOKEN=$token"
echo "WOOLY_INTEGRATION_VOTER_TOKEN=$voter_token"
