# A picture is drawn at its preview and opened at its original on a screen of its own

A screenshot of small text is grainy in the TUI and sharp on the web. The cause is the copy fetched, not how it is
painted: a post's picture is drawn at the instance's preview (`PostMedia.Preview`, Mastodon's `small`, 644×357 for the
screenshot that raised this), and on a high-density screen a full-width **Picture box** is 1500–2000 pixels across, so
the preview is stretched about three times before the terminal ever sees it. The original was 2325×1290. The premise
`Drawn.Attached` gives for the preview — that a terminal draws a few hundred pixels across at most — no longer holds.

**The timeline keeps the preview.** Fetching the original wherever the preview would be stretched is what Mastodon's
own web client does, through a `srcset` that lets the browser choose, and it was the first thing settled on. It was
turned back on its cost here: pixels are held at the size they are drawn (ADR-0025), so an original costs its box —
about 7 MB at that width against 0.9 MB for the preview — and the decoded tier's 64 MB holds about nine pictures rather
than seventy. Scrolling back would be a decode or a fetch far more often, for a sharpness most photographs do not need.

**The original is one key away instead, on a Picture screen.** A drawn still picture joins the walk: `←`/`→` reach it
after the post's text references, in the order the pictures are drawn, and `⏎` opens it over the whole window at its
original, its description and a hint row beneath it, `←`/`→` stepping through the post's other pictures and `esc` going
back. The preview, already held, is drawn there at once and the original replaces it when it lands, in a box settled
from the original's shape so that nothing moves. One picture is held only while it is looked at, so the screen may read
a download past the 8 MB `Pictures.MostBytes` allows the timeline — up to 32 MB, decoded straight to the window's size —
and an original past even that leaves the preview standing with a word on the status row.

**It opens inside the terminal, not in the reader's browser,** although that was the cheaper way and is how a video
opens (ADR-0017). A video cannot be played here; a picture can, and sending the reader out of the TUI to read a
screenshot is the thing a terminal client exists to avoid — and over SSH it would open on the wrong machine.

**A walked picture is bracketed, at its own size.** Everything else walked is drawn inside `‹ ›`, so that it reads on a
terminal without colour. A picture has no text to put them round, so a walked one is drawn between tall brackets in the
columns either side of its box — `⎡⎢⎣` and `⎤⎥⎦` — which stay visible however much of the picture is scrolled off. Both
columns are given up by every attachment's box, walked or not, so that nothing moves when a picture is walked to and so
that a video's box lines up with a picture's beside it. A caption was not bracketed instead because a reader can hide
it (`hide_drawn_caption`), and a label row over every picture was a row each for pictures nobody walks to.

## Consequences

This amends ADR-0017, under which `Image` never joined the walk: a drawn still picture now does, and a picture that
cannot be drawn keeps its `⏵`, description and address as before. A **warned** post's pictures are not walked until it
is asked past, as nothing on it is.

**Amendment: a picture is clicked as other references are** (review of #514). Opening a picture by clicking was first
left to wait for mouse support. The mouse already walks to a reference with a click and opens it with a double click
(#475, #479), and a picture left out of that was the one reference a reader could see and not click, so it joins: a
click anywhere on its box, or in the bracket columns beside it, walks to it and brackets it, and a double click there
opens its Picture screen, in a feed and on the post screen alike. The whole box answers rather than the bracket columns
alone, because a picture has no text to aim at and the columns are blank until it is walked.

**The brackets settled on are `⎡ ⎢ ⎣` and `⎤ ⎥ ⎦`** (#519), with `▏` and `▕` as the fallback. The tall brackets are
the ones that read as brackets on a terminal without colour, the corners saying where a box starts and ends where a
pair of thin bars says only that something stands beside it; and they are one column wide everywhere, being neutral in
Unicode's East Asian widths where the block elements `▏` and `▕` are ambiguous and are drawn two wide by a terminal set
for CJK. They were settled on without being looked at: the check the ticket asked for, drawing them beside a box in
Ghostty and macOS Terminal, is still to be done by eye, and if they draw badly there — broken between rows, or off the
cell's centre — the fallback replaces them.
