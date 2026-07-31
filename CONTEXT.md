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

**Direct message**:
A post whose visibility is direct, which Mastodon delivers only to the accounts its text mentions. Not a separate kind
of thing from a post and not composed by a separate path — `dm send` is post authoring with the audience settled and the
recipient written into the text (ADR-0013). "DM" is the CLI's noun for the branch and nothing else.
_Avoid_: private message, PM

**Conversation**:
One thread of direct messages, as the instance keeps it: who it is with, whether anything in it is unread, and what was
last said. It carries an id of its own, which is what shows it and what marks it read — distinct from the id of any
**post** in it, and the two are not interchangeable.

**Instance**:
The Mastodon server a given account is registered on, identified by its domain (e.g. `mastodon.social`). Mastodon's own newer UI/docs increasingly say "server", but this project uses "instance" throughout for consistency with the wider ecosystem (other clients, API error messages) and to avoid ambiguity with "server" as hostname or client-server architecture.
_Avoid_: server (when referring to a Mastodon instance)

**Account**:
A user's identity on a specific instance, addressed as `username@instance` when referenced from outside its home instance. Distinct from a local CLI **profile** (below).

**Tie**:
One of the three things the profile's own account can have with another and undo again: following it, blocking it, or
muting it. Each is on or off rather than an act of its own, so `unfollow` is a follow taken off rather than a fourth
thing to do. Distinct from **standing**, which is what the instance reports the ties currently are.

**Follow request**:
A follow waiting for the account being followed to accept it, which is what following a **locked** account — one that
approves its followers by hand — leaves behind instead of a follow. Named by the id of the account that asked, which is
what accepting or rejecting one takes.

**Standing**:
Where the profile's own account stands with another one: whether it follows it, has a **follow request** waiting with
it, is followed by it, and whether it has blocked or muted it. An instance sends this only where it is asked, so an
account may carry none — which says the question was not put, not that the answers are all no.

**Profile**:
A named local credential/config entry in this CLI tool, pointing at one Mastodon account. A user may have multiple profiles (e.g. personal + work accounts, possibly on different instances). One profile is the "current" profile used by default; commands may override it per-invocation.
_Avoid_: account (when referring to the CLI's local credential entry, to keep it distinct from the Mastodon account itself)
