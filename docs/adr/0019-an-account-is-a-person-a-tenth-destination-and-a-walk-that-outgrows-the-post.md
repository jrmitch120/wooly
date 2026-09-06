# An account is a person, the rail grows a tenth destination, and the walk outgrows the post

Map #159 asked which three to five features Wooly's account and discovery surface was missing, weighed the whole of
Mastodon's people surface against four criteria, and admitted four: an account screen that says who somebody is
(#164), a browser for either side of an account's follows (#165), a way to move between the three kinds of search
result (#166), and follow suggestions (#171, #172). ADR-0011 settled what a search asks for and ADR-0012 settled what
a tie is; neither covers what an account *is* on a screen, and neither covers a rail. This is that ADR.

The shortlist stopped at four rather than five, deliberately: the second slot was left empty rather than filled with a
candidate that had already lost. Lists, followed hashtags, endorsements, explore, trends, the directory, featured tags
and the write half of a private note are all ruled out on the map, each with its reason recorded there.

## An account is a person, and the header block is the first thing on the screen

`Account` was a scoreboard — an address, a display name and three counts — because that is all a search result needed
(ADR-0011). It gains six facts and stops being one: `Bio`, `Fields`, `Joined`, `IsLocked`, `IsBot` and `AvatarUrl`,
with `AccountField` beside it carrying a field's label, what it says, and the moment a link on it was verified.

**None of them takes `Standing`'s absent-versus-empty treatment, and the difference is the whole reason `Standing` has
it.** ADR-0012 made `AccountStanding` nullable because Mastodon sends a relationship only from the relationship
endpoints, so five falses from a followers list would be five lies. `note`, `fields`, `created_at`, `locked`, `bot` and
`avatar` ride on every account entity Mastodon sends, from every endpoint this client calls. An empty bio is a person
who wrote nothing, and there is no state in which the question went unput — so a nullable there would be a distinction
with nothing on the other side of it.

A bio arrives as the same flattened plain text a post's content does, through the same flattener — which **moves out of
`Core.Posts` to a neutral home** both wire mappers call. Two flatteners over the one HTML subset is how a bio comes to
render differently from the post beneath it; and the module was never about posts in the first place, but about the
HTML an instance serves.

**The header block becomes the screen's first walkable thing.** This is the decision with the reach. A verified link on
somebody's profile is the most useful thing the screen has, and it is only useful if `⏎` opens it — so the block has to
be pickable, which means `j`/`k` land on it, `←`/`→` walk the references in a bio and in a field's value, and the post
keys go quiet while it is picked. That last is not new behaviour but inherited: a follow **notification** carries no
post either, and picking one already leaves those keys with nothing to act on rather than guessing.

**A mention in a bio is drawn and never walked.** `⏎` on a mention resolves off `Post.Mentions`, which the wire sends
down with every post; a bio carries no such list, and a bare `@maria` resolved against the reader's own instance opens
whoever *their* server has by that name — the exact failure `docs/tui-shell.md` warns about. A hashtag and an address
need no resolution at all, so they walk. This is the same shape as an `Image` attachment and a link preview's author
name: drawn, and deliberately not openable.

The header draws whole and cuts nothing. Its worst case is around 29 rows and the screen scrolls, so one `j` puts the
posts on screen — where a truncated bio would be permanent damage done to solve a problem one keypress already solves.
Every section brings its own separator, so an account with no bio, no fields, nobody in common and no note collapses to
four rows and a divider with no gaps left behind.

**No new `Role`, and no emoji.** A verified field takes a `✓` after its value, in `Role.Muted`, because Mastodon only
ever verifies a link and the mark therefore always lands on something already drawn in `Role.Link`. The two flags read
`⚙ bot` and `⚿ locked`, the word carrying and the glyph decorating. 🤖 and 🔒 are astral, `TextWrap.Clip` cuts by
`char`, and their column width is terminal-dependent — three reasons the 61-column contract cannot absorb them. Every
glyph the TUI has today is BMP and one column wide, and it stays that way until something makes `TextWrap` rune-aware,
which is a shell-wide change no feature should smuggle in.

## The rail carries ten destinations, and the tenth is paid for once

Follow suggestions arrive as **`Discover`**, a rail destination immediately after `Search` and in its group — the
places you go when you want something, as against the timelines you read and the profile that is you.

`Destination.cs` says the nine were listed from the start on purpose, "a rail that grows four entries later is a
different rail", so growing one is a change to a settled shape and the argument is on the record. A key on the search
screen was the cheaper answer and lost. What makes the tenth entry worth its cost is that the screen behind it is
**sectioned**: who to follow arrives as a heading with a count, not as the whole screen, so a second kind of suggestion
later is a heading rather than an eleventh rail entry. The cost is paid once, now, instead of again per kind.

The name is `Discover` rather than `Explore` — Mastodon's own word for a surface this map ruled off-theme, which would
promise trends — and rather than `Who to follow`, which names today's only section and would need renaming the day a
second one arrived.

**No second door.** Discover is an **Arrival**: the stack resets to one screen and `esc` goes nowhere, which is what
keeps a destination from also being a pushed screen. Its unread count is always zero, like Search's — nothing there is
waiting for anybody.

**A suggestion is read from `/api/v2/suggestions`, because the reason is the feature.** v1 throws `sources` away, and
"followed by people you follow" is the entire argument for putting a stranger on screen; a bare list of strangers is a
list of strangers. Mastonet has v1 only, so the read is one raw `GET` over the named `HttpClient` every Mastonet call
already runs through — ADR-0011 named that as the cheaper of the two ways out of a Mastonet gap, and this is the second
time it has been taken. Dismissal is `DELETE /api/v1/suggestions/:id`, which Mastonet does have.

**It lands on a narrow port of its own, `IFollowSuggestions`, not on `IAccountRelationships`.** ADR-0012 put ties,
lists and requests on one port because they are one subject reached through one family of endpoints; `/suggestions` is
not in that family, and that port's own doc comment says so. A screen composing two narrow ports is cheaper than one
port meaning "whatever Discover shows" — which is the shape ADR-0005 asks for and the shape `IInstanceSearch` and
`ITimelineReader` already have.

## The walk outgrows the post, and a screen's sections gain a key

Three of the four features needed the same two things, and they are built once.

**`Screen` stops deriving its references from a `Post?`.** It asks the picked thing what it carries, with today's post
implementation as the default. A bio is the first reference source in this client that is not a post, and it will not
be the last.

**`Line` gains a heading mark and `Scroll` gains one function beside `To`**, and `[` / `]` move the pick to the run
before or after the current one. `]` is the next section, `[` the one before, on three merits: unshifted, which is what
a key pressed to get somewhere should be and what the `j`/`k` beside it already are; the vim bracket family, which is
move-by-structure and nothing else, so the convention it borrows needs no analogy to carry it; and symmetric and
directional on sight. `{`/`}` was this decision's first answer, taken while `[`/`]` were being held against a possible
shell-wide swap of `j`/`k`; that reservation has been released, the keys are spent on merit, and **the `j`/`k` → `[`/`]`
swap is off rather than deferred**. `{`/`}` stay free as the fallback should these ever have to move.

Three rules make the jump behave:

- **It brings the section's heading with it, but only when the heading is not already on the page.** `Scroll.To` moves
  the minimum needed to show the selection, so an unaided jump downwards lands the picked row on the *bottom* row with
  the section just left still filling the screen — a press that reads as having done nothing. Always anchoring the page
  on the heading fixes that and settles cleanly, but on a search whose three kinds all fit on one screen it scrolls the
  accounts off the top for no reason. So: anchor when the heading is off the page, and otherwise leave the offset
  alone.
- **It reclaims, like `j`/`k`.** Computing "the section after this one" from a pick the reader cannot see is exactly
  the failure reclaiming exists to prevent, so after `↓ ↓ ↓ ]` the jump runs from the topmost item on the page.
- **It clamps at the ends and never wraps.** Nothing in this shell wraps, and with three sections a wrap saves one
  press at the cost of an ambiguous position. A reader already among the posts presses `]` and nothing happens, which
  is the clamp saying so.

The seam is where it is because `Scroll` answers from the rows alone — it finds the selection by `Role.Selection` and
the topmost item by `Line.Item`, which is why `Picked` stamps rows and no screen scrolls itself. Stamping a heading
keeps that property; a screen telling the window which row to anchor on would give every screen a way to break
scrolling in a module it never touches, with nothing to catch it at compile time (#51).

**Headings carry counts** — `── 7 accounts ──`, `── 2 pinned ──`, `── 11 followed by people you follow ──` — the
`── {n} replies ──` vocabulary the post screen already speaks, and it tells a reader whether the run below is worth
walking or worth jumping. A run whose total is a *fact* is counted; a run that is a page of an unbounded list is not,
which is why `── their posts ──` stays uncounted under a counted pinned run. **A section with nothing in it draws no
heading at all** — no heading over nothing, no announcement of a key that would do nothing, and per-kind absence is the
ordinary case on almost every search.

The status row says `[/]:section`, shell-wide, and only on a screen that has two or more headed runs right now.

## What each surface got, said out loud

The map's standing rule is that every feature decides per surface and "nothing" is a legitimate answer that has to be
said rather than left to fall out. Three of the four features are TUI-only:

- **The following browser: nothing on the CLI.** `account following` and `account followers` already print a page,
  which is the right shape for a pipe, and narrowing there would read the whole list to filter it locally — which is
  `wooly account following | grep`, done better.
- **Moving between sections: nothing on the CLI and nothing in Core.** A non-interactive report has no cursor to move,
  and `--type` is already the CLI's answer to the same need.
- **Follow suggestions: nothing on the CLI.**
- **Pinned posts: nothing on the CLI**, because the CLI has no way to read an account's posts at all — there is no
  `timeline account` — so pinned posts would be the only posts it ever printed for an account. Inventing the
  account-posts surface through that back door is a bigger decision than the feature that noticed it.
- **The account screen gains `account show <address>`,** which the CLI turns out never to have had: the `account`
  branch is follow/unfollow, block, mute, the two lists and requests, and `profile show` is the local credential entry,
  a different thing entirely. The port call behind it already exists, and a bio is exactly what somebody piping `jq`
  wants. Familiar followers stays off it — a call spent on something only a screen benefits from.

## Pinned posts are a sixth `TimelineScope`, not a new port

An account's timeline is read with `pinned: false` hardcoded in `TimelineReader.Page`, so an account whose bio says
"see my pinned post" has, in Wooly today, made that post unreachable — a pin is usually old and often a reply, and the
account timeline drops both. That is a functional hole, which is what carries it past features that merely match what
the web does.

Marking rows already in hand was never available: Mastodon sends `Status.pinned` only for the reader's *own* posts, so
it reads false on everybody else's. A section or nothing.

It is a sixth scope on `ITimelineReader` rather than a port of its own, because pinned posts *are* posts read from an
account — precisely what that port is for, and ADR-0005 asks for narrow ports rather than a port per call. **ADR-0007
is untouched**: the read goes through `PagedReading.Collect` harmlessly, one page with no Link header, which is the
opposite of the trends case this map ruled out, where `offset` cursors would have broken the one paging loop.

On screen it is one `PostList` with the headings spliced — the `[...ancestors, post, ...replies]` shape the post screen
already uses — with the duplicate dropped from the *timeline* run in the shell, so the screen is handed two disjoint
lists and cannot disagree with itself about which run a post is in.

## Consequences

**Opening an account is getting expensive, and the reason is older than this work.** `IAccountRelationships.Show`
resolves the address, and `Timelines.Read(Timeline.By(address))` resolves it again inside `TimelineReader` — so today's
arrival is four calls, of which two are the same lookup. Familiar followers makes five and the pinned read makes six,
three of them resolving one address. This ADR deliberately does not fix it: threading an id through `Timeline` would
undo the reason `Timeline` takes an address at all (ADR-0012's "an id means nothing on any other instance"), which is a
port-shape decision that should not ride in behind a feature. It leaves the map as its own issue.

**The shell gains its first drill-in cache.** `DestinationCache` is keyed by `DestinationKind`, one entry per rail
destination, and the follow browser is not a destination — so it keeps its own, age-only, one minute, keyed by account
and side. The visible symptom of getting this wrong is following somebody from inside the browser, popping out and
re-opening within the minute to a row that has not caught up; `Shell.Tie()` already forgets a destination for exactly
this reason, so the fix is one line beside code doing the same job, the day it bites.

**One reader-owned number joins the three in `ShellTiming`** — the follow-list threshold, above which the browser
browses rather than searches. It goes beside them so a reader looking for the shell's numbers finds them together.

**ADR-0011 is not amended.** Its three decisions all still read true: a search's ask stays whole, `--type` still
narrows what came back, and absent-versus-empty is untouched. Moving *within* an answer is a different question from
narrowing the *ask*, and the TUI still sends `Everything` and always will.

**Two things to check against a live instance before building on them.** Mastonet 3.1.3's `Account.Bot` looks unbound —
every other property on the entity carries an explicit JSON name and the literal string `bot` appears nowhere in the
assembly — so it may read false on every account; if it does, `IsBot` is dropped rather than paid for with a raw GET,
and lands free later. And Mastodon's suggestion sources are documented as excluding accounts already followed,
dismissed or blocked, which is what lets the Discover screen skip the relationships call entirely; if that turns out to
be false, the standing suffix comes back — the call does not.
