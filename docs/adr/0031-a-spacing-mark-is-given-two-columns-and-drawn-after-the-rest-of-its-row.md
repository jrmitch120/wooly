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
