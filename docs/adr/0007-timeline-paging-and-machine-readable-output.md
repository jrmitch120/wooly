# Paging is the reader's business, a fetch cut short still reports what it got, and `--json` is an envelope

Timeline reading is the first feature that fetches more than one thing, and three decisions fall out of that. They are recorded here because every later ticket that reads a list — notifications, searches, an account's posts — inherits all three.

**A caller asks for posts, not for pages.** `ITimelineReader.Read` takes a count and answers with that many posts; the `max_id` cursor, the 40-post ceiling an instance serves, and the number of calls it takes are inside the adapter. The alternative — handing a page and a cursor back for the caller to carry — would put the same loop in the CLI, the TUI, and every later list command, and each copy would get the end-of-timeline condition slightly differently. That condition is the instance's own `next` link, not the size of the page it sent: an instance drops posts a filter hid from a page it had already counted, so a page can arrive short of what was asked for and still have more behind it, and treating short as final would report a fetch as complete with most of it missing. Only where no next page is named does a short page mean the end. One thing overrides the link either way — a page with nothing on it, which no further ask can improve on, and which is what stops an instance that names a next page forever from being followed forever.

**A rate limit part way through is a partial result, not a lost one.** The reader stops at the limit and returns the posts already in hand alongside the limit that stopped it (`Fetch<Post>.StoppedBy` — `TimelineFetch` when this was written; see ADR-0010's amendment), rather than throwing and discarding them. What makes that worth the extra type is the case it separates: a fetch stopped on its first page holds no posts, and a caller unable to tell that from a timeline with nothing on it would report an empty timeline to a user who has a full one. The limit is carried as the `RateLimitedException` itself so the CLI can throw the instance's own answer rather than a second-hand copy, which is also what keeps ADR-0006's promise that no command writes its own error text: the command prints the posts it got to stdout and throws, and the one handler puts the message on stderr and exit `5` on the process. Nothing waits, per ADR-0006 — the TUI is still free to.

**`--json` is an object, not an array of posts.** A bare array would be the more obvious thing to pipe into `jq`, and it is exactly what cannot say the one thing above: `[]` from a rate limit on the first page and `[]` from a quiet timeline are the same two characters. So the document carries `posts` alongside `complete` and, when something cut the fetch short, `rateLimit`. Under a pipe the exit code is gone by the time the JSON is parsed, which is why the fact has to be in the output as well as in the code. Its field names are written out at the serialization boundary rather than derived from the domain records, so that renaming a domain property cannot silently rename somebody's `jq` filter, and they are this project's vocabulary — `boosts`, `favorites` — not the API's.

One further thing follows from a tag timeline being addressed by path rather than by query: **a hashtag is one word, checked before it is asked for.** A value with a slash in it does not fetch a tag timeline at all — it walks to whatever endpoint the slashes reach, and the answer comes back rendered as posts. So the rule lives in `Hashtag`, next to `InstanceDomain` and for the same reason: the argument parser asks it to answer a typo where the user can see it, the domain asks it again because a `Timeline.Tag` that cannot be built wrongly is what lets the value be interpolated into a path at all, and neither can start saying something the other does not.

The reader's own tests sit at the `HttpMessageHandler` seam. That is not a new exception: ADR-0005 already puts a thin adapter's tests there, and paging and the rate limit are both below `IMastodonClient` for the reason ADR-0006 gives — the `max_id` the second call carries and the `429` the first one gets are only observable as HTTP.

## Consequences

The 40-post page size is a constant in the adapter rather than something a caller can ask for. A later ticket that wants to stream a timeline as it arrives, rather than collect it, reopens the first decision above: an `IAsyncEnumerable<Post>` would let a TUI paint the first page while the second is in flight, at the cost of the completeness signal no longer being one value returned at the end.

`--limit` is a count of posts and says nothing about calls, so a large one is quietly several requests against the instance's quota. Nothing yet caps it, on the grounds that the rate limit is now something the client reports rather than something it can walk into unknowingly.

## Amendment: a fetch carries where to read on from (spec #460, ticket #469)

The first decision above ends "no caller sees a page or a cursor", and for a caller that asks once and stops — the CLI,
and every TUI screen until now — that held. The TUI now **reads on**: as the reader nears the end of a list it asks the
instance for what stands behind the last thing the list holds, and adds it at the bottom (GLOSSARY.md). Something has
to say where "behind" starts, and the caller is the only thing that still has the list in hand when the next read is
put.

**So a `Fetch<T>` carries an `Onward` unless the list is whole, and a caller can only hand it back.** It is opaque:
constructed inside `Wooly.Core`, compared by value, and read by nothing outside it. A reader port gains a second read
that takes one — `ITimelineReader.ReadOn(profile, timeline, from, limit)` is the first — still asking for a count of
items, never a page size or a `max_id`. Everything this ADR kept in the adapter stays there: the page size, the `max_id`
the next call carries, the link header as the authority on whether there is a next page, the last item's id as the
fallback where an instance sends none, the empty page that stops the loop whatever the instance names, and the rate
limit as a partial result. `PagedReading.Collect` takes the position to start from and hands back the one to go on from,
so it is still the one copy of the loop.

**Whole is the instance's call, said by the same end condition as before.** A list is whole when no next page is named
or a page arrives empty; a short page that still names a next page is not the end, for the filtered-posts reason given
above. `Fetch.IsWhole` is exactly `Onward is null`. A fetch a rate limit stopped is never whole: it keeps the position the
refused page would have started at, which is the top of the list where the limit came on the first page — so the next
read resumes there rather than skipping what it never got, and a list stopped on its first page is never mistaken for a
list with nothing on it, which was this ADR's second decision. A fetch that stopped at what the caller asked for, with
the instance still naming more, carries where the instance said the next page starts.

**Re-reading from the top was the alternative, and it is what the browsed follow list and a group's actors already
do** (#180, #455): ask again to a longer limit and keep only the tail. It needs no cursor, which is why it was chosen
then. It does not survive being every list's way of reading on. Read *n* re-requests every page before it, so the calls
grow with the square of how far the reader has read; and on a live timeline anything arriving at the top in the meantime
shifts the offset, so the tail kept repeats posts already shown. Those two screens move onto `Onward` with the rest
(#467).

What a caller still cannot do is the thing this ADR was guarding against: hold a page, read a cursor, or carry its own
copy of the end condition. The CLI hands nothing back, and `--limit` reads exactly as it did.

## Amendment: a tag set of several runs keeps nothing on a rate limit (ADR-0033)

A rate limit part way through is still a partial result for every list read as one, a tag set of four tags or fewer
included. A tag set read as several runs of four, merged (`MergedReading`), keeps nothing when the limit refuses any of
its runs, since a merged list missing a run looks whole while missing posts from its middle; ADR-0033 records why.
