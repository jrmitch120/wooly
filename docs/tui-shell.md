# The TUI shell: the contract

What ADR-0014 decided, in a form a ticket can be written against. The reasoning is in the ADR; this is the enumerable
part — regions, screens, keys, roles, and the shape of a theme.

The shells this describes were prototyped first; the drawings live on the throwaway branch
`worktree-prototype-tui-shell` (`src/Wooly.Tui.Prototype/SCREENS-C.md`), which is the primary source for why this and
not something else. None of that code is production code.

## Regions

Panels, since ADR-0021. The rail and the content are each a rounded frame with a title on its top edge; the frames do
the dividing, so there is no breadcrumb row, no blank row under it and no gutter column.

```
╭ Timelines ───────╮╭ Home › Post by @ben  ✱ ──────────────────────────────────╮
│Home             3││ content                                                  │
│Local             ││ (feed · post · account · conversation · search)          │
│Federated         ││                                                          │
╰──────────────────╯│                                                          │
╭ Hashtags ────────╮│                                                          │
│dotnet            ││                                                          │
│cats              ││                                                          │
╰──────────────────╯│                                                          │
╭ Explore ─────────╮│                                                          │
│Discover          ││                                                          │
│Search            ││                                                          │
╰──────────────────╯│                                                          │
╭ Inbox ───────────╮│                                                          │
│Notifications    4││                                                          │
│Direct messages   ││                                                          │
│Follow requests   ││                                                          │
╰──────────────────╯│                                                          │
╭ You ─────────────╮│                                                          │
│@jeff             ││                                                          │
╰──────────────────╯│                                                          │
╭ API ─────────────╮│                                                          │
│ ██████████░░  92%││                                                          │
╰«─────────────────╯╰──────────────────────────────────────────────────────────╯
 Post: j/k | Refresh: g | Destination: tab | Group: ` | Read: ⏎ | …+10 | Keys: ?
```

| Region | Size | Holds |
|---|---|---|
| Rail | 20 columns including its frames, full height less the status row; none at all hidden (**Hidden rail**, below) | Up to five **rail groups** — Timelines, Hashtags, Explore, Inbox and You, Hashtags drawn only where the config file writes a **Tag set** (below) — in colour the selected entry on `rail-current` and, while tabbing, the cursor's entry on `rail-cursor`; without colour `▶`/`▷` (ADR-0021), each its own titled panel, the group holding the cursor framed in `panel-border-active`, on the press rather than once the selection follows; each destination with its unread count; at the foot an `API` panel holding, with two or more profiles, the instance acted as (ADR-0020), then the budget as a gauge. `«` just inside the foot's bottom-left corner, the toggle's mark (**Hidden rail**, below). On a terminal too short to frame every group the rail steps down (below) |
| Content | the rest of the width, all of it with the rail hidden; full height less the status row | A panel titled with the breadcrumb (below), holding exactly one screen. Its rows start on row 1. With the rail hidden, `»` just inside its bottom-left corner, the toggle's mark |
| Status | 1 row, the shell's full width | The current screen's keys as `Does: key \| Does: key`, as many as fit and `…+N` for the rest — with the rail hidden, `Rail: \` in place of the destination and group hints; or a notice; or a confirmation |

The content panel's title is the stack: the crumbs walked through in `muted`, the one stood on in `panel-title`, eliding
from the left so it always ends where you are, and the fetch mark, a star spinner, a space after it while a fetch
is in flight. The mark's 2 columns are held whether or not it is drawn, so it never moves the trail. The panel's edge is
`panel-border-active`, bar a scrollbar's track (below): it is the panel being read. Its rows are 58 columns wide at an 80-column terminal: the 61
of ADR-0014 less the cell the gutter column gave up to the content panel's left edge, and less the two the rail took
when it widened to 20 so that a label and its count both fit (#272). A screen is drawn in 57 of them, the margin's
column given up on the right to mirror the gutter on the left (#495), so text, pictures and the right-aligned status and
age stop one column short of the edge; 57 is the width every screen must read well at. The rules between things and
the picked thing's band still run to the edge, over the margin. The exception is a screen with widgets of its own —
compose, the description screen and the public profile form (ADR-0030) — which is drawn the panel's whole 58, since its
widgets are sized from the viewport, and keeps its 2 columns of padding each side.

Whenever a screen's rows don't fit the page, the panel's right edge is its **Scrollbar** (#500): beside the rows that
scroll, the edge's own `│` is the track, drawn in `panel-border`, and a thumb on it, in `scrollbar` — `█` unless the
config file's `scrollbar` preference chooses another glyph ("Starting it", below; #505) — is the page's share of those
rows and where the page stands among them. It costs no columns — every screen still reads at 57 — and leaves the rules
and the picked thing's band running to the column inside the edge; a picture, clipped to the viewport, never reaches it.
Only while there is a bar does the right edge differ from the other three, which stay `panel-border-active`; without
colour the glyphs carry it. The thumb is `round(track × page / total)` rows, never fewer than 2, and travels the rest of
the track as the page's top travels from the first row to the last page's, a page scrolled onto the blank under the last
thing measured as at the foot. It is measured off the rows and scroll the frame settles — every row of every loaded
thing, pictures included, so a list that **reads on** is measured against what is loaded so far — and the view settles
the frame in whichever of its passes comes first, the edge or the rows, so the thumb moves on the same frame as the rows
whatever moved them. Beside a screen's pinned rows — the file browser's folder, filter and rule — the edge stays as it
is, and the track measures only the rows that scroll under them. A screen with widgets of its own draws none, and nor
does any screen where `scrollbar = "none"` in the config file's preferences turns it off ("Starting it", below; #501,
#505). A click on the bar, track or thumb, moves nothing — only the thumb's drag moves the page — and the bar answers
the pointer on the margin column inside the edge as well, beside the track's rows only, so that no click there picks the
row beside it (#502, #503; the mouse table, below).

The rail needs 23 + N rows to frame every group with N tag sets on it (Timelines 5, Hashtags 2 + N, Explore 4, Inbox 5,
You 3, API 4), and 21 with none, since then there is no Hashtags group at all; an 80×24 terminal gives it 23, so one set
is enough to make an 80×24 rail compact (ADR-0033). Shorter than that, it steps down rather than clipping:

| Rows for the rail | The rail |
|---|---|
| 23 + N or more (21 with no sets) | Framed |
| 15 + N to 22 + N (14 to 20 with no sets) | Compact: each group's title as a heading row, no frames, the API panel as one gauge row |
| fewer than 15 + N (fewer than 14 with no sets) | Compact, and scrolled to keep the cursor's group in view |

The sets are fixed for a session — they change only when the config file does — so which of the three a terminal's
height draws is too: nothing on the rail moves while you read.

The API panel is counted at its 4 rows whether or not there is an instance to put in it, so whether the rail is framed
is a fact about the terminal's height and never about how many profiles there are; with one profile, the row it does not
use is left blank above it. Compact, the instance sits above the gauge row where the rail has a row to spare for it, and
goes where it has none: the gauge is what the rail is the one thing able to spend. Compact entries are indented under
their headings by the mark column, blank in colour, so a heading and an entry are told apart on every terminal. The
scroll is worked out afresh from where the cursor is on every frame, and the gauge row stays at the foot (#272).

The frames are this client's own, painted in roles like everything else, not Terminal.Gui's `Border`. A picture placed in
a framed region is placed inside the frame (ADR-0021). There is one way to draw one, `Panel` (#270): a rounded edge in
`panel-border`, or `panel-border-active` for the panel you are in, and the title on the top edge in `panel-title`, with a
space either side of it and no `┤ ├`, clipped from its end where it does not fit. The rail's groups are its rows
(#272). The content panel is the one place a view draws an edge round rows it does not build: `PaintedView` lays the
same edges on a one-cell ring round its viewport, so the rows, the scroll and every picture are measured from the
inside, and a picture scrolled half past an edge is clipped at it rather than drawn over it. Compose's editor sits
inside the same edges, laid over the panel (#365), and is drawn again on every frame the panel is: Terminal.Gui draws
the later of two siblings first, and one nothing has changed is not drawn, so a panel redrawn alone would paint its rows
over the fields. The panel's top edge is drawn a second time by a one-row view laid over it, so a tick of the
fetch mark redraws that row alone rather than the panel and every picture on it (#217).

Every one of those numbers is **columns a terminal draws in, never characters**. `ドット絵アカウント` is nine characters
and eighteen columns, `é` written as a letter and a combining mark is two characters and one column, and a row padded
or wrapped by its characters is a row drawn at some other width than the one it was laid out in — too wide and the
terminal wraps the overflow onto the next row, too narrow and the row stops short of the edge (#207). So one measure
answers for the whole shell, `Glyphs.Columns`, off Terminal.Gui's own reading of a rune's width: `Span.Width` reads it,
`TextWrap` wraps and clips by it, `Glyphs.Padded` pads by it, and the view paints by it. Nothing in the TUI lays out by
`string.Length`. Cuts land between graphemes, never inside one — half of a ZWJ sequence, or a base parted from its
combining mark, is something nobody wrote.

The one place that measure is not Terminal.Gui's is a **spacing mark** (`GLOSSARY.md`, ADR-0031, #434): a vowel sign
that takes room of its own, which Terminal.Gui counts as no column and terminals draw in none, one or two — herdr's
`অসমীয়া` came out two columns wider than counted, and every cell after it on the row with it. There is no count every
terminal agrees with, so a character carrying one is held in two columns: the view paints it with the next cell blank,
and the client's own output (`HeldOutput`, handed to Terminal.Gui as its `ansi` driver's) writes it after every other
cell of its row, at a place of its own, and places the cell after it too. A terminal drawing it two wide covers the
blank and keeps it whole; one wide shows a gap inside the word; wider hides the next character. Nothing further along
the row moves on any of them. Terminal.Gui's own editing widgets — compose's editor, Lang, the bio — are not painted by
the view, so they leave no blank: their rows still hold, but there a terminal drawing the mark wide may cover the
character after it. Terminal.app is beyond it: it draws such text off its own cells and everything after it on the row
with it, even a border placed at an explicit column, so a post in these scripts still pushes its row there. Where the
text is the client's own rather than an author's — the Lang list — it is written in English instead.

## Hidden rail

`\` hides the rail and shows it again (`GLOSSARY.md`, #445), and each toggle is saved as `hidden_rail` for the next
launch to open on (#442; "Starting it", below). Hidden means gone, not shrunk: the content panel and its breadcrumb
start at the shell's first column and take the whole of whatever the **Shell width** has left, 80 columns at 80 and 78
inside its edges. Hiding changes nothing about the stack or about where you are.

```
╭ Home ────────────────────────────────────────────────────────────────────────╮
│▌Jeff                                                                    ○ now│
│▌@jeff@mastodon.social                                                        │
│▌                                                                             │
│▌Hello world                                                                  │
│                                                                              │
╰»─────────────────────────────────────────────────────────────────────────────╯
 Post: j/k | Refresh: g | Rail: \ | Read: ⏎ | Reply: r | …+5 | Keys: ?
```

- **Nothing of it is drawn**: no destinations, no unread counts, no instance and no budget. The Inbox is still read
  while it is hidden, and a badge rising still rings the bell; only the count is not drawn.
- **Walking it is locked**, since a cursor nobody can see would land somewhere nobody chose. `tab`, `shift-tab`, `` ` ``
  and `~` move neither the cursor nor the selection and start no settle window. Instead the status row says
  `Rail hidden — \ to show` for a moment, on a prompt taking letters as anywhere else. Where `\` is typed as a letter
  instead, as on the file browser, it says only `Rail hidden`, since there `\` would not show it. Going straight to a
  named destination still works, because the breadcrumb says where you landed: `/` arrives at Search, and choosing a
  profile on `ctrl-p` starts again on its Home. A walk under way when the rail is hidden, inside its settle window,
  still lands.
- **The status row** drops the `Destination: tab` and `Group: `` ` `` hints, which name the locked walk, and says
  `Rail: \` where the first of them stood. A row with no walk hint to stand in for, such as a drilled-in screen's or
  the follow list's filter's, says `Rail: \` just before `Keys: ?`, or last where it has no `?`, so that it is said
  wherever `\` shows the rail (#447). Shown, the row is as it was, and does not offer `\`.
- **A toggle** lays the content out again at its new width, pictures with it (ADR-0025), and keeps the picked post on
  the page row it was drawn on as near as rewrapping allows; with nothing picked the page is left where it begins.
- **On a prompt taking letters** `\` still hides and shows the rail rather than being typed, so that the hidden rail is
  never out of the keys' reach: on Search's prompt, the follow list's filter and Add a profile. A search, a follow and
  an instance have no use for a backslash (#447). It is typed as a letter only on compose, the description editor and
  the public profile form, whose fields take it before the shell sees it, and on the file browser, whose filter takes
  it since a Windows path is typed with it (#447). It does nothing where there is no rail. It is listed on the `?`
  screen under the frame's keys.
- **Remembered.** One setting for every profile, as the **Shell width** is, and saved straight away through the same
  port (`IShellPreferenceStore`); opening on it is not a toggle, and saves nothing. A fresh install opens on the shown
  rail. A toggle that cannot be saved still holds for the session, and the status row says why as a failure, which
  stays until something else is said. A toggle that is saved says nothing: the rail going or coming is answer enough.
- **The mark** is one cell for both states, column 1 of the shell's bottom frame row, so that a click undoing a click
  lands where the first did: `«` just inside the bottom-left corner of the shown rail's foot (`╰«──`) — on the compact
  rail, which has no frames, the first cell of the gauge row after its space in, the bar a cell shorter for it — and
  `»` just inside the content panel's bottom-left corner (`╰»──`) while the rail is hidden. Each points the way a click
  would send the rail. A left click on it is `\`, through the **Keymap** on the screen in front, so it toggles and
  saves exactly as the key does and does nothing where the key does nothing: on compose, the description editor and
  the public profile form, where the mark is still drawn, and where an open question is declined instead. Where there
  is no rail there is no mark. The glyph is the whole of what it says, in the role of the edge or row it is on, so it
  reads the same with colour and without; both are Latin-1 and not East Asian ambiguous, so a column wide on every
  terminal. No hover, no drag, the wheel over it does nothing, and a double click is one toggle: its second click
  lands on the mark the first drew in the same cell, and is spent on that toggle.

## Shell width

The shell is drawn in a share of the terminal's width: all of it, or less, in steps of ten down to half (`GLOSSARY.md`,
#413). `-` narrows it a step and `+` or `=` widens it a step (**Keys**, below), and each step taken is saved as
`shell_width` for the next launch to open at (#414, #415; "Starting it", below). Every width this document gives — the
rail's 20, the 58 columns at 80 and the 57 a screen is drawn in — is the shell's, which is the terminal's at 100%.

- **The columns** are worked out from the terminal's `T` and the share `p` on every layout:
  `min(T, max(80, ⌊T × p / 100⌋))`, and the shell's first column is `⌊(T − columns) / 2⌋`, so it is centred and the
  spare column of an odd remainder falls on the right: the left edge moves only when the width does. A resized terminal
  keeps the share and re-centres. The height is always the terminal's whole height.
- **Never under 80 columns**, the width the shell is laid out for: on a terminal 80 wide or less the shell is all of it.
  A saved share the floor overrides on a narrow terminal is left as saved, for a wider one to draw.
- **The steps.** Narrower is the largest multiple of ten below the share and at least 50; wider is the smallest above it
  and at most 100. Narrower is refused where there is none, or where it would take the shell under 80 columns on this
  terminal, and says `Narrowest`; wider is refused only at 100, and says `Full width`. A step taken says `Width 90%`
  and is saved straight away, through the shell's one narrow port for its preferences (`IShellPreferenceStore`); a
  refused one saves nothing, so what is saved is always a width that was seen. A share written by hand between the
  steps, such as 73, is drawn as written, and a step takes it back onto them: 70 narrower, 80 wider. Each of the three
  is said for two seconds (`ShellTiming.WidthSaidFor`) and then let go of, the status row going back to its keys; why a
  step could not be saved is a failure, and stays until something else is said.
- **The margins** are cells the window never draws, left at the terminal's default attribute: no **Role**, so no theme
  entry, under any theme or `NO_COLOR`. They are the terminal's, outside the shell (ADR-0014, ADR-0021). Terminal.Gui
  clears a screen to a white on black of its own, which written out would be a black margin, so the window puts every
  clear back to the terminal's own colours (`CSI 39m`, `CSI 49m`) before anything draws. A step clears the screen as it
  lays the window out again, so nothing of the wider shell is left in them.
- **Everything inside follows for free**: the root window (`ShellWindow`) is the shell, given an `X` and a `Width` from
  the share, and the rail, the content panel, its title row, the status row and the overlays laid over the panel's
  viewport — compose, the description editor, the public profile form — are all laid out against it. The rail-hidden
  first-run shell is centred the same way. "Nothing jumps" reads the shell's width where it read the terminal's.
- **The mouse inside the shell** lands where it would at full width: every hit test works out screen positions from the
  view tree, so the offset carries through. **In the margins** the window takes every mouse event before any view is
  asked (`ShellWindow.InTheMargins`, #416): a button there does nothing, and a wheel notch there is the content panel's
  notch. A view holding the mouse — one being dragged in — keeps its events wherever the pointer goes, so its let-go
  still reaches it (**Pointer**, below).
- **Pictures follow too** (#417): a sixel box, Kitty's placeholder cells and a Kitty box are all placed from the view
  tree, and what the terminal is let draw a picture in is cut to its view, not to the whole screen, so each lands inside
  the narrowed content panel. A step clears the screen, so every picture is sent or placed again at the new panel's
  width and place in the frame the step lands. Only the box narrows: the cell is still measured as the whole terminal's
  pixels over its whole cells.

## Screens, and who owns them

A screen is a place in the stack, not a window. Entering one pushes, `esc` pops, and the breadcrumb is the stack.

| Screen | Reached by | Ticket |
|---|---|---|
| Feed — home, local, federated, each **Tag set** on the rail, its breadcrumb the set's name | A rail destination | #28, sets #511, past four tags #512 |
| Hashtag — a tag walked to, not one of the rail's sets | A search result, `⏎` on a `#hashtag` typed into search, or `⏎` on a picked hashtag reference | #29, reference #65, direct #305 |
| Post — the post whole, its ancestor chain above and its replies below | `⏎` on a feed item, or on its address typed into search | #28, ancestors #72, direct #306 |
| Account — who they are, what they are to you, their pinned posts and their posts, or their posts and replies | `a` on a feed item or inside a post, `⏎` on a handle or profile address typed into search, `s` to swap runs | shell #28, tie actions #29, the person #164, pinned #172, replies #229, direct #306 |
| Follows — everyone an account follows, or everyone who follows it | `w` on an account, `s` to swap sides | #165 |
| Actors — everyone in one notification group, crumbed by its kind: `Favorited by`, `Boosted by`, `Followed you`, or the instance's own word | `w` on a notification group of several | #455 |
| Actors — one side of a post's actors, everyone who favorited it or everyone who boosted it, crumbed `Favorited by` / `Boosted by` | `w` on a picked post anywhere but Notifications, `s` to swap sides | #489 |
| Discover — who to follow, in sections by why | A rail destination | #171 |
| Search — prompt and results | A rail destination, or `/` | #29, moving between kinds #166 |
| Notifications | A rail destination | #29 |
| Direct messages — conversations, then a thread | A rail destination | #30 |
| Follow requests | A rail destination | #29 |
| Compose / reply / edit — a screen on the stack, like any other | `c`, `r` or `e` | #28 |
| Edit public profile — the own account's display name, avatar, header, four custom fields and the locked, bot and discoverable toggles, read as written, laid as compose's headers are; then the bio laid as compose's post is, between two hairlines at the foot with its count under them, out of the instance's `max_note_length` (500 where it does not say) and counted as a post is — a count only, the instance having the last word | `E` on **You** — on the own account only, said on the status row and drawn nowhere else on the screen | #406, #407 (ADR-0028) |
| Attach — the file browser, the folders and the files the instance accepts | `ctrl-o` on a compose or a reply, `⏎` on its Media header or a click on its words | #376 |
| Choose avatar / Choose header — the file browser for one picture | `⏎` on, or a click on, the edit public profile form's Avatar or Header row | #407 |
| Media — the attachments screen, a row for each thing attached to a compose or a reply | `⏎` on the Media header, or a click on its line, where a short terminal folded the rows into it | story 58 |
| Profiles — every profile on this machine, marked `acting as` and `default` | `ctrl-p` | #240 (ADR-0020) |
| Add a profile — the instance, a sign-in through the browser or a pasted token, the token checked, a name | `a` on the profiles screen, or launching with nobody to act as | #245, #247 (ADR-0020) |
| Picture — one still picture on a post over the whole window at its original, fitted whole, its description wrapped and centred beneath it, crumbed `Picture by @author`, or `Picture 2 of 4 by @author` where the post has several | `⏎` on a walked picture, in a feed or on the post screen | #516, #517 (ADR-0034) |
| Media inside a post or feed item | Drawn in place | #31 (ADR-0016) |

Every screen owes three things: it reads at 57 columns (58 for a screen with widgets of its own — compose, the
description screen and the public profile form — which is drawn the panel's whole width), it says what its keys are on
the status row, and it names roles rather than colours (below).

## Keys

A key may mean different things on different screens — `d` dismisses a notification and deletes a post — which is
workable only because the status row always shows the current screen's keys. What may **not** vary is the frame:

| Key | Everywhere |
|---|---|
| `esc` | Up one level of the stack. Never quits. |
| `ctrl-q` | Quit. |
| `?` | The keymap for this screen. The spec has no in-app help story; this is the shell adding one, and #28 carries it — every other screen inherits it for free. |
| `tab` / `shift-tab` | Moves the cursor (`▶`) at once, one destination down or up the rail, crossing from one rail group into the next. The selection follows it (`▷` while it lags behind), and that destination loads, once the tabbing has stopped for ~250ms. Opening a draft — compose, a reply, an edit, the public profile form — before then calls that off: the cursor goes back to the destination selected, nothing loads, and the draft stays in front (#429). With the rail hidden, nothing but `Rail hidden — \ to show` on the status row, or `Rail hidden` where `\` is typed as a letter, as on the file browser (#445, #447). |
| `` ` `` / `~` | Moves the cursor to the first destination of the next or previous **rail group**, wrapping. Settles and loads as `tab` does (ADR-0021), and is locked as `tab` is with the rail hidden (#445). |
| `/` | Search. Goes to the search destination; what it opens onto is #29's. |
| `ctrl-p` | The profiles screen, pushed onto the stack. On compose it takes the draft off first and opens the screen in its place, asking first where the draft is touched (ADR-0020, #373); it is not listed there. |
| off-page pick | Not a key but a rule over the keys that act on the pick: with no row of the pick on the page, the first such key only picks what is on the page, and the next acts on it ("What moving settled", #476). Listed on the `?` screen as `off-page pick   the first key picks what is on the page`, since the status row stays still as the page moves. |
| `-` / `+` or `=` | The **Shell width** one step narrower, or one wider: `=` is `+` without the shift. Typed as letters wherever letters are typed, as `/` and `?` are, and nothing on compose, the description editor or the public profile form. Said on the status row for two seconds (`Width 90%`, or `Narrowest` / `Full width` where there is no step to take); listed on the `?` screen and on no status row (#414). See **Shell width**, above. |
| `\` | Hides the rail, the content taking the whole width of the shell, or shows it again, saved for the next launch (#442, #445). On a prompt taking letters too, rather than typed: Search's, the follow list's filter and Add a profile (#447). Typed as a letter only on compose, the description editor and the public profile form, whose fields take it, and on the file browser, whose filter takes a Windows path (#447). Nothing where there is no rail. Listed on the `?` screen, and on the status row only while the rail is hidden, as `Rail: \` (#445). See **Hidden rail**, above. |

Feed and post:

| Key | Does | Note |
|---|---|---|
| `k` `j` | The next post / the one before it, with the screen following the selection | |
| `↓` `↑` | Move the screen by a few rows, leaving the selection alone | The only way to read a post taller than the terminal to its end |
| `PgDn` `PgUp` | The same, a screenful at a time | A screenful is however many rows there is room for |
| `Home` `End` | The same, to the top of the list / the foot of it | fn-← and fn-→ on a Mac. On the file browser and the attachments screen, whose arrows walk the cursor, they take the cursor to the first entry and the last |
| `⏎` | Open the post | Answers only, inside a post: the post itself is already open (#48) |
| `a` | Open the author's account | |
| `c` | Compose | |
| `r` | Reply | |
| `b` | Boost / un-boost | Needs viewer state on `Post` |
| `f` | Favorite / un-favorite | Needs viewer state on `Post` |
| `p` | Pin / unpin | Own posts only |
| `e` | Edit | Own posts only |
| `d` | Delete | Own posts only, **confirmation required** (story 43) |
| `w` | Who favorited it, or who boosted it: the actors screen, one side at a time | On a boost, the post boosted. Opens "Favorited by" where the post's count of favorites is above zero, else "Boosted by" where its boosts are, else says `Nobody has favorited or boosted this.` and opens nothing — settled from the counts before anything is read, and always the list, never a shortcut to one account (#489). On Notifications `w` is who did what the row reports, and on an account screen's header block their follows (below, #490) |
| `x` | Show what the post is hiding | Its warned text and poll, its sensitive attachments and link preview, or both halves at once (#113, #116, #119). What it showed belongs to the screen it was pressed on (#121, below) |
| `←` `→` | Walk the references (hashtag, mention, link, attachment, drawn picture) inside the picked post | Clamps at the ends; `esc`/`j`/`k` clear the pick; `⏎` opens what's picked (below), a walked picture on its Picture screen, inside the TUI (#516), letting the reference go as every opening does, so that `esc` comes back to the post picked and nothing walked (review of #514). A walked picture is drawn between `⎡⎢⎣` and `⎤⎥⎦` beside its box (`[` `]` for a box one row tall) (#515), and a click anywhere on its box walks to it and a double click there opens it (review of #514) |
| `1`-`9` `0` | Toggle the 1st-10th option of the picked post's poll | Only where the poll is on screen and would still take a vote — no-op otherwise, and off the status row there too; `esc`/`j`/`k` discard the toggle |
| `v` | Cast the toggled poll vote | **Confirmation required** (story 43), same as delete. Only where the poll is on screen and would still take a vote — and on a poll already voted in or closed, says which rather than asking |

Screen-local, and deliberately colliding with the above because they are never on screen together:

| Screen | Keys |
|---|---|
| Account | `F` follow/unfollow · `M` mute/unmute · `B` block/unblock — capitals, so a lower-case mark key can never fire a tie by accident, and on nobody's account but the own one, which cannot have a tie with itself and draws no standing, nobody in common and no note · `w` follows, which is everyone they follow with `s` a keypress from everyone who follows them — while the header block is picked; on one of their posts, pinned or not, `w` is that post's who as on any feed, and the status row says `w:follows` or `w:who` as the pick moves (#490) · `s` posts and replies, or back to posts alone, in place · `[`/`]` section, on an account carrying a pinned run · `E` edit public profile, on the own account only — **You**, or yours reached any other way — and off the status row and answered nowhere else, as `p`/`e`/`d` are on somebody else's post; it reads the public profile as written first, and a read that fails opens nothing and says why (#406) |
| Edit public profile | `↑`/`↓` walk the fields top to bottom — display name, Avatar, Header, the custom fields, Locked, Bot, Discoverable, then the bio at the foot — stopping at either end rather than coming round; a custom field's label and value sit side by side, so `↑`/`↓` keep to the column down the rows, coming in on the labels, and `→` off the end of a label crosses to its value with the caret at the start, `←` off the start of a value back to its label; `←`/`→` move nothing else, being the caret's in a field and nobody's on a picture or toggle; in the bio `↑` walks only off its first line and `↓` only off its last, the caret having them between, and `⏎` in a one-line field walks on to the next in reading order, a label to its value; nothing is marked with a bar — a field walked to has the caret, and a picture or toggle, having none, has its value lit in `selected-text`, as compose's Media and To are · `space` or `⏎` flips the toggle walked to · on **Avatar** or **Header** `⏎` opens the file browser for one picture, crumbed `Choose avatar` or `Choose header`, and `ctrl-v` or `alt-v` pastes a picture from the clipboard — or the first copied file that is a picture — into it, saying on the status row where the clipboard holds neither, or where this machine has nothing to read the clipboard with — every press, there being no text to paste instead, where compose says the missing tool once a session; the status row offers `⏎ choose file` and `ctrl-v/alt-v paste` there, and other letters are nobody's. A picture picked is drawn in place of the current one with its file name and `sent on save` beside it, and goes up only in the save (ADR-0028) — a touch, like any other edit; on a terminal that draws, each row is two rows tall with the picture in a box (the avatar four columns wide, the header twelve, each filled by its picture, cropped from the middle — #438) and the words after the header's box width, and where nothing is drawn it is one line, `Avatar  change…`, `Avatar  none · choose…` or `Avatar  cat.png · sent on save` (#407) — `change…` and `choose…` saying what `⏎` does, the picture saying what is there · `ctrl-s` save, sending only what differs from what was read (ADR-0028): the custom fields as a set, an emptied row left out, which removes its field; with nothing changed it sends nothing and says so · `esc` leave — asking `Discard these changes? y / n` first where anything differs from how it opened, as every way out does, and only `y` goes (#373) · `tab`/`shift-tab` are the frame's, never a walk of the form's fields. On a terminal too short for the whole form the field walked to is always on screen: the form moves up or down only when the walk leaves what is shown, and only as far as brings it back. A save takes the form off and reads the account under it again, saying "Public profile saved." — the own account further down the stack shows it as saved, a list held a moment ago with the own account or a post it wrote or boosted on it is read again when next reached, and a new avatar or header is drawn rather than the old one; a refusal keeps the form and every edit in it, with the instance's own words on the status row until an edit spends them (#406). While the list of people to mention is open in the bio: `↑`/`↓` pick · `tab`/`⏎` insert · `esc` close the list, never the form (#408) |
| Follows — following or followers | `f` filter, on a list under the threshold · `s` swap to the other side, in place · `⏎` open that account |
| Actors | `⏎` open that account · `s` swap to the other side, in place, on a post's actors only — a notification group's have one side (#489) · `o` open the post's page on its home instance in the browser, on a remote post's actors only, where the whole list is (#491) |
| Search results | `[`/`]` section, where two or more kinds found something |
| Discover | `F` follow/unfollow · `d` dismiss, one-way · `[`/`]` section |
| Notifications | `w` open who did it — whoever favorited, boosted, followed or mentioned (#451), `a` staying the post's author as on every feed (#448); on a group of several, the actors screen listing every one of them, read from the group's own accounts and read on as the page nears the end (#455, #467) · `d` dismiss the group, every notification in it (#457) · `D` clear all |
| Follow requests | `a` accept · `x` reject |
| Direct messages | `⏎` open the conversation · `m` mark read — `m` again inside the thread, where a reader who has just read it is most likely to press it |
| Conversation | `m` mark read, and every key that acts on a post, since each message in it is one |
| Profiles | `⏎` act as that profile, for this session — not offered on the one already acted as · `D` make it the default, for the CLI and the next launch — not offered on the one already the default · `a` add a profile · `R` sign it in again, replacing its token — offered on every row · `x` remove it, after a confirmation — refused on the one acted as and on the default |
| Add a profile | `⏎` on to the next step · `t` paste a token instead, while the browser is out or after it failed · `esc` back to the list, calling off a sign-in or a check in flight — or, as the only screen on first run, back to the first step, with `ctrl-q` quit offered (#247) |
| Compose / reply / edit | `ctrl-s` send or save — waiting first on anything attached still going up, which `esc` calls off (#375) · `esc` throw it away — asking `Discard this post? y / n` first where it differs from how it opened, as every way out of it does (#373) · `ctrl-w` move the typing between the post and the content warning over it — on all three, each carrying a warning field of its own (#123, #139, #140); `⏎` in the warning hands the typing back too (#320) · `ctrl-v` or `alt-v` attach a picture or copied files from the clipboard on a fresh post or a reply, from any field, Media and its rows included, else paste text as before — `alt-v` because Windows Terminal and the console host keep `ctrl-v` as their own paste (#380, ADR-0015); the status row offers `ctrl-v/alt-v paste` on Media while there is room · `ctrl-o` opens the file browser over a fresh post or a reply, from any field (#376), and the status row offers `ctrl-o add media` wherever it adds — with room left on the post, and not on Media, which offers its `⏎` for the same thing · `↑` on the post's first line moves the typing up into the headers, `↑`/`↓` move it between the headers that take typing, and `↓` off the last returns it to the post, the way a mail client's do (ADR-0024, #337); the walk stops at either end rather than coming round — `↑` on To and `↓` below the post do nothing — and the status row offers only the ways it goes: `↓ field` on To, `↑↓ field` on the headers under it, `↑ field` in the post · on **Media**, which the walk stops on between the warning and the post on a fresh post or a reply, `⏎` opens the file browser, as a click on its words does (#376) — or, where a short terminal folded the rows into its line, the attachments screen listing them (story 58) — and `s` flips the sensitive toggle once anything is attached, as a click on it does, unless a warning holds it on (#379); other letters there are nobody's · the walk goes through Media's **rows** too, `↑` from the post reaching the bottom one first (#378): on a row `del`/`backspace` take it off, `ctrl-z` brings back the last one taken off (one deep, only on Media and its rows), `shift-↑`/`shift-↓` move it, `r` retries it where a retry can help, `s` flips the toggle as on the header, and `⏎` opens the description editor, where `esc` and `ctrl-s` are both done (#377) · on **To** `←`/`→` choose the visibility, skipping any it does not allow, and the status row offers `←→ choose` ahead of the walk (#338); letters there are nobody's · on **Lang** a code or a name typed opens the list of languages, as do a click and `⏎`; the walk goes To, Lang, the warning, Media, the post — the order they are drawn in — and steps over Lang where a short terminal has given its row up (#340) · `tab`/`shift-tab` are the frame's here as everywhere, never a walk of compose's fields. While the list of people to mention is open: `↑`/`↓` pick · `tab`/`⏎` insert · `esc` close the list, never the draft (#318). While the list of languages is open: `↑`/`↓` pick · `tab`/`⏎` choose · `esc` close the list, never the draft — and the status row offers those three ahead of the rest (#340) |
| Choose avatar / Choose header | the Attach browser's keys, but it lists only pictures, `ctrl-a` shows no other files, one file may be chosen, and `⏎` takes the file chosen, else the one under the cursor, back to the form's row as its picture (#407) · `esc` back to the form unchanged |
| Attach | type to filter, fuzzily (#376) · `space` choose the file under the cursor, or let it go — up to what the post has room for · `⏎` attach what is chosen, wherever the cursor is, else the file under the cursor, else open the folder under it · `→` open a folder · `←` up a folder · `↑`/`↓` walk the list · `ctrl-a` every file, and again only the accepted types · `backspace`/`delete` only ever the filter, stopping at empty · `esc` clear the filter, then back to the draft unchanged |
| Media (the attachments screen) | `↑`/`↓` walk the rows · `⏎` describe the row · `del`/`backspace` take it off · `ctrl-z` bring back the last one taken off · `shift-↑`/`shift-↓` move it · `r` retry it where a retry can help · `s` the sensitive toggle · `ctrl-o` the file browser, attaching onto the compose under it · `ctrl-v`/`alt-v` attach from the clipboard onto it · `esc` back to the draft (story 58) |
| Picture | `←`/`→` step to the post's previous or next still picture, in the order they are drawn and passing over a video or the like on the same post, each drawn as any is with its own description, stopping at either end rather than coming round; the status row says them only where the post has several, and the crumb counts which this is (`Picture 2 of 4 by @author`), updating with each step (#518, review of #514) · `esc` back at its first press, to the screen it was opened from with the same pick and nothing walked, whichever was stepped to — nothing on the screen is walked or picked for it to let go of first, the steps being the screen's own (#516, #518, review of #514) |
| Home, local, federated, each tag set, Discover, Notifications, Messages, Requests, Post, Account, Follows, Actors | `g` refresh — evicts the destination's cache entry (where one exists) and re-runs the same fetch its own arrival runs |

### What the four screens settled

The keys above are the contract. These are the questions building them raised, answered once so the next screen does
not answer them differently:

- **A key with nothing to act on is not announced.** The row is what says which keys this screen answers to, so a key
  on it that is silent when pressed reads as a shell that missed the press. Stated once and applied everywhere: while
  no post is picked out, every key of `PostKeys.OnAPost` bar `c` comes off the row — a follow notification, the
  account screen's header block, an empty feed, an empty inbox. `c` stays, a fresh post being written from anywhere.
  `Screen.Keys` asks it for every screen at once rather than each screen asking for itself, so a screen added later
  inherits the rule rather than being remembered (#193; #179 did it for `b`, `f`, `d` and `r` on the one screen).
  Taken off **by hint rather than by letter**: a screen's own `d dismiss`, `m mark read`, `a accept` and `⏎ open` all
  act, and a key means what its screen says it means. The same rule takes the poll digits and `v` off a row with no
  poll to vote in, and `⏎` off a post screen's own post (#48).
- **And a key that would refuse the picked post comes off too** (#220, amending the answer above, which had called
  `p`, `e` and `d` on somebody else's post a key doing its job because each answers *Only your own posts can be
  pinned/edited/deleted.*). Once the row fills to a rank and counts what it could not fit, a key announced where it
  does nothing wastes a slot and makes `…+N` promise a `?` entry that answers nothing. So `p`, `e` and `d` come off a
  post that is not the reader's own — asked of the post inside a boost — and `x` off one with nothing left to ask
  past: nothing hidden, or already asked past on this screen, which means it leaves the row the moment it is pressed.
  The shell still refuses the press; only what the row and `?` say changed. Whose a post is rides on the post
  (`Post.IsMine`), set where every post crosses from the wire by the one comparison `ActiveProfile.SignsInAs` makes —
  the same one the shell's refusals ask — so a screen still knows about no instance.
- **And a screen's own keys come off an empty row the same way** (#195, amending #193's answer above, which had put
  them outside the rule as idle for their own reasons). A key that acts on one of the things a screen
  walks needs one of them to be there, and no widening of `PostKeys` reaches these: `w`, `d` and `D` on an inbox with
  nothing waiting, `⏎` and `m` with nobody writing, `⏎`, `a` and `x` with nobody asking, `⏎` on a search that found
  nothing. Each key says so where it is declared (`KeyHint.NeedsAPick`) and `Screen.Keys` drops them while the walk is
  empty, so this too is inherited rather than remembered. What stays is `j`/`k` and `↓`/`↑` — an empty screen shows its
  emptiness, which is explanation enough for a walk that does not move, and a row cut back to `g tab ?` reads as broken
  rather than as empty — and `g`, which on an empty screen is the one key that still does something, the thing it does
  being to fill the screen. Empty means *a list with nothing on it*: a compose editor, the keymap and a notice walk
  nothing at all and are untouched, and the search prompt drops its arrows while it is taking letters for that reason
  rather than this one.
- **A notification is not the post it is about.** `d` dismisses by the group's own key (#457), or on an instance too old
  to group by the notification's own id; every other key on the row acts on the post it carries, so a mention can be
  answered without leaving the inbox. A follow carries no post, and picking one leaves those keys with nothing to act
  on rather than guessing — which is why they are off its row.
- **Notifications are drawn as the instance groups them** (ADR-0032, #453). Each row is a **Notification group**, read
  from `GET /api/v2/notifications` with no `grouped_types`, or one notification a group where the instance has no
  grouped read. Its names line names two actors and counts the rest by the instance's count (`Alice, Bob and 6 others
  ★ favorited your post`), with the kind's icon and sentence in the kind's colour and the unread mark and newest age at
  the right; above a post drawn whole by its actor — a mention — the line is the kind alone (`@ mentioned you`) and
  the post's byline is left as on every feed. A group is `unread` when its newest notification is past the marker.
  Rows are never merged by post: two groups about one post are two rows.
- **A group's actors stand in an avatar strip under its names line** (#454, layout A7b of #450): their faces at a
  byline's 4×2, in the instance's order, indented two columns with two between faces, as many as the width has room for
  up to the sample sent, then a muted `+N` on the top row counting the rest of the group. Face `n` starts `6n` columns
  in whatever the group holds, and five columns are always held for the `+N` — exact up to `+9999`, then `+12k`,
  `+3M`, `+2G`, rounded down — so nothing moves as a group grows. Where the terminal draws no pictures the strip takes
  no rows, and a post drawn whole by its actor has none — its byline has the face.
- **The mouse reaches every actor the keyboard does** (#456, `NotificationsScreen.Clicked`). A click on a face in the
  strip, either of its two rows, opens that actor's account; a click on the names or the kind's sentence of a names line
  is `w` — the actors screen on a group of several, the actor on a group of one. A click anywhere else on a group (the
  gaps, the `+N`, the age, the post) picks it and nothing more, and a double click there is `⏎`, opening the post. A
  double click on a face or the names is that click once, never also `⏎`.
- **`D` asks first.** Emptying the inbox takes away a list nobody has necessarily read and nothing brings it back, so
  it is confirmed on the same terms `post delete` is — the same confirmation, saying `clear` rather than `delete`.
- **A count and the list under it are one fact.** Arriving at a destination sets its badge from the same answer the
  screen is drawn from, and dismissing or answering something moves both.
- **An Inbox badge is the instance's unread count** (ADR-0027). Notifications counts what is newer than the account's
  read marker — counted apart from the list, which holds read ones too. An account with no marker counts nothing at
  first, and from then on what is newer than its **session marker** — the newest it had at that first count, or the
  beginning where it had none (`SessionMarkers`, kept per profile for the session and never sent to the instance) — so
  what arrives while the shell runs still counts, and arriving at Notifications draws those `unread`;
  Messages counts the conversations flagged unread and Requests every request, off their lists. The shell's opening
  counts and arriving at the destination count the same way (`InboxCount`), and a dismissal reads the Notifications
  count again rather than taking one off it. Nothing waiting draws no badge; an unread count that reaches the 40 one
  read counts draws `40+` — 40 or more unread conversations, a full list of requests — and Notifications reads one past
  the 40 it counts, so exactly 40 draws `40` and only more draws `40+`. Messages and Requests are counted again off the
  whole list held after every read on (#466), as in "What reading on settled".
- **Arriving at Notifications reads them** (ADR-0027, #397). The read marker moves to the newest notification shown, so
  the badge clears — on the web too — and the rows that were newer than the old marker carry the word `unread` before
  their age for that visit. Nothing unread is nothing to move; an account with no marker gets one at the newest. A move
  the instance does not take says nothing and leaves the badge at the count it read. Reading never dismisses: every row
  stays. Messages and Requests read nothing on arrival.
- **The Inbox badges are polled every 60 seconds while the stream is down** (`ShellTiming.PollEvery`, #398) for the
  profile acted as, off the shell's clock: all three are read again and set outright from the answers, a count that
  cannot be read leaves its badge as drawn, and a profile switch lets the old profile's poll go and starts the new one's.
- **The Inbox is kept live over the user stream** (`IInboxStream`, ADR-0027, #401). The shell opens it for the profile
  acted as when it opens — before its first destination has been read, so a slow first read holds nothing back —
  polling until it says it is up and not after. What it says is gathered for
  `ShellTiming.GatherFor` (2s) from the first of it and re-read as one: a notification re-reads Notifications, and
  Requests too where somebody asked to follow; a conversation re-reads Messages. A direct message comes over it as a
  mention and a conversation, so gathering is what makes it one re-read, and one bell. A drop, or a stream that will
  not come up, starts the poll again and tries the stream again after `ReconnectAfter` (2s), doubling each failure up
  to `ReconnectAtMost` (2 minutes), and back to the shortest only once it has shown it works — said something, or
  stayed up as long as the wait it would be tried again after — so a connection accepted and closed at once (a proxy's,
  or a restarting streaming service) is backed off rather than tried every 2s for ever; coming up again re-reads all
  three for what arrived meanwhile — the first time it comes up reads nothing, the opening counts being fresh. None of
  it is drawn. A profile switch closes the old stream with everything else the profile had, and nothing it still says
  is applied.
- **A rising Inbox badge rings the terminal's bell** (#399): `BEL`, written through the shell's own output, so the
  terminal's settings decide whether it sounds, flashes or bounces — no OS sound and no branch for any one terminal.
  It rings when a read puts a badge above the last count of it for the same profile, once per re-read however many
  badges rose (a poll reading all three is one re-read). The first count of each badge after the shell opens or a
  profile switch never rings, whichever read lands first; being on the destination does not stop it; a badge that
  falls or stays never rings. A badge at `40+` cannot count higher, so it rises where the newest thing waiting under it
  changed (`Found.Badge.Newest`). The last count is the instance's (`InboxBadges`), not whatever the rail draws: marking
  a conversation read or answering a request moves it by one, as the instance would, so the next arrival still rings —
  but under `40+` it stays `40+` (never `39+`) and the count is read again, quietly, since answering what was newest
  under it is nothing arriving. Re-reads of one badge may overlap; one asked for before another that has already
  landed is dropped, so an older count never overwrites a newer one or rings again for the same arrival. `bell = false`
  in `[preferences]` turns it off, and the badges still move.
- **What a re-read finds lands at the top of the Inbox screen in front** (#400). Every re-read of a count — a poll, a
  stream event — hands what it read to the screen on top of the stack (`Screen.PutOnTop`), and only while the rail is
  showing that destination: new notifications join their **Notification group** — the re-read reads the first page of
  groups too, only while Notifications is in front with no list read in flight and something is unread — so a group
  already on screen takes the re-read's count, actors and age and moves up rather than being drawn twice, and a group
  not on screen is put on top (#458) — a re-read begun before Notifications came in front read no groups, and puts
  nothing on it but is read again, groups and all, rather than make an unread notification a group of one whose
  `ungrouped-<id>` key an instance that groups does not have; either is drawn `unread`, and the read marker is moved past them so the
  badge clears again — but only where the re-read put everything unread on screen: one that found more than the 40 it
  counts leaves the marker for the reader's next arrival or `g` and the badge at `40+`, since there are unread ones past
  the 40 that no screen has shown, while exactly 40 all shown moves it; a conversation new or changed (last post or unread flag) moves to the top as the instance has
  it; a new request goes on top. The same thing stays picked, and the content region keeps it on the page row it was
  on (`Shell.Grew` → `PaintedView.KeepPickOnItsRow`), so the page moves with what went in above it and nothing jumps. Where the
  picked thing is itself what changed — the conversation the reader is on spoken in again — it is brought up to date
  where it stands rather than moved to the top, and the pick stays on it: the row the reader is on never jumps, and it
  takes its place at the top the next time the list is read (`Picked.TakeIn`). A notification group is the
  exception: the picked group joined moves to the top like any other and the pick goes with it, so Notifications
  always reads newest first, as a fresh read would draw it (#458). Nothing is put on screen while the
  destination's own list read — its arrival or refresh — is in flight: arriving draws an empty screen until its list
  lands, and that arrival draws the list and moves the marker itself, so a re-read landing first only counts, and the
  count is read again once the list read is over, for whatever arrived after the list was read (`ListReads`). A
  dismiss, an answered request, a page or a favourite in flight holds nothing back. A screen
  underneath, or a destination walked away from, is told nothing; a re-read that raised the count or put anything on
  screen forgets that destination's cached list, so coming back inside the cache window reads it again rather than
  drawing the list from before — and that visit's cleared badge — over what arrived.
- **A prompt taking letters takes `/`, `?`, `` ` `` and `~` too**, which is the one exception to the frame keys above.
  A web address and a question are both things somebody is entitled to search for, and a prompt that could not take a
  slash would refuse the query most likely to be pasted into it; code in backticks and a path under `~` are the same
  (#265). Every other frame key, `esc`, `ctrl-q` and `tab` among them, still means what it means everywhere, and the
  status row says what the prompt answers to. So does `\`, which hides and shows the rail rather than typing a
  backslash, since no search, follow or instance needs one (#447). The one prompt that types it is the file browser's
  filter, since a Windows path is typed with it.
- **`/` from the search screen's results starts a fresh prompt** rather than doing nothing, since that is the one place
  the key is most likely to be pressed twice.
- **A screen's own keys go in front of the shared ones on the status row.** The row is one row and draws what it has
  room for from the front (*What the status row settled*), so the keys a reader can find on no other screen are the
  ones that have to be there.
- **A status row's key and its explanation are visually split** — *changed by ADR-0021*: the pair is now drawn
  `Does: key`, the words capitalised as a label (their first letter only, never the key) and the pairs divided by
  ` | ` (#268); the split by role below stands. As first settled: the key stays `Role.Chrome`, the words explaining it
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
  follow-up). Over a post being written, the thing asked for is changing the draft: an edit to the post or the warning,
  or `ctrl-w`, takes the notice down — the keys go to the fields there, and the one key left that would otherwise have
  cleared it, `esc`, throws the draft away (#319).
- **`esc` is always up one level, of whichever kind of level is currently open** — amended from "up one level of the
  stack" now that a reference pick is a level of its own. With a reference picked, the first `esc` clears the pick;
  the next pops the screen (#64). This is the one addition ADR-0014's frame keys have taken since being settled.
- **A rail destination's screen says `tab` rather than `esc`**, because it is the bottom of the stack and there is
  nothing under it to walk back to.
- **A hashtag a search found opens as a screen on the stack**, not as a place on the rail. Which tags the rail keeps
  places for are the tag sets the reader wrote down, and a search result is not them changing their mind.
- **`⏎` on a direct query opens what it names instead of searching** (#305). A `#` followed by one tag word — the
  rule `timeline tag` takes a tag by, so `#日本語` is as direct as `#cats` — opens that tag's timeline pushed onto
  search, the same screen and breadcrumb (`Search › #cats`) picking a hashtag result gives, and asks the instance
  nothing, since any well-formed tag has a timeline. `esc` comes back to the prompt as it was left, `#cats` still
  typed. Anything else — `cats`, `#cats dogs`, a lone `#` — is searched for as before, and the status row says
  `Search: ⏎` whatever is typed. The CLI's `search` never does this: scripts depend on its one output shape.
- **`⏎` on a handle or a web address searches, then opens what it names on an exact match** (#306). A handle is
  `@alice`, `@alice@host` or `alice@host` — an `@` somewhere, no whitespace, one or two parts; a bare `alice` is an
  ordinary search. An address is a single `http`/`https` address, read as opening a link reads one, so
  `host/@alice/110` is asked as `https://host/@alice/110` — though without a scheme it needs a dotted host and a path,
  since a post or a profile always has one, and `node.js` or `3.14` stays a search for words. Either asks the one resolving search it always asked, and no
  more. A handle opens the account whose full address it is (a bare `@alice` read against the profile's instance); an
  address opens the one post or account it resolved to. What opens is pushed onto search as picking that result would
  push it, with the results put up underneath, so `esc` comes back to them and the query. With no exact match —
  `alice@host` turning up only `alicia@host`, a page that is neither a post nor a profile — the results are listed as
  for any query, or `Nothing found`, and never an error. `@alice hello`, `@a@b@c` and an address with words after it
  are ordinary searches.
- **`⏎` on a follow request opens whoever is asking**, because the question is about a person and the answer to it is
  on their account screen.
- **`⏎` inside a post opens anything on the thread but the post itself** — an answer below it, or an ancestor above it
  (#86). That one post is what the screen is already about, so opening it would push a copy of the screen the reader
  is standing on — and pressing again would push another, stacking duplicates and a breadcrumb of places nobody went
  (#48). It is left off the status row while that post is picked rather than announced and refused; every other key on
  the row still acts on it, since a post being read is still a post to boost, favorite, answer or take down. Which row
  that is is found by the post's id rather than by an ordinal: it stopped being the first thing on the screen when the
  ancestors landed above it, and a deletion higher up the chain moves it again.
- **A reveal belongs to the screen it was made on, and lasts exactly as long as that screen does** (#121). `x` writes
  to a `Revealed` the screen holds, so a warning asked past on the feed stays past for as long as that feed screen is
  on the stack: through `j`/`k` and the arrows, through a boost or a favorite putting a fresh copy of the post in place
  — the set is keyed by post id rather than by the copy in hand — and through drilling in and walking back out, since a
  pop hands back the very screen the reveal was made on rather than building another. **What ends a reveal is that
  screen ending**, which is three things and no others: `esc` pops it, `g` replaces it with a fresh one, and arriving
  at a destination clears the stack out from under it. Nothing else prunes anything, and nothing needs to — the same
  law the page a screen is showing follows (`Screen.Began`, below).
  Drilling in asks again even though nothing lapsed, which is the other half of the same rule rather than an exception
  to it: the post screen is a *new* screen and has been asked nothing, and so is the same post reached again through a
  search result. Two presses of `x` on one post is the expected shape of this. One `Revealed` shared by the whole stack
  was the alternative — a post asked past staying asked past wherever it was next drawn — and was refused. A warning is
  a request to be asked before being shown, and honouring it once is not consent to skip the asking everywhere
  afterwards; showing somebody something on the strength of a press made on a screen they have since left is the one
  thing this key can get wrong that pressing it again does not undo.

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
  (`PostThread`, GLOSSARY.md) — both halves around the post, since it was never one call's worth of question to ask
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

### What a reply sent from the post screen settled

A reply sent from a post screen appears in its thread as soon as the instance takes it, rather than only on the next
`g` (#482):

- **Placed, not read again.** The post the send answers with is taken into the thread by `PostThread.TakeIn`, which
  puts it where Mastodon's next read would: depth first, each reply followed by its own answers, and the answers to one
  post oldest first — so it lands straight after the last of the answered post's own answers. A read would reach only
  the screen in front, open at the top, and be dropped as an enquiry if the reader had walked away.
- **Only the new reply is placed.** The replies already held keep the order they were served in. Mastodon orders a
  thread over every post in it, including ones the viewer cannot see, and filters afterwards, so a reply whose answered
  post is hidden keeps that post's place — which nothing held could work out again.
- **Self-replies lifted, across the whole thread** (#483). Mastodon's last step (`promote_by!`) moves every reply that
  answers its own author up to where the first reply answering someone else stands, keeping their order. `TakeIn`
  places the new reply on its own side of that lift, after the nearest reply on that side that comes before it depth
  first, so a thread of your own lands ahead of everyone else's answers and an answer to someone's self-reply goes where
  the instance puts it. Whether a reply answers its own author is read off its `InReplyTo.Handle`, which `PostWire`
  sets to the post's own author exactly when Mastodon says so — so no head post's author is needed to judge a direct
  answer to it.
- **Every post screen on the stack that holds what it answers takes it in**, through the `Change.PostSent` every screen
  hears. One that answers an ancestor (`N up`) is in a thread around something else and is left off; only `Sent.` says
  it went out.
- **Only the screen it was written from moves.** `Change.PostSent.WrittenFrom` is the screen directly beneath the
  compose when it was sent; that one picks the reply and its page follows it, so a reply landing below the rows on show
  is brought into view. Any other keeps its pick on the thing it was on and its page where the reader left it.
- **Feeds, Notifications, account and search screens are unchanged.** A refresh is how they show what has arrived.
- **A direct message reply reaches its conversation from wherever it was written** (#484).
  `Change.PostSent.Conversation` is the conversation of the nearest conversation screen on the stack holding the post
  the reply answers — beneath the compose, or beneath a post screen opened from it — worked out when it is sent. Every
  conversation screen of that conversation puts the reply on the end, which is where the next read, around the last
  message, puts it too; only the one it was written from picks it and has its page follow. The Messages list moves that
  conversation's row in place. A reply to something no conversation on the stack holds reaches none of them.

### What references settled

`←` and `→` walk the things inside a post's text that point somewhere else. **Reference** is the word — a hashtag,
mention, or address inside a post's text — replacing `BodyText`'s internal "marks" language, which collided with
`Post.Marks` (boost/favorite/pin) (#64, #65):

- **Every screen with a post drawn on it**, not just feed and post — the breadth `PostKeys.OnAPost` has, plus the
  conversations list, where the row is a conversation and the post drawn under it is its last message. That one is
  `Screen.Referencing` rather than `Screen.Picked`: widening `Picked` there would have handed `d`, `b` and `f` a post
  the screen never offered them (#83).
- **And, since #164, on something that is not a post at all**: the account screen's header block, whose **Bio** and
  whose **Custom field** values carry references of their own. `Screen` stops deriving them from a `Post?` and asks the
  picked thing what it carries, with today's post implementation as the default. A bio is the first source here that is
  not a post and will not be the last, which is why the generalisation was made rather than a second path added.
- **A mention in a bio is drawn and never walked.** `⏎` on a mention resolves off `Post.Mentions`, and a bio carries no
  such list; a bare `@maria` resolved against the reader's own instance opens whoever *their* server has by that name,
  which is the failure this section already warns about below. Hashtags and addresses need no resolution and walk
  normally, which is what makes a verified link on somebody's profile reachable — the most useful thing on the screen.
  The precedent for drawn-but-unwalkable is a video's preview box and a link preview's author name.
- **Walkable**: hashtag, mention, and address — the three references `BodyText` finds — followed, since #109
  (ADR-0017), by every `Video`, `Animation`, `Audio` or `Unknown` attachment on the post, in attachment order, and
  since #116 (ADR-0018) by the post's link preview, last of all. **Not walkable**: an `Image` attachment where it is
  linked rather than drawn — a drawn one is walked after the labels' places, and opens on its Picture screen (#515,
  #516, ADR-0034); the name a link preview says wrote the page, which is plain text and
  never an address; text still behind a content warning, which has no references until `x` shows it, since the
  brackets marking a pick would be behind the warning too (#83); and every attachment on a **warned** post, along with
  its link preview, for the same reason since #113 — its label is behind the warning with the rest of what is hidden,
  so `←`/`→` would walk to something nobody can see and `⏎` would open a video the reader never asked for. The two
  halves are asked separately: a post marked sensitive with no warning written over it shows its text, so what is
  written in it goes on being walked while its attachments do not. Both are asked of `OnShow` — the one module that
  says what a post is showing this reader — rather than worked out here and again where the rows are drawn, so the walk
  can never reach past what was put on screen (#145).
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
- **A click on a reference walks to it** (#475), as `→` would have arrived there, picking the thing it is inside first
  and letting go of any reference held in another; a single click never opens anything. Every run drawn for a
  reference — both halves of a wrapped one, its brackets, an attachment's kind, a link preview's title, every cell of a
  drawn picture's box and the bracket columns beside it — carries the
  reference it is drawn for (`Span.Reference`), and the click is answered off that rather than off a second reckoning
  of where each reference fell. It is the reference itself, not its place in the walk, so what was drawn is matched
  against what may be walked to: a mention in a bio matches nothing walkable, and the click only picks.
- **`→` enters at the first reference on the page, `←` at the last on the page** (#477), so a tall post scrolled
  partway down is not entered at a reference scrolled off above. Where none of the pick's references are on the page,
  `→` enters at the post's first and `←` at its last, and the page brings it in (below). Once inside, further motion
  walks every one of the post's references, on the page or not, and at either end **clamps**, matching `Picked<T>`'s
  existing convention rather than wrapping.
- **With the pick off the page, `→` and `←` reclaim and enter in one press** (#477). The press picks what `j`/`k`
  would reclaim (the first thing whose start is on the page, "What moving settled") and enters that thing's references
  at the first or last on the page, even mid-walk: the walk inside the post gone off the page is let go with the old
  pick rather than stepped on. Where the reclaimed thing has no references, the press only reclaims. The view says
  both the reclaim and which references are on the page off one lot of rows (`PaintedView.OnThePage`, read off each
  run's `Span.Reference`, so a wrapped address counts while either half shows), and the shell does the rest
  (`Shell.WalkReference`).
- **The page follows a walked reference** (#473). Walking to a reference that is not on the page moves the page the
  least it must to bring its row onto it — onto the bottom row walking down, the top row walking up — the same
  minimum move `j`/`k` bring the pick back with (`Scroll.ToReference`, keyed on every row a run of it is drawn
  on, `Span.Reference`, so an address wrapped across two rows comes on whole where it fits). Walking to one already on the page leaves the page
  where it is. A page that has to move stops following the pick, since following shows a post taller than the room
  from its top on every frame and would take the reference straight off again; the window asks for it after a walk
  step (`PaintedView.ShowReference`), spent on the next frame only, as `[`/`]`'s anchor is.
- **`esc` clears the pick first**, popping the screen on the next press (above). **`j`/`k` clear it too**, since the
  reader has left the post.
- **A reference that scrolls off is let go** (#478). The moment none of its rows are on the page — after `↓`/`↑`,
  `PgUp`/`PgDn`, a wheel notch, or a resize that reflows it off — the brackets go and the status row leaves reference
  mode, so `⏎` never opens something the reader cannot see and `esc` is never spent on it. Scrolling back does not
  bring it back. Its rows are every row a run of it is drawn on (`Span.Reference`, asked by
  `Scroll.ReferenceOffThePage`), so an address wrapped across two rows is kept while either shows, and the middle row
  of one wrapped across three counts.
  The selection itself still stays where it is, by the "What moving settled" contract: only the reference is let go
  eagerly, because only a reference can be opened by mistake. It is checked on the rows each frame lays out anyway,
  and only while a reference is held (`PaintedView.HeldReference`/`LetReferenceGo` → `Shell.LetReferenceGo`), so
  scrolling with none held lays nothing out it did not before (#51). The status row is added to the window first so
  that Terminal.Gui, which draws the views added first last, draws it after the content: it says what the content's
  frame settled, in that same frame.
- **Bracketed `‹reference›`** — brackets, always drawn, in colour and no-colour terminals alike. ("Marked" is the
  word `Post.Marks` has, which is the collision the rename was about — GLOSSARY.md.) The brackets take
  their own role (`Role.ReferencePicked`), independent of whatever role the bracketed text already carries, so a
  picked hashtag stays hashtag-coloured and only the brackets shift. Underline was considered — a separate SGR
  attribute, zero width cost — and set aside in favour of brackets; the two added columns on the one row a pick
  lands on are an accepted cost.
- **The status row swaps** to a reference-mode row while one's picked — `←/→ reference · ⏎ open · esc back`-shaped,
  ahead of the screen's shared keys, and standing in for any of them it shares a key with, so `⏎` is announced once
  (`PostKeys.OnAReference`). Said by `Screen` itself rather than by each screen, which is why a screen's own list is
  `OwnKeys` and `Keys` is what the status row reads (#83).
- **`←/→` is announced before the walk starts too**, wherever the picked thing has a reference to walk to — the
  same `References` the walk itself asks, so the hint describes the **Picked** thing — even with the pick scrolled off
  the page, where the arrows would reclaim and act on another (#477), since a status row that changed as the pick
  crossed the page edge would flicker as the reader scrolled. Otherwise the only way to learn the walk exists is to
  press it. Ranked right behind `⏎:read`, so it never pushes the key used most on
  a post off the row; where there is no `⏎:read` (inside a post, the account header, a conversation) it goes in
  front (`PostKeys.BeforeAReference`). On an 80-column feed that leaves it in `…+N` and `?`, which is accepted.
- **`⏎` does four different things, refusals share one notice** (#85, #109). Which of the four is the role the
  reference draws in, since that vocabulary already tells them apart. A hashtag opens exactly the way a search result
  for one already opens — a `Tag` **Subject** brought up by `Arrival.Open`, which `SearchScreen` reaches through
  `Reach.Open` — same `FeedScreen`, same breadcrumb, no new screen type, and the rail's tag
  sets left alone. A mention opens the account screen, resolved off `Post.Mentions` — which the wire
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
- **A walked picture is a fifth, and the one that stays inside the TUI** (#516, ADR-0034). `⏎` on it pushes its
  **Picture screen** rather than handing its address to anything, told apart from a video's address — the two share
  `Media` — by the attachment its reference was made of (`Screen.OpenWalkedPicture`). Nothing is fetched to open it.
- **`⏎` lets the reference go, on every outcome** (#474): a hashtag pushed, a mention opened, a picture's Picture
  screen pushed, an address or an attachment's or link preview's address handed to the browser, and a refusal alike.
  `Shell.OpenReference` lets it go as part of opening, before the outcome is known, so no outcome is an exception.
  Walking back out of the hashtag, account or picture it opened, the first `esc` goes back rather than being spent on
  an old bracket, to the same post picked and nothing walked; after a link, the next `→` starts again at the first
  reference; and the status row leaves reference mode at once. A refused reference costs one `→` to pick again. A
  picture was once the exception, left walked under its Picture screen so that `esc` came back to it (#516); that was
  reversed for one rule across every reference (review of #514), the Picture screen keeping for itself which picture
  it opened on and stepping from there.
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
- **The question names neither the answer nor the post.** `Cast the answer you ticked? This cannot be undone.`, and
  `Cast the 3 answers you ticked?` for several — counted rather than named, because the ballot is on screen with every
  agreed answer drawn `[x]`, and the id of the post the poll happens to be on answers a question nobody voting has.
  This first quoted one answer, clipped at 25 columns; that put the row at exactly 80 columns and named something the
  reader could already check on the ballot, so it went with #219 (see *a confirmation names only what a reader can
  check*, below).
- **The ballot says how to cast it**, in a muted row of its own under the boxes: `v casts this vote, esc discards it`.
  On the poll rather than only on the status row, because this is the one moment in the shell where a key has to be
  found rather than remembered — the reader is looking at the boxes they have just ticked, not at the foot of the
  screen. It costs one row, and only while a vote is standing uncast.
- **No refetch.** `POST /api/v1/polls/:id/votes` returns the complete updated poll in the same response; that feeds
  the same `Change.PostChanged` `Mark` already reports (#234).
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
- **A poll goes behind the post's content warning, on the terms its text does** (#119). A post carrying one draws no
  poll at all until `x` — no options, no bar, no counts, no closing time — and nothing stands in its place, because the
  warning already up is what asks. Behind the *warning text* alone, not `IsWarned`: the sensitive flag is a mark over
  media, and a poll's answers are words its author typed, so a post marked sensitive with nothing written over it shows
  its poll exactly as it shows its text. The two vote keys follow what is on screen — `Screen.Poll` is `null` while the
  warning stands, which takes the digits and `v` off the status row and makes both no-ops, the same rule stated in the
  other direction for a poll that would take no vote.

### What refresh settled

There was no way to ask a destination for fresh posts short of leaving and coming back, or waiting out the 1-minute
cache. Streaming stays out of scope (below); a manual refresh is the in-scope answer (#68):

- **`g`, screen-local** — not a frame key, no `F5`, no dual binding; this shell has no existing precedent for either,
  and both would cut against internal consistency. The status row shows `g refresh` only on a screen that has one.
- **Lives on every destination that reads a list**: the cached feed destinations (home, local, federated, each tag set), plus
  Notifications, Messages, Requests, the post screen, and the account screen. The conversation screen and search
  results are left out — a live thread and a live search are each their own, smaller question, not decided here.
- **Evicts the screen's cache entry, then re-runs the same read that brought it up** — one path for every screen that
  refreshes: `Shell.Refresh` names no screen type and calls `Arrival.Again` with the screen's **Subject** (#100,
  #233). What to evict, what to read, what it becomes and what it counts are all things the subject already says: the
  timeline or list for a destination, `Thread` for the post screen, the account's four calls for the account screen,
  and the follow list's own read for a follow list. There is one cache, keyed by subject; only the rail's destinations
  and follow lists held whole are ever in it.
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
  arrival already follows. Notifications reads its count alongside, against the read marker, and a refresh reads them
  as arriving does: the marker moves past what arrived, and what arrived is what is drawn `unread` (ADR-0027, #397).
- **A refresh goes through `Enquiry` like every other fetch**, discarded unread if the screen it was asked from is no
  longer in front of the reader. No new
  in-flight UI beyond the breadcrumb's existing fetch mark; a second `g` while anything at all is in flight is
  a silent no-op — the guard is the breadcrumb's own `Fetching`, since a refresh landing on top of a boost or a
  deletion still in flight is the same stale answer by another route. Since #213 that mark waits 400ms before it
  appears, so a refresh can be in flight with nothing on screen saying so: `g` pressed twice inside that window is a
  no-op with nothing to explain it, which was true before the delay and is only easier to reach now.
- **What is on screen stands until there is something fresher to put in its place.** An arrival puts an empty screen
  up at once because what was showing is about somewhere the reader has left; a refresh is the one case where that is
  not true, so it takes neither that step nor the overtake — nothing is in flight to overtake, since the key is
  refused while anything is. A refresh a rate limit or a refusal ends is then a notice over the list they were
  reading rather than an empty screen where it used to be, with the cache already evicted. A follow list is the
  exception, and was before: it puts its fresh, empty screen up at once and fills it, which is the shape it opens with.
- **Every refreshed screen is replaced where it stands**, rather than pushed or reset: nobody has gone anywhere, so what
  was drilled through to get there is still under them and `esc` still walks back out of it. Nothing rechecks the top
  of the stack by hand — `Enquiry`'s one rule already drops an answer whose screen is no longer in front, which is the
  same rule `Reach.Swap` and a follow list's fill land by. Every refresh builds a new screen rather than changing the
  one on the stack, which is what starts the scroll offset again: the view notices a screen has been replaced by
  identity.
- **A hashtag walked to from a search has no refresh**, though it is the same `FeedScreen` a tag set on the rail opens
  onto. Which of the two a screen is is settled by who built it, not by what is in it: an arrival's feed refreshes and a
  pushed one does not. It is out of scope with the search results it was opened from.

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
  longer than 61 columns and a link with its end cut off is not a link. An `Image` joins the walk only where it is
  drawn (ADR-0034), so there is nothing here for `←`/`→` to reach.
- **A drawn still picture is walked to** after the post's text references, in attachment order beside the labels
  (#515). Walked, it is drawn between `⎡⎢⎣` and `⎤⎥⎦` in the column either side of its box that every attachment's box
  gives up (#519), a glyph on each row so that a box partly scrolled off still shows them, and `[` `]` on a box one row
  tall — in `ReferencePicked`, the role `‹ ›` use. Only the bracket columns change on walking to it or letting it go.
  Every cell of every row carries the reference, the box's own and its bracket columns alike, each row held out to the
  column past the box's right edge whatever the pixels left blank on it: so a click anywhere on the box or beside it
  walks to the picture and brackets it, and a double click there opens its Picture screen, in a feed and on the post
  screen alike (review of #514, ADR-0034). Which pictures are in a box is
  read off the rows themselves, written down by the call the content region lays them out through
  (`Screen.LaidOut`): a terminal that cannot draw links every picture, and a region too narrow for a box draws a
  picture's description alone, and either way the picture is not walked. A video's or animation's preview box, a link
  preview's picture and avatars are never walked to as pictures.
- **`⏎` on a walked picture opens its Picture screen** (#516, ADR-0034), a screen pushed onto the stack and crumbed
  `Picture by @author` (`Picture 2 of 4 by @author` where the post has several): the one picture fitted whole into the
  window less the description's rows and one more, at its shape and centred across, never cropped or stretched out of
  shape; what its author said it shows (`PostMedia.Shows`, so `a picture, undescribed` where they said nothing) wrapped
  and centred beneath it, row by row, shown as written, markup and all, and cut at a quarter of the content region's
  rows (one at least), its last row shown ending in `…`, so that a description as long as a whole article — one bridged
  from a blog can be — never squeezes the picture out, the picture being given its room first (review of #514). Under
  that, the one more row: kept for saying why the original will not come, centred, and blank where it is coming or here,
  so that nothing moves when it is said. Nothing under that: a hint row there saying `esc back` again was taken out, the
  status row already listing the keys, and its row given to the picture (review of #514). Its box is settled again on
  every frame from the room the window gives it, so a resize draws the picture to fit the new window. It draws the
  instance's original (#517): an empty box until the original lands — blank cells in the page's colour, with no
  **Stand-in**'s blur or shade, which felt off on a screen of one picture (review of #514) — then the original in the
  same cells, the box settled from the original's shape (`PostMedia.OriginalShape`, the smaller copy's standing in where
  the instance said none) so that nothing moves. Not the smaller copy the timeline already holds, though it is there at
  once: drawn first and swapped for the original, it read as the picture flashing (review of #514). The smaller copy is
  drawn, stretched to the box, only where the original will not come, and is wanted by the screen only then. The
  original is a `Drawn` of its own (`Drawn.Original`), held apart from the timeline's pictures: it counts against
  neither budget and lets none of them go to make room, may read up to 32 MB (`Pictures.MostBytesHeldApart`) where an
  inline picture stays at 8 MB, is decoded no larger than the window, and is let go the first frame that does not want
  it — so once the screen goes, while the smaller copy stays held for the timeline, its download called off where it is
  still on its way. `esc` goes back at its first press, to the screen it was opened from with the same pick and nothing
  walked, opening it having let the picture go as opening a link does (review of #514); on the post screen as in a
  feed.
- **`←`/`→` on the Picture screen step along the post's still pictures** (#518), in the order they are drawn, a video or
  the like on the same post passed over, each drawn as any picture is there with its own description, and stopping at
  either end rather than coming round, as the walk does. The crumb counts them where there are several,
  `Picture 2 of 4 by @author`, and the status row offers `←→`; neither says anything of them where there is one. The
  steps are the screen's own verbs (`Verb.NextPicture`, `Verb.PreviousPicture`) rather than a walk of its references, so
  the first `esc` still goes back, to nothing walked underneath whichever was stepped to; the screen steps from the
  picture it was opened on, which it holds itself rather than reading off a walk underneath. The screen sends for the
  originals of all the post's pictures, not only the one shown: that one first, and the rest from the first frame one of
  them has landed or been refused. It holds every one landed or refused apart until it goes, so a step no longer lets go
  of the picture it leaves (review of #514); one stepped off before anything has landed is let go, its download called
  off. A step to a picture whose original has landed draws it at once, with no empty box first, and a step back finds
  the one left still there. A step to one still on its way shows its empty box, or the smaller copy where its original
  was refused, and what is said of an original under the description is only ever about the picture shown. Where
  pictures are drawn as Kitty placeholders or as sixel, the landed ones not shown are encoded ahead in the boxes they
  are drawn in and on the rows those boxes set them on (`Line.Ahead`), so the step draws its picture on its own frame;
  one encoded ahead for a window since resized is dropped, never having been sent. The cost is up to four originals
  held at the window's size while the screen is open (ADR-0034).
- **A warned post's attachments are drawn only once the reader has asked for them.** A spoiler text, the instance's own
  sensitive flag, or both, and `x` shows them along with whatever else the post is hiding (#113). Until it is pressed
  there is no box, no label, no description and no address — and no `Wants`, so nothing is fetched and nothing is
  decoded, which is the point rather than a side effect: scrolling a feed of sensitive posts costs no data for pixels
  nobody asked to see. A post carrying only the flag says `⚠ Sensitive media` and `x  show it` where its attachments
  would be, because a post already showing a warning is already asking and a post showing neither would be hiding
  something with nothing on screen to say so. Since #116 the flag covers a **link preview** on the same terms, whether
  or not anything is attached beside it — so that one prompt stands above everything a flagged post is holding back.
  The prompt is the one part of this the conversations list leaves off, `x` having nothing to act on there (#120,
  below); the warning above it, and the hiding itself, are the same on every screen.
- **Ghostty and kitty are sent each picture once, and draw it as text** (#292, ADR-0022). The picture is drawn as
  Kitty's Unicode placeholder cells in the content panel's own rows, so it moves in the same frame as the text, a box
  half off the page draws the rows still on it, and a scroll sends no image data. It is encoded off the UI thread, a
  screen ahead of the page, and its box keeps the rows it reserved until it is ready.
- **Only a terminal known by name draws placeholders.** Ghostty and kitty are recognised from their environment at
  startup; nothing can ask a terminal whether it draws placeholders. WezTerm takes Kitty graphics but prints
  placeholders as boxes, so it — like Windows Terminal, Warp, and anything inside tmux or screen — draws through a
  `PictureView` box: through Kitty where Terminal.Gui says the terminal speaks it, and through sixel otherwise
  (ADR-0023). Terminal.Gui says so only for kitty and Ghostty, so in practice every box is sixel.
- **A sixel is encoded once for each cut of a picture, and handed to the driver ready** (#292, ADR-0023). A picture
  is scaled to its box once; a box straddling the edge of the page is framed to the rows still on it; each cut is
  encoded once, kept, and encoded ahead off the UI thread for the next few rows of a scroll. A notch over sixel
  photographs went from ~104 ms to ~29 ms. Sixel still sends every visible picture on every step — that is the
  protocol.
- **A frame paints each cell once** (#292). The content panel's frame paints its top and bottom edges and the two end
  cells of each row between, not whole rows for the clip to cut down; a row paints its spans and then clears only
  what is left; and the viewport is not cleared before the rows that cover all of it. A wheel notch over text went
  from ~16–19 ms to ~11–13 ms, headless; what remains is Terminal.Gui's own.
- **A placeholder cell is the size the kernel says** — the window's pixels over its cells (`TIOCGWINSZ`), then the
  protocol's answer, then 10×20. A box keeps the protocol's answer, which is what Terminal.Gui draws it by.
- **There is no cell-based fallback.** A terminal offering neither sixel nor the Kitty graphics protocol links every
  attachment, a photograph included, exactly the way the CLI writes one. The coloured-block rendering the ticket asked
  for was built and rejected on the evidence: a photograph as a few dozen rectangles resembles nothing and is worse
  than the description it replaced.
- **A picture is drawn at the full width of the column it is in, at its own proportions**, capped at 16 rows in a feed
  item and 32 on the post screen. Width-driven rather than height-driven — that is the difference between an inline
  picture and a postage stamp.
- **An attachment's box gives up a column either side** (#519, ADR-0034). A still picture's box and a video's or an
  animation's preview are drawn a column in from each edge of the content region, walked or not: those columns are
  where a walked picture's brackets go, so walking to one moves nothing, and a video's box lines up with a picture's on
  the same post. A link preview's picture and every fixed box keep the width they had.
- **Fixed boxes are cropped to fill; shaped boxes fit** (#438). Every drawn picture has a **picture box**
  (`PictureBox`), settled before its pixels arrive and holding its Stand-in until they do. A shaped box — a post's
  picture, a link preview's — is the shape the instance reported, 16:9 where it reported none, and the picture is fitted
  inside it, centred, with slack beside it or above and below where the shape was a guess. A fixed box — an avatar, 4×2
  on a byline and 8×4 on an account header; a pending attachment's picture, 3×1; the edit public profile form's avatar,
  4×2, and header, 12×2; a custom emoji, 2×1 — is filled: a picture of another shape is cropped to it from the middle,
  as an instance crops an avatar, and never stretched. Kitty and sixel take a picture to its box by the one step
  (`PictureDecoder.Filled`), so the two cannot crop differently. A Stand-in's blur is the exception: decoded square, it
  is stretched to its box, which gives it back the picture's proportions (ADR-0025's amendment for #438).
- **A custom emoji is a picture two columns wide and one row tall, in the line** (#523, ADR-0035). A shortcode the post
  lists — `:php:`, `:US_AZ:` — is drawn in a post's words, and in the preview of the reader's own post under a
  notification, as a Unicode emoji sits: wrapped as one character two columns wide, so `:US_AZ:Phoenix` breaks before
  it where it does not fit and never inside it, and dropped whole where a row is clipped. Whether it takes those two
  columns or its shortcode's letters is the Raster's alone: on any terminal that draws, the two columns are held from
  the first frame, shaded as a Stand-in until its still picture lands and for good where it is refused or never comes,
  so nothing rewraps. Through Kitty's placeholders it is then two placeholder cells, its picture fetched and decoded
  once for every post that uses it, keyed by its static address, and sent to the terminal once per box size. Through a
  box, sixel and Kitty's, it is drawn from a pool of boxes of its own (`Placing.MostEmojiBoxes`, 64), built before
  anything is drawn as the other 24 are, and on sixel cut into a cache of its own (`SixelPictures.MostEmojiHeld`, 128
  cuts), so a page of them never leaves an attachment or an avatar undrawn nor throws away the cuts a photograph was
  encoded ahead for; one that finds no box left is painted as its shaded room again, and nothing moves (#527). Where
  nothing draws, the shortcode is written as the instance wrote it, in `muted`, so it reads as a picture that was meant.
  A `:word:` the post does not list — a time like `10:30:45`, an emoji since deleted — is the words someone wrote, in
  `body`, and so is one written inside an address or a tag. An animated custom emoji is drawn at its still. A custom
  emoji is no **reference**: it is not walked to, a click on it picks the post, and it is never on the Picture screen.
  A warned post's are not sent for until its words are shown. The CLI writes the shortcodes as written, text and JSON.
- **A display name draws its custom emoji the same way** (#524, ADR-0035): `Jeff :verified: :US_AZ:` shows its pictures
  in the feed's two-row byline and the post screen's three-row one, the `boosted` and `boosted by` line, a
  notification's names line, and every Account block — a follow list, follow requests, search results, Discover's
  suggestions, a post's or a notification's actors, and the account screen's header. The emoji are the ones the account
  lists, the author's own rather than the post's, and each actor's own on a names line naming two. A name clipped to
  its room holds each emoji whole and drops one that does not fit with the rest of what is cut. The row is measured as
  drawn, two columns an emoji, so a byline's audience and age keep their column, and a names line's sentence follows
  the last name with its age kept at the right. Familiar followers are named by handle on the account screen, so they
  draw none.
- **So do a content warning, a poll's options, a bio and custom fields** (#526, ADR-0035). A warning draws the emoji the
  post lists, which the instance keeps for the warning and the words alike, while it hides the words and once they are
  shown, and in place of the words in a notification's preview; it is one row, clipped with each emoji held whole. A
  warned post's own words want none of their pictures until `x` shows them. A poll option draws the emoji the poll
  lists, its own list apart from the post's, after the bar, share and count, which keep the columns they take on an
  option written without one. An account screen's bio and each custom field draw the emoji the account lists, a field's
  label as well as its value, the label's shortcode muted with the rest of the label where nothing draws.
- **The description stands above the picture**, so it does not move when the pixels land under it. Until they do, the
  box under it holds its Stand-in, the size the picture will be, so nothing moves when the picture replaces it.
- **Nothing about a picture is ever an error,** but one. A fetch that fails and a file that will not decode both leave
  the description standing on its own, which is what a terminal that cannot draw shows anyway. The exception is the
  Picture screen's original (#517): one larger than 32 MB, or one that cannot be fetched or decoded, has the smaller
  copy drawn in its place and `The original is too large to show.` or `The original could not be fetched.` said on a
  row of the screen's own under the description, kept for it whether or not anything is said there, so that nothing
  moves; never on the status row, which goes on listing the keys (review of #514).
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
- **Its box is reserved from the card's own `width`/`height`** (#348, ADR-0025), as an attachment's is from its
  metadata: there from the first frame, shaded in its `stand-in` until the picture lands, and the picture fitted and
  centred inside it where its proportions differ. A card whose sides are not both positive — instances send `0` where
  they have no picture — is no shape, and the box is the same 16:9 default an attachment without one gets.
- **Its Stand-in is a blur where the card carried a blurhash** (#349), as an attachment's is: decoded in-repo
  (`Blurhash`, memoized apart from the picture cache in `Blurs`) and drawn over the shade through the same raster path
  as the picture, at the box's size, then replaced by the picture in place. No blurhash, or one that does not decode,
  is the shaded fill. A warned post draws no blur until asked past.
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
- **`j` and `k` reclaim a pick that has scrolled off screen, by the first start on the page.** With no row of the
  selected thing on the page, the next `j` or `k` picks the first thing whose *start* is on the page instead of moving
  from a thing the reader can no longer see; the reclaiming press does nothing else, and pressing again moves normally
  from there. A thing's start is the first row of its content: a post's content warning, else its text, else whatever is
  attached, polled or previewed, whichever is drawn first, below its boost line, its notification's strip and its byline
  and avatar; a notification's, the post it is about (the reminder of the reader's own post under a favourite). A thing
  with no content — a follow, an account's header block or row, a hashtag — starts at its first row. Each post marks the
  row its content starts on where its rows are built (`Line.ContentStarts`, in `PostLines`), and `Scroll.Reclaim` takes
  a thing's first row so marked, else its first row stamped with the thing, so no screen keeps its own record of where
  things begin. This replaces #472's rule, which started a thing at its first row, after use: scrolling just past a
  post's byline and avatar, with its text still on the page, the next press grabbed the post after it rather than the
  one being read (asked in review of #480). A post whose foot fills the top of the page is passed over for the first
  thing the reader can see beginning (#472). Where no thing's start is on the page, because one post taller than the
  page fills it, the reclaim falls back to the topmost thing with any row showing, so `↓ ↓ ↓ j` on a tall post picks out
  the post being read rather than the one after it — and a page scrolled past the last thing entirely, which is the
  blank under it, reclaims that last thing rather than nothing. "Off the page" still means *no* row of the pick shows: a
  pick with only its foot on the page is where the reader stands, and `j`/`k` move on from it. The reclaim is worked out
  once, in `Scroll.Reclaim`, and every key that reclaims asks that one answer through `PaintedView.Reclaimable`.
- **The keys that act on the pick reclaim first.** With no row of the pick on the page, the first press of a key that
  acts on it — `⏎ read`, `b`, `f`, `r`, the poll's `1`–`0` and `v`, and every other key that needs a pick — only
  reclaims, picking what `j`/`k` would; the next press acts on what is picked now, which is on the page. A favourite, a
  boost, a reply, a vote or a read never lands on a post the reader cannot see, and the cost is one extra press. Which
  keys those are is a property of what the key means, not of whether the status row offers it right now
  (`Verbs.NeedsAPick`, and `Keymap.NeedsAPick` for `F` on Discover and `m` on the conversations list, whose meaning
  turns on the screen): `d`, `p` and `e` on somebody else's post, `x` with nothing hidden, and the post keys while a
  thing with no post is picked reclaim too, rather than being refused or ignored about a post the reader cannot see.
  `D clear all` does not: it clears every notification rather than the picked one, so it asks at once and leaves the
  pick where it was, as decided in review of #480. `ShellWindow.Do` asks it once an open question and a
  prompt taking letters have had the key, before the key is carried out, and asks `PaintedView.Reclaimable` what to
  reclaim, as `j`/`k` do. A pick with its foot on the page is acted on at once, and keys that need no pick — `g`, `c`,
  `esc`, `?`, the rail — reclaim nothing. The status row does not change as the page moves, so it cannot say this; the
  `?` screen's *Everywhere* list does, in a row of its own: `off-page pick   the first key picks what is on the page`
  (#476).
- **A row says which post it is part of.** `Role.Selection` marks only the post already picked out and so cannot name
  any other; every screen holding a selection numbers its rows with the same ordinal `Screen.Pick` takes, including the
  four that do not number a plain list of posts — the post screen, where the ancestors come first and the post itself
  is at `ancestors.Count` (#86), search across its three kinds, notifications, and direct messages.
- **The offset starts again whenever the screen is replaced by another one.** Pushing a screen, arriving at a
  destination and refreshing all mean different rows, and an offset made on the last lot says nothing about this one —
  so it starts at row 0 and follows the pick again. On every screen but one that is also where the pick is, since they
  open on their first thing; the post screen opens on a post with its ancestors above it, and following the pick is
  what carries the page down to it on the first draw rather than opening the reader onto the top of somebody else's
  thread (#86).
- **A screen walked back out to gets its page back, which is the one exception.** A pop is the replacement whose
  premise is false: the stack hands back the very screen that was drilled off, holding the very list the offset was
  made on, so starting again moves the page under a reader who has not moved — and the further down the feed they were
  reading, the further it moves (#133). Each screen keeps the row its page last began on and whether that page was
  still following the pick (`Screen.Began`, `Screen.Followed`), which the window writes onto the screen it is leaving
  and resumes on the one it is arriving at (`Resume`). The follow flag is carried as well as the row, because a reader
  who had walked the page away from the pick with `↓` should come back to what they were reading rather than be
  snapped onto the pick. Nothing gates this on the pop and nothing needs pruning: a push, an arrival and a refresh
  each build a *new* screen, which remembers row 0 and following — so `Resume` is the whole of what a replacement
  does to the offset, and starting again is what it says on a screen nobody has read yet — and a screen is on the
  stack for precisely as long as there is somewhere to walk back to it from. A remembered offset can be stale by
  the time it is walked back to, the terminal having been resized or a post deleted out of the screen while the reader
  was away; the clamp the draw already applies is the whole of the answer to that, since the page is then wrong by at
  most the height of whatever changed and `j` reclaims — which is strictly closer than starting again.
- **`PgUp`/`PgDn` and `Home`/`End` walk the screen.** A page is a screenful of rows, because that is what a page is:
  somebody asking for the next one is asking about what they are looking at, not about how many posts happen to be on
  it. They used to move the selection by ten posts, which on a feed with pictures on it was several screens at once.
  `Home` and `End` used to pick out the first post and the last, on the reading that the ends of a list are things
  rather than places — but on a Mac they are fn-← and fn-→, pressed in passing, and they carried the pick off from
  wherever the reader had put it (asked in review of #480). So they take the page to the top of the list and to the
  foot of it, the last row on the page's last row, and leave the pick where it is, as `↓`, `↑`, `PgUp` and `PgDn`
  do; none of the four reclaims anything, and the next `j` or `k` reclaims from the page they came to. On the file
  browser and the attachments screen the arrows walk the cursor rather than the page, so there `Home` and `End` take
  the cursor to the first entry and the last.
- **`[` and `]` move the pick between headed runs, and bring the run's heading with them** — but only when that
  heading is not already on the page (#166). `Scroll.To` moves the minimum needed to show the selection, so an unaided
  jump downwards lands the picked row on the bottom row with the section just left still filling the screen, which
  reads as a press that did nothing. Always anchoring the page on the heading fixes that and settles cleanly, but on a
  screen whose runs all fit at once it scrolls the first of them off the top for nothing. So the page moves only where
  it has to. The mechanism is a heading mark on `Line` and one function beside `Scroll.To`, which keeps the property
  the rest of this section rests on: a scroll answer is computed from the rows alone.
- **`[`/`]` reclaim, like `j`/`k`, and clamp like everything else.** Working out "the run after this one" from a pick
  the reader can no longer see is the exact failure reclaiming exists to prevent, so after `↓ ↓ ↓ ]` the jump runs from
  the thing `j`/`k` would reclaim: the first whose start is on the page (#472). At the ends it clamps and never wraps:
  nothing in this shell wraps, and with three runs a wrap saves one press at the cost of an ambiguous position. A reader
  already among the last run presses `]` and nothing happens, which is the clamp saying so rather than the key being
  broken.
- **`k` is the next post and `j` is the one before it**, which is the opposite way round from vim. Asked for
  deliberately, and written down because the vim reading is the one anybody will assume — so a future "fix" would
  silently reverse what `j` does on every screen in the shell.

### What reading on settled

#469 put **reading on** on Home, the first list to get it (spec #460). Home used to read 40 posts and stop, so a reader
who walked to the bottom had no way to see anything older without leaving the TUI. What this document now holds it to:

- **Nobody asks for it.** As the reader goes down Home and the page comes within one content-region height of the
  last row of the list, the shell asks the instance for the older posts behind the last one held and adds them below.
  It is measured in the region's own height, so it starts at the same moment however tall the terminal is, and it
  rarely, if ever, lets a reader see the list stop and wait.
- **The view starts it, whatever moved it (#461).** Walking the pick with `k` or `j` moves the page with it, and `↓`,
  `↑`, `PgDn`, `PgUp`, `Home`, `End` and the wheel move the page and leave the pick where it was.
  Either way it is where the page has got to that reads on, so a reader scrolling down with the pick still on the
  first post reads on just as one walking down does, and the pick stays put.
- **The window says where the page is, once per move.** Only the window knows how tall the region is and where the
  rows have scrolled to, so the frame that settles the page tells the shell how many rows lie under it
  (`Shell.Viewed`). It says so after any of the moves above, and on a screen coming in front (arrived at, drilled
  into, refreshed or walked back to), and on nothing else. A frame is drawn for many reasons that are not the reader
  going anywhere, a read on landing among them, and a list whose read came back with nothing new would ask again on
  every one. The reader's own moving is the only throttle.
- **Only ever further down.** What has arrived since is still **Refresh**'s (`g`), above everything already read.
  `g` builds a new screen read from the top, so it drops everything read on.
- **Nothing moves.** The older posts go under the last one held, and the pick and the rows on the page stay where
  they were.
- **One read at a time per screen.** Moving about near the end while a read is in flight asks for nothing more.
- **The foot of the list says what is happening.** It is one muted row under the last post: "reading on…" while a read
  is in flight, "couldn't read on" after one failed, and "that's everything" once the instance has nothing behind
  what the list holds, which makes it **whole**. A whole list is never asked again, and a quiet one whose first read
  held everything says so straight away. Between reads, with more to come, the row is not drawn. Neither is it drawn
  on an empty list, whose own notice already speaks for it. On a list of people — a Follow list, the follow requests,
  either kind of actors — a blank sets it apart from the last block, as one sets each block from the next, so it does
  not read as part of the last person (#489).
- **Being whole is the instance's call, not a count's.** The instance names no next page, or a page comes back empty
  (ADR-0007). A short page that still names a next page is not the end. What the shell holds is the instance's own
  onward position, carried on the `Fetch` and handed back as it came (ADR-0007's amendment), never a page or a cursor.
- **Leaving mid-read drops the answer**, under the stale rule every enquiry keeps: nothing lands on a screen the
  reader is not looking at. Walking back out hands back the same screen, still near the end, and it reads on again
  from where it was. The dropped read cost nothing.
- **A read that fails says so, and is tried again by the reader's walking** (#462). The foot says "couldn't read on"
  and the enquiry's **Notice** says why, as for any failure. The list is left as it was, and where to read on from does
  not move. The next time the page nears the end, which is any move that tells the shell where the page is, it asks
  again from the same place. No timer ever does: a client retrying on its own would ask the instance on the reader's
  behalf while they were not looking. `g` isn't needed either, and pressing it would lose their place.
- **A rate limit is waited out, and the read carries on from where it stopped** (#462). A reader does not throw a
  limit that stops it part way through: it answers what arrived and the limit together, on the `Fetch`, with where the
  refused page would have started. So a read on doesn't land that as it stands. It waits the limit out the way every
  **Enquiry** waits out a thrown one, counting down on the status row while the foot still says "reading on…". Then it
  reads on from where the limit stopped it, and lands what arrived on both sides of the wait as one read. Nothing
  already read is lost, and nothing is asked for twice (`Enquiry.Ask.ReadOn`, which every list that reads on goes
  through).
- **A fetch a rate limit stopped is never whole.** It keeps where it stopped, so a first read cut short reads on from
  there rather than saying "that's everything". One stopped before anything arrived still shows the emptiness notice
  it always has ("Rate limited part way through"), and its foot draws nothing, as on any empty list. The notice goes
  once a read on brings anything, there and on a list it stopped part way with items in hand: what it said had arrived
  is no longer what has, and it would otherwise stand over the list read on for as long as the screen did.
- **The cache holds everything read on, and where to read on from** (#463). It is still the one cache, keyed by
  subject (ADR-0014): each read on that lands is added under what the list's entry already holds, and the onward
  position it came back with replaces the old one. A reader who has read on to 400 posts on Home, gone to Notifications
  and come back within the minute finds all 400 held. The screen is still a fresh one, opened at the top as every
  **Arrival** is, and walking to the end of it reads on from where the last read on stopped, so no call is spent on
  posts already in hand. A list whose read on found nothing behind it is held whole, and says "that's everything".
- **The minute counts from the last read on**, not from the first read. The cache asks whether this is still the list
  the reader just left, and a reader reading on was reading it a moment ago, however long ago its top was read. A read
  on adds only to a list the cache still holds: one that `g` or a change has forgotten stays forgotten, and is read
  again on coming back. Each read on that lands restarts the minute (spec #460's amendment): counted from the first
  read, a reader who had read on for longer than a minute would come back to the list read again from the top, and
  story 25 would rarely hold.
- **`g` drops all of it.** Refresh forgets the subject's entry and reads it again from the top, so the list opens at
  the top on what came back and the cache holds that and nothing older.
- **A change reaches a post read on exactly as it reaches one from the first read.** The change table asks the held
  list about every item in it, so a post marked or deleted forgets every cached list holding it, however it was
  reached, and a tie forgets Home with everything read on it. The screen showing the post hears the change and patches
  it in place, as it does any post.
- **Every timeline reads on, and so do an account's posts (#464).** These screens read on, the same way Home does:
  - Home, Local, Federated and every tag set on the rail;
  - a hashtag walked to from a post or a search, which is the same timeline reached another way;
  - an account's posts, below the pinned run, following whichever run is showing: their posts, or **Posts and
    replies** once `s` has widened it. The pinned run is never read on. It is one page in the account's own order, and
    a post pinned long ago that comes back in an older page is still drawn once, in the pinned run (#182).
- **Notifications reads on too (#465).** The older **Notification groups** go under the last one held, read the way
  the screen was first read: grouped from `/api/v2`, from the `max_id` the instance's own link named, or, where the
  instance cannot group, each notification a group of one from `/api/v1` (ADR-0032). The instance groups a page at a
  time, so a group already held can come back on an older page with older notifications of its own. It stays where it
  is, drawn once. A group arriving live still goes on top, read on or not, and neither waits for the other nor moves
  what the other put. The older groups are behind the **Read marker**, so reading on moves neither the marker nor the
  badge, and draws them read (ADR-0027).
- **Direct messages and Follow requests read on too** (#466), the older conversations and the rest of the requests
  waiting put under those held, with the same foot. Each read on asks where the profile stands only with the people it
  brought. Whatever arrives live still goes on top (`Screen.PutOnTop`) while reading on adds at the bottom, and the two
  never meet.
- **Their badges are counted again off the whole list held after each read on** (`InboxCount.OffListReadOn`), as every
  read of a destination counted off its list sets its badge — so requests read on to 55, and then whole, say `55` rather
  than `40+`, and an older unread conversation read on is counted. Exact once the list is whole; while it is not,
  Requests says `N+` for the N held, every one of them waiting, and Messages says `N+` only where every conversation
  held is unread, the one case where what is held cannot be the whole count — so 3 unread among 80 held say `3`, as the
  first read has always counted them (spec #460's amendment). It is the list going further down, not anything arriving,
  so it never rings the bell, and it is the count the next one rises against. The cache keeps it with the list read on,
  so arriving there again within the minute says it too, not what the first read said. Answering a request or marking a
  conversation moves it as before: by one under an exact badge, and read again under `N+`. The next poll or stream
  re-read counts the first 40 again and sets the badge from those, as every re-read does: the `55` holds until then, and
  that re-read says `40+` again, which is still true, where a `55` kept over a fresher read could be wrong (spec #460's
  amendment). Notifications' badge is counted against the read marker, not off the list, and older notifications are
  behind it, so reading on there never moves it.
- **A browsed Follow list and a group's actors read on too** (#467), in place of the paging they had, which re-read
  the list from the top to a longer limit each time `k` or `End` landed on its last row. They read on by the same
  trigger, from the same onward position, with the same foot (*What a follow list settled*), and ask where the profile
  stands only with the people a read on brought. A held Follow list does not: it is read whole at once, as before.
- **Every list the spec named reads on.** Every timeline, an account's posts, the Inbox, a browsed **Follow list** and
  a notification group's actors — and, since #489, one side of a post's actors. Every other list (Discover, a search, a
  **Thread**, a conversation, a held Follow list) reads once and never asks, and draws no foot.

### What conversations settled

The last two screens, and the one place a screen writes words of its own into what a reader is sending:

- **Opening a conversation does not mark it read.** `⏎` shows the thread and leaves the mark exactly as it found it;
  `m` is the only thing that clears it (ADR-0013). A client that cleared it on the way past would make "what have I not
  read" unanswerable for anything that looked afterwards — including this shell's own badge.
- **A conversation is marked read by its own id**, which is not the id of any post in it (GLOSSARY.md). The same id
  opens it, and the two screens holding it — the list and the thread pushed from it — are both moved by one answer, so
  a row cannot still say `unread` under a thread just marked.
- **Every reply opens with the mention already in it.** Mastodon routes by the handles it parses out of a post's text:
  a direct post reaches the accounts its text mentions and nobody else, and on every other visibility `in_reply_to_id`
  threads the reply and notifies nobody at all — so a reply that named nobody would not reach the account it answers
  (#130). It is written where the reader can see and edit it rather than added silently on the way out, by the same
  `DirectMessage.To` that `dm send` uses, which is what lets a reader delete a name they did not mean to ping — and a
  reply that is nothing but the mention it opened with is refused as nothing written. In a conversation every account
  in it is named, not only whoever spoke last — an instance says who a conversation is *with*, which is who is still in
  it rather than who one message in it happened to name. Everywhere else it is the post itself: the answered account
  followed by everyone that post named, off `Post.Mentions` already in hand and so costing no fetch. That covers a
  direct message read outside its conversation too (#132) — Mastodon delivers a direct post to the accounts its text
  mentions and nobody else, so its mentions *are* the rest of the conversation, and naming only its author would answer
  a three-way message to one of the accounts having it. The reader's own account is never written in — including where
  the message named them, which is how it reached them, and on a message they wrote themselves, which therefore opens
  addressed to whoever they sent it to — and an address this client cannot parse is left out of the mention rather than
  thrown over the reply, where the reader can see that it is missing.
- **A reply lands at the end of the thread it answers, and on the row it was opened from** — rather than appearing
  nowhere until the conversation is read again. A conversation is read in the order it was said in, what was just said
  is part of it, and it is the conversation's last word as well as the thread's last message.
- **A warned message on the list shows its warning and not the `x  show it` row under it** (#120). The row is an offer
  of a key, and the key has nothing to act on here: what this screen picks out is a conversation, so `x` finds no post
  and would do nothing — the same rule the poll keys follow, that a key announced where it does nothing reads as a
  shell that missed the press. The message stays behind the warning, on both halves of **warned** alike, and the way to
  read it is `⏎`, which opens the thread where every message *is* picked out and the row and the key are both back.
  Giving the list its own reveal was the alternative, and was refused for the reason #83 kept `Referencing` apart from
  `Picked`: it would have `x` acting on something the screen does not offer.
- **The unread indicator is the word, not a glyph.** This client's glyphs already say who can see a post — `○ ◌ ● ✉` —
  and a second circle beside `●` is one mark too many to tell apart at a glance. The word takes `rail-unread`, the same
  role as the badge counting it on the rail. A notification unread on arrival carries the same word, for the same
  reason (#397).

### What a compose's own content warning settled

`r` on a warned post opened an editor with nothing written over it, so a reply to a warned thing went out bare unless
its author remembered to warn it again by hand — which Mastodon's own clients do not ask of anybody (#123, #139, #140):

- **A reply opens on the warning of the post it answers**, in a field of its own on the row above the editor. A reply
  to a warned post is usually about the warned thing, and an author who has to re-type the warning is one who sometimes
  will not. For a boost it is the warning of the post *inside* it — the post the reply targets, and the resolution
  already made for every other key.
- **Pre-filled, not imposed.** The field is the author's from the moment it opens: kept, edited or cleared, and what
  goes out is whatever it holds when `ctrl-s` is pressed — letter for letter, spaces included, since a client that
  tidied a warning would be editing the author's words on the way past. Cleared, it sends no warning at all rather than
  a post behind a blank — an instance reads an empty `spoiler_text` as no warning, which is what
  `PostDraft.ContentWarning` already says in null.
- **The instance's sensitive flag is not carried across.** It is a mark an instance put over somebody else's
  attachments; a fresh compose has none, and nothing about answering a post says the answer's own media is sensitive.
  Only the words the author wrote cross over, which is the same split **Warned** draws everywhere else.
- **Both of the composes that publish a post carry the field** — a reply pre-filled (#123), a fresh post empty, having
  nothing to have been filled from (#139). `c` was the gap worth closing on its own account rather than for symmetry:
  `post create --cw` has always been able to warn a post, so the TUI was the one surface of this client
  that could not, and a reader who has just warned a reply reaches for the same key on a fresh post.
- **`e` carries it too, opening on the warning the post is already behind** (#140). `PostEdit` tells "leave the warning
  alone" from "take it away", and a field that opens *empty* says neither — which is what once kept the field off an
  edit. A field opening on the post's own warning says all three by construction: left alone it sends the same warning
  back, cleared it sends empty and the warning comes off, typed into where the post had none it puts one on. So the TUI
  always sets `PostEdit.ContentWarning` and `ChangesContentWarning` is always true from this surface. The third state
  is the CLI's, where `--cw` can be absent from the command line; a field somebody is looking at has no such state,
  because they saw the row and whatever it holds is what they want. What goes out is the field as it stands, letter for
  letter — `PostEdit.ContentWarningWanted` is what reads a field of nothing but spaces as none at all.
  - The warning round-trips exactly: `Post.ContentWarning` comes off the wire as `SaidOrNothing(status.SpoilerText)` —
    plain text, unlike `Content`, which is stripped through `PostContent.ToPlainText`. Re-sending it changes nothing.
  - Clearing it unblurs nothing. `PostAuthor.Edit` computes `sensitive: existing.Sensitive == true || ...`, so the
    instance's own flag survives an edit that takes the words away, and its `GetStatus` read stays as it is — the CLI
    still needs it to keep the "silence leaves it" promise. The other direction is not symmetrical and is not meant to
    be: a warning typed into a post that had none marks that post sensitive, which is what the same expression has
    always done for `--cw` and for `post create`. `e` reaches it now, and hiding more is the harmless direction.
  - The one real exposure is a stale `About`: saving re-sends a warning that may have been changed elsewhere since the
    timeline was read. The body already carries precisely that, the editor being pre-filled from the same possibly
    stale post, so it is not a new class of risk.
- **The band is held on all three anyway**, both rows blank on the edit while there was no field to put in them (#142).
  A row that holds a place and says nothing is against the habit `PostLines.Parts` keeps — a part with nothing in it is
  skipped rather than spaced — and it was the exception that earned it: an editor that starts higher on `e` than on `c`
  moves the thing the reader is typing into, which is worse than two rows of chrome that are briefly empty. #140 filled
  the field in and nothing else shifted, which is what the row was being held for.
- **`ctrl-w` moves the typing between the two**, because a terminal takes the keys of whichever field has them.
  While the warning has them the editor keeps its text and its place, every printable key goes into the field — `?`
  and `/` included — and the status row says `ctrl-w  back to the post`. Neither field offers the keymap: `?` is a letter in the post and
  the warning alike, so compose's status row names no `?` (#320). `esc` still
  means what it means everywhere: up one level, throwing the whole compose away.
- **The warning is a field of its own** (#320), a one-line text field laid over its header's value column the way the
  editor is laid over the body: it selects with shift and the arrows, moves by word, takes a paste and takes the
  mouse. It takes `esc`, `ctrl-s` and `ctrl-w` off the widget as the editor does, and `enter` hands the typing back to
  the post. A click into either field moves the typing there and keeps `ctrl-w`'s direction in step. Its keys are its
  own before they bubble, so `?` is a letter in it with no rule of the shell's, and the shell carries no letters into a
  compose screen at all. Selected text is drawn in `selected-text`, as in the post.
- **The row says it is there when it is empty**, muted — `⚠ no content warning` until the headers layout made it
  `⚠  none · ctrl-w to add`, and `Warn  none · ctrl-w to add` since #375 — and `say what it's about` while it is being
  written (#317, below). A row a reader can type into is a row they have to be able to find, and the status row's
  `ctrl-w` is the other half of saying so. Written, it takes the same `content-warning` role a warned post's own
  warning is drawn in, label and all, so a warning being written
  looks like the warning it will become. The caret is the field's own, the terminal's cursor, since #320 — before
  that a painted `▌` stood for it, the way the search prompt's does.
- **A blank row stands above it, on every compose alike** (#143). It was ADR-0015's reply block that used to end in
  one, which is why `c` and `e` had none: hung off the block, the space appeared on a reply and nowhere else, and the
  one row all three screens have in common was the row they spaced differently. The blank belongs to the warning now,
  so a reply reads label, quote, blank, warning, editor and the other two read blank, warning, editor. The headers
  layout (#317, below) keeps the rule and moves the blank: it stands above the whole block of headers, on every
  compose alike.
- **It costs two rows above the editor, and they are the last rows to give way** — one row since #317, the warning
  header, which is still the last to go. ADR-0015's block already gives up
  its tail on a terminal too short for everything; the warning band does not, being a row the reader types into rather
  than a quote of something they can see elsewhere. One of the two is a row the reply screen was already spending.
- **The screen says what goes out; the shell puts it** (#146). `ComposeScreen.Outgoing` answers an `Outgoing` — a
  `PostDraft` to publish or a `PostEdit` to save, whole — and `Drafting.Send` makes the one call, pops, and says `Sent.`
  or `Saved.`. It was the shell that used to assemble both, which meant the shell was what had to know which of the
  screen's two warning members was the right one for which purpose: the raw field on an edit, where empty means *take
  the warning away*, and the trimmed one on a publish, where empty means *no warning at all*. That is one decision
  about one field, and it is the field's screen that makes it now. The field itself is nobody else's to write, and the
  reading that decides between a warning and none is `ContentWarnings.Written` in `Wooly.Core` — one rule that the
  field, `--cw` and `PostEdit.ContentWarningWanted` all read, rather than the same expression written out three times.
  The CLI's third state is untouched by that: it is `--cw` being absent from the command line, which is a fact about
  the invocation rather than about what was written in it.

### What the compose headers settled

Compose is laid out as a mail client's compose — variant A of the prototype on `prototype/compose` (#313, #317) — on a
fresh post, a reply and an edit alike:

- **Rows, top to bottom:** a blank; the headers — From, To, Lang, the reply header and its quote, Warn, and Media with
  a row under it for each pending attachment on a fresh post or a reply, or for each the post carries on an edit
  (ADR-0024, #338, #340, #375, #381); a hairline; a
  blank; the editor; a hairline; the row the count sits on (#319). Two columns of padding either side of all of it.
  The hairlines are `panel-border`.
- **Headers are a right-aligned label column five wide, two spaces, then the value** — five for `Media`, the widest of
  the words every header is labelled with (#375). `From` reads the profile's
  handle in `byline-handle` and ` · instance` muted — the instance said even with one profile set up, since this is
  the row a reader checks before sending. A reply's header is labelled with the feed's own reply mark rather than a
  word, and worded by `PostReplyName` (`answering @handle`, or `continuing` for a self-reply), with up to three
  non-blank rows of what is being answered under it behind a `│ ` gutter. The warning's is labelled `Warn` — a word
  like the rest, where it was the feed's bare `⚠` until #375 — lit in `content-warning` while there is a warning or one
  is being written and muted otherwise, so the reader sees at a glance whether the post is going out behind one.
- **To says who the post goes to** (ADR-0024, #338): a row of radio buttons, `● public  ○ unlisted  ○ followers
  ○ direct`, the filled bubble the one chosen. It starts on what would go out — `default_visibility` where the config
  sets one, and on a reply the narrower of that and the post being answered (the post's own where the config sets
  none) — and sends what it shows, chosen only where the author moved it, so `PostAuthor` narrows a starting
  preference exactly as on the CLI. Where nothing is known on a fresh post it starts on `account default` and sends
  nothing, and `account default` stays the row's first choice — `● account default  ○ public  …`, or
  `◂ ● account default ▸` where that does not fit — so an author who steps off it can step back, by key or by click,
  and send nothing again. On a reply, values wider than the post being answered are `muted`, skipped by `←`/`→` and ignore clicks; on
  an edit the whole row is `muted` and takes neither keys, clicks nor the typing, since Mastodon cannot change it.
  Choosable values are `body`; the chosen one is `selected-text` while To has the typing, which is how a row with no
  caret shows where the typing is, and reads reversed with no colour. Where the row does not fit, it falls back to the
  one value with an arrow either side, `◂ ● followers ▸`, the arrows `muted` and a click on one a step.
- **The screen paints every row and says where the editor goes**, both from one layout, at the content region's
  height (`Drawing.Height`) — so what is painted and where the editor is laid over it cannot disagree. Where nobody
  says the height, it lays out as tall as its rows and the editor's least want.
- **On a terminal too short for everything, rows give way in a fixed order**: the pending attachments' rows first, by
  folding into the Media header's line (below); then the quote's tail, the blanks, the reply header, the foot, `From`
  and `Lang`, the Media header, the hairline under the headers. **To**, the warning header and three rows of editor are
  kept whatever the height (#338) — the rule ADR-0015 and #123 already kept, with more dressing in front of it to go first.
- **The foot counts what has been used of the post's limit** (#319): `n / limit`, right-aligned inside the padding,
  `muted` up to nine tenths of the limit, `quota-low` in the last tenth and `error` past it. It counts the way the
  instance judges a post (`PostLength`): by grapheme cluster, any address Mastodon links (`https://`, `gemini://` and
  the rest) as the instance's length for one, a mention of somebody elsewhere as its `@username` alone, and the warning
  letter for letter on top of the post, as the instance adds it. The screen's text follows the editor on every edit,
  so the count does too, and a reply or an edit counts what it opened with from the start.
- **The limit is the instance's own** (`IInstanceLimits`): `/api/v2/instance`'s `max_characters` and
  `characters_reserved_per_url`, or `/api/v1/instance` where there is no `v2` — Pleroma's `max_toot_chars` included.
  `LimitsByInstance` asks it the first time a post is written on an instance rather than at launch, so a reader who
  never writes one is never charged for it, then holds it by instance for the session: two profiles on one share it,
  and a switch keeps it. Until it lands, and wherever the instance does not answer, the count is out of Mastodon's 500
  and 23. It is not put through the enquiry, so a failure says nothing and a rate limit counts nothing down over the
  post; it is asked again the next time a post is written.

### What pending attachments settled

A fresh post and a reply attach files under a **Media** header, under Warn, in the layout chosen from the prototype on
`prototype/374-attachments` (#374, #375). An edit carries the post's own attachments through unchanged (ADR-0008), and
lists them under a read-only Media header of its own (#381).

- **The header says what it holds, then the key that adds to it, all muted**, as Warn does: `Media  none · ctrl-o to
  add`, `2 of 4 · ctrl-o to add` counted against the instance's limit, and `4 of 4` once the post is full.
- **A row per pending attachment, under the header**: the grip `⠶` muted, its picture three columns wide and one row
  tall, cropped to fill rather than squashed (#382, #438), its name cut with `…` to 32 columns, its kind (picture, animation, video, sound) muted, its size right-aligned and muted,
  `x` in `destructive`, and one status column saying one thing at a time — the upload's gauge, `████░░░░  54%`, in
  `gauge` and `gauge-empty`; `processing`; once ready its description in quotes, or the quiet mark `no alt text`; or
  why it failed, in `error`. There is no mark for ready. **Nothing moves**: every column comes from the terminal's
  width alone, never a name's, and the status column is at most 40 wide. Where that would leave the status fewer than
  16 columns the kind goes first, then the name narrows as far as 12.
- **The picture on a row is drawn where the terminal draws** — sixel, Kitty through a box, or Kitty's placeholders —
  through the same path the feed's pictures take (ADR-0022, ADR-0023, ADR-0025): the row says it wants the picture,
  `Placing` asks the cache for it and places it, and sixel is quantized by `SixelPalette` for a `PictureView`, never by
  Terminal.Gui's own encoder, which tinted photographs red on WezTerm (#374). The file is read off the disk rather
  than sent for (`Drawn.Attaching`, a `file:` address `Pictures.Over` reads itself), under the same 8 MB cap a file
  server is held to, and decoded no larger than the row's box. **Three columns, not the one #375 held**: one cell of a
  photograph is a blot, and three are what the prototype tried in place and enough to tell roughly what a picture is;
  seeing it properly is the description editor's. The column is held on every terminal — the `stand-in`'s shade
  while a picture is on its way, blank where none is coming — so nothing on a row moves when one arrives, and a
  terminal that draws nothing lays the rows out the same. Only the pictures the decoder reads are sent for (JPEG, PNG,
  GIF, WebP): a video, a sound or a HEIC photograph keeps its column blank rather than a shade for a picture that never
  comes.
- **On a short terminal the rows fold into the header's line** before any other row gives way, wherever a row each
  would leave the editor fewer than three: `3 of 4 · 3 no alt text · 1 failed · ctrl-o to add · □ sensitive`, the
  failures in `error`. They unfold when there is room again. Folded, `⏎` on the line or a click on it pushes the
  **attachments screen** (`AttachmentsScreen`, story 58), crumbed `Media`: the header's line unfolded, a blank, and the
  compose's own rows (`ComposeMedia`, #387), drawn as under the header, the first picked. Every key and click a row takes under the header it
  takes there — `⏎` or a click on the description describes, `del`/`backspace` or a click on `x` takes off, `ctrl-z`
  brings back, `shift-↑↓` or a drag moves, `r` or a click on `retry (r)` retries, `s` or a click on the toggle flips
  it — and `ctrl-o` attaches more, all of it changing the compose's own draft through drafting; `↑`/`↓` walk the
  rows. `esc` goes back to the draft, the walk on the header.
- **The sensitive toggle ends the header's line**, after a `·`, once anything is attached (#379): `□ sensitive` muted
  while it is off, `■ sensitive` in `content-warning` while it is on, and `■ sensitive (warning)`, the same, while a
  warning is written — a warning puts the whole post behind a click already (ADR-0008), so the toggle is held on and
  locked, and `s` then says `The warning already hides what is attached.` Clearing the warning gives back whatever the
  author had set, which is kept underneath. The walk stops on the header between Warn and the post — on a fresh post
  or a reply whether or not anything is attached, since `⏎` there opens the file browser (#376) — its own words lit in
  `selected-text` rather than a selection bar, and once something is attached the status row offers `s sensitive`
  there; `s` on the header or a click on the toggle flips it. What goes out is the author's own setting, as the
  draft's `Sensitive`, and the warning as the warning.
- **A drop attaches**: a terminal pastes the paths of files dropped onto it, so a paste on a compose screen made
  entirely of whole paths to existing files of a type the instance accepts — split as a terminal quotes a drop, spaces
  escaped with a backslash or a path in quotes, `file://` addresses and `~` taken as the paths they stand for — attaches
  them, as many as the post has room for (`Dropped`). Any other paste, a sentence with a path in it included, is text
  as it always was.
- **`ctrl-v` attaches from the clipboard** of the machine Wooly runs on (`IClipboard`, #380), anywhere on a fresh
  post or a reply — in any field, on the Media header and its rows, which take no paste of their own, and on the
  attachments screen. **`alt-v` does the same**, because Windows Terminal and the console host bind `ctrl-v` to the
  terminal's own paste and never pass it on: there `ctrl-v` pastes the clipboard's text, or nothing when it holds only
  a picture, and `alt-v` is the key that reaches Wooly (ADR-0015). Both are the keymap's (`Verb.PasteFromTheClipboard`);
  the fields that take typing ask it before their own paste, which either key falls through to. The status row offers
  `ctrl-v/alt-v paste` on the Media header and on the attachments screen while the post has room. A picture there is written to `pasted-1.png`, `pasted-2.png` and so on — counted over the session,
  in a temporary folder of the session's (`PastedPictures`), taken away with every picture in it as the session ends —
  and attached from that file; copied files attach as a drop does, those of a type
  the instance takes, up to the limit, and where it takes none of them the status row says so. Anything else is the
  field's own paste, unchanged. The clipboard is read through the operating system, never the terminal, which only
  ever pastes text: `osascript` and AppKit's pasteboard on macOS, `wl-paste` under Wayland and then `xclip` on Linux,
  and Windows PowerShell on Windows (`OsClipboard`) — none of it needing Git. On a Linux machine with neither tool the
  status row says once a session that pasting a picture or files needs one, and text still pastes. `⌘V` is the
  terminal's and only ever carries text, and over SSH the clipboard read is the far machine's, so a paste there is
  text: expected, not a fault. On macOS Terminal `alt-v` types `√` unless Option is set to act as Meta, and `ctrl-v`
  works there anyway. An edit's `ctrl-v` and `alt-v` are text and read no clipboard.
- **Each file goes up the moment it is attached** (ADR-0026): drafting (ADR-0029) sends it through `IPostAuthor.Attach`,
  as the profile acted as, and feeds where it has got to back into the screen on the drawing thread — the screen holds
  it and draws it, and reaches nothing itself (ADR-0015). The calls are `AttachmentCalls`', drafting's collaborator for
  them, which hands every answer back as an event. Not through the enquiry: the row says how it is going, the status row
  says nothing, and it is not stopped by a screen pushed over the draft. **Every upload ends**: a refusal says the
  instance's own reason on the row; a dropped connection says `connection lost`, a rate limit `rate limited`, an
  instance that failed to answer `instance failed (502)`, and a call the client gave up waiting on `timed out`; and a
  failure nothing expected ends the row refused all the same, saying what it was — no row is left going up, and no send
  left waiting on one. None is tried again by itself (ADR-0006), though the author can retry from the row where a retry
  can help (below). A compose screen leaving the stack calls off what it is still sending, and leaves what went up for
  the instance to clear away.
- **`ctrl-s` waits rather than refuses.** With anything still going up or being processed it keeps the screen up and
  says `Will send once 1 attachment finishes — esc to stop.`, and sends once they have; `esc` calls the send off and
  says the draft is as it was. A screen opened over compose meanwhile — the file browser, a description — leaves the
  send asked for: back on compose it sends, where everything finished while it was away, or says again that it will.
  It never sends without them. With anything refused it does not send, and says so. The
  post goes out through `IPostAuthor.PublishAttached`, naming the pending attachments by id in the order shown — the
  route every TUI post now takes, attachments or none — and a post of attachments alone, with no text, sends.
- **The limits are the instance's** (`IInstanceLimits`): `configuration.statuses.max_media_attachments`, else 4;
  `configuration.media_attachments.supported_mime_types`, else Mastodon's own; and `description_limit`, else 1500 — read
  with the post's own limit, on the same answer.
- **The rows are walked to, and fixed where they are** (#378). `↑` from the post walks the rows from the bottom, then
  the header, then Warn, and `↓` walks back; a row walked to carries `▌` in `selection` against its grip and nothing
  else — its name stays in its usual role, picked or not, since a name lit as well read as a second thing picked —
  the bar in a column held for it so that nothing moves. On a row `del` or `backspace` takes it off
  the post, the walk staying on the row that took its place, else the one above, else on the header; `ctrl-z`
  brings back the last one taken off, in its place, with its description and as far as its upload had got — one deep,
  only while the walk is on Media or its rows, and not onto a full post; `shift-↑`/`shift-↓` move it a place, the post
  going out in the order shown. Not `alt-↑`/`alt-↓`, which macOS Terminal turns into word jumps. The status row offers
  `r retry` where it applies, `shift-↑↓ move`, `del remove` and `ctrl-z bring back`.
- **A retry is offered only where it can help**: a dropped connection reads `connection lost  retry (r)`, the offer in
  `key`, and `r` or a click on it sends the file up again from nothing — the author's to ask for, never done by itself
  (ADR-0006). So do a rate limit, which passes, an instance that failed to answer, and a call the client gave up
  waiting on. A file the instance refuses — too large, of a type it does not take, or any other refusal — says why
  and offers nothing, as does a failure nothing expected. Where
  the reason and the offer do not both fit the status column, the offer shortens to `(r)` and the reason is cut.
  Anything refused blocks `ctrl-s` until it is taken off or a retry goes through.
- **The pointer**: a click on a row picks it, a click on its `x` (or a column either side) takes it off, a click on
  `retry (r)` retries it, and dragging a row from anywhere on it moves it live — it takes each place the pointer
  reaches and the others make way, with no landing marker, and the release is not also a click (`ComposeMediaField`).
  Only a press on a row picks it up, and its release or click puts it down: some terminals report the pointer moving
  with no button held in the same words as one moving with the left button held, which taken for a drag moved the
  rows about after a click as the pointer passed over them. Two quick clicks on `x` take two rows off — the second the
  row that took the first one's place — though Terminal.Gui reports the second as a double click, which anywhere else
  on a row describes; the same on `retry (r)`. The attachments screen's rows drag the same way, a press there picking
  the row as it picks it up, and none of them is ever on the band. A drag let go of anywhere — the content panel's
  frame and the hidden rail's `»` on it too — ends there, so the next click is a click.
- **Past the limit**, a drop attaches as many as fit and the status row says `2 left out — 4 is the most a post can
  carry.`, or `Nothing attached — …` onto a full post.
- **Anything attached touches the draft** (#373), so leaving it asks first.
- **An edit's Media header only lists** (#381): a row for each attachment the post carries, in a pending row's columns
  wherever it has something for them — its small picture, sent for from the instance's preview through the feed's
  picture path (`Drawn.Kept`); its kind muted in the kind column, or in the otherwise blank name column where a narrow
  terminal gave the kind column up; and its description in quotes or `no alt text` in the status column. No name or
  size, which the instance does not hand back — under `Media  2 · kept as they are`, or
  `Media  none` with no key where it carries nothing. No grip, no `x`, no key or click that adds, removes, reorders or
  opens anything, and a drop on an edit is text. The rows fold into the header's line on a short terminal,
  `2 · 1 no alt text · kept as they are`, and with the same headers the editor starts on the same row as on `c`.

### What descriptions settled

Every pending attachment can carry a **description** — "alt text" on screen, the domain's word in code — written in
the description editor the prototype chose (#374, #377).

- **On a row walked to (#378) the status row offers `⏎ describe`.**
- **`⏎` on a row, a click on its description or `no alt text`, or a double click anywhere on it opens the editor**, a
  screen pushed over the draft (ADR-0015) and crumbed `Describe <name>`. A single click elsewhere on a row only walks
  onto it, so a slightly-off click pushes nothing.
- **The editor**: a blank under the panel's edge, `Description (alt text)` muted, a blank, the field, and a counter
  `n / limit` along the foot — muted, `quota-low` in the last tenth, `error` past the instance's description limit
  (1500 where it does not say), counted in code points as an instance counts them. On a panel 80 wide or more the
  field sits in the right half, and the picture being described goes top left, level with the label; on a narrower
  one it goes above the label, in a place of up to ten rows with a blank under it, taken from what the field can
  spare of its least three and not held at all where that is less than two rows (#382). Either place is held from
  the first frame, so the label and the field never move when the pixels land; the picture is set against its top
  left at its own proportions. Where the terminal cannot draw, or there is no picture to read, nothing is held above
  the label. `esc` and `ctrl-s` are both "done" and
  keep what was typed; there is no cancel. Done goes back to the row described, still picked, so the next row's
  description is a walk and a `⏎` away. A description can be written at any time, while the file is still going up
  included.
- **A ready row says its description in quotes**, cut with `…` inside the closing quote where the status column is too
  narrow, or the quiet mark `no alt text`. Sending never asks about a missing one.
- **A description goes to the instance once there is an attachment to put it on, and again whenever it changes**
  (`IPostAuthor.Describe`): as the editor is left, or as the attachment it was written for comes back ready. One at a
  time per attachment, the last one written last. `ctrl-s` waits on a description still on its way as it does on an
  upload, and sends one not yet taken first. A refusal, a dropped connection or any other failure is said on the status
  row (`The description of cat.png was refused: …`) and calls a waiting send off; it is sent again on the next `ctrl-s`,
  never by itself (ADR-0006).

### What the file browser settled

`ctrl-o` on a fresh post or a reply, `⏎` on its Media header or a click on the header's words pushes the file browser,
crumbed `Attach`, in the look the prototype on `prototype/374-attachments` settled (#374, #376). An edit opens none, nor
does a post already carrying all it can, which says `This post carries all it can — 4 of 4.`

- **It works on the real file system, and opens where the author last attached from** this session — else in the
  folder Wooly was launched from. Nothing is saved: the next session starts from the launch folder again. Drafting
  (ADR-0029) reads each folder (`LocalFiles`) and hands the screen a `FolderListing`; the screen reads nothing
  itself, as no screen does (ADR-0015), and says which folder `→`, `←` or `⏎` opens next.
- **Rows, top to bottom:** the folder, `~` for the home folder and cut from its start where it is long, with
  `2 chosen · 2 more fit` (or `4 more fit on this post`) against the right; the filter, `filter ss0229▏` or
  `type to filter`, with what is listed and the key that changes it against the right — `pictures, video, sound ·
  ctrl-a every file` from the instance's accepted types, or `every file · ctrl-a accepted only` — a click on which does
  the same as the key; a rule; then `◂ ..`, the folders as `▸ name/` in `link`, and the files, each `☐`, or `☑` once
  chosen, with its kind, size and date muted against the right. The selection bar `▌` sits against the box column,
  so it has something beside it on every row, and the cursor's row is on the band, its name in its usual role; with
  the preview pane open the band stops at the list's `│` (`Line.BandsTo`) rather than running on under the pane. Hidden files and folders — a leading dot, or marked hidden — never show,
  and every file shown under `ctrl-a` that the instance does not accept is muted.
- **The filter is fuzzy, fzf's way** (`FuzzyName`): the letters typed must all be in a name, in order, anywhere; letters
  in a run, or starting a word or a number, score more and skipped letters cost a little, so `ss0229` puts
  `Screenshot 2024-02-29 at 9.31.03 AM.png` first and loose matches still show lower down. `..` stays on top, folders
  ahead of files, and the cursor lands on the closest match. Fuzzy so that `space` can always mean choose: a name's
  spaces never need typing. `backspace` and `delete` only ever edit the filter — held down, they stop at empty rather
  than walking up the folders — and `esc` clears it before it goes back. `\` is a letter here, as on compose's
  fields, rather than the hidden rail's key, since a Windows path is typed with it; hidden, the locked walk says only
  `Rail hidden` and the status row has no `Rail: \` (#447).
- **Choosing**: `space`, a click on a box, or a ctrl- or shift-click on a row chooses a file, or lets it go, up to what
  the post still has room for; choices outlast a change of filter or of folder, and the cursor stays where it is.
  `space` is the keymap's (`Verb.Choose`), asked by the window ahead of the filter's letters. `⏎` attaches what is
  chosen, in the order chosen, wherever the cursor is — or, with nothing chosen, the file under the cursor — as pending
  attachments, exactly as a drop does (#375), and the browser goes. With neither, on a folder, `⏎` opens it, as `→`
  does; `←` goes up one. A folder opens on its first entry; going up
  lands on the folder just left.
- **Everything above the rule stays pinned**: the folder, the filter and the rule are the top three rows of the panel
  however far a long folder's list scrolls, and the list scrolls under them (`Screen.Pinned`, which the content region
  reads). The cursor is never hidden under them: walking with `↑`/`↓` scrolls so its row is in the room below the rule,
  and one the wheel has scrolled under them is off the page, so the next arrow takes the first row showing below the
  rule, as it would a row wheeled off the top.
- **The mouse**: a click moves the cursor, a double click opens a folder or attaches a file — on a box it is a second
  click on the box — and the wheel scrolls the list and nothing else, wherever the pointer is over the panel: the
  list, the pinned rows or the preview, sideways notches dropped and a trackpad's bursts a row each. It never moves the
  cursor, chooses, attaches or leaves, whatever the terminal says the pointer did between notches (`MouseButtons`, under
  **Pointer**) — the arrows walk the list (`Verb.NextEntry`, `Verb.PreviousEntry`), as `j`/`k` walk posts elsewhere, `j` and
  `k` being letters here. A click lands on the row drawn where it points: on a pinned row it is that row's — `ctrl-a
  every file` shows every file, the folder row picks nothing — never a list row scrolled under it.
- **`esc` with no filter goes back to the draft unchanged**: nothing attached, nothing asked.
- **On a terminal 90 wide or more the list keeps to the left** and a pane opens on the right past a `│`, where the
  picture under the cursor goes (#382): top left, at its own proportions as wide as the pane allows, level with the
  list's first row on the page — the first under the rule — its name and size muted under it. Moving the cursor, or
  filtering it onto another file, changes it. "On the page" because the list scrolls with the content panel: the
  browser keeps to the page (`Screen.KeepsToThePage`), is told where it is (`Drawing.Top`, so the level is
  `Drawing.Top` plus the three pinned rows), and is laid out again on a frame whose scroll moved, so the picture is on
  the page with the cursor however far down a long folder it is. The pane runs to the foot of
  the page however short the list, so a picture changes no row's height. Nothing is in it for a folder, a file that is
  not a picture the decoder reads, or on a terminal that cannot draw.

### What mentioning somebody settled

Typing `@` at the start of a word in the post opens a list of people to mention under it (#318, #313), and so does
typing one in the edit public profile form's bio (#408):

- **The people come from what is already on screen, so suggesting one costs no request.** The shell keeps a store per
  profile for the session (`PeopleToMention`), and every screen says who it shows (`Screen.Seen`) as it arrives, is
  drilled into, is refreshed or fills: post authors, boosters, everyone a post mentions, the post being answered,
  conversations, notifications, account screens, follow lists, requests, Discover and search. One person per address,
  the profile's own account left out, nothing written to disk, and a profile switch starts it again.
- **The profile's follows join them, read once the first `@` is typed (#321).** One background read of the profile's
  own following list, nobody named, capped at 400 — five pages of 80 — so that somebody following thousands never
  spends a large share of the rate limit on autocomplete; composing without an `@` costs nothing. Each page is offered
  as it lands, and an open list redraws with it. The read is not asked through the enquiry: one the rate limit stopped
  keeps what it got, one refused keeps what was known, both silently, and neither is asked again that session. A follow
  made through the shell adds the person at once, and an unfollow drops them from the follows — outlasting a page read
  before it.
- **Where the follows read are not all of them, the instance searches them (#322).** Not all of them means capped at
  400, still arriving, or stopped by a refusal. Then a query fewer than five known people answer to is sent to
  `GET /api/v1/accounts/search` with `following=true`, unresolved, five at most (`IInstanceSearch.FindFollowed`) — but
  only once asking has paused for the shell's settle (250ms, `ShellTiming.Settle`), once per pause, and whether it is
  still worth it is asked again when the pause comes, since the follows may have finished meanwhile. A list asking
  again for the query it already asked for, as a redraw or a caret move does, is the same pause. What it finds joins
  the follows, one per address, and an open list redraws; each query is searched once a session. Like the read, it is
  not asked through the enquiry, and a refusal is silent.
- **An @-word** starts with `@` at the start of a line or after whitespace and runs over letters, digits, `_`, `.`,
  `-` and `@` up to the caret, read from the text as written — off the editor's unwrapped caret — rather than as
  wrapped. So `name@example.com` opens nothing, and a full address is one word. A word that is already a whole address
  somebody in the store answers to closes the list.
- **Matching** is in three tiers — a handle starting with the query, then any word of the display name starting with
  it, then a handle containing it — and within a tier the people seen first, most recently seen first, a screen's top
  row counting as the one met last, then the follows by name. Case is ignored, five at most, and custom-emoji
  shortcodes come out of names before matching and drawing.
- **The list is its own painted view over the editor**, every cell a role: a rounded box in `panel-border`, the picked
  row marked `▌` in `selection` with its name in `reference-picked` and its handle in `byline-handle`, other rows in
  `body` and `muted`, the matched letters in `mention`, and its keys along the bottom edge. At least 40 columns, never
  wider than the editor; on the row under the caret, left on the word's `@`, or over the line where there is no room
  below — read from where the editor put its caret, which is the only place word wrap is settled. No taller than the
  more room there is under the caret's row or over it; where that is fewer rows than people, it shows as many as fit
  and scrolls to keep the pick on them.
- **It is one list any compose field can offer rows to (#335).** Being a list — the pick, the scroll, the box, the keys
  and the pointer — is `PickList<T>` and `PickLines`, once; what a row says, where the list hangs and what picking does
  are the field's. The people to mention are the first such list, and the language list (#340) the second.
- **Its keys come ahead of the editor's, only while it is open.** `tab`/`⏎` replace the word with `@user` for somebody
  on the profile's own instance or `@user@instance` for anybody else, and a space. The word is selected and replaced
  as one edit, the way a paste goes in, so one undo puts the word back. `esc` closes the list for the rest of that
  word. Closed, it takes nothing: `⏎` adds a line and `esc` throws the draft away, as they always have. Never in the
  warning.
- **The bio's list is the post's (#408).** `MentionList` is told by the view that lays it beside its editor when it may
  open — compose while the typing is in the post, the edit public profile form while it is in front — and that view
  plugs it into its editor's `Ahead`. The people, the matching, the keys and the pointer are all the same; `esc` on it
  closes the list, never the form, and with it closed `esc` leaves the form as it always does.
- **It takes the pointer too, only while it is open (#335).** A click on a person is `tab` on them; a notch of the wheel
  over it moves the pick a person at a time, stopping at either end, and scrolls; a right click on it is nothing (#307).
  A click anywhere outside it closes it as `esc` does, and is spent on that, from the press to the click: the caret
  stays where it was and nothing under the pointer is clicked. So it is asked about ahead of every view, where the
  terminal's mouse events arrive.

### What compose's Lang settled

- **Lang is a one-line field under To (#340)**, laid over its value column as the warning field is over the warning's.
  It shows a language as its code and its own name, `fr  Français`, the name muted on the painted row — or its English
  name, `hi  Hindi`, where its own name is written in a script whose letters join, reorder or stack (47 of 214): some
  terminal draws such a name off its own cells and pushes the list's border out of place beside it (#432), and
  Terminal.app does it to words no rule about the text predicts, so only the scripts never shaped — Latin, Greek,
  Cyrillic, Armenian, Georgian, Han, kana, Hangul — keep their own names (ADR-0031). Typing either name still finds it.
  Empty, it says `none · the instance decides` and sends no language.
- **It starts on the author's own language**: `default_language`, else the account's posting language as its instance
  said it (#339), else empty — on a reply as on a fresh post, never the answered post's. An edit opens on the post's own
  and always sends what the field holds, as the warning does (#140); cleared, that is an empty language, which goes
  out as no language at all. Mastodon cannot remove a post's language — an edit with none keeps the one it has — so
  clearing Lang on an edit leaves the post's language alone (ADR-0024's amendment).
- **The list of languages is the second `PickList`**, hung under the field and no lower than the panel's foot. Typing
  narrows it through `PostLanguageName.Matching`: a code typed in full first, then codes it starts, then names in
  English or their own that hold it. A click on Lang or `⏎` in it opens every language, picked on the one held. A
  pick writes the language into the field and leaves the typing there; the list's bottom edge says `tab choose`.
- **What is not a language is refused at send**, with `PostLanguageName.Rejection` as the notice, and nothing goes
  out — half a name is what the field holds on the way to the whole of one, so it is not refused while typed.
- **On a short terminal it gives way with From**, being lower, just before it.

### What the account screen settled

The account screen was a scoreboard — a name, a handle, three counts, then posts. It says who somebody is now, and in
doing so it became the first screen in the shell whose pick is not a post (#164, #172):

- **The header block is the screen's first walkable thing.** `j`/`k` land on it, `←`/`→` walk the hashtags and
  addresses in the **Bio** and in a **Custom field**'s value, `⏎` opens one, and the keys that act on a post go quiet
  while it is picked — the rule above, whose precedent is a follow notification: it carries no post and leaves those
  keys with nothing to act on rather than guessing. **The pick opens on the header**, not on the first post: the
  screen is about the person, landing below them would make the bio something you walk back to, and it puts a verified
  link one `→` away on arrival.
- **The order is who they are, then what they wrote, then what they are to you.** Avatar, name, handle, the presence
  line, joined and flags — which are the **Account block** every listing draws, not a shape of this screen's own
  (*What one account row settled*) — then bio and fields; then standing, familiar followers and your own note, closest
  to the posts. **Every section brings its own separator**, so an account with none of the middle or bottom collapses
  to four rows and a divider with no gaps left behind.
- **It draws whole and cuts nothing.** Worst case is around 29 rows — a long bio wraps to nine, four fields to eight —
  and that is fine, because the screen scrolls and one `k` puts the posts on screen. A truncated bio would be permanent
  damage done to solve a problem one keypress already solves.
- **An avatar is an 8×4 inset that takes its columns back** where the terminal cannot paint one, rather than holding a
  placeholder open — what #62 already settled on a byline.
- **A custom field is one row, wrapping, its continuation indented two columns**: `Label: value ✓`, label in
  `Role.Muted`, value in `Role.Body` or in `Role.Link` where it is an address, which is then walkable. A wrapped
  address stays one reference across both rows, which `TextWrap`'s row offsets already handle for post text. The `✓` is
  `Role.Muted` and there is **no verified role** — Mastodon only verifies links, so the mark always lands on something
  already coloured, and an unverified field says nothing extra.
- **The flags read `⚙ bot` and `⚿ locked`, and there is no emoji anywhere.** The word carries and the glyph decorates,
  so a terminal drawing either as a box loses nothing. 🤖 and 🔒 are astral and their column width is
  terminal-dependent — Terminal.Gui, the terminal and this client need not agree on it, which is the disagreement the
  chrome should not be built on. A clip landing mid-pair is no longer one of the reasons: since #207 the shell measures
  in columns and cuts between graphemes. Every glyph the TUI uses of its own is BMP and one column — `○ ◌ ● ✉ ⚠ ▒ ⏵ ★ ↺
  ▌ ▷ ▶ ‹ ›` — and somebody else's words are measured rather than assumed.
- **Your own note about them is drawn whole**, `Your note: <text>` in `Role.Muted`, wrapped rather than clipped: it is
  the reader's own writing, and cutting it would be cutting their words. It costs no call — it has been arriving on
  every relationship payload and being dropped (ADR-0012's amendment).
- **Familiar followers is one row, two handles and a count** — `Followed by @jon, @sam and 3 others`, `Role.Muted`,
  clipped at 61. Handles rather than display names, because a display name is not unique and this row is making an
  identity claim. It is asked **last** of the four calls the arrival makes, so a rate limit costs only this row; the
  list is nullable, and null draws nothing while empty means nobody in common.
- **Pinned posts are a run of their own, above the timeline**: `── {n} pinned ──`, then `── their posts ──`
  uncounted. The pinned run is complete and unpaged so its total is a fact; the timeline run is a page of an unbounded
  list, and counting it would be a number about the fetch pretending to be a number about the account. One `PostList`
  with the headings spliced, the way the post screen already splices `[...ancestors, post, ...replies]`.
- **The duplicate is dropped from the timeline run, never from pinned**, and dropped by id in
  the `Account` subject's read, so the screen is handed two disjoint lists and cannot disagree with itself about which run a post is
  in. On their posts alone only a recent pinned *normal* post can be in both, that call excluding replies; with
  replies in, a recent pinned reply can be too, and is drawn in the pinned run alone the same way. Reading on keeps it
  so: an older pin coming back in a page read on is left out of the timeline run as it is added (#464).
- **Empty draws nothing; not asked draws a row.** No pinned posts is no heading, no rows and no gap, which is the
  common case. A pinned read a rate limit stopped draws `Pinned posts not asked for.`, the same distinction
  `AccountLines.Standing` already draws `Standing not asked for.` for — which is why pinned is asked *before* familiar
  followers: it is content, and the other is one decorative row.
- **`p` moves nothing.** Un-pinning inside the run leaves the post where it is and takes its `   pinned` word off;
  pinning one down in the timeline leaves it there and gives it the word; the heading goes on saying what the fetch
  found until `g`. A post vanishing upward while the reader is looking at it is worse than a stale count. The row keeps
  saying `pinned` inside the pinned run rather than being suppressed as redundant against the heading: the heading says
  what the fetch found and the row says what is true now, it is the only signal that an un-pin took, and on every
  account but the reader's own the word is never drawn at all — Mastodon sends `Status.pinned` only for your own posts.
- **`s` swaps their posts for their posts and replies, in place** (#229), and a second `s` swaps back. It is the follow
  list's `s` made over one account's timeline: the whole screen re-read with `Timeline.WithReplies` in place of
  `Timeline.By`, standing where the screen stood — new crumb, pick back on the header block, stack no deeper. The
  crumb reads `@maria posts and replies`, the heading `── their posts and replies ──`, and the status row says which
  run the key swaps to. **The screen still opens on posts alone**, for ADR-0019's screen-reader reasons: only the
  reader's own press widens it, and nothing remembers the widened choice on the next visit. `g` re-asks whichever run
  is showing. A verb of its own, `SwapPostsAndReplies`, rather than `SwapSide` again — a timeline has no sides — and bound on
  the account screen and the follow list only, `s` meaning nothing anywhere else.

### What a follow list settled

`w` opens everyone an account follows; `s` swaps to everyone who follows them, in place. One screen, either side,
anybody's account — `IAccountRelationships.List` already takes a `FollowSide` and an optional account (#165):

- **One key rather than two, and it swaps rather than pushes.** The account screen is crowded — it inherits the post
  keys, so `f` is favorite and `b` is boost, and its capitals are spoken for by the three ties. `s` re-asks in place
  with a new crumb, the pick reset and the filter cleared, because a toggle that pushed would grow the stack on every
  flip. Someone else's followers therefore costs `w` then `s`, which is the less-asked side from someone else's
  profile. The crumb reads `@maria following` / `@maria followers`.
- **The mode is picked from the count before the first fetch.** An `Account` already carries both counts, so the screen
  knows how large the list is before asking for anything rather than discovering the problem hundreds of calls in.
  Under the threshold it holds the whole list, walkable from the first page of 80 with the rest landing behind a pick
  that does not move; while what is on screen is not the whole of it — still coming, or stopped short by a rate limit
  — the count says both numbers, so a thin filter result reads as *still reading* rather than as *nobody matches*. At
  or above it the screen is browsed by **reading on** (#467, *What reading on settled*) **with no filter at all**, `f`
  off the status row and the count reading `240 of 877,000 read` (`Number.Of`, which is how this client writes every
  count). It reads the first 80, and as the page comes within a screenful of the end — by `k`, `End` or whatever moved
  the page — the next 80 from where the last read stopped (`IAccountRelationships.ListOn`), put under the last person
  held with the pick unmoved. Nothing is read twice: each read on is one page, however far down the reader has got,
  where the paging it replaced re-read every page before it on every ask. The foot says "reading on…" while a read is
  out and "that's everything" once the instance has nothing behind what is held.
  The rest of a held list arrives in **one further ask rather than 24**, which is where the implementation departs from
  #180's "80 a page": asking page by page with `IAccountRelationships.List`, which reads to a *limit* from the top,
  re-reads every page before it — 25 asks would cost some 300 requests against the 25 the ticket budgeted. A held list
  is unchanged by reading on: it is still read whole at once, re-read for the rest to the list's own count, and draws
  no foot. The standing call is still made a page of 80 ids at a time, that endpoint taking many ids and not unboundedly
  many — and on a browsed list only for the people a read on brought, never again for those already on screen.
- **No filter is offered where it cannot be honest.** Filtering what has already been read is the dishonest filter
  #162 ruled out, and 240 read of 877K makes "no matches" meaningless. Refusing to open the screen was rejected too:
  walking the first few hundred of anyone's followers is a real thing to do, on the same rows with the same `⏎`.
- **The account screen is not marked to say which count leads to a filterable list.** The number is already on screen
  and the browser's status row says which mode it is in; marking it would mean explaining a threshold to a reader
  instead of telling them what they have once they arrive.
- **A row is the shared Account block**, drawn the way every listing draws a person (*What one account row settled*),
  with the compact standing on its fourth row. Neither `:id/following` nor `:id/followers` sends a standing, so it is
  filled by **one batched `/accounts/relationships` call per page**, not per row; where that call did not happen or
  failed the row stays **silent** rather than implying no tie.
  That call is **the one Core change this cost**: #165 said there would be none, having priced the listing alone, and
  no port reached that endpoint for more than one account (`Show` asks about the one it read). So
  `IAccountRelationships.Standing` was added rather than reached past, which is the rule `ShellPorts` already states —
  it answers with the accounts it was given carrying their standing, and with nothing where the instance refused,
  which is the shape `FamiliarFollowers` already set for a call that decorates rows worth drawing without it (#180).
- **A row never says what the list it is on already says.** Your own following list drops "you follow them", true of
  every row by construction, and shows "they follow you". Your own followers list drops "they follow you" — the *gap*
  is the signal there, since what you want is who you have not followed back. Someone else's list implies nothing, so
  the full compact standing shows.
- **The screen arrives walkable, not typing.** The search screen opens typing only because it has nothing to show yet.
  `f` opens the prompt, drawn with the search screen's exact construction — `Filter: ` label, typed text, `▌` caret.
  Typing narrows on every keystroke, `⏎` returns to walking with the filter still applied, `esc` clears it. `/` was
  never available: it is a frame key meaning "go to search" everywhere. It reuses `IsTyping`, so the status row swaps
  to the letters-are-text keymap and the filter is a fact about the shell a test can set and read (ADR-0015).
- **What it has to say about the list goes on the screen, not the status row.** The row holds either a notice or the
  keymap and never both, and a list with nobody on it has nothing to walk — so a notice said there would stand for as
  long as the screen did, and every key the screen answers to would be hidden behind it. It is drawn as the first
  muted row instead, which is what every other list this shell draws already does. Nothing is counted over a list
  nobody has arrived on either: `0 of 5 read` is a subtraction a reader has to do, and the notice above it has already
  said the whole of what it would have told them (#180).
- **Both numbers on their own read as *still reading*, so a read that has stopped says why.** An instance can only
  serve the part of a *remote* account's follows it actually holds, which is mostly its own people — so `@lizardbill`'s
  951 followers arrive as the 16 who are local here, the count coming off their own instance and the list coming off
  yours. Nothing is wrong and nothing more is coming, and `16 of 951 read` alone leaves a reader waiting for the other
  935. A held list's row says `16 of 951 read — this instance offers no more` once the reading has finished short. Not
  where a notice above has already said why: a rate limit stops a read too, and a count blaming the instance's reach
  underneath that would contradict it and guess at a cause the notice already knows. A browsed list's row keeps the
  two numbers and drops the suffix (#467): its foot says "that's everything" once the instance has nothing behind what
  is held, which is the same fact said the way every list that reads on says it.
- **An account that lists nobody is not an account that follows nobody.** Mastodon serves an empty list for an account
  that keeps who it follows to itself, while the count on the profile goes on saying five — so the screen says *their
  profile says 5 following, but the instance listed nobody* rather than *they follow nobody yet*, which would report a
  setting as a fact. Both numbers, and no cause guessed at: the same honesty an absent standing gets.
- **No per-row rule.** The search screen rules between posts, because a run of them is a feed; there is one kind here
  and nothing to separate, and at 900 people it would be 1,800 rows, half of them horizontal lines. One rule under the
  prompt keeps the screen search-shaped.
- **It is cached**: age only, one minute, keyed by its subject — the account's id and the side. The shell's first
  drill-in cache, and since #233 in the same cache the rail's destinations are held in. `esc` back is
  already free — the stack hands back the very screen with its page intact (#133) — so the cache only pays on a re-open
  after popping.

### What a post's who settled

`w` on a picked post opens who favorited it or who boosted it, on the actors screen Notifications uses; `s` swaps to
the other side in place (#487, #489). `IPostEngagement.ReadActors` reads one side of a post's **Actors** from
`/statuses/:id/favourited_by` or `/reblogged_by`, paged by the instance's link and read on by `ReadActorsOn` (#488):

- **One side at a time, never merged.** The instance keeps the two lists apart and so does the screen, for the reason
  ADR-0032 gives against grouping on the client. The crumb says which: `Favorited by` or `Boosted by`, the same words a
  favorite or boost notification group's actors are crumbed by, from the one place (`ActorSideWords`).
- **The opening side is the post's counts', settled before anything is read.** Favorites above zero open
  `Favorited by`; otherwise boosts above zero open `Boosted by`; otherwise the status row says `Nobody has favorited
  or boosted this.` and nothing is read or pushed. There is no shortcut to one account where a count is one: the list
  is what says whether they favorited or boosted, and a count can be stale.
- **A list shorter than its count says why** (#491). An instance lists only the favorites and boosts it knows of, and
  Mastodon delivers each to the post's home instance alone, so a remote post's list holds only the reader's own
  instance's people: one person under 140 favorites, which "that's everything" alone reads as a bug. A side read to its
  end (not stopped by a rate limit) holding fewer than the post's count says so above the list, wrapped rather than
  clipped: `mastodon.online knows of 1 of 140 favorites. The rest are on mastodon.social.` for a remote post, `1 of 3
  boosts can be listed.` for a local one (accounts deleted, suspended or blocked since). The count is the one the post
  carried when `w` was pressed. A side that reads back empty says the same — `…knows of none of 5 favorites…` — and
  `Nobody has favorited this.` / `Nobody has boosted this.` is left for a side counted at zero, reached by `s`. The
  CLI's `post favorited-by` / `boosted-by` say the same line under the list, on stdout like an empty list's line —
  nothing failed — and `--json` carries the count as `total`. The sentence is `ActorShortfall`'s, in Core, so the two
  front ends give the same reason in the same words. Reading the home instance directly was set aside: a tokenless
  call to a server the reader never signed in to, with its own ids, rate limits and a standing lookup per person.
- **`o` opens the post on its home instance**, where the whole list is: its `Url`, in the platform's browser through
  **Reach**'s `Browse`, refused and said as `⏎` on a link is. Offered and answered on a remote post's actors only — a
  local post's list has nowhere else to be — and the status row says where: `o:on mastodon.social`.
- **On a boost it is the post boosted**, as every post verb acts on, and it is the same verb on every screen a post can
  be picked on — a feed, a thread, the post screen, a search, a conversation — bar Notifications, where `w` stays who did
  what the row reports (#451). On an account screen it goes by the pick: their follows on the header block, as before,
  and the post's who on one of their posts (#490). `w:who` is on the status row while a post is picked, in the tail with
  `x` and the author's three, and the account screen's `w:follows` only while its header block is.
- **The actors screen is generalised, not copied.** It lists a notification group's actors or one side of a post's,
  with the same rows (the compact Account block with **Standing**, asked once a page), `⏎`, `g` and reading on; what
  differs is the crumb, the empty text, why a list is short, and `s` and `o`, which only a post's actors answer — each
  said once by `ActorsOf`, the closed pair of a group and a post's side the screen holds. Both kinds' first read is
  one shape in `Subject`, differing only in the port it asks. `s` swaps as a Follow list's does — the other side stood
  in this one's place, so flipping never grows the stack and `esc` returns to the post.
- **Never held.** A post's actors **Subject** is compared by the post's id and side and never cached, so the reader's
  own favorite or boost is on the list the next time it is opened. An open list is not changed live by a mark.
- **No click opens it.** A post's counts stay inert, kept for the marks themselves, as on the web.

### What moving between kinds and sections settled

A search's results were always labelled — `── accounts ──`, `── hashtags ──`, `── posts ──` have been drawn over the
first result of each kind since the four destinations landed — and what was missing was a way to move between them.
The answer generalised: three screens use it (#166, amended by #171 and #172):

- **`]` is the next run, `[` the one before.** Unshifted, which is what a key pressed to get somewhere should be and
  what the `j`/`k` beside it already are; the vim bracket family, which is move-by-structure and nothing else, so the
  convention needs no analogy to carry it; and symmetric and directional on sight. `{`/`}` was this decision's first
  answer, taken only because `[`/`]` were being held against a possible shell-wide swap of `j`/`k`; that reservation
  was released, **the swap is off rather than deferred**, and `{`/`}` stay free as the fallback if these ever have to
  move. `K`/`J` lost on making capitals mean movement, where they are the deliberate-act register (`F`, `M`, `B`, `D`)
  capitalised precisely so a slip cannot fire one. `n`/`N` lost on reading as "next match" — a within-results search
  that does not exist, and sitting beside `/` it would advertise one.
- **It moves the pick, not the page**, and brings the heading with it under the rule in "What moving settled" above.
  It is "take me to the posts", and a reader who arrives at a post wants to press `⏎`, not `k` first.
- **Headed runs only, and the account screen's header block is not a stop.** The jump brings a run's heading with it,
  and the header has no heading to bring; `[` from the first pinned post clamps. Nothing is unreachable — `k` from
  there lands on the header.
- **A heading carries its count where the run's total is a fact** — `── 7 accounts ──`, `── 2 pinned ──`,
  `── 11 followed by people you follow ──`, the `── {n} replies ──` vocabulary the post screen already speaks. It costs
  nothing, the counts being in hand, and it tells a reader whether the run below is worth walking or worth jumping. A
  run that is a page of an unbounded list is left uncounted, which is why `── their posts ──` stays as it is.
- **A run with nothing in it draws no heading at all.** The post screen's zero-fallback (`── replies ──`) does not
  apply: three "nothing" headings on a two-result search is noise, and per-kind absence is the ordinary case for almost
  every query. The screen already says `Nothing found for {asked}.` where all three are empty, which is where "was it
  even asked?" actually bites. This is deliberately unlike the CLI, where `--type` makes absence meaningful and
  ADR-0011's null-versus-empty earns its keep — the TUI always asks for everything.
- **What separates two results is what the kind is, and one kind is parted from the next by the blank over its
  heading.** Posts keep the feed's rule between them; accounts take the blank every screen listing people takes
  (#180, #198); hashtags take nothing at all, being one row apiece under a heading that has already counted them,
  where a separator per row would spend as many rows on the gaps as on the tags. Between rather than after, so no
  separator is left hanging under the last result — and the first heading takes no blank of its own, having the
  prompt's.
- **A run of hashtags is a table, and its columns are the run's.** The counts stand off the longest name in the run,
  and each number is right-aligned in its own column, so a reader glances down one column rather than reading twenty
  rows: `81` under `14` compares at sight, `81` under `1` does not. Measured once per run off what is drawn, `#` and
  thousands separators included — a row that measured itself could only align with itself.
- **Aligning is all or nothing, and the run decides, not the row.** On a terminal too narrow to hold the longest name
  and the counts together the run gives its columns up altogether and every row falls back to a two-space gap and
  unpadded counts — the row this screen drew before it had columns. Narrowing them instead would be worse than
  ragged: padding is spent from the left and the row is clipped from the right, so columns held past the width cost
  the accounts count that would otherwise have fitted. One row giving up its columns while its neighbours kept theirs
  would be the raggedness the columns exist to prevent.
- **The status row says `[/]:section`, shell-wide**, and only where the screen has two or more headed runs *now*. One
  key that means one thing everywhere is named one way everywhere, which is why this is `section` rather than the
  `kind` the search screen alone would have said. A key announced where it does nothing reads as a shell that missed
  the press, which is how the poll digits and `⏎`-inside-a-post already behave. It goes first in `PostKeys.Around`'s
  `its` list, immediately after `j/k:post`, so the movement keys sit together and `F`/`M`/`B` stay an unbroken group —
  and `Around` already puts a screen's own keys ahead of the shared ones, so the nine columns come out of
  `tab:destination` and `?:keys`, both learnable on any other screen.
- **One list, still.** `Picked<Result>` is untouched: the jump is an index into the list that already exists, with no
  per-kind list, no second count and no fold state, so the order results are drawn in and the order they are picked out
  in stay the same order by construction. The tabbed header row and the collapsing sections both lost on exactly this,
  and the tabbed shape would also have handed back what ADR-0011 bought the reader — seeing at a glance that the word
  turned out to be a hashtag.
- **The TUI never asks for one kind.** `SearchKind` and `SearchResults.Matching` exist and the CLI's `--type` uses
  them; the TUI sends `Everything` and always will. Narrowing the *ask* is a different answer from moving within the
  *answer*, and a reader who knew the kind would not be searching a half-remembered word. A kind syntax would also have
  to share a prompt that must already accept `#tags`, `@handles` and web addresses.

### What discover settled

The ninth of the rail's fixed destinations, and the only one it has ever grown by (ADR-0014's amendment). Its one section today is who
to follow (#171):

- **It is a destination and not a key on the search screen.** What earns the entry is that the screen is sectioned, so
  a second kind of suggestion later is a heading rather than an eleventh rail entry — the cost is paid once now instead
  of once per kind. **There is no second door**: no `S` on the search screen, because Discover is an **Arrival** and
  the stack resets to one screen, and a place that was both a destination and a pushed screen is what GLOSSARY.md's
  **Destination** term exists to prevent.
- **The reason is a heading, and every reason gets one** — `── 11 followed by people you follow ──`,
  `── 6 like people you followed lately ──`, `── 4 featured by this instance ──`, `── 9 talked with most here ──`,
  `── 10 most followed here ──`, in that fixed rank. On a small instance the good two are often empty, and a heading is
  what makes a weak reason *readable as weak* rather than an unexplained row; the reader discounts "most followed here"
  themselves. **The server's order inside a section is kept** — it ranked them, and re-sorting would be Wooly asserting
  a judgement it has no data for. The longest heading is 38 columns, so 61 is never in question.
- **A person is drawn once, under their best reason.** `sources` is an array per suggestion, and the same face twice
  would carry a second `F` that does nothing.
- **A row is the shared Account block** (*What one account row settled*) and still **costs no relationships call** —
  a deliberate departure from a **Follow list**'s recipe. Mastodon's suggestion sources exclude accounts already
  followed, dismissed or blocked, so a standing word would be blank on every row by construction; the screen is one
  call, not two. The heading says why and the row says who.
- **`F` toggles and `d` dismisses, and nothing moves.** `F` appends a muted ` · following` and takes it off again, the
  same contract it has on the account screen, so a mis-press is undone where it happened. `d` appends ` · dismissed`
  and is one-way — there is no un-dismiss endpoint, so a second `d` is a no-op — and takes **no confirmation**, nothing
  of the reader's being destroyed and the cost of a mis-press being one suggestion out of forty on a list the server
  regenerates. A dismissed row **stays walkable** and still answers `⏎` and `F`: dismissing says "stop suggesting", not
  "hide", and a row that vanished would take its own undo with it. No row reorders and no heading recounts as you act —
  the feature was admitted for `k k k F F`, and a list that reflows under your fingers is the one thing that breaks it.
- **Empty is a real answer, so there is no not-asked to draw.** A destination always asks on arrival, so an empty
  screen means the instance answered "nobody": one muted `Nobody suggested.`, on the same construction as search's
  `Nothing found for …`, and no apology for a new account or a small instance. A failure is not an empty screen either
  — the **Enquiry** turns it into the shell's notice.
- **Forty of forty, and no paging.** `limit=40`, no `offset`: a screen showing all of what it asked for has nothing to
  page. It is cached for free, and **a tie or a dismiss made here forgets Discover's entry**, beside the Home a tie
  already forgets — both now rows of the one table a **Change** is settled by (`Arrival.Apply`, #234) — without it,
  following somebody and
  coming back inside the minute shows them still suggested, which is worse than the follow browser's equivalent because
  the server *would* have dropped them.
- **Its status row puts the acting keys first**: `j/k person · ⏎ open · F follow · d dismiss · [/] section · g refresh ·
  tab destination`. That is the opposite of the search screen's placement, and deliberately: there, jumping between
  kinds is the feature that screen gained; here, `F` and `d` are why anyone opened the screen.

### What one account row settled

Four screens listed accounts and each drew one its own way — a byline on search, a byline and a presence line on
follow requests, a byline and a standing suffix on a follow list, a byline and a suffix of its own on Discover — while
a fifth, the account screen's header block, drew the person properly. Four shapes for a person are four ideas of the
same person, so there is now one (#198):

- **The Account block is the header block's own top four rows**, extracted rather than copied so both call one method
  (`AccountLines.Block`): the display name, the handle, the presence counts, then `Joined Jul 2023` with the
  `⚙ bot` / `⚿ locked` flags and the screen's own word after it, `·`-joined and muted, beside the **same 8×4 avatar**
  the account screen spends. The header block is then *the block plus* the bio, the custom fields and the standing. If
  the two drifted by even a flag, a locked account would read as locked on its own screen and not on the list you
  found it from, which is the whole reason this was worth doing.
- **The standing word moved to row 4.** It was a muted suffix on the byline. Row 4 is already a `·`-joined list of
  small facts about the person, which is what `following · follows you` is, and it leaves row 1 as the name alone.
- **No per-screen variant, and no third avatar size.** `Avatar`'s two named sizes are still the whole of what may be
  asked for — a size named at a call site is a size that can drift from the one beside it — and a listing screen's
  only say is what it adds to row 4.
- **The accounts run is taller on a mixed search page, and that is accepted.** `[`/`]` already jump between kind-runs
  and the heading says how many accounts there are, so a reader who wants the posts has one keypress — the same
  argument ADR-0019 used to refuse truncating a bio.
- **One blank row stands between blocks, and no new rules.** Follow requests, follows and Discover had no separator
  between people, and four-row blocks laid end to end run together; a blank costs the one row a rule would while
  leaving the list looking like the list of people it is. Search draws the same blank between its accounts, so a run of
  people reads as a run of people on every screen that lists them.
- **The block is one pick and there is nothing to walk inside it.** Unlike the header block it carries no bio and no
  custom fields, so it holds no references: `←`/`→` stay unconsumed on all four screens and `⏎` does what it did.
  The `▌` gutter sits to the left of the avatar and runs down all four rows, which is what a post's byline settled.
- **The block itself costs no call and no Core change.** Both follow-list endpoints, the search endpoint, the
  pending-requests endpoint and the suggestions endpoint all answer with full account entities, so every field is
  already in hand. Only the **standing** is not sent, and who asks for one was settled separately (#204): follow
  requests and search's accounts run now each spend **one batched `/accounts/relationships` call per page**, the
  follow list already did, and Discover still makes none.
- **A follow notification is not on this list.** Its row says who did what and how long ago, which is an event rather
  than a person, and `NotificationsScreen.Happened` keeps drawing it.

### What asking for a standing settled

#198 gave four listing screens one row and put the standing word at the end of it; it deliberately did not change
which of them had one to put there. #204 did:

- **Every screen that lists accounts asks for a standing unless it has a reason not to.** A default with one named
  exception, rather than a list of four that the fifth listing screen forgets to join. There are two reasons not to
  and no others: **asking cannot help it** — Discover, whose suggestion sources exclude accounts already followed,
  dismissed or blocked, so the word would be blank on every row (ADR-0019) — and **there is nothing to decorate**, an
  empty run, which costs no call and no guard because `Standing` answers an empty ask with its own input.
- **Follow requests and search's accounts run now each carry the compact standing** on row 4, exactly as a follow list
  does, at **one batched `/accounts/relationships` call per page** apiece. On requests, whether you already follow the
  person asking to follow you is plausibly the most useful thing on the row; `following` is what it is read for.
- **Requests asks inside its destination's own read**, the lambda the arrival already runs — so refresh re-asks for
  free and no other destination has to opt out of anything. **Search asks before it paints**, two calls under the one
  enquiry, so the results go up with their standings already on them rather than being decorated a moment later.
- **A page of requests implies a *negative*, which is not a fourth `Implied` case.** "They follow you" is false by
  definition of a request still waiting, and the compact standing only ever adds words for positives — so
  `Implied.Nothing` already draws the right row, and what changed is its definition: **nothing to suppress**, which is
  honest for a search run, for somebody else's follows and for a page of requests alike.
- **The silence rule is untouched.** A refused or rate-limited standing leaves every row with no suffix at all, raises
  no notice and leaves the list on screen — `ShellPorts.StoodOrSilent` is the one place the `?? people` behind that is
  written, for all three call sites. Writing it down turned up a bug: `Standing` did not actually catch a refusal,
  its catch list having been copied from a call that reaches the endpoint by a different route (ADR-0012's third
  amendment). Fixed here, since this change would have spread it to two more screens.
- **The CLI is left alone.** `account requests list` and `search` have the same gap; adding a standing to their JSON
  is a machine-readable-output change to argue under ADR-0007 and is worth its own issue.

### What the breadcrumb settled

> **Changed by ADR-0021.** The trail is now the content panel's title rather than a row of its own. What it says and
> how it elides stand as below; the row, its `crumb` band and the blank row under it are gone, and `crumb` and
> `crumb-current` are retired: the crumb stood on is `panel-title` and the ones walked through are `muted` (#271). The
> fetch mark's columns are now held whether or not it is drawn, so a trail no longer elides differently mid-fetch.
>
> **Changed by #281.** The fetch mark is a star spinner straight after the trail, not the word `fetching` with dots
> at the end of the edge. The bullets below on the mark say what it is now; the dots are kept only where marked as
> superseded.

The trail said where you are and drew itself as one `Role.Chrome` span, so it said nothing of the sort — and
`FeedScreen.Crumb` lowercased the rail's own label, so the sidebar said `Home` and the breadcrumb said `home`. #168
and #213 settled the row; #216 and #217 build it:

- **Every crumb is sentence case** — `Home`, `Notifications`, `Direct messages`, `Follow requests`, `Search cats`,
  `Post by @maria`, `With maria and joe`, `Keys`, `Compose`, `Reply to @maria`, `Edit` — **except a crumb that opens
  with a handle or a hashtag**, which is spelled the way that thing is spelled: `@maria`, `@maria following`,
  `@maria followers`, `#cats`. Nobody's username is capitalised. One rule rather than two: the rail's ten labels are
  already sentence case, so a rail-label crumb matches the rail by construction and `.ToLowerInvariant()` is deleted
  rather than replaced by a rule about rail labels.
- **The crumb you are standing on is told from its ancestors by foreground alone**, in one new role,
  `crumb-current` — named to pair with `rail-current`, the same *and this is the one you are at* relationship. The
  ancestors take `crumb`, and **the `›` separator keeps no role of its own**: the trail reads as structure because
  its end brightens, not because its separators dim, and a second role is a second public name in everybody's
  `[themes.*]` table for something nobody would theme separately. A glyph before the current crumb, a band on the
  current crumb alone and dimming the ancestors were all drawn and rejected — in this shell a band means *the thing
  you are on* (`selection`, `rail-current`), so banding one crumb says that crumb is a selection.
- **The no-colour case is carried by position**: with no glyph, a mono terminal draws exactly the row it drew before,
  and the crumb you are standing on is always the last one by construction. Nothing improves there and nothing is
  missing; colour is what is added for the terminals that have it.
- **A long trail elides from the left** — `… › @maria@mastodon.social following › Keys`, the lead being `… › ` in
  `chrome`, four columns. Clipping from the right kept the crumbs saying where you came from and lost the one saying
  where you are. Dropping the middle was preferred first and dropped on evidence: `_stack[0]` is always the
  destination screen, which `RailLines.Entry` is already drawing in `rail-current` with a mark, 18 columns to the
  left — so it spends columns re-saying the rail. Eliding from the left elides only what the rail is still showing.
- **A blank row divides the breadcrumb from the content.** The breadcrumb keeps row 0, row 1 is blank, content starts
  at row 2. This is the separator vocabulary every screen already uses, and it works in mono where a band would not.
  The cost is one content row on every screen, forever, and it is worth spending because the breadcrumb blends into
  the content on every screen — where the second status row refused below would fix a problem only the busiest screen
  has. A horizontal rule was rejected: the only rules in the TUI are the rail's foot divider and Discover's section
  headings, which on Discover would be four rows apart from it.
- **And the breadcrumb row is banded, which this map first refused.** The blank row was too quiet on its own: with
  nothing else on screen carrying a background, the frame's top row read as the first line of what was being read.
  The refusal stands where it was aimed — a band is no way to tell the crumb you are standing on from its ancestors,
  which is `crumb-current`'s job and is done by foreground. Banding the *row* says something else, and something the
  row needs said: this is the frame. So `crumb` was added, the band goes under everything on the row — crumbs,
  separators, the `… › ` lead, the room left over and the fetch mark — and the seam under it went back to being
  blank. A band under half the row would read as a highlight on that half, which is the objection this keeps.
- **The fetch mark moves, and is laid out so nothing else does.** `· ✢ ✱ ✶ ✻ ✽ ✻ ✶ ✱ ✢`, Claude Code's star
  growing from a dot and shrinking back, a frame every 400ms and the first again after the last, in `spinner`, a space
  after the trail's last crumb (#281). One space rather than two, so the star sits evenly between the crumb and the
  edge's rule, which the edge parts from the title by one space too. Every frame is one column, so the mark is as wide
  on one tick as the next. Its **2 columns** — the space and the glyph — are held whether or not it is drawn, so the
  trail elides in the same room at rest as on every tick and never re-elides as a fetch starts or ends. At 80 columns
  that leaves the trail 54 columns.
  It sits **straight after the trail rather than at the far end of the edge**, beside the crumb you are standing on,
  which is what the fetch is about to replace. A trail long enough to elide fills most of its room, so there the
  spinner lands near the end of the edge anyway; a short trail is followed by it. A panel too narrow to hold the mark
  beside a column of trail never draws it, and holds no room for it.
  A braille spinner (`⠋ ⠙ ⠹ …`) was tried first and dropped: its dots sit in the top rows of the cell, so it floated
  above the crumb it follows. A star is drawn about the middle of the line, as a letter is.
  *Superseded by #281:* the mark was `fetching.` → `fetching..` → `fetching...`, owning the rightmost 11 columns of
  the edge and 3 more parting it from the trail, which left the trail 42 columns at 80. The word was dropped because a
  spinner frame is never drawn at rest, so its presence alone says *in flight*, and the 11 columns it gives back go to
  the trail.
- **The mark waits one tick before appearing at all**, so a fetch that lands in 80ms shows nothing and a cached
  destination never flashes one. The delay *is* one `MarkStep` and needs no second number. A four-state cycle with a
  beat of rest was rejected: it takes the mark off the edge for a beat of every cycle, which reads as *finished* on the
  one row whose job is to say *working*.
- **The mark animates on every terminal.** `NO_COLOR` and `TERM=dumb` are about colour, not motion, and holding still
  there was refused rather than overlooked — it would be the shell's first behavioural difference on a plain
  terminal. In mono the spinner is all the motion there is. The mark has a role of its own, `spinner`, so a theme can
  colour it apart from `loading`; both built-ins draw it in `rail-unread`'s colour. It is carried without colour by
  the spinner frame itself — moving, and drawn only while a fetch is in flight. There is no ASCII fallback:
  the shell already draws `›`, `…` and the box-drawing characters with none.
- **The spinner goes on turning while a rate limit is waited out.** The wait is inside the same enquiry, so the question
  really is still in flight: the mark ticks at 400ms on the breadcrumb while the status row counts down at 1000ms.
  Freezing it would make the one row that says *alive* say *stuck* at the moment the shell most needs to look alive.
- **`ChromeLines.Breadcrumb` takes a frame number rather than a flag.** Nought draws no mark, which is what makes
  the delay assertable with no terminal in the room. The frames and the 2 columns they hold stay in `ChromeLines`,
  beside the trail arithmetic they have to agree with; the view hands over a number it counted and nothing else.
- **The frame is labelled in lowercase; the shell's prose is sentence case with a full stop.** So `esc keep`,
  `j/k:post` and `…+10` are one register and `Already read.` and `Clear every notification? This cannot be undone.`
  are the other. A crumb is neither — it is the name of a place, spelled as above.
  *Changed by ADR-0021* for the status row's hints alone: they are labels, `Post: j/k`, capitalised as lazygit's
  strip is (#268). `esc keep` and `…+10` stay lowercase. *Changed by #281:* the breadcrumb's mark is a spinner and
  says no word, so it is in neither register.

### What the status row settled

> **Changed by ADR-0021.** A hint is drawn `Does: key`, the pairs divided by ` | `, in place of `key:does` divided by
> ` · `. The rank order, `…+N`, the pinned `?` and the rule that a key that cannot act is off the row all stand.

`PostKeys.Around` emitted around fourteen hints and `ChromeLines`' clip cut them at the right, so a reader on a busy
screen saw an ellipsis where the keys should be — and since `?:keys` is last in every list, the row announcing where
the cut keys could be found was the first thing cut. The lever is the standing preference: **the status row is a
reminder, `?` is the reference.** #169, #214 and #215 settled the row; #218, #219, #220 and #221 build it:

- **The rule, stated once so a screen added later inherits it**: the status row draws as many whole hints as the
  terminal has room for, in rank order, then `…+N` for the ones it could not fit, then `?:keys` — which is never cut.
  A key that cannot act on what is picked out right now is not on the row and not in the count.
- **The rank**, which is what `PostKeys.Around` assembles: the walk (`j/k:post`, `j/k:thread`, and `←/→:reference` once
  a reference is picked — before one is, `←/→` ranks right behind `⏎`, as "What references settled" says); the
  screen's own keys, including `g:refresh`; the way out (`esc:back`, or `tab:destination` and `` `:group `` at the
  bottom of the stack); the shared post keys worth reminding somebody of (`⏎` `r` `b` `f` `a` `c`); the tail, which
  is what can be learned anywhere or acts only on your own posts (`↓/↑` `x` `p` `e` `d`); and `?:keys`, pinned. The
  order used to be assembled for reading; promoting it into a ranking fixed two accidents — `↓/↑:row` no longer outranks the three
  marks, and the way out is no longer at the end, where `tab:destination` fell into the overflow on the feed.
- **The fill stops at the first hint that does not fit** rather than skipping it for a narrower one behind it.
  Skipping would fill a few more columns at the price of the row no longer being in rank order, and of the keys shown
  reshuffling as the terminal is resized. The worst case leaves 8 columns unused; that is what the row reading as a
  ranking costs.
- **`?` is pinned; the way out is kept by rank instead.** One absolute, not two: the whole rule leans on `?` being
  reachable, whereas `esc` and `tab` are in `?`'s own *Everywhere* block and mean the same thing on every screen.
  Rank keeps them on the row in every state measured; a screen that pushes them off is a screen with too many keys,
  and that is that screen's bug.
- **The row holds about seven hints**, which is the number every candidate was measured against. The worst screen is
  the **account screen** — seven own letters in front of the shared ten, 21 hints wanting 239 columns against 80 — and
  a picked reference *stands in for* the poll keys rather than stacking with them, so the two in-front cases are
  alternatives. Pruning to a fixed set fits and draws four hints on a quiet feed, wasting 37 columns, which is the
  *a row cut back to `g tab ?` reads as broken rather than as empty* failure #195 named. Grouping (`b/f:marks`) saves
  about 30 columns, still cuts 4 to 9 hints silently, and spends #66's key-to-word mapping and a second meaning for
  the colon — and a group would have to match on the **hint** rather than the letter, or the inbox's `d:dismiss` is
  swallowed into `p/e/d:yours`, which is the bug #193 fixed. **A second row is refuted rather than disfavoured**: two
  rows at 80 columns still need `…+3` on the worst case and `…+6` on the account screen, measured before #229 gave
  it `s`.
- **The overflow mark is `…+10`, drawn in `muted`, and adds no role.** `…` is the shell's own *there was more* glyph,
  so the **no-colour case is carried by glyph** — a mono terminal draws the mark exactly as a coloured one does,
  which is what makes it different from the ellipsis it replaces: that said *something was cut*, this says *ten
  things were cut and `?` has them*, and the `+N` is the part `?` answers. `+10` with no glyph, a bare `…`,
  `…10 more` and `…+10 in ?` were all drawn and rejected.
- **The count is hints this screen answers to right now and the row had no room for.** An idle key is not *more
  keys* — it is not a key here at all — so counting one would make the mark promise `?` entries that do nothing.
- **A key that cannot act is off the row.** `p`, `e` and `d` act only on your own posts; `x` means nothing on a post
  with nothing hidden or already revealed; `b` and `f` need viewer state the post already carries. This is the
  direction #87, #119, #193 and #195 already started, applied consistently. **`x` leaves the row once it has been
  pressed** — a reveal is one-way, so the key has nothing left to do and `…+4` becomes `…+3`, which is the same rule
  applied honestly. The two facts it needs: `Revealed.Ask` already answers *can `x` act here* with no new state, and
  **`Post` carries whether it is the reader's own**, the way `PostMarks` already carries which of the three marks
  were theirs. That keeps the filter in `Screen.Keys` beside the other two, teaches a screen nothing about the
  instance, and hands the same fact to the CLI; `Shell.IsMine` becomes the one place that *sets* it.
- **`?` stays in step with the row.** `HelpScreen` reads `about.Keys`, which *is* `Screen.Keys`, so the filter
  applies to the keymap in the same place rather than beside it. `?` answers *what does this screen answer to*, and
  on somebody else's post `p` is honestly not one of them. The row is more discoverable than it was, not less:
  `?:keys` is pinned where it used to be first to be cut.
- **The row degrades instead of truncating at every width.** At 40 columns the worst case is
  `1-0:option · v:vote · …+14 · ?:keys`; the floor is ` …+16 · ?:keys`, 15 columns, and no width the shell supports
  reaches it. The row is full width, so 80 columns is 80 here rather than the breadcrumb's 61.
- **A confirmation reserves the columns its answer needs, draws the question in what is left, and clips the
  question — never the answer.** The row may lose what it is asking about; it may never lose how to answer. The same
  argument the breadcrumb makes one row up: cut what the reader can reconstruct from what is on screen, never what
  tells them how to act. A confirmation getting its own row or a modal is the frame change ADR-0014 says has to be
  earned, and nothing here earns it.
- **A confirmation names only what a reader can check.** `Delete post {id}?` becomes **`Delete this post?`** — no
  screen draws a post id, so it can be checked against nothing, while `Role.Selection` says which post on the feed as
  everywhere else, and the row stops depending on an instance's id format. The vote stops quoting the option:
  **`Cast the answer you ticked?`** against many's `Cast the 3 answers you ticked?`, on the reasoning that already
  counted three rather than naming them — the ballot is on screen with every answer drawn `[x]` — and the longest
  answer said goes with it.
- **`This cannot be undone.` stays verbatim, and is the droppable half.** The question degrades in two steps: draw
  the ask and the warning if both fit, else the ask alone, and only clip mid-word if even the ask will not. A `…`
  should mean *something you cannot see was cut*, which is false of a boilerplate sentence identical on all three
  confirmations. So `Confirmation`'s question splits into the ask and a defaulted warning, which also stops three
  callers repeating the same sentence. At 80 columns all three fit whole: 62, 70 (or 73) and 69 columns.
- **A confirmation's no-colour case is position.** Under `NO_COLOR` a confirmation and a notice are both a sentence
  on the status row — but a confirmation is the only status row that ends in `<key> <word> · esc <word>`, and a
  notice never shows keys. No leading glyph and no band.
- **A touched compose asks before it is thrown away, on every way out (#373).** `esc`, a right click, `tab`,
  `shift-tab`, `` ` ``/`~`, a click on the rail or on a crumb behind it, `/` from a screen over the draft — the
  attachments screen, or help over it (#426) — `ctrl-p` and `ctrl-q` all go through one check in the shell: where a
  compose screen the way out would take off differs from what it opened with — its text, its warning, To or Lang
  (`ComposeScreen.Touched`) — the confirmation row asks **`Discard this post? y / n`**, and nothing else happens
  until it is answered. Only `y` discards and finishes that way out; the way out's own key
  pressed again does not, so `esc esc` or `tab tab` never throws a draft away. Anything else keeps the draft and does
  nothing more — a stray second `tab` cannot finish what the first started. A step along the rail (`tab`,
  `shift-tab`, `` ` ``/`~`) that takes a draft off, asked or not, arrives at the destination stepped to at once,
  without the settle (ADR-0014 amended), so the screen under the draft is never shown in between. Tabbing on from there
  settles as ever.
  *Touched* is what the screen holds rather than whether a key was pressed: a reply still holding only its mention and
  the warning it opened on, an unchanged edit, and a letter typed and rubbed out again all leave without asking. The
  question reads `y / n` rather than `y discard · esc keep` because it is put in front of `esc` among other ways out,
  and it carries no warning sentence, the question being the whole of it. While it is open the compose fields hand
  every key to it, so a letter answers rather than being typed. Sending is not a way out of this kind and asks nothing.
- **80 columns is the floor and the degradation ships anyway.** Nothing below 80 earns a design compromise, but a row
  that asks a question must not be able to lose its answer at *any* width, so reserve-the-answer and the sentence
  drop are a guarantee rather than a supported layout.
- **The CLI does not follow.** `Consent.Given` keeps `Delete post {id}? This cannot be undone.` — there is no
  selection and no width contract there, the user typed the id as an argument, and echoing it back *is* the
  confirmation. `Confirmation` is a TUI type.
- **A key you press is drawn in `key`, its own role.** `chrome` was never one job: it paints the frame's furniture —
  the trail's ancestors, the row's separators, the rail's rule — *and*, through `KeyHint.Spans`, the key, which after
  the fill rule is the most actionable token on the row. That is why #66's split between a key and its explanation
  reads as invisible, and why `HelpScreen` had already worked around it by drawing its key column in `byline-handle`
  blue. `key` paints a token you press in three places — the status row's key, the help screen's key column, the
  confirmation row's answer keys — and **not prose that names a key**: `v casts this vote, esc discards it` stays
  whole, because lighting one word inside a sentence teaches that the shell's prose is pressable. It paints **the
  key, not its padding**, so the help screen's 16-column key column is the key in `key` and a `body` spacer after it
  — a themer who gives `key` a background would otherwise get a 16-column band across every help row.
- **The standing test, which is the transferable part**: *a colour distinction is owed where a reader's next action
  depends on telling the two apart.* A key against its gloss passes — you press one and not the other. A separator
  against a gloss fails: both are furniture. So ` · ` stays `chrome` (` | ` on the status row since ADR-0021), every explanation stays `muted`, the `…+N` mark
  stays `muted`, and **`quota`, `audience` and `muted` go on sharing one hex on purpose** — nobody has to tell a
  rate-limit number from a visibility glyph to do anything, and no test forbids two roles sharing a hex. Adjacency
  was the first cut and does not survive: it would force apart every role that ever shares a row. A distinction only
  truecolor shows still counts as a boost and never as a carrier, which is the basis `loading` already ships on.

### Where the code answers this

One module, `Shell/Keymap.cs`, holds the whole of the dispatch above: a `ShellKey` and a `Screen` go in and a `Verb`
comes out, and it is the only place in the TUI that names a screen type to decide what a key means (#147). It was four
places before — the window's frame keys, the window's content keys, `Shell.Press` for the four that collide, and the
compose pair scoped by a type test on the window — and none of the four was this document.

Three things stayed outside it, each deliberately:

- **What a screen announces.** `Screen.Keys` and `PostKeys` are untouched. What a screen offers and what it answers are
  asserted against each other rather than derived from one another, because a key announced and then refused reads as a
  shell that missed the press, and one source would make that class of bug untestable.
- **Whether the press was used.** `←`, `→` and the digits are consumed only where there is something to walk or toggle,
  and that is the screen's answer, relayed as the `bool` `Shell.Do` returns. The keymap says what a key *means*; only
  the screen knows what is on the post, and asking it in two places is how two places come to disagree.
- **What a screen-local verb does.** The keymap decides which verbs a screen is sent; the screen carries them out, in its
  own file, through `Screen.Answer(verb, reach)` (#232). `Shell.Do` asks **Drafting** of every verb first, through one
  arm, and a draft's verbs — compose, reply and edit among them — are drafting's (ADR-0029). Of the rest it keeps the
  frame's verbs and the ones that act on the picked post of any screen — marks, delete, vote, reveal, opening a
  reference or an author — and `m`, which reads across the conversation and DM screens; every other verb falls through
  to the screen. What a screen can do while it answers is **Reach** (GLOSSARY.md): the ports and profile, one `Put`
  through the shell's **Enquiry**, `Open`, `Swap` and `Again` of a **Subject** (#233, #422), `Say`, `Confirm`,
  `Changed`, `Tell` of a **Change** (#234) and `IsMe` —
  and nothing of the stack, the rail or the cache. So the account screen's
  ties, `w` and `s` are in `AccountScreen.cs`; a follow list's `s`, `f` and `⏎` in `FollowsScreen.cs`; search's `⏎` in
  `SearchScreen.cs`; `w`, `d` and `D` in `NotificationsScreen.cs`; the actors screen's `⏎` and `s` in
  `ActorsScreen.cs`; `a`, `x` and `⏎` in `FollowRequestsScreen.cs`; the conversation list's `⏎` in
  `DirectMessagesScreen.cs`; and Discover's `⏎`, `F` and `d` in `DiscoverScreen.cs`. A `Confirmation` carries what
  agreeing to it does, so `D` asks the same way a delete and a vote do.
- **The verbs that need a terminal.** `ShellWindow` still carries out `ctrl-q`, the six movements that walk the page
  rather than the list (`↓`/`↑`, `PgDn`/`PgUp`, `Home`/`End`), `j`/`k` and `[`/`]` — which move the pick *and* the
  page — the arrows and `Home`/`End` that walk the entries of the file browser and the attachments screen likewise,
  and `ctrl-s`, which
  the editor widget takes off the keys before the shell's own path can. Nothing else about a key is the window's: it translates the press
  and hands the verb on.

The window knows nothing of `ComposeScreen`. Compose's geometry and focus — laying the editor and the warning, To and
Lang fields where the screen says they go (#315), which has the keys, and what each opens with — are `ComposeView`'s, one
view the window adds once over the content panel's viewport (#365). It also takes the arrows no field took and walks the
fields with them, as the keymap says they mean on compose. Its Media header and rows take no typing, and leave the window
every key the keymap means something by on compose, swallowing the rest — the keymap binds every key compose does not
answer to nothing, so the rows keep no list of keys of their own. The lists of people to mention and of languages are its too,
each hung under the field it serves and asked about every click ahead of the views (#366): the window knows nothing of
compose beyond adding `ComposeView` and saying where the content viewport is.

Every mouse report is put right before any of them reads it, by `MouseButtons`, which the client lays over the
application before the window is shown, so that it is asked ahead of `ComposeView`'s lists and every view (**Pointer**).
It decides nothing a gesture means; it only says what the buttons did.

### What the profiles screen settled

#240 put the profiles screen on the stack (ADR-0020). What this document now holds it to:

- **`ctrl-p` is a frame key**, beside `esc`, `ctrl-q`, `?`, `/` and `tab`. It pushes the screen from wherever the reader
  is, drilled in or not, and pressed on the screen itself it pushes no second one. On compose it is a way out of the
  draft, since drafts do not survive a switch: it takes the compose off and opens the screen in its place, asking
  first where the draft is touched (#373). Like every frame key it is on no screen's status row. No help screen is ever
  drawn over compose for it to be left off, `?` being a letter in both of compose's fields (#320).
- **A profile is two rows**: its name, followed by `acting as` and `default` where they apply, then the full
  `@handle@instance` — or the instance alone where no account was ever established. The markers are words in `muted`,
  so they read with no colour, and the name is clipped before they are. Profiles are walked with `j`/`k`, one blank row
  between two of them.
- **The plaintext-token warning** heads the screen whenever the credential file is the token store, in `content-warning`
  and wrapped to the content width. The words are `TokenStorageDescription`'s, the same ones `profile add` and
  `profile show` print.
- **It fetches nothing.** The list is read off the local config when `ctrl-p` is pressed, through `ProfilePorts` — a
  port of its own and not one of `ShellPorts`, whose ports all reach an instance. No enquiry is put, so there is no fetch
  mark and no answer to land late.

### What the add screen settled

#245 put adding a profile on the stack, over the profiles screen (ADR-0004, ADR-0020). What this document now holds it
to:

- **The order is the instance, a sign-in, the check, then a name.** `profile add` asks for the name first and is
  unchanged; the TUI asks for it last, when it can default to the handle just signed in as.
- **The instance is checked before anything is sent**, by `InstanceDomain`, and a rejection is drawn under the field in
  that rule's own words.
- **The browser is the default.** The sign-in is begun, the browser opened, and the authorization address drawn either
  way, in `BrowserSignIn`'s words — the ones `profile add` prints. The address is cut across rows rather than clipped,
  since it is there to be copied whole. `Waiting for the browser to come back...` is drawn in `loading` while the
  redirect is awaited. `t` gives the browser up for a pasted token, and a browser that fails says why and offers `⏎` to
  try again.
- **A pasted token is never drawn**: one `*` a letter, and trimmed as `profile add` trims one.
- **The token is checked before anything is written.** A refusal is drawn on the add screen, in `error` and in words,
  and the reader is put back at the token field, emptied, rather than at the start.
- **The name begins as the handle**, and is edited like any field. A name already in use asks first, in the
  confirmation row's usual form, because adding replaces — and declining writes nothing.
- **`IProfileRegistry.Add` is the only writer**, and decides whether the profile becomes the default as it does for
  `profile add`. The shell then stands a fresh profiles list in place of the one underneath, with the new row picked,
  and says what was added on the status row. It does not switch to it.
- **Every call is an enquiry**, so each has the fetch mark and none blocks the drawing thread. What comes back lands
  on the add screen while it is still on the stack, not only while it is in front: a reader who opened the keymap
  while the browser was out has not walked away from a token already issued. Once the screen is off the stack —
  `esc`, `tab`, `/` — nothing lands, nothing is written, and the loopback listener has been closed.
- **Letters are typed into the field on the step in front**, instance, token or name, so `t` and the frame's `/` and
  `?` mean something only on the steps that are waiting rather than taking letters.
- **A paste goes into whichever field is typing**, here and in every other field the shell types into itself (the
  search prompt, the compose warning, the follows filter). A terminal in bracketed-paste mode sends a paste as one
  string rather than as keys, so the shell takes it as text: a line break or a tab stands as a space, and it is never
  replayed as keys. Where nothing is typing the paste is left to whatever has focus, which on compose is the editor —
  but for files dropped onto the terminal, which compose attaches (#375).

### What switching settled

#243 put switching on `⏎` on the profiles screen (ADR-0020). What this document now holds it to:

- **For this session only.** The profile is resolved through `IProfileRegistry.Resolve`, as `--profile` resolves one,
  and nothing is written: the config file and the token store are byte-for-byte what they were, and the default
  profile has not moved.
- **Not offered on the profile already acted as**, where it would do nothing; on every other row it is `⏎:act as`.
- **A switch starts again on Home**, as if launched with the new profile. The shell is reset in place rather than
  rebuilt: the rail keeps its place in the window but its labels (the Profile destination's `@handle`, the instance
  row) and every count are the new profile's, the cursor and selection are on Home, and the stack, the arrival and its
  cache, every pick and reading, a confirmation waiting and a remark on the status row are let go of. The quota the
  old profile's instance last reported is not drawn until an instance reports the new one's.
- **Nothing asked as the old profile reaches the new one.** The enquiry the old profile asked through is abandoned:
  every call in flight is cancelled, and no answer, failure, countdown or fetch-mark tick of it is let back onto the
  drawing thread. The rail's counts, which are read beside the enquiry rather than through it, are cancelled with it.
  An action the instance already received is not undone and not presented as undone — its answer is simply never
  drawn.
- **A profile that can't be resolved isn't switched to** — a token gone from the keyring above all. Nothing is let go
  of, and the resolver's own words say why on the status row, in `error`.
- **Pictures survive**, being a file server's rather than anybody's, and the window's rather than the shell's.

### What making a profile the default settled

#244 put making a profile the default on `D` on the profiles screen (ADR-0020). What this document now holds it to:

- **It is `profile switch`**, through the same `IProfileRegistry.Switch`: the config file's `current_profile` is what
  changes, so `wooly profile list` and every command with no `--profile` go by it afterwards. The token store is not
  touched.
- **It does not change who this session is acting as.** Nothing reaches an instance, and every later request goes out
  as before. A reader who wants both presses `⏎` as well; the two are kept apart so that browsing a work account in
  the TUI never changes which account a CLI script posts as.
- **It asks nothing**, since `D` on the old default puts it back.
- **The `default` marker moves** on a list read again with the row still picked, and the status row says who commands
  now act as, in `profile switch`'s words.
- **Not offered on the profile already the default**, where it would do nothing; on every other row it is
  `D:make default`. `D` means clear all on the notifications inbox; on the profiles screen there is no inbox to empty.

### What removing a profile settled

#246 put removing a profile on `x` on the profiles screen (ADR-0020). What this document now holds it to:

- **It is `profile remove`**, through the same `IProfileRegistry.Remove`: the config entry and the token go together,
  and nothing reaches an instance. The token is only this machine's copy; the authorization is the instance's to
  revoke.
- **It asks first**, in the same form as deleting a post: `Remove this profile? This cannot be undone.  y remove · esc
  keep`. Declining, `esc` included, changes nothing.
- **The profile acted as is refused**, with *Switch to another profile before removing this one.*
- **So is the default**, with *Make another profile the default before removing this one.*, as `profile remove` refuses
  it too (ADR-0020). Removing it would leave every command with no `--profile`, and the next launch, with nothing to act
  as. Choosing another is the reader's, with `D`. Both refusals come before anything is asked.
- **`x` stays on the status row on every row**, those two included. That is the one exception to #220's rule that a
  key which would refuse comes off: each refusal says what to do first, which `p`'s *Only your own posts* does not, and
  a reader who never saw `x` offered on the row they are on would not learn what to do to remove it.
- **The row goes, and the pick moves beside it**: to the row under it, or the one above where it was the last. Down to
  one profile, the rail's instance row goes too.
- `x` means show what the post is hiding everywhere else; on the profiles screen there is no post.

### What first run settled

#247 moved the launch's profile failures inside the shell (ADR-0020). What this document now holds it to:

- **The shell can start with nobody to act as.** It then shows the add screen as its only screen, with no rail and no
  gutter: the breadcrumb and the content take every column, since every destination is read as somebody. `Opening`
  decides this from `IProfileRegistry.Resolve` before a screen exists.
- **That is no profile set up, none the default, or a token missing from the store** — every
  `AuthenticationException` the resolver throws — **and a token refused at launch**: an enquiry refused before anything
  has been read as the profile it launched as. A refusal after that, or after a switch, is only said on the status row
  ("What signing in again settled", below).
- **A missing or refused token opens the add screen filled in for that profile.** Its instance and name are fixed and
  drawn, its crumb is *Sign in again*, and why is drawn under them in `error`. Only the sign-in and the check run; the
  token then replaces the old one under the same name, without asking, since replacing it is what the screen is for. A
  token that signs in as somebody other than the account on record is refused on the screen, and nothing is written.
- **Only the add screen's own keys mean anything.** `⏎` and `t` as ever; `tab`, `/`, `?` and `ctrl-p` do nothing,
  since each goes somewhere and there is nowhere. `esc` has nothing under it, so it starts the steps over — giving up
  a sign-in or a check in flight, and keeping the instance typed — and is not offered on the first step. `ctrl-q` is
  on the status row throughout, and quits with `TuiExit.Success`.
- **A profile written starts the shell as a launch would**: acting as it, on Home, with the rail, its counts read.
- **Still said on stderr, with `TuiExit.Failed`**: a config file that can't be read, one naming a default that isn't
  there, and a `--profile` naming nothing. Each is a mistake the reader just made where they made it, and a form is not
  the place to fix it.

### What signing in again settled

#248 put signing a profile in again on `R` on the profiles screen, and said a token refused mid-session (ADR-0020). What
this document now holds it to:

- **`R` is the add screen filled in for the picked profile**, as first run fills it for a refused token: the instance
  and name fixed, the crumb *Sign in again*, and only the sign-in and the check. The token then replaces the old one
  through `IProfileRegistry.Add`, without asking, since replacing it is why `R` was pressed. The name, the instance, the
  account on record and whether it is the default are as they were. The reader is back on the list with the row
  picked, told *Replaced profile work.*
- **A token that signs in as somebody else is refused** on the screen, naming both accounts, and nothing is written.
  Catching a working token for the wrong person is what the check is for.
- **Offered on every row**, the one acted as and the default included: every profile's token can be replaced.
- **Signing in again as the profile acted as keeps the session.** It is still the same person, so nothing is let go of:
  the stack, the arrival's cache, picks and readings stay, nothing is read again for it, and every request from then on
  goes out with the new token. Only what asks as the profile is built again, around the same enquiry. `a` writing over
  the profile acted as, under its name but as another account, is not the same person: the session starts again on
  Home acting as it, as a switch does.
- **A token refused mid-session is said, not acted on.** Any enquiry the instance answers with a 401 puts *This
  profile's token was refused — ctrl-p to sign in again.* on the status row, in `error`, and nothing opens by itself:
  whether to sign in again now is the reader's. A 401 to a request made with a token is an `AuthenticationException`
  from core (`RefusedTokenHandler`), so the CLI reports the same refusal with its authentication exit code (ADR-0006).
  A refusal of a question asked as a profile no longer acted as says nothing.
- `R` is its own key because `r` is reply; off the profiles screen it means nothing.

## Pointer

The mouse is another way to ask for what the keys already ask for (#286). It adds no verb and no table beside the
**Keymap**: `ShellWindow` is where Terminal.Gui's mouse events stop, as it is for keys, and all it works out is which
panel the pointer is over. What the gesture then does is a move the keys already make.

| Gesture | Over | Does |
|---|---|---|
| Wheel down / up | The content panel | The page moves one row per notch, the finest step a terminal has, so a trackpad's stream of small events glides rather than lurching three rows at a time (#292). Three notches are as far as one `↓` / `↑`, and it clamps at the same ends. **Picked** stays where it is, so after wheeling it off the page `j`/`k` reclaim the first thing whose start — the first row of its content, or its first row where it has none — is on the page (the topmost thing showing, where no start is), as after the arrows (#472, asked in review of #480). On help and notices too, which are content like any other screen. A notch is that arrow through the **Keymap**, with a row for its step, so with a confirmation open it declines it and scrolls nothing, as `↓` does (#287) |
| Wheel | The rail | Nothing: not the cursor, not the selection, not the rail's scroll and not the page beside it |
| Wheel down / up | Either margin of a narrowed shell | The content panel's wheel, exactly: so a reader need not aim for the panel to scroll (**Shell width**, #416) |
| Click, double click, right click, drag | Either margin of a narrowed shell | Nothing, not even declining an open question: the margins are the terminal's, outside the shell (#416). A drag begun inside the shell — a row of the attachments screen, the **Scrollbar**'s thumb — carries on there, holding the mouse (#503) |
| Wheel sideways | Anywhere | Nothing. A trackpad drifting sideways sends these between vertical notches, and Terminal.Gui's left and right carry up's and down's bits, so they are asked about first and dropped |
| Wheel, click | The compose editor | Terminal.Gui's own handling: the wheel scrolls the draft and a click places the caret. Nothing of the shell's |
| Click | A value of compose's **To** | Chooses it and gives To the typing, so `←`/`→` carry on from there; a click on either arrow of the narrow form steps, and anywhere else on the row gives To the typing. A value To does not allow, and the whole row on an edit, ignores it. The wheel over the row does nothing (ADR-0024, #338) |
| Click | A field of the edit public profile form | Gives it the typing, the caret where the click was, so the walk carries on from there (#406) |
| Click | A toggle of the edit public profile form | Flips it and takes the walk there, as `space` on it does (#406) |
| Click | The Avatar or Header row of the edit public profile form | Takes the walk there and opens the file browser for one picture, as `⏎` on it does (#407) |
| Right click | Anywhere, with the edit public profile form in front | `esc`, wherever it lands, a field included: over a touched form it asks `Discard these changes? y / n` (#406) |
| Click | A person in the open list of people to mention, in compose's post or the public profile's bio | `tab` on them: their `@user` goes in place of the word and the list closes (#335, #408) |
| Wheel down / up | The open list of people to mention | Moves the pick a person at a time, stopping at either end, scrolling the list where it holds more people than it shows (#335) |
| Click | Anywhere outside the open list of people to mention | Closes it, as `esc` does, and nothing more: the caret stays, the draft or the bio is as it was, and what is under the pointer is not clicked (#335, #408) |
| Click | A destination on the rail | Arrives there at once, through the rail's immediate path: the cursor and the selection move together, there is no settle window, and any landing the tabbing left waiting is abandoned, so only the destination clicked is read (#288). Which entry a row is comes off the rail's own rows, each carrying its destination's place as its `Line.Item`, so the click lands on what is drawn under the pointer whether the rail is framed, compact or compact and scrolled |
| Click | The destination already shown | Walks back out to its own screen: drilled in from it, the stack goes back to its one bottom screen with the page and **Picked** it was left on, as any pop keeps, and nothing is asked of the instance — the way a sidebar's entry takes you back to its top page (#289). On its own screen already, nothing. With a touched compose drilled in it asks first, as every way out of one does (#373). The first crumb of the breadcrumb is the same click (#308). One of the two moves the mouse has that the keys do not: tabbing back onto the destination shown is still a walk that ended where it began |
| Click | The `«`/`»` mark, column 1 of the shell's bottom frame row: on the shown rail's foot, or the first cell of its compact gauge row, and on the content panel's bottom-left corner with the rail hidden | `\` through the **Keymap**: the rail hidden or shown and saved, or nothing where `\` means nothing (#443, #445; **Hidden rail**, above). Which cell is the mark is the one cell, shown or hidden, so the window answers it before any view is asked: hidden, it is on the content panel's frame, whose cells Terminal.Gui hands to the panel's own edge. None where there is no rail |
| Click | A group's title, a compact heading, the API panel, the rest of the edge the mark is on | Nothing. Only destinations and the mark answer a click |
| Click | A crumb of the breadcrumb, behind the one in front | Walks back to that screen in one move: everything drilled in above it comes off the stack, as that many presses of `esc` would take it off, and the screen is in front as it was left — its **Page**, its **Picked** and any **Reference** picked on it — with nothing asked of the instance. What the screens taken off asked for is dropped, as with any pop. The first crumb is the click on the destination shown above. Which crumb a column is comes off the breadcrumb's own spans, each carrying its place in the stack as its `Span.Item`, as the rail's rows carry theirs, so the click lands on the crumb drawn under the pointer however the trail is elided. A crumb whose walk back would take a touched compose off the stack asks first, as every way out of one does (#373); a crumb above a compose still walks back. Crumbs are not drawn as clickable. The other move the keys do not have (#308) |
| Click | The crumb in front, the `… ›` lead of an elided trail, a separator, the fetch mark, the edge after the trail | Nothing. Only a crumb behind the one in front answers a click |
| Click | A row of a thing in the content | Picks that thing, through the same pick `[`/`]` make: any row of it counts — a tall post's byline, its text, a picture on it — since which thing a row is part of is its `Line.Item`, the ordinal `Screen.Pick` takes. The remark on the status row goes, as it does for `j`/`k`, and a **Reference** walked to inside the thing is let go, so the `⏎` after it opens the thing rather than the reference. The page stays where it is rather than following the pick, so what was clicked never moves out from under the pointer. The same on every listing screen, the account screen's header block (after which the post keys go quiet, as after `j`/`k`) and a search's results in their sections among them (#290). A click on a **Reference** — a hashtag, mention or address in a post's text, a **Bio** or a **Custom field**, an **Attachment**'s own address, a **Link preview**'s, a drawn picture anywhere on its box or in the bracket columns beside it — picks the thing it is inside and walks to it, exactly as `→` would have, brackets and the status row's reference mode included; a click on either row of an address the wrap cut in two, or on the brackets of the one already walked to, counts. It never opens anything. Which reference a column is comes off the runs drawn under the pointer, each carrying the reference it is drawn for as its `Span.Reference`, as a crumb's carry its `Span.Item`; a reference drawn and never walked, a mention in a bio, picks the thing and walks to nothing (#475) |
| Click | A heading, a rule, a blank, the space under the last thing | Nothing. A click never guesses which thing was meant |
| Double click | A row of a thing in the content | That click, and then `⏎` through the **Keymap** on the screen in front, so it means whatever `⏎` means there: it opens a post, a search result, a conversation, the asker of a follow request or a person, and switches to a profile on the profiles screen. Where `⏎` means nothing for what is picked — the account screen's header block — it picks and does nothing more. On a **Reference** it opens the reference: the first click walks to it (#475), and the `⏎` opens it and lets it go (#474) — a hashtag's screen, a mention's account, an address handed to the browser, a drawn picture's **Picture screen**. It opens the one the first click walked to, without asking the row again, so the brackets that click drew moving the row a column on, or wrapping a long address anew, cannot change what opens (#479). Off any reference the click lets a walked **Reference** go first, so a double click on a post's text with a hashtag walked to opens the post. Terminal.Gui's own detection says what is a double click (#291) |
| Double click | A heading, a rule, a blank, the space under the last thing | Nothing: never a `⏎` on what was picked before |
| Double click | The breadcrumb | Its first click's walk back and nothing more: never a `⏎` on the screen it landed on (#308) |
| Click | The **Scrollbar**, its track or its thumb: the content panel's right edge or the margin column just inside it, beside the track's rows | Nothing: the click moves no page, leaves **Picked** where it is and picks no row beside the margin. With a confirmation open it declines it, as a click anywhere does. A press on the thumb instead takes hold of it for the drag, the only thing on the bar that moves the page (below, #503). The bar answers on two columns though only the edge is drawn, so there are two to aim for the thumb with; the window answers them before any view is asked, the edge being the panel's frame — the hit area #502 gave the bar, whose page turn on the track was dropped |
| Drag | The **Scrollbar**'s thumb, on the edge or the margin column inside it | The **Page** follows the pointer row by row, the grabbed point of the thumb kept under it — held as how far into the thumb it is, so that a thumb shortened by rows arriving mid-drag, a list read on, comes back under the pointer on the next motion with the grabbed point inside it — and thumb and page are drawn moved on the frame the motion lands in. The page's top is the thumb's maths inverted, held at the track's ends, so dragged past the top or the foot of the track they stop there. The press takes hold of the mouse (the window grabs it), so the drag carries on wherever the pointer wanders — off the bar sideways, out of the panel, into a margin of a narrowed shell — and letting go anywhere ends it, the click Terminal.Gui makes of the let-go spent on it, so that nothing under the pointer is clicked and the next click is a click. It moves the page as `↓`/`↑` do and not **Picked**, so `j`/`k` reclaim afterwards; a held **Reference** carried off the page is let go; and near the foot of a list that reads on it reads on, as every scroll there does. With a confirmation open the press takes nothing and moves nothing, and the let-go's click declines it (#503) |
| Double click, right click | The **Scrollbar** | Nothing: the double click is no `⏎`, and the right click is no `esc` (#502) |
| Click, double click, right click | The content panel's right edge, and the margin column inside it, where no bar is drawn — a screen whose rows fit, one with widgets of its own, or with `scrollbar = "none"` | As without a scrollbar: the edge answers no button, and a click in the margin beside a thing's row picks it, as on its blank space (#502) |
| Right click | Anywhere but the **Scrollbar**: the content, the rail, the breadcrumb, the status row | `esc`, through the **Keymap** on the screen in front, so up one level of whichever kind is open: it lets a picked **Reference** or an uncast poll toggle go before it pops the screen, declines a confirmation, clears the filter prompt's filter (as `esc` does, not as a left click does) and calls off a sign-in in flight on Add a profile. On a destination's own screen it does nothing, and never quits. Over the rail it does not arrive at the destination under the pointer, and over the breadcrumb it does not walk back to the crumb. Each right click the terminal reports is one `esc`, Terminal.Gui's double and triple included, so three quick right clicks walk back three levels. Only the terminal's right button counts: ctrl+click stays a left click, and the middle button means nothing (#307) |
| Right click | Anywhere, with compose in front | `esc`, wherever it lands — a field, the rail, the breadcrumb, the status row: over a touched draft it asks `Discard this post? y / n`, and a second right click agrees as a second `esc` does; over an untouched one it leaves (#373). Taken ahead of the view under the pointer, so the compose editor's own context menu never opens, and on the open list of people to mention it neither closes the list nor picks |
| Right click | Anywhere, with the description editor in front | `esc`, wherever it lands, its field included, as on compose and the edit public profile form: done, back to the draft with the description kept and sent as leaving it any other way sends it. With a question open — `tab` off a touched draft under it asks — it answers it as `esc` does. Taken ahead of the field, whose own context menu never opens (#430) |

**Open questions win.** While a confirmation is on the status row or the filter prompt is open, a click anywhere is a
key the question does not take: it declines the confirmation, or closes the prompt with what was typed still narrowing
the list, as `⏎` would, and whatever the click was on is not carried out. With nobody to act as there is no rail to
click, and a click does no more than the keys there allow. Terminal.Gui reports a double click's first click on its own
before the pair, so a double click whose first click declined a question or closed the prompt is spent with it and
opens nothing behind it. A click on a crumb is one of these clicks too: it declines or closes the question and walks
nowhere. A right click is not one of these clicks but `esc`, and answers the question as `esc` does:
it declines the confirmation too, but takes the filter off rather than leaving what was typed narrowing the list. Over
the fields of a draft — compose's, the edit public profile form's, the description editor's alike — a click or a wheel
notch while a question is open goes to the question, not the field: it declines it, and the field neither takes the
typing nor moves its caret nor scrolls. A sideways notch is no answer there either. A key answers the question the same
way from any of their fields: `y` agrees, and anything else keeps the draft, the field taking none of it (#373, #430).

**A button is believed only from a press** (`MouseButtons`, review of #372). Terminal.Gui turns on any-motion
tracking in SGR form, takes the reports as the terminal words them, and makes clicks of them itself: a click as a
pressed button is let go, and a double click of a second within 500 ms on the same cell. A terminal can word the
pointer merely moving as a drag with a button held. Ghostty does it once it has missed a button's let-go — a right
click whose let-go a menu or a change of focus took — and keeps doing it until its window closes; macOS's terminal was
found doing it with the left button (#378). Terminal.Gui took the button for down and made a click of the next report
that said it was not, a wheel notch or another button's press most of all. With the right button "held", every notch
and every left click was a right click, which is `esc`: the wheel and a click in empty space backed out of compose and
the file browser. With the left one, the wheel over the file browser moved the cursor and then attached a file and
left, and over a Media row clicked whatever was under the pointer. `MouseButtons` is laid over the application ahead
of every view and puts each report right before anyone reads it, for every button alike: the pointer moving with a
button "held" is the pointer moving, unless a press of it was reported and neither its let-go nor its click has come
since; a click with no press reported since the last one is no click; and a double or triple click whose first half
was one of those counts only the clicks there were. What the gesture then means is unchanged.

A double click on a run that stands for a key of its own — an attachment's `x` or `retry (r)`, a file's box — is a
click on it and no `⏎`, since Terminal.Gui reports the second of two quick clicks on one cell only as the pair.

Mouse tracking stays on, and nothing turns it off: drag means nothing but a row of the attachments screen and the
**Scrollbar**'s thumb, and selecting text to copy goes through the terminal's modifier bypass (`⌥` in iTerm2, `Fn` in
Terminal.app) until selection is a feature of its own. The `?`
screen says what the mouse does beside the frame's keys, an action a row with its input in the key column, as every
key's is (#446):

```
wheel           scroll the content
drag the thumb  scroll the content with it
click           arrive on the rail; pick a row
double click    read (⏎)
right click     up one level, as esc
click a crumb   walk back to it
click «/»       hide or show the rail
```

The `drag the thumb` row is left out when `scrollbar = "none"` turns the **Scrollbar** off ("Starting it", below), since
no screen then has a thumb to drag; it is there for every other style (#505).

A meaning too long for the screen's width, the mouse's or any key's, carries on to the rows under it, each starting in
the meaning column, rather than being cut with `…`: at 80 columns with the rail shown that is `tab / shift-tab`'s and
`` ` `` / `~`'s (#446).

## Starting it, and the destinations the reader writes

`wooly-tui` takes one option, `--profile <name>`, and it means what it means everywhere else: act as that profile for
this run, without changing which one is the default (story 9). Everything else about the profile — which instance, which
token — is resolved through `IProfileRegistry` exactly as a command's scope resolves it. With nobody to act as, it opens
on adding a profile ("What first run settled", above).

Nine of the rail's destinations are the same nine for everybody. The rest are the reader's own: one for each **Tag
set**, a name given to between one and twelve hashtags and read as one timeline of the public posts carrying any of them
(ADR-0033). Which tags are nobody's business but the reader's, so the sets are written in the same TOML file everything
else lives in (ADR-0003), as an ordered `[[tag_sets]]` array beside the `[preferences]`:

```toml
[preferences]
bell = false
hidden_rail = true
hide_drawn_caption = true
scrollbar = "line"
shell_width = 70

[[tag_sets]]
name = "dotnet"
tags = ["dotnet", "csharp", "fsharp"]

[[tag_sets]]
name = "cats"
tags = ["cats"]
```

Each set is a destination in the rail's **Hashtags** group, between Timelines and Explore, in the order the file writes
them, and labelled with its name, clipped with `…` where it is wider than the rail. With no sets there is no Hashtags
group at all, and no entry standing for a tag nobody has named. Arriving at a set is arriving anywhere: the stack resets
to its timeline, its breadcrumb the set's name. It is the feed screen every timeline is — held for the same minute,
`g` reads it again, it reads on near its foot and says "that's everything" when whole — and carries no unread count.
A set of up to four tags is one ask of the instance, `/api/v1/timelines/tag/<first>?any[]=<second>&…`, newest first:
its first tag in the path and the rest as `any[]`, which Mastodon unions with it. Mastodon reads no more than four tags
of an ask and drops the rest without saying so, so a larger set costs one ask for every four tags — its tags cut into
runs of four in the order written, five to eight tags two asks, nine to twelve three, never one per tag — and the runs'
answers are interleaved here newest first, a post more than one run found shown once. Each run reads on from its own
place, and reading on asks again only of the runs whose posts the merged list has used up; the set is whole, and says
"that's everything", only when every run is. One run failing fails the read — arrival, read on or `g` alike — with the
error any failed read shows, and nothing the other runs answered is kept or drawn: a merged list missing a run would
look whole while missing posts from its middle. A run the rate limit refuses is the same, said as any read the limit
stopped is, with nothing kept; waiting it out and reading on asks every run of that read again. A set of one run has no
middle to miss, so a rate limit part way through it keeps what arrived, as it does on Home. The sets are the same
for every profile, each read against whichever instance is acted as, and nothing in the CLI reads one: `timeline tag`
still reads one tag. A hashtag walked to from a post or a search opens as it always has, and is kept nowhere.

The file's rules for a set: a tag may be written `#dotnet` or `dotnet`, and is kept bare; a tag written twice in one
set, in any case, counts once. The whole file is refused, as a `shell_width` that is not a whole number is, with a
message naming what is wrong, for a set with no `name`, a name another set has whatever its case, no `tags`, a tag that
is not one word, or more than twelve tags — three asks, so the dearest set's cost is known before it is written. Sets
have no key in the shell; they change when the file does, so the rail is fixed for a session. Every save of another
preference — `-`/`+`, `\`, the profiles screen — writes them back exactly, in order, straight after `[preferences]`;
with no sets, it writes no `[[tag_sets]]` at all.

The single `hashtag` preference the rail kept one place for is gone. A `hashtag = "…"` left in `[preferences]` is not
refused and does nothing — it is not made into a set — and the next save, which rewrites the whole file, drops it; to
keep that tag, write a set of one.

`hide_drawn_caption` is another reader-owned preference in this section (#71) — a picture's caption, `false` (the
default) shows it always, `true` hides it once a picture is actually drawn, per "What media settled" above. `bell` is a
third (#399): `true` (the default, and what an absent key means) rings the terminal's bell when an Inbox badge rises,
`false` keeps the badges live without the sound. Like the others it is the same for every profile, and has no key in
the shell yet.

`shell_width` is a fourth (#415): the **Shell width** a launch opens at, a whole number from 50 to 100 (above, "Shell
width"). One written out of range is clamped to the nearer end rather than refused; 100 is what an absent key means, and
widening back to it writes 100. Unlike the others it has keys, `-` and `+`/`=`, and each step they take saves it — which
rewrites the whole file, as `D` on the profiles screen does, so comments written in it by hand do not survive.

`hidden_rail` is a fifth (#442, #445): `true` opens a launch on the **hidden rail** (above, "Hidden rail"). `false` (the
default, and what an absent key means) opens on the shown rail, and showing the rail again writes `false`. Anything but
`true` or `false` is refused against the file, as a `shell_width` that is not a whole number is. Like `shell_width` it
has a key, `\`, and each toggle saves it, rewriting the whole file the same way.

`scrollbar` is a sixth (#501, #505): how the content panel's **Scrollbar** (above) draws its thumb, or that there is
none, one of five strings:

| Value | Thumb | |
|---|---|---|
| `"block"` | `█` | the default, and what an absent key means |
| `"line"` | `┃` | a heavy line on the edge's `│` — without colour (`NO_COLOR` or the plain theme), told from the track by weight alone |
| `"left-half"` | `▌` | the cell's left half, toward the screen |
| `"right-half"` | `▐` | the cell's right half, toward the terminal's edge |
| `"none"` | — | no scrollbar: from the next launch the right edge is drawn as it was before, `│` the whole way down in `panel-border-active`, and answers no button, on every screen |

Only the thumb's glyph differs: the track, the colours, the thumb's length and place, where the bar answers the pointer
and the thumb's drag are the same in every style. Whichever is set is kept through every save of the others, as `bell`
is. Anything else — `true` and `false` among it — is refused against the file. Like `bell` it is read at launch, the
same for every profile, and has no key in the shell.


Every save writes each preference that has a default, at its current value, the default included, so a file nobody has
tuned still lists every setting there is to change (#506): `bell = true`, `hidden_rail = false`,
`hide_drawn_caption = false`, `scrollbar = "block"` and `shell_width = 100`. They are real keys rather than
commented-out lines, because a save rewrites the whole file and a comment would not survive it. A file without them
gains them on its next save and means what it meant, since an absent key still reads as its default — a `[preferences]`
written by hand can leave any of them out. `default_visibility` and `default_language` have no default to write (absent
is the server's own, and TOML has no null), so they are written only once set. Every key is
written in alphabetical order, as in the example above, whichever order a file written by hand had them in.

## Roles

A view names a role; the theme resolves it to an attribute. Nothing constructs a colour (ADR-0014). Each role has a
glyph or a position that carries the same meaning when colour is gone — all but `selected-text`, which has neither and
is drawn reversed instead (#316).

| Role | Paints | Carried without colour by |
|---|---|---|
| `body` | A post's text | — |
| `hashtag` | A tag inside a post's text | the `#` |
| `mention` | An account named inside a post's text | the `@` |
| `link` | An address inside a post's text | the scheme |
| `muted` | Timestamps, counts nobody acted on, hints, a status row key's explanation, the row's `…+N` overflow mark, and the crumbs walked through on the content panel's title — the `›` between them and the `… › ` a long trail leads with included, and a custom emoji's shortcode where nothing draws (ADR-0035) | position, and `…` on the mark, and a shortcode's colons |
| `byline-name` | A display name | position |
| `byline-handle` | `username@instance` | the `@` |
| `audience` | The visibility mark | `○ ◌ ● ✉` |
| `content-warning` | A warning and its text | `⚠` |
| `media` | Image placeholders, attachment links, a link preview's title, and the columns a byline holds for an avatar | `▒▒▒▒`, `⏵` |
| `stand-in` | A drawn picture's box while the picture is not here: the shaded fill standing in for it, at its size, under the blur where the instance sent a blurhash (ADR-0025, #349), and a custom emoji's two columns until its picture lands (ADR-0035) | `░` |
| *(none — a picture's own pixels)* | A drawn picture | it is the picture |
| `poll` | Options and their bars | the bar itself, and `✓ `/`[x]` marking a picked one |
| `reference-picked` | The brackets around a picked reference | `‹ ›`, always drawn |
| `boost` / `boost-mine` | The boost mark, and it when it is yours — one colour in both built-ins, the glyph telling them apart | `↺` (open) vs `⥀` (closed) |
| `favorite` / `favorite-mine` | The favorite mark, and it when it is yours — one colour in both built-ins, likewise | `☆` (hollow) vs `★` (filled) |
| `replies` | The reply count under a post | `↩` |
| `selection` | The selected row | `▌` in the gutter |
| `band` | Behind every row of the selected thing | the `▌` beside each row |
| `selected-text` | Text selected in the compose editor or its warning field (#320), and the value chosen on To while To has the typing (#338) — the body's text on a background of its own, lifted clearly off the page where `band` is barely there (#316) | drawn reversed |
| `rail` / `rail-current` | Destinations, and the one loaded — in colour its band, label and count together | without colour, one glyph, one column: `▶` where the tabbing has got to, `▷` where it settled if that differs — they coincide at rest, so only `▶` shows. In colour no mark: the band carries it (ADR-0021) |
| `rail-cursor` | The rail entry the tabbing has got to, while the selection has not yet followed it | `▶` without colour; in colour, its band (ADR-0021) |
| `rail-unread` | An unread count, and the word on an unread conversation or a notification unread on arrival | the number, and the word |
| `quota` / `quota-low` | Rate-limit budget left, and nearly spent; `quota` also the instance above it, with two or more profiles | the number |
| `gauge` / `gauge-empty` | The API budget's filled and empty cells | `█` and `░`, and the percentage |
| `chrome` | The frame's furniture: the status row's leading space and ` \| ` separators | position |
| `key` | A key you press: the status row's, the help screen's key column, a confirmation's `y` and `esc` — never prose that names a key, never the padding beside one | position — last in its pair, after the words' colon; first column on the help screen |
| `panel-border` / `panel-border-active` | A panel's frame, and the frame of the panel you are in: the content panel, and the rail group holding the cursor | the box characters; which group is active is carried by the cursor's entry's band in colour, and by `▶` on it without |
| `scrollbar` | The **Scrollbar**'s thumb on the content panel's right edge, on a track drawn in `panel-border` so the thumb stands out by contrast. Both built-ins draw it in `panel-border-active`'s colour (#500) | the thumb's glyph — `█`, or the `scrollbar` preference's `┃`, `▌` or `▐` (#505) — against the track's `│` |
| `panel-title` | A panel's title on its top edge: a rail group's name, and the crumb being stood on at the end of the content panel's trail, told from the crumbs walked through by foreground | position, on the edge — the current crumb is always the last, and the trail elides from the left |
| `loading` | Something under way that the reader waits on, said in words: the sign-in screen's wait for the browser | the words |
| `spinner` | The fetch mark a space after the content panel's trail — one star spinner frame, in 2 columns held whether or not it is drawn. Both built-ins draw it in `rail-unread`'s colour | a spinner frame, moving, drawn only while a fetch is in flight |
| `destructive` | A delete affordance and its confirmation | the word |
| `error` | A failure the shell has to say out loud | the word |

The people-side work (#159) added no role, deliberately and in four places: a verified **Custom field** takes a `✓`
after a value already drawn in `link`, the `⚙ bot` / `⚿ locked` flags carry in their words, a **Suggestion**'s reason is
a heading rather than a colour, and every standing suffix and section heading is `muted`. Each of those reads
identically under `NO_COLOR`, which is the test a new role has to fail before it is worth adding.

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

`key`'s colour is a rule rather than a hex: an accent, told from its `muted` gloss and the `chrome` around it by hue
rather than by brightness, and never the one accent that marks where you are. In both built-ins it is the colour a
`byline-handle` is drawn in, sapphire beside the frames' blue (#273; it was a hueless grey before the panels, #221).

`band` is the one role no span takes. Which rows are the selected thing's is decided where they are stamped, and each
is marked as picked; the view draws a marked row from edge to edge in what the theme answers for a banded role — `band`'s
background under every role that would sit on the page, and its own under one that has one, so a picked reference a
theme has given a background stays told apart. Whether a role has a background of its own is the theme's to say, never
a colour the view compares (#269). The `▌` beside each row is unchanged, and `Scroll`, `j`/`k` and `[`/`]` still find
the pick by `selection`.

The rail used to reserve two columns — `▶` for the cursor, `▸` for the selection — and showed them adjacent almost
all the time, since the two coincide at rest and differ only for the ~250ms settle window. It now reserves one:
`▶` (filled) on the cursor's row, `▷` (hollow, U+25B7) on the settled row only while the two differ, extending the
audience row's filled/hollow vocabulary (`○`/`●`) rather than teaching a third shape. This retires the no-colour
risk the old scheme carried outright rather than mitigating it: the design never reads `Role.RailCurrent`'s band for
"which one's current", so `NO_COLOR` and a themed terminal show identical marks. The freed column goes to the
destination label (#67).

> **Changed by ADR-0021.** That column is now drawn only where colour is not. In colour the rail has no marks at all:
> the selected entry is on `rail-current` and, mid-tab, the cursor's on `rail-cursor`, and the column goes to the label
> (#272). The rail's line builder is told which by the theme (`ITheme.DrawsColour`).

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
background      = "default"
body            = "#d5d2e0"
muted           = "#7c7891"
byline-name     = "#f2f0f7"
byline-handle   = "#8fa8ff"
content-warning = "#e0af68"
hashtag         = "#6fcf97"
mention         = "#e0af68"
link            = "#8fa8ff"
replies         = "#a99cc9"
boost           = "#6fcf97"
boost-mine      = "#9ef2b8"
favorite        = "#c58fe8"
favorite-mine   = "#e0b6ff"
panel-title     = "#8fa8ff"
rail-unread     = "bright-red"
destructive     = "#ff7a93"

# A role may set its own background; a half it leaves out keeps whatever it was overriding.
# The band behind the selected thing is themed apart from the page.
[themes.midnight.band]
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
  there: the theme's page for nearly every role, and its own background for `band`, `rail-current`, `rail-cursor` and
  `selected-text` — so restating the current entry's foreground, or a selection's, does not silently take away the
  background it is drawn on.
- The page and the band are themed apart. `background` moves the page and leaves the band; `band` moves every row of
  the selected thing, the `▌` beside it included, and leaves the page. `selection` has no background of its own for
  that reason: it sits on the band with the rest of its row (#269).
- `background` is the theme's, not a role: setting it moves everything that was sitting on the page. Every built-in
  theme's page is `default`, the terminal's own background, so the app meets the terminal's padding with no seam
  (ADR-0021). A theme may name a colour instead; `default` is also accepted anywhere a role takes a colour.
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

Without colour, the rail carries one column for this, in the left column with the destination names (in colour the
bands carry it, ADR-0021), whose glyph depends on the row:
`▶` where the tabbing has got to, `▷` where it settled if that differs, blank otherwise — the two coincide at rest,
so only `▶` shows (#67, amending ADR-0014's earlier two-column, two-mark description below). It carries no third
mark for *chosen but not loaded* and none for a fetch in flight — the right-hand column is unread counts and nothing
else, and a fetch is announced once, at the end of the content panel's title. A rail somebody is reading should hold
still.

That one announcement is the only thing in the shell that animates, and it is laid out so that nothing around it moves:
2 columns straight after the content panel's trail, a star spinner turning a frame every 400ms through ten and
starting over (#281), and nothing at all until the first tick — so the cached case above never flashes a mark (#213).
Whether a fetch is in flight keeps its meaning and its two jobs, gating `g` and the follows paging; what changed is only
what the breadcrumb draws. It is a **count** of questions in flight rather than a flag, because two enquiries overlap
readily — a boost sent while a timeline is still loading — and the first to finish would otherwise say the shell was
idle while the second was still running.

The alternatives were built and measured — a cursor that moves free until `⏎` commits, a key per destination, a jump
list — and all cost one fetch against cycling's six *before* the settle rule, which is what closed the gap. They are on
the prototype branch (`SCREENS-C.md`) if the decision is ever revisited.

## The numbers

Settled in #28, and added to by #213. All four lengths of time live in one place in the code (`ShellTiming`), so a
reader looking for them finds them together.

| What | How long | Why that |
|---|---|---|
| Settle window | 250ms | Long enough that a deliberate double-tap lands as one move; short enough that a single tab does not read as a pause. |
| Destination cache | 1 minute | Long enough that walking out along the rail and back is free; short enough that a timeline left and returned to a minute later is fetched rather than remembered. This client forgets a destination early when it is the thing that changed it — a post published, deleted or marked, or the public profile saved while it carries one of the own account's posts. |
| Countdown step | 1 second | The unit a rate-limit countdown counts in. |
| Mark step | 400ms | How long one frame of the breadcrumb's spinner is held, and — since the mark waits for its first tick — how long a fetch runs before it is announced at all. The slowest rate a glance still catches moving: a second reads as a stall and 250ms as a machine in trouble. Deliberately not the countdown's second, which would say the mark counts something (#213). |
| Follow-list threshold | 2,000 | Below it a **Follow list** is held whole and narrowed live; at or above it the screen is browsed by reading on and offers no narrowing. Set by the shape of real accounts: a following count is bounded and a followers count is not — 877K followers is ~11,000 requests at 80 a page, which is not a list to promise a search over. |

One cache age for everything, rather than one per kind of destination. The question the cache answers is "is this still
the timeline I just left", not "is this still current", and that has the same answer wherever you left from.

## Open questions

1. ~~**Whether a theme can decline to set a background** and inherit the terminal's own.~~ Settled by #46: it cannot.
   **Reversed by ADR-0021:** it can, and every built-in does. Terminal.Gui 2.4's `Color.None` is written as `CSI 49m`.
   What follows is #46's answer, kept for the record.
   A `Terminal.Gui` attribute is a foreground/background pair with nothing in it meaning "whatever was there", and its
   `Attribute.Default` is a concrete white on black rather than a sentinel the driver reads as "leave it". So a
   background is always written down — the theme's own, or the built-in's — and a terminal that wants no colour is
   answered by not colouring anything (`NO_COLOR`), which is a different question and already settled above.
2. ~~**Where compose lives.**~~ Settled by ADR-0015: a screen on the stack, like everything else. A reply draws the
   first rows of what it is answering above the editor, which is the part of the split region that was worth keeping.
3. ~~**How long the settle window and the cache should be.**~~ Settled above.
