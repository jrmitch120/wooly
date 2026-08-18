# The TUI shell: the contract

What ADR-0014 decided, in a form a ticket can be written against. The reasoning is in the ADR; this is the enumerable
part — regions, screens, keys, roles, and the shape of a theme.

The shells this describes were prototyped first; the drawings live on the throwaway branch
`worktree-prototype-tui-shell` (`src/Wooly.Tui.Prototype/SCREENS-C.md`), which is the primary source for why this and
not something else. None of that code is production code.

## Regions

```
┌ 18 ─────────────┬─────────────────────────────────────────────────────────┐
│ rail            │ breadcrumb                                        1 row │
│                 ├─────────────────────────────────────────────────────────┤
│ destinations    │ content                                                 │
│ + unread counts │ (feed · post · account · conversation · search results) │
│                 │                                                         │
│ ─────────────── │                                                         │
│ quota           │                                                         │
├─────────────────┴─────────────────────────────────────────────────────────┤
│ status: the keys this screen answers to            · quota          1 row │
└───────────────────────────────────────────────────────────────────────────┘
```

| Region | Size | Holds |
|---|---|---|
| Rail | 18 columns, full height less the status row | Destinations, their unread counts, the rate-limit quota at its foot |
| Breadcrumb | 1 row, content width | Where you are in the stack; a fetch-in-progress marker at its right |
| Content | the rest | Exactly one screen at a time |
| Status | 1 row, full width | The current screen's keys; the quota again when the rail is hidden |

At 80 columns this leaves the content 61. That is the width every screen must read well at — it is the narrow case the
right-hand context pane failed (ADR-0014).

## Screens, and who owns them

A screen is a place in the stack, not a window. Entering one pushes, `esc` pops, and the breadcrumb is the stack.

| Screen | Reached by | Ticket |
|---|---|---|
| Feed — home, local, federated, the rail's own hashtag | A rail destination | #28 |
| Hashtag — a tag walked to, not the rail's own | A search result, or `⏎` on a picked hashtag reference | #29, reference #65 |
| Post — the post whole, its ancestor chain above and its replies below | `⏎` on a feed item | #28, ancestors #72 |
| Account — who wrote it, standing, their posts | `a` on a feed item or inside a post | shell #28, tie actions #29 |
| Notifications | A rail destination | #29 |
| Search — prompt and results | A rail destination, or `/` | #29 |
| Follow requests | A rail destination | #29 |
| Direct messages — conversations, then a thread | A rail destination | #30 |
| Compose / reply / edit — a screen on the stack, like any other | `c`, `r` or `e` | #28 |
| Media inside a post or feed item | Drawn in place | #31 (ADR-0016) |

Every screen owes three things: it reads at 61 columns, it says what its keys are on the status row, and it names roles
rather than colours (below).

## Keys

A key may mean different things on different screens — `d` dismisses a notification and deletes a post — which is
workable only because the status row always shows the current screen's keys. What may **not** vary is the frame:

| Key | Everywhere |
|---|---|
| `esc` | Up one level of the stack. Never quits. |
| `ctrl-q` | Quit. |
| `?` | The keymap for this screen. The spec has no in-app help story; this is the shell adding one, and #28 carries it — every other screen inherits it for free. |
| `tab` / `shift-tab` | Moves the cursor (`▶`) at once. The selection follows it (`▷` while it lags behind), and that destination loads, once the tabbing has stopped for ~250ms. |
| `/` | Search. Goes to the search destination; what it opens onto is #29's. |

Feed and post:

| Key | Does | Note |
|---|---|---|
| `k` `j` | The next post / the one before it, with the screen following the selection | `Home`/`End` too, for the first and the last |
| `↓` `↑` | Move the screen by a few rows, leaving the selection alone | The only way to read a post taller than the terminal to its end |
| `PgDn` `PgUp` | The same, a screenful at a time | A screenful is however many rows there is room for |
| `⏎` | Open the post | Answers only, inside a post: the post itself is already open (#48) |
| `a` | Open the author's account | |
| `c` | Compose | |
| `r` | Reply | |
| `b` | Boost / un-boost | Needs viewer state on `Post` |
| `f` | Favorite / un-favorite | Needs viewer state on `Post` |
| `p` | Pin / unpin | Own posts only |
| `e` | Edit | Own posts only |
| `d` | Delete | Own posts only, **confirmation required** (story 43) |
| `x` | Show what the post is hiding | Its warned text, its sensitive attachments, or both (#113) |
| `←` `→` | Walk the references (hashtag, mention, link) inside the picked post | Clamps at the ends; `esc`/`j`/`k` clear the pick; `⏎` opens what's picked (below) |
| `1`-`9` `0` | Toggle the 1st-10th option of the picked post's poll | Only where the poll would still take a vote — no-op otherwise, and off the status row there too; `esc`/`j`/`k` discard the toggle |
| `v` | Cast the toggled poll vote | **Confirmation required** (story 43), same as delete. On a poll already voted in or closed, says which rather than asking |

Screen-local, and deliberately colliding with the above because they are never on screen together:

| Screen | Keys |
|---|---|
| Account | `F` follow/unfollow · `M` mute/unmute · `B` block/unblock — capitals, so a lower-case mark key can never fire a tie by accident |
| Notifications | `d` dismiss one · `D` clear all |
| Follow requests | `a` accept · `x` reject |
| Direct messages | `⏎` open the conversation · `m` mark read — `m` again inside the thread, where a reader who has just read it is most likely to press it |
| Conversation | `m` mark read, and every key that acts on a post, since each message in it is one |
| Home, local, federated, hashtag, Notifications, Messages, Requests, Post, Account | `g` refresh — evicts the destination's cache entry (where one exists) and re-runs the same fetch its own arrival runs |

### What the four screens settled

The keys above are the contract. These are the questions building them raised, answered once so the next screen does
not answer them differently:

- **A notification is not the post it is about.** `d` dismisses by the notification's own id; every other key on the
  row acts on the post it carries, so a mention can be answered without leaving the inbox. A follow carries no post,
  and picking one leaves those keys with nothing to act on rather than guessing.
- **`D` asks first.** Emptying the inbox takes away a list nobody has necessarily read and nothing brings it back, so
  it is confirmed on the same terms `post delete` is — the same confirmation, saying `clear` rather than `delete`.
- **A count and the list under it are one fact.** Arriving at a destination sets its badge from the same answer the
  screen is drawn from, and dismissing or answering something moves both.
- **A prompt taking letters takes `/` and `?` too**, which is the one exception to the frame keys above. A web address
  and a question are both things somebody is entitled to search for, and a prompt that could not take a slash would
  refuse the query most likely to be pasted into it. Every other frame key — `esc`, `ctrl-q`, `tab` — still means what
  it means everywhere, and the status row says what the prompt answers to.
- **`/` from the search screen's results starts a fresh prompt** rather than doing nothing, since that is the one place
  the key is most likely to be pressed twice.
- **A screen's own keys go in front of the shared ones on the status row.** The row is one row and a longer list is cut
  off at the right, so the keys a reader can find on no other screen are the ones that have to survive the cut.
- **A status row's key and its explanation are visually split**: the key stays `Role.Chrome`, the words explaining it
  take `Role.Muted` — reusing `Muted`'s existing "hints" job rather than adding a role — joined by a tight colon
  (`j/k:post`) in place of the plain space used before. The colon is the no-colour carrier and costs nothing: it is
  the same width as the space it replaces (#66). Brackets and capitalising the key were prototyped and rejected —
  brackets cost two columns per hint and collide with `‹reference›`'s bracket vocabulary below; capitalising would
  misrepresent a case-significant key (`d` deletes, `D` clears all). The confirmation and notice rows show no keys
  and are untouched. This is `KeyHint`'s own rendering, so a future `?` keymap screen inherits it for free.
- **A notice takes the whole row, so it goes as soon as it is spent.** The row holds a notice or the keymap and never
  both, which makes a remark left standing every key the screen answers to, hidden. So a remark goes when the reader
  walks to another thing — it was about the one they left — and when they do the thing it asked for, as toggling a poll
  answer does. It already went on `esc` and on arriving anywhere; those two are the same rule said earlier (#87
  follow-up).
- **`esc` is always up one level, of whichever kind of level is currently open** — amended from "up one level of the
  stack" now that a reference pick is a level of its own. With a reference picked, the first `esc` clears the pick;
  the next pops the screen (#64). This is the one addition ADR-0014's frame keys have taken since being settled.
- **A rail destination's screen says `tab` rather than `esc`**, because it is the bottom of the stack and there is
  nothing under it to walk back to.
- **A hashtag a search found opens as a screen on the stack**, not as the rail's hashtag destination. Which tag the
  rail keeps a place for is a setting the reader wrote down, and a search result is not them changing their mind.
- **`⏎` on a follow request opens whoever is asking**, because the question is about a person and the answer to it is
  on their account screen.
- **`⏎` inside a post opens anything on the thread but the post itself** — an answer below it, or an ancestor above it
  (#86). That one post is what the screen is already about, so opening it would push a copy of the screen the reader
  is standing on — and pressing again would push another, stacking duplicates and a breadcrumb of places nobody went
  (#48). It is left off the status row while that post is picked rather than announced and refused; every other key on
  the row still acts on it, since a post being read is still a post to boost, favorite, answer or take down. Which row
  that is is found by the post's id rather than by an ordinal: it stopped being the first thing on the screen when the
  ancestors landed above it, and a deletion higher up the chain moves it again.

### What a post's byline settled

A feed of short posts used to read as one undifferentiated column of text; the boundary between posts, and what a
post answers, are both drawn into the byline now (#62, #63):

- **A rule, not a blank line, separates two posts** — variant F of six prototyped on
  [`prototype/feed-separator`](https://github.com/jrmitch120/wooly/tree/prototype/feed-separator). Costs **+1 row**
  against the blank row it replaces.
- **The byline is two rows, not one**, with a 4-column avatar thumbnail beside both (after the shape `tut` draws):
  name, the audience/age tail, on the first; `@handle` on the second. Costs **+2 rows** (the split, plus the blank
  row the two-row shape wants between byline and body) and **+5 columns**, spent only on those two rows — the
  avatar and the gap after it. Body, media and counts stay full width.
- **`Counts` gets a blank row of its own** ahead of it, so it reads as a footer rather than one more line of the
  post. **+1 row.**
- **A post is a run of parts, and a blank row stands between each** — the byline, the text, *each attachment on its
  own*, the link preview, the counts. Generalised from the two blanks above once a post carrying two pictures showed the rule was
  needed at seams nobody had put one at by hand: the body ran into the first caption, and the first picture's last
  row into the second caption. One rule in `PostLines.Parts` rather than a `Line.Blank` per seam, so a part added
  later is spaced without anyone remembering to. **+1 row per attachment**, and nothing on a post carrying none —
  which is most of them. A part with nothing in it is skipped rather than separated, so a post that is a picture and
  no words does not open on a doubled blank.
- **A reply carries a `↳` mark above its byline**, in the slot the boost row already owns, all `Role.Muted`:
  `↳ answering @handle` (the common case, named off `Post.Mentions`, no extra fetch), `↳ continuing` (a self-reply),
  or the bare `↳ reply` (the answered account isn't in `Mentions`). If a post is both a boost and a reply, the boost
  row comes first. **+1 row**, only on a post that is a reply. Drawn on every screen that shares `PostLines.Feed` /
  `PostLines.Whole` — feed, post, conversation, search, direct messages, account — none suppresses it, **except**
  the post screen's own subject-post row once its ancestor is drawn whole immediately above it (below).
- **The picked post's `▌` gutter sits to the left of the avatar** — no collision with the avatar or the rule.
- **This needs `Post` to carry an avatar and a reply target that do not exist yet** — `AvatarUrl` (or similar,
  fetched and drawn through `IPictures` the way `Media` already is) and `InReplyTo` (`PostId`, and a `Handle?` that
  is `null` when unresolvable). Both are read back off the instance, resolved once in `PostWire.ToPost`.
- **Compose's own three-row reply preview adopts the same label**, replacing `Answering @handle:` with
  `↳ answering @handle` (no trailing colon) or `↳ continuing`, via `Shell.IsMine` — already computed where compose
  is pushed for `r`. The bare `↳ reply` variant never applies here: compose always holds the full post it answers,
  never a wire-level guess. The three rows themselves stay — ADR-0015's reason for them (seeing enough to answer
  accurately without leaving the screen) is a different job than this mark's, which is identification while
  scrolling, and nothing since cheapens leaving the screen. An amendment to ADR-0015's wording, not a supersession.

### What the post screen's ancestor settled

`PostScreen` shows a post's replies underneath it; it now shows what the post itself answers, above it, all the way
back to the thread's root (#72):

- **The whole ancestor chain, uncapped** — not just the immediate parent. Free: `PostEngagement` already calls
  `GetStatusContext`, discarding `context.Ancestors` while keeping `context.Descendants`; ancestors ride the same
  call, at zero extra cost. The port's method is `IPostEngagement.Thread` and it answers with a **thread**
  (`PostThread`, CONTEXT.md) — both halves around the post, since it was never one call's worth of question to ask
  what a post answers separately from what answered it.
- **Drawn whole**, via `PostLines.Feed`, the same way a reply already draws — not as the compact `↳` mark above,
  whose cost argument (one fetch per reply, on a whole timeline) does not apply to a single already-fetched screen.
- **Joins the same `Picked<Post>` list** replies already sit in: `[...ancestors, post, ...replies]`. Walked with
  `j`/`k`, opened with `⏎` — pushing a fresh `PostScreen` — through the same mechanics a reply already uses. No new
  interaction axis, and no overlap with reference-walking: an ancestor is a whole separate post, not text inside
  this post's body.
- **The default pick still lands on the subject post**, not the top of the thread — index `ancestors.Count`.
- **A `── {n} up ──` heading** stands above the post, mirroring `── {n} replies ──` below it, with a blank either side
  of it rather than a rule: a heading is already a ruled row.
- **The subject post's own `↳` mark comes off only where the chain came back.** Taking it off is paid for by the
  ancestor drawn whole immediately above it, so a reply whose chain came back empty — a deleted parent, an instance
  that did not send it — keeps the mark, which is then the only thing on the screen saying the post answers anything.
- **The subject post is found by its id, not by an ordinal.** Both of `PostScreen`'s index-0 assumptions — which post
  the screen is about, and which row `⏎` refuses — were wrong the moment anything was drawn above it, and a count
  taken once at construction would go stale the moment an ancestor was deleted out from under the reader.
- **The status row says `j/k:thread`** rather than `j/k:post · replies`, which stopped being all of what the walk
  reaches.
- **Costs one whole post's rows per ancestor** — the same bill a reply already pays, not a new category — plus one
  heading row when any ancestor exists.

### What references settled

`←` and `→` walk the things inside a post's text that point somewhere else. **Reference** is the word — a hashtag,
mention, or address inside a post's text — replacing `BodyText`'s internal "marks" language, which collided with
`Post.Marks` (boost/favorite/pin) (#64, #65):

- **Every screen with a post drawn on it**, not just feed and post — the breadth `PostKeys.OnAPost` has, plus the
  conversations list, where the row is a conversation and the post drawn under it is its last message. That one is
  `Screen.Referencing` rather than `Screen.Picked`: widening `Picked` there would have handed `d`, `b` and `f` a post
  the screen never offered them (#83).
- **Walkable**: hashtag, mention, and address — the three references `BodyText` finds — followed, since #109
  (ADR-0017), by every `Video`, `Animation`, `Audio` or `Unknown` attachment on the post, in attachment order, and
  since #116 (ADR-0018) by the post's link preview, last of all. **Not walkable**: an `Image` attachment, which is
  drawn or linked but never opened this way; the name a link preview says wrote the page, which is plain text and
  never an address; text still behind a content warning, which has no references until `x` shows it, since the
  brackets marking a pick would be behind the warning too (#83); and every attachment on a **warned** post, along with
  its link preview, for the same reason since #113 — its label is behind the warning with the rest of what is hidden,
  so `←`/`→` would walk to something nobody can see and `⏎` would open a video the reader never asked for. The two
  halves are asked separately: a post marked sensitive with no warning written over it shows its text, so what is
  written in it goes on being walked while its attachments do not.
- **An attachment reference carries no place in the post's text.** It is appended after every one `BodyText` found,
  in the order the attachments themselves were sent, and it is drawn on `PostLines`' own path rather than sliced out
  of a wrapped row — the kind's own name (`MediaKindName.Written`, capitalized) is the whole of the walkable span, with
  the author's own description alongside it where they gave one. The raw address is not printed at all; it is only
  ever what `⏎` opens (#109).
- **A link preview's reference is placed the same way, after every attachment's** (`LinkPreviewReference`, #116), and
  its walkable span is the page's title — the site's own name where the instance sent no title, and the address itself
  where it sent neither, since the address is the whole reason a preview is walked to. Its address will usually be one
  a `link` reference in the post's own text already reaches, and it is walked anyway: a title is a pointer to the
  article rather than the article, and hunting a long post for the matching link is not an answer (ADR-0018).
- **Matched once per post, on the flattened text before the wrap** rather than per wrapped row, which is what gives
  them an order and a place to be walked by. `TextWrap` carries each row's offset into that text and every row is a
  slice of it at that offset, so a row's spans are the post's references sliced by its own range. An address longer
  than the content region used to be cut into two halves that each matched nothing and drew as prose; it is now one
  reference drawn across two rows (#83).
- **`→` enters at the first reference, `←` at the last**; further motion in the same direction at either end
  **clamps**, matching `Picked<T>`'s existing convention rather than wrapping.
- **`esc` clears the pick first**, popping the screen on the next press (above). **`j`/`k` clear it too**, since the
  reader has left the post. **`↓`/`↑` (page scroll) do not** — they leave the selection alone by the existing
  "What moving settled" contract, and a reference pick lives inside the selected post, not on a row.
- **Bracketed `‹reference›`** — brackets, always drawn, in colour and no-colour terminals alike. ("Marked" is the
  word `Post.Marks` has, which is the collision the rename was about — CONTEXT.md.) The brackets take
  their own role (`Role.ReferencePicked`), independent of whatever role the bracketed text already carries, so a
  picked hashtag stays hashtag-coloured and only the brackets shift. Underline was considered — a separate SGR
  attribute, zero width cost — and set aside in favour of brackets; the two added columns on the one row a pick
  lands on are an accepted cost.
- **The status row swaps** to a reference-mode row while one's picked — `←/→ reference · ⏎ open · esc back`-shaped,
  ahead of the screen's shared keys, and standing in for any of them it shares a key with, so `⏎` is announced once
  (`PostKeys.OnAReference`). Said by `Screen` itself rather than by each screen, which is why a screen's own list is
  `OwnKeys` and `Keys` is what the status row reads (#83).
- **`⏎` does four different things, refusals share one notice** (#85, #109). Which of the four is the role the
  reference draws in, since that vocabulary already tells them apart. A hashtag opens exactly the way `Shell.OpenTag`
  already opens one found by search — same `FeedScreen`, same breadcrumb, no new screen type, and the rail's own
  hashtag destination left alone. A mention opens the account screen, resolved off `Post.Mentions` — which the wire
  carries down with every post, so no fetch is spent working out who a `@maria` is; a handle written bare is whoever
  the post names by that username, and where two accounts share one the first the post lists wins. Unresolvable, `⏎`
  does nothing and the status row says **"That mention couldn't be resolved."** `c` with a mention picked opens a
  fresh compose (not a reply) with `@handle ` pre-filled — in full where the post resolved it, since a bare handle
  written back out would reach whoever this profile's own instance has by that name, and as written where it did not;
  `a` still means the post's author, never the picked mention. An address, and an attachment's own address alike, open
  the platform's browser (Windows/macOS/Linux, each its own call) for `http`/`https` only — a refused scheme says
  **"That kind of address isn't opened."**, no browser available says **"No browser available."** — both through the
  shell's existing `Say(notice, isError: true)` mechanism, no new shell state. Both address arms are the ones that
  push nothing: the reader has been sent somewhere this client does not draw, so there is nothing to `esc` back from.
  An attachment's own address needs no elided-form or scheme check the way body text does — it arrives off the wire
  already well-formed (`PostMedia.Url`) rather than matched by pattern out of prose — so it goes through the exact
  same `Shell.OpenAddress` call a picked link already does, refusal for refusal.
- **The launch is decided apart from the act, and outside `ShellPorts`.** This is the first thing in the shell that
  leaves the terminal, so it lives in its own small seam rather than folded into the ports (which are specifically
  "everything the shell reaches an *instance* through" — a browser reaches outside the instance). `BrowserLaunch`
  holds both decisions with no process in them — which addresses are opened at all, and what each platform is asked
  to do about the ones that are — and `IWebBrowser`, the seam the OAuth sign-in already sends somebody to a browser
  through (ADR-0004), is what runs it. That is what makes a hostile scheme and a wrong platform call both assertable
  without a process ever starting, the way role selection is the assertable part of drawing (ADR-0014). What is
  painted as an address is matched by pattern, so what arrives is not necessarily an address at all: the elided forms
  an instance serves (`example.com/notes`, `www.example.com/notes`) are read as `https`, and an address handed over
  that names a scheme and does not name one of the two is refused rather than repaired. A colon before the path is a
  port where digits follow it and a scheme where anything else does — `Uri` alone reads `www.example.com:8080/notes`
  as a scheme called `www.example.com`, which would refuse an ordinary page for having a port on it.
- **What this costs**: nothing permanent. The pick itself spends two columns, transiently, only on the row a
  reference is picked on.

### What a poll settled

`Post` was write-only for a poll — `Role.Poll` was themed and documented with nothing that ever emitted it. Reading
one back, and voting on one, are both built now (#69, #74):

- **`Post` gains a read-side pair**, `PostPoll`/`PostPollOption`, matching `MediaAttachment`/`PostMedia`'s mnemonic.
  The existing write-side type a draft carries renames `PostPoll` → `PollDraft`, freeing the name.
  `PostPollOption.Votes` is `long?` — `null` is a real third state, distinct from a genuine zero, for the instances
  that withhold the per-option breakdown until this profile votes or the poll closes.
- **Drawn on both the feed and the post screen**, full detail on both, and in the CLI's `PostReport.Write`: a
  ~10-cell `▓`/`░` block bar per option carrying `Role.Poll` (unchanged in the contract — nothing new needed there,
  beyond a leading `✓ ` marking a picked option), percentage and raw count beside it (`▓▓▓▓▓▓░░░░ 62% (145)`), `0%`
  and an empty bar for a genuinely unvoted option versus no bar at all for one whose count is withheld, `Closed` in
  place of the end-time line when shut, silence when there is no end date, and a muted "choose as many as you like"
  line when multiple-choice.
- **Voting takes digits, not a new walkable axis.** `1`-`9` then `0` address up to ten options directly on the
  picked post; a digit toggles a local unsent selection — `[x]`/`[ ]` in place of the bar's leading mark — the same
  mechanic for single- and multiple-choice (single-choice toggling is exclusive: picking a new option clears the
  last). `j`/`k`/`esc` discard an uncommitted toggle, the same rule references use for a picked reference above.
- **`v` casts it**, through the existing `Confirmation`/story-43 pattern — a cast vote qualifies more than delete
  does, since the API refuses a second vote outright rather than allowing recovery. The two keys are on the status row
  only while the picked post carries a poll **that would still take a vote** — one that has not closed and that this
  profile has not already voted in (`PostPoll.TakesAVote`) — the same swap a picked reference already makes and for the
  same reason: a key announced where it does nothing reads as a shell that missed the press. A picked reference wins
  over a poll, being the level the reader is standing on. An answered poll is a result to read: the digits do nothing
  there, and `v` says which of the two reasons it is rather than nothing at all, the way `m` answers on a conversation
  already read. Whether a vote would *land* is still the instance's (ADR-0009) — this is only about what is offered.
- **The question names the answer, not the post.** `Vote for "Dogs"? This cannot be undone.` — what a vote can be
  wrong about is which answer it is for, and the id of the post the poll happens to be on answers a question nobody
  voting has (unlike a delete, where the id is the whole risk). Several ticked answers are counted rather than listed
  (`Cast the 3 answers you ticked?`), and one answer is clipped at 25 columns, because the question takes the status
  row and `y vote · esc keep` takes what is left: a question that pushes the answer key off the right is one nobody
  knows how to answer. Nothing is lost by counting — the ballot is on screen with every agreed answer drawn `[x]`.
- **The ballot says how to cast it**, in a muted row of its own under the boxes: `v casts this vote, esc discards it`.
  On the poll rather than only on the status row, because this is the one moment in the shell where a key has to be
  found rather than remembered — the reader is looking at the boxes they have just ticked, not at the foot of the
  screen. It costs one row, and only while a vote is standing uncast.
- **No refetch.** `POST /api/v1/polls/:id/votes` returns the complete updated poll in the same response; that feeds
  the same `Replace(...)` call `Mark` already uses.
- **`Vote(...)` lands on `IPostEngagement`** beside `Mark`/`Show`/`Replies` — and is the one call there that takes the
  post rather than its id, because Mastodon votes on the *poll*, whose id is not the post's and is only knowable from
  the post itself. A reader who can see the options they are voting on is already holding it, so the TUI pays one call;
  the CLI, which holds an id, reads the post first. The CLI gets `post vote <post-id> <choice>...`, joining the
  boost/favorite/pin command family, confirming via `Consent.Given`, and numbering the answers from 1 as they are
  printed rather than from the zero the API counts by.
- **A refusal is a notice, not a crash.** An instance that will not take a vote — a second one, above all — says why,
  and that answer is named as a failure of this client's own (`VoteRefusedException`) so the shell draws it over what
  the reader was reading. It is the one refusal in `PostEngagement` that is retyped rather than passed on; nothing else
  there can be refused in a way the reader cannot simply try differently.
- **A role-emission contract test is worth having** — one that walks every `Role` and asserts some view can produce
  it, the thing that would have caught `Role.Poll` sitting dead in the contract in the first place.

### What refresh settled

There was no way to ask a destination for fresh posts short of leaving and coming back, or waiting out the 1-minute
cache. Streaming stays out of scope (below); a manual refresh is the in-scope answer (#68):

- **`g`, screen-local** — not a frame key, no `F5`, no dual binding; this shell has no existing precedent for either,
  and both would cut against internal consistency. The status row shows `g refresh` only on a screen that has one.
- **Lives on nine destinations**: the four cached feed destinations (home, local, federated, the hashtag), plus
  Notifications, Messages, Requests, the post screen, and the account screen. The conversation screen and search
  results are left out — a live thread and a live search are each their own, smaller question, not decided here.
- **Evicts the destination's cache entry, then re-runs the same fetch its own arrival runs** — `Arrival.At()` for the
  four feeds and Notifications/Messages/Requests, which is one arrival for all seven (#100), `Replies` for the post
  screen, both of `OpenAccount`'s calls for the account screen.
- **It opens at the top, on the newest of what came back.** Nothing about where the reader was standing is carried
  over — not the scroll offset, not which post was picked. That is the whole of what the key is for: somebody pressing
  `g` is asking to see what has arrived, and what has arrived is above everything they have already read. A refresh
  that held their place would fetch the new posts and leave them off the top of the page, which is fetched and
  invisible. So a refreshed screen is a screen replaced, and a screen replaced starts its offset again (below) with its
  first thing picked out — which needs no special case at all, and is why there is none.
- **Nothing is awaited, and `Shell.Refresh` answers with nothing.** Everything a refresh does happens in a callback the
  host queues onto the main loop, which runs *after* the task `Refresh` hands back has completed. A flag set in that
  callback and returned across the await is read before it is written — always false in a terminal, always true under a
  test fake that runs the callback inline — so a refresh must not report anything that way. The window hears the answer
  as a screen change on `Changed`, in the right order and on the drawing thread, the same way it hears every other
  screen change.
- **The badge moves with the count**, from the same answer the screen redraws from — the same rule every other
  arrival already follows.
- **A refresh goes through `Enquiry` like every other fetch**, discarded unread if the reader has moved on. No new
  in-flight UI beyond the breadcrumb's existing `fetching…` marker; a second `g` while anything at all is in flight is
  a silent no-op — the guard is the breadcrumb's own `Fetching`, since a refresh landing on top of a boost or a
  deletion still in flight is the same stale answer by another route.
- **What is on screen stands until there is something fresher to put in its place.** An arrival puts an empty screen
  up at once because what was showing is about somewhere the reader has left; a refresh is the one case where that is
  not true, so it takes neither that step nor the overtake — nothing is in flight to overtake, since the key is
  refused while anything is. A refresh a rate limit or a refusal ends is then a notice over the list they were
  reading rather than an empty screen where it used to be, with the cache already evicted. This is the only place a
  destination is read without `Arrival`'s first two steps, and it is `Arrival.Again` rather than a second reading of
  the same table.
- **The post and account screens are replaced where they stand**, rather than pushed or reset: nobody has gone
  anywhere, so what was drilled through to get there is still under them and `esc` still walks back out of it. Neither
  is reached through an arrival, so neither is overtaken by one — each rechecks that the top of the stack is still the
  screen it was asked about, the same idiom `Find()` and `OpenResult()` use. Every refresh builds a new screen rather
  than changing the one on the stack, which is what starts the scroll offset again: the view notices a screen has been
  replaced by identity.
- **A hashtag walked to from a search has no refresh**, though it is the same `FeedScreen` the rail's own hashtag
  destination opens onto. Which of the two a screen is cannot be read off what is in it — a tag the reader named and a
  tag they walked to are the same destination by value — so it is settled by who built it: an arrival's feed refreshes
  and a pushed one does not. It is out of scope with the search results it was opened from.

### What media settled

Media is drawn in place inside a feed item or a post, at whatever width the content region has (ADR-0016):

- **Video, audio, an animation and anything this client has no word for get a `⏵`**, its kind's own name capitalized —
  walkable and, since #109 (ADR-0017), what `⏎` opens ("What references settled") — with the description alongside
  where its author gave one. No raw address is printed for these anymore: it is only ever what `⏎` hands the browser.
- **A video's and an animation's own preview is drawn in a box under that label** (#110, ADR-0017), through the exact
  `Drawn`/`Inset`/`IPictures`/`PictureView` pipeline a picture goes through and gated the same way on the terminal
  offering sixel or Kitty. `PostMedia.IsDrawable` is what says so, and it is no longer `Opens`' opposite: a video is
  both drawn and walked. ADR-0016 refused the frame because there was nothing to say it was meant to move; the
  permanent label beside it is that something. It is always exactly one still picture — nothing autoplays, loops, or
  is decoded in this process.
- **A `Video`/`Animation` with no preview, and every `Audio`/`Unknown`, stays label-plus-description.** A video's own
  file is motion rather than a picture, so sending for it would fetch a whole video to fail to decode it; cover art on
  a sound is not a frame standing in for motion and does not earn a box, and `Unknown` cannot promise a box means
  anything at all (ADR-0017). Neither is a case in `PostLines` — both are just `IsDrawable` answering no.
- **The label never moves and never hides**; only the description under it does, behind `hide_drawn_caption` and only
  once the preview has actually landed. Pending, or never coming, and the description stands.
- **A still picture that cannot be drawn is unaffected by #109**, and keeps the shape every attachment had before it: a
  `⏵`, the description, and the address on the rows below — wrapped rather than clipped, since a real address is
  longer than 61 columns and a link with its end cut off is not a link. `Image` never joins the walk, so there is
  nothing here for `←`/`→` to reach.
- **A warned post's attachments are drawn only once the reader has asked for them.** A spoiler text, the instance's own
  sensitive flag, or both, and `x` shows them along with whatever else the post is hiding (#113). Until it is pressed
  there is no box, no label, no description and no address — and no `Wants`, so nothing is fetched and nothing is
  decoded, which is the point rather than a side effect: scrolling a feed of sensitive posts costs no data for pixels
  nobody asked to see. A post carrying only the flag says `⚠ Sensitive media` and `x  show it` where its attachments
  would be, because a post already showing a warning is already asking and a post showing neither would be hiding
  something with nothing on screen to say so. Since #116 the flag covers a **link preview** on the same terms, whether
  or not anything is attached beside it — so that one prompt stands above everything a flagged post is holding back.
- **Sixel is preferred over Kitty, and the preference is subscribed to.** Both capabilities are answers the terminal
  sends back some frames after startup, so a preference set once at startup is set against nothing and then overwritten
  (ADR-0016).
- **There is no cell-based fallback.** A terminal offering neither sixel nor the Kitty graphics protocol links every
  attachment, a photograph included, exactly the way the CLI writes one. The coloured-block rendering the ticket asked
  for was built and rejected on the evidence: a photograph as a few dozen rectangles resembles nothing and is worse
  than the description it replaced.
- **A picture is drawn at the full width of the column it is in, at its own proportions**, capped at 16 rows in a feed
  item and 32 on the post screen. Width-driven rather than height-driven — that is the difference between an inline
  picture and a postage stamp.
- **The description stands above the picture**, so it does not move when the pixels land under it. Until they do there
  is no box: a picture on its way is its `▒▒▒▒` description and nothing else.
- **Nothing about a picture is ever an error.** A fetch that fails and a file that will not decode both leave the
  description standing on its own, which is what a terminal that cannot draw shows anyway.
- **A drawn picture's caption can hide, behind a preference that defaults off.** `hide_drawn_caption` (below) hides
  `Described` only once a picture is *actually drawn* — the same branch that emits `Box(inset)`. A terminal that
  cannot draw at all and one that can but hasn't gotten this picture yet are treated identically, so there is no
  arrival flicker to weigh. Applies the same to a feed item and the post screen; there is no reveal key once
  hidden — the preference itself is the opt-in (#71). Since #110 it hides a video's or an animation's *description*
  on the same branch and on the same terms — never its label, which is what `⏎` acts on rather than a caption.

### What a link preview settled

What an instance made of a link the author already wrote into a post — a title, a site name, a description, sometimes
a picture — is drawn after everything the author attached (#116, ADR-0018):

- **Text, then attachments, then the preview**, which is the order Mastodon's own web UI uses. Nothing in its docs says
  a post carrying attachments is never sent a preview too, so both are drawn and the order between them is settled now
  rather than found out later. It is a part of its own in `PostLines.Parts`, so a blank row stands ahead of it — **+1
  row**, and nothing on a post the instance previewed no link in.
- **The title is the row that is walked**, behind the same `⏵` an attachment's label carries and bracketed the same way
  while it is picked. The site's name stands in for a title the instance made nothing of, and the address itself where
  it sent neither. Under it, indented past the mark and all `muted`: the site, the description, and `by ` whoever the
  page says wrote it — one row each, clipped rather than wrapped, and nothing at all for whatever the instance did not
  say.
- **The author's name is plain text and is never walked to.** Two things opening the same place was the trade already
  made for the preview's own address; a third, which usually differs from it and rarely matters enough to open, is
  where consistency with attachments stopped being the stronger argument (ADR-0018).
- **No address is printed on any of those rows** — the shape a `Video` label already has since #109, not the wrapped
  URL rows an undrawn `Image` still gets. The address is what `⏎` hands the browser, and the rows an undrawn `Image`
  prints are for the one thing on a post that is *not* walked to. The CLI, which has no `⏎` to offer, prints it
  instead (#117).
- **Its picture goes through the same `Drawn`/`Inset`/`IPictures`/`PictureView` pipeline an attachment's does** — same
  width-driven box, same 16/32-row cap, same nothing-at-all on a terminal offering neither sixel nor Kitty, where what
  is left is the words. `Drawn.LinkPreview` names it by the *link's* address rather than the picture's, the way an
  avatar is named by its handle: the same article shared by two accounts is one picture however each instance spells
  the proxy it serves the pixels through.
- **`hide_drawn_caption` does not touch it**, which is the one thing it does differently from an attachment. That
  preference drops what a picture says *it shows* once the picture is on screen saying it (#71); a preview's
  description is about the page rather than about the picture beside it, so a box landing under the words does not
  stand in for them.
- **A warned post's preview is behind the warning with its attachments** — no title, no site, no description, no
  author, no box and no `Wants`, so nothing is fetched for it either, until `x` (#113). Nothing extra is said in place
  of it: a post hiding anything is already showing its author's warning or the `⚠ Sensitive media` prompt, and a second
  one under that would be the same offer made twice.
- **The sensitive flag counts a link preview as something to hide, attachments or not** (#116, ADR-0016's second
  amendment). The carve-out #113 wrote — the flag means nothing on a post carrying no attachments — was reasoning about
  a post with nothing but words on it; an instance picks a picture for a preview and serves it the same way it serves
  an attachment's, so a flagged post carrying only a preview was a flagged picture drawn full width with nothing asked
  first. The *whole* preview goes, image or no image: the reader asked to see nothing of what the post is holding, and
  whether a preview has a picture is not a second question for `IsWarned` and `PostLines` to answer differently. The
  post's own text is untouched — the flag is not a warning its author wrote about what they said.

### What moving settled

`j`/`k` and `↓`/`↑` were one key until a post with pictures on it grew taller than a terminal, at which point the
selection was the only scroll position a screen had and the foot of such a post could not be reached at all (#51):

- **The screen has a scroll position of its own, and the reader owns it.** `↓` and `↑` move it and leave the selection
  where it is; `j` and `k` move the selection and ask for it to be scrolled back into view. Between those presses the
  offset is whatever the arrows made it, so `Scroll.To` answers a request rather than every frame.
- **An arrow press is a wheel notch, not a row.** Three rows, because a picture is sixteen and a row a press is
  sixteen presses to get past one — and it is the post nobody could reach the foot of that these keys exist for. The
  number is one constant in `ShellWindow`. The far end of the scroll is clamped when the rows are drawn rather than
  when a key is pressed, since working them out to clamp against would lay out every post on screen twice for one
  keypress, and that cost is what a reader feels as a slow scroll.
- **`j` and `k` reclaim a selection that has scrolled off screen.** With no row of the selected post on the page, the
  next `j` or `k` selects the topmost post on the page instead of moving from a post the reader can no longer see;
  pressing it again moves normally from there. A post whose top has scrolled off but which still has rows showing *is*
  the topmost post, so `↓ ↓ ↓ j` picks out the post being read rather than the one after it — and a page scrolled past
  the last post entirely, which is the blank under it, reclaims that last post rather than nothing.
- **A row says which post it is part of.** `Role.Selection` marks only the post already picked out and so cannot name
  any other; every screen holding a selection numbers its rows with the same ordinal `Screen.Pick` takes, including the
  four that do not number a plain list of posts — the post screen, where the ancestors come first and the post itself
  is at `ancestors.Count` (#86), search across its three kinds, notifications, and direct messages.
- **The offset starts again whenever the screen is replaced, with no exceptions.** Pushing a screen, popping back to
  one, arriving at a destination and refreshing all mean different rows, and an offset made on the last lot says
  nothing about this one — so it starts at row 0 and follows the pick again (`Restart`). On every screen but one that
  is also where the pick is, since they open on their first thing; the post screen opens on a post with its ancestors
  above it, and following the pick is what carries the page down to it on the first draw rather than opening the
  reader onto the top of somebody else's thread (#86).
- **`PgUp`/`PgDn` walk the screen, `Home`/`End` walk the selection.** A page is a screenful of rows, because that is
  what a page is: somebody asking for the next one is asking about what they are looking at, not about how many posts
  happen to be on it. They used to move the selection by ten posts, which on a feed with pictures on it was several
  screens at once. The ends of a list are things rather than places, so `Home` and `End` still pick out the first post
  and the last, and neither of the four reclaims anything — a reader asking for the top of the list is not asking about
  the page they were on.
- **`k` is the next post and `j` is the one before it**, which is the opposite way round from vim. Asked for
  deliberately, and written down because the vim reading is the one anybody will assume — so a future "fix" would
  silently reverse what `j` does on every screen in the shell.

### What conversations settled

The last two screens, and the one place a screen writes words of its own into what a reader is sending:

- **Opening a conversation does not mark it read.** `⏎` shows the thread and leaves the mark exactly as it found it;
  `m` is the only thing that clears it (ADR-0013). A client that cleared it on the way past would make "what have I not
  read" unanswerable for anything that looked afterwards — including this shell's own badge.
- **A conversation is marked read by its own id**, which is not the id of any post in it (CONTEXT.md). The same id
  opens it, and the two screens holding it — the list and the thread pushed from it — are both moved by one answer, so
  a row cannot still say `unread` under a thread just marked.
- **A reply in a conversation opens with the mention already in it.** Mastodon delivers a direct post to the accounts
  its text mentions and to nobody else, so a reply that named nobody would reach nobody. It is written where the reader
  can see and edit it rather than added silently on the way out, by the same `DirectMessage.To` that `dm send` uses —
  and a reply that is nothing but the mention it opened with is refused as nothing written. Every account in the
  conversation is named, not only whoever spoke last; an address this client cannot parse is left out of the mention
  rather than thrown over the reply, where the reader can see that it is missing. Nothing is added to a reply that is
  not direct.
- **A reply lands at the end of the thread it answers, and on the row it was opened from** — rather than appearing
  nowhere until the conversation is read again. A conversation is read in the order it was said in, what was just said
  is part of it, and it is the conversation's last word as well as the thread's last message.
- **The unread indicator is the word, not a glyph.** This client's glyphs already say who can see a post — `○ ◌ ● ✉` —
  and a second circle beside `●` is one mark too many to tell apart at a glance. The word takes `rail-unread`, the same
  role as the badge counting it on the rail.

## Starting it, and the one destination that needs configuring

`wooly-tui` takes one option, `--profile <name>`, and it means what it means everywhere else: act as that profile for
this run, without changing which one is current (story 9). Everything else about the profile — which instance, which
token — is resolved through `IProfileRegistry` exactly as a command's scope resolves it.

Eight of the nine destinations are the same eight for everybody. The ninth is a hashtag, and which one is nobody's
business but the reader's, so it is a setting in the same TOML file everything else lives in (ADR-0003):

```toml
[preferences]
hashtag = "dotnet"
hide_drawn_caption = true
```

`hide_drawn_caption` is the other reader-owned preference in this section (#71) — a picture's caption, `false` (the
default) shows it always, `true` hides it once a picture is actually drawn, per "What media settled" above.

With none set, the destination is still on the rail — it says no tag has been named and asks the instance for nothing,
rather than being a rail entry that swallows a keypress.

## Roles

A view names a role; the theme resolves it to an attribute. Nothing constructs a colour (ADR-0014). Each role has a
glyph or a position that carries the same meaning when colour is gone.

| Role | Paints | Carried without colour by |
|---|---|---|
| `body` | A post's text | — |
| `hashtag` | A tag inside a post's text | the `#` |
| `mention` | An account named inside a post's text | the `@` |
| `link` | An address inside a post's text | the scheme |
| `muted` | Timestamps, counts nobody acted on, hints, a status row key's explanation | position |
| `byline-name` | A display name | position |
| `byline-handle` | `username@instance` | the `@` |
| `audience` | The visibility mark | `○ ◌ ● ✉` |
| `content-warning` | A warning and its text | `⚠` |
| `media` | Image placeholders, attachment links, a link preview's title, and the columns a byline holds for an avatar | `▒▒▒▒`, `⏵` |
| *(none — a picture's own pixels)* | A drawn picture | it is the picture |
| `poll` | Options and their bars | the bar itself, and `✓ `/`[x]` marking a picked one |
| `reference-picked` | The brackets around a picked reference | `‹ ›`, always drawn |
| `boost` / `boost-mine` | The boost mark, and it when it is yours | `↺` (open) vs `⥀` (closed) |
| `favorite` / `favorite-mine` | The favorite mark, and it when it is yours | `☆` (hollow) vs `★` (filled) |
| `selection` | The selected row | `▌` in the gutter |
| `rail` / `rail-current` | Destinations, and the one loaded | one glyph, one column: `▶` where the tabbing has got to, `▷` where it settled if that differs — they coincide at rest, so only `▶` shows |
| `rail-unread` | An unread count, and the word on an unread conversation | the number, and the word |
| `quota` / `quota-low` | Rate-limit budget left, and nearly spent | the number |
| `chrome` | Breadcrumb and status rows | position |
| `loading` | The `fetching…` mark on the breadcrumb | the word itself |
| `destructive` | A delete affordance and its confirmation | the word |
| `error` | A failure the shell has to say out loud | the word |

Role selection is testable without a terminal and is expected to be tested: *a post of mine offers delete in the
destructive role*, *an unread conversation's badge takes `rail-unread`*. Drawing is not tested (ADR-0005, ADR-0014).

The table above is the contract, and the three places it is written down — this table, the `Role` enum, and the
`RoleName` table of what each one is called in a config file — are checked against each other by a test rather than by
a reader.

The one thing that carries colour without naming a role is a drawn picture, whose pixels are the content rather than an
emphasis somebody chose — there is no sense in which `dark` and `light` would answer them differently. The scan that
enforces "no view constructs a colour" names the one file allowed to (ADR-0016); every screen is still caught.

`muted` is the broad one — timestamps, hints, counts, empty-list notices, editor chrome — and stays broad on purpose. A
themer cannot make an empty-list notice dimmer than a timestamp, and that is a smaller loss than a vocabulary nobody
can hold in their head.

The rail used to reserve two columns — `▶` for the cursor, `▸` for the selection — and showed them adjacent almost
all the time, since the two coincide at rest and differ only for the ~250ms settle window. It now reserves one:
`▶` (filled) on the cursor's row, `▷` (hollow, U+25B7) on the settled row only while the two differ, extending the
audience row's filled/hollow vocabulary (`○`/`●`) rather than teaching a third shape. This retires the no-colour
risk the old scheme carried outright rather than mitigating it: the design never reads `Role.RailCurrent`'s band for
"which one's current", so `NO_COLOR` and a themed terminal show identical marks. The freed column goes to the
destination label (#67).

### The three inside a post's text

`hashtag`, `mention` and `link` are found in the flattened plain text of a post's body, at the one place a body is
wrapped, so the feed, the post screen, a conversation and the notification list all draw them without knowing about
them. Three things follow from where they are found:

- **A mention is not a `byline-handle`.** The byline is who wrote this; a mention is somebody else being named. A theme
  that wants them alike writes the colour twice. `link` is likewise not `media`, which paints attachment links.
- **An address is matched by pattern**, since an instance elides part of the one it displays: a scheme, a `www.`, or a
  domain with a path on it. So a bare domain somebody typed as prose is painted as a link, and a domain with nothing
  after it — `Node.js`, `config.toml` — is not. The imprecision is deliberate and costs nothing but colour.
- **The compose editor stays plain.** The three patterns start matching at three different points in a word, so text
  would change colour under the cursor and a would-be mention would light and go out again; and this client has not
  resolved what somebody is halfway through typing, so a role there would assert something it has not checked.

## A theme

Themes are tables in the same TOML config file everything else lives in (ADR-0003). Two ship built in, `dark` and
`light`; a user's own is another table.

```toml
# The theme the TUI uses. A built-in name, or one defined below.
theme = "dark"

[themes.midnight]
background      = "#12111a"
body            = "#d5d2e0"
muted           = "#7c7891"
byline-name     = "#f2f0f7"
byline-handle   = "#8fa8ff"
content-warning = "#e0af68"
hashtag         = "#6fcf97"
mention         = "#e0af68"
link            = "#8fa8ff"
boost           = "#6fcf97"
boost-mine      = "#9ef2b8"
favorite        = "#c58fe8"
favorite-mine   = "#e0b6ff"
rail-unread     = "bright-red"
destructive     = "#ff7a93"

# A role may set its own background; a half it leaves out keeps whatever it was overriding.
[themes.midnight.selection]
foreground = "#f2f0f7"
background = "#2a2942"
```

Rules:

- A colour is a hex triple (`#8fa8ff`) or one of the sixteen ANSI names (`red`, `bright-blue`, …). Named colours let a
  theme follow whatever the terminal's own palette is set to; hex does not. ANSI's `white` is the dim one a terminal
  writes its text in — the bright one is `bright-white`, and `bright-black` is the dark grey.
- `Terminal.Gui` quantises hex to the nearest of 16 on a 16-colour terminal, so a theme is authored once.
- A theme is an override rather than a complete set, so adding a role later does not break every user's config. Any
  role it leaves out falls back to the built-in it is read against: the one whose brightness its `background` matches,
  or — for a theme naming no background — the one it shares a name with, so a `[themes.dark]` table is the built-in
  `dark` with changes on top. The page beats the name because the failure being guarded against is the one a fallback
  must never produce: a theme naming a light page and nothing else, drawn in light text.
- A role may be a colour or a table of `foreground` and `background`. A half it leaves out keeps what the built-in had
  there: the theme's page for nearly every role, and its own band for the selected row and the current rail entry — so
  restating the selection's foreground does not silently take away the band it is drawn in.
- `background` is the theme's, not a role: setting it moves everything that was sitting on the page. A theme cannot
  decline to have one and inherit the terminal's own — `Terminal.Gui` attributes are a foreground/background pair with
  no "leave it alone" in them, and its own default pair is a concrete white on black rather than a sentinel. So the
  page is always written down: this theme's, or the built-in's.
- A theme naming a role that does not exist is a config error with the role named, not a silent no-op — and so is a
  colour this client cannot read, and a `theme = "…"` naming a theme nobody wrote. Every theme in the file is read,
  not only the one in use, so a typo is reported the day it is written rather than the day it is switched to.
- `NO_COLOR` and `TERM=dumb` beat `theme = "…"`, always: every role resolves to one pair and the glyphs above carry
  everything. The file is still read on such a terminal, so that a mistake in it is reported to everybody rather than
  only to the readers whose terminals happen to have colour.

## Fetching, since the rail loads on arrival

`tab` walks the rail and what it lands on loads (ADR-0014). Three rules keep that affordable, and none of them makes
the keyboard feel slow:

- **The cursor moves on the press; the selection moves when the pressing stops.** A press moves the cursor and restarts
  a settle window (~250ms) that every later press abandons. When it closes, the selection follows the cursor and that
  destination — only that one — is fetched. Six tabs are six cursor moves, one selection and one fetch.
- **A destination is cached for a short while.** A step onto one fetched recently draws immediately and asks for
  nothing, so walking out along the rail and back is one fetch per destination rather than one per arrival.
- **An overtaken fetch is discarded, never drawn.** A reader who has moved on must not have a stale timeline appear
  underneath them.

The rail carries one column for this, in the left column with the destination names, whose glyph depends on the row:
`▶` where the tabbing has got to, `▷` where it settled if that differs, blank otherwise — the two coincide at rest,
so only `▶` shows (#67, amending ADR-0014's earlier two-column, two-mark description below). It carries no third
mark for *chosen but not loaded* and none for a fetch in flight — the right-hand column is unread counts and nothing
else, and a fetch is announced once on the breadcrumb. A rail somebody is reading should hold still.

The alternatives were built and measured — a cursor that moves free until `⏎` commits, a key per destination, a jump
list — and all cost one fetch against cycling's six *before* the settle rule, which is what closed the gap. They are on
the prototype branch (`SCREENS-C.md`) if the decision is ever revisited.

## The numbers

Settled in #28. All three live in one place in the code (`ShellTiming`), so a reader looking for them finds them
together.

| What | How long | Why that |
|---|---|---|
| Settle window | 250ms | Long enough that a deliberate double-tap lands as one move; short enough that a single tab does not read as a pause. |
| Destination cache | 1 minute | Long enough that walking out along the rail and back is free; short enough that a timeline left and returned to a minute later is fetched rather than remembered. This client forgets a destination early when it is the thing that changed it — a post published, deleted or marked. |
| Countdown step | 1 second | The unit a rate-limit countdown counts in. |

One cache age for everything, rather than one per kind of destination. The question the cache answers is "is this still
the timeline I just left", not "is this still current", and that has the same answer wherever you left from.

## Open questions

1. ~~**Whether a theme can decline to set a background** and inherit the terminal's own.~~ Settled by #46: it cannot.
   A `Terminal.Gui` attribute is a foreground/background pair with nothing in it meaning "whatever was there", and its
   `Attribute.Default` is a concrete white on black rather than a sentinel the driver reads as "leave it". So a
   background is always written down — the theme's own, or the built-in's — and a terminal that wants no colour is
   answered by not colouring anything (`NO_COLOR`), which is a different question and already settled above.
2. ~~**Where compose lives.**~~ Settled by ADR-0015: a screen on the stack, like everything else. A reply draws the
   first rows of what it is answering above the editor, which is the part of the split region that was worth keeping.
3. ~~**How long the settle window and the cache should be.**~~ Settled above.
