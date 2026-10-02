# The shell is panels: a rail of groups, the breadcrumb as a title, and the terminal's own page

ADR-0014 settled the shell's bones: a rail that stays, a stack you walk back out of, roles instead of colours. Those
held up. The complaint was the skin. The frame drew itself in rules and bands that read as bland, and nothing on screen
said where one region stopped and the next began except a `│` and a blank row. Two prototype rounds answered it, on the
real shell with real posts rather than in mockups. Both are kept out of `main` as primary sources:

- `prototype/tui-ux` built four layouts from scratch: lazygit panels, an aerc mail client, a glow-style reader, and
  yazi's Miller columns. Every one gave up the rail-and-stack, and every one read worse than the bones it replaced. That
  was the evidence the bones were right.
- `prototype/tui-skins` laid three skins over the real `ShellWindow` (statusline, panels, Charm), then four variations
  on the panels skin (tabs in the title, stacked rail, cards, top bar). The **stacked rail** variation won and was tuned
  over several rounds of use. This ADR is what it settled.

**The rail and the content are panels.** Each is a rounded frame with a title on its top edge. The content panel's
title *is* the breadcrumb: the same trail, eliding from the left as before, the fetch mark at its end. So the breadcrumb
row, the blank seam row under it, and the gutter column beside the rail all retire, because the frames now do the
dividing those three cells did. At 80 columns the content panel's rows are 60 columns wide, one fewer than ADR-0014's
61, because the gutter column's cell becomes the content panel's left edge. The panel's top edge sits on row 0 where
the breadcrumb was, and its first row of content is row 1. That gains back the row #216 spent on the seam and pays it
to the bottom edge.

The frames are drawn by this client, not by Terminal.Gui's `Border` adornment. The prototype used both and the
difference decided it:

- A `Border` titles itself `┤title├`, and nothing in its settings removes the brackets.
- A `Border` takes its colour from the view's `Scheme`, which is outside the role vocabulary ADR-0014 holds every cell
  to.
- The rail is several frames stacked in one region, which no `Border` describes.

One frame-drawing path, painting roles, serves both regions. A picture placed in a framed region is placed inside the
frame, never over it.

**The rail is four rail groups, each its own panel, with the API budget in a fifth at its foot.**

| Group | Holds |
|---|---|
| Timelines | Home, Local, Federated, Hashtag |
| Explore | Discover, Search |
| Inbox | Notifications, Direct messages, Follow requests |
| You | the profile's own account |

ADR-0019 put Discover "immediately after `Search` and in its group", a group of five that mixed what comes to you with
what you go looking with. That grouping goes. The five split by what each entry is for: Explore holds tools that never
carry an unread count, and Inbox holds where every unread count lives. The rail's own order becomes the order shown, so
`tab` and `shift-tab` walk it top to bottom exactly as they walked the old list. The group holding the selected
destination draws its frame in the active role. The settle window is unchanged (ADR-0014): the cursor moves on every
press, and the selection follows, with its one fetch, only once the presses stop. Where colour is drawn, bands are the
whole of the rail's mark, with no `▶`. The selected entry is on `rail-current` (blue on a dark band). While tabbing, the
cursor's entry is on `rail-cursor`, a lighter band, and the selected entry keeps its own band until the window closes.
At rest the two coincide and only `rail-current` shows. Where colour is not drawn (`NO_COLOR`, `TERM=dumb`), the rail
keeps its one-column `▶`/`▷` exactly as #67 left it. That is the rule below, applied.

A rail group is new vocabulary, distinct from a **Section** (a headed run on a screen, walked with `[`/`]`). "Section"
was the word that came to hand in the prototype, and it is the one word the rail's boxes must not be called.

The rail needs 22 rows framed: Timelines 6, Explore 4, Inbox 5, You 3, API 4. An 80×24 terminal gives it 23. On a
shorter terminal it steps down rather than clipping:

1. Framed, where every group fits.
2. Compact, where they do not: each group's title becomes a heading row, the frames go, and the API panel becomes one
   gauge row (15 rows).
3. Scrolled, where even that does not fit: the compact rail scrolls to keep the cursor's group in view.

**A meaning needs a carrier on every terminal, not a glyph on every terminal.** ADR-0014 said colour is never the only
thing carrying a meaning, so every state had a glyph *as well as* a colour. That is amended. The guarantee it existed
for stands: nothing a reader acts on may vanish under `NO_COLOR`. What changes is that the glyph is owed only where
colour is not drawn. Where it is drawn, a band may carry a meaning alone. So a line builder may ask whether colour is
drawn, and draw a glyph only when it is not. B2's rail is the case that earned it: a `▶` on an entry already banded and
blue said the same thing twice, and the shell read cleaner without it. The test is per meaning and per terminal: with
colour, is it carried? Without, is it carried? Both answers must be yes, and are tested.

The post's `▌` is not affected: in colour it is the band's edge, and the band without it reads as a stripe rather than a
selection. Every other glyph ADR-0014 lists (`○ ◌ ● ✉`, `⚠`, `↺`/`★`, `‹ ›`) carries a distinction that colour would
also have to carry and stays on every terminal.

**`` ` `` and `~` move to the next and previous rail group, landing on its first entry.** `tab` and `shift-tab` are
unchanged: one destination forward and back. Two alternatives were tried and lost:

- Tab bound to a rail group, with Shift-Tab moving between groups. Tried in use and rejected the same day. It took away
  "previous destination", which is worth more than it looks, and made one key's meaning depend on where the cursor
  stood.
- Digits per group. Not available: `1`–`0` are a poll's answers on every screen.

A group jump moves the cursor and restarts the settle window like any other press, so walking past three groups to a
fourth is still one fetch (ADR-0014).

**The page is the terminal's own.** Every built-in theme draws on the terminal's default background, and a user's
theme may still name one. This reverses #46's answer to `docs/tui-shell.md`'s first open question, which said a theme
cannot decline a background because a Terminal.Gui attribute had no "leave it alone" in it. Terminal.Gui 2.4 has one.
`Color.None` is written to the terminal as `CSI 49m`, which the prototype verified in captured output. On a terminal
with padding round its cells, the app now meets that padding with no seam. Before, the app's own page stopped a
fraction of a cell short of the window's edge in a colour the terminal did not have.

**The selected thing is a band across all its rows, not only a mark in its gutter.** The `▌` mark stays as the
no-colour carrier. The band is a role, painted where the rows are built, not substituted inside the view that paints
them.

**`dark` is replaced by the panels palette.** The colours are Catppuccin Mocha's, with blue as the one accent: the
active frame, panel titles, the current destination, the gauge. Boost green and favourite yellow keep the jobs they
had, and reply counts gain a colour of their own (mauve) where they were `muted`. The pick mark stays `#f2f0f7`. The
values tuned in use were measured against a black terminal:

| What | Colour |
|---|---|
| The picked post's band | `#16171c` |
| The rail's current entry | `#1f2128` |

`light` is redrawn to the same structure. The status row becomes lazygit's `Does: key | Does: key` and keeps every rule
#169 gave it (ranked, `…+N`, `?:keys` never cut).

**Roles change, in both directions.**

- Added: `panel-border`, `panel-border-active`, `panel-title`, `replies`, `gauge`, `gauge-empty`, `band`,
  `rail-cursor`.
- Retired with their regions: `seam`, `crumb`, `crumb-current`.

`loading` stays, for the fetch mark that now ends the content panel's title. Each new role still has to pass ADR-0014's
test: something a reader's next action depends on telling apart. The frames pass it, because which panel is which is
the whole of their job. `replies` passes on the same ground as `boost` and `favorite`, since the three counts sit side
by side.

**Rejected, and on the prototype branch if it is ever revisited:**

- Statusline segments: helix, lualine, zellij.
- Charm's frameless style.
- Timelines as tabs in the content title.
- Every post as its own card.
- A full-width top bar.

Each read well in a screenshot, and each lost on the shell's own terms. Tabs hid the rail's timelines whenever you
drilled in. Cards cost two rows and four columns per post at the 60-column floor. The top bar spent a row repeating what
the rail already said.

## What this supersedes

- **ADR-0014.** Its rule that colour never carries a meaning alone, now: a carrier on every terminal. Its regions: the rail of 18 columns stays, the breadcrumb, seam and gutter go, and content starts on
  row 1. Its role table: as above. The #160 amendment's breadcrumb band, seam band and blank row.
- **ADR-0019.** Discover's place on the rail.
- **`docs/tui-shell.md` open question 1.**

`docs/tui-shell.md` carries the enumerable detail (regions, keys, roles, the theme's shape), and the "What the
breadcrumb settled" and "What the status row settled" sections say which of their rules this changes.

## Amendment: the cursor's group is the lit one (#272)

The group lit in the active role is the one holding the cursor, not the selected destination. Lit by the selection, the
frame waited out the settle window, so a press of `` ` `` landed in a group that did not say so for a quarter of a
second. Lit by the cursor, it moves on the press, alongside `rail-cursor`'s band and, without colour, `▶`. Mid-tab the
selection keeps its `rail-current` band in a frame no longer lit, until the window closes and the two agree again. What
a press costs is unchanged: the frame is drawing, and the selection still follows, with its one fetch, only once the
presses stop.
