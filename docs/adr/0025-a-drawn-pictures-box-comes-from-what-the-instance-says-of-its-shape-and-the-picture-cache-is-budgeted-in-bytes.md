# A drawn picture's box comes from what the instance says of its shape, and the picture cache is budgeted in bytes

Scrolling a timeline in the TUI made the text jump, on Kitty and sixel terminals alike (#345). A post was laid out with
one `▒▒▒▒` row until its **drawn** picture arrived, then grew by as many as sixteen rows and pushed everything under it
down. Scroll on, and the same picture could be let go of while it was still on screen: the post shrank back to its one
row, and grew again when the picture was sent for again. Going from Home to Local and back fetched what had already
been fetched. Three things did it, and this ADR settles the two of them that are hard to reverse:

- A box existed only once its pixels did, because `Inset.For` sized it from the decoded picture. The instance already
  says each attachment's shape in its metadata, and `PostWire` threw that away.
- `Pictures` held `MostHeld = 32` pictures and let go of the one *fetched* longest ago. Asking for a picture again did
  not renew it, so the picture on screen could be the one dropped. Since bylines gained avatars (#77) a screenful can
  want ten or more pictures, and the three screens `Want` reaches over can want more than 32. On Kitty, letting go also
  deletes the terminal's copy (ADR-0022), so the next frame sends it again.
- Every destination shares that one cache, so coming back to a timeline meant fetching pictures again.

**A drawn picture's box is settled before its pixels arrive, from the shape the instance reports, and never changes size
after that.** For an **Attachment** the shape is the width and height in the metadata's `small` entry, which describes
the preview this client fetches, and `original` where `small` does not have both. For a **Link preview** it is the
card's own width and height, trusted only where both are positive. Where no shape is given, the box is 16:9 at full
width. Every box is under the same caps as before: sixteen rows in a feed, thirty-two on the post screen. The rest of
ADR-0016's sizing stands: full width, rows from proportions and the cell size, and narrowed to match when it hits a cap.
What changes is whose proportions they are: the instance's, read from the post, not the picture's, read from the pixels.
An avatar keeps the fixed box it already had.

So `PostLines` no longer asks `IPictures` whether a picture has pixels before giving it a box. On a terminal that can
draw, every drawable attachment on a post that is not warned gets its box. The rows a post takes are known when the post
is laid out, and they are the same before the picture is here, after it arrives, and after it is let go of.

**When the pixels do not have the reported shape, the picture is fitted and centred, and the box stays the same size.**
An instance can be wrong about the shape, a card can report one that does not match its image, and the 16:9 default is a
guess. In every case the picture is scaled to fit inside the box, centred, with the page background around it. A wrong
guess wastes a strip of space. Resizing the box would make the text jump, which is the problem this ADR is fixing.

**Until the pixels arrive, the box shows its Stand-in, and the Stand-in has no text in it.** Where the instance sent a
blurhash, the Stand-in is that blurhash decoded to a small picture and drawn through the same raster path as any other
picture, so on a terminal that can draw, it is drawn the way the picture will be. Where there is no blurhash, the
Stand-in is a plain shaded fill in a theme role. Avatars never come with a blurhash, so theirs is always the fill. A
picture that cannot be fetched at all keeps its Stand-in, and its box stays. ADR-0016's "nothing about a picture is
ever reported as an error" still holds. The only change is that a failure no longer costs rows either.

A blur is held apart from the picture cache, and is not counted in its budgets. It is decoded from the post rather than
fetched, at 32 × 32 pixels whatever the box, because a blur has no detail to lose by being stretched. That is 4 KB a
blur, and `Blurs` remembers the 256 decoded most recently, so a megabyte at most. Counting blurs in the cache's budget
would let a screen of them push out the pictures they are waiting on. `Blurs` is made by the shell and handed to the
screens on their `Drawing`, the way the picture cache is, rather than held process-wide. On Kitty a blur sent to the
terminal is a picture the terminal holds like any other, so the view tells the terminal to forget it once it is no
longer near the page: when its picture replaces it, when it is scrolled away, or when there is no room left to draw the
page at all (ADR-0022).

The Stand-in has no caption or description inside it. That text stays on its own row above the box, where it is today.
Writing it inside the Stand-in as well would show it twice, with the second copy disappearing when the picture lands. It
would also break `HideDrawnCaption`: a reader who turned captions off would still see one in every box whose picture had
not arrived yet, so the setting would depend on network timing. A Stand-in with no text means the same thing whether
captions are on or off.

**A warned post reserves nothing until the reader asks past its warning.** It gets no box, no Stand-in and no `Wants`,
which is ADR-0016's amendment for #113 unchanged. When `x` is pressed, every box appears at its final size with its
Stand-in in it, so the post re-flows once, when the reader asked it to, and not again when the pixels arrive. Mastodon's
web client shows a blur behind a warning. This client does not, because a blurhash is a picture of the picture: its
colours and its rough composition are visible, and those are part of what the warning is hiding. Showing the box with no
blur in it was also rejected, because an empty box the size of a photograph still says that a photograph is there and
how it is shaped. The warning, or `⚠ Sensitive media`, is what a warned post shows.

**The picture cache has two tiers, each budgeted in bytes, each ordered by when a picture was last *wanted*, and what is
on screen is never let go of.**

- **Decoded pixels, about 64 MB.** These are downscaled when they are decoded to the largest box the current window
  could draw: the full content width by the post screen's thirty-two rows, in pixels. The content width is the inside of
  the panel posts are drawn in, 58 columns at an 80-column terminal, not the whole window's. This is what `Of` answers
  from.
- **Downloaded bytes, about 32 MB.** A decoded picture that is let go of can be decoded again from these without going
  back to the network, and so can a picture held at a size the window has since grown past.

Each tier lets go of the picture wanted longest ago. When a picture was fetched does not matter. Anything marked as on
screen in the latest frame is never let go of from the decoded tier, even when that tier is over budget. The screen is
small, so being over budget by that much is bounded. Kitty is still told to forget a picture only when its pixels leave
the decoded tier, so the terminal holds no more than the client does (ADR-0022), and a picture still held is not sent to
the terminal again. Pixels decoded again for a wider window replace the ones held, and that counts as the old pixels
leaving: the copy the terminal holds at the old size is forgotten, and the sharper one is sent in its place.

A picture waiting its turn to be decoded again from its file keeps that file, however far over budget the bytes tier is
while it waits. Letting go of the file would turn the decode it is waiting for into a fetch.

These budgets replace `MostHeld`. They are also the cap a future infinite-scroll timeline will rely on. A count was the
wrong unit for that, because it treats an avatar thumbnail and a full-width photograph as the same cost.

So the port changes too. `IPictures.Want(drawn)`, one picture at a time, becomes one call per frame. It carries the
pictures wanted, nearest first, with those on screen marked. Only a whole frame can say which pictures are on screen
now, which pictures used to be wanted and are not any more, and which order fetches should start in. `Of` stays a
lookup that fetches nothing, as ADR-0016 had it. The view wants about three screens ahead in the direction of travel
and one behind. Before the page has moved at all it wants two either side. Once it has moved, a frame drawn with the page
standing still keeps the reach of the way it last moved, rather than going back to two either side. Most frames drawn
with the page still are a picture landing, and reaching less far on each of them would abandon the fetches queued for the
far screen every time a near picture arrived. Fetches still start under the existing limit of four at a time, nearest
first. A queued fetch whose picture is no longer wanted is abandoned before it starts. A fetch already
in flight finishes and lands in the bytes tier.

**What the budgets were chosen against, and where to change them.** A decoded picture is four bytes a pixel. At the
decoder's present bounds (`PictureDecoder.LongestSide` × `TallestSide`, 1024 × 640) a full-size photograph is about
2.6 MB, and an avatar is tens of kilobytes. 64 MB therefore holds two dozen photographs at that size or more, plus every
avatar on several screens. That is more than the five screens the view wants at once, and far more than 32 pictures
held for a timeline of avatars. The bytes tier is sized against `Pictures`' own observation that a preview download is
tens of kilobytes, so 32 MB is several hundred previews, which is a morning's scroll or two destinations' worth.
Together they stay fixed however long the session runs.
Both numbers are starting points and were not measured. They are constants on `Pictures`, where `MostHeld` was. A window
large enough to raise the decoded size raises what each photograph costs, and the decoded budget is the first number to
revisit if that becomes common.

## What this supersedes

In ADR-0016:

- "The rows now follow from the picture's own proportions and the pixels-per-cell the protocol reports." The rows follow
  from the shape the instance reports, or 16:9, and the cell size. The caps and the narrowing at a cap stand.
- "Until they do there is no box at all — a picture still on its way is its description and nothing else, and the rows
  appear under it." The box is there from the first frame, with its Stand-in in it.
- The Consequences' "held to a bounded number of the most recently wanted". The bound is two byte budgets, and "most
  recently wanted" is now true. It was a description of what was meant, not of what `MostHeld` did, which went by
  fetch.
- "The box stays empty" for a picture that will not load. The box shows its Stand-in.

ADR-0022's "A box whose picture is not ready yet keeps the `▒` its rows already carry" is replaced by the Stand-in for the
same reason. "Fit, never squash" now relies on the fitting described above, not on the box already having the picture's
shape. Kitty's Unicode placeholders are still called placeholders in the code. The Stand-in is a different thing
(CONTEXT.md).

The `MostHeld` count, and its history (sixteen, then 32 for #77), are superseded completely.

## Considered options

- **Keep sizing from pixels and hide the shift.** Scroll anchoring, holding the line being read still while posts above
  it grow, or holding back a picture until its post scrolls out of view. Each one moves something else, and none of them
  helps the post the reader is actually looking at, which is the one whose picture arrives under the line they are
  reading. The shape is already on the wire, so there was nothing worth guessing.
- **A bigger count.** Raising `MostHeld` to 128 would have hidden most of the evictions on a typical screen. It would
  still drop by fetch order, could still drop what is on screen, and would put no bound on memory as photographs grow.
  The count went from sixteen to 32 for #77 with exactly this reasoning, and the next feature that added pictures to a
  screen would have needed it raised again.
- **A single tier.** Holding only decoded pixels at 96 MB would hold fewer pictures, because pixels cost ten to fifty
  times more than the file they came from, and every decoded picture let go of would cost a network round trip. Holding
  only the bytes would mean decoding again on every frame a picture is drawn. Two tiers make letting go of pixels cost a
  decode, not a fetch, which is also what lets a resize redraw sharp pictures without the network.
- **A blur behind a warning.** Rejected above. It is what Mastodon's web client does, and it shows part of what the
  warning hides.
- **A caption in the Stand-in.** Rejected above. It shows the same text twice, and it makes `HideDrawnCaption` depend on
  timing.

## Consequences

The trade-offs are space against shift, and memory against fetching again. A picture whose real shape differs from the
one reported, or one with no shape at all, wastes space around it, and this client accepts that so that the text never
jumps. The cache holds about 96 MB at most, plus whatever is on screen past budget, where it used to hold 32 pictures
of any size. This client accepts that so that scrolling back, and coming back to a destination, costs nothing.

`post show` and the timeline commands do not change. `PostMedia` and `LinkPreview` gain a shape and a blurhash, but the
CLI's text and `--json` output gain neither field, because nothing outside the TUI uses them yet.
