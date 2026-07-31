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
| Compose / reply — an editor over the content region | `c` or `r` | #28 |
| Media inside a post or feed item | Drawn in place | #31 |

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
| `tab` / `shift-tab` | Counts a step. The highlight moves, and the destination loads, once the tabbing has stopped for ~180ms. |
| `/` | Search. |

Feed and post:

| Key | Does | Note |
|---|---|---|
| `j` `k` / `↓` `↑` | Move the selection | `PgUp`/`PgDn`/`Home`/`End` too |
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
| Direct messages | `⏎` open the conversation · `m` mark read |

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
| `poll` | Options and their bars | the bar itself |
| `boost` / `boost-mine` | The boost mark, and it when it is yours | `↺`, and the count |
| `favorite` / `favorite-mine` | The favorite mark, and it when it is yours | `★`, and the count |
| `selection` | The selected row | `▌` in the gutter |
| `rail` / `rail-current` | Destinations, and the one being shown | `▸` |
| `rail-unread` | An unread count | the number's presence |
| `quota` / `quota-low` | Rate-limit budget left, and nearly spent | the number |
| `chrome` | Breadcrumb and status rows | position |
| `loading` | A fetch in flight; stale content while it lands | `◴` |
| `destructive` | A delete affordance and its confirmation | the word |
| `error` | A failure the shell has to say out loud | the word |

Role selection is testable without a terminal and is expected to be tested: *a post of mine offers delete in the
destructive role*, *an unread conversation's badge takes `rail-unread`*. Drawing is not tested (ADR-0005, ADR-0014).

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

`tab` walks the rail and what it lands on loads (ADR-0014). Three rules keep that affordable, and none of them changes
what the user does:

- **A press counts a step and restarts a settle window (~180ms); nothing is drawn or fetched until it closes.** Each
  press abandons the window before it, so a run of presses is one move: the highlight lands where the count reached
  and that one destination is fetched. Holding tab through six destinations is one move and one fetch, not six.
- **A destination is cached for a short while.** A step onto one fetched recently draws immediately and asks for
  nothing, so walking out along the rail and back is one fetch per destination rather than one per arrival.
- **An overtaken fetch is discarded, never drawn.** A reader who has moved on must not have a stale timeline appear
  underneath them.

Because nothing moves ahead of its content, there is no half-state to indicate — no marker for *chosen but not loaded*.
What the rail highlights is always a destination that was actually asked for.

The alternatives were built and measured — a cursor that moves free until `⏎` commits, a key per destination, a jump
list — and all cost one fetch against cycling's six *before* the settle rule, which is what closed the gap. They are on
the prototype branch (`SCREENS-C.md`) if the decision is ever revisited.

## Open questions

1. **Whether a theme can decline to set a background** and inherit the terminal's own. `Terminal.Gui` attributes are a
   foreground/background pair, so "inherit" needs checking against the driver rather than assuming.
2. **Where compose lives** — an editor pushed onto the stack like any other screen, or a region that opens under the
   feed so the thing being replied to stays visible.
3. **How long the settle window and the cache should be** — 180ms and "a short while" are the prototype's guesses, and a timeline and a notification list may not want the same cache age.
