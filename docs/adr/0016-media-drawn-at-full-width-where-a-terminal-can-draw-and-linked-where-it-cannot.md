# Media: drawn at full width where a terminal can draw, and linked where it cannot

Story 49 asks the TUI to draw images inline — sixel, then the Kitty graphics protocol, then coloured cells — while
stories 50 and 51 keep the CLI to a link and alt text always, and keep video and audio to a link and a description on
both surfaces. The ladder itself turned out to be the easy part; the rest was decided by building it and looking at it,
and one of those decisions goes against what the ticket asked for.

**Only a still picture is drawn. Everything else is a link and what its author said it shows.** `PostMedia.IsDrawable`
is the whole rule, and it is true of `MediaKind.Image` and nothing else. Video and audio are story 51's, and the reason
it gives — a reader must not be shown a broken or misleading inline rendering — applies just as squarely to a `gifv`,
which Mastodon serves as a soundless looping video with a still preview beside it. Drawing that preview would put a
frozen frame on screen with nothing to say it was meant to move, which is the misleading rendering rather than an
exception to it. So an animation is linked, like a video. An attachment of a kind this client has no word for is linked
too: an instance is free to serve something newer than this build, and a client that guessed would guess wrong exactly
where it mattered.

Both surfaces read that one rule and one phrase off the attachment itself, rather than each keeping its own table.
`Shows` is what an attachment says it is — its author's description, or `a picture, undescribed` where they gave none —
and the CLI writes it after the address while the TUI writes it under the box. Two tables is how `post show` comes to
describe a file one way and the timeline another. On the TUI the address is wrapped over as many rows as it takes
rather than clipped: a real attachment address runs to about a hundred characters, the contract gives a screen 61, and
a link with its end cut off has not been shown as a link.

An attachment's address is written on every post the CLI prints, including a timeline's, which is deliberately unlike
the post's *own* web address — that one is written only for a post asked for by id, because a line of it per post on a
timeline is a line of noise per post. The two are not the same thing: a post's address is a second way to reach what is
already on screen, and an attachment's address is the only way to reach the attachment at all. `--json` grows a `media`
array for the same reason, which also closes the gap story 15 left open once #28 taught `Post` to carry media at all.

**There is no cell-based fallback. A terminal that cannot draw links its attachments, exactly as the CLI does.** The
ticket asked for a third rung — a coloured cell per pixel, on the reasoning that it always works and so the TUI needs
no link-and-alt-text fallback. It was built, and it was wrong: a photograph in a box of a few dozen cells is a few
dozen coloured rectangles that resemble nothing, and it is strictly worse than the description it replaced, because a
reader can read a description. So `PictureWay` has two rungs and a floor, and on the floor every attachment — a
photograph included — reads the way `post show` writes it: `⏵`, what its author said it shows, and the address on the
rows below.

That decision is what forces the shape of everything else here. Whether a terminal can draw at all, and how big its
cells are, has to be known while a post's *rows* are being worked out rather than while they are being painted, because
it changes what the rows are. So `IPictures` is threaded into `Screen.Lines`, answers `Cell` as well as `Of`, and a
screen laid out with no terminal in the room — which is every test — links everything. That in turn keeps the
interesting half of this assertable, which is what ADR-0005 and ADR-0014 ask for: *a video is linked rather than
drawn*, *a terminal that draws nothing links everything*, *a picture keeps its own proportions*, *a gutter moves the box
along with the text*. Pixels are not asserted, and stay a manual smoke test.

**A picture is drawn at the full width of the column it is in, at its own proportions.** Width-driven, not
height-driven, and that is the whole difference between an inline picture and a thumbnail: the first attempt fixed the
height at six rows and let the width follow, which produced a box around eighteen columns wide — a postage stamp beside
what every other terminal client draws. The rows now follow from the picture's own proportions and the pixels-per-cell
the protocol reports, and the only limit is a cap on height (sixteen rows in a feed item, thirty-two on the post
screen) so that a tall photograph cannot take a screen and a half of a feed. A picture that hits the cap is narrowed to
match, so it is still the shape it was.

The picture's own description stands *above* its box rather than below, so that it does not move when the pixels land
underneath it. Until they do there is no box at all — a picture still on its way is its description and nothing else,
and the rows appear under it rather than a hole opening above the text a reader is part-way through.

**Terminal.Gui draws the pixels, and the preference for sixel is expressed where its ladder reads it.** `ImageView`
implements both protocols, and the raster plumbing underneath it — retained image ids, dirty tracking, clipping a
picture that is half scrolled off — is not reachable from outside the library anyway. It tries Kitty first and sixel
second, which is story 49's order reversed. Rather than reimplement the ladder to swap two rungs of it,
`RasterProtocol.PreferSixel` sets Kitty support aside on a terminal that reports both, so that sixel is what is left; a
terminal with only one of them is untouched. `ImageView` would also fall back to coloured cells of its own accord,
which this client does not want, so a `PictureView` that finds no raster protocol hides rather than drawing — the belt
to the brace of a post that has already laid itself out knowing there is no box.

That preference is *subscribed to*, not set once, and the difference is the whole of why it is a module rather than two
lines in `Program`. Both capabilities are found out by asking the terminal and waiting: the detectors queue ANSI
requests whose answers arrive on the input loop some frames after the application starts, and `IDriver.SixelSupport` is
null until one does. A preference expressed at startup would therefore be expressed against two nulls, and then
overwritten by whichever answer landed second. So `PreferSixel` hooks `SixelSupportChanged` and
`KittyGraphicsSupportChanged` and re-settles on each; setting Kitty aside raises the event it is subscribed to, which
comes back and finds nothing left to do. Which rung a given pair of answers picks is `RasterProtocol.Chosen`, a
function of the two results and nothing else — which is exactly the "fallback chain's selection logic, not the actual
pixel rendering" that #2's testing decisions name as worth a test, and it has one.

What Terminal.Gui does not do is decode a file: `ImageView` takes a `Color[,]` and has no image library. So this ticket
adds one — SixLabors.ImageSharp, held at 3.x. It is fully managed, which matters for #32's self-contained
single-file executables, where a native image library would mean a second artifact per architecture. Its licence is the
Six Labors Split License, royalty-free for a project under an OSI-approved licence, which this one (MIT) is. 4.0 is not
taken because it adds a build-time licence-key check that fails the build until somebody registers for a key, and a
clone of this repository has to build.

**A photograph's pixels are the one thing in the TUI a theme has no business answering.** ADR-0014's rule is that no
view constructs a colour: a view names a role and the theme resolves it. A picture is not an exception to that rule so
much as outside it — its colours are the content, not an emphasis somebody chose, and there is no sense in which
`dark` and `light` would answer them differently. The scan that enforces the rule therefore names one file,
`Media/PictureDecoder.cs`, and a second test asserts that the named file is still there, so the allowance cannot
quietly come to cover nothing or be renamed around. Every screen is still caught.

**A picture is taken off the screen by clearing the view that drew it, not by hiding it.** Sixel is painted into the
terminal's cells, so drawing text over it erases it. A Kitty placement is not: it persists until it is explicitly
deleted. Terminal.Gui offers two ways to reach that delete, and they are not equally strong. Hiding a view leaves it to
the sweep the framework makes after each frame of views no longer rendering; clearing `ImageView.Image` withdraws the
image from the output buffer there and then, which is what makes the driver emit the delete. Only the second is a
promise. A picture stuck over somebody else's post while scrolling is that difference.

**And a picture is put where the rows want it before the boxes are drawn, not while they are.** Terminal.Gui draws a
view's SubViews *before* its own content, so a `PaintedView` that placed its boxes from `OnDrawingContent` placed them
after they had already drawn — every picture landing where the rows had wanted it one frame ago. The symptom was
precise and is worth writing down, because it is what told us where to look: moving between posts a row at a time left
the pictures behind while the text moved, a page-jump was clean, and so was any key that changed the screen. The two
clean cases are the ones where a picture leaves the view entirely, and letting go of one takes it off the terminal at
once without needing a redraw to do it. So the boxes are settled from `OnClearingViewport`, which is the first thing a
view does when it draws and therefore the last moment before its SubViews draw.

That also means the rows are worked out once a frame and shared, rather than worked out again to paint. Where the
scroll has got to is derived from where it was, so asking twice in one frame can answer twice differently — and text
drawn at one scroll position with pictures placed at another is the thing all of this exists to prevent.

Three more rules follow, and `PaintedView` keeps all three:

- **Everything is released before anything is placed.** A box whose picture is no longer wanted is cleared at the top
  of the frame, so nothing is ever drawn over a placement the terminal has not yet been told to drop.
- **A box never goes from one picture straight to another.** Boxes are matched to attachments by id, and one freed this
  frame is the last resort when a home is being found for a new picture — so between two pictures on one view there is
  always a frame in which that view held nothing.
- **The pool is built in the constructor, and only where there are pictures at all.** Growing it on demand meant adding
  a subview from inside the parent's own draw, which mutates the tree that draw is walking. It is fixed at eight, which
  at a minimum of sixteen rows a picture is more than the tallest terminal can show, and the rail and the two chrome
  rows build none.

**Asking whether a picture is here is not the same as sending for it, and only the view may send.** A post works out
its rows whether or not it is anywhere near the screen — that is what a list of posts does — so a lookup that also
fetched fetched everything. On an account of nothing but photographs that was forty posts' worth of downloads and
full-size decodes, against a cache too small to hold them, every frame: a machine out of memory, by way of lag,
flicker, and pictures that never finished arriving.

So `IPictures` has two questions instead of one. `Of` is a lookup and nothing more. `Want` says an attachment is worth
having, and only `PaintedView` says it — of the rows within a screen's reach of the scroll position, which is the one
thing that knows where that is. The rows carry `Wants` on the description standing in for a picture, so an attachment
can be named as waiting before any of its pixels exist. Three smaller bounds fall out of the same failure and are worth
keeping together: at most four pictures are fetched and decoded at once, since decoding holds a whole picture before it
is scaled; a picture is decoded no taller than the tallest box can be, and where the format allows it that shrinking
happens *in* the decoder rather than after it, so a large photograph never exists whole in memory; and a burst of
arrivals is announced as one redraw rather than one each, because a forced redraw re-encodes every picture on screen.

**Where a screen scrolls to has to settle, because its own answer is its next input.** Pictures made a post taller
than the terminal for the first time, and the scroll rule the shell had shipped with since #28 could not hold one:
keeping the end of a too-tall post on screen means scrolling down, keeping its start on screen means scrolling back,
and asked afresh each frame it alternated between the two. Every flip moved the boxes, moving a box asks for a redraw,
and the redraw asked again — a loop that ran as fast as the terminal could draw, which is what a reader saw as very
fast flicker with pictures over the text.

The rule is now said outright rather than left to fall out of the arithmetic: a post taller than the room is shown from
its top and held there, which is what the code's own comment had claimed all along. It lives in `Rendering.Scroll` with
one property asserted above all others — asked twice over the same rows it answers the same thing twice.

The property still holds and is still what stops the flicker; what has changed since is what it is asked of. #51 gave
the reader a scroll position of their own — `↓` and `↑` move the screen by a row and leave the selection alone — so
`Scroll.To` now answers a `j` or `k` press asking for the selection to be brought back into view, rather than answering
every frame. That is a weaker demand on it, not a different one: the screen goes on following the selection for as long
as the arrows leave the offset alone, so a `To` that disagreed with itself would flicker exactly as it used to.

## Consequences

Previews are fetched on their own `HttpClient`, not through the one every Mastodon call goes through: a file server
needs no token, counts against no rate limit, and a picture that will not load must not spend the retry budget a
timeline's fetch is relying on. They are asked for once each, held to a bounded number of the most recently wanted, and
scaled down as they are decoded — a terminal draws a few hundred pixels across, and at four bytes a pixel a client that
held a morning's scrolling at full size would be holding a morning's scrolling.

Holding a too-tall post at its top left the rest of it out of reach, because nothing scrolled *within* a post — `j` and
`k` moved between posts and the arrow keys were bound to the same thing, so on a post with four attachments, five
screens tall, the later pictures could not be got to at all. That was a shortcoming of the shell's one movement rather
than of anything here, and #51 has since split it in two: the arrows walk rows, `j` and `k` walk posts, and a `j` after
the arrows have carried the selection off screen takes back the topmost post on the page (`docs/tui-shell.md`). The
bound on what is fetched is unchanged by it — a screen's worth of rows either side of wherever the scroll has got to,
which is now wherever the reader has put it.

Nothing about a picture is ever reported as an error. A fetch that fails, a file that will not decode, a format this
build has no decoder for: all of them are "no picture", the box stays empty, and the row that says what is attached is
what the reader has — which is the same thing the reader has on a terminal that cannot draw at all.

## Amendment: a post's attachments are part of what its warning covers (ticket #113)

This ADR settled what is drawn and ADR-0017 settled what is opened, and neither of them asked whether the reader wanted
to see it yet. That is a gap here rather than a feature nobody got to: everything above decides how an attachment is
*rendered* — a box at full width, or the address and what its author said it shows — and none of it decides *whether*,
which for media an instance has marked sensitive is the first question. Until #113 the answer was always yes. A
photograph flagged sensitive was drawn in place, full width, with nothing asked of the reader first, and #110 extended
that to a video's and an animation's own preview, which is what made it worth writing down.

**A post's attachments are behind its warning, and the warning is either half of one.** Mastodon carries two fields:
`spoiler_text`, which is the warning an author wrote, and `sensitive`, which is the instance's own flag over the media.
They are the same promise made twice, and this client was keeping neither half — `PostWire.ToPost` read the spoiler text
and dropped the flag, and `PostLines.Media` ran outside the warning gate entirely. So `Post` now carries `Sensitive`
alongside `ContentWarning`, and `Post.IsWarned` is the one question everything else asks: a spoiler text, the flag, or
both. The flag alone counts for nothing on a post carrying no attachments, which an instance is free to send — it is a
mark over media, so with none under it nothing is behind anything and `x` is not spent reporting that it acted.

The gate this opens is a gate on rendering, not a filter on the model. A warned post's attachments emit no rows, so
they carry no `Wants`, so `PaintedView` never asks `IPictures` for them — and *that* is the point rather than a side
effect. A reader scrolling a feed of sensitive posts should pay no data for pixels they have not asked to see, which is
the same reasoning that split `IPictures` into `Of` and `Want` in the first place. It is asserted directly, because it
is the kind of property that silently stops holding: a row emitted "just to reserve the space" would put the fetch back
without anything on screen looking wrong.

**What each half hides is still its own.** The flag is over the media, not the words: a post marked sensitive with no
spoiler text shows its text exactly as it always did, and only its attachments are kept back. So `Screen` asks two
questions where it asked one — `Readable`, which is the text and its references, and `Uncovered`, which is the
attachments and theirs — and `x` answers both at once, since a reader asking to see what a post is hiding is not asking
about one field of it.

**The attachment references join the gate, which reverses the exemption they were written.** ADR-0017 said nothing
about warnings at all; the exemption was made on its behalf, in `Screen.References`' own remarks and in
`docs/tui-shell.md`, and it gave a reason: an attachment's box and description already stood outside the warning, so
there was nothing for a bracket on one to hide behind. Hiding the media inverts the reasoning rather than merely
outweighing it —
`←`/`→` would walk to a label nobody can see, and `⏎` would open a video the reader never asked for. Nothing about how
a reference is walked or opened changes; only which ones exist before `x` is pressed.

**A post marked sensitive with nothing written over it says so where its attachments would be.** This is the one thing
added rather than gated, and it is what makes the rest reachable. On a post carrying a spoiler text the warning is
already up and already naming the key, so the attachments simply go. On a post carrying only the flag there is no
warning row at all, and hidden-with-no-sign is not hidden — it is a photograph the reader has no way to know is there
and a key they have no way to know means anything. So `⚠ Sensitive media` and the same `x  show it` stand in the
attachments' place, and that row is the whole of what is drawn: no box, no label, no description, no address, and
nothing sent for.

**The CLI is left exactly as it is, and this is the decision rather than the default.** It links every attachment
whatever the kind — the address and what its author said it shows — and hiding an address behind a prompt would be
hiding it from a script, on a surface that has no keystroke to reveal it with and where the output is as often piped as
read. Nothing is *rendered* there for a warning to be about: the reasoning at the top of this ADR is that a link and a
description are what a reader gets when pixels are not on the table, and that is precisely what a reader who has not
asked for the pixels should get too. `--json` grows no `sensitive` field for the same reason it grows nothing else it
has no consumer for; the field is on `Post` and available to add the day something asks for it.
