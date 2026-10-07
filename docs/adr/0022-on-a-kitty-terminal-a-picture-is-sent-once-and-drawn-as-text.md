# On a Kitty terminal, a picture is sent once and drawn as text

ADR-0016 had Terminal.Gui draw the pixels: a pooled `PictureView` (Terminal.Gui's `ImageView`) per box, placed over the
rows a post reserved for it on every frame. That held for a still screen and fell apart on a moving one. Scrolling past
pictures was slow, and the pictures jumped: the text moved at once and each picture landed at its new place a moment
later (#292). Measured headless in #287, Release build, 120×50, busy 1200×900 photographs:

| Per wheel event | Time | Sent to the terminal |
|---|---|---|
| No pictures on screen | ~23 ms | ~6 KB |
| Sixel, 3 photos, page moving | ~235 ms | ~480 KB |
| Kitty, 2 photos, page moving | 35–216 ms | ~11 MB |

On a Kitty terminal `ImageView` sends `a=T` (send and show) with raw 32-bit pixels at the photo's original size on every
frame its box moves, and crops and resends a photo half scrolled off. That is the jump. None of it is reachable from
outside the library, so it cannot be tuned; it has to be replaced. Pictures are central to this client, so the fix was
not allowed to hide, drop or blank a picture while it scrolls.

**On a terminal known to draw them, ADR-0016's "Terminal.Gui draws the pixels" is replaced by Kitty's Unicode
placeholders.** Each picture is sent to the terminal once, as a PNG shrunk to its box's pixels (`a=t,f=100`, in
4096-character chunks), and given a virtual placement over its box's columns and rows (`a=p,U=1`). From then on it is
drawn *as text*: every cell of the box is `U+10EEEE`, then a diacritic naming the cell's row and one naming its column,
with the image's id as the cell's 24-bit foreground colour. The picture is part of the rows, so it moves in the same
frame as the words around it. A box half off the top of the page draws only its lower rows, and the terminal crops it.
A scroll sends no image data at all. The prototype (`prototype/292-kitty-placeholders`) showed this by eye in Ghostty,
and that Terminal.Gui passes the placeholder graphemes and their colours through intact.

**`PaintedView` paints the placeholders itself, from the same scroll position as the text.** The settling that placed
the boxes (ADR-0016) now asks, for each box on the page, whether its picture has been sent at this size, and the draw
writes the placeholder cells for every visible row a box covers — including the lower rows of a box whose top is above
the page, which is why `Line.Insets` names a box once, on its top row. A box whose picture is not ready yet keeps the
`▒` its rows already carry. The `PictureView` pool stays for every other terminal, and is left empty where
placeholders are drawn.

**Placeholders come first, but only on a terminal known by name to draw them.** Sixel cannot move an image already on
screen, so every scroll step resends every visible picture; placeholders send none. But speaking the Kitty graphics
protocol is not the same as drawing its placeholders. WezTerm takes a Kitty transmission and prints the placeholder
cells as boxes: its implementation is an open pull request
(wezterm/wezterm#7924), and other clients have had to exclude it by name (yorukot/superfile#1665). #292 assumed WezTerm
would be the terminal the flip helped; it would have been the one it broke. So `RasterProtocol.Chosen` answers
`Placeholders` only where `KnownTerminal` names the terminal as Ghostty or kitty, and everywhere else draws through a
box. A terminal that adds placeholders is one line in `KnownTerminal`. (Which protocol a box draws through was left as
ADR-0016 had it, sixel first, until ADR-0023 measured a scroll and put Kitty first.)

**The terminal's image store is a port.** `ITerminalImages` is what the client says to it: transmit this PNG under this
id over so many columns and rows, forget this id, forget everything. `KittyImages` writes the escape sequences.
`Placeholders`, the module behind it, decides when to say each. Tests hold a fake that records the calls, so "a scroll
transmits nothing" and "a dropped picture is forgotten" are assertions rather than escape sequences read back
(ADR-0005). Pixels in a real terminal stay a manual smoke test.

What the prototype taught, kept as rules:

- **An id belongs to one picture at one box size, and ids are unique to the run.** A placeholder cell names an image,
  not a placement, so a terminal draws it with the first placement it holds for that id. Ids reused across runs left an
  old placement in place, and a portrait photograph came out as a one-column sliver. Ids count up from a random base,
  and everything is forgotten (`a=d,d=A`) just before the run's first picture is sent and again on the way out — not
  at startup itself, so that a terminal which never draws a picture is never sent a graphics command at all.
- **A picture dropped from `Pictures` is forgotten by the terminal** (`a=d,d=I`), every size of it, so the terminal
  holds no more than the client does. The drop can happen on whatever thread a fetch finished on, so the forget is queued and
  written on the UI thread at the next frame.
- **Encoding is off the UI thread.** A PNG encode took ~260 ms the first time a photo came into view, and doing it
  inside a draw stalls that scroll. The encode starts once a box is within a screen of the page, the reach a picture is
  fetched over, so a picture scrolled to is usually ready; one that is not keeps its `▒` until it is, and a redraw
  brings it in. The transmission itself is written on the UI thread, before the frame that
  draws its cells.
- **Fit, never squash.** The PNG is resized to exactly the box's pixels, and the box already has the picture's shape
  (`Inset.For`), so nothing is stretched as long as the cell size is right.

**A photograph's id is the second thing a theme has no business answering.** The id rides in a cell's foreground colour,
so painting it builds a colour, which ADR-0014's scan forbids outside the theme. Like `PictureDecoder`'s pixels it is
content rather than emphasis, and `Media/KittyPlaceholder.cs` joins the scan's short list of files allowed to.

**The name is read from the environment, at startup.** No terminal is asked whether it draws placeholders, because
there is no way to ask: the protocol's own query says whether a terminal takes Kitty graphics, not whether it draws
placeholders. Terminal.Gui does not ask even that. Its `KittyGraphicsSupportDetector` reads `KITTY_WINDOW_ID` and a
`TERM_PROGRAM` of `kitty` or `ghostty` and nothing else (2.4.17), and then asks the window's size in pixels, which in
the prototype held the first picture back 5–10 seconds. Ghostty and kitty say who they are (`TERM_PROGRAM` of
`ghostty`, `TERM` of `xterm-ghostty` or `xterm-kitty`, `KITTY_WINDOW_ID`, `GHOSTTY_RESOURCES_DIR`), and `KnownTerminal`
reads that before the first frame. A terminal that names itself nowhere — Ghostty over ssh with a plain `TERM`, say —
draws through a box. The name is never trusted where it would be wrong:

- **Not in Windows Terminal or WezTerm.** `WT_SESSION`, `WEZTERM_PANE` and a `TERM_PROGRAM` of `WezTerm` outweigh
  everything else, because a `TERM` carried in from elsewhere would otherwise send them placeholders they print as
  boxes. Warp is excluded for the same reason.
- **Not inside tmux or screen.** A multiplexer inherits the variables of the terminal it was started in but does not
  pass the protocol through. tmux passthrough is out of scope.

**A cell is the size the kernel says it is.** The PNG a picture is sent as is the box's cells in pixels, so a cell
guessed too small is a picture the terminal stretches into its box: Terminal.Gui's guess was 10×20 where Ghostty's cells
are 16×34. `WindowSize` asks the kernel for the window's size (`TIOCGWINSZ` on standard output) and divides its pixels
by its cells. It is measured once per size of screen, since a post asks several times a frame and a change of font size
changes how many cells the window holds. On Apple Silicon `ioctl` has to be declared with six dummy arguments ahead of
the pointer, because a variadic argument goes on the stack there; the plain declaration crashed the prototype.

- **Then the protocol's answer, then 10×20.** Most terminals fill in no pixels, and Windows has no `TIOCGWINSZ` at all,
  so it is never called there. Asking the terminal itself (`CSI 16 t`) is left for when a terminal is found that needs
  it.
- **A box keeps the protocol's own answer.** Terminal.Gui's image view sizes what it draws by that, and a box shaped by
  one cell size and filled by another is a picture of the wrong shape.

Out of scope, and left to the later slices of #292: a cheaper sixel path for Windows Terminal and iTerm2, drawing once
per batch of wheel events, and cheaper frames. tmux passthrough is not attempted.

## Amendment: a picture let go of is forgotten through a drain, on the UI thread (#358)

This ADR said the drop could happen on whatever thread a fetch finished on, with the forget queued for the next frame.
It no longer happens there at all. `Pictures` is constructed with no callback for what it lets go of: it writes down
the ids whose pixels it let go of — on a frame's `Want`, or as a picture lands — under its own lock, and hands each back
once through `IPictures.Drain`. The content view drains it once a frame, before the placeholders flush, and tells the
placeholders to drop each one; on a terminal not drawing placeholders the list is discarded. A picture let go of as
another lands is drained on the redraw that landing already asks for, so the terminal's copy is still deleted, a frame
later at most. Kitty is told anything only from the UI thread, and nothing the cache does reaches into it.
