# Reading and narrowing the accounts a profile follows

Research for [#162](https://github.com/jrmitch120/wooly/issues/162), part of the people/discovery map
([#159](https://github.com/jrmitch120/wooly/issues/159)). What an instance actually offers a following browser, and
what Mastonet 3.1.3 hands us of it.

Sources are pinned: Mastodon at
[`d79f2c5`](https://github.com/mastodon/mastodon/tree/d79f2c5a709e6cff12ed65452fa1526dacb1dacd), Mastonet at
[`cc6e00a`](https://github.com/glacasa/Mastonet/tree/cc6e00af72a1f583fe769a167962b26bbf1bdc9f). Mastonet's shipped
3.1.3 binary was checked against that tree; the four calls below match (no `3.1.3` tag exists in the repo, so the
binary is the authority and it agrees).

## The short answers

| Question | Answer |
| --- | --- |
| Does `GET /api/v1/accounts/search` take `following=true`? | **Yes** — but it narrows to **the authenticated user's own** following set, never an arbitrary profile's. |
| Is `Standing` sent on a search result? | **No.** |
| Is `Standing` sent on `following` / `followers`? | **No.** Same serializer, same silence. |
| Does `following` / `followers` page by Link header? | **Yes**, on a cursor that is a **follow id**, not an account id. |
| Does `accounts/search` page by Link header? | **No.** `offset` only, and Mastonet does not expose it. |
| Does Wooly's `PagedReading` already handle the list shape? | **Yes**, correctly, including the no-fallback-cursor case. |

---

## 1. `GET /api/v1/accounts/search`

### Parameters

Per the [docs](https://docs.joinmastodon.org/methods/accounts/#search) and
[`Api::V1::Accounts::SearchController`](https://github.com/mastodon/mastodon/blob/d79f2c5a709e6cff12ed65452fa1526dacb1dacd/app/controllers/api/v1/accounts/search_controller.rb):

| Param | Type | Default | Max | Notes |
| --- | --- | --- | --- | --- |
| `q` | String | — | — | Required. |
| `limit` | Integer | 40 | **80** | |
| `offset` | Integer | 0 | — | Skip the first n. The **only** paging this endpoint has. |
| `resolve` | Boolean | false | — | WebFinger lookup; use when `q` is an exact address. |
| `following` | Boolean | false | — | Limit to accounts *the authenticated user* follows. |

The 80 is not stated in the controller — it falls out of `limit_param`, which caps at twice the default when no
explicit max is given, and `DEFAULT_ACCOUNTS_LIMIT` is 40
([`Api::BaseController`](https://github.com/mastodon/mastodon/blob/d79f2c5a709e6cff12ed65452fa1526dacb1dacd/app/controllers/api/base_controller.rb)):

```ruby
DEFAULT_ACCOUNTS_LIMIT = 40

def limit_param(default_limit, max_limit = nil)
  return default_limit unless params[:limit]
  [params[:limit].to_i.abs, max_limit || (default_limit * 2)].min
end
```

The endpoint is authenticated — `doorkeeper_authorize! :read, :'read:accounts'` plus `require_user!`. There is no
anonymous form of it.

### The catch on `following=true` — it is scoped to *you*, not to the profile being browsed

This is the single most consequential fact for the browser's shape. The controller passes `current_account`, and
nothing in the request can name a different one:

```ruby
AccountSearchService.new.call(
  params[:q], current_account,
  limit: limit_param(DEFAULT_ACCOUNTS_LIMIT),
  resolve: truthy_param?(:resolve),
  following: truthy_param?(:following),
  offset: params[:offset]
)
```

`following` then resolves to the signed-in account's own follows, on both backends
([`AccountSearchService`](https://github.com/mastodon/mastodon/blob/d79f2c5a709e6cff12ed65452fa1526dacb1dacd/app/services/account_search_service.rb)):

```ruby
def following_ids
  @following_ids ||= @account.active_relationships.pluck(:target_account_id) + [@account.id]
end
```

and, without Elasticsearch, in
[`Account::Search`](https://github.com/mastodon/mastodon/blob/d79f2c5a709e6cff12ed65452fa1526dacb1dacd/app/models/concerns/account/search.rb):

```sql
WITH first_degree AS (
  SELECT target_account_id FROM follows WHERE account_id = :id
  UNION ALL SELECT :id
)
... WHERE accounts.id IN (SELECT * FROM first_degree)
```

**So**: server-side narrowing is available for `wooly account following` with no argument (the profile's own list) and
for the TUI's own-following screen. It is **not** available for `account following alice@example.social`. A browser
that serves both sides has to narrow client-side for the other-profile case regardless, which argues for narrowing
client-side everywhere rather than having the screen behave differently depending on whose list it is.

Note also that `following_ids` and `first_degree` both include **the user's own account id**. A `following=true`
search that matches your own display name returns you, though you do not follow yourself. A browser filtering a list
"of accounts I follow" would show a row that is not in that list.

### What it matches against

Two backends, and they differ.

**With Elasticsearch** — `/api/v1/accounts/search` does *not* pass `use_searchable_text`, so it builds the
`AutocompleteQueryBuilder`, which matches **`username` and `display_name` only**:

```ruby
multi_match: { query: @query, type: 'most_fields', fields: %w(username username.*) },
multi_match: { query: @query, type: 'most_fields', fields: %w(display_name display_name.*) },
```

(The `FullQueryBuilder`, which *also* matches a `text` field carrying the bio, is reached only when
`use_searchable_text: true` — and the only caller that passes it is `SearchService`, i.e. `/api/v2/search`. See
[`search_service.rb`](https://github.com/mastodon/mastodon/blob/d79f2c5a709e6cff12ed65452fa1526dacb1dacd/app/services/search_service.rb).)

**Without Elasticsearch** — the Postgres fallback, a `tsvector` over three columns with weights:

```sql
setweight(to_tsvector('simple', accounts.display_name), 'A') ||
setweight(to_tsvector('simple', accounts.username), 'B') ||
setweight(to_tsvector('simple', coalesce(accounts.domain, '')), 'C')
```

**Bio is never matched by `/api/v1/accounts/search`, on either backend.** Handle, display name, and (DB path only)
instance domain.

Worth knowing for a type-to-narrow field: the DB query is a **prefix** match on whole words —
`generate_query_for_search` appends `:*` — so typing `jo` finds `john` but never `bjorn`. That is not the substring
match a user types-to-filter expects from a local list. Client-side narrowing over an already-fetched list can be
substring; server-side narrowing cannot.

There is also a floor: with no signed-in account a query under `MIN_QUERY_LENGTH = 3` returns nothing. Since this
endpoint always has one, that floor does not bite here — but it does colour how few characters are worth sending.

### What it returns

A **full `Account`** — `render json: @accounts, each_serializer: REST::AccountSerializer`, the same serializer the
followers and following lists use. Not a thinner shape. That means bio (`note`), custom `fields`, `locked`, `bot`,
`created_at` and the three counts all arrive, which is relevant to
[#161](https://github.com/jrmitch120/wooly/issues/161) as well.

### Paging

**No Link header.** The controller has no `after_action :insert_pagination_headers`, unlike the followers and
following controllers, which do. `offset` is the whole of it, and it flows straight through to `LIMIT :limit OFFSET
:offset` (DB) or `.offset(offset)` (ES). One further wrinkle: `exact_match` is only computed when `offset.zero?`, so
the resolved WebFinger hit appears on the first page only.

---

## 2. `GET /api/v1/accounts/:id/following`

### Parameters

| Param | Type | Default | Max |
| --- | --- | --- | --- |
| `limit` | Integer | 40 | **80** |
| `max_id` / `since_id` / `min_id` | String | — | — |

The docs mark the three id params **"Internal parameter. Use HTTP `Link` header for pagination."** and add: *"Because
Follow IDs are generally not exposed via any API responses, you will have to parse the HTTP `Link` header to load
older or newer results."*

### Paging: Link header, on a follow id

Confirmed in
[`FollowingAccountsController`](https://github.com/mastodon/mastodon/blob/d79f2c5a709e6cff12ed65452fa1526dacb1dacd/app/controllers/api/v1/accounts/following_accounts_controller.rb):

```ruby
def paginated_follows
  Follow.where(account: @account).paginate_by_max_id(
    limit_param(DEFAULT_ACCOUNTS_LIMIT), params[:max_id], params[:since_id])
end

def pagination_max_id
  @accounts.last.passive_relationships.first.id
end

def records_continue?
  @accounts.size == limit_param(DEFAULT_ACCOUNTS_LIMIT)
end
```

`pagination_max_id` is a `Follow` row's id, in a different id space from the `Account` ids in the body. **Wooly's
existing comment is exactly right** and worth not re-deriving later — see `idOf: null` in
`src/Wooly.Core/Relationships/AccountRelationships.cs`.

Two consequences the loop has to respect, and does:

1. **A next page exists only when the page came back full** (`records_continue?`). No Link header means the end.
2. **A page can be shortened after the count.** `load_accounts` applies `not_excluded_by_account(current_account)`
   before the size check, so accounts you have blocked, that have blocked you, or that you have muted are dropped
   (`excluded_from_timeline_account_ids` in `app/models/account.rb`) and `records_continue?` then reads false. A
   profile that follows people you have muted can have its following list end early, server-side. Nothing a client can
   fix; worth knowing before someone reports it as a Wooly bug.

### Visibility

`hide_results?` returns an **empty array, HTTP 200** — not a 404 or 403 — when the account has "hide your social
graph" set (`hides_following?`), is unavailable, or has blocked you. A following browser therefore cannot tell
"follows nobody" from "won't say" on the response alone. The `hide_collections` attribute *is* on the serialized
account, so the browser could read it off the account it drilled from and say so, but it takes a deliberate check.

---

## 3. Is `Standing` sent on either? No — on neither.

[`REST::AccountSerializer`](https://github.com/mastodon/mastodon/blob/d79f2c5a709e6cff12ed65452fa1526dacb1dacd/app/serializers/rest/account_serializer.rb)
carries no relationship state at all:

```ruby
attributes :id, :username, :acct, :display_name, :locked, :bot, :discoverable, :indexable, :group, :created_at,
           :note, :url, :uri, :avatar, :avatar_static, :avatar_description, :header, :header_static,
           :header_description, :followers_count, :following_count, :statuses_count, :last_status_at,
           :hide_collections, :show_media, :show_media_replies, :show_featured
```

No `following`, no `followed_by`, no `requested`, no `blocking`, no `muting`. All three endpoints in question —
`accounts/search`, `:id/following`, `:id/followers` — render through this one serializer. Relationship state lives
only behind `GET /api/v1/accounts/relationships`.

**This confirms both claims already recorded in `CONTEXT.md` ("An instance sends this only where it is asked") and in
`Account.Standing`'s own doc comment ("a search result and a followers list say nothing about standing"). Neither
endpoint is the exception.** The existing `null`-means-not-asked design stands unchanged.

**What this costs a browser.** Showing a tie badge on rows means a second call to
`/api/v1/accounts/relationships`, which takes `id[]` repeated — Mastonet's `GetAccountRelationships(IEnumerable<string>
ids)` already does that, so one call per page of 40–80 rows, not one per row. Wooly currently only ever asks for a
single id (`AccountRelationships.Show`); the many-id overload is untouched but available. Note the practical asymmetry:
on *your own* following list every row is by definition `following: true`, so the second call buys nothing there — it
buys something only on another profile's list, or when the browser wants to show `followed_by` / `blocking` /
`muting` too.

---

## 4. `GET /api/v1/accounts/:id/followers`

Identical in every respect that matters. Same 40/80 limits, same Link-header-only paging, same `REST::AccountSerializer`,
same empty-200 hiding. The only differences in
[`FollowerAccountsController`](https://github.com/mastodon/mastodon/blob/d79f2c5a709e6cff12ed65452fa1526dacb1dacd/app/controllers/api/v1/accounts/follower_accounts_controller.rb)
are which side of the `Follow` row is queried (`Follow.where(target_account: @account)`), which association the cursor
is read off (`active_relationships` rather than `passive_relationships` — still a follow id either way), and which
privacy flag hides it (`hides_followers?`).

**There is no `following=true` equivalent for followers.** `accounts/search` narrows by who you follow and by nothing
else. Narrowing a followers list is client-side, always, for everyone.

---

## 5. What Mastonet 3.1.3 exposes

All four calls are on the `IMastodonClient` interface, so nothing here needs a raw HTTP call. Sources:
[`MastodonClient.Account.cs`](https://github.com/glacasa/Mastonet/blob/cc6e00af72a1f583fe769a167962b26bbf1bdc9f/Mastonet/MastodonClient.Account.cs),
[`MastodonClient.cs`](https://github.com/glacasa/Mastonet/blob/cc6e00af72a1f583fe769a167962b26bbf1bdc9f/Mastonet/MastodonClient.cs).

```csharp
Task<MastodonList<Account>> GetAccountFollowing(string accountId, ArrayOptions? options = null);
Task<MastodonList<Account>> GetAccountFollowers(string accountId, ArrayOptions? options = null);
Task<List<Account>>         SearchAccounts(string q, int? limit = null, bool resolveNonLocalAccouns = false,
                                           bool onlyFollowing = false);
Task<IEnumerable<Relationship>> GetAccountRelationships(IEnumerable<string> ids);
```

### `SearchAccounts` — `following=true` is one argument away

```csharp
string url = "/api/v1/accounts/search?q=" + Uri.EscapeDataString(q);
if (limit.HasValue)          url += "&limit=" + limit.Value;
if (resolveNonLocalAccouns)  url += "&resolve=true";
if (onlyFollowing)           url += "&following=true";
return Get<List<Account>>(url);
```

The shipped 3.1.3 assembly contains the literals `/api/v1/accounts/search?q=`, `&limit=`, `resolve=true` and
`&following=true`, and the parameter names `resolveNonLocalAccouns` and `onlyFollowing` — so the source above is what
we have on disk.

Two gaps:

- **No `offset`.** The only paging `accounts/search` has is the one parameter Mastonet omits. Server-side narrowing is
  therefore capped at one page of at most 80, full stop, unless we go raw.
- **Returns `List<Account>`, not `MastodonList<Account>`** — which is consistent, since the endpoint sends no Link
  header to populate one.

Wooly **already calls this**, in `src/Wooly.Core/Accounts/AccountLookup.cs`, with `Candidates = 10` and
`resolveNonLocalAccouns: true`, leaving `onlyFollowing` at its default. Turning on server-side narrowing for the
own-following case is a one-argument change to an existing, tested call path.

### `GetAccountFollowing` / `GetAccountFollowers`

```csharp
var url = $"/api/v1/accounts/{accountId}/following";
if (options != null) url += "?" + options.ToQueryString();
return GetMastodonList<Account>(url);
```

`ArrayOptions` emits `max_id`, `since_id`, `min_id`, `limit`. `GetMastodonList` parses the `Link` header into
`NextPageMaxId` / `PreviousPageSinceId` / `PreviousPageMinId` with the regex `_id=([0-9]+)`
([`BaseHttpClient`](https://github.com/glacasa/Mastonet/blob/cc6e00af72a1f583fe769a167962b26bbf1bdc9f/Mastonet/BaseHttpClient.cs)).
Numeric-only, which is fine for Mastodon's snowflake ids but would not survive a fork using non-numeric ones.

### `Search` (v2) — thin

```csharp
public Task<SearchResults> Search(string q, bool resolveNonLocalAccouns = false)
```

`q` and `resolve` only. `type`, `limit`, `offset`, `following`, `account_id`, `min_id`, `max_id` are all unavailable.
That already shapes ADR-0011 (one ask, three kinds, narrowed client-side in `SearchResults.Matching`), and it means
the v2 endpoint's own `following=true` — which exists, and matches bio via `use_searchable_text` — is out of reach
without a raw call. Note v2 search's limits are different anyway: default 20, max 40 per kind.

---

## What this means for the browser

Restating only what the facts force, not what the feature should be:

1. **Narrowing has to be client-side to work the same on both sides.** Server-side narrowing exists only for your own
   following list, matches prefixes rather than substrings, never matches bio, and caps at 80 results with no paging
   through Mastonet. A single client-side filter over a fetched `Fetch<Account>` behaves identically for your list and
   anyone else's, and matches the substring the user expects.
2. **The paging Wooly has is the right paging.** `PagedReading` with `pageSize: 80` and `idOf: null` is already exactly
   what these two endpoints want; the browser needs no new loop.
3. **Tie badges cost a call.** Not one per row — one per page, via the many-id `GetAccountRelationships` overload — and
   nothing on your own following list.
4. **"Follows nobody" and "won't say" look the same.** Both are `[]` with a 200. Distinguishing them means reading
   `hide_collections` off the account already in hand.

## Facts checked and confirmed unchanged

- `CONTEXT.md`, **Standing**: "An instance sends this only where it is asked, so an account may carry none." Correct.
- `Account.Standing` doc comment: "a search result and a followers list say nothing about standing because Mastodon
  does not send it there." Correct, and true of the following list too.
- `AccountRelationships.PageSize = 80`: correct, and it is genuinely the ceiling (`limit_param` caps at `40 * 2`).
- `AccountRelationships.Collect`, `idOf: null`: correct — "Mastodon pages these lists by the id of the follow, not of
  the account followed."
- `AccountRelationships.Show`, "a search answers with accounts and never with where the profile stands with them":
  correct.
