# One ask however narrow, what was not asked for is absent, and a refusal has nothing to hand back

Search (#25) adds a single `search "<query>" [--type accounts|hashtags|posts] [--json]` covering all three kinds of
result. It is the third thing this client reads from an instance, after timelines (ADR-0007) and notifications
(ADR-0010), and the first one that is not a list at all — which is where most of the decisions below come from.

**One command, because a searcher rarely knows which of the three they are looking for.** A half-remembered word is as
likely to be somebody's handle as a tag or a phrase in a post, and three commands would make the user guess before the
instance does. Mastodon agrees: `/api/v2/search` answers with `accounts`, `statuses` and `hashtags` together, in one
call, and `--type` is how a user who does know says so.

**`--type` narrows what came back, not what was asked for.** Mastodon's endpoint takes a `type` parameter, and Mastonet
3.1.3's `Search(q, resolve)` does not send one — nor a limit, nor an offset. ADR-0001 keeps this client on Mastonet, and
the alternative here is to build the request path and query string by hand for one command, next to a library doing it
for every other. So the instance is asked for everything however narrow the query is, and `SearchResults.Matching` keeps
the kind wanted. The narrowing lives in the domain rather than in the command for the reason ADR-0007 gives about paging
loops: a TUI search prompt is the second caller, and two callers each deciding what `--type` means is how the same flag
comes to mean two things.

**A kind that was not asked for is absent; a kind that found nothing is empty.** `--json` writes no `posts` field at all
after `--type accounts`, and writes `"posts": []` for a search that looked and found none. ADR-0007 made this argument
about a whole timeline — `[]` from a rate limit and `[]` from a quiet timeline are the same two characters — and
`--type` sharpens it: a script told `"posts": []` by a search that never looked for posts would report that nothing it
searched for had been posted. The same distinction is carried in the domain rather than invented at the serialization
boundary, because the report a person reads needs it too: `search cats --type accounts` finding nothing says "No
accounts matching 'cats'", which is a smaller claim than "Nothing matching 'cats'" and the only true one.

**A search is one call, so a rate limit is a failure rather than a partial answer.** There is no `SearchFetch` beside
`TimelineFetch` and `NotificationFetch` (one `Fetch<T>` since ADR-0010's amendment), and no `complete` field in the
JSON. Those exist because a paged read can be
stopped half way and still be holding most of what was asked for; a search that is refused is holding nothing, and an
envelope saying `complete: false` next to three empty lists would be a more elaborate way of saying what the exit code
and the message on stderr already say (ADR-0006). The command therefore catches nothing and lets the limit reach the one
handler.

**Asking is resolving.** The search is sent with `resolve=true`, so a query that is a handle or a web address makes the
instance go and fetch what it has not met. Without it, pasting the address of a post you are looking at in a browser
finds nothing at all on an instance that has never federated it — which reads as a broken client rather than as a
deliberate narrowness. The cost is that such a search is slower, and that looking something up is itself an act that
leaves a copy of it on your instance.

Two new domain values fall out. **An account becomes a thing this project has a record for**, rather than the string a
post carries: `Accounts.Account` holds the address, the display name and the three counts an instance sends. It names
the first two with the same two words a post names them with — `account` and `author`, in the domain and in the JSON —
so that one `jq` filter reads who somebody is wherever they turn up. **A hashtag result carries how much use it has
had**, because a search for a word finds several near-identical tags and the usage is what says which one people are
actually posting to; and its name is bare, put through the same `Hashtag.Bare` rule the tag timeline is read by, so a
tag a search turned up is one `timeline tag` will take.

## Consequences

There is no `--limit` on `search`, and no paging. What comes back is whatever the endpoint serves by default — 20 of
each kind on a stock Mastodon — and this client cannot ask for the next 20, because Mastonet sends neither `limit` nor
`offset`. A ticket that wants either reopens the second decision above, and has two ways to go: contribute the
parameters upstream, or drop this one adapter below `IMastodonClient` to the `HttpClient` every Mastonet call already
runs through. The second is cheaper and costs ADR-0001's promise that one library speaks to the instance.

`--type accounts` costs the instance exactly what a full search costs it. The flag saves the user's screen, not the
instance's work, and a rate limit is reached at the same rate either way.

ADR-0010 predicted that a third list command would be where `TimelineSettings` and the notification list's copy of it
were worth collapsing into a shared base. This is the third read command and it is not that third list: it takes no
`--limit`, does no paging, and has no fetch to report as incomplete, so it shares nothing with them but `--json`. The
collapse those two ADRs keep deferring is still waiting for a genuinely paged third list — an account's posts, or its
followers (#26).

`Account` says nothing about what the profile's own account has done about it — following, blocked, muted. Relationship
management (#26) is where that arrives, and it should extend this record rather than introduce a second account type:
two of them is how a search result and a followers list come to describe the same account differently.

## Amendment: a search of the profile's follows does not resolve, and finds nobody where refused (map #313, ticket #322)

The mention list in compose asks a second, narrower search of `IInstanceSearch`: `FindFollowed`, which is Mastodon's
`GET /api/v1/accounts/search` with `following=true`, five at most, through Mastonet's own `SearchAccounts(q, limit,
resolveNonLocalAccouns, onlyFollowing)` — so this one *does* send a limit, and stays on Mastonet.

**It is not resolving.** "Asking is resolving" above is about a person who pasted an address and means "find me this".
Somebody the profile follows is somebody its instance has already met, so there is nothing to go and fetch, and a
lookup that leaves a copy of an account on the instance is not something a suggestion should do behind the reader's
back while they type.

**A refusal other than the rate limit finds nobody.** The second decision above lets a refused search reach the one
handler, because a user who typed `search` asked for an answer and is owed the reason there is none. Nobody asked for a
suggestion, so an instance that will not search follows — a 403 for the token, a 422 for a parameter it does not take —
answers with no one, as #204 settled for the standing a list is decorated with. A rate limit is still raised, and the
shell drops it silently; it is raised so that a caller can still tell it apart.

## Amendment: search leaves Mastonet, a page is a limit and an offset, and an offset needs a kind (spec #598, ticket #599)

The Consequences above named two ways out of the missing `limit` and `offset`, and this takes the cheaper one: `Find`
no longer goes through `IMastodonClient` but calls `/api/v2/search` itself, through `RawMastodonCall` over the
`HttpClient` every Mastonet call already runs through — so it is retried, rate-limit-checked and refused exactly as
before, and nothing above the port can tell. It is the same way out ADR-0019 (suggestions), ADR-0032 (grouped
notifications) and ADR-0033 (tag sets) took for their own gaps. ADR-0001's promise that one library speaks to the
instance was already spent on those; this is one more endpoint, not a new kind of exception to it.

**A page is a limit and an offset, and the offset is a fact about a narrowed search only.** `SearchQuery` carries a
`SearchPage`: how many of each kind (1 to 40, Mastodon's cap per kind) and how many to skip, each sent only where it was
said, so a search asked nothing about a page is sent exactly as Mastonet sent it and is served the instance's own
default. The instance ignores `offset` without `type` and answers with the first page again — which a script paging
through would take for the next one — so a page with an offset is refused for a search of every kind, in the domain
(`SearchQuery.For`) and, before anything is asked, by the CLI as a usage error (ADR-0006). `search --limit` and
`search --type … --offset` are the CLI's spelling of it.

**`--type` now narrows what is asked for as well as what came back.** A narrowed search names its kind to the instance
(`accounts`, `hashtags`, or `statuses`, which is the instance's word for posts), which is what lets it page, and spares
the instance the two kinds it would have worked out for nothing — the Consequences paragraph on `--type` costing the
instance as much as a full search no longer holds. `SearchResults.Matching` still drops the kinds not asked for, so the
absent-versus-empty rule does not lean on the instance leaving them out.

**Past the first page the instance stops resolving, without saying so.** That is harmless: the first page already went
and fetched the handle or address it was asked about, so a later page has nothing left to resolve.

**A full page of hashtags can be one longer than the limit.** The instance puts an exact match ahead of the hashtags it
found, and the answer may then hold 41. The adapter keeps it — it is the tag most likely meant — so whatever judges
whether a page came back full compares against the limit asked, not the count returned.

**A refusal is said in the instance's words.** Mastonet raised one with the instance's `error`, which the CLI printed as
it was; the raw call raises `SearchRefusedException` with the same words, rather than a bare failed status that would be
reported as a defect. An instance that failed to answer — a `5xx` — is no refusal, and raises `InstanceFailedException`,
as the trends (ADR-0036) and the link timeline do: the three raw reads #598 added say a failure one way. A rate limit
is still raised as itself, and is still a failure rather than a partial answer.

`FindFollowed` is untouched, and stays on Mastonet: its endpoint has everything it needs there.
