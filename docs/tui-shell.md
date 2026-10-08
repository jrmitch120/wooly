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
│Hashtag           ││                                                          │
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
╰──────────────────╯╰──────────────────────────────────────────────────────────╯
 Post: j/k | Refresh: g | Destination: tab | Group: ` | Read: ⏎ | …+10 | Keys: ?
```

| Region | Size | Holds |
|---|---|---|
| Rail | 20 columns including its frames, full height less the status row | Four **rail groups**, in colour the selected entry on `rail-current` and, while tabbing, the cursor's entry on `rail-cursor`; without colour `▶`/`▷` (ADR-0021), each its own titled panel, the group holding the cursor framed in `panel-border-active`, on the press rather than once the selection follows; each destination with its unread count; at the foot an `API` panel holding, with two or more profiles, the instance acted as (ADR-0020), then the budget as a gauge. On a terminal too short to frame every group the rail steps down (below) |
| Content | the rest of the width, full height less the status row | A panel titled with the breadcrumb (below), holding exactly one screen. Its rows start on row 1 |
| Status | 1 row, full width | The current screen's keys as `Does: key \| Does: key`, as many as fit and `…+N` for the rest; or a notice; or a confirmation; the quota again when the rail is hidden |

The content panel's title is the stack: the crumbs walked through in `muted`, the one stood on in `panel-title`, eliding
from the left so it always ends where you are, and the fetch mark, a star spinner, a space after it while a fetch
is in flight. The mark's 2 columns are held whether or not it is drawn, so it never moves the trail. The panel's edge is
always `panel-border-active`: it is the panel being read. Its rows are 58 columns wide at an 80-column terminal. That is
the width every screen must read well at: the 61 of ADR-0014 less the cell the gutter column gave up to the content
panel's left edge, and less the two the rail took when it widened to 20 so that a label and its count both fit (#272).

The rail needs 22 rows to frame every group (Timelines 6, Explore 4, Inbox 5, You 3, API 4), and an 80×24 terminal gives
it 23. Shorter than that, it steps down rather than clipping:

| Rows for the rail | The rail |
|---|---|
| 22 or more | Framed |
| 15 to 21 | Compact: each group's title as a heading row, no frames, the API panel as one gauge row |
| fewer than 15 | Compact, and scrolled to keep the cursor's group in view |

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

## Screens, and who owns them

A screen is a place in the stack, not a window. Entering one pushes, `esc` pops, and the breadcrumb is the stack.

| Screen | Reached by | Ticket |
|---|---|---|
| Feed — home, local, federated, the rail's own hashtag | A rail destination | #28 |
| Hashtag — a tag walked to, not the rail's own | A search result, `⏎` on a `#hashtag` typed into search, or `⏎` on a picked hashtag reference | #29, reference #65, direct #305 |
| Post — the post whole, its ancestor chain above and its replies below | `⏎` on a feed item, or on its address typed into search | #28, ancestors #72, direct #306 |
| Account — who they are, what they are to you, their pinned posts and their posts, or their posts and replies | `a` on a feed item or inside a post, `⏎` on a handle or profile address typed into search, `s` to swap runs | shell #28, tie actions #29, the person #164, pinned #172, replies #229, direct #306 |
| Follows — everyone an account follows, or everyone who follows it | `w` on an account, `s` to swap sides | #165 |
| Discover — who to follow, in sections by why | A rail destination | #171 |
| Search — prompt and results | A rail destination, or `/` | #29, moving between kinds #166 |
| Notifications | A rail destination | #29 |
| Direct messages — conversations, then a thread | A rail destination | #30 |
| Follow requests | A rail destination | #29 |
| Compose / reply / edit — a screen on the stack, like any other | `c`, `r` or `e` | #28 |
| Attach — the file browser, the folders and the files the instance accepts | `ctrl-o` on a compose or a reply, `⏎` on its Media header or a click on its words | #376 |
| Media — the attachments screen, a row for each thing attached to a compose or a reply | `⏎` on the Media header, or a click on its line, where a short terminal folded the rows into it | story 58 |
| Profiles — every profile on this machine, marked `acting as` and `default` | `ctrl-p` | #240 (ADR-0020) |
| Add a profile — the instance, a sign-in through the browser or a pasted token, the token checked, a name | `a` on the profiles screen, or launching with nobody to act as | #245, #247 (ADR-0020) |
| Media inside a post or feed item | Drawn in place | #31 (ADR-0016) |

Every screen owes three things: it reads at 58 columns, it says what its keys are on the status row, and it names roles
rather than colours (below).

## Keys

A key may mean different things on different screens — `d` dismisses a notification and deletes a post — which is
workable only because the status row always shows the current screen's keys. What may **not** vary is the frame:

| Key | Everywhere |
|---|---|
| `esc` | Up one level of the stack. Never quits. |
| `ctrl-q` | Quit. |
| `?` | The keymap for this screen. The spec has no in-app help story; this is the shell adding one, and #28 carries it — every other screen inherits it for free. |
| `tab` / `shift-tab` | Moves the cursor (`▶`) at once, one destination down or up the rail, crossing from one rail group into the next. The selection follows it (`▷` while it lags behind), and that destination loads, once the tabbing has stopped for ~250ms. |
| `` ` `` / `~` | Moves the cursor to the first destination of the next or previous **rail group**, wrapping. Settles and loads as `tab` does (ADR-0021). |
| `/` | Search. Goes to the search destination; what it opens onto is #29's. |
| `ctrl-p` | The profiles screen, pushed onto the stack. On compose it takes the draft off first and opens the screen in its place, asking first where the draft is touched (ADR-0020, #373); it is not listed there. |

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
| `x` | Show what the post is hiding | Its warned text and poll, its sensitive attachments and link preview, or both halves at once (#113, #116, #119). What it showed belongs to the screen it was pressed on (#121, below) |
| `←` `→` | Walk the references (hashtag, mention, link) inside the picked post | Clamps at the ends; `esc`/`j`/`k` clear the pick; `⏎` opens what's picked (below) |
| `1`-`9` `0` | Toggle the 1st-10th option of the picked post's poll | Only where the poll is on screen and would still take a vote — no-op otherwise, and off the status row there too; `esc`/`j`/`k` discard the toggle |
| `v` | Cast the toggled poll vote | **Confirmation required** (story 43), same as delete. Only where the poll is on screen and would still take a vote — and on a poll already voted in or closed, says which rather than asking |

Screen-local, and deliberately colliding with the above because they are never on screen together:

| Screen | Keys |
|---|---|
| Account | `F` follow/unfollow · `M` mute/unmute · `B` block/unblock — capitals, so a lower-case mark key can never fire a tie by accident · `w` follows, which is everyone they follow with `s` a keypress from everyone who follows them · `s` posts and replies, or back to posts alone, in place · `[`/`]` section, on an account carrying a pinned run |
| Follows — following or followers | `f` filter, on a list under the threshold · `s` swap to the other side, in place · `⏎` open that account |
| Search results | `[`/`]` section, where two or more kinds found something |
| Discover | `F` follow/unfollow · `d` dismiss, one-way · `[`/`]` section |
| Notifications | `d` dismiss one · `D` clear all |
| Follow requests | `a` accept · `x` reject |
| Direct messages | `⏎` open the conversation · `m` mark read — `m` again inside the thread, where a reader who has just read it is most likely to press it |
| Conversation | `m` mark read, and every key that acts on a post, since each message in it is one |
| Profiles | `⏎` act as that profile, for this session — not offered on the one already acted as · `D` make it the default, for the CLI and the next launch — not offered on the one already the default · `a` add a profile · `R` sign it in again, replacing its token — offered on every row · `x` remove it, after a confirmation — refused on the one acted as and on the default |
| Add a profile | `⏎` on to the next step · `t` paste a token instead, while the browser is out or after it failed · `esc` back to the list, calling off a sign-in or a check in flight — or, as the only screen on first run, back to the first step, with `ctrl-q` quit offered (#247) |
| Compose / reply / edit | `ctrl-s` send or save — waiting first on anything attached still going up, which `esc` calls off (#375) · `esc` throw it away — asking `Discard this post? y / n` first where it differs from how it opened, as every way out of it does (#373) · `ctrl-w` move the typing between the post and the content warning over it — on all three, each carrying a warning field of its own (#123, #139, #140); `⏎` in the warning hands the typing back too (#320) · `ctrl-v` or `alt-v` attach a picture or copied files from the clipboard on a fresh post or a reply, from any field, Media and its rows included, else paste text as before — `alt-v` because Windows Terminal and the console host keep `ctrl-v` as their own paste (#380, ADR-0015); the status row offers `ctrl-v/alt-v paste` on Media while there is room · `ctrl-o` opens the file browser over a fresh post or a reply, from any field (#376) · `↑` on the post's first line moves the typing up into the headers, `↑`/`↓` move it between the headers that take typing, and `↓` off the last returns it to the post, the way a mail client's do (ADR-0024, #337); the walk stops at either end rather than coming round — `↑` on To and `↓` below the post do nothing — and the status row offers only the ways it goes: `↓ field` on To, `↑↓ field` on the headers under it, `↑ field` in the post · on **Media**, which the walk stops on between the warning and the post on a fresh post or a reply, `⏎` opens the file browser, as a click on its words does (#376) — or, where a short terminal folded the rows into its line, the attachments screen listing them (story 58) — and `s` flips the sensitive toggle once anything is attached, as a click on it does, unless a warning holds it on (#379); other letters there are nobody's · the walk goes through Media's **rows** too, `↑` from the post reaching the bottom one first (#378): on a row `del`/`backspace` take it off, `ctrl-z` brings back the last one taken off (one deep, only on Media and its rows), `shift-↑`/`shift-↓` move it, `r` retries it where a retry can help, `s` flips the toggle as on the header, and `⏎` opens the description editor, where `esc` and `ctrl-s` are both done (#377) · on **To** `←`/`→` choose the visibility, skipping any it does not allow, and the status row offers `←→ choose` ahead of the walk (#338); letters there are nobody's · on **Lang** a code or a name typed opens the list of languages, as do a click and `⏎`; the walk goes To, Lang, the warning, Media, the post — the order they are drawn in — and steps over Lang where a short terminal has given its row up (#340) · `tab`/`shift-tab` are the frame's here as everywhere, never a walk of compose's fields. While the list of people to mention is open: `↑`/`↓` pick · `tab`/`⏎` insert · `esc` close the list, never the draft (#318). While the list of languages is open: `↑`/`↓` pick · `tab`/`⏎` choose · `esc` close the list, never the draft — and the status row offers those three ahead of the rest (#340) |
| Attach | type to filter, fuzzily (#376) · `space` choose the file under the cursor, or let it go — up to what the post has room for · `⏎` attach what is chosen, wherever the cursor is, else the file under the cursor, else open the folder under it · `→` open a folder · `←` up a folder · `↑`/`↓` walk the list · `ctrl-a` every file, and again only the accepted types · `backspace`/`delete` only ever the filter, stopping at empty · `esc` clear the filter, then back to the draft unchanged |
| Media (the attachments screen) | `↑`/`↓` walk the rows · `⏎` describe the row · `del`/`backspace` take it off · `ctrl-z` bring back the last one taken off · `shift-↑`/`shift-↓` move it · `r` retry it where a retry can help · `s` the sensitive toggle · `ctrl-o` the file browser, attaching onto the compose under it · `ctrl-v`/`alt-v` attach from the clipboard onto it · `esc` back to the draft (story 58) |
| Home, local, federated, hashtag, Discover, Notifications, Messages, Requests, Post, Account, Follows | `g` refresh — evicts the destination's cache entry (where one exists) and re-runs the same fetch its own arrival runs |

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
  walks needs one of them to be there, and no widening of `PostKeys` reaches these: `d` and `D` on an inbox with
  nothing waiting, `⏎` and `m` with nobody writing, `⏎`, `a` and `x` with nobody asking, `⏎` on a search that found
  nothing. Each key says so where it is declared (`KeyHint.NeedsAPick`) and `Screen.Keys` drops them while the walk is
  empty, so this too is inherited rather than remembered. What stays is `j`/`k` and `↓`/`↑` — an empty screen shows its
  emptiness, which is explanation enough for a walk that does not move, and a row cut back to `g tab ?` reads as broken
  rather than as empty — and `g`, which on an empty screen is the one key that still does something, the thing it does
  being to fill the screen. Empty means *a list with nothing on it*: a compose editor, the keymap and a notice walk
  nothing at all and are untouched, and the search prompt drops its arrows while it is taking letters for that reason
  rather than this one.
- **A notification is not the post it is about.** `d` dismisses by the notification's own id; every other key on the
  row acts on the post it carries, so a mention can be answered without leaving the inbox. A follow carries no post,
  and picking one leaves those keys with nothing to act on rather than guessing — which is why they are off its row.
- **`D` asks first.** Emptying the inbox takes away a list nobody has necessarily read and nothing brings it back, so
  it is confirmed on the same terms `post delete` is — the same confirmation, saying `clear` rather than `delete`.
- **A count and the list under it are one fact.** Arriving at a destination sets its badge from the same answer the
  screen is drawn from, and dismissing or answering something moves both.
- **A prompt taking letters takes `/`, `?`, `` ` `` and `~` too**, which is the one exception to the frame keys above.
  A web address and a question are both things somebody is entitled to search for, and a prompt that could not take a
  slash would refuse the query most likely to be pasted into it; code in backticks and a path under `~` are the same
  (#265). Every other frame key — `esc`, `ctrl-q`, `tab` — still means what it means everywhere, and the status row
  says what the prompt answers to.
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
- **A hashtag a search found opens as a screen on the stack**, not as the rail's hashtag destination. Which tag the
  rail keeps a place for is a setting the reader wrote down, and a search result is not them changing their mind.
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
- **And, since #164, on something that is not a post at all**: the account screen's header block, whose **Bio** and
  whose **Custom field** values carry references of their own. `Screen` stops deriving them from a `Post?` and asks the
  picked thing what it carries, with today's post implementation as the default. A bio is the first source here that is
  not a post and will not be the last, which is why the generalisation was made rather than a second path added.
- **A mention in a bio is drawn and never walked.** `⏎` on a mention resolves off `Post.Mentions`, and a bio carries no
  such list; a bare `@maria` resolved against the reader's own instance opens whoever *their* server has by that name,
  which is the failure this section already warns about below. Hashtags and addresses need no resolution and walk
  normally, which is what makes a verified link on somebody's profile reachable — the most useful thing on the screen.
  The precedent for drawn-but-unwalkable is an `Image` attachment and a link preview's author name.
- **Walkable**: hashtag, mention, and address — the three references `BodyText` finds — followed, since #109
  (ADR-0017), by every `Video`, `Animation`, `Audio` or `Unknown` attachment on the post, in attachment order, and
  since #116 (ADR-0018) by the post's link preview, last of all. **Not walkable**: an `Image` attachment, which is
  drawn or linked but never opened this way; the name a link preview says wrote the page, which is plain text and
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
- **`←/→` is announced before the walk starts too**, wherever the picked thing has a reference to walk to — the
  same `References` the walk itself asks, so the hint shows exactly where the arrows would act. Otherwise the only
  way to learn the walk exists is to press it. Ranked right behind `⏎:read`, so it never pushes the key used most on
  a post off the row; where there is no `⏎:read` (inside a post, the account header, a conversation) it goes in
  front (`PostKeys.BeforeAReference`). On an 80-column feed that leaves it in `…+N` and `?`, which is accepted.
- **`⏎` does four different things, refusals share one notice** (#85, #109). Which of the four is the role the
  reference draws in, since that vocabulary already tells them apart. A hashtag opens exactly the way a search result
  for one already opens — a `Tag` **Subject** brought up by `Arrival.Open`, which `SearchScreen` reaches through
  `Reach.Open` — same `FeedScreen`, same breadcrumb, no new screen type, and the rail's own
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
- **Lives on nine destinations**: the four cached feed destinations (home, local, federated, the hashtag), plus
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
  arrival already follows.
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
- **`j` and `k` reclaim a selection that has scrolled off screen.** With no row of the selected post on the page, the
  next `j` or `k` selects the topmost post on the page instead of moving from a post the reader can no longer see;
  pressing it again moves normally from there. A post whose top has scrolled off but which still has rows showing *is*
  the topmost post, so `↓ ↓ ↓ j` picks out the post being read rather than the one after it — and a page scrolled past
  the last post entirely, which is the blank under it, reclaims that last post rather than nothing.
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
- **`PgUp`/`PgDn` walk the screen, `Home`/`End` walk the selection.** A page is a screenful of rows, because that is
  what a page is: somebody asking for the next one is asking about what they are looking at, not about how many posts
  happen to be on it. They used to move the selection by ten posts, which on a feed with pictures on it was several
  screens at once. The ends of a list are things rather than places, so `Home` and `End` still pick out the first post
  and the last, and neither of the four reclaims anything — a reader asking for the top of the list is not asking about
  the page they were on.
- **`[` and `]` move the pick between headed runs, and bring the run's heading with them** — but only when that
  heading is not already on the page (#166). `Scroll.To` moves the minimum needed to show the selection, so an unaided
  jump downwards lands the picked row on the bottom row with the section just left still filling the screen, which
  reads as a press that did nothing. Always anchoring the page on the heading fixes that and settles cleanly, but on a
  screen whose runs all fit at once it scrolls the first of them off the top for nothing. So the page moves only where
  it has to. The mechanism is a heading mark on `Line` and one function beside `Scroll.To`, which keeps the property
  the rest of this section rests on: a scroll answer is computed from the rows alone.
- **`[`/`]` reclaim, like `j`/`k`, and clamp like everything else.** Working out "the run after this one" from a pick
  the reader can no longer see is the exact failure reclaiming exists to prevent, so after `↓ ↓ ↓ ]` the jump runs from
  the topmost item on the page. At the ends it clamps and never wraps: nothing in this shell wraps, and with three runs
  a wrap saves one press at the cost of an ambiguous position. A reader already among the last run presses `]` and
  nothing happens, which is the clamp saying so rather than the key being broken.
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
  role as the badge counting it on the rail.

### What a compose's own content warning settled

`r` on a warned post opened an editor with nothing written over it, so a reply to a warned thing went out bare unless
its author remembered to warn it again by hand — which Mastodon's own clients do not ask of anybody (#123, #139, #140):

- **A reply opens on the warning of the post it answers**, in a field of its own on the row above the editor. A reply
  to a warned post is usually about the warned thing, and an author who has to re-type the warning is one who sometimes
  will not. For a boost it is the warning of the post *inside* it — the post the reply targets, and the resolution
  `Shell.Compose` already makes for every other key.
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
  `PostDraft` to publish or a `PostEdit` to save, whole — and `Shell.Send` makes the one call, pops, and says `Sent.`
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
  tall (#382), its name cut with `…` to 32 columns, its kind (picture, animation, video, sound) muted, its size right-aligned and muted,
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
  compose's own rows, drawn as under the header, the first picked. Every key and click a row takes under the header it
  takes there — `⏎` or a click on the description describes, `del`/`backspace` or a click on `x` takes off, `ctrl-z`
  brings back, `shift-↑↓` or a drag moves, `r` or a click on `retry (r)` retries, `s` or a click on the toggle flips
  it — and `ctrl-o` attaches more, all of it changing the compose's own draft through the shell; `↑`/`↓` walk the
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
- **Each file goes up the moment it is attached** (ADR-0026): the shell sends it through `IPostAuthor.Attach`, as the
  profile acted as, and feeds where it has got to back into the screen on the drawing thread — the screen holds it and
  draws it, and reaches nothing itself (ADR-0015). The calls are `AttachmentCalls`', the shell's collaborator for
  them, which hands every answer back as an event. Not through the enquiry: the row says how it is going, the status
  row says nothing, and it is not stopped by a screen pushed over the draft. **Every upload ends**: a refusal says the
  instance's own reason on the row; a dropped connection says `connection lost`, a rate limit `rate limited`, an
  instance that failed to answer `instance failed (502)`, and a call the client gave up waiting on `timed out`; and a
  failure nothing expected ends the row refused all the same, saying what it was — no row is left going up, and no
  send left waiting on one. None is tried again by itself (ADR-0006), though the author can retry from the row where
  a retry can help (below). A compose screen leaving the stack calls off what it is still sending, and leaves what
  went up for the instance to clear away.
- **`ctrl-s` waits rather than refuses.** With anything still going up or being processed it keeps the screen up and
  says `Will send once 1 attachment finishes — esc to stop.`, and sends once they have; `esc` calls the send off and
  says the draft is as it was. It never sends without them. With anything refused it does not send, and says so. The
  post goes out through `IPostAuthor.PublishAttached`, naming the pending attachments by id in the order shown — the
  route every TUI post now takes, attachments or none — and a post of attachments alone, with no text, sends.
- **The limits are the instance's** (`IInstanceLimits`): `configuration.statuses.max_media_attachments`, else 4;
  `configuration.media_attachments.supported_mime_types`, else Mastodon's own; and `description_limit`, else 1500 — read
  with the post's own limit, on the same answer.
- **The rows are walked to, and fixed where they are** (#378). `↑` from the post walks the rows from the bottom, then
  the header, then Warn, and `↓` walks back; a row walked to carries `▌` in `selection` against its grip and its name
  in `selected-text`, the bar in a column held for it so that nothing moves. On a row `del` or `backspace` takes it off
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
  rows about after a click as the pointer passed over them. The attachments screen's rows drag the same way.
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
  keep what was typed; there is no cancel. A description can be written at any time, while the file is still going up
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
  folder Wooly was launched from. Nothing is saved: the next session starts from the launch folder again. The shell
  reads each folder (`LocalFiles`) and hands the screen a `FolderListing`; the screen reads nothing itself, as no
  screen does (ADR-0015), and says which folder `→`, `←` or `⏎` opens next.
- **Rows, top to bottom:** the folder, `~` for the home folder and cut from its start where it is long, with
  `2 chosen · 2 more fit` (or `4 more fit on this post`) against the right; the filter, `filter ss0229▏` or
  `type to filter`, with what is listed and the key that changes it against the right — `pictures, video, sound ·
  ctrl-a every file` from the instance's accepted types, or `every file · ctrl-a accepted only` — a click on which does
  the same as the key; a rule; then `◂ ..`, the folders as `▸ name/` in `link`, and the files, each `☐`, or `☑` once
  chosen, with its kind, size and date muted against the right. The selection bar `▌` sits against the box column,
  so it has something beside it on every row. Hidden files and folders — a leading dot, or marked hidden — never show,
  and every file shown under `ctrl-a` that the instance does not accept is muted.
- **The filter is fuzzy, fzf's way** (`FuzzyName`): the letters typed must all be in a name, in order, anywhere; letters
  in a run, or starting a word or a number, score more and skipped letters cost a little, so `ss0229` puts
  `Screenshot 2024-02-29 at 9.31.03 AM.png` first and loose matches still show lower down. `..` stays on top, folders
  ahead of files, and the cursor lands on the closest match. Fuzzy so that `space` can always mean choose: a name's
  spaces never need typing. `backspace` and `delete` only ever edit the filter — held down, they stop at empty rather
  than walking up the folders — and `esc` clears it before it goes back.
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
- **The mouse**: a click moves the cursor, a double click opens a folder or attaches a file, and the wheel scrolls the
  list — the arrows walk the list (`Verb.NextEntry`, `Verb.PreviousEntry`), as `j`/`k` walk posts elsewhere, `j` and
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

Typing `@` at the start of a word in the post opens a list of people to mention under it (#318, #313):

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
- **It takes the pointer too, only while it is open (#335).** A click on a person is `tab` on them; a notch of the wheel
  over it moves the pick a person at a time, stopping at either end, and scrolls; a right click on it is nothing (#307).
  A click anywhere outside it closes it as `esc` does, and is spent on that, from the press to the click: the caret
  stays where it was and nothing under the pointer is clicked. So it is asked about ahead of every view, where the
  terminal's mouse events arrive.

### What compose's Lang settled

- **Lang is a one-line field under To (#340)**, laid over its value column as the warning field is over the warning's.
  It shows a language as its code and its own name, `fr  Français`, the name muted on the painted row. Empty, it says
  `none · the instance decides` and sends no language.
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
  and that is fine, because the screen scrolls and one `j` puts the posts on screen. A truncated bio would be permanent
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
  replies in, a recent pinned reply can be too, and is drawn in the pinned run alone the same way.
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
  or above it the screen browses paged on demand — `j` past the bottom fetches the next 80 — **with no filter at all**,
  `f` off the status row and the count reading `240 of 877,000 read` (`Number.Of`, which is how this client writes
  every count).
  The rest of a held list arrives in **one further ask rather than 24**, which is where the implementation departs from
  #180's "80 a page": `IAccountRelationships.List` reads to a *limit* rather than from a cursor, so asking page by page
  re-reads every page before it — 25 asks would cost some 300 requests against the 25 the ticket budgeted. The standing
  call is still made a page of 80 ids at a time, that endpoint taking many ids and not unboundedly many.
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
  935. The row says `16 of 951 read — this instance offers no more` once the reading has finished short. Not where a
  notice above has already said why: a rate limit stops a read too, and a count blaming the instance's reach
  underneath that would contradict it and guess at a cause the notice already knows.
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
  It is "take me to the posts", and a reader who arrives at a post wants to press `⏎`, not `j` first.
- **Headed runs only, and the account screen's header block is not a stop.** The jump brings a run's heading with it,
  and the header has no heading to bring; `[` from the first pinned post clamps. Nothing is unreachable — `k` from
  there lands on the header, and `Home` always has.
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

The tenth rail destination, and the first the rail has ever grown (ADR-0014's amendment). Its one section today is who
to follow (#171):

- **It is a destination and not a key on the search screen.** What earns the entry is that the screen is sectioned, so
  a second kind of suggestion later is a heading rather than an eleventh rail entry — the cost is paid once now instead
  of once per kind. **There is no second door**: no `S` on the search screen, because Discover is an **Arrival** and
  the stack resets to one screen, and a place that was both a destination and a pushed screen is what CONTEXT.md's
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
  the feature was admitted for `j j j F F`, and a list that reflows under your fingers is the one thing that breaks it.
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
  `shift-tab`, `` ` ``/`~`, a click on the rail or on a crumb behind it, `ctrl-p` and `ctrl-q` all go through one
  check in the shell: where a compose screen the way out would take off differs from what it opened with — its text,
  its warning, To or Lang (`ComposeScreen.Touched`) — the confirmation row asks **`Discard this post? y / n`**, and
  nothing else happens until it is answered. Only `y` discards and finishes that way out; the way out's own key
  pressed again does not, so `esc esc` or `tab tab` never throws a draft away. Anything else keeps the draft and does
  nothing more — a stray second `tab` cannot finish what the first started. A step along the rail (`tab`,
  `shift-tab`, `` ` ``/`~`) that takes a draft off, asked or not, arrives at the destination stepped to at once,
  without the settle (ADR-0014 amended), so the screen under the draft is never shown in between; stepped onto the
  destination already shown, it walks back out to it as a click on it does. Tabbing on from there settles as ever.
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
  own file, through `Screen.Answer(verb, reach)` (#232). `Shell.Do` keeps the frame's verbs and the ones that act on the
  picked post of any screen — marks, reply, compose, edit, delete, vote, reveal, opening a reference or an author — and
  `m`, which reads across the conversation and DM screens; every other verb falls through to the screen. What a screen
  can do while it answers is **Reach** (CONTEXT.md): the ports and profile, one `Put` through the shell's **Enquiry**,
  `Open` and `Swap` of a **Subject** (#233), `Say`, `Confirm`, `Changed`, `Tell` of a **Change** (#234) and `IsMe` —
  and nothing of the stack, the rail or the cache. So the account screen's
  ties, `w` and `s` are in `AccountScreen.cs`; a follow list's `s`, `f` and `⏎` in `FollowsScreen.cs`; search's `⏎` in
  `SearchScreen.cs`; `d` and `D` in `NotificationsScreen.cs`; `a`, `x` and `⏎` in `FollowRequestsScreen.cs`; the
  conversation list's `⏎` in `DirectMessagesScreen.cs`; and Discover's `⏎`, `F` and `d` in `DiscoverScreen.cs`. A
  `Confirmation` carries what agreeing to it does, so `D` asks the same way a delete and a vote do.
- **The verbs that need a terminal.** `ShellWindow` still carries out `ctrl-q`, the four movements that walk the page
  rather than the list, `j`/`k`, `Home`/`End` and `[`/`]` — which move the pick *and* the page — the arrows that walk
  the entries of the file browser and the attachments screen likewise, and `ctrl-s`, which
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
| Wheel down / up | The content panel | The page moves one row per notch, the finest step a terminal has, so a trackpad's stream of small events glides rather than lurching three rows at a time (#292). Three notches are as far as one `↓` / `↑`, and it clamps at the same ends. **Picked** stays where it is, so after wheeling it off the page `j`/`k` reclaim the topmost thing on the page, as after the arrows. On help and notices too, which are content like any other screen. A notch is that arrow through the **Keymap**, with a row for its step, so with a confirmation open it declines it and scrolls nothing, as `↓` does (#287) |
| Wheel | The rail | Nothing: not the cursor, not the selection, not the rail's scroll and not the page beside it |
| Wheel sideways | Anywhere | Nothing. A trackpad drifting sideways sends these between vertical notches, and Terminal.Gui's left and right carry up's and down's bits, so they are asked about first and dropped |
| Wheel, click | The compose editor | Terminal.Gui's own handling: the wheel scrolls the draft and a click places the caret. Nothing of the shell's |
| Click | A value of compose's **To** | Chooses it and gives To the typing, so `←`/`→` carry on from there; a click on either arrow of the narrow form steps, and anywhere else on the row gives To the typing. A value To does not allow, and the whole row on an edit, ignores it. The wheel over the row does nothing (ADR-0024, #338) |
| Click | A person in the open list of people to mention | `tab` on them: their `@user` goes in place of the word and the list closes (#335) |
| Wheel down / up | The open list of people to mention | Moves the pick a person at a time, stopping at either end, scrolling the list where it holds more people than it shows (#335) |
| Click | Anywhere outside the open list of people to mention | Closes it, as `esc` does, and nothing more: the caret stays, the draft is as it was, and what is under the pointer is not clicked (#335) |
| Click | A destination on the rail | Arrives there at once, through the rail's immediate path: the cursor and the selection move together, there is no settle window, and any landing the tabbing left waiting is abandoned, so only the destination clicked is read (#288). Which entry a row is comes off the rail's own rows, each carrying its destination's place as its `Line.Item`, so the click lands on what is drawn under the pointer whether the rail is framed, compact or compact and scrolled |
| Click | The destination already shown | Walks back out to its own screen: drilled in from it, the stack goes back to its one bottom screen with the page and **Picked** it was left on, as any pop keeps, and nothing is asked of the instance — the way a sidebar's entry takes you back to its top page (#289). On its own screen already, nothing. With a touched compose drilled in it asks first, as every way out of one does (#373). The first crumb of the breadcrumb is the same click (#308). One of the two moves the mouse has that the keys do not: tabbing back onto the destination shown is still a walk that ended where it began |
| Click | A group's title, a compact heading, the API panel | Nothing. Only destinations answer a click |
| Click | A crumb of the breadcrumb, behind the one in front | Walks back to that screen in one move: everything drilled in above it comes off the stack, as that many presses of `esc` would take it off, and the screen is in front as it was left — its **Page**, its **Picked** and any **Reference** picked on it — with nothing asked of the instance. What the screens taken off asked for is dropped, as with any pop. The first crumb is the click on the destination shown above. Which crumb a column is comes off the breadcrumb's own spans, each carrying its place in the stack as its `Span.Item`, as the rail's rows carry theirs, so the click lands on the crumb drawn under the pointer however the trail is elided. A crumb whose walk back would take a touched compose off the stack asks first, as every way out of one does (#373); a crumb above a compose still walks back. Crumbs are not drawn as clickable. The other move the keys do not have (#308) |
| Click | The crumb in front, the `… ›` lead of an elided trail, a separator, the fetch mark, the edge after the trail | Nothing. Only a crumb behind the one in front answers a click |
| Click | A row of a thing in the content | Picks that thing, through the same pick `[`/`]` make: any row of it counts — a tall post's byline, its text, a picture on it — since which thing a row is part of is its `Line.Item`, the ordinal `Screen.Pick` takes. The remark on the status row goes, as it does for `j`/`k`, and a **Reference** walked to inside the thing is let go, so the `⏎` after it opens the thing rather than the reference. The page stays where it is rather than following the pick, so what was clicked never moves out from under the pointer. The same on every listing screen, the account screen's header block (after which the post keys go quiet, as after `j`/`k`) and a search's results in their sections among them (#290) |
| Click | A heading, a rule, a blank, the space under the last thing | Nothing. A click never guesses which thing was meant |
| Double click | A row of a thing in the content | That click, and then `⏎` through the **Keymap** on the screen in front, so it means whatever `⏎` means there: it opens a post, a search result, a conversation, the asker of a follow request or a person, and switches to a profile on the profiles screen. Where `⏎` means nothing for what is picked — the account screen's header block — it picks and does nothing more. The click lets a walked **Reference** go first, so a double click on a post with a hashtag walked to opens the post. Terminal.Gui's own detection says what is a double click (#291) |
| Double click | A heading, a rule, a blank, the space under the last thing | Nothing: never a `⏎` on what was picked before |
| Double click | The breadcrumb | Its first click's walk back and nothing more: never a `⏎` on the screen it landed on (#308) |
| Right click | Anywhere: the content, the rail, the breadcrumb, the status row | `esc`, through the **Keymap** on the screen in front, so up one level of whichever kind is open: it lets a picked **Reference** or an uncast poll toggle go before it pops the screen, declines a confirmation, clears the filter prompt's filter (as `esc` does, not as a left click does) and calls off a sign-in in flight on Add a profile. On a destination's own screen it does nothing, and never quits. Over the rail it does not arrive at the destination under the pointer, and over the breadcrumb it does not walk back to the crumb. Each right click the terminal reports is one `esc`, Terminal.Gui's double and triple included, so three quick right clicks walk back three levels. Only the terminal's right button counts: ctrl+click stays a left click, and the middle button means nothing (#307) |
| Right click | Anywhere, with compose in front | `esc`, wherever it lands — a field, the rail, the breadcrumb, the status row: over a touched draft it asks `Discard this post? y / n`, and a second right click agrees as a second `esc` does; over an untouched one it leaves (#373). Taken ahead of the view under the pointer, so the compose editor's own context menu never opens, and on the open list of people to mention it neither closes the list nor picks |

**Open questions win.** While a confirmation is on the status row or the filter prompt is open, a click anywhere is a
key the question does not take: it declines the confirmation, or closes the prompt with what was typed still narrowing
the list, as `⏎` would, and whatever the click was on is not carried out. With nobody to act as there is no rail to
click, and a click does no more than the keys there allow. Terminal.Gui reports a double click's first click on its own
before the pair, so a double click whose first click declined a question or closed the prompt is spent with it and
opens nothing behind it. A click on a crumb is one of these clicks too: it declines or closes the question and walks
nowhere. A right click is not one of these clicks but `esc`, and answers the question as `esc` does:
it declines the confirmation too, but takes the filter off rather than leaving what was typed narrowing the list.

Mouse tracking stays on, and nothing turns it off: drag means nothing, and selecting text to copy goes through the
terminal's modifier bypass (`⌥` in iTerm2, `Fn` in Terminal.app) until selection is a feature of its own. The `?`
screen says what the mouse does beside the frame's keys, ending `right click back` and carried on to a second line with
no key of its own, `click a crumb to walk back to it`, since one line no longer fits beside the key column at 80 columns.

## Starting it, and the one destination that needs configuring

`wooly-tui` takes one option, `--profile <name>`, and it means what it means everywhere else: act as that profile for
this run, without changing which one is the default (story 9). Everything else about the profile — which instance, which
token — is resolved through `IProfileRegistry` exactly as a command's scope resolves it. With nobody to act as, it opens
on adding a profile ("What first run settled", above).

Nine of the ten destinations are the same nine for everybody. The odd one out is a hashtag, and which one is nobody's
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
glyph or a position that carries the same meaning when colour is gone — all but `selected-text`, which has neither and
is drawn reversed instead (#316).

| Role | Paints | Carried without colour by |
|---|---|---|
| `body` | A post's text | — |
| `hashtag` | A tag inside a post's text | the `#` |
| `mention` | An account named inside a post's text | the `@` |
| `link` | An address inside a post's text | the scheme |
| `muted` | Timestamps, counts nobody acted on, hints, a status row key's explanation, the row's `…+N` overflow mark, and the crumbs walked through on the content panel's title — the `›` between them and the `… › ` a long trail leads with included | position, and `…` on the mark |
| `byline-name` | A display name | position |
| `byline-handle` | `username@instance` | the `@` |
| `audience` | The visibility mark | `○ ◌ ● ✉` |
| `content-warning` | A warning and its text | `⚠` |
| `media` | Image placeholders, attachment links, a link preview's title, and the columns a byline holds for an avatar | `▒▒▒▒`, `⏵` |
| `stand-in` | A drawn picture's box while the picture is not here: the shaded fill standing in for it, at its size, under the blur where the instance sent a blurhash (ADR-0025, #349) | `░` |
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
| `rail-unread` | An unread count, and the word on an unread conversation | the number, and the word |
| `quota` / `quota-low` | Rate-limit budget left, and nearly spent; `quota` also the instance above it, with two or more profiles | the number |
| `gauge` / `gauge-empty` | The API budget's filled and empty cells | `█` and `░`, and the percentage |
| `chrome` | The frame's furniture: the status row's leading space and ` \| ` separators | position |
| `key` | A key you press: the status row's, the help screen's key column, a confirmation's `y` and `esc` — never prose that names a key, never the padding beside one | position — last in its pair, after the words' colon; first column on the help screen |
| `panel-border` / `panel-border-active` | A panel's frame, and the frame of the panel you are in: the content panel, and the rail group holding the cursor | the box characters; which group is active is carried by the cursor's entry's band in colour, and by `▶` on it without |
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
| Destination cache | 1 minute | Long enough that walking out along the rail and back is free; short enough that a timeline left and returned to a minute later is fetched rather than remembered. This client forgets a destination early when it is the thing that changed it — a post published, deleted or marked. |
| Countdown step | 1 second | The unit a rate-limit countdown counts in. |
| Mark step | 400ms | How long one frame of the breadcrumb's spinner is held, and — since the mark waits for its first tick — how long a fetch runs before it is announced at all. The slowest rate a glance still catches moving: a second reads as a stall and 250ms as a machine in trouble. Deliberately not the countdown's second, which would say the mark counts something (#213). |
| Follow-list threshold | 2,000 | Below it a **Follow list** is held whole and narrowed live; at or above it the screen browses a page at a time and offers no narrowing. Set by the shape of real accounts: a following count is bounded and a followers count is not — 877K followers is ~11,000 requests at 80 a page, which is not a list to promise a search over. |

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
