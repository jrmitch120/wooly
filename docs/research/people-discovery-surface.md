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

## Candidate: endorsements — featured profiles

### What it is

Picking a handful of people you follow and putting them on your own profile as "these are worth reading" — the web UI
calls it *Featured profiles*, the API calls it endorsements, and the database calls it a pin.

Two halves that are worth separating, because they have very different value:

- **Writing**: choosing who *you* feature. Rare — a handful of accounts, changed maybe twice a year.
- **Reading**: seeing who *somebody else* features. This is a discovery move — "who does this person I just found
  vouch for?" — and it is a genuinely good one, because a hand-curated list of five beats an algorithm.

### The endpoints

Four, and the naming is a mess of three historical spellings for the same act. From
[`config/routes/api.rb`](https://github.com/mastodon/mastodon/blob/d79f2c5a709e6cff12ed65452fa1526dacb1dacd/config/routes/api.rb):

```ruby
post :pin,       to: 'endorsements#create'
post :endorse,   to: 'endorsements#create'
post :unpin,     to: 'endorsements#destroy'
post :unendorse, to: 'endorsements#destroy'
```

`pin`/`unpin` and `endorse`/`unendorse` are **the same controller actions on the same route table** — the docs mark
`pin`/`unpin` deprecated as of 4.4.0 "in favor of" the new spelling, but nothing is removed and both work.

| Call | Scope | Takes | Returns |
| --- | --- | --- | --- |
| `GET /api/v1/endorsements` | `read:accounts` | `limit` (40/80, **`limit=0` = everyone unpaged**), `max_id`/`since_id` | Array of `Account`, Link header |
| `GET /api/v1/accounts/:id/endorsements` | public (`authorize_if_got_token!`) | same paging | Array of `Account`, Link header |
| `POST /api/v1/accounts/:id/endorse` (or `/pin`) | `write:accounts` | — | `Relationship` |
| `POST /api/v1/accounts/:id/unendorse` (or `/unpin`) | `write:accounts` | — | `Relationship` |

The second row is the interesting one and is easy to miss: **anybody's featured profiles are readable, without a
token**, via
[`Api::V1::Accounts::EndorsementsController`](https://github.com/mastodon/mastodon/blob/d79f2c5a709e6cff12ed65452fa1526dacb1dacd/app/controllers/api/v1/accounts/endorsements_controller.rb).
It returns `[]` for an unavailable account rather than erroring — the same "cannot tell empty from withheld" shape
#162 recorded for `:id/following`.

`endorsed` is on the relationship, so Wooly's existing `/accounts/relationships` call **already brings it back**:

```ruby
attributes :id, :following, :showing_reblogs, :notifying, :languages, :followed_by,
           :blocking, :blocked_by, :muting, :muting_notifications, :muting_expires_at,
           :requested, :requested_by, :domain_blocking, :endorsed, :note
```

One constraint the API does not enforce but the UI implies: endorsing somebody you do not follow is not blocked at the
model level (`AccountPin.find_or_create_by!`), but the web only offers it on accounts you follow.

### Mastonet

**Covered for your own; not covered for anyone else's.** From
[`MastodonClient.AccountActions.cs`](https://github.com/glacasa/Mastonet/blob/cc6e00af72a1f583fe769a167962b26bbf1bdc9f/Mastonet/MastodonClient.AccountActions.cs):

```csharp
Task<MastodonList<Account>> GetEndorsements();                  // GET  /api/v1/endorsements
Task<Relationship> Endorse(string accountId);                   // POST /api/v1/accounts/{id}/pin
Task<Relationship> Unendorse(string accountId);                 // POST /api/v1/accounts/{id}/unpin
```

- `GetEndorsements()` takes **no `ArrayOptions`** — one page of 40, no way to ask for more, though it returns a
  `MastodonList` whose `NextPageMaxId` will be populated. Given a handful of endorsements per account that is
  academic; given `limit=0` exists server-side it is still a gap.
- **There is no `GetAccountEndorsements(accountId)`.** The whole discovery half — reading somebody *else's* featured
  profiles — needs a raw `GET /api/v1/accounts/{id}/endorsements`. One authenticated GET, Link-header paged, returning
  the same `Account` array shape Wooly's `AccountWire.ToAccount` already maps. This is the single cheapest raw call on
  the map.
- `Endorse`/`Unendorse` use the deprecated `/pin` spelling. Functionally identical today (same route target); the risk
  is only that a future Mastodon drops the alias.
- `Relationship.Endorsed` **is** on Mastonet's entity, so the flag is already arriving on every relationship call
  Wooly makes.

### What it costs Wooly

**Core.** `AccountStanding` deliberately keeps only five of the thirteen relationship facts, and `AccountWire` says
why: *"The rest — endorsements, domain blocks, whether boosts are shown — belong to commands this client does not
have, and a record holding them would promise answers no command here can give."* Adding endorsements means adding
`Endorsed` to `AccountStanding` and amending that comment — a two-line change, and a comment that was written
anticipating exactly this.

Then either a fourth `AccountTie` member, or a deliberate decision not to. **A fourth tie is tempting and probably
wrong.** CONTEXT.md defines a **Tie** as *"one of the three things the profile's own account can have with another
and undo again: following it, blocking it, or muting it"* — all three are about *what you see*. An endorsement is
about *what others see*, which is a different kind of fact wearing the same on/off shape. Adding it to `AccountTie`
would make `AccountRelationships.Apply`'s switch grow an arm with no cost, but it would also make `wooly-cli account
tie` report a fourth thing that is not a tie in the term's own sense.

Reading somebody's featured profiles is a `Fetch<Account>` method on `IAccountRelationships` — the same `Collect` loop
as `List`, with a raw call underneath.

**TUI.** Read side: a row on the account screen, or a drill. `AccountLines.Standing` already composes a
`"you follow them · they follow you"` sentence out of the standing's flags; `"featured by you"` is one more clause and
costs one line of code. A "featured profiles" list is another `FollowRequestsScreen`-shaped screen. Write side: a
fourth capital key on `AccountScreen`, which currently has `F`/`M`/`B` and `g` — the obvious letter is `E`, and the
screen's own remark says capitals exist *"so that a lower-case mark key can never fire one by accident"*.

**CLI.** `account endorse` / `account unendorse` / `account endorsements [handle]`, mirroring the existing
follow/unfollow/followers triple exactly. Perhaps the lowest-friction CLI addition of any candidate here.

**What a terminal does better.** The write half: **no**, honestly. Choosing four people to feature is a rare,
deliberative act and a web page with avatars is better at it than a list of handles.

The read half: **yes, meaningfully.** Reading @someone's featured profiles is a two-key drill from the account screen
in a TUI, and on the web it is a tab on a profile page you have to scroll to and that many themes hide. Combined with
the following browser (already a named candidate), it makes a coherent "walk outward from one person" story that the
web has no equivalent of.

**Honest counter-argument.** Endorsements are lightly used across the network — many accounts have none, and a screen
that is empty for four profiles out of five reads as broken rather than as informative. The `hide_collections` /
availability behaviour above means an empty answer and a withheld one look identical, so the screen cannot even say
which it is without checking the account it drilled from.

---

## Candidate: private notes on an account

### What it is

A sticky note only you can see, stuck to somebody's profile: "met at the conference", "posts great photos, awful
takes", "this is my colleague's alt".

### The endpoints

One, and it does everything.

| Call | Scope | Takes | Returns |
| --- | --- | --- | --- |
| `POST /api/v1/accounts/:id/note` | `write:accounts` | `comment` (String, optional) | `Relationship` |

Added in 3.2.0. Reading is free — the note is an attribute of the **relationship**, so any
`GET /api/v1/accounts/relationships?id[]=…` already carries it.

Two behaviours worth pinning down, from
[`Api::V1::Accounts::NotesController`](https://github.com/mastodon/mastodon/blob/d79f2c5a709e6cff12ed65452fa1526dacb1dacd/app/controllers/api/v1/accounts/notes_controller.rb):

```ruby
def create
  if params[:comment].blank?
    current_account.account_notes.find_by(target_account: @account)&.destroy
  else
    @note = current_account.account_notes.find_or_initialize_by(target_account: @account)
    @note.comment = params[:comment]
    @note.save! if @note.changed?
  end
  render json: @account, serializer: REST::RelationshipSerializer, relationships: relationships_presenter
end
```

1. **A blank comment deletes the note.** There is no separate delete verb; clearing the field *is* the delete. That is
   the same "an empty field means the thing is gone" semantics `ContentWarnings.Written` already settles for a post's
   warning, and `PostEdit.ContentWarningWanted` already distinguishes from "leave it alone" — so Wooly has an exact
   precedent, including the three-state problem.
2. **The answer is never null.** `REST::RelationshipSerializer#note` returns `''` when there is none, so a client
   cannot tell "no note" from "empty note" — and does not need to, because they are the same thing.

There is no "list all my notes" endpoint. A note is only ever reachable through the account it is about.

### Mastonet

**Read: yes. Write: no.** `Relationship.Note` is on the entity
([`Relationship.cs`](https://github.com/glacasa/Mastonet/blob/cc6e00af72a1f583fe769a167962b26bbf1bdc9f/Mastonet.Entities/Relationship.cs)):

```csharp
/// <summary>
/// This user's profile bio
/// </summary>
[JsonPropertyName("note")]
public string Note { get; set; } = string.Empty;
```

Note the doc comment is **wrong** — it says "this user's profile bio", which is `Account.Note`. The JSON name is right
and the value is the private note. Anyone reading Mastonet's IntelliSense will get this backwards, which is worth a
line in whatever ticket lands it.

There is **no** `/note` path in the shipped 3.1.3 binary's URL table and no method for it on `IMastodonClient`.
Writing needs a raw call:

```
POST {instance}/api/v1/accounts/{id}/note
Authorization: Bearer {token}
Content-Type: application/x-www-form-urlencoded

comment=met+at+the+conference
```

Response is a `Relationship`, which can be deserialised straight into `Mastonet.Entities.Relationship` and fed to
`AccountWire.ToStanding` unchanged. This is the simplest raw call on the map: one POST, one form field, no paging, no
id juggling beyond the `AccountLookup.Resolve` that every account-taking Wooly call already pays.

### What it costs Wooly

**Core.** A `string? Note` on `AccountStanding` (or on `Account` — but the standing is where it belongs, because it is
a fact about the pair, and because it arrives on the same call). One method on `IAccountRelationships`:
`Task<Account> Note(profile, address, string? comment, ct)`. The `null`-versus-empty question is already answered in
this codebase's own vocabulary and the answer can be copied: `ContentWarnings.Written` is the shared "what counts as
an empty field" rule and this is the same rule.

Wooly currently drops the note on the floor: `AccountWire.ToStanding` maps five of the thirteen relationship fields.
So the read half is a **one-line change to an existing mapper plus one field**, and everything downstream of
`Show` gets it for free.

**TUI.** Reading: one more row under `AccountLines.Standing`, wrapped via `TextWrap` at 61 columns. Writing: a text
field, and the TUI has exactly one text-entry idiom — `ComposeScreen`, which ADR-0015 made a screen on the stack, and
`SearchScreen`, which keeps its query as a plain `string` on the screen rather than in a widget. A one-line note
prompt is closer to `SearchScreen`'s shape (`Type(char)` / `Backspace()`, `IsTyping`, a `▌` caret) than to the compose
editor's, and `SearchScreen` shows that idiom costs about twenty lines. That is the cheapest text input in the
codebase.

**CLI.** `account note <handle> [comment]` — set with an argument, clear with `--clear` or an empty string, print with
no argument (from the relationship the `account tie` command already fetches). `AccountReport`/`AccountJson`/
`AccountDocument` each gain a field.

**What a terminal does better.** Reading, yes — a note is a line of text and a terminal draws lines of text next to
the handle for free, whereas Mastodon's web UI puts the note in a collapsible box on the profile. Writing, roughly
even; it is a one-line text field either way.

The **strongest** claim here is not the TUI but the CLI + scriptability angle: notes are the only per-account
free-text field the API offers, which makes `wooly-cli account note` the natural place to hang anything a user wants
to remember about a person, and `--json` makes the whole set greppable — except that there is no "list all notes"
endpoint, so a full set can only be assembled by walking your following list and reading relationships in batches of
40–80. That is one call per page of follows, which for a typical account is one or two calls. Cheap enough to be a
real feature, and it is a thing the **web cannot do at all**.

**Honest counter-argument.** Notes are a power-user feature with low measured usage across the network, and the
"annotate everyone you follow" workflow above is a use case being invented here rather than one observed. On raw
frequency (criterion 1) this is likely the lowest-scoring candidate on the list.

---

## Candidate: explore — trends, and who to follow

Three separate features that the web bundles into one "Explore" tab. They should be weighed separately: two are
public and trivial, one is personal and is the actual discovery feature.

### 1. Trends

**What it is.** What is being talked about on this instance right now — tags, posts, and linked articles.

| Call | Scope | Takes | Returns |
| --- | --- | --- | --- |
| `GET /api/v1/trends/tags` | **public** | `limit` (default 10, max 20), `offset` | Array of `Tag` |
| `GET /api/v1/trends/statuses` | **public** | `limit` (default 20, max 40), `offset` | Array of `Status` |
| `GET /api/v1/trends/links` | **public** | `limit` (default 10, max 20), `offset` | Array of `Trends::Link` |

No Link header — `offset` only, like `accounts/search`. All three are anonymous-readable, which means a Wooly profile
is not even needed to draw them.

**Mastonet.** Two of three:

```csharp
Task<IEnumerable<Tag>> GetTrendingTags();                                   // GET /api/v1/trends/tags — no limit/offset
Task<MastodonList<Status>> GetTrendingStatuses(int? offset, int? limit);    // GET /api/v1/trends/statuses
```

`GetTrendingTags()` takes no arguments at all, so it is the server's default 10 and no more. `GetTrendingStatuses` has
both. **`trends/links` is absent** from the 3.1.3 binary's URL table entirely — a raw GET, returning an array of
preview-card-shaped objects that Wooly has no type for (`LinkPreview` is close but is *attached to a post*, and
`Trends::Link` carries its own history array).

**Cost to Wooly.** Trending statuses is *free* in the strongest sense: it is a list of posts, so it is a
`Fetch<Post>` — but it does **not** fit `Timeline`, because `TimelineReader` drives everything through
`PagedReading.Collect` with `max_id` cursors and this endpoint pages by `offset`. Either a new one-method port, or
`ITimelineReader` grows an offset path. Trending tags is a `IReadOnlyList<Hashtag>`, which `HashtagWire` already
produces. TUI: another feed-shaped screen, or a section on the search screen. CLI: `trends tags` / `trends posts`.

**What a terminal does better.** **Honestly, no.** Trends are a browse-and-graze surface — you skim, you follow a
link, you look at pictures. That is what a browser is for. The keyboard-first argument barely applies: there is
nothing to *do* to a trend but open it. The one genuine terminal advantage is `wooly-cli trends tags --json` in a
cron job, which is a niche of a niche.

Also worth saying plainly: trends is arguably **not on this map at all.** #159's theme is "the people side — profiles,
following, and finding them", and trending *posts* and *links* are content discovery, not people discovery. Trending
*tags* sits on the boundary and mostly matters as an input to followed hashtags.

### 2. Follow suggestions — who to follow

**What it is.** The instance's own guesses at people you would want to follow, drawn from who you already follow, who
they follow, and what the staff have featured.

| Call | Scope | Takes | Returns |
| --- | --- | --- | --- |
| `GET /api/v2/suggestions` | `read:accounts` | `limit` (40/80), `offset` | Array of `Suggestion` |
| `GET /api/v1/suggestions` | `read:accounts` | `limit` (40/80), `offset` | Array of `Account` — **deprecated** |
| `DELETE /api/v1/suggestions/:account_id` | `write:accounts` | — | `{}` — "don't suggest this person again" |

The v1/v2 difference is **only the serializer**. Both controllers build the same
`AccountSuggestions.new(current_account)` and call `.get(limit, offset)`; v1 does `.map(&:account)` and renders bare
accounts, v2 renders `REST::SuggestionSerializer`, which adds *why*:

```ruby
attributes :source, :sources
has_one :account, serializer: REST::AccountSerializer

LEGACY_SOURCE_TYPE_MAP = {
  featured: 'staff',
  most_followed: 'global',
  most_interactions: 'global',
  similar_to_recently_followed: 'past_interactions',
  friends_of_friends: 'past_interactions',
}.freeze
```

So `sources` is the real, five-valued reason (`friends_of_friends`, `similar_to_recently_followed`, `featured`,
`most_followed`, `most_interactions`) and `source` is the flattened three-valued legacy one. **The reason is the whole
value of the feature** — "followed by people you follow" is worth reading, "popular on this server" is not — and it is
exactly what v1 throws away.

v1 is deprecated (`deprecate_api '2021-05-16'`), which in Mastodon means it still works but every response carries a
`Deprecation` header. It is not scheduled for removal at the pinned commit.

**Mastonet.** v1 only:

```csharp
Task<IEnumerable<Account>> GetFollowSuggestions();       // GET    /api/v1/suggestions — no limit, no offset
Task DeleteFollowSuggestion(string accountId);           // DELETE /api/v1/suggestions/{id}
```

No v2 anywhere in the 3.1.3 binary. So Mastonet gives the accounts and *not* the reasons. Getting the reasons is a raw
`GET /api/v2/suggestions?limit=40` — one call, no paging beyond `offset`, and the JSON is a thin wrapper around an
account Wooly already maps.

**Cost to Wooly.** A `Suggestion` record (`Account` + a `SuggestionReason` enum + a `SuggestionReasonName` mapper,
the house pattern again), a one-method port, and a dismissal method. TUI: a list screen, `⏎` to open the account,
`F` to follow inline, `d` to dismiss — noting that `d` is already "dismiss a notification" and "delete a post" on
other screens, which `Keymap` is built to handle. CLI: `account suggestions`, plus `account suggestions dismiss`.

**What a terminal does better.** This is the strongest terminal case among the three explore features, and the case
is specific: **following somebody is one keystroke from the list**. On the web, who-to-follow is a card grid where
each follow is a mouse trip; in a TUI it is `j j j F F`. A daily driver churning through twenty suggestions does it in
seconds. Showing the *reason* as a muted clause on the same row — which needs the raw v2 call — is what makes the
list scannable rather than a lucky dip.

**Honest counter-argument.** Suggestions are only as good as the instance's data, and on a small instance the list is
short and stale. This is also the candidate whose value is least under Wooly's control: a great UI over a bad list is
still a bad list.

### 3. The directory

**What it is.** An opt-in phone book of accounts on this instance (and ones it federates with), sorted by who posted
most recently or who joined most recently.

`GET /api/v1/directory` — **public**, `offset`, `limit` (40/80), `order` (`active` | `new`), `local` (bool). Array of
`Account`. No Link header. Added in 3.0.0.

**Mastonet.** Covered whole, including a `DirectoryOrder` enum:

```csharp
Task<IEnumerable<Account>> GetDirectory(int? offset, int? limit, DirectoryOrder? order, bool? local);
```

**Cost to Wooly.** Trivial — no new Core type at all, since it returns accounts. One port method, one screen or one
command.

**What a terminal does better.** **No.** The directory is a browse surface for finding people by avatar and bio,
which is precisely what a text UI is worst at, and it is opt-in so it is sparsely populated. Included here for
completeness, not as a live candidate.

---

## Candidate: familiar followers

### What it is

"Three people you follow also follow this person" — the social proof line, shown on a profile you have just landed on.

### The endpoint

`GET /api/v1/accounts/familiar_followers` — `read:follows`, takes `id[]` (repeated), returns an array of
`FamiliarFollowers`, each `{ id, accounts: [Account] }`. Added in 3.5.0. **Batched by design**: one call answers for
many accounts at once, exactly like `/accounts/relationships`, so it fits the "one call per page, not one per row"
shape #162 already recorded for standing badges.

From
[`Api::V1::Accounts::FamiliarFollowersController`](https://github.com/mastodon/mastodon/blob/d79f2c5a709e6cff12ed65452fa1526dacb1dacd/app/controllers/api/v1/accounts/familiar_followers_controller.rb):

```ruby
def set_accounts
  @accounts = Account.without_suspended.where(id: account_ids).select(:id, :hide_collections)
end

def account_ids
  Array(params[:id]).map(&:to_i)
end
```

Note `select(:id, :hide_collections)` — the presenter honours the "hide your social graph" flag, so an account that
hides its followers answers with an empty `accounts` array rather than leaking them. Also note `.map(&:to_i)`: a
non-numeric id silently becomes `0` rather than erroring.

### Mastonet

**Not exposed.** No `familiar_followers` in the 3.1.3 binary's URL table, no method on `IMastodonClient`. A raw
`GET /api/v1/accounts/familiar_followers?id[]=1&id[]=2` — the same repeated-array-param shape Mastonet's own
`GetAccountRelationships(IEnumerable<string> ids)` builds, so there is a working pattern to copy inside the library.
No paging.

### What it costs Wooly

**Core.** No new *port* — it belongs on `IAccountRelationships`, which is already "one port over one family of
endpoints" by its own doc comment. One method, one small record (`FamiliarFollowers(string AccountId,
IReadOnlyList<Account> Accounts)`) or just a dictionary.

**TUI.** One row on `AccountScreen`, under `AccountLines.Standing`: *"followed by alice, bob and 3 others you
follow"*. That is one line of prose, and prose is what `AccountLines` already produces (`Standing` joins clauses with
` · `). At 61 columns it clips like everything else. This is arguably the **cheapest genuinely new information** any
candidate here adds to an existing screen.

**CLI.** A line on `account tie` / a field on `--json`. Or nothing, and "nothing" is a legitimate answer per the map's
own rule.

**What a terminal does better.** Neutral. It is the same one line of text in either medium. The argument for it is not
that a terminal does it better — it is that it is nearly free and it directly serves the *expanded profiles*
candidate the map has already named. Weighed on its own it is thin; weighed as a rider on expanded profiles it is
close to free.

---

## Smaller things in the theme, for completeness

### Featured hashtags on a profile

Distinct from **followed** hashtags: featuring a tag puts it on your own profile as "I post about this", where
following one puts its posts in your feed. Different scope (`write:accounts` vs `write:follows`), different table,
same `POST /api/v1/tags/:id/feature`.

- `GET /api/v1/featured_tags` — your own.
- `POST /api/v1/featured_tags` (`name`), `DELETE /api/v1/featured_tags/:id`.
- `GET /api/v1/featured_tags/suggestions` — tags you use a lot, offered for featuring.
- `GET /api/v1/accounts/:id/featured_tags` — **anyone's**, public, added 3.3.0.

**Mastonet** covers the first three (`GetFeaturedTags`, `FeatureTag`, `UnfeatureTag`, `GetFeaturedTagsSuggestions`,
plus `FeatureTag`/`UnfeatureTag` on `/api/v1/tags/{name}/feature`) but **not** `GET /api/v1/accounts/:id/featured_tags`
— the read-anyone's variant, which is the only half with discovery value.

#159 already parks this: *"Pinned posts and featured hashtags on a profile. Each is a list hanging off an account,
which is a different question from the header block a profile draws."* Nothing found here changes that; the note is
that the *read* endpoint is public and free, and Mastonet lacks exactly that one.

### Pinned posts

`GET /api/v1/accounts/:id/statuses?pinned=true`. **Already reachable today** — Wooly's `TimelineReader.Page` calls
`GetAccountStatuses(accountId, options, onlyMedia: false, excludeReplies: true, pinned: false, excludeReblogs: false)`,
so flipping one argument is the whole of the API cost. The open question is presentational (a second list above the
account's timeline), which is why #159 parks it with featured hashtags.

### `GET /api/v1/accounts/lookup`

Not a feature, but relevant to the cost of every other one here. Wooly resolves an address to an id via
`SearchAccounts(…, resolveNonLocalAccouns: true)` and takes an exact match, because — as `AccountLookup`'s own comment
says — *"Mastonet 3.1.3 has no lookup endpoint"*. That is still true; the endpoint itself does exist
(`GET /api/v1/accounts/lookup?acct=alice@example.social`, public, returns one `Account`).

**But it is not a drop-in replacement.** From
[`Api::V1::Accounts::LookupController`](https://github.com/mastodon/mastodon/blob/d79f2c5a709e6cff12ed65452fa1526dacb1dacd/app/controllers/api/v1/accounts/lookup_controller.rb):

```ruby
@account = ResolveAccountService.new.call(params[:acct], skip_webfinger: true) || raise(ActiveRecord::RecordNotFound)
```

`skip_webfinger: true` means lookup **cannot discover an account the instance has never met**, which is precisely what
`resolve: true` on the search path is for. So lookup is cheaper and exact for accounts already known, and useless for
the first-contact case. Any ticket that wants to speed up address resolution has to keep both paths.

### Mutes and blocks as lists

`GET /api/v1/mutes` and `GET /api/v1/blocks`, both Link-header paged, both returning accounts, both covered by
Mastonet (`GetMutes`, `GetBlocks`, with `ArrayOptions`). Wooly can *set* both ties and cannot *list* either — so
"who have I muted?" is currently unanswerable in this client while "who do I follow?" is a command. It is a small,
symmetrical gap rather than a feature, and it is closer to housekeeping than to discovery.

---

## What Mastonet 3.1.3 covers, in one table

Verified against the shipped binary at `~/.nuget/packages/mastonet/3.1.3/lib/net8.0/Mastonet.dll` and the pinned
source tree.

| Feature | Endpoint | Mastonet 3.1.3 | Gap |
| --- | --- | --- | --- |
| Followed hashtags | `GET /followed_tags` | `ViewFollowedTags(ArrayOptions?)` | — |
| Follow / unfollow a tag | `POST /tags/:n/(un)follow` | `FollowTag` / `UnfollowTag` | — |
| One tag | `GET /tags/:n` | `GetTagInfo` | `Tag` has no `Id`, no `Featuring` |
| Lists — read | `GET /lists`, `/lists/:id` | `GetLists`, `GetList` | `List` has no `Exclusive` |
| Lists — write | `POST`/`PUT`/`DELETE /lists` | `CreateList`, `UpdateList`, `DeleteList` | **title only**; no `replies_policy`, no `exclusive` |
| List members | `GET`/`POST`/`DELETE /lists/:id/accounts` | `GetListAccounts`, `AddAccountsToList`, `RemoveAccountsFromList` | — |
| Lists containing an account | `GET /accounts/:id/lists` | `GetListsContainingAccount` | — |
| A list's timeline | `GET /timelines/list/:id` | `GetListTimeline` | id typed `long`, not `string` |
| My endorsements | `GET /endorsements` | `GetEndorsements()` | no `ArrayOptions` — one page |
| Endorse / unendorse | `POST /accounts/:id/(un)endorse` | `Endorse` / `Unendorse` | uses deprecated `/pin` spelling |
| **Anyone's endorsements** | `GET /accounts/:id/endorsements` | **absent** | raw GET |
| Read a private note | (on `Relationship`) | `Relationship.Note` | doc comment is wrong |
| **Write a private note** | `POST /accounts/:id/note` | **absent** | raw POST, one form field |
| Trending tags | `GET /trends/tags` | `GetTrendingTags()` | no `limit`/`offset` |
| Trending posts | `GET /trends/statuses` | `GetTrendingStatuses(offset, limit)` | — |
| **Trending links** | `GET /trends/links` | **absent** | raw GET, no Wooly type |
| Suggestions (v1) | `GET /suggestions` | `GetFollowSuggestions()` | deprecated; no limit/offset |
| **Suggestions (v2)** | `GET /api/v2/suggestions` | **absent** | raw GET — this is where the *reasons* live |
| Dismiss a suggestion | `DELETE /suggestions/:id` | `DeleteFollowSuggestion` | — |
| Directory | `GET /directory` | `GetDirectory(offset, limit, order, local)` | — |
| **Familiar followers** | `GET /accounts/familiar_followers` | **absent** | raw GET, batched `id[]` |
| My featured tags | `GET`/`POST`/`DELETE /featured_tags` | `GetFeaturedTags`, `FeatureTag`, `UnfeatureTag` | — |
| **Anyone's featured tags** | `GET /accounts/:id/featured_tags` | **absent** | raw GET |
| Pinned posts | `GET /accounts/:id/statuses?pinned` | `GetAccountStatuses(…, pinned: true)` | — already called with `pinned: false` |
| Account lookup | `GET /accounts/lookup` | **absent** | raw GET; not a drop-in (no WebFinger) |
| Mutes / blocks as lists | `GET /mutes`, `/blocks` | `GetMutes`, `GetBlocks` | — |

**Six raw calls** would cover every gap that matters, and each is a single unauthenticated-shaped GET or a
single-field POST. Per #159's own rule this is a tiebreak, not a veto — and on this evidence it barely separates the
candidates at all.

---

## Cost summary

Rough, relative, and deliberately not a ranking.

| Candidate | New Core types | TUI | CLI | Raw HTTP |
| --- | --- | --- | --- | --- |
| Followed hashtags | 1 field + 1 port | new screen **or** one key; possible 10th rail entry | new `tag` branch (3 cmds) | none |
| Lists | new noun, enum, wire, port, 6th `TimelineScope` | 2–3 screens + a picker-shaped destination | new `list` branch (5–7 cmds) | 1 (faithful edit) |
| Endorsements | 1 field on `AccountStanding` (+ maybe a 4th tie) | 1 row + optional list screen + 1 key | 3 commands | 1 (read anyone's) |
| Private notes | 1 field + 1 method | 1 row + a `SearchScreen`-shaped prompt | 1 command | 1 (write) |
| Suggestions | 1 record + 1 enum + 1 port | 1 list screen | 1–2 commands | 1 (for the reasons) |
| Trends | none (tags) / awkward (posts) | 1 screen or a search section | 2 commands | 1 (links) |
| Familiar followers | 1 small record, no new port | 1 row | 1 field or nothing | 1 |
| Directory | none | 1 screen | 1 command | none |
| Featured tags (anyone's) | reuses `Hashtag` | 1 section on the account screen | 1 field | 1 |

## Where the theme's edge is

Deliberately **not** covered, per #159's out-of-scope list: bookmarks, filters and keyword mutes, scheduled posts,
drafts, edit history, quote posts, translation, thread mute, profile *editing* (`PATCH /accounts/update_credentials`),
reports, announcements, streaming.

Two of those are adjacent enough to be worth a sentence, because they will come up:

- **`PATCH /api/v1/accounts/update_credentials`** is how a profile's own bio, custom fields, `locked`, `discoverable`
  and `hide_collections` are set. Mastonet covers it (`UpdateCredentials`). It is profile *editing* and is out — but
  it is the write half of the *expanded profiles* candidate the map has already named, so that ticket owes a sentence
  saying it is read-only.
- **`GET /api/v1/announcements`** is instance-wide notices, covered by Mastonet, and is out.

