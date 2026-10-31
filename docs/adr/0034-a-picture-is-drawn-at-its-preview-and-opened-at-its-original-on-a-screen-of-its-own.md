# A picture is drawn at its preview and opened at its original on a screen of its own

A screenshot of small text is grainy in the TUI and sharp on the web. The cause is the copy fetched, not how it is
painted: a post's picture is drawn at the instance's smaller copy (`PostMedia.Preview`, Mastodon's `small`, 644×357 for
the screenshot that raised this), and on a high-density screen a full-width **Picture box** is 1500–2000 pixels across,
so the smaller copy is stretched about three times before the terminal ever sees it. The original was 2325×1290. The
premise `Drawn.Attached` gives for the smaller copy — that a terminal draws a few hundred pixels across at most — no
longer holds.

**The timeline keeps the smaller copy.** Pixels are held at the size they are drawn (ADR-0025), so an original costs its
box — about 7 MB at that width against 0.9 MB for the smaller copy — and the decoded tier's 64 MB would hold about nine
pictures rather than seventy. Scrolling back would be a decode or a fetch far more often, for a sharpness most
photographs do not need.

**A drawn still picture is a Reference, and the original is one step away, on a Picture screen.** `←`/`→` reach it after
the post's text references, in the order the pictures are drawn, and `⏎` opens it. The mouse reaches it as it reaches
any reference (#475, #479): a click anywhere on its box, or in the bracket columns beside it, walks to it, and a double
click there opens it, in a feed and on the post screen alike. The whole box answers rather than the bracket columns
alone, because a picture has no text to aim at and the columns are blank until it is walked. Opening a picture lets go
of the walked reference, as opening any reference does (#474): `esc` from the Picture screen comes back to the same post
picked and nothing walked, with no brackets drawn. The Picture screen keeps for itself which picture it was opened on.

**It opens inside the terminal, not in the reader's browser,** although that was the cheaper way and is how a video
opens (ADR-0017). A video cannot be played here; a picture can, and sending the reader out of the TUI to read a
screenshot is the thing a terminal client exists to avoid — and over SSH it would open on the wrong machine.

**A walked picture is bracketed, at its own size.** Everything else walked is drawn inside `‹ ›`, so that it reads on a
terminal without colour. A picture has no text to put them round, so a walked one is drawn between tall brackets in the
columns either side of its box, `⎡ ⎢ ⎣` and `⎤ ⎥ ⎦` (`[` `]` on a box one row tall), a glyph on every row so that they
stay visible however much of the picture is scrolled off. Both columns are given up by every attachment's box, walked or
not, so that nothing moves when a picture is walked to and so that a video's box lines up with a picture's beside it.
The tall brackets are the ones that read as brackets without colour, the corners saying where a box starts and ends
where a pair of thin bars says only that something stands beside it; and they are one column wide everywhere, being
neutral in Unicode's East Asian widths where the block elements `▏` and `▕` are ambiguous and are drawn two wide by a
terminal set for CJK. They were settled on without being looked at (#519): drawing them beside a box in Ghostty and
macOS Terminal is still to be checked by eye, and if they draw badly there — broken between rows, or off the cell's
centre — `▏` and `▕` replace them.

**The Picture screen shows one picture over the whole window at its original.** Its box is settled from the original's
shape (`PostMedia.OriginalShape`, the smaller copy's standing in where the instance said none), fitted whole and
centred, and settled again on every frame from the room the window gives it. The box is left empty until the original
lands — blank cells in the page's own colour, with no blur, no shade and no text — taking the cells the original will
fill, so that nothing moves when it does. The smaller copy is drawn there, stretched to the box, only where the original
is refused: too large, or not fetched or not decoded; and only then does the screen want the smaller copy at all.

Beneath the picture is what its author said it shows, or `a picture, undescribed`, centred row by row and cut at a
quarter of the screen's rows (one at least), its last row ending in `…`, so that a description as long as an article
cannot leave the picture no room. Under that is one row kept for saying why the original will not come, centred as the
description is: `The original is too large to show.` or `The original could not be fetched.`, said of the picture shown
for as long as it is shown, and blank otherwise. It is kept whether anything is said there or not, so that nothing moves
when it is. Nothing is drawn under it: the status row lists the keys, `esc back` and, where the post has several
pictures, `←→ picture`, which step through them in the order they are drawn, stopping at either end, and it goes on
listing them whatever the screen says of an original. The crumb counts them, `Picture 2 of 4 by @author`, and a post
with one picture has `Picture by @author`.

**The screen holds the originals of all the post's pictures while it is open.** The one shown is sent for first, and the
rest from the first frame one of them has landed or been refused, so that they do not share the line with the picture
being looked at. Each landed or refused is held until the screen goes, so a step to a picture whose original has landed
draws it at once, and a step back finds the one left still held; one stepped off before anything has landed is let go,
its download called off, so that it does not hold up the one stepped to. What the screen wants, and what it says of an
original, are read off what the cache holds as of the frame, the screen keeping nothing of its own between frames but
which picture is shown. Where pictures are drawn as Kitty placeholders or as sixel, the landed originals not shown are
encoded ahead, each in the box it is drawn in and on the row that box would set it on (`Line.Ahead`), so that a step
draws its picture on its own frame rather than one encode later; one encoded ahead for a window since resized is dropped
rather than held, never having been sent.

The cost is memory, held for as long as the screen is open: up to four originals, four being the most attachments
Mastodon carries on a post. Each is decoded straight to the window, about 9 MB of pixels for a window 2000 by 1200
pixels, and its file is held beside it: a few megabytes for most photographs, 32 MB at most. They are held apart from
the timeline's pictures (`Drawn.HeldApart`): none of it counts against the timeline's two budgets, which are unchanged,
and none of the timeline's pictures is let go to make room. All of it is let go the first frame after the screen goes,
the smaller copies staying held for the timeline; a Kitty terminal is told to forget every one it was sent, and those
encoded ahead and never sent are dropped with them. An original still on its way then has its download called off, so
that it does not go on holding one of the few places the timeline's pictures are fetched through, and one wanted again
before that download is over — the picture opened again at once — waits for it to be over rather than being fetched a
second time beside it.

**A download is read up to 32 MB** (`Pictures.MostBytesHeldApart`) for an original, where the timeline stops at 8 MB
(`Pictures.MostBytes`). An original past 32 MB is refused, and the smaller copy is drawn in its place.

## Considered options

- **Fetch the original on the timeline wherever the smaller copy would be stretched.** It is what Mastodon's own web
  client does, through a `srcset` that lets the browser choose, and it was the first thing settled on. Refused on its
  memory, as above.
- **Open a picture in the reader's browser**, as a video opens. Refused, as above: a picture can be drawn here.
- **Bracket a walked picture's caption.** A reader can hide it (`hide_drawn_caption`).
- **A label row over every picture to bracket.** A row each for pictures nobody walks to.
- **Draw the smaller copy on the Picture screen at once, and swap in the original when it lands.** Tried, it read as the
  picture flashing rather than sharpening: two pictures in turn, the first already a little wrong.
- **The blurred Stand-in in the Picture screen's box until the original lands,** as a feed draws a picture arriving.
  Tried, it felt off on a screen that shows one picture, where it is the whole of what is there to look at; and while
  the screen held only the original shown, it popped in on every step, fair on opening and tiresome by the third picture
  of four.
- **Keep the picture walked when the Picture screen is left with `esc`,** so that a reader looking at a post's pictures
  one by one carries on from the one opened (#516). Refused because a picture should follow the same rule as every other
  reference, that opening it lets it go; and once the Picture screen stepped through the post's pictures itself (#518)
  there was nothing left for it to buy.
- **A hint row under the description**, saying `esc back` and `←→ picture 2 of 4`. It said again what the status row
  already lists, on a screen whose every row is better spent on the picture; the count went to the crumb.
- **Say why an original will not come on the status row,** as a notice. Tried, it put the keys out of sight: the status
  row shows a notice in place of the keys, and with no hint row `esc` was then shown nowhere. A notice beside the keys
  was the other way, and was turned down as a change to the status row of every screen for the sake of one, where a row
  of the Picture screen's own costs only that screen a row. Saying it there also keeps the screen from writing to the
  status row while it is being laid out, so what a frame draws follows from what the cache holds alone.

## Consequences

This amends ADR-0017, under which `Image` never joined the walk: a drawn still picture now does, and a picture that
cannot be drawn keeps its `⏵`, description and address as before. A **warned** post's pictures are not walked until it
is asked past, as nothing on it is.

Spec #514's user stories 11 and 12, which drew the smaller copy on the Picture screen first, are superseded.
