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
timelines, notifications and account lists alike. What it cost is the per-command wording of both options' help text —
an attribute is fixed for every command that inherits it — so `--limit` now reads "How many to fetch" rather than "How
many posts to fetch", and `--json` "Write what was read as JSON" rather than "Write the timeline as JSON", on commands
that shipped saying the more specific thing. Only the message turning down a limit of none still names what is being
counted. Buying that wording back means declaring the options per command again, which is the duplication three ADRs
asked to be rid of; the trade is worth revisiting only if a user is actually misled by the general wording.

An address may be written any of the ways Mastodon shows one — `alice@hachyderm.io`, `@alice@hachyderm.io`, or a bare
`alice` for somebody on the profile's own instance — where the issue asked only for `user@instance`. A handle is copied
out of a profile page or a post as often as it is typed, and refusing the two spellings a user is most likely to have
copied would be a rule with nothing behind it. A bare username is qualified with the profile's instance before anything
is matched, so it can never silently reach somebody else's account of the same name.

`account unfollow` reports "Unfollowed", not "no longer following". The same command withdraws a follow request that
was never accepted, and what comes back cannot tell the two apart — either way the profile now neither follows the
account nor waits on it. Reporting the act rather than the state is the only thing true in both cases.

Accounts read from these lists are paged by the instance's own link header alone. Mastodon paginates followers and
following by the id of the *follow*, not of the account followed, and this client never sees one — so `PagedReading`
takes a null fallback cursor here, and an instance that names no next page has ended the list. The alternative, reusing
the last account's id, asks for a page starting somewhere in another id space and silently skips or repeats accounts.
A timeline and an inbox still pass their fallback, because a post and a notification are what those endpoints page by.

`search --json` gains an `id` on every account, because a search result and a followers list are now the same
`AccountDocument`. That is ADR-0011's own request — one spelling of an account wherever it turns up — arriving as an
additive change to a shipped command's output rather than as a new command's.

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
more call on this port — `GetAccountRelationships` — rather than a new port. That call is `Show`, and ADR-0019 later
put a CLI verb on it too (#183), so this paragraph now records where the gap was rather than where it is.

## Amendment: a private note is part of a standing, and the port takes a fifth call (map #159)

Two additions from the people-side map (#159, ADR-0019). Neither changes the shape this ADR settled; both are things it
predicted — "one more call on this port rather than a new port" — arriving.

**`AccountStanding` gains `Note`.** Mastodon's `Relationship` carries `note`, a private line the profile's own account
keeps against another one that nobody else ever sees, and it has been arriving on every relationship call this client
makes and being dropped on the floor by `AccountWire.ToStanding`. It belongs here rather than on `Account` for the same
reason the rest of a standing does: it is a fact about the pair, not about the person — two profiles reading the same
account read different notes — and it arrives on the very payload the rest of the record is built from, so it costs
nothing.

Only the **read** half. Writing a note (`POST /accounts/:id/note`) is one endpoint and would be cheap, and it was
weighed on the map and refused: the workflow that wants it was reasoned into existence rather than observed, and it
loses to gaps a reader actually falls into. Note that Mastonet's own doc comment on `Relationship.Note` is wrong — it
says "this user's profile bio", which is a different field and, since ADR-0019, one this client now reads for real and
draws a few rows away.

**Familiar followers is the fifth call on this port.** `GET /accounts/familiar_followers` answers which of the accounts
this profile follows also follow a given one, and it lands here by this ADR's own test: it is the relationship family,
reached the same way, wanted by the same screen. It is **not** in Mastonet 3.1.3, so it is a raw batched `GET` using
the repeated-`id[]` array pattern `/accounts/relationships` already uses; and it needs no record of its own, because
the endpoint answers accounts and one account's worth of them is a list of them.

What it does need is a **nullable** answer, and for the same reason `AccountStanding` is nullable: the account screen
asks for it last, so a rate limit that stops it leaves the whole screen standing and only that row missing — and null
is what lets that row say "not asked" rather than "nobody in common". That distinction is this ADR's, applied one level
further out.

## Amendment: a resolution may travel to a read, and never to a write (#184)

This ADR's "a user types an address; every endpoint takes an id; so an address is looked up first" was written about
one call at a time, and stayed right while a screen made one. The account screen makes four (ADR-0019), and three of
them were independently resolving the same address — the arrival was seven calls, of which three were the one
`SearchAccounts(resolve: true)`. The follows browser and the account refresh did the plainer version of the same
thing: each held an `Account` whose id it had already used, converted it back into an `AccountAddress`, and paid a
call to arrive at the id again.

**A resolved account reaches a port as one value, `NamedAccount`.** It carries the address always and the instance's
own id where whoever is asking has already paid to learn one. It is built in exactly two ways: from an
`AccountAddress` alone, or from an `Account` already read, which supplies both halves off the one record. There is
deliberately no constructor taking an address and an id separately — an address naming one account beside an id naming
another is the failure this value exists to make unrepresentable, and it is the failure a loose `string accountId`
beside an address would leave available.

**The rule that decides which calls take one: a read may be handed a resolution; the call that produces the resolution
does not take one; no write takes one.** So `Timeline.By` and `Timeline.Pinned` take a `NamedAccount` — one factory
each rather than an overload per argument type, two ways to say it being two chances to say the wasteful one — and
`IAccountRelationships.List` takes a `NamedAccount?`, null still meaning "my own lists" and still costing the
current-user call, which is the shorter route to an id rather than a lookup worth avoiding. `Show` keeps its
`AccountAddress`: it is where an arrival's resolution comes from, and the only caller that could hand it a
pre-resolved account is refresh. Leaving its parameter an address is what makes that prohibition structural rather
than remembered. `Set` keeps its `AccountAddress` too — every tie goes on paying for its own lookup — and `Answer` is
untouched, taking an id off a list this client has just printed.

**Reuse is allowed only while it rides on an `Account` the reader is looking at.** The original ADR's warning still
holds: an id cached across enquiries is a way to act on the wrong account after somebody moves instances. Nothing here
is cached. The shell's account enquiry hands its three following reads the resolution `Show` answered with a moment
earlier and never one the caller arrived holding, so a refresh — the one command meaning "check this is still true" —
is not the one command that cannot correct a wrong id.

### Consequences

Opening an account screen is five calls where it was seven, one of them a lookup: the account, its timeline, its
pinned run, its familiar followers, and the single resolving search that fed the last three. Opening the follows
browser from that screen makes no lookup at all. Refreshing still makes one, by design.

`TimelineReader` now has two paths to an account id, and both must stay correct: the CLI has no resolved account to
give, so a `Timeline` naming an account with no id resolves it exactly as before. The alternatives weighed and refused
were a per-enquiry cache inside the adapter — every core port is a singleton and an enquiry is a shell concept the
core knows nothing about, so what is left is a time-boxed cache, which is the cache this ADR already refused — and an
optional id beside the address on `Timeline`, which is the two fields that can disagree, with the adapter left to
decide which it believes.

The four calls of the arrival still run one after another. Their ordering is load-bearing — the pinned run is read
before familiar followers so that content takes the better odds against a rate limit (ADR-0019) — and running them in
parallel is a separate decision, not a consequence of this one.

## Amendment: which listing surface spends a relationships call, and the three reasons one might not (#204)

The TUI has four screens that merely *list* accounts, and since #198 all four draw the same **Account block**. Only
one of them — the follow list — asked the instance where the reader stood with the people on it. Follow requests and
search's accounts run drew the block with no suffix at all, and honestly so: neither the pending-requests endpoint nor
the search endpoint sends a relationship, and a screen that did not ask draws nothing rather than "no tie". But
"honest" is not the same as "right", and on follow requests in particular, whether you already follow the person
asking to follow you is plausibly the most useful thing on the row.

**Both now ask, at one batched `Standing` call per page each.** Follow requests asks inside its destination's own
read, which is the lambda the arrival already runs — so a refresh re-asks for free and the shared arrival shape is
unchanged. The list is read to at most 40 in a single read with no paging and the endpoint is chunked at 80 ids
elsewhere, so it is one call and never more. Search asks after the find and before it paints, two calls under the one
enquiry, so the screen paints once with the standings in place rather than decorating a moment later.

**The rule this generalises to, and the two reasons a listing screen might not spend the call.** A screen that lists
accounts asks for a standing unless it has a reason not to, which leaves three states a listing surface can be in:

- **It asks** — the follow list, follow requests, and search's accounts run. The default, and what a new listing
  screen inherits without being added to a list.
- **Asking cannot help it** — **Discover**, and only Discover. Mastodon's suggestion sources exclude accounts already
  followed, dismissed or blocked, so a fetched standing is blank on every row by construction. ADR-0019 settled this
  and it is not reopened here.
- **There is nothing to decorate** — an empty run. This costs no guard at any call site: `Standing` answers an empty
  ask with its own input before it creates a client, so a hashtag-only search and a page with nobody waiting on it
  each pay nothing by the port's own rule rather than by one three callers remember.

**`Standing`'s signature is untouched and `Implied` keeps its three cases.** A pending-requests list implies a
*negative* — "they follow you" is false by definition of a request still waiting — and the compact standing only ever
adds words for positives, so `Implied.Nothing` already draws the right row there: "following" and "asked" survive,
"follows you" never appears, "blocked" and "muted" survive. What changed is `Nothing`'s *definition*, which read as
"somebody else's list, where a reader stands in no particular relation to anyone on it" and was plainly false on a
requests list, where the reader stands in a very particular relation to everybody. It now means **nothing to
suppress** — one sentence that is honest for a search run, for somebody else's follows and for a page of requests
alike. A fourth case was weighed and refused: only a claim a list makes of everybody on it is worth suppressing, and a
negative is not one.

**The silence rule is unchanged and now has one place to live.** A refused or rate-limited standing answers with
nothing rather than throwing, and `ShellPorts.StoodOrSilent` is where the `?? people` that turns that into silent rows
is written — once, for all three call sites, rather than at each of them. It sets no fetch's `StoppedBy` and raises no
notice: the rows draw silent and the list stands.

**One bug fell out of writing that down.** `Standing` promised silence on a refusal and did not deliver it: its catch
list was copied from `FamiliarFollowers`, which reaches the endpoint by a raw `GET` and so sees a refusal as an
`HttpRequestException` — but `Standing` goes through Mastonet, which turns the same refusal into a
`ServerErrorException` that nothing caught. It escaped the port, and `Enquiry` does not catch it either, since it is
not a `WoolyException`. That was already true of the follow list; #204 would have spread it to two more screens, so it
is fixed here rather than left for them to inherit. The catch is a call-path difference and not a promise one — a
refusal is the instance declining to answer however it is spelled.

### Consequences

Arriving at follow requests with anybody waiting is two calls where it was one, and a search that turned up accounts
is two where it was one. Both are bounded at one extra call per page and neither is on a hot path — a reader arrives
at requests to answer them and runs a search deliberately.

**The CLI is deliberately left alone.** `account requests list` and `search` have the same gap, and adding a standing
to their JSON would populate a document field that is currently always null — a machine-readable-output change to
argue under ADR-0007, not a consequence of a TUI rendering decision. It is worth its own issue.
