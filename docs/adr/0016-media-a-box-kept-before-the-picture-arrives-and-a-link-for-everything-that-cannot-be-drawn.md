# Media: a box kept before the picture arrives, and a link for everything that cannot be drawn

Story 49 asks the TUI to draw images inline — sixel, then the Kitty graphics protocol, then coloured cells — while
stories 50 and 51 keep the CLI to a link and alt text always, and keep video and audio to a link and a description on
both surfaces. The ladder itself turned out to be the easy part; four other things had to be decided to build it, and
they are what this records.

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
describe a file one way and the timeline another.

**A picture's box is kept before the picture arrives, and kept when it never does.** A post's rows say how many columns
and rows each of its pictures gets; whether there are pixels to put in the box is asked separately, on every frame. The
alternative — reserve rows only once the bytes are decoded — was rejected because a feed of ten posts would reflow ten
times as previews landed one by one, under a reader who is trying to read it. What that costs is a box of blank rows
where a picture could not be fetched at all, which is a real cost and the right one: the row underneath still says
`▒▒▒▒` and what the attachment shows, so nothing is lost but space, and space is what the reader would have spent
anyway if the fetch had worked.

It also keeps the interesting half of this testable with no terminal in the room, which is what ADR-0005 and ADR-0014
ask for. *A video is linked rather than drawn*, *four pictures share one band rather than taking four*, *a gutter moves
the boxes along with the text* are all facts about rows, and they are asserted. Pixels are not, and stay a manual smoke
test.

**Four attachments share one band, side by side.** Mastodon allows four, and four stacked boxes would bury the post
carrying them — a feed item would be mostly pictures and the text an afterthought. So the drawn attachments get one
band of rows between them, laid out left to right in the order their author attached them, with the descriptions in the
same order underneath. A feed item's band is six rows and a post screen's is twelve, because a reader who pressed enter
on a post has said which post they care about.

**Terminal.Gui draws the pixels, and the preference for sixel is expressed where its ladder reads it.** `ImageView`
already implements exactly the three rungs story 49 asks for, including the coloured-cell fallback that needs nothing
of the terminal, and the raster plumbing underneath it — retained image ids, dirty tracking, clipping a picture that is
half scrolled off — is not reachable from outside the library anyway. It tries Kitty first and sixel second, which is
story 49's order reversed. Rather than reimplement the ladder to swap two rungs of it, `RasterProtocol.PreferSixel`
sets Kitty support aside on a terminal that reports both, so that sixel is what is left; a terminal with only one of
them is untouched, and one with neither still draws cells.

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

## Consequences

Previews are fetched on their own `HttpClient`, not through the one every Mastodon call goes through: a file server
needs no token, counts against no rate limit, and a picture that will not load must not spend the retry budget a
timeline's fetch is relying on. They are asked for once each, held to a bounded number of the most recently wanted, and
scaled down as they are decoded — a terminal draws a few hundred pixels across, and a client that held a morning's
scrolling at full size would be holding a morning's scrolling.

Nothing about a picture is ever reported as an error. A fetch that fails, a file that will not decode, a format this
build has no decoder for: all of them are "no picture", the box stays empty, and the row that says what is attached is
what the reader has — which is the same thing the reader has on a terminal that cannot draw at all.
