# A sixel is encoded once for each cut, and handed to the driver ready

ADR-0022 took Kitty terminals off Terminal.Gui's `ImageView`. Everywhere else — Windows Terminal, iTerm2, WezTerm,
anything inside tmux — a picture is still drawn through a box, and almost always as sixel. Sixel cannot move an image
already on screen, so every scroll step sends each visible picture again. That much is the protocol. Measuring what a
step cost showed that most of it was not (#292, slice 3).

**What a step cost, and why.** Headless, Release, a 120×50 terminal, a feed of busy photographs at the size
`PictureDecoder` holds them, one wheel notch at a time:

| Per notch | Median | Worst | Sent |
|---|---|---|---|
| Before | 104 ms | 220 ms | ~590 KB |
| After, notches 16 ms apart over new rows | 27–44 ms | 83–137 ms | ~590 KB |
| After, notches back to back over new rows | 26–34 ms | 52–87 ms | ~590 KB |
| After, back over rows already drawn | 23–28 ms | 82–152 ms | ~590 KB |

Each "after" row is the spread over several runs on one machine, an Apple Silicon laptop, with the palette below in
place; the worst case is noisy. Before the palette was fixed the same runs were ~25–30 ms and ~500 KB. A palette that
actually covers the picture uses more of its colours, which costs bytes, and the right colours are worth them.

A frame with no pictures on it costs ~23 ms, so a step over pictures now costs about what a step over text does, plus
the sending. Back to back is a fast flick, where the cuts encoded ahead are not ready in time and some are encoded on
the frame; the time spent encoding ahead is off the UI thread and is not in these numbers. The photographs are
853×640, the size the decoder holds a 1200×900 one at, so "before" is not the ~235 ms #287 measured with three photos
on another page. What headless cannot see at all is the terminal parsing and drawing the ~500 KB it is still sent each
step, which on Windows Terminal may well be most of what a reader feels. The measurement is `ScrollMeasure`, skipped unless `WOOLY_MEASURE=1`, so it can be run again on any
machine; its remarks give the command. Windows Terminal has not been measured yet. It is the terminal this is for, and a
manual check there is owed.

Two things inside Terminal.Gui made up the difference:

- `ImageView` throws its scaled and encoded picture away whenever its frame changes (`OnFrameChanged`), and a scroll
  changes every box's frame on every step. So every visible picture was scaled and encoded again, whole, on the UI
  thread, every step.
- A box straddling the top or bottom of the page is cut by the driver, which encodes the cut again on every frame.

**A picture is scaled to its box once, and each cut of it encoded once and kept.** `SixelPictures` holds the picture
at its box's size in pixels, and a sixel for each cut drawn — the rows of the box on the page and the columns that fit
— keyed by the picture, the box, the cell, the cut and the colours. Scrolling back over a cut is free. 32 cuts are kept,
the one used longest ago going first. A cut holds its pixels as well as its sixel, a megabyte or so for a photograph
across a wide page, so the bound is memory; every cut on the page is used again each frame, so it is never the one
let go of.

**A box is framed to the part of it on the page.** `PaintedView` frames a straddling box to the rows still on it and
asks for that cut, so the driver is handed exactly what it draws and never cuts anything itself.

**`PictureView` hands the driver its sixel ready, and draws nothing of `ImageView`'s.** It stays an `ImageView`
because the driver keeps a raster image from one frame to the next only where an image view showing something claims
it, and that claim (`CollectActiveRasterImageIds`) is internal to the library. So `PictureView` keeps `Image` set,
which is the claim, and overrides the draw: it paints the cells under it as the picture's (no background at all, which
is how the driver knows to leave them) and adds a `RasterImageCommand` under the image view's own id with the pixels
and the encoded sixel. On a terminal drawing Kitty through a box none of this applies: there the image view draws the
whole box as before, because its Kitty path sends the picture once and places a crop of it as the box moves, which is
already what this does for sixel and cheaper. This leans on two things Terminal.Gui does not promise — the id's shape,
`ImageView_{hash}`, and an encoded sixel being used as is where the cut is the whole destination — so an upgrade of
the library is a reason to run the smoke test and the measurement again.

**A sixel's palette is chosen from the whole picture.** The first look at this in WezTerm drew the shapes right and
the colours wrong: a dark green sign in a pale illustration came out salmon. Terminal.Gui's palette builder
(`PopularityPaletteWithThreshold`) merges colours in the order it meets them — a column at a time from the left edge —
and stops at the first 64. A photograph has 64 colours in its first column or two, so the palette was the left edge's,
and anything only further right was drawn in the nearest of those. `ImageView` used the same builder, so every sixel
terminal had this before this change too. `SixelPalette` replaces it with ImageSharp's Wu quantizer over the whole
picture: on two photographs the mean colour error fell from 14 and 31 to 6 and 7, at about the same cost to encode.
The palette is built per cut (and is 256 colours now, below), so a cut is drawn in its own colours.
`Media/SixelPalette.cs` joins the colour scan's list, since a palette is the photograph's colours.

**A cut a scroll is about to want is encoded ahead, off the UI thread.** A box at the edge of the page is cut a row
differently on every step, and encoding that cut on the frame that wanted it was most of what was left once nothing was
encoded twice. An encode takes longer than the gap between two notches of a trackpad, so a row ahead is not enough:
`PaintedView` prepares the cuts four rows ahead the way the page is moving, and one row the other way. A cut asked for
while it is still being prepared is waited for, not encoded twice.

**Notches are already drawn at most once a frame.** Terminal.Gui drains the whole input queue before it draws, and a
notch only moves the page's offset and asks for a redraw. Notches arriving during a slow frame are therefore added up
and drawn by the next one, and a fast flick never queues behind slow frames. That is pinned by a test, not built.

**A box draws through Kitty before sixel, on any terminal that speaks both.** This reverses story 49 and ADR-0016,
and `RasterProtocol.PreferSixel`, which set Kitty aside on a terminal reporting both, is gone. In WezTerm, which answers
both and so drew sixel, scrolling past pictures was choppy even with everything above: sixel resends every picture on
the page on every step. Through a box, Terminal.Gui's image view sends a Kitty picture once and only places a crop of it
again as it moves. Measured the same way:

| Per notch, through a box | Median | Worst | Sent |
|---|---|---|---|
| Sixel | ~26 ms | 55–105 ms | ~590 KB every notch |
| Kitty | 18–23 ms | 39–61 ms | 114–209 KB on average: placements, and a picture once as it arrives |

#287 measured 11 MB a notch for Kitty through `ImageView`; Terminal.Gui 2.4.17's image view sends a picture once and
crops it by placement, so that no longer holds. **In practice this rung is never reached, though.** Terminal.Gui says a
terminal speaks Kitty only where its environment names kitty or Ghostty (`KittyGraphicsSupportDetector` asks the
terminal nothing), and those two draw placeholders. WezTerm is never told it speaks Kitty, so it draws sixel, and
dropping `PreferSixel` changed nothing there. Asking the terminal itself would need Terminal.Gui to read the reply,
which it does not: the reply would arrive as keys. The order is a rule about protocols, not about any one terminal, and
it is also the order `ImageView` tries them in, so the picture `PaintedView` encodes for and the one the driver draws
cannot disagree. Kitty is also drawn in full colour, which sixel cannot be.

**A sixel is 256 colours, the most it allows, and is quantized before it is encoded.** At 64, the cap Terminal.Gui's
image view uses, a photograph's smooth gradients broke into patches in WezTerm, with the palette right and too few of
them. `SixelPalette` now quantizes the picture itself with ImageSharp's Wu quantizer, which leaves every pixel a palette
colour exactly, so Terminal.Gui's encoder finds each one in a dictionary rather than searching the palette for the
nearest. That makes the colour count nearly free on this side; what it costs is bytes, which the terminal parses on
every step. Over new rows, a notch every 16 ms:

| Sixel | Sent per notch | Look, on a blurred photograph |
|---|---|---|
| 64 colours | ~600 KB | Patches |
| 64, dithered (Bayer 8×8) | ~730 KB | Smooth, grainy, a little washed out |
| 128 | ~800 KB | Some banding |
| 256 | ~1 MB | Close to the original |

256 was chosen by eye in WezTerm, undithered. Scrolling there is still choppier than through placeholders, and turning
a page quickly is choppier still: a page lands on cuts nothing encoded ahead, and every picture on it is ~1 MB the
terminal must take in. Encoding the whole of every box within a screen of the page ahead was tried for that and did
not help. It competed with the frame for the processor and churned the kept cuts, and the median page stayed at
70–110 ms. **This is as far as sixel goes.** It cannot move an image, so a terminal that draws only sixel will scroll
pictures less smoothly than one drawing placeholders, however its frames are made.

**Findings for the levers left alone:**

- **Windows Terminal still has no Kitty graphics.** It draws sixel only. Kitty support is an open request
  (microsoft/terminal#8389) that its developers have been experimenting with. So this path, not placeholders, is the
  one Windows readers get.
- **Synchronized output (DEC mode 2026)** would not make sixel cheaper, but it could stop text landing a moment ahead of
  its picture. Windows Terminal added it in Preview 1.25 (its release notes), and recent iTerm2 builds report it,
  though iTerm2 3.4 answers that it is permanently off. Terminal.Gui 2.4.17 never emits it, and has no hook around
  the write of a frame: `LayoutAndDrawComplete` fires after it, and nothing fires before it. Wrapping frames would mean
  writing the mode on from inside a view's draw and off from that event. That is fragile enough to want a terminal to
  try it on, and it is left for a follow-up.
