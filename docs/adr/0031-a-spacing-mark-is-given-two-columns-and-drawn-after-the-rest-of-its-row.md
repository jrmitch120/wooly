# A spacing mark is given two columns and drawn after the rest of its row

Text carrying a **Spacing mark** (Unicode Mc, the vowel signs of Bengali, Devanagari, Tamil and the rest) is measured
three ways and there is no right one: Terminal.Gui counts `মী` as one column, WezTerm draws it in one, herdr draws it
as a wide cell of two, and GNU screen gives the mark a cell of its own (#434). Terminal.Gui writes a row as one run and
moves the cursor only where a run breaks, so every cell after such text lands wherever the terminal's count put it —
borders, the rail's divider, badges and right-aligned ages pushed out of place, and a full row wrapped onto the next.
#432 dodged it for the Lang list alone by showing those languages' English names.

So a cluster carrying a spacing mark is held in two columns — the layout's measure counts it as two, and the painter
leaves the second cell blank — and the output writer draws it after every other cell on its row, at an explicit
position. A terminal that draws it two wide covers the blank with it; one that draws it one wide shows a gap inside the
word; one that draws it wider hides the next character. In every case the rest of the row is where it was laid out.

The rule is about the character, not the terminal: nothing asks which terminal this is.

## Considered options

- **Move the cursor after each such cluster, in its own one cell** (the issue's first option). Measured in herdr, a
  cell written into the second column of a wide cluster erases the cluster: `অসমীয়া` came out `অস  `. The row held
  and the word did not.
- **Slack at the end of a row carrying such text.** The words survive, but everything after them on the row still
  shifts, and room is lost on every such row.
- **Change Terminal.Gui's count upstream.** Terminals disagree in both directions, so any count only moves the
  disagreement.

## Consequences

- The writer half is a Wooly output (`HeldOutput`, an `AnsiOutput`) handed to the application through the private
  field its component factory is read from, because Terminal.Gui 2.5.0 offers no public way to: its `DriverRegistry`
  names a factory for each driver, but the application makes its own by name and never calls it. A Terminal.Gui without
  the field leaves the application writing as Terminal.Gui does — the row moving as before, nothing worse — and a test
  says so. The field is `readonly`, and set, it overrides any driver named or forced after it — so it is set just
  before `Init` with none named. Ask upstream for a public way to hand an application its component factory; when there
  is one, use it.
- Terminal.app is beyond any of this: probed, it drew a border placed by an explicit cursor move one or two columns
  left after `सिन्धी`, `தமிழ்`, `తెలుగు` and `हिन्दी` — it lays such text out off its own cells, and what follows with it.
  Probing all 214 language names showed no Unicode property that predicts it: Bengali, full of spacing marks, drew in
  place, Telugu drifted with no virama, Arabic drew in place and Arabic-script Malay drifted. An author's words are
  shown as written and shift there; the client's own text — the Lang list's names — keeps a language's own name only
  where it is in a script never shaped (Latin, Greek, Cyrillic, Armenian, Georgian, Han, kana, Hangul) and is written in
  English otherwise, so the client's chrome is straight on every terminal.
- Text in Terminal.Gui's own editing widgets (compose, the bio, search) gets the writer half only: its row holds, but in
  a terminal that draws the cluster wide it may cover the character after it while it is being typed.
- A virama conjunct with no spacing mark (`न्द`, `ক্ষ`) is counted alike everywhere today, and only because .NET's
  grapheme segmentation splits it after the virama. A runtime adopting Unicode's conjunct rule (GB9c) would make it one
  Terminal.Gui cell drawn two wide; a test pins the split so that upgrade says so.

## Amendment: a character newer than the tables is held the same way

`🫪` (U+1FAEA, Unicode 17) is in neither .NET 10's table nor Wcwidth 4.0.1's, so Terminal.Gui counted it as one column,
and the macOS terminal it was seen on drew it in two. Every cell after it moved a column right: the content panel's edge
and the scrollbar's thumb went into the right margin, whose cells stay blank from frame to frame and so are not written
again, and they stayed behind as the post scrolled away. A terminal with tables older than the emoji counts it as one,
so no count agrees with all of them, which is the disagreement this ADR was written for. So a character .NET calls
unassigned and Terminal.Gui counts as one column is held in two columns and drawn after the rest of its row, like a
spacing mark. A terminal that knows it covers the blank; one that doesn't shows a gap after a stand-in glyph. Characters
reserved for future Han and Hangul stay as they are, because Terminal.Gui already counts them as terminals do. The rule
needs no table of emoji, so a later Unicode needs no change here. A .NET whose table is newer than Wcwidth's would stop
holding what Wcwidth still counts as one column, and the row would move again; `GlyphsTests` pins `🫪` so that upgrade
says so.

## Amendment: what has nothing to draw is dropped, and every character is held to its measure

A zero-width space, a direction mark, a word joiner, a byte-order mark and the like were the opposite disagreement: this
client counted each as no column, Terminal.Gui gave each a cell and wrote it in the run, and a terminal draws it in none.
There is no column to hold, so `Glyphs.Plain` drops them, as it drops a selector, along with control characters and line
and paragraph separators. The format characters segmentation joins to a neighbour stay: an emoji's joiners, a flag's tags
and the marks prepended to a number. A tab, which HTML reads as a space, becomes one when an instance's HTML is flattened.
Wcwidth 4.0.1 also counts Ethiopic's `ሀ` as no column, its range for Hangul's vowels running one character too far, and
`Glyphs.Columns` puts that column back.

`MeasuredAsPlacedTests` walks every character there is: each must take the cells Terminal.Gui gives it, and each Unicode
calls wide (`UnicodeWide.txt`, from Unicode 17) must be counted two or held. A table upgraded on one side only fails
there first. Two exceptions are named in it: a Hangul vowel with no consonant before it, which no Korean text has, and a
skin tone alone.

## Amendment: the cells past the end of a row are written with it

The output now also writes the blanks right of everything else on any row it is writing, one move and a few blanks a
row. They never change, so Terminal.Gui never wrote them, and a cell any miscount put there stayed until the screen was
cleared. Now it goes the next time its row is drawn, whatever the miscount was. The second column of a wide character at
the end of a row is left out, because writing it would erase the character.

## Amendment: a symbol or emoji is written apart from what follows it

A table can only say what its Unicode said. So that the next miscount costs a character rather than a row, a cell holding
a symbol or an emoji is now written in the output's later passes too, though it is still counted as Terminal.Gui counts
it and gets no blank. What follows it is placed by itself, so a terminal drawing it wider than counted covers the next
cell or two and moves nothing past them. That also bounds an emoji sequence on a terminal that adds its parts up. Box
drawing and block elements are left in the first pass, since every terminal draws them in one cell and they run the
length of a row.

Measured over a 120×50 frame with every cell written, against Terminal.Gui's own output, on Release builds: text in
Latin or Cyrillic costs nothing more, and a feed's page with an emoji or a post's action symbols on two rows in five goes
from a median of 502 to 542 µs and from 6.7 to 7.6 KB. Twenty emoji on every row goes from 291 to 424 µs and 6.8 to 22.9
KB. Forty narrow symbols side by side on every row, where each pass takes only one of each, is the worst case: 300 µs to
3 ms and 5.8 to 38.8 KB, still well inside a frame. `ScrollMeasure` does not go through this output, so these came from a
throwaway measure of `HeldOutput.Write` itself.

Mode 2027, which asks a terminal to treat a grapheme cluster as one character, is deliberately not requested. With this
in place it would only tidy what is covered, and on a terminal that clusters by Unicode's conjunct rule (GB9c) it could
make `न्द` one character drawn differently from the two Terminal.Gui counts, which the consequences above warn of.
