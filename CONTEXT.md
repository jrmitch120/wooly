# Mastodon CLI Client

A cross-platform (.NET) terminal client for Mastodon — a scriptable CLI command surface plus an interactive TUI, sharing one API/auth/config layer.

## Language

**Post**:
A single unit of user-authored content on Mastodon (text, media, poll, content warning). The API's wire format calls this a `status`; older community usage calls it a "toot". This project always says "post" in the spec, domain code, and CLI command/output text — `status`/`toot` may still appear at the literal API-wire-format layer (e.g. deserializing the API's `status` JSON field) but never in user-facing language or domain vocabulary.
_Avoid_: status, toot

**Boost**:
Re-sharing another account's post to your own followers. The API calls this a `reblog`. This project always says "boost" in user-facing language and domain code; `reblog` may still appear as the literal API field name at the wire layer.
_Avoid_: reblog, repost, retweet

**Favorite**:
Marking a post as liked, without re-sharing it. The API spells this `favourite` (British spelling). This project uses the US spelling "favorite" everywhere outside the literal API-wire layer.
_Avoid_: favourite, like

**Notification**:
Something an instance tells an account happened to it: somebody mentioned it, followed it, boosted or favorited one of
its posts. This project names those four and passes any other kind on under the instance's own word for it (ADR-0010).
A notification is distinct from the post it is about — it has an id of its own, which is what dismisses it.

**Instance**:
The Mastodon server a given account is registered on, identified by its domain (e.g. `mastodon.social`). Mastodon's own newer UI/docs increasingly say "server", but this project uses "instance" throughout for consistency with the wider ecosystem (other clients, API error messages) and to avoid ambiguity with "server" as hostname or client-server architecture.
_Avoid_: server (when referring to a Mastodon instance)

**Account**:
A user's identity on a specific instance, addressed as `username@instance` when referenced from outside its home instance. Distinct from a local CLI **profile** (below).

**Profile**:
A named local credential/config entry in this CLI tool, pointing at one Mastodon account. A user may have multiple profiles (e.g. personal + work accounts, possibly on different instances). One profile is the "current" profile used by default; commands may override it per-invocation.
_Avoid_: account (when referring to the CLI's local credential entry, to keep it distinct from the Mastodon account itself)
