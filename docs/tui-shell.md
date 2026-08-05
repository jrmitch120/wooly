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
| Feed — home, local, federated, a hashtag | A rail destination | #28 |
| Post — the post whole, with its replies | `⏎` on a feed item | #28 |
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
| `tab` / `shift-tab` | Moves the cursor (`▶`) at once. The selection (`▸`) follows it, and that destination loads, once the tabbing has stopped for ~250ms. |
| `/` | Search. Goes to the search destination; what it opens onto is #29's. |

Feed and post:

| Key | Does | Note |
|---|---|---|
| `k` `j` | The next post / the one before it, with the screen following the selection | `Home`/`End` too, for the first and the last |
| `↓` `↑` | Move the screen by a few rows, leaving the selection alone | The only way to read a post taller than the terminal to its end |
| `PgDn` `PgUp` | The same, a screenful at a time | A screenful is however many rows there is room for |
| `⏎` | Open the post | |
| `a` | Open the author's account | |
| `c` | Compose | |
| `r` | Reply | |
| `b` | Boost / un-boost | Needs viewer state on `Post` |
| `f` | Favorite / un-favorite | Needs viewer state on `Post` |
| `p` | Pin / unpin | Own posts only |
| `e` | Edit | Own posts only |
| `d` | Delete | Own posts only, **confirmation required** (story 43) |
| `x` | Reveal a content warning | |

Screen-local, and deliberately colliding with the above because they are never on screen together:

| Screen | Keys |
|---|---|
| Account | `F` follow/unfollow · `M` mute/unmute · `B` block/unblock — capitals, so a lower-case mark key can never fire a tie by accident |
| Notifications | `d` dismiss one · `D` clear all |
| Follow requests | `a` accept · `x` reject |
| Direct messages | `⏎` open the conversation · `m` mark read — `m` again inside the thread, where a reader who has just read it is most likely to press it |
| Conversation | `m` mark read, and every key that acts on a post, since each message in it is one |

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
- **A rail destination's screen says `tab` rather than `esc`**, because it is the bottom of the stack and there is
  nothing under it to walk back to.
- **A hashtag a search found opens as a screen on the stack**, not as the rail's hashtag destination. Which tag the
  rail keeps a place for is a setting the reader wrote down, and a search result is not them changing their mind.
- **`⏎` on a follow request opens whoever is asking**, because the question is about a person and the answer to it is
  on their account screen.

### What media settled

Media is drawn in place inside a feed item or a post, at whatever width the content region has (ADR-0016):

- **Only a still picture is drawn.** Video, audio, an animation and anything this client has no word for get a `⏵`, the
  description, and the address on the rows below — wrapped rather than clipped, since a real address is longer than 61
  columns and a link with its end cut off is not a link. Never an inline rendering attempt (story 51). An animation is
  linked rather than drawn as the still its preview happens to be, because a frozen frame with nothing to say it was
  meant to move is the misleading rendering the story rules out.
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
  four that do not number a plain list of posts — the post screen, where 0 is the post itself, search across its three
  kinds, notifications, and direct messages.
- **The offset starts again whenever the screen is replaced.** Pushing a screen, popping back to one and arriving at a
  destination all mean different rows, and an offset made on the last lot says nothing about this one.
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
```

With none set, the destination is still on the rail — it says no tag has been named and asks the instance for nothing,
rather than being a rail entry that swallows a keypress.

## Roles

A view names a role; the theme resolves it to an attribute. Nothing constructs a colour (ADR-0014). Each role has a
glyph or a position that carries the same meaning when colour is gone.

| Role | Paints | Carried without colour by |
|---|---|---|
| `body` | A post's text | — |
| `muted` | Timestamps, counts nobody acted on, hints | position |
| `byline-name` | A display name | position |
| `byline-handle` | `username@instance` | the `@` |
| `audience` | The visibility mark | `○ ◌ ● ✉` |
| `content-warning` | A warning and its text | `⚠` |
| `media` | Image placeholders and attachment links | `▒▒▒▒`, `⏵` |
| *(none — a picture's own pixels)* | A drawn picture | it is the picture |
| `poll` | Options and their bars | the bar itself |
| `boost` / `boost-mine` | The boost mark, and it when it is yours | `↺`, and the count |
| `favorite` / `favorite-mine` | The favorite mark, and it when it is yours | `★`, and the count |
| `selection` | The selected row | `▌` in the gutter |
| `rail` / `rail-current` | Destinations, and the one selected | `▸`, with `▶` for the cursor |
| `rail-unread` | An unread count, and the word on an unread conversation | the number, and the word |
| `quota` / `quota-low` | Rate-limit budget left, and nearly spent | the number |
| `chrome` | Breadcrumb and status rows | position |
| `loading` | Stale content while a fetch lands | the breadcrumb says `fetching…` |
| `destructive` | A delete affordance and its confirmation | the word |
| `error` | A failure the shell has to say out loud | the word |

Role selection is testable without a terminal and is expected to be tested: *a post of mine offers delete in the
destructive role*, *an unread conversation's badge takes `rail-unread`*. Drawing is not tested (ADR-0005, ADR-0014).

The one thing that carries colour without naming a role is a drawn picture, whose pixels are the content rather than an
emphasis somebody chose — there is no sense in which `dark` and `light` would answer them differently. The scan that
enforces "no view constructs a colour" names the one file allowed to (ADR-0016); every screen is still caught.

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
boost           = "#6fcf97"
boost-mine      = "#9ef2b8"
favorite        = "#c58fe8"
favorite-mine   = "#e0b6ff"
rail-unread     = "#ff7a93"
destructive     = "#ff7a93"

# A role may set its own background; anything unset falls back to the theme's.
[themes.midnight.selection]
foreground = "#f2f0f7"
background = "#2a2942"
```

Rules:

- A colour is a hex triple or one of the sixteen ANSI names (`red`, `bright-blue`, …). Named colours let a theme follow
  whatever the terminal's own palette is set to; hex does not.
- Any role a theme leaves out falls back to the built-in of the same brightness — a theme is an override, not a
  complete set, so adding a role later does not break every user's config.
- A theme naming a role that does not exist is a config error with the role named, not a silent no-op.
- `Terminal.Gui` quantises hex to the nearest of 16 on a 16-colour terminal, so a theme is authored once.
- When the driver reports no colour at all (`NO_COLOR`, `TERM=dumb`) every role resolves to the terminal's default pair
  and the glyphs above carry everything.

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

The rail carries exactly two marks for this, both in the left column with the destination names: `▶` where the tabbing
has got to, `▸` what is selected. It carries no third mark for *chosen but not loaded* and none for a fetch in flight —
the right-hand column is unread counts and nothing else, and a fetch is announced once on the breadcrumb. A rail
somebody is reading should hold still.

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

1. **Whether a theme can decline to set a background** and inherit the terminal's own. `Terminal.Gui` attributes are a
   foreground/background pair, so "inherit" needs checking against the driver rather than assuming. #46's, along with
   the rest of the theme file.
2. ~~**Where compose lives.**~~ Settled by ADR-0015: a screen on the stack, like everything else. A reply draws the
   first rows of what it is answering above the editor, which is the part of the split region that was worth keeping.
3. ~~**How long the settle window and the cache should be.**~~ Settled above.
