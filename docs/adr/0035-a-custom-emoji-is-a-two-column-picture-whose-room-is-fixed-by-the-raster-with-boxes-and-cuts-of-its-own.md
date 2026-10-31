# A custom emoji is a two-column picture whose room is fixed by the Raster, with boxes and cuts of its own

An **instance** gives every post and account the **custom emoji** it uses, a **shortcode** with a picture behind it,
and a post's words, its warning, a display name, a **bio**, a **custom field** and a poll's options all carry them.
Wooly dropped them, so `:US_AZ:` and `:php:` showed as letters.

So a custom emoji is a picture two columns wide and one row tall, about square in a cell twice as tall as it is wide,
drawn in the line like a Unicode emoji and broken and clipped as one: never cut in half, dropped whole where it does not
fit. It is drawn wherever the **Raster** draws pictures at all: through Kitty's placeholders, Kitty through a box and
sixel alike. Its static picture is fetched through the picture cache like any other, one picture for every post that
uses it, keyed by its address. Where the Raster draws nothing, the shortcode stays as written, set apart so it reads as
a picture that was meant. A `:word:` the post or account does not list is the words someone wrote.

**The room is the Raster's, not the picture's.** On a terminal that draws, a custom emoji takes its two shaded columns
from the first frame, whether or not its picture has landed, and keeps them if the picture is refused or never comes.
Holding the shortcode until the picture landed would turn seven columns of `:US_AZ:` into two under the reader and
rewrap the paragraph; the meaning lost from one shaded emoji costs less than every line below it moving.

**On the box-drawn ways it has boxes and cuts of its own.** Sixel and Kitty through a box draw every picture through a
fixed pool of boxes (24, shared by avatars and attachments) and sixel keeps 32 encoded cuts, both sized for a few large
pictures. A timeline of decorated names wants dozens of small ones: in the shared pool, attachments would go missing
past the 24th box and the cuts encoded ahead would be thrown away every step. Custom emoji get a pool and a cut cache
of their own, sized from `ScrollMeasure` over a busy timeline, and a custom emoji with no box left is the shaded room it
had before it landed — only custom emoji ever go without, and nothing moves.

**What a page of them holds, and costs** (#527). Headless, Release, a 120×50 terminal on sixel, one wheel notch at a
time, over forty posts each writing sixteen of forty custom emoji between its words — busy 36-pixel pictures, as an
instance serves them — with a busy 853×640 photograph on every fourth (`ScrollMeasure.AWheelNotchOverSixelCustomEmoji`):

| Per notch | Median | Worst | Sent | Drawn through boxes, a frame |
|---|---|---|---|---|
| Before: every emoji its shade | 12–41 ms | 15–53 ms | ~420–545 KB | the 1–2 photographs |
| In the shared 24 boxes and 32 cuts | 21–59 ms | 36–123 ms | ~155–390 KB | 24 emoji, the photographs dropped |
| After: 64 boxes and 128 cuts their own | 18–33 ms | 31–98 ms | ~790–865 KB | 58 emoji (64 most), photographs |

Each row is the spread over its three runs — notches 16 ms apart down new rows, back to back down, back to back up —
on one machine, an Apple Silicon laptop, "before" once and the others twice; the worst case is noisy. A page down cost
75 ms before, 115–120 ms shared and 58–64 ms after. Shared, the emoji took every box and the photographs went undrawn
on most frames, which is the case for a pool of their own. A frame held 58 custom emoji on most notches and 64 at most,
so the pool is 64 boxes: a taller page of them leaves its last few shaded rather than an attachment undrawn. The cut
cache is 128, two pages of the most a page held: a custom emoji is the one cut wherever it is on the page, so the cache
holds what is on it, the rows encoded ahead and a page scrolled back to, at a few kilobytes each. Each custom emoji
drawn is a sixel of its own, about five kilobytes of a busy one, so the page sends ~320 KB more a step than its shade
did and a notch back to back costs ~6 ms more — the protocol (ADR-0023), and what drawing them there costs at all.

Static only: an animated custom emoji is drawn at its still, as nothing else in Wooly moves on its own. Where a line
cannot hold a picture at all — the **crumb**, the follows filter, the mention list — the shortcodes are taken out of
the name, as mention matching already did, and the handle stands in where nothing is left. The CLI writes the shortcode
as the instance wrote it, text and JSON alike.

## Considered options

- **Kitty's placeholders only.** A placeholder cell is text, so a custom emoji costs no box and nothing on a scroll,
  and none of the box-drawn limits come into it. Rejected: Windows Terminal draws only sixel, and its readers would
  have seen shortcodes where every other reader saw pictures.
- **Share the box pool and the cut cache, made larger.** One set of limits, but a screen of custom emoji would still
  decide whether an attachment is drawn, and one size cannot suit both 36-pixel pictures and full-width photographs.
- **The shortcode until the picture lands.** Says the most before it lands, and moves the paragraph when it does.

## Consequences

- Each scroll step on sixel sends every custom emoji on the page again, as it does every picture (ADR-0023), each a
  sequence of its own: ~320 KB more a step on the page measured above.
- The box-drawn ways build 88 boxes before anything is drawn, where they built 24.
- A custom emoji is not a **reference**: it is not walked to, not clicked open, and not shown on the **Picture screen**.
