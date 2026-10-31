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
original, its description and a hint row beneath it (amended below), `←`/`→` stepping through the post's other pictures
and `esc` going back (amended below). The preview, already held, is drawn there at once and the original replaces it when it lands
(amended below), in a box settled from the original's shape so that nothing moves. One picture is held only while it is
looked at (amended below), so the screen may read a download past the 8 MB `Pictures.MostBytes` allows the timeline —
up to 32 MB, decoded straight to the window's size — and an original past even that leaves the preview standing with a
word on the status row.

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

## Amendment: the Stand-in waits for the original, not the preview (review of #514)

The decision above drew the preview on the Picture screen at once and the original over it when it landed. Tried, the
swap read as the picture flashing rather than sharpening: two pictures in turn, the first already a little wrong. The
screen now draws the picture's **Stand-in** until the original lands, and the original then in the same cells, as a
feed draws a picture arriving. The preview is drawn there only as the fallback, where the original is refused as too
large or cannot be fetched or decoded, with the status row's word as before; and only then does the screen want it, so
that opening a picture never draws one copy only to replace it. This supersedes spec #514's user stories 11 and 12.

## Amendment: no hint row; the crumb counts the pictures (review of #514)

The hint row under the description said again what the status row already lists, `esc back` and, where there were
several pictures, `←→ picture 2 of 4`, on a screen whose every row is better spent on the picture. It is gone, and its
row goes to the picture. The count it carried moves to the crumb, `Picture 2 of 4 by @author`, which a step updates; a
post with one picture keeps `Picture by @author`. The description, and `a picture, undescribed`, are centred under the
picture row by row, and cut at a quarter of the screen's rows with `…`, so that a description as long as an article
cannot leave the picture no room.

## Amendment: the screen holds the original of every picture on the post while it is open (review of #514)

The screen first wanted only the original of the picture it showed, so a step let go of the one it left and sent for the
next, and the Stand-in came up on every step: fair on opening, tiresome by the third picture of four. The screen now
sends for the originals of all the post's pictures it steps through. The one shown is sent for first, and the rest only
once it has landed or been refused, so that they do not share the line with the picture being looked at. Each is held
apart until the screen goes, as the shown one was, and a step no longer lets go of the one it leaves. A step to a
picture whose original has landed draws it at once, with no Stand-in, and a step back finds the one left still held. A
step to one still on its way shows its Stand-in, or the smaller copy where its original was refused, as before, and the
status row's word about a refused original is only ever about the picture shown. Where pictures are drawn as Kitty
placeholders or as sixel, the landed originals not shown are encoded ahead, each in the box it is drawn in
(`Line.Ahead`), so that a step draws its picture on its own frame rather than one encode later.

The cost is memory, held for as long as the screen is open: up to four originals rather than one, four being the most
attachments Mastodon carries on a post. Each is decoded to the window, about 9 MB of pixels for a window 2000 by 1200
pixels, and its file is held beside it: a few megabytes for most photographs, and at most the 32 MB
`Pictures.MostBytesHeldApart` reads. None of it counts against the timeline's two budgets, which are unchanged, and all
of it is let go the first frame after the screen goes, the smaller copies staying held for the timeline. A Kitty
terminal is told to forget every one it was sent, and those encoded ahead and never sent are dropped with them.

## Amendment: opening a picture lets go of it, as opening any reference does (review of #514)

The decision above has `esc` going back from the Picture screen, and #516 went back to the picture still walked: the
one reference `⏎` left walked as it opened, so that a reader looking at a post's pictures one by one carried on from
the one opened. That was an exception to the rule every other reference keeps, that `⏎` or a double click lets the
walked reference go as part of opening it (#474), and once the Picture screen stepped through the post's pictures
itself (#518) there was nothing left for it to buy. In use, the selection outlived what it had been opened for, and a
picture should follow the same rule as every other reference. It is reversed: opening a picture, by `⏎` or by a double click, lets go of the walked reference
exactly as opening a link does, and `esc` from the Picture screen comes back to the same post picked and nothing
walked, with no brackets drawn. The Picture screen keeps for itself which picture it was opened on, and steps from
there. This supersedes "`esc` going back" above where #516 read it as going back to the picture walked.
