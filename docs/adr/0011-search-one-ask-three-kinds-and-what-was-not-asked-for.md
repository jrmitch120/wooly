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
