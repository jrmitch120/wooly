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

**Tab counts steps along the rail, and the highlight moves — and the destination loads — once the tabbing stops.**
Three other mechanisms were prototyped and measured — a cursor that moves for free until enter commits it, a key bound
to each destination, a jump list taking a name — and against a fake instance answering in 450ms, all three cost one
fetch where cycling, moving and asking on every keypress, cost six with five discards on the walk from Home to Follow
requests. Cycling is kept, because the six turns out to be a property of *when* a press is acted on rather than of the
interaction: a run of tab presses is one navigation, not six, and only the destination somebody stopped on was ever
wanted.

So a press advances a count and restarts a short settle window (250ms in the prototype); every press abandons the
window the press before it left, and only the last one lands. When it does, the highlight moves to where the count
reached and that destination is fetched. The same six tabs are now one visible move and one fetch — what every other
mechanism costs — and the five destinations passed over are neither drawn nor requested.

The thing this buys beyond the fetch count is that there is no in-between state at all. Nothing on the rail moves
ahead of its content, so nothing has to explain itself: no half-selected destination, no marker for *chosen but not
loaded*, no timeline sitting under a highlight that belongs to a different one. What the rail shows is always a
destination that was actually asked for — which is also why the rail carries no fetch-in-flight marker of its own. Its
right-hand column is unread counts and nothing else; that a fetch is happening is said once, on the breadcrumb, where
the content it will replace already is. A rail somebody is reading should hold still. Two things still follow: an answer overtaken before it lands is discarded
rather than drawn, and a destination is worth caching briefly so that walking out along the rail and back is one fetch
per destination rather than one per arrival. The rate-limit quota on the rail earns its corner here more than under
any of the other three, because this is the mechanism that could spend it by accident.

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
