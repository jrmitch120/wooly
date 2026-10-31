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

- Each scroll step on sixel sends every custom emoji on the page again, as it does every picture (ADR-0023). Each is a
  kilobyte or two, but each is a sequence of its own; `ScrollMeasure` before and after says what that costs.
- A custom emoji is not a **reference**: it is not walked to, not clicked open, and not shown on the **Picture screen**.
