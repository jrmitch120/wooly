# Three ties that are on or off, an address looked up before every act, and a request answered by id

Relationship management (#26) adds eleven command-line verbs under one noun — `account follow`/`unfollow`,
`block`/`unblock`, `mute`/`unmute`, `followers`/`following`, and `requests list`/`accept`/`reject`. Underneath they are
three switches, two lists and one answered question. Six decisions keep it that size.

**Three ties that are on or off, not six acts.** `AccountTie` names `Follow`, `Block` and `Mute`, and
`IAccountRelationships.Set` takes the tie and whether it is `wanted`. This is ADR-0009's decision about a post's marks,
applied to an account, and the argument is the same one: un-muting is not its own thing to do, it is muting undone, and
six methods through the port, the adapter, the fake and the CLI is six chances for `unblock` to grow a behaviour `block`
does not have. As three ties, the six commands are six one-line classes over one base, the adapter is one `switch` from
`(tie, wanted)` to Mastodon's six endpoints, and what the user is told comes from one table.

**One port for the whole noun, not three.** Ties, lists and follow requests sit on `IAccountRelationships` together.
They are one subject reached through one family of endpoints, and the screen that will want them (#33's account view)
wants all three at once: what this account is to me, who follows it, and — on my own — who is waiting. Three ports would
be three fakes to write for that one screen, and ADR-0005's seam is only cheap while a fake stays a few lines long.

**A user types an address; every endpoint takes an id; so an address is looked up first.** Mastodon's relationship
endpoints are all `/accounts/:id/…`, and an id means nothing on any other instance — nobody can read one off a profile
page, and no other command in this client asks for one. So `account follow alice@hachyderm.io` spends a call resolving
the address through the same resolving account search a `search` makes (ADR-0011, "asking is resolving"), which is also
what finds an account this instance has never met. Mastonet 3.1.3 exposes no `/accounts/lookup`, so this is the only
route that stays inside ADR-0001's one library. **Only an exact match on the full address is taken**: an instance
answers a lookup with everything that resembles the query, and blocking `alicia@hachyderm.io` because `alice@hachyderm.io`
was not federated yet is not a mistake worth being helpful about. A miss is `UnknownAccountException`, reported with the
usage exit code beside an unknown profile, because it is a value on the command line that is wrong.

**Standing extends `Account` rather than becoming a second account type.** ADR-0011 asked for exactly this, and
`AccountStanding` — following, requested, followed-by, blocking, muting — hangs off `Account` as a nullable property.
Nullable is the point: Mastodon sends a relationship from the tie endpoints and nothing at all from a followers list or
a search, and five falses would tell a reader that the profile follows none of the accounts on its own following list.
Absent means "not asked". In `--json` the standing is nested under `standing` rather than spread across the top level,
because `following` there already means how many accounts this one follows, and one field cannot be both a count and a
yes-or-no. `Account` also gains the instance's `id`, which is what a pending follow request is named by.

**Following a locked account is reported as the request it is.** Mastodon answers a follow of a locked account with
`requested: true` and `following: false`, and this client says "Asked to follow alice@hachyderm.io" rather than "Now
following". The distinction is the whole reason `Set` hands back a standing instead of nothing: told "now following", a
user would wait for posts that cannot arrive until somebody accepts.

**A follow request is answered by id, not by address.** `account requests accept 42` takes the id `account requests list`
just printed — which is the asking account's own id, the same thing Mastodon's `/follow_requests/:id/authorize` takes.
An address would cost a lookup to arrive back at that id, and a request is answered off a list this client printed
seconds earlier, where the id is in front of the user and exact. It is the shape `notification dismiss` already has.
Answering reads the account first and acts second: Mastonet's `AuthorizeRequest` and `RejectRequest` hand back nothing,
so a request accepted first would leave nobody to name in the report, and an id that names nobody now fails before
anything has been let in.

## Consequences

`account followers` with nobody named costs two calls — `verify_credentials` for the profile's own id, then the list —
and every tie costs two, the lookup and the act. That is the price of addressing accounts the way users read them.
A client that wanted one call per act would have to cache addresses against ids, which is a store to invalidate and a
way to act on the wrong account after somebody moves instances.

The paged-list settings collapse that ADR-0007, ADR-0010 and ADR-0011 each deferred happens here, because this is the
genuinely paged third list those ADRs were waiting for: `PagedListSettings` now carries `--limit` and `--json` for
timelines, notifications and account lists alike. The one thing it cost is the per-command wording of the `--limit`
help text — an attribute is fixed for every command that inherits it — so the description reads "How many to fetch"
everywhere, and only the message turning down a limit of none still names what is being counted.

`AccountStanding` carries five of the thirteen facts a Mastodon relationship holds. Endorsements, domain blocks, notes,
notification-muting and whether boosts are shown are left off deliberately: each belongs to a command this client does
not have, and a record that held them would promise answers nothing here can give. `account block` blocks the account,
not its domain — `BlockDomain` is a much larger act, and it should be its own verb when it arrives rather than a flag on
this one.

Following always asks for boosts and muting always mutes notifications, because those are the endpoints' own defaults
and this client has no flag for either. A `--no-boosts` on `follow`, or a mute that leaves notifications alone, is a
later ticket that changes only `AccountRelationships.Apply` — the tie is already the right shape to carry it.

Nothing here reads a relationship without changing it. There is no `account show`, so the only way to see where you
stand with somebody is to act on them, or to read a list. That is the gap #33's account view fills, and it needs one
more call on this port — `GetAccountRelationships` — rather than a new port.
