# What else is on Mastodon's people-and-discovery surface

Research for [#161](https://github.com/jrmitch120/wooly/issues/161), part of the map in
[#159](https://github.com/jrmitch120/wooly/issues/159). The three candidates the map already names — expanded
profiles, a following browser, and telling the three kinds of search result apart — are **not** covered here. This is
what is left in the theme.

Its sibling, [#162](https://github.com/jrmitch120/wooly/issues/162), covers `accounts/search` and the
following/followers lists in depth (`docs/research/following-and-account-search.md` on `research/following-search`).
Where a fact belongs to that ticket it is referenced, not repeated.

**No recommendation is drawn here.** The shortlist is a separate, human ticket.

## Sources, pinned

- **Mastodon** at [`d79f2c5`](https://github.com/mastodon/mastodon/tree/d79f2c5a709e6cff12ed65452fa1526dacb1dacd) —
  the same commit #162 pinned, so the two documents agree by construction.
- **Mastonet** at [`cc6e00a`](https://github.com/glacasa/Mastonet/tree/cc6e00af72a1f583fe769a167962b26bbf1bdc9f),
  checked against the shipped **3.1.3** binary in `~/.nuget/packages/mastonet/3.1.3/lib/net8.0/Mastonet.dll`. No
  `3.1.3` tag exists in the repo; every claim below about what 3.1.3 has was verified against the binary's own
  URL-literal and identifier tables, and agrees with that tree.
- **This repo**, at `564ef28`.

## The shape of the answer in one paragraph

Six candidates in the theme, plus three smaller ones. **Mastonet covers almost all of it** — followed hashtags, lists
whole, endorsements, follow suggestions, trending tags and statuses, the directory, featured tags. It has exactly
three gaps that matter: **writing** a private note, `familiar_followers`, and the newer `/api/v2/suggestions`. Cost to
Wooly varies by an order of magnitude: followed hashtags is a small Core port with no new domain noun; lists is a
whole new noun with a CRUD surface and a sixth timeline scope; trends is nearly free but arguably not a *people*
feature at all.

---

## Candidate: followed hashtags

### What it is

Subscribing to a hashtag the way you subscribe to a person: everything posted under `#dotnet` turns up in your home
timeline from then on, without you keeping a place on the rail for it or going and looking.

This is the one candidate that **directly meets a settled decision head-on**. `docs/tui-shell.md` gives the rail
exactly one hashtag destination, set in TOML:

```toml
[preferences]
hashtag = "dotnet"
```

and says of it: *"Eight of the nine destinations are the same eight for everybody. The ninth is a hashtag, and which
one is nobody's business but the reader's."* Followed hashtags is Mastodon's own answer to the same need, kept
server-side, unbounded in number, and already shared with the reader's phone. Whatever the shortlist decides, it has
to say something about that rail entry.

### The endpoints

| Call | Takes | Returns |
| --- | --- | --- |
| `GET /api/v1/followed_tags` | `limit` (default **100**, max **200**), `max_id`/`since_id`/`min_id` | Array of `Tag`, each with `following: true` |
| `GET /api/v1/tags/:id` | `:id` is the tag **name**, case-insensitive | One `Tag` |
| `POST /api/v1/tags/:id/follow` | name | The `Tag`, now `following: true` |
| `POST /api/v1/tags/:id/unfollow` | name | The `Tag`, now `following: false` |

Scopes: `read:follows` to list, `write:follows` to follow and unfollow. (`write:accounts` covers feature/unfeature,
which is a different thing — see *featured hashtags* below.)

**Paging is by Link header**, on the id of the `TagFollow` row — the same shape as `:id/following`, and the same
warning applies. From
[`FollowedTagsController`](https://github.com/mastodon/mastodon/blob/d79f2c5a709e6cff12ed65452fa1526dacb1dacd/app/controllers/api/v1/followed_tags_controller.rb):

```ruby
TAGS_LIMIT = 100

def set_results
  @results = TagFollow.where(account: current_account).joins(:tag).eager_load(:tag).to_a_paginated_by_id(
    limit_param(TAGS_LIMIT), params_slice(:max_id, :since_id, :min_id))
end

def records_continue?
  @results.size == limit_param(TAGS_LIMIT)
end
```

`limit_param(100)` with no explicit max caps at `100 * 2` = **200**, by the same rule #162 derived for the 80 on
account lists. Pagination headers arrived in 4.1.0; the endpoint itself in 4.0.0.

**What a `Tag` carries** — from
[`REST::TagSerializer`](https://github.com/mastodon/mastodon/blob/d79f2c5a709e6cff12ed65452fa1526dacb1dacd/app/serializers/rest/tag_serializer.rb):

```ruby
attributes :id, :name, :url, :history
attribute :following,  if: :current_user?
attribute :featuring,  if: :current_user?
```

`history` is the same seven-day `[{day, uses, accounts}]` array Wooly's `HashtagWire.Total` already folds down.
`following` and `featuring` are sent **only to an authenticated request** — a signed-out search gets neither.

One behaviour worth knowing before somebody reports it as a bug: `set_or_create_tag` will happily construct an unsaved
`Tag` for a name nobody has ever used, so `GET /api/v1/tags/neverused` answers 200 with an empty history rather than
404. Only a name failing `Tag::HASHTAG_NAME_RE` is a 404.

### Mastonet

**Fully covered.** From
[`MastodonClient.AccountActions.cs`](https://github.com/glacasa/Mastonet/blob/cc6e00af72a1f583fe769a167962b26bbf1bdc9f/Mastonet/MastodonClient.AccountActions.cs):

```csharp
Task<Tag> GetTagInfo(string tag);                                  // GET  /api/v1/tags/{tag}
Task<Tag> FollowTag(string tag);                                   // POST /api/v1/tags/{tag}/follow
Task<Tag> UnfollowTag(string tag);                                 // POST /api/v1/tags/{tag}/unfollow
Task<MastodonList<Tag>> ViewFollowedTags(ArrayOptions? options);   // GET  /api/v1/followed_tags
```

`ViewFollowedTags` returns a `MastodonList<Tag>`, so `PagedReading.Collect` drives it unchanged — Link-header paging
with `idOf: null`, exactly as `AccountRelationships.Collect` already does, and for the same reason (the cursor is a
follow id, not the tag's).

Two gaps in Mastonet's **`Tag` entity**
([`Tag.cs`](https://github.com/glacasa/Mastonet/blob/cc6e00af72a1f583fe769a167962b26bbf1bdc9f/Mastonet.Entities/Tag.cs)):

```csharp
public string Name { get; set; }
public string Url  { get; set; }
public IEnumerable<History>? History { get; set; }
public bool? Following { get; set; }
```

- No `Id`. Harmless: every tag endpoint takes the **name**, and Wooly's `Timelines.Hashtag` already normalises names.
- No `Featuring`. Only matters if featured hashtags is in scope (below).

`Following` is `bool?`, which lines up neatly with Wooly's existing "absent is not the same as false" convention on
`Account.Standing` — a tag from an unauthenticated search says nothing, a tag from `followed_tags` says `true`.

### What it costs Wooly

**Core.** `Search.Hashtag` (`Name`, `RecentPosts`, `RecentAccounts`, `Url`) is already exactly the right shape and
already fed by `HashtagWire.ToHashtag`, which puts the name through `Timelines.Hashtag.Bare`. The only domain change
is one nullable `bool? Followed` on `Search.Hashtag`, carrying the same doc-comment argument `Account.Standing` makes.

New port — call it `IFollowedHashtags` — with three methods (`List`, `Set(tag, wanted)`, `Show`). It would be the
smallest port in the codebase: `Fetch<Hashtag> List(...)` over `PagedReading.Collect`, plus a follow/unfollow pair
modelled on `AccountRelationships.Set`'s tie-on-or-off shape (ADR-0012), which is already the house pattern for "one
thing that is on or off, not two acts". Note the port needs **no address lookup**, so it avoids the whole
`AccountLookup` round trip that makes `AccountRelationships` expensive.

**TUI.** Two shapes are available, and they differ a lot in cost.

1. *A screen.* A list of followed tags, `j`/`k` to walk, `⏎` to open that tag's `FeedScreen` — which `Shell.OpenTag`
   already does for a tag found by search, per `docs/tui-shell.md`: *"A hashtag opens exactly the way `Shell.OpenTag`
   already opens one found by search — same `FeedScreen`, same breadcrumb, no new screen type."* So opening costs
   nothing new. The screen itself is close to `FollowRequestsScreen` (82 lines) in shape: a `Picked<T>` list of
   one-row items with a capital-letter action key. Reached from a tenth rail destination, or drilled into.
2. *A key on an existing screen.* `F` on a picked hashtag — in search results, or on a `FeedScreen` showing a tag —
   toggling the follow. Nearly free, adds no screen, but gives no way to see *what you follow*.

Either way `Keymap` gains an entry, since #147 made it the single table where a key's meaning per screen lives.

**A tenth rail destination is not free.** `Rail` is built from an ordered `IReadOnlyList<Destination>` and
`DestinationKind` is a closed enum of nine; the rail is 18 columns and full height, so a tenth row fits, but
`Destination.cs` states the nine deliberately: *"All nine were listed here from the start, before four of them had a
screen — deliberately, because the rail's shape is what #28 settled, and a rail that grows four entries later is a
different rail."* Adding one is a decision, not an implementation detail.

**CLI.** A `tag` branch is missing entirely — there is no `tag` noun in `WoolyCommandApp`, only `timeline tag`. Three
commands (`tag follow`, `tag unfollow`, `tag list`) modelled on `account follow` / `account following`, reusing
`PagedListCommand` / `PagedListSettings` and `Listing`. `SearchReport` and `SearchJson` already render hashtags, so
the output shapes exist.

**What a terminal does better.** Genuinely better, for a specific reason: the web's followed-tags page is a
settings-ish list you visit rarely, whereas in a terminal `tag follow dotnet` is one line in a shell script and
`wooly-cli tag list --json` is pipeable. The TUI half is a keyboard-first list, which is criterion (2)'s paradigm
case. The **weaker** claim is the reading half — a followed tag's posts land in the home timeline anyway, so the
feature is about *managing subscriptions*, and managing subscriptions is a thing terminals and web pages do at roughly
equal quality.

**Honest counter-argument.** This is the candidate most likely to be judged as *replacing* an existing decision rather
than adding to it. If the rail's one hashtag place is kept alongside a followed-tags list, a reader has two unrelated
ways to keep a tag, one local and one server-side, and nothing on the rail says which is which.

---

## Candidate: lists

### What it is

A named subset of the people you already follow — "work", "the football people" — each with a timeline of its own, so
you can read one corner of your feed without the rest of it.

### The endpoints

Eight, in three groups. Scopes: `read:lists` to read, `write:lists` to change.

**The lists themselves**
([`ListsController`](https://github.com/mastodon/mastodon/blob/d79f2c5a709e6cff12ed65452fa1526dacb1dacd/app/controllers/api/v1/lists_controller.rb)):

| Call | Takes | Returns |
| --- | --- | --- |
| `GET /api/v1/lists` | nothing | Array of `List` — **unpaged**, `List.where(account: current_account).all` |
| `GET /api/v1/lists/:id` | — | One `List` |
| `POST /api/v1/lists` | `title` (required), `replies_policy`, `exclusive` | The `List` |
| `PUT /api/v1/lists/:id` | the same three | The `List` |
| `DELETE /api/v1/lists/:id` | — | `{}` |

`params.permit(:title, :replies_policy, :exclusive)` is the whole of what a list is.

**Who is in one**
([`Lists::AccountsController`](https://github.com/mastodon/mastodon/blob/d79f2c5a709e6cff12ed65452fa1526dacb1dacd/app/controllers/api/v1/lists/accounts_controller.rb)):

| Call | Takes | Returns |
| --- | --- | --- |
| `GET /api/v1/lists/:id/accounts` | `limit` (default 40, max 80; **`limit=0` returns everyone, unpaged**), `max_id`/`since_id` | Array of `Account`, Link header |
| `POST /api/v1/lists/:id/accounts` | `account_ids[]` | `{}` |
| `DELETE /api/v1/lists/:id/accounts` | `account_ids[]` | `{}` |
| `GET /api/v1/accounts/:id/lists` | — | The lists **this account is in**, unpaged |

**Reading one**: `GET /api/v1/timelines/list/:list_id`, an ordinary timeline — same `limit` 20/40, same Link-header
paging as home.

### Facts the docs do not make obvious

**You can only add people you already follow.** Not enforced in the service —
[`AddAccountsToListService`](https://github.com/mastodon/mastodon/blob/d79f2c5a709e6cff12ed65452fa1526dacb1dacd/app/services/add_accounts_to_list_service.rb)
just does `@list.accounts << account` — but in the join model
([`ListAccount`](https://github.com/mastodon/mastodon/blob/d79f2c5a709e6cff12ed65452fa1526dacb1dacd/app/models/list_account.rb)):

```ruby
def validate_relationship
  return if list_owner_account_is_account?
  errors.add(:account_id, :must_be_following) if follow_id.nil? && follow_request_id.nil?
  ...
end
```

A **pending follow request counts** — you can list somebody whose locked account has not answered yet — and `active`
(`where.not(follow_id: nil)`) is how Mastodon tells a live member from a pending one. **You can always add yourself**,
follow or not. So a Core type saying "a list is a subset of your follows" would be very nearly, but not exactly,
right.

**Fifty lists, 256-character titles**
([`List`](https://github.com/mastodon/mastodon/blob/d79f2c5a709e6cff12ed65452fa1526dacb1dacd/app/models/list.rb)):

```ruby
PER_ACCOUNT_LIMIT  = 50
TITLE_LENGTH_LIMIT = 256
enum :replies_policy, { list: 0, followed: 1, none: 2 }, prefix: :show, validate: true
```

`exclusive` (default false) means "keep these people's posts **out** of home" — the only genuinely destructive-ish
knob on the feature, and the one a client that silently drops it will get wrong on an edit.

`replies_policy` defaults to **`list`** (show replies to members of the list); the other two are `followed` (replies to
anyone you follow) and `none`.

### Mastonet

**Almost fully covered**, from
[`MastodonClient.cs`](https://github.com/glacasa/Mastonet/blob/cc6e00af72a1f583fe769a167962b26bbf1bdc9f/Mastonet/MastodonClient.cs):

```csharp
Task<IEnumerable<List>> GetLists();                                          // GET    /api/v1/lists
Task<IEnumerable<List>> GetListsContainingAccount(string accountId);         // GET    /api/v1/accounts/{id}/lists
Task<MastodonList<Account>> GetListAccounts(string listId, ArrayOptions?);   // GET    /api/v1/lists/{id}/accounts
Task<List> GetList(string listId);                                           // GET    /api/v1/lists/{id}
Task<List> CreateList(string title);                                         // POST   /api/v1/lists
Task<List> UpdateList(string listId, string newTitle);                       // PUT    /api/v1/lists/{id}
Task DeleteList(string listId);                                              // DELETE /api/v1/lists/{id}
Task AddAccountsToList(string listId, IEnumerable<string> accountIds);       // POST   /api/v1/lists/{id}/accounts
Task RemoveAccountsFromList(string listId, IEnumerable<string> accountIds);  // DELETE /api/v1/lists/{id}/accounts
Task<MastodonList<Status>> GetListTimeline(long listId, ArrayOptions?);      // GET    /api/v1/timelines/list/{id}
```

Three gaps, all small and all pointing the same way — **Mastonet models a list as a title and nothing else**:

1. `CreateList` and `UpdateList` send **`title` only**. `replies_policy` and `exclusive` cannot be set. Both throw
   `ArgumentException` on an empty title before any call is made.
2. Mastonet's `List` entity has `Id`, `Title`, `RepliesPolicy` — and **no `Exclusive`**, though the server sends it
   (`REST::ListSerializer`: `attributes :id, :title, :replies_policy, :exclusive`). An exclusive list is
   indistinguishable from an ordinary one through Mastonet, and a round-trip edit silently drops the flag.
3. `GetListTimeline` takes a **`long`**, where every other id in the library is a `string`. Cosmetic, but it does not
   compose with `List.Id` without a parse.

Read-only, and create-with-a-title, work today. Editing a list faithfully needs a raw `PUT /api/v1/lists/:id` with
`title`, `replies_policy` and `exclusive` — form-encoded, one call, no paging. Reading `exclusive` back needs either a
raw `GET` or an extra field on a Wooly-side wire record.

### What it costs Wooly

By far the **largest** candidate on this map, and the only one that adds a domain noun with a lifecycle.

**Core.** A new `Lists/` folder alongside `Accounts/` and `Search/`:

- `AccountList` — `Id`, `Title`, `RepliesPolicy`, `Exclusive`. Mastodon's word is "list", which collides badly with
  `IReadOnlyList<T>` and with `Mastonet.Entities.List`; the codebase's existing answer to that collision is the alias
  pattern at the top of `AccountRelationships.cs` and `HashtagWire.cs`. CONTEXT.md would owe a term.
- `RepliesPolicy` — a three-valued enum plus a `RepliesPolicyName` mapper: exactly the `PostVisibility` /
  `PostVisibilityName` and `SearchKind` / `SearchKindName` pattern already in the tree.
- `AccountListWire` — the wire crossing, alongside `AccountWire`, `PostWire`, `HashtagWire`, `NotificationWire`.
- `IAccountLists` port, roughly six methods. **Every call that names a member takes an account id**, and Wooly's house
  rule is that a user types an **address** (`AccountAddress`), so add/remove costs an `AccountLookup.Resolve` round
  trip per account — the same tax `AccountRelationships.Set` already pays and documents.
- **`TimelineScope` gains a sixth member.** `Timeline` is a sealed record with private construction and factories, so
  a `Timeline.List(id)` factory drops in cleanly beside `Timeline.Tag` and `Timeline.By`; `TimelineReader.Page`'s
  switch gains an arm calling `GetListTimeline`; `Timeline.Description` gains a sentence. Genuinely small — but note
  `Timeline.By` carries an `AccountAddress` and resolves it once before the first page, and a list id has no
  equivalent user-typed name, so `Timeline` would carry its first id-shaped field.

**TUI.** Two screens minimum: a list-of-lists, and each list opening onto a `FeedScreen` (which `Timeline.List` makes
free, the way the rail's hashtag destination is free). Members management is a third screen and is the expensive part.
A rail destination is arguable — lists are a *place you read*, which is what the rail is for, but the rail has one
entry per place and a reader with eight lists cannot have eight entries. The likeliest shape is one "Lists"
destination opening onto a chooser, which is a shape **no existing destination has**: each of the nine either reads a
timeline, reads a list of things that open onto posts or accounts, or is search. A destination that opens onto a
*picker* is new furniture.

**CLI.** A `list` branch of five to seven commands (`list list`, `list show`, `list create`, `list delete`, `list add`,
`list remove`, plus `timeline list <id>`). Straightforward — every piece has a precedent in the `account requests`
branch — but it is the biggest single addition to the command tree since `dm`.

**What a terminal does better.** Mixed, and the two halves point opposite ways.

- *Reading* a list is a timeline, and `wooly-cli timeline list work` composes with pipes in a way the web cannot. Real
  win.
- *Managing* a list is closer to the "profile-editing form" the map's criterion (2) explicitly calls out as **not** a
  terminal win. Mastodon's web UI for adding people to a list is a search box and a plus button, which is fine.
- The genuinely terminal-shaped middle is `GetListsContainingAccount` — "which of my lists is this person in?" as a
  line on the account screen. One unpaged call, a fact the web buries two clicks deep, and it costs nothing but a row.

**Honest counter-argument.** Lists are a *curation* feature: the work is in the setting up, done rarely, and the web
already does it adequately. Daily-driver frequency (criterion 1) is high for *reading* a list and near zero for
*editing* one, which suggests the read half and the write half should be weighed separately rather than as one
feature.

---

<!-- MORE -->
