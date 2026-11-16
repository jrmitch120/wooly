# Trends are read through a port of their own over HttpClient, and a trending link opens as the posts sharing it

Search and trends (#598) put what an instance ranks as trending — its **Trend**s — under the empty search prompt in the
TUI and behind a new `trends` command in the CLI (#601). Three kinds trend: hashtags, posts and **Trending link**s,
each read from an endpoint of its own (`/api/v1/trends/tags`, `/statuses`, `/links`), each paged by `limit` and
`offset`.

**Trends are a port of their own, `ITrends`, not a part of `IInstanceSearch`.** A trend is not the answer to a
question: nothing is typed, there is no query to narrow, and the three kinds are three calls rather than one call
answering with three lists, which is the whole shape `SearchResults` exists for. Folding them into search would give
the search port a method that ignores its query, and its fakes a second kind of answer to script. A port of their own
keeps each fake telling one story, and is what the CLI's `trends` and the TUI's prompt share, so a flag and a key
cannot come to read trends two ways (ADR-0005). The port reads one kind a page at a time — `Hashtags`, `Posts`,
`Links`, each taking a `TrendPage` — because its two callers want different pages of each: the prompt ten hashtags,
ten links and three posts, a narrowed screen the instance's maximum, the CLI whatever `--limit` says.

**All three are read raw, over the `HttpClient` every Mastonet call runs through.** Mastonet 3.1.3 has no trending
links at all, keeps its trending posts off `IMastodonClient`, and reads paging only from `max_id` links, where every
trends endpoint pages by `offset`. One of the three would have to be raw whatever was decided; reading the other two
through Mastonet would put one port over two ways of asking for a page, which is two places for `offset` to come to
mean different things. So `InstanceTrends` asks all three through `RawMastodonCall`, the one place ADR-0011's way out
of a library gap is taken — retried, rate-limit-checked and authorized exactly as a Mastonet call is. Hashtags and
posts are still read as Mastonet's own entities and cross through `HashtagWire` and `PostWire`, so a trending hashtag is
a hashtag and a trending post a post however they arrived; only the link has a wire record of its own
(`TrendingLinkWire`), Mastonet's `Card` having no `history`.

**Nothing trending is an answer, not a failure.** An instance with trends turned off answers each endpoint with `[]`,
and so does one where nothing has trended yet. Both read as "nothing trending": `trends` reports it and exits 0, and an
empty Search says it under the prompt. A refusal is different, and is raised as a `TrendsRefusedException` carrying
the instance's own words — a `WoolyException`, because that is what the TUI's enquiry says on the status row, and any
other exception reaching it is said by nobody. An instance that failed to answer — a `5xx` — raises
`InstanceFailedException` instead, as search and the link timeline (which raises `TimelineRefusedException` for a
refusal other than its not-trending 404) do too. A rate limit is raised as it is anywhere: each read is one call, so,
as with a search (ADR-0011), there is no partial answer to hand back.

**A page the instance would cut short is refused before it is asked.** Mastodon serves at most 20 hashtags or links and
40 posts a page, and quietly serves its maximum for a larger limit. A script asking for 50 would read a page of 20 as
the end of the ranking, so `TrendPage.Rejection` turns such a limit down, and the CLI reports it as a usage error
(ADR-0006). With every kind asked at once the smallest maximum binds. `--offset` needs no `--type`, unlike search's:
every trends endpoint pages by offset, so it is sent with the limit to each kind asked.

**A trending link opens as the posts sharing it, not as the article.** What people are saying about a link is why it
trends, and the article is a page a browser already shows better than a terminal can. So opening one reads Mastodon's
link timeline (`/api/v1/timelines/link?url=…`, #603) and the article is one key further, in the browser (#607). The
link timeline answers only for a link that is trending and allowed, and a 404 otherwise, so a link that has stopped
trending has no posts behind it: the reader is told so on the status row, with the article still offered.

**A Trending link is a new domain value** — its address, title, description, site, and how many people shared it. "How
many" adds up the accounts of every day the instance reported, as a hashtag's `RecentAccounts` does, so a trending
link and a trending hashtag count their people the same way. A blank description or site is absent rather than an
empty row, and a card with no address is dropped, there being nothing to open.

## Consequences

ADR-0001's promise that one library speaks to the instance loses another endpoint family, as it did to grouped
notifications (ADR-0032) and tag sets (ADR-0033). The cost stays bounded only while `RawMastodonCall` is the one place
a hand-rolled call is built.

`TrendPage` is a value of its own rather than search's `SearchPage` (#599). The two page differently — a
search's offset means something only with one kind, a trend's always — and sharing one value would make one of the two
rules a special case of the other.

The usage counts sum every day the instance sends (seven, on Mastodon), which is not the web app's "in the past two
days". A front end wanting the web app's figure needs the days themselves, which neither `Hashtag` nor `TrendingLink`
carries today.
