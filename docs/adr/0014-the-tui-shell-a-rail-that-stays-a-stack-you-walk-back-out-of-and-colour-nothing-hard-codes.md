# The TUI shell: a rail that stays, a stack you walk back out of, and colour nothing hard-codes

The TUI (#28) is the first surface in this project where the shape of the thing has to be decided before any of it can
be built, because whatever the shell says about reaching a second screen is repeated by every screen #29, #30 and #31
add. Eight working shells were built to answer it (`src/Wooly.Tui.Prototype`, on its own branch, out of `main`), and the
four decisions below come out of reading them rather than out of arguing about them.

**The shell is a rail that never leaves, a feed with the rest of the width, and a stack you walk back out of.** Down
the left is a fixed 18-column rail of destinations — the four timelines, notifications, direct messages, follow
requests, search, the profile's own account — each carrying its own unread count, with the rate-limit quota (story 54)
at the bottom of it. Everything else is content. A post or an account is not a pane beside the feed and not a modal over
it: it is somewhere you *go*, by pressing enter on a list item, and somewhere you come back from by pressing escape,
with the trail along the top saying where you are (`home › post by @ben › @ben@hachyderm.io`).

Two shapes were rejected on evidence rather than taste. A modal over the timeline is the cheapest thing to build and the
most familiar, and it hides the timeline every time it is used — with #29 and #30 adding six more screens, the shell
would spend most of its life covered up. A second pane beside the feed reads beautifully at 120 columns and starves the
feed at 80: the prototype's three-column arrangement left the feed 37 columns, which wraps a post every five or six
words. The rail alone costs 19, so the same terminal leaves the feed 61 — a rail is affordable and a rail plus a context
pane is not. What the context pane held (who wrote this, where you stand with them) is exactly what the account screen
holds now, one keystroke away, at full width.

**Tab moves a cursor along the rail at once; the selection follows it — and the fetch goes out — when the tabbing
stops.** Three other mechanisms were prototyped and measured — a cursor that moves for free until enter commits it, a
key bound to each destination, a jump list taking a name — and against a fake instance answering in 450ms, all three
cost one fetch where cycling, selecting on every keypress, cost six with five discards on the walk from Home to Follow
requests. Cycling is kept, because the six turns out to be a property of *when a press is acted on* rather than of the
interaction: a run of tab presses is one navigation, not six, and only the destination somebody stopped on was ever
wanted.

So the rail separates the two things a keypress used to do at once. The cursor moves on every press, immediately,
because a key that draws nothing for a quarter of a second reads as lag however much work it is saving. The selection
moves only when the presses stop, on a settle window (250ms in the prototype) that every press restarts and abandons
the one before it — and moving the selection is what asks the instance for anything. The same six tabs are six cursor
moves, one selection, and one fetch, which is what every other mechanism costs.

The rail therefore carries two marks and never needs a third: one for where the tabbing has got to, one for what is
selected, both in the same column on the left where the destination names already are. There is no marker for *chosen
but not loaded* and none for a fetch in flight — the right-hand column stays unread counts, and that a fetch is
happening is said once on the breadcrumb, beside the content it is about to replace. A rail somebody is reading should
hold still. Two smaller things still follow: an answer overtaken before it lands is discarded rather than drawn, and a
destination is worth caching briefly so that walking out along the rail and back is one fetch per destination rather
than one per arrival. The rate-limit quota on the rail earns its corner here more than under any of the other three,
because this is the mechanism that could spend it by accident.

**No view builds a colour.** Nothing in the TUI constructs a `Terminal.Gui` `Attribute`, names a `StandardColor`, or
holds a palette of its own — a view says which *role* the thing it is drawing plays (a byline's name, a handle, a
timestamp, a content warning, a boost mark, an unread badge, the selected row) and the current theme answers with an
attribute. The prototype does the opposite, with a static `Ink` class of hard-coded pairs, and it is worth saying why
that has to go rather than merely that it does: a hard-coded pair cannot be themed, cannot degrade, and cannot be
tested. Role selection is the only part of rendering that is assertable without a terminal — *this post is mine, so its
delete affordance takes the destructive role; this conversation is unread, so its badge takes the unread role* — and
that is precisely the part where a mistake shows up as the wrong thing being emphasised on somebody's screen. Terminal.
Gui's own `Scheme` is the *output* of resolving a role, not the vocabulary: its roles (`Normal`, `Focus`, `HotNormal`,
`Disabled`) describe what a widget is doing, and none of them describe what a boost is.

**Colour is never the only thing carrying a meaning.** Every state the TUI shows has a glyph before it has a colour:
`○ ◌ ● ✉` for the four audiences, `⚠` for a content warning, `↺` and `★` for the two marks, `▌` for the selected row,
the word `fetching…` on the breadcrumb for a fetch in flight. This is not decoration. `Terminal.Gui` reports a terminal as `ColorCapabilityLevel.
NoColor` when `NO_COLOR` is set or `TERM=dumb`, and a shell that says "this post is boosted" only by turning a number
green says nothing at all there — the same nothing it says to a reader who cannot separate that green from that grey.
Colour makes the glyphs faster to scan; it is never asked to carry a fact by itself.

**A theme is part of Wooly's own TOML config, not a second configuration system.** `Terminal.Gui` ships a
`ConfigurationManager` with its own themes, read from JSON at conventional paths (`~/.tui-config.json` and friends). It
is not adopted: story 5 promises one human-readable TOML file holding this client's non-secret configuration, ADR-0003
put it at the OS-conventional path, and a second file in a second format that also restyles the app would make "where
do I change this" a question with two answers. So `ConfigurationManager` is enabled for the library's hard-coded
defaults only, which also means a `~/.tui-config.json` left behind by some other Terminal.Gui application cannot quietly
restyle Wooly. Themes are `[themes.<name>]` tables in the existing config file, a theme is chosen by name, and two are
built in — one for dark terminals, one for light. Colours are written as hex or as one of the sixteen ANSI names; the
driver quantises hex down on a 16-colour terminal, so a theme does not have to be authored twice.

**None of this can draw until `Post` grows.** A timeline cannot render a lit star, an unlit one, or an image, because
today's `Post` carries neither the viewer's own state — has *this* profile favorited, boosted or pinned it — nor any
media read back from an instance (`MediaAttachment` is the upload side only). The prototype models the difference as a
`FeedItem` wrapper to make the gap explicit. Widening `Post` belongs to #28 as the first thing it does, and #31 is
blocked on the same widening, not on a rendering decision.

## What this means for the tickets

The concrete contract — regions and their sizes, the keymap, the role table, the theme file's shape, and which screen
belongs to which ticket — is `docs/tui-shell.md`, so that #28, #29, #30, #31 and the theming ticket can each be written
against something enumerable rather than against this prose.

Testing follows ADR-0005's rule that the highest-value seam wins. Role selection and screen state are behaviour a test
can assert with no terminal in the room; drawing is not, and stays manually smoke-tested as the spec says. One thing has
changed since that was written, though, and is worth recording where somebody will find it: `Terminal.Gui` v2.4 ships
`IApplication.GetInputInjector()` and a virtual time provider, and the prototype uses them to drive a shell and capture
its screen with nobody at the keyboard. That does not make pixels worth asserting, but it does make "this key on this
screen leaves the app in that state" cheap enough to reconsider when the shell settles.

## Amendment: three things this ADR said, tightened by driving the shell (map #61)

The frame this ADR set stood up to a round of use — a rail, a stack, roles instead of colours. Three places in it
turned out to be one clause too narrow once real posts, real polls and a real keyboard were driven through it. Each
is a correction to wording this ADR already committed to, not a change of shape; `docs/tui-shell.md` carries the
enumerable detail, this records what changed and why.

**`esc` gains a first job.** This ADR said `esc` is up one level of the stack, full stop. Walking the references
inside a post's text (ticket #64) needed a mode of its own — a picked hashtag, mention or address, distinct from the
picked post itself — and `esc` was the obvious key to leave it with. So the rule is now "up one level, of whichever
kind of level is currently open": a reference pick is a level below the screen it lives on, and `esc` clears the
nearer one first. Every other frame key is untouched, and a screen with no reference picked behaves exactly as this
ADR described.

**The rail carries one mark, not two.** This ADR said the rail "carries exactly two marks... both in the left column"
— one for the cursor (`▶`), one for the selection (`▸`) — and that a third mark, for anything else, was deliberately
left out. Driving it (ticket #67) found that the two coincide at rest and differ only for the ~250ms settle window,
so a reader looking at the rail almost always saw `▶▸` side by side where one glyph would do. The rail now spends one
column: `▶` on the cursor's row, its hollow twin `▷` on the settled row only while the two differ, blank otherwise.
The reasoning underneath is exactly what it was — no marker for *chosen but not loaded*, none for a fetch in flight,
a rail somebody is reading should hold still — only the count of marks changes, from two columns to one.

**The shell gains an action that leaves the terminal.** This ADR is silent on anything past the process boundary —
every port it describes (`ShellPorts`) reaches an instance, and nothing in the shell was ever asked to reach outside
it. Opening an address found inside a post (ticket #65, built as #85) is the first thing that does: `⏎` on a picked
reference that is an `http`/`https` link launches the platform's own browser. It is deliberately **not** folded into
`ShellPorts`, which this ADR scoped to "everything the shell reaches an instance through" — a browser launch reaches
past the instance entirely, so it gets its own small seam.

That seam turned out to be two things rather than one, and splitting them is what makes it testable the way role
selection is. `BrowserLaunch` is the decision with no process in it: which addresses are handed to a machine at all
(`http` and `https`, named — a scheme is the whole of what makes handing text to a shell dangerous) and what each
platform is asked about the ones that are (the address itself under `UseShellExecute` on Windows, `open` on macOS,
`xdg-open` on Linux). `IWebBrowser` — which already existed, because the OAuth sign-in has to send somebody to a
browser too (ADR-0004) — is the act. So "was the right platform call requested" and "was a hostile scheme refused"
are both asked of a value, on every platform at once, with no process ever starting; and there is one browser
abstraction in this codebase rather than a second one grown beside it.

None of this reopens what the ADR decided: the shell is still a rail that stays, a stack you walk back out of, and
colour nothing hard-codes.

## Amendment: the rail carries ten destinations, not nine (map #159)

This ADR settled a rail of nine places and `Destination.cs` records why all nine were listed from the start, before
four of them had a screen: "a rail that grows four entries later is a different rail." The people-side map (#159)
grows it by one — **`Discover`**, immediately after `Search` and in its group — and the reasoning is on the record in
ADR-0019 rather than repeated here. The short of it: a key on the search screen was the cheaper answer and lost,
because what makes the entry worth its cost is that the screen behind it is built in sections, so the *next* kind of
suggestion is a heading on it rather than an eleventh entry. The rail is paid for once instead of once per kind.

Everything else about a destination is untouched: `Discover` arrives the way the other nine arrive, resets the stack to
one screen, caches for the same minute in the same `DestinationCache`, and counts nothing unread — the same answer
Search gives, since nothing there is waiting for anybody.

Three places in the codebase and its docs enumerate nine and now enumerate ten: `DestinationKind`, `RailLines.Of`'s
group rules, and `docs/tui-shell.md`. CONTEXT.md's **Destination** term says which one grew and why, so that the next
person to propose an eleventh finds the bar it has to clear.

## Amendment: a pick is not necessarily a post, and a screen's sections are walkable (map #159)

Two more corrections of wording, in the same spirit as the three above.

**What `←`/`→` walk is asked of the picked thing, not of a post.** This ADR and the reference work under it assumed a
screen's references come from a `Post?`. The account screen's header block is the first thing that is picked and is not
a post (ADR-0019): a **Bio** and a **Custom field**'s value carry hashtags and addresses to walk, and the post keys go
quiet while the block is picked — inherited behaviour rather than new, since a follow notification already leaves them
with nothing to act on. `Screen` therefore asks the picked thing what it carries, with the post implementation as the
default.

**`[` and `]` move between the headed runs on a screen**, bringing the run's heading with them when it is not already
on the page, reclaiming like `j`/`k` and clamping at the ends. They are screen-local rather than frame keys — the
frame this ADR fixed is untouched — but they mean one thing on every screen that has two or more headed runs, and the
status row says `[/]:section` on all of them. The mechanism is a heading mark on `Line` and one function beside
`Scroll.To`, which keeps this ADR's property that a scroll answer is computed from the rows alone.

## Amendment: the chrome says where you are, and the status row says less (map #160)

Two complaints started this: the breadcrumb blends into the content, and the status row truncates. Both are about the
two rows this ADR called the frame and then never came back to, and both were answered by prototypes rather than by
argument (map #160, tickets #168, #169, #213, #214, #215). The frame is unchanged in shape — a rail that stays, a
stack you walk back out of, roles instead of colours — and four things it said are now said more exactly.
`docs/tui-shell.md` carries the enumerable detail; this records what moved and why.

**The breadcrumb tells where you are standing from where you walked through, and the content region starts a row
lower.** This ADR gave the trail one job — "with the trail along the top saying where you are" — and the code drew it
as one span, so it said nothing of the sort. The crumb at the end now takes a role of its own, `crumb-current`; the
ancestors stay `chrome`; the separator keeps no role, because the trail reads as structure by the end of it
brightening rather than by the separators dimming. A glyph before the current crumb and a band behind it were both
drawn and rejected — in this shell a band means *the thing you are on* (`selection`, `rail-current`), and banding the
whole row says the whole row is that. The no-colour case is carried by **position**, which the second change makes
load-bearing: a long trail now elides **from the left**, where `TextWrap.Clip` cut it from the right and so kept the
crumbs saying where you came from and lost the one saying where you are. The trail always ends where you are, on
every terminal.

The content region starting at row 2 rather than row 1 is a change to the regions this ADR fixed, and is the one
frame change on this map that was earned: a blank row divides the breadcrumb from the content the way every screen in
this shell already divides one thing from the next, it works where a band does not, and — unlike the second status
row refuted below — it answers a complaint that is true on *every* screen rather than on the busiest one. A
horizontal rule was rejected for asking a reader to learn that one line means a frame boundary and another, four rows
away on Discover, means a section heading.

**What divides one region from the next is drawn, in a role of its own.** This ADR gave the shell four regions and
said nothing about the cells between them, so nothing painted them and Terminal.Gui did — in the grey of its own
default scheme, which read as a border down the side of the rail and, once the content region moved down a row, as a
band across the top of the screen. Both are now `seam`: the blank row under the breadcrumb, and the column dividing
the rail from everything right of it. The column carries a `│` rule as well as a band, because a division made of
colour alone is no division on the terminals this ADR promised to serve — the row's is carried by its being blank,
which is what every screen already does between one thing and the next.

**The status row is a reminder and `?` is the reference, so the row may be incomplete but must never truncate.**
This ADR left the status row to `docs/tui-shell.md` and the doc left its behaviour when full to `TextWrap.Clip`,
which cut at the right — and `?:keys` is last in every list, so the row announcing where the cut keys could be found
was the first thing cut. The row now draws as many whole hints as it has room for in a rank order written on purpose,
then `…+N` for what it could not fit, then `?:keys`, which is never cut. A key that cannot act on what is picked out
is off the row and out of the count, which is this shell's own existing rule (#87, #119, #193, #195) applied
consistently rather than in four places.

**A second status row was asked for and refuted, on evidence.** The worst case is the account screen — twenty hints
wanting 217 columns against 80 — and two rows still need `…+3` there, so the second row does not solve the problem it
would charge every screen a row for. Pruning to a fixed set fits and wastes 37 columns on a quiet screen, which is
the *"a row cut back to `g tab ?` reads as broken rather than as empty"* failure #195 already named; grouping
(`b/f:marks`) saves 30 columns, still cuts 4 to 9 hints silently, and spends the key-to-word mapping #66 bought. The
fill rule is what the arithmetic leaves, and that the frame held under it is worth as much on the record as a frame
that moved.

**The one thing that animates is the one thing that says the shell is alive.** This ADR said a fetch is announced
once, on the breadcrumb, and that the rail holds still. That stands; the mark now moves — a dot every 400ms, three of
them, and start over, laid out at its widest so neither the word nor the trail beside it can shift. It waits one tick
before appearing at all, so a cached destination never flashes it. Two consequences reach this ADR's seams rather
than the doc's numbers. `IShellHost` keeps its two members: the tick is a one-shot `After` re-armed while anything is
in flight, exactly as the rate-limit countdown already re-arms one, so the host seam does not grow a repeating timer
for a thing one caller wants. And the tick raises an **event of its own** rather than `Changed`, because `Changed`
redraws the whole window — the rail, the content and every picture placement — which is precisely the rail this ADR
said should hold still, two and a half times a second for as long as any fetch is in flight.

**A confirmation reserves its answer before it draws its question.** The third thing the status row can hold had
never been measured: at 80 columns the delete confirmation drew 79 and the vote confirmation 80, so an instance whose
post ids are longer clipped `esc keep` and left a row that asks something and does not say how to answer. The rule is
the breadcrumb's, one row down — cut what the reader can reconstruct from what is on screen, never what tells them
how to act. The questions shortened on the same ground: the post id and the quoted poll option each name something a
reader cannot check, while `Role.Selection` and the ballot already say which post and which answers. Story 43's
requirement is the asking, not the wording.

**`chrome` was never one job, and the key you press gets a role.** This ADR's rule is that colour never carries a
meaning alone, and it is met throughout the above by glyph and position. The new claim is narrower and is the
standing test this map leaves behind: **a colour distinction is owed where a reader's next action depends on telling
two things apart.** `chrome` painted the frame's furniture *and*, through `KeyHint.Spans`, the key on the status row
— which after the fill rule is the most actionable token on it — so #66's split between a key and its explanation was
invisible in both built-in themes, and `HelpScreen` had already worked around it by drawing its key column in
`byline-handle` blue. One new role, `key`, paints a token you press in the three places one is drawn, and nothing
else moves: `quota`, `audience` and `muted` go on sharing one hex, because nobody has to tell a rate-limit number
from a visibility glyph to do anything. Adding a role is adding a public name to everybody's `[themes.*]` table, and
two were added here against five that were argued for and refused.

None of this reopens what the ADR decided. The regions gained a row inside the content's half of the frame and lost
nothing; every decision above was measured at 61 and 80 columns and drawn on a terminal reporting no colour.
