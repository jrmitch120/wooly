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
client counted each as no column, Terminal.Gui gave each a cell and wrote it in the run, and a terminal draws it in
none. There is no column to hold, so `Glyphs.Plain` drops them, as it drops a selector, along with control characters
and line and paragraph separators. The format characters segmentation joins to a neighbour stay: an emoji's joiners, a
flag's tags and the marks prepended to a number. So does a soft hyphen, which this client and Terminal.Gui count alike.
A tab becomes a space, the way HTML reads one, in the same place in the text, and `Glyphs.Columns` counts it as one
before it becomes one. Wcwidth 4.0.1 also counts Ethiopic's `ሀ` as no column, its range for Hangul's vowels running one
character too far, and `Glyphs.Columns` puts that column back.

`Plain` can make text shorter, so an offset into text as sent is not an offset into a span. `AccountLines` converts the
ends of a field's value before comparing them to its spans. Nothing else adds up a span's length against the text it
came from.

`MeasuredAsPlacedTests` walks every character there is: each must take the cells Terminal.Gui gives it, and each Unicode
calls wide (`UnicodeWide.txt`, from Unicode 17) must be counted two or held. A table upgraded on one side only fails
there first. It leaves out a Hangul vowel or final with no consonant before it, which no Korean text has; among the wide
characters, marks that take no room of their own; and a skin tone alone.

## Amendment: the cells past the end of a row are written with it

The output now also writes the blanks right of everything else on any row it is writing, in a pass of their own after
everything else on the frame. They never change, so Terminal.Gui never wrote them, and a cell any miscount put there
stayed until the screen was cleared. Now it goes in the frame that drew it. They are a pass of their own, because a run
carrying on through them to the terminal's last column would wrap on a count out by one, where stopping short leaves a
stray cell for them to clear. A row whose text reaches the last column has none, and wraps on such a count as it did.

The cell after a wide, held or written-apart character is left out, because a terminal may have drawn the character over
it and writing it would erase the character. A picture's blanks are safe in every pass: ADR-0023 relies on Terminal.Gui
never writing the blanks a picture claims, ADR-0016 on text never being drawn over a sixel, and those blanks are the
terminal's own colour, which is how Terminal.Gui tells. A test pins a box at the very end of a row.

## Amendment: a symbol or emoji is written apart from what follows it

A table can only say what its Unicode said. So that the next miscount costs a character rather than a row, a cell
holding something that can be an emoji — a symbol, an arrow, a skin tone or a keycap — is now written in the output's
later passes too, though it is still counted as Terminal.Gui counts it and gets no blank. What follows it is placed by
itself, so a terminal drawing it wider than counted covers the next cell or two and moves nothing past them. That also
bounds an emoji sequence on a terminal that adds its parts up. Box drawing and block elements stay in the first pass,
since every terminal draws them in one cell and they run the length of a row. Side by side, every other one goes in one
of two passes (a wide character's second column counting as its own), so a run of any length costs the same two.

Letters are not written apart. `ScrollMeasure.AFrameThroughTheOutput` writes a 120×50 frame with every cell dirty,
against Terminal.Gui's own output, and was run on a Release build. Writing every non-ASCII cell apart took Cyrillic from
10.6 to 40.8 KB and 514 to 1151 µs, and Han from 5.1 to 16.4 KB and 237 to 543 µs, on every frame of a reader's own
language, and placing a shaped script's letters one by one has not been looked at in a terminal. Rows of those scripts
still move if a terminal counts them differently; `Glyphs.WrittenApart` is the one place to change that.

| Page | Terminal.Gui | This output |
|---|---|---|
| Latin | 600 µs, 7.8 KB | 610 µs, 7.8 KB |
| Cyrillic | 514 µs, 10.6 KB | 569 µs, 10.6 KB |
| Han | 239 µs, 5.1 KB | 261 µs, 5.1 KB |
| A rail and a rule | 795 µs, 19.3 KB | 858 µs, 19.3 KB |
| A feed, with an emoji or action symbols on two rows in five | 508 µs, 6.8 KB | 556 µs, 7.6 KB |
| 20 emoji on a row | 299 µs, 8.7 KB | 504 µs, 24.4 KB |
| 40 emoji side by side | 293 µs, 8.8 KB | 697 µs, 24.8 KB |
| 40 narrow symbols side by side | 303 µs, 9.7 KB | 712 µs, 26.1 KB |
| Braille across the whole row, the worst found | 782 µs, 18.0 KB | 2348 µs, 65.3 KB |

Mode 2027, which asks a terminal to treat a grapheme cluster as one character, is not requested for now. With this in
place it would mostly tidy what is covered, and on a terminal that clusters by Unicode's conjunct rule (GB9c) it could
make `न्द` one character drawn differently from the two Terminal.Gui counts, which the consequences above warn of.
Neither has been looked at in a terminal.

## Still open

A skin tone with no emoji before it measures as nothing while a terminal may draw it wide; written apart, it covers what
follows it rather than moving the row. Terminal.Gui's own editing widgets get the writer half of all this, as the
consequences above say of a spacing mark: `🫪` or a symbol being typed into compose keeps its row but may cover the
character after it.
